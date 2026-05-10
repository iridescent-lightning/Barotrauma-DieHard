using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Barotrauma.Extensions;
using Barotrauma;

namespace BarotraumaDieHard
{
    class Scavengeable : ItemComponent
    {

        // 进度属性
        private float scavengeTimer;
        private bool isScavenging;
        private Character user;
        private static List<ItemPrefab> cachedSmallItems;

        [Editable, Serialize(5.0f, IsPropertySaveable.Yes, description: "搜刮所需时间（秒）")]
        public float ScavengeDuration { get; set; }
        
        [Editable, Serialize(1, IsPropertySaveable.Yes, description: "最小生成数量")]
        public int MinLootCount { get; set; }

        [Editable, Serialize(3, IsPropertySaveable.Yes, description: "最大生成数量")]
        public int MaxLootCount { get; set; }
		
        public Scavengeable(Item item, ContentXElement element)
            : base(item, element)
        {
           
            IsActive = true;
            canBeSelected = true;
        }

        // 当玩家按下 E 键交互时触发
        public override bool Select(Character character)
        {
            if (character == null || isScavenging) return false;

            // 开始搜刮逻辑
            isScavenging = true;
            user = character;
            scavengeTimer = 0f;
            
            return false; // 返回 false 以允许原生的交互 UI 显示
        }
		
		
		
        public override void Update(float deltaTime, Camera cam)
        {
            if (!isScavenging || user == null) return;

            // 检查玩家是否离开或中断交互
            if (Vector2.Distance(item.WorldPosition, user.WorldPosition) > 150f || user.IsDead)
            {
                CancelScavenging();
                return;
            }

            scavengeTimer += deltaTime;

            user.AnimController.UpdateUseItem(false, item.WorldPosition + new Vector2(0.0f, 100.0f) * ((item.Condition / item.MaxCondition) % 0.1f));

#if CLIENT
				float progressPercentage = Math.Clamp(scavengeTimer / ScavengeDuration, 0f, 1f);

                // --- 关键部分：模仿 RepairTool 的原生进度条调用 ---
                // 参数说明：唯一ID, 位置, 进度(0-1), 起始颜色, 结束颜色, (可选)文本标签
                // 建议使用 item.ID 而非 user.ID，这样进度条会锚定在物品上，且不会与其他玩家的动作冲突
                var progressBar = user.UpdateHUDProgressBar(
                    item.ID, 
                    item.DrawPosition, 
                    progressPercentage, 
                    GUIStyle.Red, 
                    GUIStyle.Green,
                    TextManager.Get("progressbar.scavenging").Value); 
#endif

            if (scavengeTimer >= ScavengeDuration)
            {
                GiveLoot();
                isScavenging = false;
                user = null;
            }
        }

        private void GiveLoot()
        {
            if (GameMain.NetworkMember != null && GameMain.NetworkMember.IsClient) return;

            if (cachedSmallItems == null || cachedSmallItems.Count == 0)
            {
                cachedSmallItems = ItemPrefab.Prefabs
                    .Where(p => p.Tags.Contains("smallitem"))
                    .ToList();
            }

            if (cachedSmallItems.Count == 0) return;

            // --- 核心修改：生成随机数量的物品 ---
            int lootCount = Rand.Range(MinLootCount, MaxLootCount); // 随机生成 3 到 5 个物品

            for (int i = 0; i < lootCount; i++)
            {
                var selectedPrefab = cachedSmallItems[Rand.Range(0, cachedSmallItems.Count)];
                
                // 稍微偏离中心点，防止重叠导致的物理卡死
                Vector2 spawnPos = item.WorldPosition + Rand.Vector(10.0f);

                Entity.Spawner.AddItemToSpawnQueue(selectedPrefab, spawnPos, onSpawned: (Item spawnedItem) => 
                {
                    // --- 核心修改：赋予物理冲量让物品飞出 ---
                    if (spawnedItem.body != null)
                    {
                        // 创建一个向上的随机扇形力
                        // X轴: -50 到 50, Y轴: 50 到 150 (确保主要向上飞)
                        Vector2 force = new Vector2(Rand.Range(-1f, 1f), Rand.Range(0f, 1f));
                        
                        // 应用冲量
                        spawnedItem.body.ApplyLinearImpulse(force);
                        
                        // 增加一点随机角速度，让物品在空中旋转
                        spawnedItem.body.ApplyTorque(Rand.Range(-5f, 5f));
                    }
                });
            }
#if CLIENT
            // 播放一个破碎/开启的音效（可选）
            SoundPlayer.PlaySound("crate_break", item.WorldPosition);
            // 1. 在箱子的当前位置生成多个粒子
    for (int i = 0; i < 15; i++)
    {
        GameMain.ParticleManager.CreateParticle(
            "scavenge_success", // 这里可以使用原版的 "dust", "bubbles", "spark" 或者你自定义的 "scavenge_success"
            item.WorldPosition, 
            Rand.Vector(Rand.Range(50f, 150f)), // 随机喷射方向和速度
            0f, 
            item.CurrentHull // 在当前的船舱环境内
        );
    }
#endif
            // 销毁原箱子
            Entity.Spawner.AddEntityToRemoveQueue(item);
        }

        private void CancelScavenging()
        {
            isScavenging = false;
            user = null;
        }

        
    }
}
