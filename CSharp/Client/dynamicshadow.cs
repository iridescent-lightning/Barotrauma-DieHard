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
        // ==================== 🛠️ 默认阴影渲染微调参数 ====================
        private static readonly float DefaultShadowLength = 40.0f; // 影子被光线拉开的最大像素距离
        private static readonly float DynamicShadowAlpha = 0.85f;  // 影子的基础透明度
        private static readonly float DefaultCenterThresholdX = 15.0f;    // 光源距离物品中心在这个像素以内时，判定为“正中间”
        private static readonly float DefaultMinOffsetX = 12.0f;          // 影子向左右两边强行拉开的最小固定X轴像素量
        private static readonly float DefaultHeightFactor = 0.5f;         // 默认的 Y 轴高度偏移量缩放比例 (旧代码中写死的 * 0.5f)
        
        // 🚀【性能减负核心】：每隔多少秒更新一次物理光源偏置计算
        private static readonly double UpdateIntervalSeconds = 0.10; 
        // ============================================================

        // 🌟 新增：专属物品自定义光影参数结构体
        private struct CustomShadowParams
        {
            public float ShadowLength;       // 专属最大拉开像素距离
            public float CenterThresholdX;   // 专属正中间判定阈值
            public float MinOffsetX;         // 专属最小固定 X 轴偏置量
            public float HeightFactor;       // 专属 Y 轴高度缩放比 (比如有些高物品需要拉长或缩短影子)

            public CustomShadowParams(float length, float threshold, float minOffset, float heightFactor = 0.5f)
            {
                ShadowLength = length;
                CenterThresholdX = threshold;
                MinOffsetX = minOffset;
                HeightFactor = heightFactor;
            }
        }

        // 🌟 新增：个别物品手动设定参数字典 (使用物品 Prefab 的 Identifier 作为 Key)
        private static readonly Dictionary<string, CustomShadowParams> CustomItemSettings = new Dictionary<string, CustomShadowParams>
        {
            // 📝 示例 1：为左轮手枪设定专属的短影子和更小的判定
            { "revolver", new CustomShadowParams(length: 20.0f, threshold: 10.0f, minOffset: 6.0f, heightFactor: 0.4f) },

            // 📝 示例 2：为潜水刀设定专属参数
            { "extinguisher", new CustomShadowParams(length: 15.0f, threshold: 12.0f, minOffset: 0.3f) },

             { "extinguisherbracket", new CustomShadowParams(length: 15.0f, threshold: 12.0f, minOffset: 0.3f) },
            
            // 📝 示例 3：为大型机柜等需要长影子的物品单独拉大参数
            { "reactor1", new CustomShadowParams(length: 60.0f, threshold: 15.0f, minOffset: 25.0f, heightFactor: 0.2f) },

            // 在此处可以继续按照格式追加任何你想要特殊对待的物品 ID
            { "suppliescabinet", new CustomShadowParams(length: 20.0f, threshold: 15.0f, minOffset: 10.0f, heightFactor: 0.7f) },

            { "junctionbox", new CustomShadowParams(length: 30.0f, threshold: 15.0f, minOffset: 20.0f, heightFactor: 0.7f) },

            { "oxygengenerator", new CustomShadowParams(length: 60.0f, threshold: 15.0f, minOffset: 45.0f, heightFactor: 0.2f) },

            { "outpostoxygengenerator", new CustomShadowParams(length: 60.0f, threshold: 15.0f, minOffset: 45.0f, heightFactor: 0.2f) }


        };

        private struct ShadowRenderData
        {
            public double LastUpdateTime;   // 上次物理更新时的时间戳 (Timing.TotalTime)
            public bool IsSplitShadow;      // 是否是正上方双向分叉影
            public Vector2 SingleOffset;    // 常规情况的单只影子偏移
            public Vector2 SplitOffsetL;    // 双向影的左偏移
            public Vector2 SplitOffsetR;    // 双向影的右偏移
        }

        private static readonly Dictionary<Item, ShadowRenderData> ShadowCache = new Dictionary<Item, ShadowRenderData>();

        [HarmonyPatch("Draw")]
        [HarmonyPatch(new Type[] { typeof(SpriteBatch), typeof(bool), typeof(bool), typeof(Color?), typeof(float?) })]
        [HarmonyPostfix]
        public static void DrawShadow(SpriteBatch spriteBatch, bool editing, bool back, Color? overrideColor, Item __instance)
        {
            if (__instance == null || spriteBatch == null) return;

            // 🌟【核心改动】：如果物品当前位于任何容器或物品栏中，直接拦截，不绘制影子
            if (__instance.ParentInventory?.Owner is Item item)
            {
                if(item.HasTag("container") || item.HasTag("weaponholder"))
                {
                    return;
                }
            }

            if (__instance.HasTag("DrawShadow") && __instance.Sprite != null)
            {
                double currentTime = Timing.TotalTime;

                if (!ShadowCache.TryGetValue(__instance, out ShadowRenderData cache) || 
                    currentTime - cache.LastUpdateTime >= UpdateIntervalSeconds)
                {
                    cache = CalculateShadowData(__instance, currentTime);
                    ShadowCache[__instance] = cache;
                }

                RenderCachedShadow(__instance.Sprite, __instance, spriteBatch, cache);
            }
        }

        /// <summary>
        /// 💡 物理光影计算：引入了配置字典检索，实现个别物品参数重载
        /// </summary>
        private static ShadowRenderData CalculateShadowData(Item item, double currentTime)
        {
            ShadowRenderData data = new ShadowRenderData
            {
                LastUpdateTime = currentTime,
                IsSplitShadow = false
            };

            LightComponent bestLight = GetNearestActiveLight(item);
            if (bestLight == null || bestLight.item == null) return data;

            // 🌟【核心功能改动】：检索字典。如果当前物品有专属参数则选用，否则使用全局默认微调参数
            float activeShadowLength = DefaultShadowLength;
            float activeCenterThresholdX = DefaultCenterThresholdX;
            float activeMinOffsetX = DefaultMinOffsetX;
            float activeHeightFactor = DefaultHeightFactor;

            if (item.Prefab != null && CustomItemSettings.TryGetValue(item.Prefab.Identifier.Value, out CustomShadowParams customParams))
            {
                activeShadowLength = customParams.ShadowLength;
                activeCenterThresholdX = customParams.CenterThresholdX;
                activeMinOffsetX = customParams.MinOffsetX;
                activeHeightFactor = customParams.HeightFactor;
            }

            Vector2 lightPos = bestLight.item.WorldPosition;
            Vector2 itemPos = item.WorldPosition;
            Vector2 diff = itemPos - lightPos;

            float distanceX = Math.Abs(diff.X);
            float distanceSquared = diff.LengthSquared();

            if (distanceSquared > 0.1f)
            {
                Vector2 lightDir = Vector2.Normalize(diff);

                // 核心判定：使用动态激活的 Threshold
                if (distanceX < activeCenterThresholdX)
                {
                    data.IsSplitShadow = true;
                    // 使用动态激活的 Length 和 HeightFactor 计算 Y 轴
                    float sY = (lightDir.Y * activeShadowLength) * activeHeightFactor;

                    // 使用动态激活的 MinOffsetX
                    data.SplitOffsetL = new Vector2(-activeMinOffsetX, -sY);
                    data.SplitOffsetR = new Vector2(activeMinOffsetX, -sY);
                }
                else
                {
                    data.IsSplitShadow = false;
                    float sX = lightDir.X * activeShadowLength;
                    if (sX > 0 && sX < activeMinOffsetX) sX = activeMinOffsetX;
                    if (sX < 0 && sX > -activeMinOffsetX) sX = -activeMinOffsetX;

                    float sY = (lightDir.Y * activeShadowLength) * activeHeightFactor;

                    data.SingleOffset = new Vector2(sX, -sY);
                }
            }

            return data;
        }

        private static void RenderCachedShadow(Sprite sprite, Item item, SpriteBatch spriteBatch, ShadowRenderData cache)
        {
            if (sprite?.Texture == null) return;

            Vector2 baseDrawPos = new Vector2(item.DrawPosition.X, -item.DrawPosition.Y);
            Color shadowColor = Color.Black * DynamicShadowAlpha;
            float shadowDepth = item.GetDrawDepth() + 0.005f; 

            if (!cache.IsSplitShadow && cache.SingleOffset == Vector2.Zero) return;

            float currentRotationRad = (item.body != null && item.body.Enabled) ? -item.body.Rotation : -item.RotationRad;

            bool isFlipped = false;
            SpriteEffects spriteEffects = SpriteEffects.None;

            if (item.body != null && item.body.Enabled)
            {
                if (item.body.Dir < 0.0f)
                {
                    isFlipped = true;
                    spriteEffects = SpriteEffects.FlipHorizontally;
                }
            }
            else if (item.FlippedX && item.Prefab.CanSpriteFlipX)
            {
                isFlipped = true;
                spriteEffects = SpriteEffects.FlipHorizontally;
            }

            if (cache.IsSplitShadow)
            {
                Vector2 finalOffsetL = ApplyRotationToOffset(cache.SplitOffsetL, currentRotationRad, isFlipped);
                Vector2 finalOffsetR = ApplyRotationToOffset(cache.SplitOffsetR, currentRotationRad, isFlipped);

                sprite.Draw(spriteBatch, pos: baseDrawPos + finalOffsetL, color: shadowColor * 0.75f, 
                            rotate: currentRotationRad, scale: item.Scale, origin: sprite.Origin, depth: shadowDepth, spriteEffect: spriteEffects);

                sprite.Draw(spriteBatch, pos: baseDrawPos + finalOffsetR, color: shadowColor * 0.75f, 
                            rotate: currentRotationRad, scale: item.Scale, origin: sprite.Origin, depth: shadowDepth, spriteEffect: spriteEffects);
            }
            else
            {
                Vector2 finalOffset = ApplyRotationToOffset(cache.SingleOffset, currentRotationRad, isFlipped);

                sprite.Draw(spriteBatch, pos: baseDrawPos + finalOffset, color: shadowColor, 
                            rotate: currentRotationRad, scale: item.Scale, origin: sprite.Origin, depth: shadowDepth, spriteEffect: spriteEffects);
            }
        }

        private static Vector2 ApplyRotationToOffset(Vector2 offset, float radians, bool isFlipped)
        {
            if (isFlipped)
            {
                offset.X = -offset.X;
            }

            if (Math.Abs(radians) < 0.0001f) return offset;

            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);

            return new Vector2(
                offset.X * cos - offset.Y * sin,
                offset.X * sin + offset.Y * cos
            );
        }

        private static LightComponent GetNearestActiveLight(Item item)
        {
            Hull currentHull = item.CurrentHull;
            if (currentHull == null) return null;

            LightComponent bestLight = null;
            float maxScore = -1f;

            foreach (Item otherItem in Item.ItemList)
            {
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

        public static void ClearCache()
        {
            ShadowCache.Clear();
        }
    }
}