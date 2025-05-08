﻿using Barotrauma.Networking;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using System;
using Barotrauma.IO;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Items.Components;
using FarseerPhysics.Dynamics;
using Barotrauma.Extensions;
using System.Collections.Immutable;
using Barotrauma.Abilities;
using System.Diagnostics;
#if SERVER
using System.Text;
#endif


using Barotrauma;
using HarmonyLib;
using System.Globalization;
using System.Reflection;


namespace BarotraumaDieHard
{
    class CharacterMod : IAssemblyPlugin
    {
        public Harmony harmony;
		
        public static bool hasZoomed = false;
		public static AfflictionPrefab pressurizedhullPrefab;
		public void Initialize()
		{
		    harmony = new Harmony("CharacterMod");

			
			
            var originalUpdateOxygen = typeof(Character).GetMethod("UpdateOxygen", BindingFlags.NonPublic | BindingFlags.Instance);
            var postfixUpdateOxygen = new HarmonyMethod(typeof(CharacterMod).GetMethod(nameof(UpdateOxygenPostfix), BindingFlags.Public | BindingFlags.Static));
            harmony.Patch(originalUpdateOxygen, postfixUpdateOxygen, null);

			var originalUpdate = typeof(Character).GetMethod("Update", BindingFlags.Public | BindingFlags.Instance);
            var postfixUpdate = new HarmonyMethod(typeof(CharacterMod).GetMethod(nameof(UpdatePostfix), BindingFlags.Public | BindingFlags.Static));
            harmony.Patch(originalUpdate, postfixUpdate, null);


			var originalConstructor = typeof(Character).GetConstructor(
			BindingFlags.Instance | BindingFlags.NonPublic, 
			null, 
			new[] { typeof(CharacterPrefab), typeof(Vector2), typeof(string), typeof(CharacterInfo), typeof(ushort), typeof(bool), typeof(RagdollParams), typeof(bool) }, 
			null);

            var postfix = new HarmonyMethod(typeof(CharacterMod).GetMethod(nameof(CharacteConstructorPostfix)));
            harmony.Patch(originalConstructor, null, postfix);

			pressurizedhullPrefab = AfflictionPrefab.Prefabs["pressurizedhull"];
			
        }

		public void OnLoadCompleted() { }
		public void PreInitPatching() { }

		public void Dispose()
		{
		  harmony.UnpatchSelf();
		  harmony = null;
		}
		
		private static float escapedTime;
        private static float updateTimer = 1.0f;



		// Declare the dictionary at the class level
		private static Dictionary<Character, float> customPressureTimers = new Dictionary<Character, float>();


		public static void CharacteConstructorPostfix(CharacterPrefab prefab, Vector2 position, string seed, CharacterInfo characterInfo, ushort id, bool isRemotePlayer, RagdollParams ragdollParams, bool spawnInitialItems, Character __instance)
		{
			
			// Ensure the dictionary has an entry for the character
			if (!customPressureTimers.ContainsKey(__instance))
			{
				customPressureTimers[__instance] = 0.0f;
			}
		}
		
		public static void UpdateOxygenPostfix(Character __instance, float deltaTime)
		{
			
			Character _ = __instance;
			if (__instance == null) { return; }

			if (__instance.CurrentHull == null || __instance.Submarine == null || __instance.IsDead) { return; }
			
			if (!__instance.IsDead && __instance.UseHullOxygen)
			{
				HullMod.AddGas(__instance.CurrentHull, "CO2", 1f, deltaTime);
			
				if (HullMod.GetGas(__instance.CurrentHull, "CO2")  > 600f)
				{
					__instance.CharacterHealth.ApplyAffliction(__instance.AnimController.MainLimb, AfflictionPrefab.Prefabs["co_poisoning"].Instantiate(1f * deltaTime));
				}
				if (HullMod.GetGas(__instance.CurrentHull, "CO") > 400f)
				{
					__instance.CharacterHealth.ApplyAffliction(__instance.AnimController.MainLimb, AfflictionPrefab.Prefabs["co_poisoning"].Instantiate(5f * deltaTime));
				}
				if (HullMod.GetGas(__instance.CurrentHull, "CL") > 200f)
				{
					__instance.CharacterHealth.ApplyAffliction(__instance.AnimController.MainLimb, AfflictionPrefab.Prefabs["chlorine_poisoning"].Instantiate(0.1f * deltaTime));
				}
			}


			if (HullMod.GetGas(__instance.CurrentHull, "Temperature") < 278.15f)
			{
				__instance.CharacterHealth.ApplyAffliction(__instance.AnimController.MainLimb, AfflictionPrefab.Prefabs["coldwater"].Instantiate(0.3f * deltaTime));
			}
			else if (HullMod.GetGas(__instance.CurrentHull, "Temperature") > 323.15f)
			{
				__instance.CharacterHealth.ApplyAffliction(__instance.AnimController.MainLimb, AfflictionPrefab.Prefabs["burn"].Instantiate((HullMod.GetGas(__instance.CurrentHull, "Temperature") - 318.15f) * deltaTime * 2f));
			}
			else if (HullMod.GetGas(__instance.CurrentHull, "Temperature") > 293.15f)
			{
				__instance.CharacterHealth.ApplyAffliction(__instance.AnimController.MainLimb, AfflictionPrefab.Prefabs["coldwater"].Instantiate(-0.5f * deltaTime));
			}
			
			float normalAirPressureFactor = Math.Max(0, __instance.Submarine.RealWorldDepth) / 100f;
			float normalHullVolume = __instance.CurrentHull.Volume / 10000f;
			float normalHullPressure = normalHullVolume * normalAirPressureFactor + 1f;
			float airPressure = HullMod.GetGas(__instance.CurrentHull, "PressurizedAir");
			float hullPressureRatio = airPressure / normalHullPressure;

			//ApplyPressureForces(deltaTime, __instance, hullPressureRatio);

			//DebugConsole.NewMessage($"pressure timer: {customPressureTimers[__instance]}");
			//DebugConsole.NewMessage($"normalHullPressure: {normalHullPressure}");
			// DebugConsole.NewMessage($"hullPressureRatio: {hullPressureRatio}");
			//DebugConsole.NewMessage($"airPressure: {airPressure}");
			Item innerCloth = _.Inventory.GetItemInLimbSlot(InvSlotType.InnerClothes);
			if (hullPressureRatio > 2f)
			{
				_.CharacterHealth.ApplyAffliction(
						targetLimb: _.AnimController.MainLimb, 
						new Affliction(pressurizedhullPrefab, 2f * deltaTime));
			}

			if ( hullPressureRatio > 5f && !innerCloth.HasTag("deepdiving")) 
			{
				_.CharacterHealth.ApplyAffliction(
						targetLimb: _.AnimController.MainLimb, 
						new Affliction(pressurizedhullPrefab, 2f * deltaTime));
				
				// Increment the customPressureTimer for this character
				customPressureTimers[__instance] += 1 * deltaTime;

				if (customPressureTimers[__instance] > _.CharacterHealth.PressureKillDelay * 0.1f)
				{
					// Apply increasing amounts of organ damage. Use '-5' to make it start from 0. use 10 to make it slower
					_.CharacterHealth.ApplyAffliction(
						targetLimb: _.AnimController.MainLimb, 
						new Affliction(AfflictionPrefab.OrganDamage, ((hullPressureRatio - 5f) / 10f) * deltaTime));
				}

				if (customPressureTimers[__instance] >= 15.0f)
				{
					// Trigger implosion if needed
					if (GameMain.NetworkMember == null || !GameMain.NetworkMember.IsClient)
					{
						_.Implode();
						if (_.IsDead) { return; }
					}
				}
					
					
			}
			else
			{
				// Reset the customPressureTimer for this character
				if (customPressureTimers.ContainsKey(__instance))
				{
					customPressureTimers[__instance] = 0.0f;
				}
			}

			
			
			
		}



		public static void UpdatePostfix(float deltaTime, Character __instance)
		{
			Character _ = __instance;
			if (_.InWater)
			{
				ApplyFlowForces(deltaTime, _);
			}
		}



		public static void ApplyFlowForces(float deltaTime, Character character)
		{
			var allGaps = Gap.GapList; // Assume Gap.GapList holds all gaps in the game world.
			
			foreach (var gap in allGaps.Where(gap => gap.Open > 0 && !gap.IsRoomToRoom))
			{
				// Get the hull linked to the gap (assuming `gap.LinkedHull` exists).
				Hull linkedHull = gap.flowTargetHull;

				if (linkedHull == null) return;

				// Check if the linked hull exists and if it's close to full water.
				if (linkedHull != null && linkedHull.WaterPercentage >= 95f) // Assuming 95% is "close to full".
				{
					// DebugConsole.NewMessage($"Skipping force application due to high water level in hull: {linkedHull.WaterPercentage}");
					continue; // Skip applying force if water level is too high.
				}

				// Calculate the distance between the character and the gap.
				var distance = MathHelper.Max(Vector2.DistanceSquared(character.WorldPosition, gap.WorldPosition) / 1000, 1f);

				// Check if the gap is "nearby" within a certain range (e.g., 1000 units).
				if (distance < 2000f) // You can adjust the threshold as needed.
				{
					// Get the direction vector of the flow from the gap.
					Vector2 flowDirection = Vector2.Normalize(gap.LerpedFlowForce);
					if (flowDirection == Vector2.Zero) continue; // Skip if the flow direction is invalid.

					// Calculate the force applied based on the flow direction and distance.
					Vector2 force = (flowDirection * gap.LerpedFlowForce.Length() / (distance / 15)) * gap.Open * deltaTime;

					// Apply force to the character.
					if (force.LengthSquared() > 0.01f)
					{
						character.AnimController.Collider.FarseerBody.ApplyForce(force * 10); // Adjust this factor as needed.
					}

					// DebugConsole.NewMessage($"Character Distance: {distance} Force Applied: {force}");
				}
			}
		}

		public static void ApplyPressureForces(float deltaTime, Character character, float hullPressureRatio)
		{
			var allGaps = Gap.GapList; // Assume Gap.GapList holds all gaps in the game world.
			
			foreach (var gap in allGaps.Where(gap => gap.Open > 0 && gap.IsRoomToRoom))
			{

				// Get the hull linked to the gap (assuming `gap.LinkedHull` exists).
				Hull linkedHull = gap.flowTargetHull;

				if (linkedHull == null) return;

				// Check if the linked hull exists and if it's close to full water.
				if (linkedHull != null && hullPressureRatio < 10f) // Assuming 95% is "close to full".
				{
					// DebugConsole.NewMessage($"Skipping force application due to high water level in hull: {linkedHull.WaterPercentage}");
					continue; // Skip applying force if water level is too high.
				}
				
				// Calculate the distance between the character and the gap.
				var distance = MathHelper.Max(Vector2.DistanceSquared(character.WorldPosition, gap.WorldPosition) / 1000, 1f);

				// Check if the gap is "nearby" within a certain range (e.g., 1000 units).
				if (distance < 1000f) // You can adjust the threshold as needed.
				{
					
					// Get the direction vector of the flow from the gap.
					Vector2 flowDirection = Vector2.Normalize(gap.LerpedFlowForce);
					if (flowDirection == Vector2.Zero) continue; // Skip if the flow direction is invalid.
					DebugConsole.NewMessage("force");
					// Calculate the force applied based on the flow direction and distance.
					Vector2 force = (flowDirection * gap.LerpedFlowForce.Length() * 1000f / (distance / 15)) * gap.Open * deltaTime;

					// Apply force to the character.
					if (force.LengthSquared() > 0.01f)
					{
						character.AnimController.Collider.FarseerBody.ApplyForce(force * 10); // Adjust this factor as needed.
					}

					DebugConsole.NewMessage($"Character Distance: {distance} Force Applied: {force}");
				}
			}
		}


		public static void ClearPressureTimerDictionary()
		    {
			    customPressureTimers.Clear();
		    }

        
	}
    
}