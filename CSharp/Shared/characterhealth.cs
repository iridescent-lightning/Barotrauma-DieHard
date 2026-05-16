using System;
using System.Reflection;
using Barotrauma;
using HarmonyLib;
using Microsoft.Xna.Framework;


using Barotrauma.Abilities;
using Barotrauma.Extensions;
using Barotrauma.Networking;


using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace BarotraumaDieHard
{
	[HarmonyPatch(typeof(CharacterHealth))]
  public partial class CharacterHealthPatch
  {
	[HarmonyPatch("ApplyDamage")]
	[HarmonyPostfix]
    public static void ApplyDamage(Limb hitLimb, AttackResult attackResult, bool allowStacking, CharacterHealth __instance)
    {

		CharacterHealth _ = __instance;

		var leftHand = _.Character.Inventory.GetItemInLimbSlot(InvSlotType.LeftHand);
		var RightHand = _.Character.Inventory.GetItemInLimbSlot(InvSlotType.RightHand);

		foreach (Affliction newAffliction in attackResult.Afflictions)
		{

			if (!_.Character.IsHuman || hitLimb.type == null) {return;}
			
			
			if (newAffliction.Prefab.LimbSpecific)
			{
				_.AddLimbAffliction(hitLimb, newAffliction, allowStacking);
				
				if (newAffliction.Identifier == "blunttrauma" && hitLimb.type == LimbType.LeftArm)
				{
					if (leftHand != null)
					{
						leftHand.Drop(_.Character);
					}
				}
				else if (newAffliction.Identifier == "blunttrauma" && hitLimb.type == LimbType.RightArm)//type is lowercase
				{
					if (RightHand != null)
					{
						RightHand.Drop(_.Character);
					}
				}
			}
      }
		// Sever legs or waist effect
	  if (_.Character.AnimController is HumanoidAnimController humanAnimController) // cast type
		{
			// Severed legs cause the player to crouch or fall down
			foreach (Limb limb in humanAnimController.Limbs)
			{
				if (limb.IsSevered && (limb.type == LimbType.LeftLeg || limb.type == LimbType.RightLeg || limb.type == LimbType.LeftThigh || limb.type == LimbType.RightThigh || limb.type == LimbType.Waist))
				{
					// Force the crouching state. This only works controlled character
					/*humanAnimController.ForceSelectAnimationType = AnimationType.Crouch; 
					humanAnimController.Crouching = true;
					_.Character.SetInput(InputType.Crouch, hit: false, held: true);*/

					// Load the crouch animation
					_.Character.SetStun(1);
					AnimationParams animParams;

					humanAnimController.TryLoadAnimation(AnimationType.Run, "HumanRunCrawl_LegSevered", out animParams, true);
					humanAnimController.TryLoadAnimation(AnimationType.Walk, "HumanWalkCrawl_LegSevered", out animParams, true);
					
					break; // Exit the loop once a severed limb is found
				}
			}
		}
    }
	
		[HarmonyPatch("Update")]
		[HarmonyPostfix]
		public static void Update(CharacterHealth __instance, float deltaTime)
        {
			CharacterHealth _ = __instance;

			// Defualt Character Status Effect Attributes
			if (_.Character.IsHuman && _.Character.InWater)
			{
				_.ApplyAffliction(_.Character.AnimController.MainLimb, AfflictionPrefab.Prefabs["coldwater"].Instantiate(0.3f * deltaTime));
			}
			else if (_.Character.IsHuman && _.Character.AnimController.IsMovingFast)//IsMovingFast doesn't detect water
			{
				_.ApplyAffliction(_.Character.AnimController.MainLimb, AfflictionPrefab.Prefabs["fatigue"].Instantiate(10f * deltaTime));
			}
			
			if (_.Character.IsHuman && !_.Character.IsDead && _.Character.CurrentHull != null)
			{
				_.Character.PressureProtection= 4500.0f;
			}
		}




		[HarmonyPatch("AddLimbAffliction")]
		[HarmonyPatch( new Type[] {typeof(Limb), typeof(Affliction), typeof(bool), typeof(bool) })]
		[HarmonyPrefix]
		// 使用 Prefix 在伤害结算前动态修改其倍率
        public static void Prefix(CharacterHealth __instance, Limb limb, Affliction newAffliction, bool allowStacking, bool recalculateVitality)
        {
            // 1. 基础安全检查
			if (__instance.Character == null || limb == null || newAffliction == null) return;
			
			// 2. 核心物种过滤：只允许普通人类(human)、人型画皮(humanhusk)和普通画皮(husk)
			// Barotrauma 的 SpeciesName 默认是小写形式的 Identifier，使用 .Value 获取纯文本
			string species = __instance.Character.SpeciesName.Value.ToLower();
			
			bool isValidTarget = species == "human" || species == "humanhusk" || species == "husk";
			if (!isValidTarget) return;

			// 3. 检查伤害类型是否是原版的枪伤 (gunshotwound)
			if (newAffliction.Prefab.Identifier == "gunshotwound")
			{
				//DebugConsole.NewMessage($"[DieHard] 捕获合法的目标枪伤 ({species}): {limb.type}");
				
				// 获取当前命中的肢体类型
				LimbType type = limb.type;
				
				// 4. 核心倍率逻辑
				if (type == LimbType.Head)
				{
					//DebugConsole.NewMessage("amplifying");
					newAffliction.Strength *= 5.0f;
				}
				else if (type == LimbType.Torso)
				{
					//DebugConsole.NewMessage("amplifyingT");
					newAffliction.Strength *= 9.0f;
				}
			}
        }
  	}
}
