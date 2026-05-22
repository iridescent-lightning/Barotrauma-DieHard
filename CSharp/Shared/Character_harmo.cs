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

#if CLIENT
using Microsoft.Xna.Framework.Graphics;
#endif



using Barotrauma;
using HarmonyLib;
using System.Globalization;
using System.Reflection;


namespace BarotraumaDieHard
{
	[HarmonyPatch(typeof(Character))]
    partial class CharacterPatch
    {
		private static float escapedTime;
        private static float updateTimer = 1.0f;

		// 升级原本的字典，用于同时存储每个角色的【高压内爆计时器】和【降频节流计时器】
		public class CharacterDataCache
		{
			public float PressureTimer; // 暴露在高压下的致死计时器（原 customPressureTimers）
			public float ThrottleTimer; // 用来降低气体/温度结算频率的计时器
		}

		private static Dictionary<Character, CharacterDataCache> charDataCache = new Dictionary<Character, CharacterDataCache>();
		private const float GAS_UPDATE_INTERVAL = 0.25f; // 气体与温度降频：每秒仅检测 4 次

		[HarmonyPatch(MethodType.Constructor)]
        [HarmonyPatch(new Type[] { 
            typeof(CharacterPrefab), typeof(Vector2), typeof(string), 
            typeof(CharacterInfo), typeof(ushort), typeof(bool), 
            typeof(RagdollParams), typeof(bool) 
        })]
        [HarmonyPostfix]
		public static void CharacteConstructorPostfix(CharacterPrefab prefab, Vector2 position, string seed, CharacterInfo characterInfo, ushort id, bool isRemotePlayer, RagdollParams ragdollParams, bool spawnInitialItems, Character __instance)
		{
			if (!charDataCache.ContainsKey(__instance))
			{
				charDataCache[__instance] = new CharacterDataCache();
			}
		}

		[HarmonyPatch("UpdateOxygen")]
        [HarmonyPostfix]
		public static void UpdateOxygenPostfix(Character __instance, float deltaTime)
		{
			if (__instance == null || __instance.IsDead || __instance.CurrentHull == null || __instance.Submarine == null) { return; }

			// 1. 获取或创建属于该角色的独立计时器缓存
			if (!charDataCache.TryGetValue(__instance, out var cache))
			{
				cache = new CharacterDataCache();
				charDataCache[__instance] = cache;
			}

			// 2. 累加降频计时器
			cache.ThrottleTimer += deltaTime;
			if (cache.ThrottleTimer < GAS_UPDATE_INTERVAL) 
			{ 
				return; // 如果没到 0.25 秒，直接放行，不进行高能耗计算！
			}
			
			// 3. 到达检测周期，计算出这一段区间的累积时间步长 (代替原本的单帧 deltaTime)
			float accumulatedTime = cache.ThrottleTimer;
			cache.ThrottleTimer = 0f; // 重置节流计时器

			// --- 以下为高能耗核心逻辑，现在每秒只跑 4 次 ---

			Item headGear = __instance.Inventory.GetItemInLimbSlot(InvSlotType.Head);
			Item bodyGear = __instance.Inventory.GetItemInLimbSlot(InvSlotType.InnerClothes);

			bool breathGearOxygen = (headGear != null && headGear.HasTag("diving")) || (bodyGear != null && bodyGear.HasTag("diving"));

			if (!breathGearOxygen)
			{
				// 往房间注入气体
				HullMod.AddGas(__instance.CurrentHull, "CO2", 5f, accumulatedTime);
			
				// 气体毒性检测
				float co2 = HullMod.GetGas(__instance.CurrentHull, "CO2");
				if (co2 > 1000f)
				{
					__instance.CharacterHealth.ApplyAffliction(__instance.AnimController.MainLimb, AfflictionPrefab.Prefabs["co_poisoning"].Instantiate(1f * accumulatedTime));
				}
				
				float co = HullMod.GetGas(__instance.CurrentHull, "CO");
				if (co > 400f)
				{
					__instance.CharacterHealth.ApplyAffliction(__instance.AnimController.MainLimb, AfflictionPrefab.Prefabs["co_poisoning"].Instantiate(5f * accumulatedTime));
				}
				
				float cl = HullMod.GetGas(__instance.CurrentHull, "CL");
				if (cl > 200f)
				{
					__instance.CharacterHealth.ApplyAffliction(__instance.AnimController.MainLimb, AfflictionPrefab.Prefabs["chlorine_poisoning"].Instantiate(0.1f * accumulatedTime));
				}
			}

			// 温度伤害检测
			float currentTemp = HullMod.GetGas(__instance.CurrentHull, "Temperature");
			if (currentTemp < 278.15f)
			{
				__instance.CharacterHealth.ApplyAffliction(__instance.AnimController.MainLimb, AfflictionPrefab.Prefabs["coldwater"].Instantiate(0.3f * accumulatedTime));
			}
			else if (currentTemp > 323.15f)
			{
				__instance.CharacterHealth.ApplyAffliction(__instance.AnimController.MainLimb, AfflictionPrefab.Prefabs["burn"].Instantiate((currentTemp - 318.15f) * accumulatedTime / 4f));
			}
			else if (currentTemp > 293.15f)
			{
				__instance.CharacterHealth.ApplyAffliction(__instance.AnimController.MainLimb, AfflictionPrefab.Prefabs["coldwater"].Instantiate(-0.5f * accumulatedTime));
			}
			
			// --- 简化的压力伤害逻辑（同步引入降频步长） ---
			float airPressure = HullMod.GetGas(__instance.CurrentHull, "PressurizedAir");

			if (airPressure > 30f)
			{
				__instance.CharacterHealth.ApplyAffliction(
					targetLimb: __instance.AnimController.MainLimb, 
					new Affliction(GameSessionDieHard.pressurizedhullPrefab, 1f * accumulatedTime));
			}

			float lethalThreshold = 70f;
			if (airPressure > lethalThreshold && (bodyGear == null || !bodyGear.HasTag("deepdiving"))) 
			{
				__instance.CharacterHealth.ApplyAffliction(
						targetLimb: __instance.AnimController.MainLimb, 
						new Affliction(GameSessionDieHard.pressurizedhullPrefab, 3f * accumulatedTime));
				
				// 累加高压暴露时间
				cache.PressureTimer += accumulatedTime;

				float damageSeverity = (airPressure - lethalThreshold) / 20f; 
				__instance.CharacterHealth.ApplyAffliction(
						targetLimb: __instance.AnimController.MainLimb, 
						new Affliction(AfflictionPrefab.OrganDamage, damageSeverity * accumulatedTime));

				// 达到 15 秒致死线内爆
				if (cache.PressureTimer >= 15.0f)
				{
					if (GameMain.NetworkMember == null || !GameMain.NetworkMember.IsClient)
					{
						__instance.Implode();
						if (__instance.IsDead) { return; }
					}
				}
			}
			else
			{
				cache.PressureTimer = 0.0f; // 安全重置
			}
		}

		// 别忘了清理函数同步修改，防止切关卡内存泄漏
		public static void ClearPressureTimerDictionary()
		{
			charDataCache.Clear();
		}




		[HarmonyPatch("Update")]
        [HarmonyPostfix]
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
						character.AnimController.Collider.FarseerBody.ApplyForce(force * 50); // Adjust this factor as needed.
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

					//DebugConsole.NewMessage($"Character Distance: {distance} Force Applied: {force}");
				}
			}
		}


		// 存储怪物实体的切割进度 (0.0f - 100.0f)
        public static Dictionary<Character, float> CutProgress = new Dictionary<Character, float>();
        // 标记已经完成收集的怪物
        public static HashSet<Character> CompletedCharacters = new HashSet<Character>();
        // 1. 定义生物物种与掉落物的映射表
        private static readonly Dictionary<string, string> DropTable = new Dictionary<string, string>
        {
            { "Crawler", "crawlerhead" },
            { "Crawler_large", "lcrawlerhead" },
            { "Crawlerhusk", "hcrawlerhead" },
            { "Crawlerbroodmother_m", "broodmothereye" },
            { "Crawlerbroodmother", "broodmothereye" },
			{ "Hammerhead", "hammerheadfin" },
            { "Hammerheadmatriarch", "matriarchfin" },
            { "Hammerhead_m", "hammerheadfin" },
            { "Hammerhead_mNamed", "hammerheadfin" },
            { "Hammerheadgold_m", "ghammerheadfin" },
            { "Hammerheadgold", "ghammerheadfin" },
            { "Spineling_giant", "spineling_giant_spike" },
            { "Spineling", "smallspinelingspike" },
            { "Tigerthresher", "tigerthresherteeth" },
            { "Bonethresher", "bonethresherhead" },
            { "Charybdis", "Charybdistooth" },
            { "Mudraptor", "DHmudraptorshell" },
            { "Mudraptor_veteran", "vmudraptorshell" },
            { "Latcher", "latchereye" },
            { "Moloch_m", "molochfragment" },
            { "Moloch", "molochfragment" },
            { "Molochblack_m", "blackmolochfragment" },
            { "Molochblack", "blackmolochfragment" },
            { "Doomworm", "wormfang" },
            { "Endworm", "wormfang" },
            { "Watcher", "watcherspike" }
        };
		[HarmonyPatch("ApplyAttack")]
		[HarmonyPostfix]
		public static void Postfix(Character __instance, Character attacker, Attack attack)
            {
                // 基础校验：必须已死、有攻击者、未完成收集[cite: 2]
                if (attacker == null || !__instance.IsDead || CompletedCharacters.Contains(__instance)) return;

                // 检查是否持刀或者斧子
                var heldItem = attacker.HeldItems.FirstOrDefault(it => 
            it.Prefab.Identifier == "divingknife" || 
            it.Prefab.Identifier == "boardingaxe");
                if (heldItem == null) return;

                // 初始化或增加进度
                if (!CutProgress.ContainsKey(__instance)) CutProgress[__instance] = 0f;
                
                // 每次点击增加 20 进度 (5次切满)
                CutProgress[__instance] += 20f;
#if CLIENT
				float progressState = CutProgress[__instance] / 100f;

                // --- 关键部分：模仿 RepairTool 的原生进度条调用 ---
                // 参数说明：唯一ID, 位置, 进度(0-1), 起始颜色, 结束颜色, (可选)文本标签
                var progressBar = attacker.UpdateHUDProgressBar(
                    __instance.ID, 
                    __instance.DrawPosition, 
                    progressState, 
                    GUIStyle.Red, 
                    GUIStyle.Green,
                    TextManager.Get("progressbar.cutting").Value); // 使用切割专用的内置文本

                if (progressBar != null) 
                { 
                    progressBar.Size = new Vector2(60.0f, 20.0f); // 匹配 RepairTool 的尺寸
                }
                // -------------------------------------------------------
#endif
                // 检查是否触发掉落
                if (CutProgress[__instance] >= 100f)
                {
                    string speciesName = __instance.SpeciesName.Value;
                    if (DropTable.TryGetValue(speciesName, out string dropIdentifier))
                    {
                        // 执行掉落逻辑[cite: 2]
                        Entity.Spawner.AddItemToSpawnQueue(ItemPrefab.Prefabs[dropIdentifier], __instance.WorldPosition);
#if CLIENT
                        GameMain.ParticleManager.CreateParticle("organeruption", __instance.WorldPosition, Vector2.Zero);
                        GameMain.ParticleManager.CreateParticle("heavygib", __instance.WorldPosition, Vector2.Zero);
#endif                        
                        // 标记完成并清理进度
                        CompletedCharacters.Add(__instance);
                        CutProgress.Remove(__instance);
                    }
                }
            }

			//每当角色被移除时，从字典中删除，防止内存泄漏
			[HarmonyPatch("Despawn")]
			[HarmonyPrefix]
            public static void Prefix(Character __instance)
            {
                // 当角色消失时，清理内存
                CutProgress.Remove(__instance);
                CompletedCharacters.Remove(__instance);
            }
        

		// Make AI can see through Windowed doors.
		// In Door harmony we adjusted the raycast logic to make the ray always passthrough the door
		[HarmonyPatch("CanSeeTarget")]
		[HarmonyPrefix]
		static void Prefix(ref bool seeThroughWindows)
		{
			seeThroughWindows = true;
		}


	}
    
}