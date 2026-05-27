/*using Barotrauma;
using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using HarmonyLib;

namespace BarotraumaDieHard
{
    public partial class CharacterPatch
    {
        // ==================== 🛠️ 人物阴影渲染微调参数 ====================
        private static readonly float CharacterShadowLength = 25.0f; // 人物影子稍拉长一点，显得高大
        private static readonly float CharacterShadowAlpha = 0.65f;  // 人物阴影稍淡一点（防死黑）
        private static readonly float CenterThresholdX = 20.0f;     
        private static readonly float MinOffsetX = 10.0f;          
        
        // 每隔 0.1 秒更新一次物理光源计算
        private static readonly double UpdateIntervalSeconds = 0.10; 

        // 🌟 人物光影数据结构
        private struct CharShadowData
        {
            public double LastUpdateTime;   
            public bool IsSplitShadow;      
            public Vector2 SingleOffset;    
            public Vector2 SplitOffsetL;    
            public Vector2 SplitOffsetR;    
        }

        // 🌟 全局人物影子缓存仓库
        private static readonly Dictionary<Character, CharShadowData> CharShadowCache = new Dictionary<Character, CharShadowData>();

        /// <summary>
        /// 挂接在 Character 的 Draw 方法上（Postfix 补丁）
        /// </summary>
        [HarmonyPatch(typeof(Character), "Draw")]
        [HarmonyPostfix]
        public static void DrawCharacterShadow(SpriteBatch spriteBatch, Character __instance)
        {
            // 基础安全检查：死人、隐形、或者不在房间（Hull）里的人不画影子
            if (__instance == null || spriteBatch == null || __instance.IsDead || __instance.Removed) return;
            DebugConsole.NewMessage("drawing");
            Hull currentHull = __instance.CurrentHull;
            if (currentHull == null) return; // 🌟 严格限制：只在 Hull（房间内）渲染，出舱外不渲染

            // 抓取人物的主躯干（Torso），用它的 Sprite 作为影子基准
            Limb torso = __instance.AnimController?.GetLimb(LimbType.Torso);
            if (torso?.Sprite == null) return;

            double currentTime = Timing.TotalTime;

            // 🚀 低频物理计算缓存检查
            if (!CharShadowCache.TryGetValue(__instance, out CharShadowData cache) || 
                currentTime - cache.LastUpdateTime >= UpdateIntervalSeconds)
            {
                cache = CalculateCharShadowData(__instance, currentHull, currentTime);
                CharShadowCache[__instance] = cache;
            }

            // 🚀 执行高性能直绘
            RenderCharShadow(torso, __instance, spriteBatch, cache);
        }

        /// <summary>
        /// 💡 人物物理光影计算（每 100 毫秒一次）
        /// </summary>
        private static CharShadowData CalculateCharShadowData(Character character, Hull currentHull, double currentTime)
        {
            CharShadowData data = new CharShadowData
            {
                LastUpdateTime = currentTime,
                IsSplitShadow = false
            };

            // 寻找同房间内最亮、最近的光源
            LightComponent bestLight = GetNearestActiveLightForChar(character, currentHull);
            if (bestLight == null || bestLight.item == null) return data;

            Vector2 lightPos = bestLight.item.WorldPosition;
            Vector2 charPos = character.WorldPosition;
            Vector2 diff = charPos - lightPos;

            float distanceX = Math.Abs(diff.X);
            float distanceSquared = diff.LengthSquared();

            if (distanceSquared > 0.1f)
            {
                Vector2 lightDir = Vector2.Normalize(diff);

                // 判定光源是否在头顶正中间
                if (distanceX < CenterThresholdX)
                {
                    data.IsSplitShadow = true;
                    float sY = (lightDir.Y * CharacterShadowLength) * 0.4f; // 人物 Y 轴压缩多一点，看起来更像贴在地上

                    data.SplitOffsetL = new Vector2(-MinOffsetX, -sY);
                    data.SplitOffsetR = new Vector2(MinOffsetX, -sY);
                }
                else
                {
                    data.IsSplitShadow = false;
                    float sX = lightDir.X * CharacterShadowLength;
                    if (sX > 0 && sX < MinOffsetX) sX = MinOffsetX;
                    if (sX < 0 && sX > -MinOffsetX) sX = -MinOffsetX;

                    float sY = (lightDir.Y * CharacterShadowLength) * 0.4f;

                    data.SingleOffset = new Vector2(sX, -sY);
                }
            }

            return data;
        }

        /// <summary>
        /// 💡 人物影子强力硬绘制（每帧执行，纯内存传递）
        /// </summary>
        private static void RenderCharShadow(Limb torso, Character character, SpriteBatch spriteBatch, CharShadowData cache)
        {
            Sprite sprite = torso.Sprite;
            if (sprite?.Texture == null) return;

            // 🌟 核心同步：获取躯干当前的绘制坐标、旋转角度和镜像翻转状态
            Vector2 baseDrawPos = new Vector2(torso.DrawPosition.X, -torso.DrawPosition.Y);
            float rotation = torso.Rotation;
            SpriteEffects spriteEffects = character.AnimController.Dir > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            Color shadowColor = Color.Black * CharacterShadowAlpha;
            
            // 🌟 深度调整：将影子推入人物图层后方（潜渊症中 Character 的渲染深度一般在 0.3~0.6 之间）
            float shadowDepth = 0.002f; 

            if (!cache.IsSplitShadow && cache.SingleOffset == Vector2.Zero) return;

            if (cache.IsSplitShadow)
            {
                // 双侧分叉影
                sprite.Draw(spriteBatch, pos: baseDrawPos + cache.SplitOffsetL, color: shadowColor * 0.7f, 
                            rotate: rotation, scale: torso.Scale, origin: sprite.Origin, depth: shadowDepth, spriteEffect: spriteEffects);

                sprite.Draw(spriteBatch, pos: baseDrawPos + cache.SplitOffsetR, color: shadowColor * 0.7f, 
                            rotate: rotation, scale: torso.Scale, origin: sprite.Origin, depth: shadowDepth, spriteEffect: spriteEffects);
            }
            else
            {
                // 单侧影
                sprite.Draw(spriteBatch, pos: baseDrawPos + cache.SingleOffset, color: shadowColor, 
                            rotate: rotation, scale: torso.Scale, origin: sprite.Origin, depth: shadowDepth, spriteEffect: spriteEffects);
            }
        }

        /// <summary>
        /// 专门针对人物优化的人间轻量光源检索（已强制限制在同一个 Hull）
        /// </summary>
        private static LightComponent GetNearestActiveLightForChar(Character character, Hull currentHull)
        {
            LightComponent bestLight = null;
            float maxScore = -1f;

            // 遍历当前房间所在潜艇的物品列表（效率比全局 ItemList 更高）
            var itemList = character.Submarine == null ? Item.ItemList : character.Submarine.GetItems(true);
            
            foreach (Item otherItem in itemList)
            {
                if (otherItem.CurrentHull != currentHull) continue;

                var light = otherItem.GetComponent<LightComponent>();
                if (light != null && light.IsOn && light.Range > 0)
                {
                    float dist = Vector2.Distance(character.WorldPosition, otherItem.WorldPosition);
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

        // 切换关卡、出入站、回主菜单时清理
        public static void ClearCharacterCache()
        {
            CharShadowCache.Clear();
        }
    }
}*/