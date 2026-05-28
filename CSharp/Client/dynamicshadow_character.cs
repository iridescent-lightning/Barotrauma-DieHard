using Barotrauma;
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
        // ==================== 🧪 独立通用阴影测试配置 ====================
        /// <summary>
        /// 🌟【模糊大影子测试开关】
        /// 设为 true:  【当前模式】全身体只在中心渲染一张通用的 global_blob_shadow.png。
        /// 设为 false: 恢复你原本的衣服、肢体碎切片动态水平影子。
        /// </summary>
        private static readonly bool EnableBlobShadowTest = true;

        /// <summary>
        /// 通用模糊影子的文件名（放在 Shadows 文件夹下）
        /// </summary>
        private static readonly string GlobalShadowFileName = "global_blob_shadow.png";

        /// <summary>
        /// 通用影子的位置微调（如果你觉得影子偏上或偏下，可以调整 Y 轴）
        /// </summary>
        private static readonly Vector2 GlobalShadowOffset = new Vector2(0f, 0f);


        // ==================== 🛠️ 原动态阴影渲染微调参数 ====================
        private static readonly float CharacterShadowLength = 65.0f; 
        private static readonly float CenterThresholdX = 20.0f;     
        private static readonly float MinOffsetX = 10.0f;          
        private static readonly double UpdateIntervalSeconds = 0.10; 

        // 🌟 核心缓存
        private static Texture2D CachedGlobalShadowTexture = null;
        private static bool AttemptedLoadingGlobalShadow = false;
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
        private static readonly ContentPackage modPackage = ContentPackageManager.AllPackages.FirstOrDefault(p => p.Name == "Barotrauma Die Hard");

        /// <summary>
        /// 获取或加载通用的全身模糊影子
        /// </summary>
        private static Texture2D GetGlobalBlobTexture()
        {
            if (CachedGlobalShadowTexture != null) return CachedGlobalShadowTexture;
            if (AttemptedLoadingGlobalShadow || modPackage == null) return null;

            AttemptedLoadingGlobalShadow = true;
            string shadowPath = Path.Combine(modPackage.Dir, "Shadows", GlobalShadowFileName);
            shadowPath = Path.GetFullPath(shadowPath);

            if (File.Exists(shadowPath))
            {
                try
                {
                    CachedGlobalShadowTexture = TextureLoader.FromFile(shadowPath);
                    DebugConsole.NewMessage($"[ShadowDebug] 💚 成功加载通用全身影子: \"{GlobalShadowFileName}\"", Microsoft.Xna.Framework.Color.Lime);
                }
                catch (Exception e)
                {
                    LuaCsLogger.LogError($"[DieHardShadow] 加载通用影子失败: {shadowPath}, 错误: {e.Message}");
                }
            }
            return CachedGlobalShadowTexture;
        }

        /// <summary>
        /// 原版肢体重定向寻路（EnableBlobShadowTest 为 false 时生效）
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

            string filename = Path.GetFileNameWithoutExtension(originalPath);
            string shadowPath = Path.Combine(modPackage.Dir, "Shadows", $"{filename}_shadow.png");
            shadowPath = Path.GetFullPath(shadowPath);

            if (File.Exists(shadowPath))
            {
                try
                {
                    Texture2D shadowTex = TextureLoader.FromFile(shadowPath);
                    if (shadowTex != null)
                    {
                        CustomShadowTextureCache[sprite.Texture] = shadowTex;
                        return shadowTex;
                    }
                }
                catch (Exception) { }
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

            // ==================== 🚀 模式一：通用全身大影子 ====================
            if (EnableBlobShadowTest)
            {
                Texture2D globalShadow = GetGlobalBlobTexture();
                if (globalShadow == null) return;

                // 以前胸/躯干中心作为渲染原点
                Vector2 drawPos = new Vector2(mainTorso.DrawPosition.X, -mainTorso.DrawPosition.Y) + GlobalShadowOffset;
                float rotation = -mainTorso.Rotation;
                SpriteEffects spriteEffects = character.AnimController.Dir > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

                // 🌟【梯子变暗/穿帮核心修复】
                float shadowDepth;
                if (character.IsClimbing)
                {
                    // 当角色在爬梯子时，肢体的原生 Depth 会暴增到 0.8x 以上导致影子被背景吞掉
                    // 这里我们给它一个强制的安全深度偏移（紧贴在当前躯干深度的后面一点点，但不允许超过极限）
                    shadowDepth = 0.89f;
                }
                else
                {
                    // 正常站立或奔跑时，维持原有的安全退后深度
                    shadowDepth = mainTorso.Sprite.Depth + 0.05f;
                }
                if (shadowDepth > 1.0f) shadowDepth = 0.999f;

                // 居中绘制单张通用纹理
                Vector2 origin = new Vector2(globalShadow.Width / 2f, globalShadow.Height / 2f);
                spriteBatch.Draw(globalShadow, drawPos, null, Color.White, rotation, origin, mainTorso.Scale, spriteEffects, shadowDepth);
                
                return; // 直接结束，不再走下面的碎肢体循环
            }

            // ==================== 🔄 模式二：原本的碎肢体动态光阴影 ====================
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
                    foreach (var sprite in targetSprites)
                    {
                        if (sprite?.Texture == null) continue;
                        
                        Vector2 baseDrawPos = new Vector2(limb.DrawPosition.X, -limb.DrawPosition.Y);
                        float drawRotation = -limb.Rotation; 
                        SpriteEffects spriteEffects = character.AnimController.Dir > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

                        Texture2D textureToDraw = GetOrCreateShadowTexture(sprite) ?? sprite.Texture;
                        Color finalShadowColor = GetOrCreateShadowTexture(sprite) != null ? Color.White : new Color(28, 28, 30, 200);

                        float shadowDepth = sprite.Depth + 0.05f;
                        if (shadowDepth > 1.0f) shadowDepth = 0.999f;

                        if (cache.IsSplitShadow)
                        {
                            spriteBatch.Draw(textureToDraw, baseDrawPos + cache.SplitOffsetL, sprite.SourceRect, finalShadowColor, drawRotation, sprite.Origin, limb.Scale, spriteEffects, shadowDepth);
                            spriteBatch.Draw(textureToDraw, baseDrawPos + cache.SplitOffsetR, sprite.SourceRect, finalShadowColor, drawRotation, sprite.Origin, limb.Scale, spriteEffects, shadowDepth);
                        }
                        else
                        {
                            spriteBatch.Draw(textureToDraw, baseDrawPos + cache.SingleOffset, sprite.SourceRect, finalShadowColor, drawRotation, sprite.Origin, limb.Scale, spriteEffects, shadowDepth);
                        }
                    }
                }
            }
        }

        private static List<Sprite> GetAllValidSpritesForLimb(Character character, Limb limb)
        {
            List<Sprite> sprites = new List<Sprite>();
            if (limb == null) return sprites;

            if (limb.type == LimbType.Head)
            {
                if (limb.HairWithHatSprite?.Sprite?.Texture != null) sprites.Add(limb.HairWithHatSprite.Sprite);
                if (limb.ActiveSprite?.Texture != null) sprites.Add(limb.ActiveSprite);
            }
            if (limb.WearingItems != null)
            {
                foreach (var wearable in limb.WearingItems)
                {
                    if (wearable?.Sprite?.Texture != null && !sprites.Contains(wearable.Sprite)) sprites.Add(wearable.Sprite);
                }
            }
            if (limb.Sprite?.Texture != null && !sprites.Contains(limb.Sprite)) sprites.Insert(0, limb.Sprite);

            return sprites;
        }

        private static CharShadowData CalculateCharShadowData(Character character, Hull currentHull, double currentTime)
        {
            CharShadowData data = new CharShadowData { LastUpdateTime = currentTime, IsSplitShadow = false };
            LightComponent bestLight = GetNearestActiveLightForChar(character, currentHull);
            if (bestLight == null || bestLight.item == null) return data;

            Vector2 diff = character.WorldPosition - bestLight.item.WorldPosition;
            float distanceX = Math.Abs(diff.X);

            if (diff.LengthSquared() > 0.1f)
            {
                Vector2 lightDir = Vector2.Normalize(diff);
                if (distanceX < CenterThresholdX)
                {
                    data.IsSplitShadow = true;
                    data.SplitOffsetL = new Vector2(-MinOffsetX, 0f);
                    data.SplitOffsetR = new Vector2(MinOffsetX, 0f);
                }
                else
                {
                    float sX = lightDir.X * CharacterShadowLength;
                    if (sX > 0 && sX < MinOffsetX) sX = MinOffsetX;
                    if (sX < 0 && sX > -MinOffsetX) sX = -MinOffsetX;
                    data.SingleOffset = new Vector2(sX, 0f);
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
                    float currentScore = (1.0f - (dist / light.Range)) * (light.LightColor.A / 255f);
                    if (currentScore > maxScore) { maxScore = currentScore; bestLight = light; }
                }
            }
            return bestLight;
        }

        public static void ClearCharacterCache()
        {
            CharShadowCache.Clear();
            foreach (var tex in CustomShadowTextureCache.Values) tex?.Dispose();
            CustomShadowTextureCache.Clear();
            CheckedTextures.Clear();
            CachedGlobalShadowTexture?.Dispose();
            CachedGlobalShadowTexture = null;
            AttemptedLoadingGlobalShadow = false;
        }
    }
}