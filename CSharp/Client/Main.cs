using System.Reflection;
using Barotrauma;
using HarmonyLib;
using Microsoft.Xna.Framework.Input;
using Networking;
using Barotrauma.Lights;
using Microsoft.Xna.Framework.Graphics;
using Barotrauma.IO;
using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Networking;
using Barotrauma.Extensions;

namespace BarotraumaDieHard
{
    // 继承 ACsMod 
    public partial class Main : ACsMod
    {
        public static Dictionary<string, Sprite> customSprites = new Dictionary<string, Sprite>();
        private static ContentPackage modPackage = ContentPackageManager.AllPackages.FirstOrDefault(p => p.Name == "Barotrauma Die Hard");

        partial void InitClient()
        {
            GameMain.LuaCs.Hook.Add("think", "DieHardUpdate", (args) =>
            {
                /*if (PlayerInput.KeyHit(Keys.M))
                {
                    if (MonsterLootStore.monsterstorePaddedFrame == null)
                        MonsterLootStore.CreateTestStore();
                    else
                        MonsterLootStore.Close();
                }*/

                //确保 UI 渲染
                MonsterLootStore.Update();

                return null;
            });

            // Need to init it to avoid null reference.
            BarotraumaDieHard.CustomHintManager.Init();    
                
            AddTextureToSpriteList("mediumsteelcabinet_open", "%ModDir%/Items/Containers/containers_opened.png", new Rectangle(0, 0, 149, 360), originPercentage: new Vector2(0.5f, 0.495f));
            AddTextureToSpriteList("mediumwindowedsteelcabinet_open", "%ModDir%/Items/Containers/containers_opened.png", new Rectangle(154, 0, 149, 360), originPercentage: new Vector2(0.52f, 0.495f));
            AddTextureToSpriteList("steelcabinet_open", "%ModDir%/Items/Containers/containers_opened.png", new Rectangle(313, 0, 371, 359), originPercentage: new Vector2(0.48f, 0.485f));
            AddTextureToSpriteList("medcabinet_open", "%ModDir%/Items/Containers/containers_opened.png", new Rectangle(814, 252, 210, 321), originPercentage: new Vector2(0.5f, 0.48f));
            //AddTextureToSpriteList("seccabinet_open_0", "%ModDir%/Items/Containers/containers_opened.png", new Rectangle(905, 14, 105, 160));
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

            //--item icons
            AddTextureToSpriteList("newMorphineIcon", "%ModDir%/Items/InventoryIconAtlas.png", new Rectangle(256,448,64,64));
            AddTextureToSpriteList("newFentanylIcon", "%ModDir%/Items/InventoryIconAtlas.png", new Rectangle(320,448,64,64));
            //-- ui icons
            AddTextureToSpriteList("farLookIcon", "%ModDir%/UI/UIAtlasGeneral.png", new Rectangle(345,554,19,15));
            AddTextureToSpriteList("openPalm", "%ModDir%/UI/UIAtlasGeneral.png", new Rectangle(341,585,41,50));
            AddTextureToSpriteList("doorHandle", "%ModDir%/UI/UIAtlasGeneral.png", new Rectangle(397,542,60,34), originPercentage: new Vector2(0.73f, 0));
            AddTextureToSpriteList("pickHand", "%ModDir%/UI/UIAtlasGeneral.png", new Rectangle(342,642,45,51), originPercentage: new Vector2(0.3f, 0.6f));
            AddTextureToSpriteList("ladderClimbIcon", "%ModDir%/UI/UIAtlasGeneral.png", new Rectangle(407,643,34,60));
            AddTextureToSpriteList("magnifierIcon", "%ModDir%/UI/UIAtlasGeneral.png", new Rectangle(465,530,50,54), originPercentage: new Vector2(-0.2f, -0.1f));
            


            SwapForCustomIconOnInit();

        }



        public static void AddTextureToSpriteList(string spriteKey, string filepath, Rectangle sourceRect, Vector2? offset = null, Vector2? originPercentage = null, float rotation = 0)
        {
            string modTexturePath = filepath;
            ContentPath contentPath = ContentPath.FromRaw(modPackage, modTexturePath);

            if (offset == null)
                offset = Vector2.Zero;

            // Initialize the Sprite without immediately loading the texture
            Sprite newSprite = new Sprite(contentPath.FullPath, sourceRect, offset, rotation);

            // Enable lazy loading if applicable
            newSprite.LazyLoad = true;

            // Calculate origin based on the percentage, defaulting to the center if no percentage is specified
            Vector2 origin = originPercentage.HasValue 
                ? new Vector2(sourceRect.Width * originPercentage.Value.X, sourceRect.Height * originPercentage.Value.Y) 
                : new Vector2(sourceRect.Width / 2, sourceRect.Height / 2);
            
            newSprite.Origin = origin;

            // Add the sprite to the dictionary with the specified key
            customSprites[spriteKey] = newSprite;
        }

        static void SwapForCustomIconOnInit()
        {
            if (customSprites == null) return;

            // --- 2. 在这里配置核心映射表：[物品的Identifier] -> [注册的贴图Key] ---
            var iconMapping = new Dictionary<string, string>()
            {
                { "antidama1", "newMorphineIcon" },      // 物品：antidama1 使用 newMorphineIcon
                { "antidama2",  "newFentanylIcon" },      // 物品：fentanyl  使用 newFentanylIcon
                // { "revolver",  "newPistolIcon" }       // 未来扩展只需要按照这个格式直接加在下面就行
            };

            // 获取 InventoryIcon 的反射 PropertyInfo（提取到循环外以提高效率）
            PropertyInfo iconProperty = typeof(ItemPrefab).GetProperty(nameof(ItemPrefab.InventoryIcon));
            if (iconProperty == null) return;

            // --- 3. 自动化循环遍历替换 ---
            foreach (var kvp in iconMapping)
            {
                string itemIdentifier = kvp.Key;
                string spriteKey = kvp.Value;

                // 检查自定义贴图库里是否存在该贴图
                if (customSprites.TryGetValue(spriteKey, out Sprite customSprite))
                {
                    // 寻找游戏中注册的物品 Prefab
                    var targetPrefab = MapEntityPrefab.Find(name: null, identifier: itemIdentifier.ToIdentifier(), showErrorMessages: false) as ItemPrefab;
                    
                    if (targetPrefab != null)
                    {
                        // 强行突破 private set 限制，注入新的图标引用
                        iconProperty.SetValue(targetPrefab, customSprite);
                        
                        // 可选：方便调试的日志
                        // DebugConsole.NewMessage($"[DieHard] 成功将物品 {itemIdentifier} 的背包图标动态替换为: {spriteKey}");
                    }
                    else
                    {
                        // 调试报错：如果你的 XML 里把这个物品删了或者名字打错了，这里会提示你
                        DebugConsole.ThrowError($"[DieHard Error] 找不到物品 Prefab: {itemIdentifier}，无法替换图标！");
                    }
                }
            }
        }

        
    }
}