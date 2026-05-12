// This class is patched to allow bots to swap weldingtoolequipment from bag to hands in case of fixing leaks. Bots will still fires one shot before switching.
using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using System;
using System.Linq;

using HarmonyLib;
using System.Reflection;
using System.Xml.Linq;

using Barotrauma;
using Networking;
using Barotrauma.Networking;




namespace BarotraumaDieHard
{
    class AIObjectiveOperateJunctionBox : AIObjective
    {
        public override Identifier Identifier { get; set; } = "Operate Junction Box".ToIdentifier();

        private readonly Item targetItem;
        private readonly bool targetLeverState;
        private AIObjectiveGoTo moveToObjective;

        public AIObjectiveOperateJunctionBox(Character character, Item item, AIObjectiveManager objectiveManager, bool targetState, float priorityModifier = 1) 
            : base(character, objectiveManager, priorityModifier)
        {
            targetItem = item;
            targetLeverState = targetState;
        }

        // 关键：当这个返回 true 时，Manager 会自动清理这个任务并触发 Completed 事件
        public override bool CheckObjectiveState()
        {
            if (targetItem == null) return true;
            return PowerTransferPatch.GetLeverState(targetItem) == targetLeverState;
        }

        public override void Act(float deltaTime)
        {
            if (targetItem == null)
            {
                Abandon = true;
                return;
            }

            // 1. 距离检查
            float dist = Vector2.Distance(character.WorldPosition, targetItem.WorldPosition);
            if (dist > targetItem.InteractDistance * 1.5f)
            {
                TryAddSubObjective(ref moveToObjective, () => new AIObjectiveGoTo(targetItem, character, objectiveManager), 
                    onAbandon: () => Abandon = true);
            }
            else
            {
                // 2. 到达目的地，执行操作
                PerformSwitch();
            }
        }

        private void PerformSwitch()
        {
            // 更新状态
            PowerTransferPatch.SetLeverState(targetItem, targetLeverState);
            PowerTransferPatch.RefreshGrid(targetItem);

            
  
                PowerTransferPatch.SendJBSwitchMessage(targetItem, targetLeverState);


            character.Speak(TextManager.Get("dialog.bot.operatedjunctionbox").Value, null, 0.0f, "operatedjb".ToIdentifier(), 10.0f);
            
            // 重要：标记已完成，这样 Manager 才知道这个任务结束了
            IsCompleted = true; 
        }
    }
}