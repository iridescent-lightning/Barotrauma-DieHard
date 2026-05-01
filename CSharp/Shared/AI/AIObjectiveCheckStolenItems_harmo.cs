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
    [HarmonyPatch(typeof(AIObjectiveCheckStolenItems))]
    class AIObjectiveCheckStolenItemsPatch
    {

        [HarmonyPatch("Inspect")]
        [HarmonyPostfix]
        public static void InspectPostfix(float deltaTime, AIObjectiveCheckStolenItems __instance)
        {
            if (__instance.character.SelectedCharacter == null) return; // A must have or the game will crash on null


            var playerInventory = __instance.character.SelectedCharacter.Inventory;
            Item gun = playerInventory.FindItemByTag("gun", recursive: true);
            
            if (gun != null)
            {
                __instance.character.Speak(TextManager.Get("dialogcheckfirearms").Value);
                __instance.currentState = AIObjectiveCheckStolenItems.State.Warn; // Change state to warn
                
            }

            //__instance.character.DeselectCharacter();


        }
        [HarmonyPatch("Warn")]
        [HarmonyPostfix]
        private static void WarnPostfix(float deltaTime, AIObjectiveCheckStolenItems __instance)
        {
            /*if (__instance.warnTimer > 0.0f)
            {
                __instance.warnTimer -= deltaTime;
                return;
            }
            var gunItemsOnCharacter = __instance.stolenItems.Where(it => it.GetRootInventoryOwner() == __instance.Target && it.HasTag("gun"));

            if (gunItemsOnCharacter.Any())
            {
                __instance.character.Speak(TextManager.Get(__instance.character.IsCriminal ? "dialogcheckstolenitems.arrest.criminal" : "dialogcheckfirearms.Drop").Value);
                DebugConsole.NewMessage("start");
                __instance.Arrest(abortWhenItemsDropped: true, allowHoldFire: true);
                foreach (var stolenItem in gunItemsOnCharacter)
                {
                    HumanAIController.ApplyStealingReputationLoss(stolenItem);
                }
            }
            else
            {
                __instance.character.Speak(TextManager.Get("dialogcheckstolenitems.comply").Value);
            }
            foreach (var item in __instance.stolenItems)
            {
                // Use this method to fix the null reference for non-static method. HumanAIController for example.
                var aiController = __instance.character.AIController as HumanAIController;
                if (aiController != null)
                {
                    aiController.ObjectiveManager.AddObjective(new AIObjectiveGetItem(__instance.character, item, aiController.objectiveManager, equip: false)
                    {
                        BasePriority = 10
                    });
                }
            }                
            __instance.currentState = AIObjectiveCheckStolenItems.State.Done;
            __instance.IsCompleted = true;
        }*/

    }
}
}