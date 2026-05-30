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
		[HarmonyPatch(new Type[] { typeof(Limb), typeof(Affliction), typeof(bool), typeof(bool) })]
		[HarmonyPrefix]
		// 使用 Prefix 在伤害结算前动态修改其倍率
		public static void Prefix(CharacterHealth __instance, Limb limb, Affliction newAffliction, bool allowStacking, bool recalculateVitality)
		{
			// 1. 基础安全检查
			if (__instance.Character == null || limb == null || newAffliction == null) return;
			
			// 2. 核心物种过滤：只允许普通人类(human)、人型画皮(humanhusk)和普通画皮(husk)
			string species = __instance.Character.SpeciesName.Value.ToLower();
			bool isValidTarget = species == "human" || species == "humanhusk" || species == "husk";
			if (!isValidTarget) return;

			// 3. 检查伤害类型是否是原版的枪伤 (gunshotwound)
			// （注意：如果防弹衣成功完全拦截了子弹，原版会将其转化为 blunttrauma 钝伤，能走到这里的说明未拦截或已被击穿）
			if (newAffliction.Prefab.Identifier == "gunshotwound")
			{
				LimbType type = limb.type;
				
				// 4. 检查是否穿着防弹衣
				bool hasBodyArmor = false;
				var inventory = __instance.Character.Inventory;
				if (inventory != null)
				{
					// 获取外衣槽位（防弹衣、潜水服等通常在 OuterClothes 槽）
					Item outerClothes = inventory.GetItemInLimbSlot(InvSlotType.OuterClothes);
					if (outerClothes != null)
					{
						// 检查物品的 Identifier 或者 Tags 是否包含防弹衣特征
						// 原版防弹衣是 "bodyarmor"，帮派防弹衣是 "banditarmor" 等，通常带有 "armor" 标签
						string itemIdentifier = outerClothes.Prefab.Identifier.Value.ToLower();
						if (itemIdentifier.Contains("armor") || outerClothes.HasTag("armor"))
						{
							hasBodyArmor = true;
						}
					}
				}

				// 5. 核心倍率逻辑
				if (!hasBodyArmor)
				{
					// 【情况 A：完全没穿防弹衣】 施加巨大的额外伤害乘数
					if (type == LimbType.Head)
					{
						newAffliction.Strength *= 5.0f;
					}
					else if (type == LimbType.Torso)
					{
						newAffliction.Strength *= 9.0f; 
					}
				}
				else
				{
					// 【情况 B：穿了防弹衣，但弹头打穿了防弹衣】 
					// 保持原样（不施加额外乘数），或者你也可以根据需要给一个微小的修正（例如 *= 1.0f）
					// DebugConsole.NewMessage($"[DieHard] 弹头击穿了 ({species}) 的防弹衣，未施加额外加成。");
				}
			}
		}
  	}
}
