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
    [HarmonyPatch(typeof(Turret))]
    partial class TurretDieHard
    {

        private static Dictionary<int, float> turretReloadValues = new Dictionary<int, float>();

		
		[HarmonyPatch("OnMapLoaded")]
        [HarmonyPostfix]
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
		
        [HarmonyPatch("Update")]
        [HarmonyPostfix]
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
                    // --- 新增安全检查 1: 检查 RequiredSkills 是否为空或没有条目 ---
                    if (requiredSkills == null || requiredSkills.Count == 0) 
                    {
                        // 如果是探照灯这种不需要技能的，直接返回，不执行后续逻辑
                        return; 
                    }

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


    /*[HarmonyPatch("TryLaunch")]
    [HarmonyPrefix]

        // 使用 Prefix，在 TryLaunch 执行前，悄悄把上一根绳索的自动断裂属性改成 false
    static void Prefix(Turret __instance)
    {
        try
        {
            // 利用反射获取私有字段 lastProjectile
            FieldInfo lastProjectileField = typeof(Turret).GetField("lastProjectile", BindingFlags.NonPublic | BindingFlags.Instance);
            if (lastProjectileField == null) return;

            var lastProjectile = lastProjectileField.GetValue(__instance) as Projectile;
            if (lastProjectile?.Item != null)
            {
                // 获取上一发弹药身上的 Rope 组件
                var rope = lastProjectile.Item.GetComponent<Rope>();
                if (rope != null)
                {
                    // 强制将其设为 false，这样官方代码的 if (is { SnapWhenWeaponFiredAgain: true }) 就不会成立
                    rope.SnapWhenWeaponFiredAgain = false;
                }
            }
        }
        catch
        {
            // 防止反射在某些极端版本下报错导致游戏崩溃
        }
    }*/
        
	}

    
    
}