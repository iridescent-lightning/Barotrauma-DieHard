using System;
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

        // 🌟 临时本地模型数据，仅在载入初始化时用一次
        private struct RawShadowParams
        {
            public float Length; public float Threshold; public float MinOffset; public float HeightFactor;
            public RawShadowParams(float l, float t, float m, float h) { Length = l; Threshold = t; MinOffset = m; HeightFactor = h; }
        }

        partial void InitClient()
        {
            GameMain.LuaCs.Hook.Add("think", "DieHardUpdate", (args) =>
            {
                MonsterLootStore.Update();
                return null;
            });

            BarotraumaDieHard.CustomHintManager.Init();    
            
            // 🌟 核心：在游戏初始化时，一次性把所有墙体预制件的过滤与计算做完！
            InitializeStructureShadowConfigs();
            InitializeItemShadowConfigs();
                
            // 1. 注册贴图路径
            RegisterSprites();

            // 2. 更换图标
            SwapForCustomIconOnInit();
        }

        /// <summary>
        /// 🌟 游戏初始化时执行一次的超高效过滤逻辑
        /// </summary>
        private static void InitializeStructureShadowConfigs()
        {
            StructureShadowPatch.AllowedPrefabConfigs.Clear();

            // 🛑 优先级 1：【绝对黑名单】
            // 凡是出现在这里的 Identifier 或 NameIdentifier，直接无视，绝不生成阴影
            HashSet<string> blacklistedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "somebadstructure", 
                "hidden_panel"
            };

            // 💡 优先级 2 & 3 合并：【专属参数与名字白名单】
            // 只要名字（Identifier 或 NameIdentifier）在这里，就自动视为在白名单内。
            // 如果有自定义参数则用自定义的，没有就自动用默认的。
            Dictionary<string, RawShadowParams> customPresets = new Dictionary<string, RawShadowParams>(StringComparer.OrdinalIgnoreCase)
            {
                // 带有专属参数的结构物
                { "CableHolderHorizontal", new RawShadowParams(5f, 5f, 5f, 0.15f) },
                { "CableHolderVertical", new RawShadowParams(5f, 5f, 5f, 0.15f) },
                { "decobarrier1", new RawShadowParams(55f, 5f, 35f, 0.2f) },

                // 仅加入白名单但使用默认参数的结构物（给一个 null 或者空结构体即可）
                { "none", default },
                { "none1", default }, // 这样 CableHolderVerticalwrecked 也能通过 NameIdentifier 匹配到这里！
                { "none2", default },
                { "custombackground", default }
            };

            // 🌟 优先级 4：【子分类白名单】批量放行
            HashSet<string> allowedSubcats = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                "pipesandvents", "shop", "cafeteria", "office", "engineering", "researchmedical", "banditseparatist", "generic" 
            };

            // 开始遍历所有结构物预制件
            foreach (var prefab in StructurePrefab.Prefabs)
            {
                if (prefab == null) continue;

                // 🔍 同时抓取当前变体名、原始基础名以及子分类
                string idName = prefab.Identifier.Value ?? "";
                string origName = prefab.OriginalName ?? ""; // 对应 XML 中的 nameidentifier 
                string subcat = prefab.Subcategory ?? "";

                // 🛑 [检查 1] 绝对黑名单拦截：只要当前 ID 或 基础名命中黑名单，直接踢出
                if (blacklistedNames.Contains(idName) || blacklistedNames.Contains(origName)) 
                    continue;

                bool isAllowed = false;
                RawShadowParams targetParams = default;
                bool useCustomParams = false;

                // 🎯 [检查 2 & 3] 检查名字/原始名白名单与特殊参数字典
                if (customPresets.TryGetValue(idName, out var paramsById))
                {
                    isAllowed = true;
                    // 判断是不是只进白名单没给特殊参数 (通过判断 Length 是否为 0)
                    if (paramsById.Length > 0f)
                    {
                        targetParams = paramsById;
                        useCustomParams = true;
                    }
                }
                else if (customPresets.TryGetValue(origName, out var paramsByOrig))
                {
                    isAllowed = true;
                    if (paramsByOrig.Length > 0f)
                    {
                        targetParams = paramsByOrig;
                        useCustomParams = true;
                    }
                }
                // 📂 [检查 4] 检查子分类白名单
                else if (allowedSubcats.Contains(subcat))
                {
                    isAllowed = true;
                }

                // 🛑 如果经过上面层层筛选还是没有命中任何白名单，彻底丢弃
                if (!isAllowed) continue;

                // 5. 开始构建一劳永逸的渲染配置
                var config = new StructureShadowPatch.ShadowConfig();
                
                if (useCustomParams)
                {
                    // 使用专属自定义参数计算
                    config.ShadowLength = targetParams.Length;
                    config.CenterThresholdX = targetParams.Threshold;
                    config.MinOffsetX = targetParams.MinOffset;
                    config.HeightFactor = targetParams.HeightFactor;

                    float sX = Math.Max(targetParams.MinOffset, targetParams.Length * 0.4f);
                    float sY = targetParams.Length * targetParams.HeightFactor * 0.5f;
                    config.StaticOffset = new Vector2(sX, -sY);
                }
                else
                {
                    // 使用全局默认参数计算
                    config.ShadowLength = StructureShadowPatch.DefaultShadowLength;
                    config.CenterThresholdX = StructureShadowPatch.DefaultCenterThresholdX;
                    config.MinOffsetX = StructureShadowPatch.DefaultMinOffsetX;
                    config.HeightFactor = StructureShadowPatch.DefaultHeightFactor;
                    config.StaticOffset = StructureShadowPatch.StaticDefaultOffset;
                }

                // 6. 扔入 Draw 钩子的高速缓存字典中
                StructureShadowPatch.AllowedPrefabConfigs[prefab] = config;
            }
        }
        /// <summary>
        /// 🌟 物品光影初始化核心：彻底消除每帧在 Draw 里的检索与条件判定
        /// </summary>
        private static void InitializeItemShadowConfigs()
        {
            ItemPatch.AllowedItemConfigs.Clear();

            // 1. 本地临时专属物品自定义光影参数（加载完即释放内存）
            Dictionary<string, ItemPatch.CustomShadowParams> customItemSettings = new Dictionary<string, ItemPatch.CustomShadowParams>(StringComparer.OrdinalIgnoreCase)
            {
                { "revolver", new ItemPatch.CustomShadowParams(20.0f, 10.0f, 6.0f, 0.4f) },
                { "extinguisher", new ItemPatch.CustomShadowParams(15.0f, 12.0f, 0.3f, 0.5f) },
                { "extinguisherbracket", new ItemPatch.CustomShadowParams(15.0f, 12.0f, 0.3f, 0.5f) },
                { "reactor1", new ItemPatch.CustomShadowParams(60.0f, 15.0f, 25.0f, 0.2f) },
                { "suppliescabinet", new ItemPatch.CustomShadowParams(20.0f, 15.0f, 10.0f, 0.7f) },
                { "junctionbox", new ItemPatch.CustomShadowParams(30.0f, 15.0f, 20.0f, 0.7f) },
                { "oxygengenerator", new ItemPatch.CustomShadowParams(60.0f, 15.0f, 45.0f, 0.2f) },
                { "outpostoxygengenerator", new ItemPatch.CustomShadowParams(60.0f, 15.0f, 45.0f, 0.2f) },
                { "op_vendingmachine1", new ItemPatch.CustomShadowParams(60.0f, 15.0f, 45.0f, 0.2f) },
                { "op_vendingmachine2", new ItemPatch.CustomShadowParams(60.0f, 15.0f, 45.0f, 0.2f) }
            };

            // 2. 遍历游戏内注册的所有物品预制件 (ItemPrefab)
            foreach (var prefab in ItemPrefab.Prefabs)
            {
                if (prefab == null) continue;

                string name = prefab.Identifier.Value ?? "";
                
                // 🔍 核心改动：检查是否在配置字典里（自动放行白名单）或者是否有 DrawShadow 标签
                bool isCustomPreset = customItemSettings.ContainsKey(name);
                bool hasShadowTag = prefab.Tags.Contains("DrawShadow");

                // 🛑 拦截：既没有标签，也不在专属字典里的物品，直接被彻底过滤掉
                if (!isCustomPreset && !hasShadowTag) continue;

                // 3. 为合格的物品绑定对应的渲染参数
                ItemPatch.CustomShadowParams finalParams;
                if (customItemSettings.TryGetValue(name, out var custom))
                {
                    finalParams = custom;
                }
                else
                {
                    // 绑定全局默认物品影子参数
                    finalParams = new ItemPatch.CustomShadowParams(
                        ItemPatch.DefaultShadowLength,
                        ItemPatch.DefaultCenterThresholdX,
                        ItemPatch.DefaultMinOffsetX,
                        ItemPatch.DefaultHeightFactor
                    );
                }

                // 4. 送入全局高速缓存字典
                ItemPatch.AllowedItemConfigs[prefab] = finalParams;
            }
        }
        
        private void RegisterSprites()
        {
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
            AddTextureToSpriteList("farLookIcon", "%ModDir%/UI/UIAtlasGeneral.png", new Rectangle(345,554,19,15));
            AddTextureToSpriteList("openPalm", "%ModDir%/UI/UIAtlasGeneral.png", new Rectangle(341,585,41,50));
            AddTextureToSpriteList("doorHandle", "%ModDir%/UI/UIAtlasGeneral.png", new Rectangle(397,542,60,34), originPercentage: new Vector2(0.73f, 0));
            AddTextureToSpriteList("pickHand", "%ModDir%/UI/UIAtlasGeneral.png", new Rectangle(342,642,45,51));
            AddTextureToSpriteList("ladderClimbIcon", "%ModDir%/UI/UIAtlasGeneral.png", new Rectangle(407,643,34,60));
            AddTextureToSpriteList("magnifierIcon", "%ModDir%/UI/UIAtlasGeneral.png", new Rectangle(465,530,50,54), originPercentage: new Vector2(-0.2f, -0.1f));
        }

        public static void AddTextureToSpriteList(string spriteKey, string filepath, Rectangle sourceRect, Vector2? offset = null, Vector2? originPercentage = null, float rotation = 0)
        {
            ContentPath contentPath = ContentPath.FromRaw(modPackage, filepath);
            offset ??= Vector2.Zero;

            Sprite newSprite = new Sprite(contentPath.FullPath, sourceRect, offset, rotation) { LazyLoad = true };

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

            foreach (var kvp in iconMapping)
            {
                string itemIdentifier = kvp.Key;
                string spriteKey = kvp.Value;
                if (customSprites.TryGetValue(spriteKey, out Sprite customSprite))
                {
                    Identifier id = itemIdentifier.ToIdentifier();
                    if (ItemPrefab.Prefabs.TryGet(id, out var targetPrefab))
                    {
                        iconProperty.SetValue(targetPrefab, customSprite);
                    }
                }
            }
        }
    }
}