﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Barotrauma.IO;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Barotrauma.Abilities;
using System.Collections.Immutable;


using Barotrauma;
using HarmonyLib;
using System.Globalization;
using System.Reflection;



namespace BarotraumaDieHard
{
	[HarmonyPatch(typeof(Wearable))]
    class WearablePatch
    {
        
		
        [HarmonyPatch("Equip")]
        [HarmonyPostfix]
		public static void EquipPostfix(Character character, Wearable __instance)
		{
			Wearable _ = __instance;
			
#if CLIENT
	 	if (_.item.HasTag("clothing"))
		{
			SoundPlayer.PlaySound("interactive_cloth_equip", _.item.WorldPosition, hullGuess: _.item.CurrentHull);
		}
			
#endif
		}


		/*public static void UnequipPostfix(Character character, Wearable __instance)
		{
			
			// 安全检查：只对当前玩家进行操作（防止网络同步时的逻辑混乱）
			// 并且确保脱下的物品确实是潜水服
			if (character == null || character != Character.Controlled) return;
			//if (!__instance.Item.HasTag("deepdiving")) return;

			// 检查玩家当前的外衣槽（OuterClothes）是否已经有东西了
			// 防止因为某种原因槽位被占用导致逻辑冲突
			
			var currentOuterClothes = character.Inventory.GetItemInLimbSlot(InvSlotType.OuterClothes);
			if (currentOuterClothes != null) return;
			
			// 1. 在玩家的背包里寻找带有 "cloth" 标签的物品
			// 我们排除掉当前刚刚脱下的这件物品
			Item clothesToWear = character.Inventory.AllItems.FirstOrDefault(it => 
				it != __instance.Item && 
				it.HasTag("clothing") && 
				it.ParentInventory == character.Inventory);

				
			
			if (clothesToWear != null)
			{
				// 2. 尝试将衣服穿上
				// TryPutItem 的参数解析：
				// clothesToWear: 目标衣服
				// index: 指定槽位（此处我们直接搜寻 OuterClothes 类型对应的索引）
				// allowSwapping: true
				// allowCombine: false
				// user: 执行者
				
				
				var outerClothes = character.Inventory.FindLimbSlot(InvSlotType.Bag);
				var HandSlotIndex = character.Inventory.FindLimbSlot(InvSlotType.RightHand);
				
				
					DebugConsole.NewMessage(clothesToWear.ToString());
					//character.Inventory.TryPutItem(clothesToWear, HandSlotIndex, true, false, Character.Controlled, true, true);
					character.Inventory.TryPutItem(clothesToWear, outerClothes, true, false, Character.Controlled, true, true);
					
					#if CLIENT
					// 细节：播一个穿衣服的沙沙声
					SoundPlayer.PlaySound("clothing_rustle", character.WorldPosition);
					#endif
				
			}
		}*/
        
		
		
        
	}
    
}