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

namespace LampMod
{
	[HarmonyPatch(typeof(LightComponent))]
	class LightComponentPatch
	{
		// 为每个 LightComponent 实例存储其原始值
        private static readonly ConditionalWeakTable<LightComponent, LightData> OriginalSettings = new();

        private class LightData
        {
            public float Flicker;
            public float FlickerSpeed;
            public double RandomOffset;
        }

        // 在初始化时记录原始值
        [HarmonyPatch("OnItemLoaded")] // 建议用 OnItemLoaded，比 OnMapLoaded 更早且更针对实例
        [HarmonyPostfix]
        public static void Postfix_OnItemLoaded(LightComponent __instance)
        {
            if (!OriginalSettings.TryGetValue(__instance, out _))
            {
                var rand = new Random();
                OriginalSettings.Add(__instance, new LightData
                {
                    Flicker = __instance.Flicker,
                    FlickerSpeed = __instance.FlickerSpeed,
                    RandomOffset = rand.NextDouble()
                });
            }
        }

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void Postfix_Update(float deltaTime, Camera cam, LightComponent __instance)
        {
            // 只处理带有 lamp 标签的物品
            if (!__instance.item.HasTag("lamp")) return;

            if (OriginalSettings.TryGetValue(__instance, out var data))
            {
                if (__instance.item.Condition / __instance.item.MaxCondition < 0.3f)
                {
                    // 使用该实例特有的随机偏移，让不同灯泡闪烁感不同
                    __instance.Flicker = 0.2f + (float)(data.RandomOffset * 0.1 - 0.05);
                    __instance.FlickerSpeed = 0.3f + (float)(data.RandomOffset * 0.1 - 0.05);
                }
                else
                {
                    // 恢复到该实例自己的原始值，而不是全局值
                    __instance.Flicker = data.Flicker;
                    __instance.FlickerSpeed = data.FlickerSpeed;
                }
            }
        }
	}
}
