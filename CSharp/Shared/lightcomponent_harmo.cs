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
        private class LightData
        {
            public float Flicker;
            public float FlickerSpeed;
            public float RandomOffset;   
            public bool? LastIsDamaged;   
            public float UpdateTimer;     // 错峰检测耐久的计时器
            public Color OriginalColor;   // 用于备份灯泡最原始的颜色和亮度
            public bool IsColorCached;    // 是否已经备份了原始颜色
            
            // ⭐ 新增：用于平滑无规律噪声的缓存变量
            public float LastIntensity;   
        }

        private static readonly ConditionalWeakTable<LightComponent, LightData> OriginalSettings = new();
        private static readonly System.Random _sharedRandom = new System.Random();

        [HarmonyPatch("OnItemLoaded")]
        [HarmonyPostfix]
        public static void Postfix_OnItemLoaded(LightComponent __instance)
        {
            if (__instance?.item == null) return;
            if (!__instance.item.HasTag("lamp")) return;

            if (!OriginalSettings.TryGetValue(__instance, out _))
            {
                float initialDelay = (__instance.item.ID % 30) * 0.016f; 
                OriginalSettings.Add(__instance, new LightData
                {
                    Flicker = __instance.Flicker,
                    FlickerSpeed = __instance.FlickerSpeed,
                    RandomOffset = (float)_sharedRandom.NextDouble(),
                    LastIsDamaged = null,
                    UpdateTimer = initialDelay,
                    IsColorCached = false,
                    LastIntensity = 1.0f
                });
            }
        }

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void Postfix_Update(float deltaTime, Camera cam, LightComponent __instance)
        {
            // 极速原生标签拦截（非 lamp 瞬间放行，保持 0 开销）
            if (__instance?.item == null || !__instance.item.HasTag("lamp")) return;

            if (!OriginalSettings.TryGetValue(__instance, out var data))
            {
                float initialDelay = (__instance.item.ID % 30) * 0.016f;
                data = new LightData
                {
                    Flicker = __instance.Flicker,
                    FlickerSpeed = __instance.FlickerSpeed,
                    RandomOffset = (float)_sharedRandom.NextDouble(),
                    LastIsDamaged = null,
                    UpdateTimer = initialDelay,
                    IsColorCached = false,
                    LastIntensity = 1.0f
                };
                OriginalSettings.Add(__instance, data);
            }

            // 【懒加载备份原始颜色】
            if (!data.IsColorCached && __instance.lightColor.A > 0)
            {
                data.OriginalColor = __instance.lightColor;
                data.IsColorCached = true;
            }

            // --- 🤖 第一部分：低频耐久检测（每 1.5 秒错峰跑一次，节省 CPU） ---
            data.UpdateTimer -= deltaTime;
            if (data.UpdateTimer <= 0f)
            {
                data.UpdateTimer = 1.5f; 

                bool isDamaged = __instance.item.Condition < (__instance.item.MaxCondition * 0.3f);

                if (data.LastIsDamaged != isDamaged)
                {
                    if (isDamaged)
                    {
                        // 坏了：开启原版强烈的频率闪烁（开/关）
                        __instance.Flicker = 0.4f + (data.RandomOffset * 0.2f - 0.1f);
                        __instance.FlickerSpeed = 0.5f + (data.RandomOffset * 0.2f - 0.1f);
                    }
                    else
                    {
                        // 修好了：恢复原版正常的闪烁参数
                        __instance.Flicker = data.Flicker;
                        __instance.FlickerSpeed = data.FlickerSpeed;
                        
                        if (data.IsColorCached)
                        {
                            __instance.lightColor = data.OriginalColor;
                        }
                    }
                    data.LastIsDamaged = isDamaged;
                }
            }

            // --- 💡 第二部分：高频无规律亮度变化（仅在坏掉时每帧执行） ---
            if (data.LastIsDamaged != true || !data.IsColorCached) return;

            // 1. 模拟无规律噪声：下一帧的亮度基于当前亮度做随机震荡（一阶马尔可夫链），彻底打破正弦波的节奏感
            // 每次目标在中等亮度（0.4~0.85）之间无序乱窜
            float targetIntensity = 0.4f + (float)_sharedRandom.NextDouble() * 0.45f;
            
            // 使用 Lerp 进行平滑，防止由于纯随机导致光圈产生类似硬切的塑料感，保持有电流过渡的感觉
            // 乘以较小的系数（如 0.25）可以让光晕产生神经质但连续的颤抖
            float intensity = MathHelper.Lerp(data.LastIntensity, targetIntensity, 0.25f);

            // 2. 注入突发闪烁截断（接触不良短路细节）
            // 给予 3% 的纯随机概率，灯泡在这一帧发生剧烈短路，亮度瞬间跌落到 5% 变暗甚至全黑
            if (_sharedRandom.NextDouble() < 0.03)
            {
                intensity = (float)_sharedRandom.NextDouble() * 0.05f; 
            }

            // 3. 注入超高频微小杂讯（光圈边缘滋滋颤抖效果）
            intensity += (float)(_sharedRandom.NextDouble() * 0.08f - 0.04f);

            // 确保安全边界，绝不溢出
            intensity = MathHelper.Clamp(intensity, 0.0f, 1.0f);
            data.LastIntensity = intensity; // 记录下来供下一帧平滑使用

            // 将无规律计算出的亮度系数，应用到正确的 lightColor 上
            __instance.lightColor = new Color(
                (byte)(data.OriginalColor.R * intensity),
                (byte)(data.OriginalColor.G * intensity),
                (byte)(data.OriginalColor.B * intensity),
                (byte)(data.OriginalColor.A * intensity)
            );
        }

        [HarmonyPatch("ReceiveSignal")]
        [HarmonyPrefix]
        public static bool Prefix(LightComponent __instance)
        {
            if (__instance?.item == null) return true;
            if (__instance.item.HasTag("door"))
            {
                return false;
            }
            return true;
        }
    }
}