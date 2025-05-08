using System;
using Barotrauma;
using Barotrauma.Networking;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Items.Components;
using Networking;



using Barotrauma.Extensions;
using System.Collections.Immutable;
using System.Globalization;
using Barotrauma.Abilities;

using HarmonyLib;
using Barotrauma.Items.Components;


namespace BarotraumaDieHard
{

    public class FabricatorDieHard : IAssemblyPlugin
    {
       public Harmony harmony;
        
        
        public void Initialize()
        {
            harmony = new Harmony("FabricatorDieHard");

            var originalUpdate = typeof(Fabricator).GetMethod("Update", BindingFlags.Public | BindingFlags.Instance);
            var postfixUpdate = typeof(FabricatorDieHard).GetMethod(nameof(UpdatePostfix), BindingFlags.Public | BindingFlags.Static);

            harmony.Patch(originalUpdate, new HarmonyMethod(postfixUpdate), null);


            var originalSelect = typeof(Fabricator).GetMethod("Select", BindingFlags.Public | BindingFlags.Instance);
            var postfixSelect = typeof(FabricatorDieHard).GetMethod(nameof(SelectPostfix), BindingFlags.Public | BindingFlags.Static);

            harmony.Patch(originalSelect, new HarmonyMethod(postfixSelect), null);
            
        }

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }


        public static void SelectPostfix(Character character, Fabricator __instance)
{
    if (character.IsBot)
    {
        DebugConsole.NewMessage(character.ToString());

        // Initialize AIObjectiveOperateItem with correct parameters
        AIObjectiveManager objectiveManager = character.AIObjectiveManager;  // Assuming character has an AIObjectiveManager
        Identifier option = "fabricate".ToIdentifier();  // Option can be specific to the operation you are performing
        bool requireEquip = false;  // Whether the bot needs to equip items to operate the fabricator

        // Create the AIObjectiveOperateItem with appropriate arguments
        AIObjectiveOperateItem objective = new AIObjectiveOperateItem(__instance, character, objectiveManager, option, requireEquip);

        // Call the method to have the bot operate the fabricator
        CrewAIOperate(character, objective, __instance);
    }
}

        public static void UpdatePostfix(float deltaTime, Camera cam, Fabricator __instance)
        {
            DebugConsole.NewMessage("Updates");
            
            
        }

        public static void CrewAIOperate(Character character, AIObjectiveOperateItem objective, Fabricator fabricator)
        {
            // If the fabricator is running, do nothing
            if (fabricator.State != Fabricator.FabricatorState.Stopped) return;

            if (character.SelectedItem == null)
            {
                // No item selected, bot will notify the player
                character.Speak("No item selected for fabrication", identifier: "fabricationitem".ToIdentifier());
                
            }

            // Refresh available ingredients for crafting
            fabricator.RefreshAvailableIngredients();

            // Check if crafting is possible
            if (fabricator.CanBeFabricated(fabricator.SelectedItem, fabricator.availableIngredients, character))
            {
                // Bot starts crafting and notifies the player
                character.Speak($"Starting to fabricate {fabricator.SelectedItem.DisplayName}", identifier: "fabricate".ToIdentifier());
                fabricator.StartFabricating(fabricator.SelectedItem, character);
            }
            else
            {
                // Notify that ingredients are missing
                character.Speak($"Not enough ingredients to fabricate {fabricator.SelectedItem.DisplayName}", identifier: "fabricationingredients".ToIdentifier());
            }

            
        }


    }
}