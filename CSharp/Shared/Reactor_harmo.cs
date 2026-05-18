using System;
using System.Reflection;
using System.Collections.Generic;
using Barotrauma;
using Barotrauma.Items.Components;
using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Linq;

namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(Reactor))]
    class ReactorDieHard
    {
        public static Dictionary<int, ItemContainer> SecondItemContainerReactors = new Dictionary<int, ItemContainer>();
        private static bool inEditor;

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void UpdatePostfix(float deltaTime, Camera cam, Reactor __instance)
        {
            // 安全检查：如果游戏正在关卡加载中或结束时，直接放行
            if (__instance.item == null) return;

            ItemContainer itemContainer = null;
            Item controlrod = null; // 【修改 1】移除了 static，改为局部变量，防止 null 污染

            // 尝试从字典中读取第二个容器
            if (SecondItemContainerReactors.TryGetValue(__instance.item.ID, out var cachedContainer))
            {
                itemContainer = cachedContainer;
            }
            else
            {
                // 【修改 2：双保险防御机制】
                // 如果开局前几帧字典还没初始化好，我们尝试直接在运行时动态抓取它身上的所有容器
                var containers = __instance.item.GetComponents<ItemContainer>().ToList();
                if (containers.Count > 1)
                {
                    itemContainer = containers[1];
                    SecondItemContainerReactors[__instance.item.ID] = itemContainer; // 顺手补回缓存
                }
                else
                {
                    // 如果这个反应堆身上真的没有第二个容器，说明可能是原版潜艇或者是其它模组的反应堆
                    // 直接 return 放行，不要执行自定义的惩罚，避免报错
                    return; 
                }
            }

            // 成功拿到容器，安全地获取里面的控制棒
            if (itemContainer != null && itemContainer.Inventory != null)
            {
                controlrod = itemContainer.Inventory.GetItemAt(0);
            }

            // 只有当涡轮有输出（即反应堆开着、或者处于自动控制启动状态）时才检测
            if (__instance.TargetTurbineOutput > 0f)
            {
                if (controlrod != null)
                {
                    if (controlrod.Condition <= 0)
                    {
                        // 控制棒耗尽，反应堆失控
                        __instance.fissionRate = 100f;
                        __instance.Item.Condition -= 1.5f * deltaTime;
                    }
                    else if (__instance.item.InPlayerSubmarine)
                    {
                        // 正常消耗控制棒
                        controlrod.Condition -= 0.05f * deltaTime;
                    }
                }
                else
                {
                    // 【修改 3】只有当初始化完全就绪，且明确检测到里面“确实没有控制棒”时才惩罚
                    // 并且加上 InPlayerSubmarine 判定，防止战局刚载入时非玩家潜艇（如前哨站、遗迹）的反应堆跟着一起平白无故爆炸
                    if (__instance.item.InPlayerSubmarine)
                    {
                        DebugConsole.NewMessage($"[DieHard] 反应堆 {__instance.item.ID} 内未检测到控制棒，反应堆开始失控！");
                        __instance.fissionRate = 100f;
                        __instance.Item.Condition -= 1.5f * deltaTime;
                    }
                }
            }

            // 临界崩溃状态触发爆炸辅助
            if (__instance.Item.Condition < 2f && __instance.Item.Condition > 0f && __instance.Temperature > 10f)
            {
                Entity.Spawner.AddItemToSpawnQueue(ItemPrefab.GetItemPrefab("reactorcsexplosionhelper"), __instance.Item.WorldPosition);
            }
        }

        [HarmonyPatch("OnMapLoaded")]
        [HarmonyPostfix]
        public static void OnMapLoadedPostfix(Reactor __instance)
        {
            foreach (Submarine sub in Submarine.Loaded)
            {
                if (sub?.Info?.OutpostGenerationParams != null)
                {
                    inEditor = true;
                }
            }
        }

        public static void ClearRactorySecondContainerDictionary()
        {
            SecondItemContainerReactors.Clear();
        }
    }
}