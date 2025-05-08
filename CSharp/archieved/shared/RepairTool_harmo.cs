﻿using FarseerPhysics;
using FarseerPhysics.Dynamics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Barotrauma.Extensions;
using Barotrauma.MapCreatures.Behavior;


using Barotrauma;
using HarmonyLib;
using System.Globalization;
using System.Reflection;
using Barotrauma.Items.Components;

// This class cause net work problem
namespace BarotraumaDieHard
{
    class RepairToolMod : IAssemblyPlugin
    {
        public Harmony harmony;
		


		public void Initialize()
		{
		    harmony = new Harmony("RepairToolMod");

			
			
            var originalFixBody = typeof(RepairTool).GetMethod("FixBody", BindingFlags.NonPublic | BindingFlags.Instance);
			var prefixFixBody = typeof(RepairToolMod).GetMethod("FixBodyPrefix", BindingFlags.Public | BindingFlags.Static);

			harmony.Patch(originalFixBody, new HarmonyMethod(prefixFixBody), null);

        }

		public void OnLoadCompleted() { }
		public void PreInitPatching() { }

		public void Dispose()
		{
		  harmony.UnpatchSelf();
		  harmony = null;
		}
		
		public static bool FixBodyPrefix(Character user, Vector2 hitPosition, float deltaTime, float degreeOfSuccess, Body targetBody, RepairTool __instance, ref bool __result)
        {
            RepairTool _ = __instance;

            if (targetBody?.UserData == null) { __result = false; return false; }

            if (targetBody.UserData is Structure targetStructure)
            {
                if (targetStructure.IsPlatform) { __result = false; return false; }
                int sectionIndex = targetStructure.FindSectionIndex(ConvertUnits.ToDisplayUnits(_.pickedPosition));
                if (sectionIndex < 0) { __result = false; return false; }

                if (!_.fixableEntities.Contains("structure") && !_.fixableEntities.Contains(targetStructure.Prefab.Identifier)) { __result = true; return false; }
                if (_.nonFixableEntities.Contains(targetStructure.Prefab.Identifier) || _.nonFixableEntities.Any(t => targetStructure.Tags.Contains(t))) { __result = false; return false; }

                _.ApplyStatusEffectsOnTarget(user, deltaTime, ActionType.OnUse, structure: targetStructure);
                _.ApplyStatusEffectsOnTarget(user, deltaTime, ActionType.OnSuccess, structure: targetStructure);
                _.FixStructureProjSpecific(user, deltaTime, targetStructure, sectionIndex);

                float structureFixAmount = _.StructureFixAmount;
                if (structureFixAmount >= 0f)
                {
                    structureFixAmount *= 1 + user.GetStatValue(StatTypes.RepairToolStructureRepairMultiplier);
                    structureFixAmount *= 1 + _.item.GetQualityModifier(Quality.StatType.RepairToolStructureRepairMultiplier);
                }
                else
                {
                    structureFixAmount *= 1 + user.GetStatValue(StatTypes.RepairToolStructureDamageMultiplier);
                    structureFixAmount *= 1 + _.item.GetQualityModifier(Quality.StatType.RepairToolStructureDamageMultiplier);
                }

                var didLeak = targetStructure.SectionIsLeakingFromOutside(sectionIndex);
                

                targetStructure.AddDamage(sectionIndex, -structureFixAmount * degreeOfSuccess, user);

                if (didLeak && !targetStructure.SectionIsLeakingFromOutside(sectionIndex))
                {
                    user.CheckTalents(AbilityEffectType.OnRepairedOutsideLeak);
                }

                //if the next section is small enough, apply the effect to it as well
                //(to make it easier to fix a small "left-over" section)
                for (int i = -1; i < 2; i += 2)
                {
                    int nextSectionLength = targetStructure.SectionLength(sectionIndex + i);
                    if ((sectionIndex == 1 && i == -1) ||
                        (sectionIndex == targetStructure.SectionCount - 2 && i == 1) ||
                        (nextSectionLength > 0 && nextSectionLength < Structure.WallSectionSize * 0.3f))
                    {
                        //targetStructure.HighLightSection(sectionIndex + i);
                        targetStructure.AddDamage(sectionIndex + i, -structureFixAmount * degreeOfSuccess);
                    }
                }
                __result = true; 
                return false;
            }
            else if (targetBody.UserData is Voronoi2.VoronoiCell cell && cell.IsDestructible)
            {
                if (Level.Loaded?.ExtraWalls.Find(w => w.Body == cell.Body) is DestructibleLevelWall levelWall)
                {
                    levelWall.AddDamage(-_.LevelWallFixAmount * deltaTime, ConvertUnits.ToDisplayUnits(hitPosition));
                }
                __result = true;
                return false;
            }
            else if (targetBody.UserData is LevelObject levelObject && levelObject.Prefab.TakeLevelWallDamage)
            {
                levelObject.AddDamage(-_.LevelWallFixAmount, deltaTime, _.item);
                __result = true;                
                return false;
            }
            else if (targetBody.UserData is Character targetCharacter)
            {
                if (targetCharacter.Removed) { __result = false; return false; }
                targetCharacter.LastDamageSource = _.item;
                Limb closestLimb = null;
                float closestDist = float.MaxValue;
                foreach (Limb limb in targetCharacter.AnimController.Limbs)
                {
                    if (limb.Removed || limb.IgnoreCollisions || limb.Hidden || limb.IsSevered) { continue; }
                    float dist = Vector2.DistanceSquared(_.item.SimPosition, limb.SimPosition);
                    if (dist < closestDist)
                    {
                        closestLimb = limb;
                        closestDist = dist;
                    }
                }

                if (closestLimb != null && !MathUtils.NearlyEqual(_.TargetForce, 0.0f))
                {
                    Vector2 dir = closestLimb.WorldPosition - _.item.WorldPosition;
                    dir = dir.LengthSquared() < 0.0001f ? Vector2.UnitY : Vector2.Normalize(dir);
                    closestLimb.body.ApplyForce(dir * _.TargetForce, maxVelocity: 10.0f);
                }

                _.ApplyStatusEffectsOnTarget(user, deltaTime, ActionType.OnUse, character: targetCharacter, limb: closestLimb);
                _.ApplyStatusEffectsOnTarget(user, deltaTime, ActionType.OnSuccess, character: targetCharacter, limb: closestLimb);
                _.FixCharacterProjSpecific(user, deltaTime, targetCharacter);
                __result = true;
                return false;
            }
            else if (targetBody.UserData is Limb targetLimb)
            {
                if (targetLimb.character == null || targetLimb.character.Removed) { __result = false; return false; }

                if (!MathUtils.NearlyEqual(_.TargetForce, 0.0f))
                {
                    Vector2 dir = targetLimb.WorldPosition - _.item.WorldPosition;
                    dir = dir.LengthSquared() < 0.0001f ? Vector2.UnitY : Vector2.Normalize(dir);
                    targetLimb.body.ApplyForce(dir * _.TargetForce, maxVelocity: 10.0f);
                }

                targetLimb.character.LastDamageSource = _.item;
                _.ApplyStatusEffectsOnTarget(user, deltaTime, ActionType.OnUse, character: targetLimb.character, limb: targetLimb);
                _.ApplyStatusEffectsOnTarget(user, deltaTime, ActionType.OnSuccess, character: targetLimb.character, limb: targetLimb);
                _.FixCharacterProjSpecific(user, deltaTime, targetLimb.character);
                __result = true;
                return true;
            }
            else if (targetBody.UserData is Item targetItem)
            {
                if (!_.HitItems || !targetItem.IsInteractable(user)) { __result = false; return false; }

                var levelResource = targetItem.GetComponent<LevelResource>();
                if (levelResource != null && levelResource.Attached &&
                    levelResource.RequiredItems.Any() &&
                    levelResource.HasRequiredItems(user, addMessage: false))
                {
                    float addedDetachTime = deltaTime *
                        _.DeattachSpeed *
                        (1f + user.GetStatValue(StatTypes.RepairToolDeattachTimeMultiplier)) * 
                        (1f + _.item.GetQualityModifier(Quality.StatType.RepairToolDeattachTimeMultiplier));
                    levelResource.DeattachTimer += addedDetachTime;
#if CLIENT
                    if (targetItem.Prefab.ShowHealthBar && Character.Controlled != null &&
                        (user == Character.Controlled || Character.Controlled.CanSeeTarget(_.item)))
                    {
                        Character.Controlled.UpdateHUDProgressBar(
                            _,
                            targetItem.WorldPosition,
                            levelResource.DeattachTimer / levelResource.DeattachDuration,
                            GUIStyle.Red, GUIStyle.Green, "progressbar.deattaching");
                    }
#endif
                    _.FixItemProjSpecific(user, deltaTime, targetItem, showProgressBar: false);
                    __result = true;
                    return false;
                }
                
                if (!targetItem.Prefab.DamagedByRepairTools) { __result = false; return false; }

                if (_.HitBrokenDoors)
                {
                    if (targetItem.GetComponent<Door>() == null && targetItem.Condition <= 0) { __result = false; return false; }
                }
                else
                {
                    if (targetItem.Condition <= 0) { __result = false; return false; }
                }

                targetItem.IsHighlighted = true;
                
                _.ApplyStatusEffectsOnTarget(user, deltaTime, ActionType.OnUse, targetItem);
                _.ApplyStatusEffectsOnTarget(user, deltaTime, ActionType.OnSuccess, targetItem);

                if (targetItem.body != null && !MathUtils.NearlyEqual(_.TargetForce, 0.0f))
                {
                    Vector2 dir = targetItem.WorldPosition - _.item.WorldPosition;
                    dir = dir.LengthSquared() < 0.0001f ? Vector2.UnitY : Vector2.Normalize(dir);
                    targetItem.body.ApplyForce(dir * _.TargetForce, maxVelocity: 10.0f);
                }

                _.FixItemProjSpecific(user, deltaTime, targetItem, showProgressBar: true);
                __result = true;
                return true;
            }
            else if (targetBody.UserData is BallastFloraBranch branch)
            {
                if (branch.ParentBallastFlora is { } ballastFlora)
                {
                    ballastFlora.DamageBranch(branch, _.FireDamage * deltaTime, BallastFloraBehavior.AttackType.Fire, user);
                }
            }
            __result = false;
            return false;
        }


        /*
        if (targetBody.UserData is Structure targetStructure)
                    {
                // Retrieve the section index where the damage will be applied
                int sectionIndex = targetStructure.FindSectionIndex(ConvertUnits.ToDisplayUnits(__instance.pickedPosition));

                // Get the operator's distance to the target.
                float distance = Vector2.Distance(user.Position, targetBody.Position);

                // Get the damageMultiplier
                float damageMultiplier = CalculateDistanceMultiplier(distance, __instance);

                // Apply damage to the target structure's specific section
                targetStructure.AddDamage(sectionIndex, -__instance.StructureFixAmount * degreeOfSuccess * damageMultiplier, user);

                // Optional: Add debug message here if needed
                DebugConsole.NewMessage($"Applied {-__instance.StructureFixAmount * degreeOfSuccess * damageMultiplier} damage || should apply {-__instance.StructureFixAmount * degreeOfSuccess} to section {sectionIndex} of structure {targetStructure.Prefab.Identifier}.");
                    }
         */

        // Method to calculate a damage multiplier based on distance
        private static float CalculateDistanceMultiplier(float distance, RepairTool instance)
        {
            // Define a maximum distance for full damage
            float maxDistance = instance.Range; 
            float veryCloseDistance = 25.0f; // Define a threshold for "very close"
            
            // If the distance is very close, return half damage
            if (distance <= veryCloseDistance)
            {
                return 0.5f; // Half damage
            }
            
            // Calculate the damage multiplier based on distance
            if (distance <= maxDistance)
            {
                // Linear interpolation between half damage and triple damage
                float normalizedDistance = distance / maxDistance; // Scale between 0 and 1
                return 0.5f + (2.5f * normalizedDistance); // 0.5 at very close, 3.0 at maxDistance
            }
            
            // Return minimum multiplier if outside max distance
            return 0.1f; // Adjust this if needed
        }




		
        
	}
    
}