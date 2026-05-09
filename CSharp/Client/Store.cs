﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Barotrauma;
using System.Xml.Linq;
using Barotrauma.Items.Components;
using Networking;
using Barotrauma.Networking;
using PlayerBalanceElement = Barotrauma.CampaignUI.PlayerBalanceElement;

namespace BarotraumaDieHard
{
    public partial class MonsterLootStore
    {
        private GUIComponent frame;
        private GUIListBox itemList;
        private List<ItemPrefab> monsterLootPrefabs;

        // 改为 public 方便 Main 类清理
        public static GUIFrame monsterstorePaddedFrame;

        // 存储当前特价物品的标识符
    private static Identifier dailySpecialId = Identifier.Empty;
    private const float SpecialMultiplier = 2.0f; // 两倍价格
    private int Balance
    {
        get
        {
            // 1. 如果是在多人模式（GameMain.Client 不为空）
            if (GameMain.Client != null)
            {
                // 返回当前控制角色的钱包余额，如果无效则返回 0
                return Character.Controlled?.Wallet?.Balance ?? 0;
            }
            
            // 2. 如果是单机模式
            if (GameMain.GameSession?.Campaign != null)
            {
                // 返回战役的公共余额
                return GameMain.GameSession.Campaign.GetBalance();
            }

            return 0;
        }
    }

        public MonsterLootStore(GUIComponent parent)
        {
            // 修正 RectTransform
            frame = new GUIFrame(new RectTransform(new Vector2(0.6f, 0.7f), parent.RectTransform, Anchor.Center));
            frame.Color = Color.DarkOliveGreen * 0.9f; // 稍微加深透明度

            // 添加一个关闭按钮，否则你进去了出不来
            var closeBtn = new GUIButton(new RectTransform(new Vector2(0.1f, 0.05f), frame.RectTransform, Anchor.TopRight), "X")
            {
                OnClicked = (btn, obj) => 
                {
                    Close();
                    return true;
                }
            };

            itemList = new GUIListBox(new RectTransform(new Vector2(0.95f, 0.8f), frame.RectTransform, Anchor.TopCenter) { RelativeOffset = new Vector2(0, 0.1f) });

            monsterLootPrefabs = ItemPrefab.Prefabs
                .Where(p => p.Tags.Contains("creature_loot"))
                .ToList();

            RefreshList();
        }

        private int GetPlayerAndSubItemCount(ItemPrefab prefab)
        {
            if (Character.Controlled == null) return 0;

            int totalCount = 0;

            // 1. 扫描玩家身上的物品（包括背包里的容器）
            if (Character.Controlled.Inventory != null)
            {
                totalCount += Character.Controlled.Inventory.AllItemsMod
                    .Count(i => i.Prefab == prefab && !i.Removed);
            }

            // 2. 扫描潜艇上所有的物品
            // 我们遍历全图物品，但只针对在当前潜艇内且未被固定的物品
                totalCount += Item.ItemList.Count(i => 
                    i.Prefab == prefab &&
                    i.Submarine == Submarine.MainSub &&
                    // 只有在物品不在玩家包里时才计算（防止重复统计）
                    i.ParentInventory?.Owner is not Character && 
                    // 安全检查：获取 Holdable 组件，如果不存在则 IsAttached 视为 false
                    !(i.GetComponent<Holdable>()?.IsAttached ?? false)
                );
            
            return totalCount;
        }

        private int GetItemPrice(ItemPrefab prefab)
        {
            // --- 核心修改：读取 XML ---
            /*var lootElement = prefab.ConfigElement.GetChildElement("LootValue");
            if (lootElement != null)
            {
                return lootElement.GetAttributeInt("value", 0);
            }
            return 0;*/

            // 直接从 <Item ...> 标签读取 "lootvalue" 属性
            // GetAttributeInt 是 Barotrauma 对 XElement 的扩展方法，非常方便
            int price = prefab.ConfigElement.GetAttributeInt("lootvalue", 0);
                return price;
        }

        public void RefreshList()
        {
            itemList.Content.ClearChildren();
            UpdateDailySpecial(monsterLootPrefabs);
            // 获取性格乘数和性格名称
            float personalityMult = GetPersonalityMultiplier();
            var controlled = Character.Controlled;
            string traitName = controlled?.Info?.PersonalityTrait?.Identifier.Value ?? "None";
            // 转换成可读性更好的格式，比如 "laid-back" -> "Laid-back"
            if (!string.IsNullOrEmpty(traitName)) 
                traitName = char.ToUpper(traitName[0]) + traitName.Substring(1);

            // --- 新增：左上角性格信息显示 ---
            var personalityInfoGroup = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.1f), itemList.Content.RectTransform), isHorizontal: true);
            
            // 标签文本
            new GUITextBlock(new RectTransform(new Vector2(0.3f, 1.0f), personalityInfoGroup.RectTransform), 
                TextManager.Get("creature_loot_store.sellerpersonality").Value, font: GUIStyle.SubHeadingFont);

            // 性格名称 (根据乘数上色)
            Color traitColor = personalityMult > 1.0f ? Color.LightGreen : (personalityMult < 1.0f ? Color.Orange : Color.White);
            new GUITextBlock(new RectTransform(new Vector2(0.7f, 1.0f), personalityInfoGroup.RectTransform), 
                traitName, color: traitColor, font: GUIStyle.SubHeadingFont)
            {
                ToolTip = personalityMult > 1.0f ? TextManager.Get("creature_loot_store.sellerpersonalitypositive").Value : 
                        (personalityMult < 1.0f ? TextManager.Get("creature_loot_store.sellerpersonalitynegative").Value : TextManager.Get("creature_loot_store.sellerpersonalitynutruel").Value)
            };

            // 分割线，区分标题和列表
            new GUIFrame(new RectTransform(new Vector2(1.0f, 0.01f), itemList.Content.RectTransform), style: "HorizontalLine");

            var sortedPrefabs = monsterLootPrefabs
                .OrderByDescending(p => p.Identifier == dailySpecialId)
                .ThenByDescending(p => GetPlayerAndSubItemCount(p) > 0)
                .ToList();

            bool hasDrawnSpecialHeader = false;
            bool specialSectionEnded = false;

            foreach (var prefab in sortedPrefabs)
            {
                int count = GetPlayerAndSubItemCount(prefab);
                int basePrice = GetItemPrice(prefab);
                bool isSpecial = prefab.Identifier == dailySpecialId;

                // --- 逻辑修改：计算价格 ---
            // 最终价格 = 基础价格 * 特价倍数(如果是) * 性格倍数
            float specialMult = isSpecial ? SpecialMultiplier : 1.0f;
            int finalPrice = (int)(basePrice * specialMult * personalityMult);

                // 1. 绘制大写的“DAILY SPECIALS”标题
                if (isSpecial && !hasDrawnSpecialHeader)
                {
                    var headerGroup = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.15f), itemList.Content.RectTransform), isHorizontal: true, childAnchor: Anchor.CenterLeft);
                    
                    new GUIImage(new RectTransform(new Vector2(0.12f, 0.8f), headerGroup.RectTransform), "StoreDealIcon", scaleToFit: true)
                    {
                        Color = Color.LightGreen,
                        CanBeFocused = false
                    };

                    string headerText = TextManager.Get("campaignstore.dailyspecials").Value.ToUpper();
                    new GUITextBlock(new RectTransform(new Vector2(0.8f, 0.8f), headerGroup.RectTransform), headerText, font: GUIStyle.LargeFont)
                    {
                        TextColor = Color.LightGreen,
                        Padding = new Vector4(10, 0, 0, 0),
                        CanBeFocused = false
                    };
                    
                    hasDrawnSpecialHeader = true;
                }

                // 2. 绘制分割线
                if (!isSpecial && !specialSectionEnded && dailySpecialId != Identifier.Empty)
                {
                    new GUIFrame(new RectTransform(new Vector2(1.0f, 0.02f), itemList.Content.RectTransform), style: "HorizontalLine");
                    specialSectionEnded = true;
                }

                //int finalPrice = isSpecial ? (int)(basePrice * SpecialMultiplier) : basePrice;
                var row = new GUIFrame(new RectTransform(new Vector2(1.0f, 0.22f), itemList.Content.RectTransform), style: "ListBoxElement");
                
                // 物品图标
                new GUIImage(new RectTransform(new Vector2(0.15f, 0.9f), row.RectTransform, Anchor.CenterLeft), prefab.InventoryIcon, scaleToFit: true);

                // 文字区域
                var textGroup = new GUILayoutGroup(new RectTransform(new Vector2(0.5f, 0.9f), row.RectTransform) { RelativeOffset = new Vector2(0.2f, 0.05f) });
                
                new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.4f), textGroup.RectTransform), prefab.Name, color: isSpecial ? Color.Gold : Color.LimeGreen, font: GUIStyle.SubHeadingFont);
                
                var priceLine = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.3f), textGroup.RectTransform), isHorizontal: true);
                
                // --- 核心修改：统一显示逻辑 ---
                if (isSpecial)
                {
                    // 特价物品显示：库存 | 特价 | 原价
                    new GUITextBlock(new RectTransform(new Vector2(0.35f, 1.0f), priceLine.RectTransform), TextManager.Get("creature_loot_store.stock").Value + $"{count} | ", font: GUIStyle.Font);
                    new GUITextBlock(new RectTransform(new Vector2(0.3f, 1.0f), priceLine.RectTransform), TextManager.Get("creature_loot_store.specailoffer").Value + $"{finalPrice} MK", color: Color.LightGreen, font: GUIStyle.Font);
                    new GUITextBlock(new RectTransform(new Vector2(0.35f, 1.0f), priceLine.RectTransform), TextManager.Get("creature_loot_store.originalprice").Value + $"{basePrice} mk", color: Color.Gray * 0.8f, font: GUIStyle.Font);
                }
                else
                {
                    // 普通：如果性格影响了价格，我们可以把颜色变一下提醒玩家
            Color priceColor = personalityMult > 1.0f ? Color.LightGreen : (personalityMult < 1.0f ? Color.Orange : Color.White);
            
            new GUITextBlock(new RectTransform(new Vector2(1.0f, 1.0f), priceLine.RectTransform), 
                $"{TextManager.Get("creature_loot_store.stock").Value}{count} | {finalPrice} mk", 
                color: priceColor, font: GUIStyle.SmallFont);
                }

                // 出售按钮
                var sellBtn = new GUIButton(new RectTransform(new Vector2(0.2f, 0.6f), row.RectTransform, Anchor.CenterRight), TextManager.Get("creature_loot_store.sellbuttontext").Value)
                {
                    Enabled = count > 0,
                    OnClicked = (btn, obj) => {
                        SellOneItem(prefab, finalPrice);
                        return true;
                    }
                };
            }
            // --- 4. 底部：显示玩家金钱余额 (利用你定义的 Balance 属性) ---
            // 在内容区域末尾添加一个统计行
            // 将高度设为父容器的 10% (0.1f)，并锚定在右下角 (BottomRight)
            var footer = new GUIFrame(new RectTransform(new Vector2(0.1f, 0.1f), monsterstorePaddedFrame.RectTransform, Anchor.BottomRight){
                            RelativeOffset = new Vector2(0.24f, 0.13f)
                            
                        }, style: null)
            {
                CanBeFocused = false
            };


            var moneyDisplay = new GUITextBlock(new RectTransform(new Vector2(1.0f, 1.0f), footer.RectTransform), 
                $"{Balance} mk", 
                color: Color.Gold, font: GUIStyle.SubHeadingFont, textAlignment: Alignment.CenterRight)
            {
                Padding = new Vector4(0, 0, 20, 0) // 离右边边缘留点距离
            };

        }

        private void SellOneItem(ItemPrefab prefab, int price)
        {
            var controlled = Character.Controlled;
            if (controlled == null) return;

            // 1. 寻找符合条件的物品（遵循之前的扫描过滤逻辑）
            Item itemToSell = controlled.Inventory?.AllItemsMod.FirstOrDefault(i => i.Prefab == prefab);

            if (itemToSell == null)
            {
                itemToSell = Item.ItemList.FirstOrDefault(i => 
                    i.Prefab == prefab &&
                    i.Submarine == Submarine.MainSub &&
                    i.ParentInventory?.Owner is not Character &&
                    !(i.GetComponent<Holdable>()?.IsAttached ?? false)
                );
            }

            // 2. 执行扣除与给钱
            if (itemToSell != null)
            {
                Entity.Spawner.AddEntityToRemoveQueue(itemToSell);
                SendSellItemMessage(itemToSell);

                // 直接调用 Character 类下的 GiveMoney 方法
                // 它会自动处理 Wallet 分发、余额增加和 UI 事件通知
                controlled.GiveMoney(price);

                // 记录到游戏分析器（参考 GameAnalyticsManager.cs）
                GameAnalyticsManager.AddMoneyGainedEvent(
                    price, 
                    GameAnalyticsManager.MoneySource.Store, 
                    prefab.Identifier.Value);

                SoundPlayer.PlayUISound(GUISoundType.PickItem);
                CoroutineManager.Invoke(() =>
                {
                    RefreshList();
                }, 0.01f);
                }
        }

        public static void CreateTestStore()
        {
            // 1. 检查是否已经存在，防止每帧重复创建
            if (monsterstorePaddedFrame != null) return; 

            // 2. 修正父级引用：使用 GUICanvas.Instance.RectTransform 最稳妥
            monsterstorePaddedFrame = new GUIFrame(new RectTransform(Vector2.One, GUICanvas.Instance), style: null)
            {
                CanBeFocused = true,
                Color = Color.Black * 0.6f // 增加明显的黑色背景用来测试
            };

            var storeUI = new MonsterLootStore(monsterstorePaddedFrame);

            // 3. 开启输入屏蔽（否则没鼠标）
            //GUI.PreventInput = true;
            
            LuaCsLogger.Log("Monster Store UI Created!"); 
        }

        public static void Update() 
        {
            if (MonsterLootStore.monsterstorePaddedFrame != null)
            {
                MonsterLootStore.monsterstorePaddedFrame.AddToGUIUpdateList();
                // 如果需要显示鼠标
                // GUI.PreventInput = true;
            }
        }

        public static void Close()
        {
            if (monsterstorePaddedFrame != null)
            {
                monsterstorePaddedFrame.Parent?.RemoveChild(monsterstorePaddedFrame);
                monsterstorePaddedFrame = null;
                
            }
        }

        private void SendSellItemMessage(Item item)
        {
            // 创建网络消息
            IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.STORE_SELL);

            msg.WriteUInt16(item.ID);    // 告诉服务器卖哪个物品
            
            
            // 发送给服务器
            NetUtil.SendServer(msg, DeliveryMethod.Reliable);
        }


            public static void UpdateDailySpecial(List<ItemPrefab> prefabs)
        {
            // 如果还没有特价物品，或者你想根据某种周期刷新，在这里逻辑处理
            if (dailySpecialId == Identifier.Empty && prefabs.Count > 0)
            {
                // 随机挑选一个
                var random = new Random();
                dailySpecialId = prefabs[random.Next(prefabs.Count)].Identifier;
            }
        }


            private float GetPersonalityMultiplier()
        {
            var character = Character.Controlled;
            if (character?.Info?.PersonalityTrait == null) return 1.0f;

            string traitId = character.Info.PersonalityTrait.Identifier.Value.ToLower();

            return traitId switch
            {
                "laid-back" => 1.2f,    // 悠闲：卖得贵 (20% 加成)
                "rude" => 0.8f,         // 粗鲁：卖得便宜 (20% 减益)
                "brokenenglish" => GetBrokenEnglishRandom(), // 乱码英语：随机波动
                _ => 1.0f
            };
        }

        private float GetBrokenEnglishRandom()
        {
            // 产生 0.5 到 1.5 之间的随机乘数
            return 0.5f + (float)Rand.Range(0.0f, 1.0f);
        }




    }
}