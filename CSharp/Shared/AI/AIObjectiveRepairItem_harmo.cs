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
    [HarmonyPatch(typeof(AIObjectiveRepairItem))]
    class AIObjectiveRepairItemPatch
    {

        [HarmonyPatch("CheckPreviousCondition")]
        [HarmonyPrefix]
        public static bool CheckPreviousConditionPrefix(float deltaTime, AIObjectiveRepairItem __instance)
        {
            // Use this to get the localization name of the hull.
            string localizedRoomName = TextManager.Get(__instance.Item.CurrentHull.RoomName).Value;


            if (__instance.Item == null || __instance.Item.Removed) { return false; }
            __instance.conditionCheckTimer -= deltaTime;
            if (__instance.previousCondition > -1 && __instance.Item.Condition < __instance.previousCondition)
            {
                // If the current condition is less than the previous condition, we can't complete the task, so let's abandon it. The item is probably deteriorating at a greater speed than we can repair it.
                __instance.Abandon = true;
            }
            else if (__instance.Item.GetComponent<Reactor>() is Reactor reactorPowered && !MathUtils.NearlyEqual(reactorPowered.CurrPowerConsumption, 0.0f, 0.1f)) 
            {
                __instance.character.Speak(TextManager.Get("dialog.bots.cannotrepairpoweredreactor").Value, identifier: "bot_repair_fail", minDurationBetweenSimilar: 20.0f);
                __instance.Abandon = true;
                //DebugConsole.NewMessage("Avoid repaire powered reactor");
                
            }
            else if ((__instance.Item.GetComponent<Powered>() is Powered poweredDevice)) 
            {
                
                
                if (poweredDevice.powerIn != null && poweredDevice.powerIn.Grid != null && poweredDevice.powerIn.Grid.Power > 1f)
                {
                    __instance.character.Speak("A " + __instance.Item.Name + " in " + localizedRoomName + " is broken." + " But it's too dangerous to repair this while it's powered!", identifier: "bot_repair_fail", minDurationBetweenSimilar: 20.0f);
                    __instance.Abandon = true;
                }
                
            }
            else if (__instance.Item.HasTag("batterycellrecharger") && (__instance.Item.Condition < 20f)) // Additional logic for broken batteries.
            {
                __instance.character.Speak("A " + __instance.Item.Name + " in " + localizedRoomName + " is broken and releasing CL gas!", identifier: "toxic_battery_report", minDurationBetweenSimilar: 20.0f);
                __instance.previousCondition = __instance.Item.Condition;
            }
            else
            {
                // If the previous condition is not yet stored or if it's valid (greater or equal to current condition), save the condition for the next check here.
                __instance.previousCondition = __instance.Item.Condition;
            }

            return false;
        }


        [HarmonyPatch("FindRepairTool")]
        [HarmonyPrefix]
        public static bool FindRepairToolPrefix(AIObjectiveRepairItem __instance)
        {
            foreach (Repairable repairable in __instance.Item.Repairables)
            {
                foreach (var kvp in repairable.RequiredItems)
                {
                    foreach (RelatedItem requiredItem in kvp.Value)
                    {
                        __instance.character.Speak("I will need " + requiredItem.JoinedIdentifiers.ToString() + " to fix the " + __instance.Item.Name, identifier: "required items", minDurationBetweenSimilar: 10.0f);
                        
                        foreach (var item in __instance.character.Inventory.AllItems)
                        {
                            if (requiredItem.MatchesItem(item))
                            {
                                __instance.repairTool = item.GetComponent<RepairTool>();
                            }
                        }
                    }
                }
            }
            return false;
        }
    }
}