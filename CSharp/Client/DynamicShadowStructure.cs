﻿using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Barotrauma;
using Barotrauma.Items.Components;
using HarmonyLib;

namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(Structure), "Draw")]
    public static class StructureShadowPatch
    {
        // ==================== 🛠️ 墙体默认阴影渲染参数 ====================
        public static readonly float DefaultShadowLength = 15.0f; 
        public static readonly float DynamicShadowAlpha = 0.45f;  
        public static readonly float DefaultCenterThresholdX = 25.0f; 
        public static readonly float DefaultMinOffsetX = 5.0f;          
        public static readonly float DefaultHeightFactor = 0.35f;         
        
        private static readonly double UpdateIntervalSeconds = 0.15; 
        private static readonly float ShadowLerpSpeed = 7.0f; 

        // 是否开启纯静态预设模式。设置为 true 时，彻底停用墙体的动态光源追踪与实时计算。
        private static readonly bool UseStaticPresetsOnly = false;

        // 静态预设模式下的默认固定影子偏移量
        public static readonly Vector2 StaticDefaultOffset = new Vector2(8.0f, -4.0f);
        // ============================================================

        // 🌟 核心结构：初始化时为每个 Prefab 生成并绑定的配置
        public class ShadowConfig
        {
            public float ShadowLength;
            public float CenterThresholdX;
            public float MinOffsetX;
            public float HeightFactor;
            public Vector2 StaticOffset; // 预先计算好的静态偏移量
        }

        // 🌟 全局高速缓存：只存放【允许绘制阴影】的 Prefab。没在里面的说明被过滤了。
        public static readonly Dictionary<MapEntityPrefab, ShadowConfig> AllowedPrefabConfigs = new Dictionary<MapEntityPrefab, ShadowConfig>();

        private class StructShadowData
        {
            public double LastUpdateTime;       
            public bool TargetIsSplitShadow;    
            
            public Vector2 TargetSingleOffset;
            public Vector2 TargetSplitOffsetL;
            public Vector2 TargetSplitOffsetR;

            public Vector2 CurrentSingleOffset;
            public Vector2 CurrentSplitOffsetL;
            public Vector2 CurrentSplitOffsetR;
            
            public float SplitBlendWeight; 
        }

        private static readonly Dictionary<Structure, StructShadowData> StructShadowCache = new Dictionary<Structure, StructShadowData>();
        private static double lastGlobalLightUpdateTime;
        private static readonly Dictionary<Hull, LightComponent> HullLightCache = new Dictionary<Hull, LightComponent>();

        [HarmonyPatch(new Type[] { typeof(SpriteBatch), typeof(bool), typeof(bool), typeof(Effect) })]
        [HarmonyPrefix] 
        public static void Prefix(SpriteBatch spriteBatch, bool editing, bool back, Effect damageEffect, Structure __instance)
        {
            // 🌟 新增安全拦截：如果后台线程还没初始化完毕，直接返回，避免主线程读取未构建完的字典
            if (!Main.IsShadowConfigInitialized) return;
            // 🌟 核心优化 1：如果 Prefab 连基础贴图都没有，直接拦截
            if (__instance?.Prefab?.Sprite == null) return;
            if (damageEffect != null || !back) return; 

            // 🌟 核心优化 2：【极致轻量化判定】。直接查初始化好的字典，如果查不到，说明该品类在载入时就被过滤了！
            if (!AllowedPrefabConfigs.TryGetValue(__instance.Prefab, out ShadowConfig config))
            {
                return;
            }

            if (editing)
            {
                if (!SubEditorScreen.IsLayerVisible(__instance)) return;
                if (!__instance.HasBody && !Structure.ShowStructures) return;
                if (__instance.HasBody && !Structure.ShowWalls) return;
            }
            else
            {
                if (HideInGame(__instance)) return;
            }

            if (Screen.Selected?.Cam != null)
            {
                if (!__instance.IsVisible(Screen.Selected.Cam.WorldView)) return;
            }

            double currentTime = Timing.TotalTime;

            if (!UseStaticPresetsOnly && currentTime - lastGlobalLightUpdateTime >= UpdateIntervalSeconds)
            {
                HullLightCache.Clear();
                lastGlobalLightUpdateTime = currentTime;
            }

            if (!StructShadowCache.TryGetValue(__instance, out StructShadowData cache))
            {
                cache = new StructShadowData();
                UpdateStructShadowTargets(__instance, cache, currentTime, config);
                cache.CurrentSingleOffset = cache.TargetSingleOffset;
                cache.CurrentSplitOffsetL = cache.TargetSplitOffsetL;
                cache.CurrentSplitOffsetR = cache.TargetSplitOffsetR;
                cache.SplitBlendWeight = cache.TargetIsSplitShadow ? 1.0f : 0.0f;
                StructShadowCache[__instance] = cache;
            }
            else if (!UseStaticPresetsOnly && currentTime - cache.LastUpdateTime >= UpdateIntervalSeconds)
            {
                UpdateStructShadowTargets(__instance, cache, currentTime, config);
            }

            if (UseStaticPresetsOnly)
            {
                RenderStructTiledShadow(__instance, spriteBatch, cache, config);
                return;
            }

            float deltaTime = (float)GameMain.GameScreen.GameTime;
            if (deltaTime > 0.15f) deltaTime = 0.15f; 

            float lerpFactor = 1.0f - (float)Math.Exp(-ShadowLerpSpeed * deltaTime);
            cache.CurrentSingleOffset = Vector2.Lerp(cache.CurrentSingleOffset, cache.TargetSingleOffset, lerpFactor);
            cache.CurrentSplitOffsetL = Vector2.Lerp(cache.CurrentSplitOffsetL, cache.TargetSplitOffsetL, lerpFactor);
            cache.CurrentSplitOffsetR = Vector2.Lerp(cache.CurrentSplitOffsetR, cache.TargetSplitOffsetR, lerpFactor);

            float targetWeight = cache.TargetIsSplitShadow ? 1.0f : 0.0f;
            cache.SplitBlendWeight = MathHelper.Lerp(cache.SplitBlendWeight, targetWeight, lerpFactor);

            RenderStructTiledShadow(__instance, spriteBatch, cache, config);
        }

        private static bool HideInGame(Structure wall)
        {
            return wall.IsHidden;
        }

        private static void UpdateStructShadowTargets(Structure wall, StructShadowData cache, double currentTime, ShadowConfig config)
        {
            cache.LastUpdateTime = currentTime;

            if (UseStaticPresetsOnly)
            {
                cache.TargetIsSplitShadow = false;
                cache.TargetSingleOffset = config.StaticOffset; // 直接拿载入阶段算好的偏移量
                cache.TargetSplitOffsetL = cache.TargetSingleOffset;
                cache.TargetSplitOffsetR = cache.TargetSingleOffset;
                return;
            }

            // ------------------ 以下为动态刷新逻辑 ------------------
            LightComponent bestLight = GetNearestActiveLightForStruct(wall, currentTime);
            if (bestLight == null || bestLight.item == null)
            {
                cache.TargetIsSplitShadow = false;
                cache.TargetSingleOffset = Vector2.Zero;
                cache.TargetSplitOffsetL = Vector2.Zero;
                cache.TargetSplitOffsetR = Vector2.Zero;
                return;
            }

            Vector2 lightPos = bestLight.item.WorldPosition;
            Vector2 wallPos = wall.WorldPosition;
            Vector2 diff = wallPos - lightPos;

            float distanceX = Math.Abs(diff.X);
            float distanceSquared = diff.LengthSquared();

            if (distanceSquared > 0.1f)
            {
                Vector2 lightDir = Vector2.Normalize(diff);
                float shadowReductionY = 1.0f - (Math.Abs(lightDir.Y) * 0.5f);
                float sY = lightDir.Y * config.ShadowLength * config.HeightFactor * shadowReductionY;

                if (distanceX < config.CenterThresholdX)
                {
                    cache.TargetIsSplitShadow = true;
                    cache.TargetSplitOffsetL = new Vector2(-config.MinOffsetX, -sY);
                    cache.TargetSplitOffsetR = new Vector2(config.MinOffsetX, -sY);
                    cache.TargetSingleOffset = cache.TargetSplitOffsetL; 
                }
                else
                {
                    cache.TargetIsSplitShadow = false;
                    float sX = lightDir.X * config.ShadowLength;
                    if (sX > 0 && sX < config.MinOffsetX) sX = config.MinOffsetX;
                    if (sX < 0 && sX > -config.MinOffsetX) sX = -config.MinOffsetX;

                    cache.TargetSingleOffset = new Vector2(sX, -sY);
                    cache.TargetSplitOffsetL = cache.TargetSingleOffset;
                    cache.TargetSplitOffsetR = cache.TargetSingleOffset;
                }
            }
        }

        private static void RenderStructTiledShadow(Structure wall, SpriteBatch spriteBatch, StructShadowData cache, ShadowConfig config)
        {
            float depth = wall.GetDrawDepth() + 0.0001f; 
            Color baseShadowColor = Color.Black * DynamicShadowAlpha;

            Vector2 drawOffset = wall.Submarine == null ? Vector2.Zero : wall.Submarine.DrawPosition;
            drawOffset += (Vector2)AccessTools.Method(typeof(Structure), "GetCollapseEffectOffset").Invoke(wall, null);

            float singleAlpha = UseStaticPresetsOnly ? 1.0f : (1.0f - cache.SplitBlendWeight);
            float splitAlpha = UseStaticPresetsOnly ? 0.0f : cache.SplitBlendWeight;

            Vector2 advanceX = MathUtils.RotatedUnitXRadians(wall.RotationRad).FlipY();
            Vector2 advanceY = advanceX.YX().FlipX();
            if (wall.FlippedX != wall.FlippedY)
            {
                advanceX = advanceX.FlipY();
                advanceY = advanceY.FlipX();
            }

            void DrawSingleSection(Rectangle drawSection, Vector2 offset)
            {
                Vector2 sectionOffset = new Vector2(
                    Math.Abs(wall.Rect.Location.X - drawSection.Location.X),
                    Math.Abs(wall.Rect.Location.Y - drawSection.Location.Y));

                if (wall.FlippedX && wall.IsHorizontal) { sectionOffset.X = wall.Rect.Right - drawSection.Right; }
                if (wall.FlippedY && !wall.IsHorizontal) { sectionOffset.Y = (drawSection.Y - drawSection.Height) - (wall.Rect.Y - wall.Rect.Height); }

                sectionOffset.X += MathUtils.PositiveModulo(-wall.TextureOffset.X, wall.Prefab.Sprite.SourceRect.Width * wall.TextureScale.X * wall.Scale);
                sectionOffset.Y += MathUtils.PositiveModulo(-wall.TextureOffset.Y, wall.Prefab.Sprite.SourceRect.Height * wall.TextureScale.Y * wall.Scale);

                Vector2 pos = new Vector2(drawSection.X, drawSection.Y);
                pos -= wall.Rect.Location.ToVector2();
                pos = advanceX * pos.X + advanceY * pos.Y;
                pos += wall.Rect.Location.ToVector2();
                Vector2 finalDrawPos = new Vector2(pos.X + wall.Rect.Width / 2 + drawOffset.X, -(pos.Y - wall.Rect.Height / 2 + drawOffset.Y));

                float currentAlpha = (offset == cache.CurrentSingleOffset) ? singleAlpha : 0.75f * splitAlpha;

                wall.Prefab.Sprite.DrawTiled(
                    spriteBatch,
                    finalDrawPos + offset, 
                    new Vector2(drawSection.Width, drawSection.Height),
                    rotation: wall.RotationRad,
                    origin: wall.Rect.Size.ToVector2() * new Vector2(0.5f, 0.5f),
                    color: baseShadowColor * currentAlpha,
                    startOffset: sectionOffset,
                    depth: depth,
                    textureScale: wall.TextureScale * wall.Scale,
                    spriteEffects: wall.Prefab.Sprite.effects ^ wall.SpriteEffects);
            }

            for (int i = 0; i < wall.Sections.Length; i++)
            {
                Rectangle drawSection = wall.Sections[i].rect;
                if (singleAlpha > 0.01f) { DrawSingleSection(drawSection, cache.CurrentSingleOffset); }
                if (splitAlpha > 0.01f)
                {
                    DrawSingleSection(drawSection, cache.CurrentSplitOffsetL);
                    DrawSingleSection(drawSection, cache.CurrentSplitOffsetR);
                }
            }
        }

        private static LightComponent GetNearestActiveLightForStruct(Structure wall, double currentTime)
        {
            Hull currentHull = Hull.FindHull(wall.WorldPosition, guess: null, useWorldCoordinates: true);
            if (currentHull == null) return null;

            if (HullLightCache.TryGetValue(currentHull, out LightComponent cachedLight))
            {
                return cachedLight;
            }

            LightComponent bestLight = null;
            float maxScore = -1f;

            foreach (Item otherItem in Item.ItemList)
            {
                if (otherItem.CurrentHull != currentHull) continue;

                var light = otherItem.GetComponent<LightComponent>();
                if (light != null && light.IsOn && light.Range > 0)
                {
                    float dist = Vector2.Distance(wall.WorldPosition, otherItem.WorldPosition);
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

            HullLightCache[currentHull] = bestLight;
            return bestLight;
        }

        public static void ClearCache()
        {
            StructShadowCache.Clear();
            HullLightCache.Clear();
        }
    }
}