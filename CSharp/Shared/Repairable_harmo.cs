using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Barotrauma.Networking; // used by the server
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

using HarmonyLib;
using System.Reflection;
using System.Xml.Linq;

using Barotrauma;


namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(Repairable))]
    partial class RepairableDieHard
    {


        // 统一的安全检查：判断一个设备是否“带电”
        public static bool IsDeviceElectrified(Repairable __instance)
        {
            var item = __instance.item;
            
            // 1. 反应堆逻辑：只要在运行且有输出就带电
            if (item.GetComponent<Reactor>() is Reactor reactor)
            {
                return !MathUtils.NearlyEqual(reactor.CurrPowerConsumption, 0.0f, 0.1f);
            }

            // 2. 电池/电容逻辑：检查输入和输出电网
            if (item.GetComponent<PowerContainer>() is PowerContainer pc)
            {
                // 使用 ?. 确保即便拔线瞬间也不会 Crash
                bool hasInputPower = pc.powerIn?.Grid?.Power > 1.0f;
                bool hasOutputLoad = pc.powerOut?.Grid?.Load > 1.0f;
                return hasInputPower || hasOutputLoad;
            }

            // 4. 上游接线盒的状态
            var powered2 = item.GetComponent<Powered>();
            if (powered2?.powerIn != null && item.Prefab.Identifier != "junctionbox")
            {
                foreach (var connection in powered2.powerIn.Recipients)
                {
                    //DebugConsole.NewMessage($"{connection.ToString()}");
                    // 检查连接的另一端电网
                    if (connection.Grid != null && connection.Grid.Voltage > 0f) 
                    {
                        // 只要上游电网有电压，即便本设备 Condition 是 0，也认为带电（因为随时会通）
                        return connection.Grid.Voltage > 0f; 
                    }
                }
            }

            // 3. 普通用电设备（接线盒、氧气机等）
            if (item.GetComponent<Powered>() is Powered powered)
            {
                return powered.powerIn?.Grid != null && powered.powerIn.Grid.Power > 1.0f;
            }

            

            return false;
        }


        [HarmonyPatch("CheckCharacterSuccess")]
        [HarmonyPrefix]
        public static bool CheckCharacterSuccessPrefix(Character character, Item bestRepairItem, Repairable __instance, ref bool __result)
        {
            
            if (character == null) { __result = false; return false; }
            if (__instance.statusEffectLists == null) { __result = true; return false; }
            if (bestRepairItem != null && bestRepairItem.Prefab.CannotRepairFail) { __result = true; return false; }

            // 使用统一检查
            if (IsDeviceElectrified(__instance))
            {
                __instance.ApplyStatusEffects(ActionType.OnFailure, 1.0f, character);
#if CLIENT
                BarotraumaDieHard.CustomHintManager.DisplayHint("electricalrepair".ToIdentifier());
#endif

        if (character.AIController is HumanAIController humanAI)
                {
                    // 让 Bot 说话，解释为什么不修了
                    character.Speak(TextManager.Get("dialog.bot.safetywarning").Value, null, 0.0f, "safetywarning".ToIdentifier(), 10.0f);
                    
                    // 强制放弃当前的维修目标
                    // 这样 Bot 才会重新扫描周围，触发去关接线盒的逻辑
                    var currentObjective = humanAI.ObjectiveManager.GetCurrentObjective();
                    if (currentObjective != null)
                    {
                        currentObjective.Abandon = true; 
                    }
                }
                __result = false;
                return false;
            }

           
            bool success = Rand.Range(0.0f, 0.5f) < __instance.RepairDegreeOfSuccess(character, __instance.RequiredSkills);
            ActionType actionType = success ? ActionType.OnSuccess : ActionType.OnFailure;

            ApplyStatusEffectsAndCreateEntityEvent(__instance, actionType, character);
            ApplyStatusEffectsAndCreateEntityEvent(__instance, ActionType.OnUse, character);
            if (bestRepairItem != null && bestRepairItem.GetComponent<Holdable>() is Holdable holdable)
            {
                ApplyStatusEffectsAndCreateEntityEvent(holdable, actionType, character);
                ApplyStatusEffectsAndCreateEntityEvent(holdable, ActionType.OnUse, character);
            }
            static void ApplyStatusEffectsAndCreateEntityEvent(ItemComponent ic, ActionType actionType, Character character)
            {
                ic.ApplyStatusEffects(actionType, 1.0f, character);
                if (GameMain.NetworkMember is { IsServer: true } && ic.statusEffectLists != null && ic.statusEffectLists.ContainsKey(actionType))
                {
                    GameMain.NetworkMember.CreateEntityEvent(ic.Item, new Item.ApplyStatusEffectEventData(actionType, ic, character));
                }
            }
            __result = success;

            return false;
        }

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void UpdatePostfix(float deltaTime, Camera cam, Repairable __instance)
        {
            if (__instance.CurrentFixer == null) return;
            if (__instance.item.GetComponent<Powered>() is Powered powered) 
            {
                if (powered == null || powered.powerIn == null || powered.powerIn.Grid == null) // Check for devices that don't have powered component.
                {
                    return;
                }
                else if (powered.powerIn.Grid.Power > 1f)
                {
                    
                    __instance.ApplyStatusEffects(ActionType.OnFailure, 1.0f, __instance.CurrentFixer);
                    return;
                }
            }
        }

        [HarmonyPatch("RepairBoost")]
        [HarmonyPostfix]

        public static void RepairBoostPostfix(bool qteSuccess, Repairable __instance)
        {
            if (qteSuccess)
            {
                var requiredSkills = __instance.RequiredSkills;
                foreach (var skill in requiredSkills)
                {
                    float skillLevel = __instance.CurrentFixer.GetSkillLevel(skill.Identifier); // Get skill level using skill's identifier

                    // Only include skills that match required skills
                    if (skillLevel > skill.Level) // Check if the user has any level in the required skill
                    {
                        __instance.item.Condition += __instance.item.MaxCondition * 0.2f;
                        //DebugConsole.NewMessage($"Required skill: {skill.Identifier}, User skill level: {skillLevel}", Color.Yellow);
                    }
                    else
                    {
#if CLIENT   
                        BarotraumaDieHard.CustomHintManager.DisplayHint("qteinsufficientskilllevel".ToIdentifier());
#endif                        
                        __instance.ApplyStatusEffects(ActionType.OnFailure, 1.0f, __instance.CurrentFixer);
                    }
                }
                
            }
        }


        [HarmonyPatch("UpdateDeterioration")]
        [HarmonyPrefix]
        public static bool UpdateDeteriorationPrefix(float deltaTime, Repairable __instance)
        {
            Repairable _ = __instance;
            if (_.item.HasTag("junctionbox") || _.item.HasTag("engine") || _.item.HasTag("command") || _.item.HasTag("battery") || _.item.HasTag("supercapacitor") || _.item.HasTag("oxygengenerator")) return false;
            


            return true;            
        }


    }
}