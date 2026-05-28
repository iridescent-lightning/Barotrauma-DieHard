/*using Barotrauma;
using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace BarotraumaDieHard
{
    public partial class CharacterPatch
    {
        // ==================== 🛠️ 人物阴影渲染微调参数 ====================
        private static readonly float CharacterShadowLength = 65.0f; 
        private static readonly float CenterThresholdX = 20.0f;     
        private static readonly float MinOffsetX = 10.0f;          
        
        private static readonly double UpdateIntervalSeconds = 0.10; 

        // 🌟 核心缓存
        private static readonly Dictionary<Texture2D, Texture2D> CustomShadowTextureCache = new Dictionary<Texture2D, Texture2D>();
        private static readonly HashSet<Texture2D> CheckedTextures = new HashSet<Texture2D>();

        private struct CharShadowData
        {
            public double LastUpdateTime;   
            public bool IsSplitShadow;      
            public Vector2 SingleOffset;    
            public Vector2 SplitOffsetL;    
            public Vector2 SplitOffsetR;    
        }

        private static readonly Dictionary<Character, CharShadowData> CharShadowCache = new Dictionary<Character, CharShadowData>();

        // 🌟 你的 Mod 专属包对象寻路
        private static readonly ContentPackage modPackage = ContentPackageManager.AllPackages.FirstOrDefault(p => p.Name == "Barotrauma Die Hard");

        /// <summary>
        /// 🌟 强行重定向寻路：让原版衣服、挂件和人类身体本体(Human_male)直接去你的 Shadows 文件夹找对应的专用影子
        /// </summary>
        private static Texture2D GetOrCreateShadowTexture(Sprite sprite)
        {
            if (sprite?.Texture == null) return null;
            
            if (CustomShadowTextureCache.TryGetValue(sprite.Texture, out Texture2D cachedShadow)) return cachedShadow;
            if (CheckedTextures.Contains(sprite.Texture)) return null;

            CheckedTextures.Add(sprite.Texture);

            if (modPackage == null) return null;

            string originalPath = sprite.FilePath?.Value; 
            if (string.IsNullOrEmpty(originalPath)) return null;

            // 无论路径多深（比如 "Content/Characters/Human/Human_male.png" 或是衣服），都剥离出纯文件名
            string filename = Path.GetFileNameWithoutExtension(originalPath);

            // 统一指向：LocalMods/Barotrauma Die Hard/Shadows/[文件名]_shadow.png
            string shadowDir = Path.Combine(modPackage.Dir, "Shadows");
            string shadowPath = Path.Combine(shadowDir, $"{filename}_shadow.png");
            shadowPath = Path.GetFullPath(shadowPath);

            if (File.Exists(shadowPath))
            {
                DebugConsole.NewMessage($"[ShadowDebug] 💚 成功命中目标！在指定 Shadows 目录加载: \"{filename}_shadow.png\"", Microsoft.Xna.Framework.Color.Lime);
                try
                {
                    Texture2D shadowTex = TextureLoader.FromFile(shadowPath);
                    if (shadowTex != null)
                    {
                        CustomShadowTextureCache[sprite.Texture] = shadowTex;
                        return shadowTex;
                    }
                }
                catch (Exception e)
                {
                    LuaCsLogger.LogError($"[DieHardShadow] 从指定文件夹加载影子失败: {shadowPath}, 错误: {e.Message}");
                }
            }

            return null;
        }

        [HarmonyPatch(typeof(Ragdoll), "Draw")]
        [HarmonyPrefix]
        public static void PrefixDrawCharacterShadow(Ragdoll __instance, SpriteBatch spriteBatch)
        {
            Character character = __instance.Character;

            if (character == null || spriteBatch == null || character.IsDead || character.Removed) return;
            
            Hull currentHull = character.CurrentHull;
            if (currentHull == null) return; 

            Limb mainTorso = __instance.GetLimb(LimbType.Torso);
            if (mainTorso?.Sprite == null) return;

            double currentTime = Timing.TotalTime;

            if (!CharShadowCache.TryGetValue(character, out CharShadowData cache) || 
                currentTime - cache.LastUpdateTime >= UpdateIntervalSeconds)
            {
                cache = CalculateCharShadowData(character, currentHull, currentTime);
                CharShadowCache[character] = cache;
            }

            if (!cache.IsSplitShadow && cache.SingleOffset == Vector2.Zero) return;

            if (__instance.Limbs != null)
            {
                foreach (Limb limb in __instance.Limbs)
                {
                    if (limb == null || limb.IsSevered) continue;
                    
                    var targetSprites = GetAllValidSpritesForLimb(character, limb);
                    if (targetSprites.Count == 0) continue;

                    foreach (var sprite in targetSprites)
                    {
                        if (sprite?.Texture == null) continue;
                        RenderSingleSpriteShadow(limb, sprite, character, spriteBatch, cache);
                    }
                }
            }
        }

        /// <summary>
        /// 💡 核心渲染：完美理顺衣服影子与身体影子的深度关系
        /// </summary>
        private static void RenderSingleSpriteShadow(Limb limb, Sprite targetSprite, Character character, SpriteBatch spriteBatch, CharShadowData cache)
        {
            Vector2 baseDrawPos = new Vector2(limb.DrawPosition.X, -limb.DrawPosition.Y);
            float drawRotation = -limb.Rotation; 
            
            SpriteEffects spriteEffects = character.AnimController.Dir > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            Texture2D textureToDraw = GetOrCreateShadowTexture(targetSprite);
            Color finalShadowColor;

            if (textureToDraw != null)
            {
                // 如果是手画的全黑影子，100% 还原纯黑
                finalShadowColor = Color.White;
            }
            else
            {
                // 没画影子的组件（比如未修改的原版手脚），使用灰色半透明保底
                textureToDraw = targetSprite.Texture;
                finalShadowColor = new Color(28, 28, 30, 200);
            }

            // 🌟【彻底解决抢图层闪烁的黄金微调】
            // 让影子的渲染深度紧紧贴在它自己对应的“本体图层”的正下方（也就是 Depth + 0.0001f）
            // 潜渊症中：衣服的 targetSprite.Depth 本身就比皮肤浅。这样一来，衣服影子就会天然盖在身体影子的上方，完美符合物理规律！
            float shadowDepth = targetSprite.Depth + 0.0001f;

            if (cache.IsSplitShadow)
            {
                spriteBatch.Draw(textureToDraw, baseDrawPos + cache.SplitOffsetL, targetSprite.SourceRect, finalShadowColor, 
                                 drawRotation, targetSprite.Origin, limb.Scale, spriteEffects, shadowDepth);

                spriteBatch.Draw(textureToDraw, baseDrawPos + cache.SplitOffsetR, targetSprite.SourceRect, finalShadowColor, 
                                 drawRotation, targetSprite.Origin, limb.Scale, spriteEffects, shadowDepth);
            }
            else
            {
                spriteBatch.Draw(textureToDraw, baseDrawPos + cache.SingleOffset, targetSprite.SourceRect, finalShadowColor, 
                                 drawRotation, targetSprite.Origin, limb.Scale, spriteEffects, shadowDepth);
            }
        }

        /// <summary>
        /// 🌟【图层收集】：第4步会自动把人类身体本体（皮肤）搜集进来
        /// </summary>
        private static List<Sprite> GetAllValidSpritesForLimb(Character character, Limb limb)
        {
            List<Sprite> sprites = new List<Sprite>();
            if (limb == null) return sprites;

            // 1. 处理头部的特殊逻辑
            if (limb.type == LimbType.Head)
            {
                if (limb.HairWithHatSprite?.Sprite?.Texture != null)
                {
                    if (limb.HairWithHatSprite.Sprite.SourceRect.Width > 0 && 
                        limb.HairWithHatSprite.Sprite.SourceRect.Width < limb.HairWithHatSprite.Sprite.Texture.Width)
                    {
                        sprites.Add(limb.HairWithHatSprite.Sprite);
                    }
                }
                
                if (limb.ActiveSprite?.Texture != null)
                {
                    if (limb.ActiveSprite.SourceRect.Width > 0 && 
                        limb.ActiveSprite.SourceRect.Width < limb.ActiveSprite.Texture.Width)
                    {
                        sprites.Add(limb.ActiveSprite);
                    }
                }
            }

            // 2. 抓取标准的衣物、潜水服等
            if (limb.WearingItems != null && limb.WearingItems.Count > 0)
            {
                for (int i = limb.WearingItems.Count - 1; i >= 0; i--)
                {
                    var wearable = limb.WearingItems[i];
                    if (wearable?.Sprite?.Texture != null)
                    {
                        if (wearable.Sprite.SourceRect.Width > 0 && 
                            wearable.Sprite.SourceRect.Width < wearable.Sprite.Texture.Width)
                        {
                            if (!sprites.Contains(wearable.Sprite))
                            {
                                sprites.Add(wearable.Sprite);
                            }
                        }
                    }
                }
            }

            // 3. 抓取附加外部挂件
            if (limb.OtherWearables != null && limb.OtherWearables.Count > 0)
            {
                for (int i = limb.OtherWearables.Count - 1; i >= 0; i--)
                {
                    var wearable = limb.OtherWearables[i];
                    if (wearable.Type == WearableType.Husk) continue;

                    if (wearable?.Sprite?.Texture != null && 
                        wearable.Sprite.SourceRect.Width > 0 && 
                        wearable.Sprite.SourceRect.Width < wearable.Sprite.Texture.Width)
                    {
                        if (!sprites.Contains(wearable.Sprite))
                        {
                            sprites.Add(wearable.Sprite);
                        }
                    }
                }
            }

            // 4. 将肢体原本的基础常规皮肤（也就是人类身体本体骨骼！）保底塞入
            if (limb.Sprite?.Texture != null)
            {
                if (limb.Sprite.SourceRect.Width > 0 && limb.Sprite.SourceRect.Width < limb.Sprite.Texture.Width)
                {
                    if (!sprites.Contains(limb.Sprite))
                    {
                        sprites.Insert(0, limb.Sprite);
                    }
                }
            }

            return sprites;
        }

        private static CharShadowData CalculateCharShadowData(Character character, Hull currentHull, double currentTime)
        {
            CharShadowData data = new CharShadowData
            {
                LastUpdateTime = currentTime,
                IsSplitShadow = false
            };

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

                if (distanceX < CenterThresholdX)
                {
                    data.IsSplitShadow = true;
                    float sY = (lightDir.Y * CharacterShadowLength) * 0.4f; 
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
                    data.SingleOffset = new Vector2(sX, 0);
                }
            }

            return data;
        }

        private static LightComponent GetNearestActiveLightForChar(Character character, Hull currentHull)
        {
            LightComponent bestLight = null;
            float maxScore = -1f;

            var itemList = character.Submarine == null ? Item.ItemList : character.Submarine.GetItems(true);
            
            foreach (Item otherItem in itemList)
            {
                if (otherItem.CurrentHull != currentHull && (!otherItem.HasTag("lamp") || otherItem.Prefab.Identifier.Value != "flashlight")) continue;

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

        public static void ClearCharacterCache()
        {
            CharShadowCache.Clear();
            foreach (var tex in CustomShadowTextureCache.Values)
            {
                tex?.Dispose();
            }
            CustomShadowTextureCache.Clear();
            CheckedTextures.Clear();
        }
    }
}*/