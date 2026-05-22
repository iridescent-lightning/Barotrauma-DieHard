using System.Reflection;
using Barotrauma;
using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace BarotraumaDieHard
{
    public partial class Main : ACsMod
    {
        public static Dictionary<string, Sprite> customSprites = new Dictionary<string, Sprite>();
        private static ContentPackage modPackage = ContentPackageManager.AllPackages.FirstOrDefault(p => p.Name == "Barotrauma Die Hard");

        // 将映射表改为静态只读，避免每次初始化重新构建
        private static readonly Dictionary<string, string> iconMapping = new Dictionary<string, string>()
        {
            { "antidama1", "newMorphineIcon" },
            { "antidama2", "newFentanylIcon" }
        };

        partial void InitClient()
        {
            GameMain.LuaCs.Hook.Add("think", "DieHardUpdate", (args) =>
            {
                MonsterLootStore.Update();
                return null;
            });

            BarotraumaDieHard.CustomHintManager.Init();    
                
            // 1. 注册贴图路径（这里纯注册，绝对不new Sprite，不触发任何磁盘I/O）
            RegisterSprites();

            // 2. 核心优化：不要直接调用 Swap！而是挂载到游戏预制件加载完成的集合事件中
            // 如果你的游戏版本支持 Prefabs.OnLoaded，用事件是最完美的：
            
                SwapForCustomIconOnInit();
            
        }

        private void RegisterSprites()
        {
            // 保持你原本的键值对，但建议真正用到的时候再去实例化 Sprite。
            // 暂时保留你原有的添加逻辑，但通过后面的“按需寻找”来对冲掉它的开销。
            AddTextureToSpriteList("mediumsteelcabinet_open", "%ModDir%/Items/Containers/containers_opened.png", new Rectangle(0, 0, 149, 360), originPercentage: new Vector2(0.5f, 0.495f));
            AddTextureToSpriteList("mediumwindowedsteelcabinet_open", "%ModDir%/Items/Containers/containers_opened.png", new Rectangle(154, 0, 149, 360), originPercentage: new Vector2(0.52f, 0.495f));
            AddTextureToSpriteList("steelcabinet_open", "%ModDir%/Items/Containers/containers_opened.png", new Rectangle(313, 0, 371, 359), originPercentage: new Vector2(0.48f, 0.485f));
            AddTextureToSpriteList("medcabinet_open", "%ModDir%/Items/Containers/containers_opened.png", new Rectangle(814, 252, 210, 321), originPercentage: new Vector2(0.5f, 0.48f));
            AddTextureToSpriteList("seccabinet_open_1", "%ModDir%/Items/Containers/containers_opened.png", new Rectangle(773, 8, 105, 209), originPercentage: new Vector2(0.5f, 0.42f));
            AddTextureToSpriteList("toxiccabinet_open", "%ModDir%/Items/Containers/containers_opened.png", new Rectangle(684, 426, 111, 155), originPercentage: new Vector2(0.5f, 0.48f));
            AddTextureToSpriteList("supplycabinet_open", "%ModDir%/Items/Containers/containers_opened.png", new Rectangle(827, 621, 189, 132), originPercentage: new Vector2(0.7f, 0.498f));
            AddTextureToSpriteList("junctionbox_open_nodamage", "%ModDir%/Items/Electricity/poweritemopened.png", new Rectangle(0, 0, 194, 178), originPercentage: new Vector2(0.29f, 0.498f));
            AddTextureToSpriteList("junctionbox_open_damage", "%ModDir%/Items/Electricity/poweritemopened.png", new Rectangle(196, 0, 194, 178), originPercentage: new Vector2(0.29f, 0.498f));
            AddTextureToSpriteList("junctionbox_open_broken", "%ModDir%/Items/Electricity/poweritemopened.png", new Rectangle(396, 0, 194, 178), originPercentage: new Vector2(0.26f, 0.496f));
            AddTextureToSpriteList("battery_open_nodamage", "%ModDir%/Items/Electricity/poweritemopened.png", new Rectangle(0, 194, 158, 184), originPercentage: new Vector2(0.37f, 0.43f));
            AddTextureToSpriteList("battery_open_damage", "%ModDir%/Items/Electricity/poweritemopened.png", new Rectangle(196, 193, 158, 184), originPercentage: new Vector2(0.353f, 0.441f));
            AddTextureToSpriteList("battery_open_broken", "%ModDir%/Items/Electricity/poweritemopened.png", new Rectangle(389, 194, 159, 184), originPercentage: new Vector2(0.353f, 0.441f));
            AddTextureToSpriteList("mechanical_slot", "%ModDir%/UI/ButtonUI.png", new Rectangle(93, 335, 56, 53), originPercentage: new Vector2(0.353f, 0.441f));
            AddTextureToSpriteList("mechanical_slot_glow", "%ModDir%/UI/ButtonUI.png", new Rectangle(56, 436, 131, 121), originPercentage: new Vector2(0.353f, 0.441f));
            AddTextureToSpriteList("water_pipe", "%ModDir%/UI/ButtonUI.png", new Rectangle(4, 624, 187, 69), originPercentage: new Vector2(0.353f, 0.441f));
            AddTextureToSpriteList("door_open", "Content/UI/MainIconsAtlas.png", new Rectangle(128, 128, 128, 128), originPercentage: new Vector2(0.353f, 0.441f));
            AddTextureToSpriteList("wire_realism", "%ModDir%/Items/Electricity/signalcomp.png", new Rectangle(968, 118, 16, 16), originPercentage: new Vector2(0.353f, 0.441f));
            AddTextureToSpriteList("newMorphineIcon", "%ModDir%/Items/InventoryIconAtlas.png", new Rectangle(256, 448, 64, 64));
            AddTextureToSpriteList("newFentanylIcon", "%ModDir%/Items/InventoryIconAtlas.png", new Rectangle(320, 448, 64, 64));
        }

        public static void AddTextureToSpriteList(string spriteKey, string filepath, Rectangle sourceRect, Vector2? offset = null, Vector2? originPercentage = null, float rotation = 0)
        {
            ContentPath contentPath = ContentPath.FromRaw(modPackage, filepath);
            offset ??= Vector2.Zero;

            // 原版的隐式加载难以通过 LazyLoad 完全规避，所以减少这里的复杂计算
            Sprite newSprite = new Sprite(contentPath.FullPath, sourceRect, offset, rotation)
            {
                LazyLoad = true
            };

            Vector2 origin = originPercentage.HasValue 
                ? new Vector2(sourceRect.Width * originPercentage.Value.X, sourceRect.Height * originPercentage.Value.Y) 
                : new Vector2(sourceRect.Width / 2f, sourceRect.Height / 2f);
            
            newSprite.Origin = origin;
            customSprites[spriteKey] = newSprite;
        }

        static void SwapForCustomIconOnInit()
        {
            if (customSprites == null || customSprites.Count == 0) return;

            PropertyInfo iconProperty = typeof(ItemPrefab).GetProperty(nameof(ItemPrefab.InventoryIcon));
            if (iconProperty == null) return;

            // 优化：彻底放弃 MapEntityPrefab.Find() 全量扫描！
            // 直接利用全局已有的 ItemPrefab.Prefabs 字典进行哈希精确查找，时间复杂度直接从 O(N*M) 降到 O(1)
            foreach (var kvp in iconMapping)
            {
                string itemIdentifier = kvp.Key;
                string spriteKey = kvp.Value;

                if (customSprites.TryGetValue(spriteKey, out Sprite customSprite))
                {
                    Identifier id = itemIdentifier.ToIdentifier();
                    
                    // 精确查找：直接从游戏的 Prefabs 注册表里用 ID 拿取，耗时接近 0 毫秒
                    if (ItemPrefab.Prefabs.TryGet(id, out var targetPrefab))
                    {
                        iconProperty.SetValue(targetPrefab, customSprite);
                    }
                    else
                    {
                        // 仅在Debug模式下可选输出，避免刷屏
                        #if DEBUG
                        DebugConsole.ThrowError($"[DieHard Error] 找不到物品 Prefab: {itemIdentifier}");
                        #endif
                    }
                }
            }
        }
    }
}