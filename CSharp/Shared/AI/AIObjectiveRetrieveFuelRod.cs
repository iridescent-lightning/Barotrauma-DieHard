using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using System.Collections.Generic;
using System.Linq;
using Barotrauma;
using Barotrauma.Networking;

namespace BarotraumaDieHard.AI
{
    class AIObjectiveRetrieveFuelRod : AIObjective
    {
        // 修复：Identifier 必须包含 set 访问器，且类型为 Identifier
        public override Identifier Identifier { get; set; } = "Retrieve Fuel Rod".ToIdentifier();
        
        private Item targetRod;
        private Item fuelRodCase;
        private Item fuelRodHolder;
        public float waitTimer = 0f;
        private const float DelayDuration = 2.0f; // 设置延迟时间（秒）
        private bool isWaiting = false;
        private AIObjectiveGetItem getEquipmentObjective;

        public AIObjectiveRetrieveFuelRod(Character character, AIObjectiveManager objectiveManager, float priorityModifier = 1) 
            : base(character, objectiveManager, priorityModifier) 
        {
            Priority = 100f;
        }

        // 修复：必须实现抽象成员 CheckObjectiveState
        public override bool CheckObjectiveState() => IsCompleted;

        // 修复：必须使用 public 访问修饰符以匹配基类
        public override void Act(float deltaTime)
        {
            // 1. 检查装备前置条件 (防护服、Holder、Case)
            if (!HasRequiredEquipment(out Identifier missingTag))
            {
                TryAddSubObjective(ref getEquipmentObjective, () =>
                {
                    // 提示缺少装备
                    if (character.IsOnPlayerTeam)
                    {
                        character.Speak(TextManager.Get("dialog.bots.needequipment").Value, ChatMessageType.Radio, 0.0f, "needequipment".ToIdentifier(), 30.0f);
                    }

                    return new AIObjectiveGetItem(character, missingTag, objectiveManager, equip: true)
                    {
                        AllowStealing = true,
                        Wear = true
                    };
                }, onAbandon: () =>
                {
                    character.Speak(TextManager.Get("dialog.bots.retrievefuelrod.failure").Value, ChatMessageType.Radio, 0.0f, "failureretrieve".ToIdentifier(), 30.0f);
                    Abandon = true;
                });
                return;
            }

            // 2. 寻找反应堆中消耗完/损坏的燃料棒
            if (targetRod == null)
            {
                var reactor = Item.ItemList.FirstOrDefault(i => i.HasTag("reactor") && i.Submarine == character.Submarine);
                if (reactor != null)
                {
                    var container = reactor.GetComponent<ItemContainer>();
                    targetRod = container?.Inventory.AllItems.FirstOrDefault(it => it.HasTag("reactorfuel") && it.ConditionPercentage < 100);
                }
            }

            if (targetRod == null)
            {
                if (character.IsOnPlayerTeam) character.Speak(TextManager.Get("dialog.bots.retrievefuelrod.nofuelrod_inreactor").Value, ChatMessageType.Radio, identifier: "nospentfuelrods".ToIdentifier());
                Abandon = true;
                return;
            }

            // 3. 执行取出并放入 Case 的逻辑
            // 修复 2：全面检查（无论是在背包里，还是已经塞进夹子/箱子里，都算 hasRod）
            fuelRodHolder = character.Inventory.FindItem(i => i.Prefab.Identifier == "fuelrodholder", recursive: true);
            fuelRodCase = character.Inventory.FindItem(i => i.Prefab.Identifier == "fuelrodcase", recursive: true);

            bool inInventory = character.Inventory.FindItem(it => it == targetRod, recursive: true) != null;
            bool inHolder = fuelRodHolder != null && fuelRodHolder.GetComponent<ItemContainer>()?.Inventory.AllItems.Contains(targetRod) == true;
            bool inCase = fuelRodCase != null && fuelRodCase.GetComponent<ItemContainer>()?.Inventory.AllItems.Contains(targetRod) == true;
            bool hasRod = inInventory || inHolder || inCase;

            
            if (!hasRod && !isWaiting) // 如果还没拿到棒子，且不在等待进入Case的过程中
            {
                AddSubObjective(new AIObjectiveGetItem(character, targetRod, objectiveManager, equip: true));
            }
            else
            {
                // 既然已经在身上了，停止 GetItem 子目标
                RemoveSubObjective(ref getEquipmentObjective);

                if (fuelRodCase == null)
                {
                    DebugConsole.NewMessage("case is null");
                    targetRod.Drop(character);
                    Abandon = true;
                    return;
                }

                var holderContainer = fuelRodHolder?.GetComponent<ItemContainer>();
                var caseContainer = fuelRodCase.GetComponent<ItemContainer>();

                // --- 逻辑分段：第一步，放入 Holder ---
                if (!isWaiting)
                {
                    DebugConsole.NewMessage("not waiting");
                    if (holderContainer != null && holderContainer.Inventory.CanBePut(targetRod))
                    {
                        holderContainer.Inventory.TryPutItem(targetRod, character);
                        if (character.IsOnPlayerTeam) character.Speak(TextManager.Get("dialog.bots.retrievefuelrod.nofuelrod_inreactor").Value, identifier: "fuelrodinholder".ToIdentifier());
                        
                        isWaiting = true; // 开启等待状态
                        waitTimer = 0f;    // 重置计时器
                    }
                    else
                    {
                        // 如果没有 Holder，直接跳过等待放入 Case (或者你可以设置必须有 Holder)
                        //isWaiting = true; 
                        Abandon = true;
                    }
                }
                // --- 第二步：计时等待 ---
                else if (waitTimer < DelayDuration)
                {
                    DebugConsole.NewMessage($"waitTimer: {waitTimer}");
                    waitTimer += deltaTime;
                    // 可以在这里添加等待时的动作，比如保持静止
                    character.AnimController.TargetMovement = Microsoft.Xna.Framework.Vector2.Zero;
                    // 2. 尝试将 Case 装备到手上（空闲的手）
                    if (fuelRodCase != null)
                    {
                        DebugConsole.NewMessage("fuelRodCase != null");
                        // 只有当它还没被拿在手上时才尝试装备，避免每帧重复调用
                        if (!character.HasEquippedItem(fuelRodCase))
                        {
                            DebugConsole.NewMessage("HasEquippedItem");
                            var rightHandSlot = character.Inventory.FindLimbSlot(InvSlotType.RightHand);
                            var leftHandSlot = character.Inventory.FindLimbSlot(InvSlotType.LeftHand);
                            character.Inventory.TryPutItem(fuelRodCase, rightHandSlot | leftHandSlot, true, false, Character.Controlled, true, true);
                            // TryEquip 会自动寻找合适的手部槽位
                            //fuelRodCase.Equip(character);
                        }
                    }
                }
                // --- 第三步：转移到 Case 并结束 ---
                else
                {
                    if (caseContainer != null && caseContainer.Inventory.CanBePut(targetRod))
                    {
                        DebugConsole.NewMessage("3rd step");
                        caseContainer.Inventory.TryPutItem(targetRod, character);
                        if (character.IsOnPlayerTeam) character.Speak(TextManager.Get("dialog.bots.retrievefuelrod.success").Value, ChatMessageType.Radio, identifier: "fuelrodsecured".ToIdentifier());
                        
                        RemoveSubObjective(ref getEquipmentObjective);
                        isWaiting = false;
                        IsCompleted = true; // 任务结束
                    }
                }
            }
        }

        private bool HasRequiredEquipment(out Identifier missingTag)
        {
            missingTag = Identifier.Empty;

            // 检查防护服
            if (!character.HasEquippedItem(TagsDieHard.RadiationGear))
            {
                missingTag = TagsDieHard.RadiationGear;
                return false;
            }

            // 检查夹具 (Holder)
            var holder = character.Inventory.FindItem(i => i.Prefab.Identifier == "fuelrodholder", recursive: true);
            if (holder == null || !character.HasEquippedItem(holder))
            {
                missingTag = "fuelrodholder".ToIdentifier();
                return false;
            }

            // 检查存放箱 (Case)
            if (character.Inventory.FindItem(i => i.Prefab.Identifier == "fuelrodcase", recursive: true) == null)
            {
                missingTag = "fuelrodcase".ToIdentifier();
                return false;
            }

            return true;
        }

        public override void OnCompleted()
        {
            base.OnCompleted();

            // 1. 脱掉防护服 (RadiationGear)
            // 寻找身上穿戴的防护服并尝试移入背包槽位，如果背包满了则丢弃在地上
            var suit = character.Inventory.FindItem(i => i.HasTag(TagsDieHard.RadiationGear), recursive: true);
            if (suit != null && character.HasEquippedItem(suit))
            {
                
                    suit.Drop(character);
                
            }

            // 2. 归还或放下工具 (Holder 和 Case)
            // 获取身上所有的相关物品
            var itemsToClear = character.Inventory.AllItems.Where(i => 
                i.Prefab.Identifier == "fuelrodholder" || 
                i.Prefab.Identifier == "fuelrodcase").ToList();

            foreach (var item in itemsToClear)
            {
                // 如果你想让 bot 更有序，可以在这里记录它们最初的位置并瞬移回去
                // 但最简单稳妥的做法是直接丢弃在当前位置，由 bot 的空闲 AI 处理物品归位
                item.Drop(character);
            }

            // 3. (可选) 让 Bot 说话反馈
            if (character.IsOnPlayerTeam)
            {
                character.Speak("Task finished. Returning to duties.", identifier: "fuelrodtaskdone".ToIdentifier());
            }
            Abandon = true;
        }
    }
}