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

		[HarmonyPatch("Unequip")]
        [HarmonyPostfix]
		public static void UnequipPostfix(Character character, Wearable __instance)
		{
			
			// 安全检查：只对当前玩家进行操作（防止网络同步时的逻辑混乱）
			// 并且确保脱下的物品确实是潜水服
			if (character == null || character != Character.Controlled) return;
			if (!__instance.Item.HasTag("deepdiving")) return;
			

			Item hat = character.Inventory.FindEquippedItemByTag("clothing");
			DebugConsole.NewMessage($"{hat}");
			
			// 1. 在玩家的背包里寻找带有 "cloth" 标签的物品
			// 我们排除掉当前刚刚脱下的这件物品
			Item clothesToWear = character.Inventory.AllItems.FirstOrDefault(it => 
			it != __instance.Item && 
			it.HasTag("clothing") && 
			// 核心：确保这个物品的组件配置里，允许放入 InnerClothes 槽位
			it.Components.OfType<Wearable>().Any(w => w.AllowedSlots.Contains(InvSlotType.InnerClothes)) &&
			it.ParentInventory == character.Inventory);

				
			
			if (clothesToWear != null)
			{
				DebugConsole.NewMessage("not null");
				// 2. 尝试将衣服穿上
				// TryPutItem 的参数解析：
				// clothesToWear: 目标衣服
				// index: 指定槽位（此处我们直接搜寻 OuterClothes 类型对应的索引）
				// allowSwapping: true
				// allowCombine: false
				// user: 执行者
				
				
				var innerClothes = character.Inventory.FindLimbSlot(InvSlotType.InnerClothes);
				var HandSlotIndex = character.Inventory.FindLimbSlot(InvSlotType.Head);
				
				
				
					DebugConsole.NewMessage(clothesToWear.ToString());
					//character.Inventory.TryPutItem(clothesToWear, HandSlotIndex, true, false, Character.Controlled, true, true);
					
					DebugConsole.NewMessage("invoke");
                    
					CoroutineManager.Invoke(() => 
                { 
                    character.Inventory.TryPutItem(clothesToWear, innerClothes, true, false, Character.Controlled, true, true);
                }, delay: 1.0f);

					
					
					#if CLIENT
					// 细节：播一个穿衣服的沙沙声
					SoundPlayer.PlaySound("clothing_rustle", character.WorldPosition);
					#endif
				
			}
		}
        
		
		
        
	}
    
}