using Microsoft.Xna.Framework;
using System;
using Barotrauma.Networking;
using Barotrauma.Extensions;
#if CLIENT
using Microsoft.Xna.Framework.Graphics;
using Barotrauma.Lights;
#endif

using System.Runtime.CompilerServices;
using Barotrauma.Items.Components;
using Barotrauma;
using HarmonyLib;

namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(LightComponent))]
    class LightComponentPatch
    {
        // 扩展数据，顺便记录上一次的“损坏状态”，避免重复赋值
        private class LightData
        {
            public float Flicker;
            public float FlickerSpeed;
            public double RandomOffset;
            public bool IsLamp;          // 缓存 Tag 检查结果，一劳永逸
            public bool? LastIsDamaged;   // 记录上一次的状态（null: 未初始化, true: 坏了, false: 正常）
        }

        private static readonly ConditionalWeakTable<LightComponent, LightData> OriginalSettings = new();

        [HarmonyPatch("OnItemLoaded")]
        [HarmonyPostfix]
        public static void Postfix_OnItemLoaded(LightComponent __instance)
        {
            if (__instance?.item == null) return;

            if (!OriginalSettings.TryGetValue(__instance, out _))
            {
                var rand = new Random();
                OriginalSettings.Add(__instance, new LightData
                {
                    Flicker = __instance.Flicker,
                    FlickerSpeed = __instance.FlickerSpeed,
                    RandomOffset = rand.NextDouble(),
                    IsLamp = __instance.item.HasTag("lamp"), // 在加载时就存好，避免 Update 频繁读取
                    LastIsDamaged = null 
                });
            }
        }

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void Postfix_Update(float deltaTime, Camera cam, LightComponent __instance)
        {
            if (__instance == null) return;

            // 1. 优先查表。如果加载时没记录（比如动态生成的物品），先去查表初始化
            if (!OriginalSettings.TryGetValue(__instance, out var data))
            {
                // 保底机制：如果没能在 OnItemLoaded 捕获，就在这里补上（Barotrauma 有时会有动态生成的物品）
                if (__instance.item == null) return;
                var rand = new Random();
                data = new LightData
                {
                    Flicker = __instance.Flicker,
                    FlickerSpeed = __instance.FlickerSpeed,
                    RandomOffset = rand.NextDouble(),
                    IsLamp = __instance.item.HasTag("lamp"),
                    LastIsDamaged = null
                };
                OriginalSettings.Add(__instance, data);
            }

            // 2. 极其廉价的布尔值过滤，直接干掉非 lamp 物品的后续逻辑
            if (!data.IsLamp) return;

            // 3. 计算当前是否处于低耐久状态
            bool isDamaged = (__instance.item.Condition / __instance.item.MaxCondition) < 0.3f;

            // 4. 状态没变就直接跳过！只有在“刚坏掉”或“刚修好”的那一帧才执行赋值
            if (data.LastIsDamaged == isDamaged) return;

            // 5. 状态发生改变，更新属性
            if (isDamaged)
            {
                __instance.Flicker = 0.2f + (float)(data.RandomOffset * 0.1 - 0.05);
                __instance.FlickerSpeed = 0.3f + (float)(data.RandomOffset * 0.1 - 0.05);
            }
            else
            {
                __instance.Flicker = data.Flicker;
                __instance.FlickerSpeed = data.FlickerSpeed;
            }

            // 6. 记录新状态
            data.LastIsDamaged = isDamaged;
        }

        [HarmonyPatch("ReceiveSignal")]
        [HarmonyPrefix]
        public static bool Prefix(LightComponent __instance)
        {
            // 这里原代码没太大性能问题，做个简单的 null 检查即可
            if (__instance?.item == null) return true;

            if (__instance.item.HasTag("door"))
            {
                return false;
            }
            return true;
        }
    }
}