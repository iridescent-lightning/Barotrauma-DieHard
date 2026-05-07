﻿using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;


using Barotrauma;
using HarmonyLib;
using System.Globalization;
using System.Reflection;


namespace BarotraumaDieHard
{
	[HarmonyPatch(typeof(CharacterInventory))]
    public class CharacterInventoryPatch
    {
 
		
		//让燃料棒从容器取出时必须拿在手上，否则可以提着燃料收纳箱直接跳过夹取燃料棒的过程
		[HarmonyPatch("TryPutItemWithAutoEquipCheck")]
		[HarmonyPostfix]
		public static void TryPutItemWithAutoEquipCheck(CharacterInventory __instance, Item item, Character user, IEnumerable<InvSlotType> allowedSlots, bool createNetworkEvent, bool __result)
		{
			// 1. 基础安全检查：如果操作失败、物品不存在或用户是机器人，则不执行
			if (!__result || item == null || user == null || user.IsBot) 
			{ 
				return; 
			}
			

			// 2. 检查是否为目标燃料棒组件
			if (item.Components.Any(c => c is BarotraumaDieHard.Items.RadioactiveFuelRod))
			{
				// 3. 使用 Inventory 基类的 FindIndex 寻找物品当前所在的索引
				int slotIndex = __instance.FindIndex(item);
				if (slotIndex < 0 || slotIndex >= __instance.Capacity) return;

				// 4. 检查该索引对应的槽位类型是否为 Any
				if (__instance.SlotTypes[slotIndex] == InvSlotType.Any)
				{
					// 5. 定义手部槽位优先级列表
					var handSlots = new List<InvSlotType> 
					{ 
						InvSlotType.RightHand | InvSlotType.LeftHand, 
						InvSlotType.RightHand, 
						InvSlotType.LeftHand 
					};

					// 6. 强制尝试重定位到手部。这会触发 Equip 逻辑将物品抓在手里
					__instance.TryPutItem(item, user, handSlots, createNetworkEvent);
				}
			}
		}

	}
    
}