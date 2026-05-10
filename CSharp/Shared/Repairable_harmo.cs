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


        [HarmonyPatch("CheckCharacterSuccess")]
        [HarmonyPrefix]
        public static bool CheckCharacterSuccessPrefix(Character character, Item bestRepairItem, Repairable __instance, ref bool __result)
        {
            
            if (character == null) { __result = false; return false; }

            if (__instance.statusEffectLists == null) { __result = true; return false; }

            if (bestRepairItem != null && bestRepairItem.Prefab.CannotRepairFail) { __result = true; return false; }
            
            // unpowered (electrical) items can be repaired without a risk of electrical shock
            // if (__instance.RequiredSkills.Any(s => s != null && s.Identifier == "electrical")). modding: inlcuding all powered items no matter requires e skill or other skills.


                if (__instance.item.GetComponent<PowerContainer>() is PowerContainer powerContainer) 
                {
                    //bug fix.使用？防止空引用崩溃
                    var gridOut = powerContainer?.powerOut?.Grid;
                    var gridIn = powerContainer?.powerIn?.Grid;

                    if (gridIn == null && gridOut == null)
                    {
                        __result = true;
                        return false;
                    }
                    //  || powerContainer.powerIn.Grid.Load > 0 this has to be removed to avoid supercapacitor crash the game. Now only the load is checked.
                    if (gridOut != null && gridOut.Load > 0)
                    {
                        __instance.ApplyStatusEffects(ActionType.OnFailure, 1.0f, character);
#if CLIENT   
                 
                        BarotraumaDieHard.CustomHintManager.DisplayHint("electricalrepair".ToIdentifier());
#endif
                        __result = false;
                        return false;
                    }

                    __result = true;
                    return false;

                }
                
                if (__instance.item.GetComponent<Reactor>() is Reactor reactor)
                {
                    if (MathUtils.NearlyEqual(reactor.CurrPowerConsumption, 0.0f, 0.1f)) { __result = true; return false; }
                }
                else if (__instance.item.GetComponent<Powered>() is Powered powered) 
                {
                    if (powered == null) // Check for devices that don't have powered component.
                    {
                        __result = true;
                        return false;
                    }
                    if (powered.powerIn == null || powered.powerIn.Grid == null) // Check for broken devices. // The first null check is necessary because door like items may have inherited Powered class but doesn't have any 'powrIn'.
                    {
                        //DebugConsole.NewMessage("no powerin or grid is null");
                        __result = true;
                        return false;
                    }
                    else if (powered.powerIn.Grid.Power <= 1f)
                    {
                        //DebugConsole.NewMessage("Power < 0");
                        __result = true;
                        return false;
                    }
                    
                    //DebugConsole.NewMessage(powered.powerIn.Grid.Power.ToString());
                    __instance.ApplyStatusEffects(ActionType.OnFailure, 1.0f, character);
#if CLIENT   
                 
            BarotraumaDieHard.CustomHintManager.DisplayHint("electricalrepair".ToIdentifier());
#endif
                    __result = false;
                    
                    return false;
                }

            // powered reactor will surely shock the repairer
            if (__instance.item.GetComponent<Reactor>() is Reactor reactorPowered && !MathUtils.NearlyEqual(reactorPowered.CurrPowerConsumption, 0.0f, 0.1f)) 
            {
                __instance.ApplyStatusEffects(ActionType.OnFailure, 1.0f, character);
                __result = false;
                return false; // Powered reactor will shock
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