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




using Microsoft.Xna.Framework.Input;



using Barotrauma;
using HarmonyLib;
using System.Globalization;
using System.Reflection;


namespace BarotraumaDieHard
{
	[HarmonyPatch(typeof(Character))]
    public static class CharacterPatch_Weapon_Reload
    {
		private static Dictionary<ushort, ReloadInfo> reloadingCharacters = new Dictionary<ushort, ReloadInfo>();

		public class ReloadInfo
	{
		public float Timer;
		public Item TargetMag;
		public Item Weapon;
		public bool IsSingleShot; // 标记是否为单发逐次装填
	}
		


		[HarmonyPatch("Update")]
        [HarmonyPostfix]
		public static void UpdatePostfix(float deltaTime, Character __instance)
		{
		// --------------------------- Relaod Function-------------------------------
		//#if CLIENT commented out for better reading. Will make it work after finish the coding.
					// 只有在服务器或本地客户端运行逻辑，防止联机时的重复执行
			if (__instance == null || __instance.IsDead) return;

			ushort charID = __instance.ID;

			// 1. 处理倒计时逻辑
			if (reloadingCharacters.ContainsKey(charID))
			{
				var info = reloadingCharacters[charID];

				__instance.AnimController.StartAnimation(AnimController.Animation.UsingItem);
				info.Timer -= deltaTime;

				

				// 如果换弹时武器掉落或切换了，取消换弹
				if (__instance.Inventory.GetItemInLimbSlot(InvSlotType.RightHand) != info.Weapon)
				{
					reloadingCharacters.Remove(charID);
					return;
				}

				if (info.Timer <= 0)
				{
					// 时间到，执行装填
					ExecuteReload(__instance, info.TargetMag, info.Weapon);
					reloadingCharacters.Remove(charID); // 移除记录
				}
				return; // 换弹期间不接受新的换弹指令
			}

			// 2. 触发逻辑：仅对当前控制的角色检测按键
			if (Character.Controlled == __instance && PlayerInput.KeyHit(Keys.R))
			{
				Item heldItem = __instance.Inventory?.GetItemInLimbSlot(InvSlotType.RightHand);
				if (heldItem == null) return;

				var container = heldItem.GetComponent<ItemContainer>();
				if (container == null || container.Inventory == null) return;

				// 根据武器动态获取时间
        		float timeToReload = GetReloadTime(heldItem, __instance);

				// 寻找新弹匣
				var bestMag = __instance.Inventory.AllItems
					.Where(it => it != null && container.CanBeContained(it, index: 0))
					.OrderByDescending(it => it.Condition)
					.FirstOrDefault();

				if (bestMag != null)
				{
					// 弹出旧弹匣
					EjectOldMags(container, __instance);

					// 存入字典，开始独立计时
					reloadingCharacters[charID] = new ReloadInfo
					{
						Timer = timeToReload, // 设定装弹时间
						TargetMag = bestMag,
						Weapon = heldItem
					};
				}
			}
		//#endif
		}

		private static void EjectOldMags(ItemContainer container, Character character)
		{
			var itemsInSlot0 = container.Inventory.GetItemsAt(0);
			foreach (Item oldMag in itemsInSlot0.ToList())
			{
				// 尝试推入背包，失败则掉落
				if (!character.Inventory.TryPutItem(oldMag, user: character, oldMag.AllowedSlots))
				{
					oldMag.Drop(character);
				}
				
				var anim = character.AnimController;
					// 获取手
				var rightHand = anim.GetLimb(LimbType.RightHand);
				var rightArm = anim.GetLimb(LimbType.RightArm);
				
				// 目标位置（比如武器前方）
				Vector2 targetPos = character.WorldPosition + new Vector2(0f, 15f);

				Vector2 dir = targetPos - rightHand.WorldPosition;

				// 给一个力
				rightHand.body.ApplyForce(dir * 5f);
				//rightArm.body.ApplyForce(dir * 50f);
			}
		}

		private static void ExecuteReload(Character character, Item mag, Item weapon)
		{
			var container = weapon.GetComponent<ItemContainer>();
			if (container != null && mag != null && mag.ParentInventory == character.Inventory)
			{
				// 确保装填时，弹匣还在玩家身上
				container.Inventory.TryPutItem(mag, user: character);
				var anim = character.AnimController;
					// 获取手
				var rightHand = anim.GetLimb(LimbType.RightHand);
				var rightArm = anim.GetLimb(LimbType.RightArm);

				// 目标位置（比如武器前方）
				Vector2 targetPos = character.WorldPosition + new Vector2(0f, 20f);

				Vector2 dir = targetPos - rightHand.WorldPosition;

				// 给一个力
				rightHand.body.ApplyForce(dir * 5f);
				//rightArm.body.ApplyForce(dir * 50f);
				
			}
		}

		private static float GetReloadTime(Item item, Character character)
		{
			float baseTime = 0.5f; // 默认时间

			if (item.Prefab.Identifier == "flashlight") baseTime = 0f;

			if (item.Prefab.Identifier == "revolver") baseTime = 0.2f;
			
			if (item.HasTag("pistol")) baseTime = 0.3f;
			if (item.HasTag("longarm")) baseTime = 1f;

			// 获取角色的武器技能等级（0-100）
    		float skillLevel = character.GetSkillLevel("weapons");
    
    		// 技能越高，时间越短（最高减少 50% 时间）
    		float skillBonus = 1.0f - (skillLevel / 100f * 0.5f);
			
			return baseTime * skillBonus;
		}


	}
    
}