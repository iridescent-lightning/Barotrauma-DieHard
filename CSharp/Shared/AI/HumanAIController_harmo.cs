// This class is patched to make bots don't find diving suits in hulls
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
using BarotraumaDieHard.Items;



namespace BarotraumaDieHard.AI
{
    [HarmonyPatch(typeof(HumanAIController))]
    class HumanAIControllerPatch
    {
        [HarmonyPatch("NeedsDivingGear")]
        [HarmonyPrefix]
        // Let us make bots just use diving mask when are inside.
        public static bool NeedsDivingGearPrefix(Hull hull, out bool needsSuit, HumanAIController __instance, ref bool __result)
        {
            HumanAIController _ = __instance;

            needsSuit = false;
            bool needsAir = _.Character.NeedsAir && _.Character.CharacterHealth.OxygenLowResistance < 1;

            if (hull == null) // No hull means the character is outside, needs a suit for pressure protection
            {
                needsSuit = !_.Character.IsImmuneToPressure;
                __result = needsAir || needsSuit;
                return false;
            }

            // If inside a hull, only check if air is needed, no suit is required even if breached
            if (hull.WaterPercentage > 90 || hull.LethalPressure > 0 || hull.ConnectedGaps.Any(gap => !gap.IsRoomToRoom && gap.Open > 0.9f))
            {
                needsSuit = false;  // No suit required since they're inside a hull, despite gaps or lethal pressure
                __result = needsAir; // Only needs air, not a suit
                return false;
            }

            // If hull has water or low oxygen, check air needs
            if (hull.WaterPercentage > 60 || (hull.IsWetRoom && hull.WaterPercentage > 10) || hull.OxygenPercentage < HumanAIController.HULL_LOW_OXYGEN_PERCENTAGE + 1)
            {
                __result = needsAir;
                return false;
            }

            __result = false;
            return false;
        }

        [HarmonyPatch("CalculateHullSafety")]
        [HarmonyPatch(new Type[] { 
            typeof(Hull), 
            typeof(IEnumerable<Hull>), 
            typeof(Character), 
            typeof(bool), // ignoreWater
            typeof(bool), // ignoreOxygen
            typeof(bool), // ignoreFire
            typeof(bool), // ignoreEnemies
            typeof(bool)  // ignorePressureProtection
            })]
        [HarmonyPrefix]
        // A small change to include an additional radiation factor for hull safety check.
       public static bool CalculateHullSafetyPrefix(Hull hull, Character character, IEnumerable<Hull> visibleHulls,  bool ignoreWater, bool ignoreOxygen, bool ignoreFire, bool ignoreEnemies, HumanAIController __instance, ref float __result)
        {
            HumanAIController _ = __instance;

            bool isProtectedFromPressure = character.IsProtectedFromPressure;
            if (hull == null) { __result =  isProtectedFromPressure ? 100 : 0; return false;}
            if (hull.LethalPressure > 0 && !isProtectedFromPressure) { __result =  0; return false;}
            // Oxygen factor should be 1 with 70% oxygen or more and 0.1 when the oxygen level is 30% or lower.
            // With insufficient oxygen, the safety of the hull should be 39, all the other factors aside. So, just below the HULL_SAFETY_THRESHOLD.
            float oxygenFactor = ignoreOxygen ? 1 : MathHelper.Lerp((HumanAIController.HULL_SAFETY_THRESHOLD - 1) / 100, 1, MathUtils.InverseLerp(HumanAIController.HULL_LOW_OXYGEN_PERCENTAGE, 100 - HumanAIController.HULL_LOW_OXYGEN_PERCENTAGE, hull.OxygenPercentage));
            float waterFactor = 1;
            if (!ignoreWater)
            {
                if (visibleHulls != null)
                {
                    // Take the visible hulls into account too, because otherwise multi-hull rooms on several floors (with platforms) will yield unexpected results.
                    float relativeWaterVolume = visibleHulls.Sum(s => s.WaterVolume) / visibleHulls.Sum(s => s.Volume);
                    waterFactor = MathHelper.Lerp(1, HumanAIController.HULL_SAFETY_THRESHOLD / 2 / 100, relativeWaterVolume);
                }
                else
                {
                    float relativeWaterVolume = hull.WaterVolume / hull.Volume;
                    waterFactor = MathHelper.Lerp(1, HumanAIController.HULL_SAFETY_THRESHOLD / 2 / 100, relativeWaterVolume);
                }
            }
            if (!character.NeedsOxygen || character.CharacterHealth.OxygenLowResistance >= 1)
            {
                oxygenFactor = 1;
            }
            if (isProtectedFromPressure)
            {
                waterFactor = 1;
            }
            float fireFactor = 1;
            if (!ignoreFire)
            {
                static float calculateFire(Hull h) => h.FireSources.Count * 0.5f + h.FireSources.Sum(fs => fs.DamageRange) / h.Size.X;
                // Even the smallest fire reduces the safety by 50%
                float fire = visibleHulls == null ? calculateFire(hull) : visibleHulls.Sum(h => calculateFire(h));
                fireFactor = MathHelper.Lerp(1, 0, MathHelper.Clamp(fire, 0, 1));
            }
            float enemyFactor = 1;
            if (!ignoreEnemies)
            {
                int enemyCount = 0;                
                foreach (Character c in Character.CharacterList)
                {
                    if (visibleHulls == null)
                    {
                        if (c.CurrentHull != hull) { continue; }
                    }
                    else
                    {
                        if (!visibleHulls.Contains(c.CurrentHull)) { continue; }
                    }
                    if (HumanAIController.IsActive(c) && !HumanAIController.IsFriendly(character, c) && !c.IsHandcuffed)
                    {
                        enemyCount++;
                    }
                }
                // The hull safety decreases 90% per enemy up to 100% (TODO: test smaller percentages)
                enemyFactor = MathHelper.Lerp(1, 0, MathHelper.Clamp(enemyCount * 0.9f, 0, 1));
            }
            // DieHard Feature: Adding used fuelrods to DangerousFuelRods List<Item> DangerousFuelRods from RadioactiveFuelRod class.
            // This is an additional check if there's any fuel rod in the list in at the same hull with the character. If any, if no radiation protection suit, set safety to 0 no matter what.
            float radiationFactor = 1f;
            if (!HasRadiationSuit(character))
            {
                foreach (Item item in RadioactiveFuelRod.DangerousFuelRods)
                {
                    if (item.CurrentHull == hull) 
                    { 
                        radiationFactor = 0f;
                        break;
                    }
                }
            }
            float dangerousItemsFactor = 1f;
            foreach (Item item in Item.DangerousItems)
            {
                if (item.CurrentHull == hull) 
                { 
                    dangerousItemsFactor = 0;
                    break;
                }
            }
            float safety = oxygenFactor * waterFactor * fireFactor * enemyFactor * dangerousItemsFactor * radiationFactor; // Adding a radiationFactor to the end.
            __result = MathHelper.Clamp(safety * 100, 0, 100);


            return false;
        }


        // Method to handle equipping a radiation suit
        protected static void EquipRadiationSuit(Character character, HumanAIController humanAIController)
        {
            // Try to find the radiation suit in the bot's inventory first
            Item radiationSuit = character.Inventory.FindItem(it => it.HasTag("radiationsuit"), recursive: true);

            // If no radiation suit is found in inventory, search for one
            if (radiationSuit == null)
            {
                // Set up the gear tag for radiation suit
                Identifier gearTag = "radiationsuit".ToIdentifier();
                
                // Create the objective to get the item
                var getItemObjective = new AIObjectiveGetItem(character, gearTag, humanAIController.ObjectiveManager, equip: true)
                {
                    AllowStealing = false, // No stealing logic
                    EquipSlotType = InvSlotType.OuterClothes, // Equip in outer clothing slot
                    Wear = true
                };
                
                // Add this objective to the human AI controller
                humanAIController.ObjectiveManager.AddObjective(getItemObjective);
                character.Speak(TextManager.Get("dialog.bots.searchingforradiationsuit").Value, null, 0.0f, "dialog.bots.searchingforradiationsuit".ToIdentifier(), 10.0f);
            }
            else
            {
                // If the suit was found in the inventory, try equipping it
                bool success = character.Inventory.TryPutItem(radiationSuit, character, createNetworkEvent: true, ignoreCondition: true);
                if (success)
                {
                    character.Speak(TextManager.Get("dialog.bots.equipradiationsuit").Value, null, 0.0f, "dialog.bots.equipradiationsuit".ToIdentifier(), 10.0f);
                }
                else
                {
                    character.Speak(TextManager.Get("dialog.bots.cantfindsuit").Value, null, 0.0f, "dialog.bots.cantfindsuit".ToIdentifier(), 10.0f);
                }
            }
        }


        public static bool HasRadiationSuit(Character character)
        {
            // Search for the radiation suit in the character's inventory
            Item radiationSuit = null;
            foreach (Item item in character.Inventory.AllItems)
            {
                if (item.Prefab.Identifier == "radiationsuit".ToIdentifier())
                {
                    radiationSuit = item;
                    break; // Exit the loop once the radiation suit is found
                }
            }

            // If the radiation suit is found, check if it's equipped in the correct slot
            if (radiationSuit != null)
            {
                bool hasEquippedRadiationSuit = character.HasEquippedItem(radiationSuit, InvSlotType.OuterClothes | InvSlotType.InnerClothes);
                return hasEquippedRadiationSuit;
            }

            // Return false if no radiation suit is found or equipped
            return false;
        }

        // Move the windowed door logic in AIObjectiveCombat
        /*[HarmonyPatch("SpotEnemies")]
        [HarmonyPostfix]
        public static void Postfix(HumanAIController __instance)
        {
            // 如果没有进入战斗状态，说明没发现敌人
            if (!__instance.ObjectiveManager.HasActiveObjective<AIObjectiveCombat>()) return;

            Character aiChar = __instance.Character;
            // 获取当前战斗目标

            // 使用 CurrentObjective 获取基类，再进行类型转换
            if (__instance.ObjectiveManager.CurrentObjective is AIObjectiveCombat combatObjective)
            {
                Character target = combatObjective?.Enemy;

                if (target == null) return;

                // --- 关键判定：是否隔着门发现 ---
                // 我们发射一条简单的射线，不使用透视补丁的逻辑，看看能不能直接看到
                // 如果原版逻辑看死，但现在 AI 锁定了目标，说明是“透视”发现的
                var obstacle = Submarine.PickBody(
                    aiChar.SimPosition, 
                    target.SimPosition, 
                    collisionCategory: Physics.CollisionWall | Physics.CollisionItem);

                if (obstacle != null && obstacle.UserData is Item item && item.GetComponent<Barotrauma.Items.Components.Door>() != null)
                {
                    
                    // 发现目标和 AI 之间确实隔着一扇门
                    aiChar.Speak(TextManager.Get("dialog.bots.spottedenemybehinddoor").Value, ChatMessageType.Radio, 0.0f, "dialog.bots.spottedenemybehinddoor".ToIdentifier(), 10.0f
                    );
                    
                    // 既然发现了门后的敌人，强制 AI 跑过去（增加侵略性）
                    var gotoObjective = new AIObjectiveGoTo(target, aiChar, __instance.ObjectiveManager, repeat: false);
                    __instance.ObjectiveManager.AddObjective(gotoObjective);
                }
            }
        }*/
        
        //Make bandits to scan the envoirment in all submarine. Otherwise they won't actively "see" in outpost missions.
        [HarmonyPatch(typeof(HumanAIController), "Update")]
        [HarmonyPostfix]
        public static void UpdatePostfix(HumanAIController __instance, float deltaTime)
        {
            // 1. 基础状态过滤：死亡、瘫痪或已经处于战斗状态的不扫描
            if (__instance.Character.IsDead || __instance.Character.IsIncapacitated) return;
            
            // 2. 战斗状态过滤：如果 AI 已经在打人了，没必要每帧再跑一次视觉雷达
            if (__instance.ObjectiveManager.HasActiveObjective<AIObjectiveCombat>()) return;

            // 3. 频率控制：每 0.2 秒左右执行一次
            // 我们直接借用 deltaTime 进行倒计时
            // 注意：这里我们使用了一个简单的逻辑：每 12 帧左右执行一次 (假设 60fps)
            // 或者你可以尝试修改实例内部的 enemyCheckTimer
            
            // 更加稳妥的写法是手动控制一个时间步长：
            if (Timing.TotalTime % 0.2f < deltaTime) 
            {
                __instance.SpotEnemies();
            }
        }
        
    }
}