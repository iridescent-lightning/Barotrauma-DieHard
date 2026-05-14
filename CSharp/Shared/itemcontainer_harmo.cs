
using System.Reflection;
using Barotrauma;
using HarmonyLib;
using Microsoft.Xna.Framework;


using Barotrauma.Abilities;
using Barotrauma.Extensions;
using Barotrauma.Networking;

using Barotrauma.Items.Components;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

using FarseerPhysics;
using System;
using System.Collections.Immutable;


namespace BarotraumaDieHard
{
	[HarmonyPatch(typeof(ItemContainer))]
	class ItemContainerPatch
	{
		//set up the system time
		private static DateTime lastUpdateTime = DateTime.MinValue;
		private static readonly TimeSpan updateInterval = TimeSpan.FromSeconds(0.5f); // 0.5-second interval

        [HarmonyPatch("Select")]
        [HarmonyPrefix]	
		public static bool SelectPrefix(Character character, ItemContainer __instance)
		{
			ItemContainer _ = __instance;
			
			if (!_.AllowAccess) { return false; }
				if (_.item.Container != null) { return false; }
				if (_.AccessOnlyWhenBroken)
				{
					if (_.item.Condition > 0)
					{
						return false;
					}
				}
				if (_.AutoInteractWithContained && character.SelectedItem == null)
				{
					foreach (Item contained in _.Inventory.AllItems)
					{
						if (contained.TryInteract(character))
						{
							character.FocusedItem = contained;
							return false;
						}
					}
				}

				// 先播放声音（无论后续是否存入物品）
    PlayContainerSound(_);  // 提取成独立方法

				/*// 1. 基础检查：确保是玩家在交互，且交互者有库存
				if (character == null || character.Inventory == null) return true;

				// 2. 获取玩家当前手里拿着的物品 (通常是右手或左手选中的物品)
				Item heldItem = character.Inventory.GetItemInLimbSlot(InvSlotType.RightHand);
				
				// 如果手里没东西，走原逻辑（比如打开柜子看一眼）
				if (heldItem == null) return true;

				// 3. 检查容器是否能放下这个物品
				// __instance.Inventory 是容器自身的库存
				if (__instance.Inventory.CanBePut(heldItem) && (__instance.Item.HasTag("weaponholder") || __instance.Item.HasTag("extinguisherholder")))
				{
					// 4. 执行存入动作
					bool success = __instance.Inventory.TryPutItem(heldItem, character);

					if (success)
					{
#if CLIENT
						// 播放一个存入的声音，增加反馈感
						SoundPlayer.PlayUISound(GUISoundType.PickItem);
#endif
						
						// 重要：返回 false 以拦截原逻辑，这样就不会弹出容器 UI 面板了
						return false;
					}
				}*/
				

				
				var abilityItem = new AbilityItemContainer(_.item);
				character.CheckTalents(AbilityEffectType.OnOpenItemContainer, abilityItem);

				if (_.item.ParentInventory?.Owner == character)
				{
					
					//can't select ItemContainers in the character's inventory (the inventory is drawn by hovering the cursor over the inventory slot, not as a GUIFrame)
					return false;
				}
				else
				{
					/*In your SelectPrefix method, it looks like you want to execute the original Select method of the ItemContainer class unless certain conditions are met. With Harmony, the return value of the prefix method determines whether the original method is called. If the prefix method returns true, Harmony proceeds to call the original method. If it returns false, the original method is skipped.*/
					return true;
				// return base.Select(character);
				}

				
				
				return false;
		}







		// 提取声音播放为独立方法，避免重复代码
private static void PlayContainerSound(ItemContainer container)
{
    if (DateTime.UtcNow - lastUpdateTime < updateInterval) return;
    
#if CLIENT
    if (container.item.HasTag("steelcabinetsfx"))
        SoundPlayer.PlaySound("interactive_large_container", container.item.WorldPosition, hullGuess: container.item.CurrentHull);
    else if (container.item.HasTag("mediumsteelcabinetsfx"))
        SoundPlayer.PlaySound("interactive_medium_container", container.item.WorldPosition, hullGuess: container.item.CurrentHull);
    else if (container.item.HasTag("extinguisherholder"))
        SoundPlayer.PlaySound("interactive_large_container", container.item.WorldPosition, hullGuess: container.item.CurrentHull);
    else if (container.item.HasTag("suppliescontainer"))
        SoundPlayer.PlaySound("interactive_emergencycab", container.item.WorldPosition, hullGuess: container.item.CurrentHull);
    else if (container.item.HasTag("securecontainer"))
        SoundPlayer.PlaySound("interactive_securitycab_open", container.item.WorldPosition, hullGuess: container.item.CurrentHull);
    else if (container.item.HasTag("medcontainer"))
        SoundPlayer.PlaySound("interactive_med_container_open", container.item.WorldPosition, hullGuess: container.item.CurrentHull);
#endif
    
    lastUpdateTime = DateTime.UtcNow;
}
		//no use leftover
		/*
		[HarmonyPatch("Equip")]
        [HarmonyPrefix]
		public static bool Equip(Character character, ItemContainer  __instance)
		{
			ItemContainer _ = __instance;
			_.IsActive = true;
			//slot => slot.only checks for equipping to hands, which is exactly what we want
			if (character != null && character.HasEquippedItem(_.item, predicate: slot => slot.HasFlag(InvSlotType.LeftHand) || slot.HasFlag(InvSlotType.RightHand)))
			{
				_.SetContainedActive(true);
				//a template will fill in sound effects here
				//DebugConsole.NewMessage("Equip");
			}
			else
			{
				_.SetContainedActive(false);
			}
			return false;
		}
		*/
		
	}
}
