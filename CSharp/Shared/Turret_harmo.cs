﻿// This patch makes turret firing rate associated with character skill levels
using Barotrauma.Networking;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Barotrauma.Extensions;
using FarseerPhysics.Dynamics;
using System.Collections.Immutable;


using Barotrauma;
using Barotrauma.Items.Components;
using HarmonyLib;
using System.Globalization;
using System.Reflection;



namespace BarotraumaDieHard
{
    class TurretDieHard : IAssemblyPlugin
    {


        public Harmony harmony;
		
        private static Dictionary<int, float> turretReloadValues = new Dictionary<int, float>();

		public void Initialize()
		{
		    harmony = new Harmony("TurretDieHard");

			harmony.Patch(
                original: typeof(Turret).GetMethod("OnMapLoaded"),
                postfix: new HarmonyMethod(typeof(TurretDieHard).GetMethod(nameof(OnMapLoaded)))
            );
			
            var originalUpdate = typeof(Turret).GetMethod("Update", BindingFlags.Public | BindingFlags.Instance);
            var postfixUpdate = new HarmonyMethod(typeof(TurretDieHard).GetMethod(nameof(UpdatePostfix), BindingFlags.Public | BindingFlags.Static));
            harmony.Patch(originalUpdate, postfixUpdate, null);

            var originalLaunch = typeof(Turret).GetMethod("Launch", BindingFlags.NonPublic | BindingFlags.Instance);
            var postfixLaunch = new HarmonyMethod(typeof(TurretDieHard).GetMethod(nameof(LaunchPostfix), BindingFlags.Public | BindingFlags.Static));
            harmony.Patch(originalLaunch, postfixLaunch, null);
        }

		public void OnLoadCompleted() { }
		public void PreInitPatching() { }

		public void Dispose()
		{
		  harmony.UnpatchSelf();
		  harmony = null;
		}
		
		public static void OnMapLoaded(Turret __instance)
    {
		
        // Check if the item has a Turret component before storing the reload value
        if (__instance.Item.GetComponent<Turret>() != null)
        {
			
            // Store the reload value using the item ID as the key
            turretReloadValues[__instance.Item.ID] = __instance.Reload; // Store original reload value
        }
		else
		{
			DebugConsole.NewMessage("No turret found.");
		}
    }

	public static float GetOriginalReload(int itemID)
    {
        // Retrieve the stored reload value for a specific turret item
        return turretReloadValues.TryGetValue(itemID, out var reloadValue) ? reloadValue : 1f; // Default to 1 if not found
    }
		

		public static void UpdatePostfix(float deltaTime, Camera cam, Turret __instance)
{
    // Access the turret instance
    Turret _ = __instance;

    // Ensure user is valid
    if (_.user != null)
    {
        // Get the associated ItemComponent (assuming Turret is the correct type)
        ItemComponent itemComponent = _.Item.GetComponent<Turret>();  // Adjust if necessary

        // Proceed only if itemComponent is valid
        if (itemComponent != null)
        {
            // Call DegreeOfSuccess to get the success degree
            float successDegree = itemComponent.DegreeOfSuccess(_.user);

            // Access the RequiredSkills directly from the ItemComponent
            var requiredSkills = itemComponent.RequiredSkills;

            // Debug message for the degree of success
            //DebugConsole.NewMessage($"Degree of success: {successDegree}", Color.Green);

            // Variable to hold the user's skill level related to the required skills
            float totalUserSkillLevel = 0.0f;
            int matchingSkillCount = 0;

            // Iterate over the required skills to get the user's corresponding skill level
            foreach (var skill in requiredSkills)
            {
                float skillLevel = _.user.GetSkillLevel(skill.Identifier); // Get skill level using skill's identifier

                // Only include skills that match required skills
                if (skillLevel > 0) // Check if the user has any level in the required skill
                {
                    totalUserSkillLevel += skillLevel;
                    matchingSkillCount++;

                    // Debug message for required skill
                    //DebugConsole.NewMessage($"Required skill: {skill.Identifier}, User skill level: {skillLevel}", Color.Yellow);
                }
            }

            // Calculate the average skill level from matched required skills
            float averageSkillLevel = matchingSkillCount > 0 ? totalUserSkillLevel / matchingSkillCount : 0;
            //DebugConsole.NewMessage($"Average user skill: {averageSkillLevel}", Color.Yellow);

            // Calculate the average required skill level
            float averageRequiredSkillLevel = requiredSkills.Average(skill => skill.Level);
            //DebugConsole.NewMessage($"Average required skill: {averageRequiredSkillLevel}", Color.Yellow);
			
            // Get the weapon's base reload speed (you need to define how to access it)
            float baseReloadSpeed = GetOriginalReload(_.Item.ID);

			// Calculate skill difference
			float skillDiff = averageSkillLevel - averageRequiredSkillLevel;

			// Adjust the lerp factor
			float lerpFactor = Math.Clamp(skillDiff / averageRequiredSkillLevel, -1f, 1f); // Normalized to a 0-1 range

			// Lerp between the lower and upper bounds for reload speed
			float adjustedReloadSpeed = MathHelper.Lerp(baseReloadSpeed * 2.3f, baseReloadSpeed * 0.5f, lerpFactor);

			// Apply the new reload speed
			_.Reload = adjustedReloadSpeed;

            //DebugConsole.NewMessage($"New Reload Speed: {_.Reload}", Color.Yellow);


			//DebugConsole.NewMessage(_.reload.ToString());
			//DebugConsole.NewMessage(_.Reload.ToString());

			
			if (_.user?.Info != null )
            {
                _.user.Info.ApplySkillGain(
                    TagsDieHard.OnDeckWeaponsSkill,
                    SkillSettings.Current.SkillIncreasePerSecondWhenOperatingTurret * deltaTime);
            }
        }

        
            
        

    }
}

        public static void ClearReloadDictionary()
		{
			
			turretReloadValues.Clear();
    		//DebugConsole.NewMessage("Turret reload values cleared.", Color.Red);
		}

		public static void ResetOriginalReloadValue()
		{
			// Iterate through each stored reload value in the dictionary
			foreach (var turretEntry in turretReloadValues)
			{
				// Get the turret's item ID and its original reload value
				int itemID = turretEntry.Key;
				float originalReload = turretEntry.Value;

				// Find the turret by item ID
				var turretItem = Item.ItemList.FirstOrDefault(item => item.ID == itemID);

				// Check if the turret item exists and has a Turret component
				if (turretItem != null)
				{
					var turretComponent = turretItem.GetComponent<Turret>();
					if (turretComponent != null)
					{
						// Reassign the original reload value to the turret
						turretComponent.Reload = originalReload;
						//DebugConsole.NewMessage($"Reset turret {itemID}'s reload speed to {originalReload}", Color.Green);
					}
				}
			}
		}

        // Not useful
        public static void LaunchPostfix(Item projectile, Character user, float? launchRotation, float tinkeringStrength, Turret __instance)
        {
            
        }

        
        

        
	}
    
}