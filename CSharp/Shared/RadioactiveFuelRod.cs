// Useless for now
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Barotrauma.Extensions;
using Barotrauma;
using System.ComponentModel.Design;
using System.Reflection.PortableExecutable;

namespace BarotraumaDieHard.Items//todo make a structural namespace DieHard.Item.Components. namespace can't be used in elsewhere
{



    class RadioactiveFuelRod : ItemComponent
    {
		private float dropTimer; // 累计烫伤时间
        private const float DropDelay = 1.0f; // 接触多久后会强制掉落

        private float fireTimer; // 累计火焰计时
        private const float FireDelay = 4.0f; // 多久后开始着火

        // 烟雾生成的间隔计时器，防止粒子太多导致卡顿
        private float particleTimer;
		public static List<Item> DangerousFuelRods = new List<Item>();
        
        public RadioactiveFuelRod(Item item, ContentXElement element)
            : base(item, element)
        {
            IsActive = true;
        }
		
		
		
        public override void Update(float deltaTime, Camera cam)
        {
            if (this.item.Condition >= item.MaxCondition)
            {
                dropTimer = 0f; // 修复后重置计时器
                fireTimer = 0f;
                return;
            }

            this.item.AddTag("ignorethis");

            // 1. 首先定义：是否有任何一层容器提供了屏蔽？
            bool isShielded = false;
            Item currentContainer = this.item.ParentInventory?.Owner as Item;

            // 向上递归检查所有父容器
            while (currentContainer != null)
            {
                if (currentContainer.HasTag("radiationshield"))
                {
                    isShielded = true;
                    dropTimer = 0f;
                    fireTimer = 0f;
                    break; // 只要找到一层屏蔽，就安全了
                }
                // 继续检查上一层容器
                currentContainer = currentContainer.ParentInventory?.Owner as Item;
            }

            // 2. 重新定义逻辑：
            // 物品不安全的条件：处于恶化状态 (Condition < Max) 且 (在地面上 OR 所在的所有容器链都没有屏蔽)
            bool isUnshielded = this.item.Condition < this.item.MaxCondition && !isShielded;

            if (isUnshielded)
            {
                // 维护危险列表逻辑（注意：你原来的代码中 Add/Remove 在 Update 里每帧切换，建议根据状态判断）
                if (!DangerousFuelRods.Contains(this.item)) { DangerousFuelRods.Add(this.item); }
                
                            


                Hull currentHull = this.item.CurrentHull;
                if (currentHull != null)
                {
                    // --- 新增：自燃逻辑 ---
                    // 如果燃料棒在空气中（没有被装在任何容器里），直接在地面产生火灾
                    // 检查直接父级：如果直接父级不是 fuelrodholder，或者根本不在容器里（在地上/人手里）
                    var directContainer = this.item.ParentInventory?.Owner as Item;
                    bool isNotInHolder = directContainer == null || directContainer.Prefab.Identifier != "fuelrodholder";

                    if (isNotInHolder)
                    {
                        fireTimer += deltaTime;

                        this.item.Condition -= 1f;
                            
                        
                        if (this.item.Condition <= 0f)
                        {
                            Entity.Spawner.AddEntityToRemoveQueue(this.item);
                            Entity.Spawner.AddItemToSpawnQueue(ItemPrefab.GetItemPrefab("meltdownfuelrod"),this.item.WorldPosition);
                        }
                        
                        
                        #if CLIENT
                            // 定义何时开始冒烟，例如当计时达到总延迟的 40% 时
                            float smokeStartThreshold = FireDelay * 0.3f;

                            if (fireTimer > smokeStartThreshold && currentHull.FireSources.Count < 2)
                            {
                                particleTimer += deltaTime;
                                // 限制粒子生成频率，每 0.1 秒生成一个烟雾
                                if (particleTimer >= 0.1f)
                                {
                                    // 计算冒烟强度 (0.0 到 1.0)，随着时间推移，烟变得更黑更浓
                                    float currentRatio = (fireTimer - smokeStartThreshold) / (FireDelay - smokeStartThreshold);
                                    float intensity = MathHelper.Clamp(currentRatio, 0.2f, 1.0f);

                                    // 创建粒子。Barotrauma有很多内置粒子，这里使用标准的 "smoke"
                                    // 可以尝试 "blacksmoker1" 让它看起来更危险
                                    GameMain.ParticleManager.CreateParticle(
                                        "fleshsmoke", 
                                        this.item.WorldPosition + Rand.Vector(5.0f), // 位置加一点随机偏移，让烟雾更自然
                                        Rand.Vector(20.0f * intensity), // 粒子速度随着强度增加
                                        0.0f, 
                                        currentHull);
                                    
                                    particleTimer = 0f; // 重置粒子计时器
                                }
                            }
        #endif

                        // 2. 检查火源
                        if (currentHull.FireSources.Count < 2 && fireTimer > FireDelay && !this.item.HasTag("meltdownfuelrod"))
                        {
                            new FireSource(this.item.WorldPosition);
                        }
                    }

                    foreach (Character character in Character.CharacterList)
                    {
                        if (character.CurrentHull != currentHull) { continue; }

                        // --- 辐射逻辑 ---
                        Item outerClothes = character.Inventory.GetItemInLimbSlot(InvSlotType.OuterClothes);
                        if (outerClothes == null || !outerClothes.HasTag("radiationsuit"))
                        {
                            character.CharacterHealth.ApplyAffliction(character.AnimController.MainLimb, 
                                AfflictionPrefab.Prefabs["radiationsickness"].Instantiate(1f * deltaTime));
                        }

                        // --- 手部持有逻辑 ---
                        var leftHand = character.Inventory.GetItemInLimbSlot(InvSlotType.LeftHand);
                        var rightHand = character.Inventory.GetItemInLimbSlot(InvSlotType.RightHand);

                        if (leftHand == this.item || rightHand == this.item)
                        {
                            // 检查是否有保护性的 Holder
                            Item fuelRodHolder = null;
                            if (leftHand != null && leftHand.Prefab.Identifier == "fuelrodholder" && leftHand != this.item) { fuelRodHolder = leftHand; }
                            else if (rightHand != null && rightHand.Prefab.Identifier == "fuelrodholder" && rightHand != this.item) { fuelRodHolder = rightHand; }

                            if (fuelRodHolder != null)
                            {
                                dropTimer = 0f; // 有 Holder，安全
                                var container = fuelRodHolder.GetComponent<ItemContainer>();
                                if (container != null && container.Inventory.CanBePut(this.item))
                                {
                                    container.Inventory.TryPutItem(this.item, character);
                                }
                            }
                            else
                            {
                                // --- 烫伤延迟与掉落逻辑 ---
                                dropTimer += deltaTime;
                                
                                // 持续接触产生的轻微烫伤 (哪怕还没掉落)
                                Limb targetLimb = (leftHand == this.item) ? 
                                    character.AnimController.GetLimb(LimbType.LeftHand) : 
                                    character.AnimController.GetLimb(LimbType.RightHand);
                                
                                character.CharacterHealth.ApplyAffliction(targetLimb, 
                                    AfflictionPrefab.Prefabs["burn"].Instantiate(10f * deltaTime));

                                // 达到延迟阈值
                                if (dropTimer >= DropDelay)
                                {
                                    // 1. 施加重度烫伤
                                    character.CharacterHealth.ApplyAffliction(targetLimb, 
                                        AfflictionPrefab.Prefabs["burn"].Instantiate(15f));

                                    // 2. 施加轻微眩晕 (Stun)
                                    character.CharacterHealth.ApplyAffliction(character.AnimController.MainLimb, 
                                        AfflictionPrefab.Prefabs["stun"].Instantiate(0.5f));

                                    // 3. 播放烫伤音效 (使用游戏内置的烫伤/伤害音效)
        #if CLIENT
                                    if (character.IsHuman)
                                        {
                                            // 这里的 soundIdentifier 需要对应你 XML 中定义的音效标识符
                                            // 比如某些 Mod 会定义 "damage_female" 和 "damage_male"
                                            Identifier soundId = character.IsFemale ? "character_damage_human_female".ToIdentifier() : "character_damage_human_male".ToIdentifier();
                                            
                                            // 播放音效
                                            SoundPlayer.PlaySound(soundId.Value, character.WorldPosition);
                                        }
        #endif
                                    
                                    // 4. 强制掉落
                                    this.item.Drop(character);
                                    dropTimer = 0f; 
                                }
                                
                            }
                        }
                        else
                        {
                            // 如果没拿在手里，重置该物品的掉落计时器
                            dropTimer = 0f;
                        }
                    }
                }
            }
            else
            {
                if (DangerousFuelRods.Contains(this.item)) { DangerousFuelRods.Remove(this.item); }
                dropTimer = 0f;
                fireTimer = 0f;
            }
        }



        
    }
}
