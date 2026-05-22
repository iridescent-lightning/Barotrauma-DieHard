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
        // 升级字典：不仅缓存 Container，还把每个反应堆专属的独立计时器存进去
        public class ReactorCacheData
        {
            public ItemContainer Container;
            public float Timer;
        }

        public static Dictionary<int, ReactorCacheData> ReactorDataCache = new Dictionary<int, ReactorCacheData>();
        private static bool inEditor;
        private const float UPDATE_INTERVAL = 0.2f;  // 每秒更新5次

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void UpdatePostfix(float deltaTime, Camera cam, Reactor __instance)
        {
            // 安全检查
            if (__instance.item == null) return;

            int reactorId = __instance.item.ID;

            // 1. 获取或创建属于当前反应堆实例的独立缓存
            if (!ReactorDataCache.TryGetValue(reactorId, out var cache))
            {
                var containers = __instance.item.GetComponents<ItemContainer>().ToList();
                if (containers.Count > 1)
                {
                    cache = new ReactorCacheData { Container = containers[1], Timer = 0f };
                    ReactorDataCache[reactorId] = cache;
                }
                else
                {
                    // 没有第二个容器，属于原版或不相干反应堆，直接放行
                    return;
                }
            }

            // 2. 实例隔离的节流控制（每个反应堆自己过自己的 0.2 秒）
            cache.Timer += deltaTime;
            if (cache.Timer < UPDATE_INTERVAL) return;
            cache.Timer = 0f; // 仅清空当前反应堆的计时器，不影响其他反应堆

            // 3. 核心逻辑执行
            Item controlrod = null;
            if (cache.Container != null && cache.Container.Inventory != null)
            {
                controlrod = cache.Container.Inventory.GetItemAt(0);
            }

            
            bool isOperating = __instance.TargetTurbineOutput > 0f;

            if (isOperating)
            {
                if (controlrod != null)
                {
                    if (controlrod.Condition <= 0)
                    {
                        // 控制棒耗尽，反应堆失控
                        __instance.fissionRate = 100f;
                        __instance.Item.Condition -= 1.5f * UPDATE_INTERVAL;
                    }
                    else if (__instance.item.InPlayerSubmarine)
                    {
                        // 正常消耗控制棒（联机模式下加个同步脏标记，确保通知到服务器/客户端）
                        controlrod.Condition -= 0.05f * UPDATE_INTERVAL;
                    }
                }
                else
                {
                    // 明确没有控制棒时的失控惩罚
                    if (__instance.item.InPlayerSubmarine)
                    {
                        // 联机环境下可以用这条，但注意别刷屏控制台
                        // DebugConsole.NewMessage($"[DieHard] 反应堆 {reactorId} 缺失控制棒！");
                        __instance.fissionRate = 100f;
                        __instance.Item.Condition -= 1.5f * UPDATE_INTERVAL;
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

        // 每次换图或清理战局时调用，防止内存泄漏
        public static void ClearRactorySecondContainerDictionary()
        {
            ReactorDataCache.Clear();
        }
    }
}