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
        public static readonly float DefaultShadowLength = 40.0f; 
        public static readonly float DynamicShadowAlpha = 0.85f;  
        public static readonly float DefaultCenterThresholdX = 15.0f;    
        public static readonly float DefaultMinOffsetX = 12.0f;          
        public static readonly float DefaultHeightFactor = 0.5f;         
        private static readonly double UpdateIntervalSeconds = 0.1; 
        // ============================================================

        // 🌟 参数结构体：对外公开以便在 Main 中直接构建
        public struct CustomShadowParams
        {
            public float ShadowLength;       
            public float CenterThresholdX;   
            public float MinOffsetX;         
            public float HeightFactor;       

            public CustomShadowParams(float length, float threshold, float minOffset, float heightFactor = 0.5f)
            {
                ShadowLength = length;
                CenterThresholdX = threshold;
                MinOffsetX = minOffset;
                HeightFactor = heightFactor;
            }
        }

        // 🌟 全局放行高速字典：只存放符合条件的 ItemPrefab，同时也是它的参数映射源
        public static readonly Dictionary<MapEntityPrefab, CustomShadowParams> AllowedItemConfigs = new Dictionary<MapEntityPrefab, CustomShadowParams>();

        private struct ShadowRenderData
        {
            public double LastUpdateTime;   
            public bool IsSplitShadow;      
            public Vector2 SingleOffset;    
            public Vector2 SplitOffsetL;    
            public Vector2 SplitOffsetR;    
        }

        private static readonly Dictionary<Item, ShadowRenderData> ShadowCache = new Dictionary<Item, ShadowRenderData>();

        [HarmonyPatch("Draw")]
        [HarmonyPatch(new Type[] { typeof(SpriteBatch), typeof(bool), typeof(bool), typeof(Color?), typeof(float?) })]
        [HarmonyPostfix]
        public static void DrawShadow(SpriteBatch spriteBatch, bool editing, bool back, Color? overrideColor, Item __instance)
        {
            if (__instance == null || spriteBatch == null || __instance.Sprite == null) return;

            // 🌟 核心改动 1：根据载入时做好的字典进行精准拦截！
            // 如果这个物品的 Prefab 没在白名单里，一律不予渲染，0 开销退出
            if (!AllowedItemConfigs.TryGetValue(__instance.Prefab, out CustomShadowParams shadowParams))
            {
                return;
            }

            // 🛠️ 背包、容器内部拦截逻辑保持不变
            if (__instance.ParentInventory?.Owner is Item item)
            {
                if (item.HasTag("container") || item.HasTag("weaponholder"))
                {
                    return;
                }
            }

            if (__instance.ParentInventory?.Owner is Character c)
            {
                if (c.Inventory != null && c.Inventory.IsInLimbSlot(__instance, InvSlotType.Bag))
                {
                    return; 
                }
            }

            // 🌟 核心改动 2：通过了上面的白名单校验后直接执行高速渲染计算
            double currentTime = Timing.TotalTime;

            if (!ShadowCache.TryGetValue(__instance, out ShadowRenderData cache) || 
                currentTime - cache.LastUpdateTime >= UpdateIntervalSeconds)
            {
                cache = CalculateShadowData(__instance, currentTime, shadowParams);
                ShadowCache[__instance] = cache;
            }

            RenderCachedShadow(__instance.Sprite, __instance, spriteBatch, cache);
        }

        /// <summary>
        /// 💡 物理光影计算：直接使用从外层带入的预匹配好的 shadowParams 参数
        /// </summary>
        private static ShadowRenderData CalculateShadowData(Item item, double currentTime, CustomShadowParams shadowParams)
        {
            ShadowRenderData data = new ShadowRenderData
            {
                LastUpdateTime = currentTime,
                IsSplitShadow = false
            };

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

                if (distanceX < shadowParams.CenterThresholdX)
                {
                    data.IsSplitShadow = true;
                    float sY = (lightDir.Y * shadowParams.ShadowLength) * shadowParams.HeightFactor;

                    data.SplitOffsetL = new Vector2(-shadowParams.MinOffsetX, -sY);
                    data.SplitOffsetR = new Vector2(shadowParams.MinOffsetX, -sY);
                }
                else
                {
                    data.IsSplitShadow = false;
                    float sX = lightDir.X * shadowParams.ShadowLength;
                    if (sX > 0 && sX < shadowParams.MinOffsetX) sX = shadowParams.MinOffsetX;
                    if (sX < 0 && sX > -shadowParams.MinOffsetX) sX = -shadowParams.MinOffsetX;

                    float sY = (lightDir.Y * shadowParams.ShadowLength) * shadowParams.HeightFactor;

                    data.SingleOffset = new Vector2(sX, -sY);
                }
            }

            return data;
        }

        private static void RenderCachedShadow(Sprite sprite, Item item, SpriteBatch spriteBatch, ShadowRenderData cache)
        {
            Vector2 baseDrawPos = new Vector2(item.DrawPosition.X, -item.DrawPosition.Y);
            Color shadowColor = Color.Black * DynamicShadowAlpha;
            float shadowDepth = item.GetDrawDepth() + 0.005f; 

            if (!cache.IsSplitShadow && cache.SingleOffset == Vector2.Zero) return;

            float currentRotationRad = (item.body != null && item.body.Enabled) ? -item.body.Rotation : item.RotationRad;

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
            if (isFlipped) { offset.X = -offset.X; }
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