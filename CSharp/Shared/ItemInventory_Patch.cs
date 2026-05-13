using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

using System.Globalization;
using System.Xml.Linq;

using FarseerPhysics;
using System.Collections.Immutable;
using HarmonyLib;
using System;

//不裝備架子直接拖拽也會燒傷，防止使用拖拽作弊
namespace BarotraumaDieHard
{
	[HarmonyPatch(typeof(Inventory))]
	class ItemInventoryPatch
	{
		// 拦截重载 1: 拖拽到指定槽位 (常见于 UI 拖拽落位)
        [HarmonyPatch("TryPutItem")]
        [HarmonyPatch(new Type[] { typeof(Item), typeof(int), typeof(bool), typeof(bool), typeof(Character), typeof(bool), typeof(bool), typeof(bool) })]
        [HarmonyPrefix]
        public static bool PrefixSlot(Inventory __instance, Item item, Character user)
        {
            
            return CheckSafety(__instance, item, user);
        }

        // 拦截重载 2: 自动寻找槽位 (常见于双击或模糊拖拽)
        [HarmonyPatch("TryPutItem")]
        [HarmonyPatch(new Type[] { typeof(Item), typeof(Character), typeof(List<InvSlotType>), typeof(bool), typeof(bool), typeof(bool) })]
        [HarmonyPrefix]
        public static bool PrefixAuto(Inventory __instance, Item item, Character user)
        {
            
            return CheckSafety(__instance, item, user);
        }

        // 统一安全检查逻辑
        private static bool CheckSafety(Inventory inventory, Item item, Character user)
        {
            // 如果物品无效、已被删除、或操作者为空，允许操作
            if (item == null || item.Removed || user == null) return true;

            // 1. 检查是否是放射性燃料棒 (通过你的自定义组件判定)
            var fuelRod = item.Components.OfType<BarotraumaDieHard.Items.RadioactiveFuelRod>().FirstOrDefault();
            
            // 2. 只有人类玩家在未穿防护服的情况下直接接触损坏的燃料棒才会触发
            if (fuelRod != null && user.IsHuman && item.Condition < item.MaxCondition)
            {
                // 3. 检查玩家是否正提着 Holder (检查左右手)
                bool holdingHolder = false;
                foreach (Item equippedItem in user.HeldItems)
                {
                    if (equippedItem.Prefab.Identifier == "fuelrodholder")
                    {
                        holdingHolder = true;
                        break;
                    }
                }

                // 4. 如果没有提着 Holder，执行惩罚
                if (!holdingHolder)
                {
                    // --- 惩罚逻辑 ---
                    
                    // 施加烫伤 (Burn)
                    // 参数: 伤害类型, 伤害数值, 部位, 来源
                    user.CharacterHealth.ApplyAffliction(
                        user.AnimController.GetLimb(LimbType.RightHand), 
                        new Affliction(AfflictionPrefab.Burn, 15.0f));

                    // 施加轻微眩晕 (Stun) 防止玩家瞬间又捡起来
                    user.SetStun(1.5f);


                    // 5. 强制掉落：让物品脱离玩家的鼠标或操作，掉在地上
                    item.Drop(user);
        #if CLIENT   
                                BarotraumaDieHard.CustomHintManager.DisplayHint("touch_hot_fuelrod_with_bare_hands".ToIdentifier());
        #endif      
                    // 6. 返回 false 拦截 TryPutItem：
                    // 这样物品就不会进入任何容器（包括背包或 Case），而是直接从原来的地方掉出来
                    return false;
                }
            }

            return true;
        }
        
    }
	
}
