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
			else if (HullMod.GetGas(__instance.CurrentHull, "Temperature" ) > 323.15f)
			{
				__instance.CharacterHealth.ApplyAffliction(__instance.AnimController.MainLimb, AfflictionPrefab.Prefabs["burn"].Instantiate((HullMod.GetGas(__instance.CurrentHull, "Temperature") - 318.15f) * deltaTime * 2f));
			}
			else if (HullMod.GetGas(__instance.CurrentHull, "Temperature") > 293.15f)
			{
				__instance.CharacterHealth.ApplyAffliction(__instance.AnimController.MainLimb, AfflictionPrefab.Prefabs["coldwater"].Instantiate(-0.5f * deltaTime));
			}
			
			// --- 简化的压力伤害逻辑 ---

			// 直接获取当前房间的压力值（假设范围 0 到 100+）
			float airPressure = HullMod.GetGas(__instance.CurrentHull, "PressurizedAir");

			// 获取角色穿着
			Item innerCloth = _.Inventory.GetItemInLimbSlot(InvSlotType.InnerClothes);

			// 1. 轻微伤害阶段 (低压警告)
			// 比如压力超过 30 就会开始感到不适
			if (airPressure > 30f)
			{
				_.CharacterHealth.ApplyAffliction(
					targetLimb: _.AnimController.MainLimb, 
					new Affliction(pressurizedhullPrefab, 1f * deltaTime)); // 造成压力感官效果
			}

			// 2. 致命伤害阶段 (高压危险)
			// 比如压力超过 70 为致命线，如果没有 deepdiving 标签的衣服就会内爆
			float lethalThreshold = 70f;

			if (airPressure > lethalThreshold && (innerCloth == null || !innerCloth.HasTag("deepdiving"))) 
			{
				// 加重视觉/听觉压力效果
				_.CharacterHealth.ApplyAffliction(
						targetLimb: _.AnimController.MainLimb, 
						new Affliction(pressurizedhullPrefab, 3f * deltaTime));
				
				// 增加角色的压力计时器
				customPressureTimers[__instance] += 1 * deltaTime;

				// 持续暴露造成内脏损伤
				// 压力越高，伤害跳得越快
				float damageSeverity = (airPressure - lethalThreshold) / 20f; 
				_.CharacterHealth.ApplyAffliction(
						targetLimb: _.AnimController.MainLimb, 
						new Affliction(AfflictionPrefab.OrganDamage, damageSeverity * deltaTime));

				// 达到 15 秒计时直接内爆 (Implode)
				if (customPressureTimers[__instance] >= 15.0f)
				{
					if (GameMain.NetworkMember == null || !GameMain.NetworkMember.IsClient)
					{
						_.Implode();
						if (_.IsDead) { return; }
					}
				}
			}
			else
			{
				// 如果压力下降或者穿上了防护服，重置计时器
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