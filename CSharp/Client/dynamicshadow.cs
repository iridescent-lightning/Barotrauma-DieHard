/*

way too lag 
using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Barotrauma;
using HarmonyLib;

namespace BarotraumaDieHard
{
    public partial class ItemPatch
    {
        // ==================== 🛠️ 阴影渲染微调参数 ====================
        private static readonly float DynamicShadowLength = 20.0f; // 影子被光线拉开的最大像素距离
        private static readonly float DynamicShadowAlpha = 0.85f;  // 影子的基础透明度
        private static readonly float CenterThresholdX = 15.0f;    // 光源距离物品中心在这个像素以内时，判定为“正中间”
        private static readonly float MinOffsetX = 12.0f;          // 影子向左右两边强行拉开的最小固定X轴像素量
        
        // 🚀【性能减负核心】：每隔多少秒更新一次物理光源偏置计算（0.1秒 = 100毫秒更新一次，肉眼完全看不出延迟，CPU开销暴降）
        private static readonly double UpdateIntervalSeconds = 0.10; 
        // ============================================================

        // 🌟 性能核心：只存储最终画图需要的物理坐标偏置结构体
        private struct ShadowRenderData
        {
            public double LastUpdateTime;   // 上次物理更新时的时间戳 (Timing.TotalTime)
            public bool IsSplitShadow;      // 是否是正上方双向分叉影
            public Vector2 SingleOffset;    // 常规情况的单只影子偏移
            public Vector2 SplitOffsetL;    // 双向影的左偏移
            public Vector2 SplitOffsetR;    // 双向影的右偏移
        }

        // 🌟 全局影子坐标仓库（使用 Item 实例作为 Key 确保每个柜子相互独立，互不打架）
        private static readonly Dictionary<Item, ShadowRenderData> ShadowCache = new Dictionary<Item, ShadowRenderData>();

        [HarmonyPatch("Draw")]
        [HarmonyPatch(new Type[] { typeof(SpriteBatch), typeof(bool), typeof(bool), typeof(Color?), typeof(float?) })]
        [HarmonyPostfix]
        public static void DrawShadow(SpriteBatch spriteBatch, bool editing, bool back, Color? overrideColor, Item __instance)
        {
            if (__instance == null || spriteBatch == null) return;

            // 只有带有 DrawShadow 标签且有主贴图的物体才进入缓存光影渲染
            if (__instance.HasTag("DrawShadow") && __instance.Sprite != null)
            {
                // 🚀【极限减负】：利用完全稳定的 Timing.TotalTime 替代不存在的 Ticks
                double currentTime = Timing.TotalTime;

                if (!ShadowCache.TryGetValue(__instance, out ShadowRenderData cache) || 
                    currentTime - cache.LastUpdateTime >= UpdateIntervalSeconds)
                {
                    // 触发低频物理光影计算，并回写进缓存仓库
                    cache = CalculateShadowData(__instance, currentTime);
                    ShadowCache[__instance] = cache;
                }

                // 🚀 绘制阶段仅执行常数级别的快速内存查找与显卡直绘，彻底消灭全图ItemList遍历的超级卡顿
                RenderCachedShadow(__instance.Sprite, __instance, spriteBatch, cache);
            }
        }

        /// <summary>
        /// 💡 物理光影计算：此方法每个物品每 100 毫秒才被调用一次
        /// </summary>
        private static ShadowRenderData CalculateShadowData(Item item, double currentTime)
        {
            ShadowRenderData data = new ShadowRenderData
            {
                LastUpdateTime = currentTime,
                IsSplitShadow = false
            };

            // 1. 低频寻找房间内最合适的光源（此时遍历ItemList的开销被稀释了十几倍）
            LightComponent bestLight = GetNearestActiveLight(item);
            if (bestLight == null || bestLight.item == null) return data;

            Vector2 lightPos = bestLight.item.WorldPosition;
            Vector2 itemPos = item.WorldPosition;
            Vector2 diff = itemPos - lightPos;

            float distanceX = Math.Abs(diff.X);
            float distanceSquared = diff.LengthSquared();

            if (distanceSquared > 0.1f)
            {
                Vector2 lightDir = Vector2.Normalize(diff);

                // 2. 核心判定：光源是否几乎处在物品的正中间顶端？
                if (distanceX < CenterThresholdX)
                {
                    data.IsSplitShadow = true;
                    // 🌟 优化1：Y 轴偏移量按要求削减为一半 (* 0.5f)
                    float sY = (lightDir.Y * DynamicShadowLength) * 0.5f;

                    // 🌟 优化2：光源在中间时，影子不再缩回中心，而是向左右两侧强行拉开保底分叉影
                    data.SplitOffsetL = new Vector2(-MinOffsetX, -sY);
                    data.SplitOffsetR = new Vector2(MinOffsetX, -sY);
                }
                else
                {
                    data.IsSplitShadow = false;
                    float sX = lightDir.X * DynamicShadowLength;
                    // 🌟 优化3：设置最小 X 轴偏移，防止边缘贴图穿帮或缩入本体
                    if (sX > 0 && sX < MinOffsetX) sX = MinOffsetX;
                    if (sX < 0 && sX > -MinOffsetX) sX = -MinOffsetX;

                    // 🌟 优化1：Y 轴偏移量按要求削减为一半 (* 0.5f)
                    float sY = (lightDir.Y * DynamicShadowLength) * 0.5f;

                    data.SingleOffset = new Vector2(sX, -sY);
                }
            }

            return data;
        }

        /// <summary>
        /// 💡 高性能硬绘制：直接读取 Vector2 偏移数据送给 SpriteBatch
        /// </summary>
        private static void RenderCachedShadow(Sprite sprite, Item item, SpriteBatch spriteBatch, ShadowRenderData cache)
        {
            if (sprite?.Texture == null) return;

            Vector2 baseDrawPos = new Vector2(item.DrawPosition.X, -item.DrawPosition.Y);
            Color shadowColor = Color.Black * DynamicShadowAlpha;
            float shadowDepth = item.GetDrawDepth() + 0.005f; // 推入夹层深度

            if (!cache.IsSplitShadow && cache.SingleOffset == Vector2.Zero) return;

            if (cache.IsSplitShadow)
            {
                // 分裂效果绘制：叠加时稍微调低透明度防止影子死黑
                sprite.Draw(spriteBatch, pos: baseDrawPos + cache.SplitOffsetL, color: shadowColor * 0.65f, 
                            rotate: item.Rotation, scale: item.Scale, origin: sprite.Origin, depth: shadowDepth);

                sprite.Draw(spriteBatch, pos: baseDrawPos + cache.SplitOffsetR, color: shadowColor * 0.65f, 
                            rotate: item.Rotation, scale: item.Scale, origin: sprite.Origin, depth: shadowDepth);
            }
            else
            {
                // 正常单侧光影绘制
                sprite.Draw(spriteBatch, pos: baseDrawPos + cache.SingleOffset, color: shadowColor, 
                            rotate: item.Rotation, scale: item.Scale, origin: sprite.Origin, depth: shadowDepth);
            }
        }

        /// <summary>
        /// 空间轻量光源检索算法（已剔除自身光源过滤）
        /// </summary>
        private static LightComponent GetNearestActiveLight(Item item)
        {
            Hull currentHull = item.CurrentHull;
            if (currentHull == null) return null;

            LightComponent bestLight = null;
            float maxScore = -1f;

            foreach (Item otherItem in Item.ItemList)
            {
                // 🌟【核心修复】：如果遍历到的物品是自己本身，或者不在同一个 Hull 里，直接跳过
                if (otherItem == item || otherItem.CurrentHull != currentHull) continue;

                var light = otherItem.GetComponent<LightComponent>();
                if (light != null && light.IsOn && light.Range > 0)
                {
                    float dist = Vector2.Distance(item.WorldPosition, otherItem.WorldPosition);
                    if (dist > light.Range) continue;

                    float brightness = light.LightColor.A / 255f;
                    if (brightness <= 0.05f) brightness = 0.5f; 

                    float distanceFactor = 1.0f - (dist / light.Range);
                    float currentScore = distanceFactor * brightness;

                    if (currentScore > maxScore)
                    {
                        maxScore = currentScore;
                        bestLight = light;
                    }
                }
            }
            return bestLight;
        }

        // 当换图、结束战局时可手动执行清理，防止长期挂接引发微小内存累积
        public static void ClearCache()
        {
            ShadowCache.Clear();
        }
    }
}*/