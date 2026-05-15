using Barotrauma;
using Barotrauma.Items.Components;
using System;
using System.Linq;
using System.Collections.Generic;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using Networking;

namespace BarotraumaDieHard
{
    public class AIObjectiveRepairWithDisconnect : AIObjective
    {
        public override Identifier Identifier { get; set; } = "repair.with.disconnect".ToIdentifier();

        private readonly Item targetItem;
        private readonly Repairable repairable;
        private readonly List<DisconnectedWireInfo> disconnectedWires = new List<DisconnectedWireInfo>();

        private AIObjective currentSubObjective = null;
        private bool isDisconnected = false;
        private bool isComplete = false;
        //添加 IsDuplicate 保护：
        public override bool IsDuplicate<T>(T otherObjective) 
        => base.IsDuplicate(otherObjective) && otherObjective is AIObjectiveRepairWithDisconnect repairObj && repairObj.targetItem == targetItem;

        private class DisconnectedWireInfo
        {
            public Wire Wire;
            public Connection TargetConnection;
            public DisconnectedWireInfo(Wire wire, Connection target) { Wire = wire; TargetConnection = target; }
        }

        public AIObjectiveRepairWithDisconnect(Character character, Item item, AIObjectiveManager objectiveManager, float priorityModifier = 1)
            : base(character, objectiveManager, priorityModifier)
        {
            targetItem = item;
            repairable = item?.GetComponent<Repairable>();
        }

        public override bool CheckObjectiveState() => isComplete;

        public override void Act(float deltaTime)
        {
            if (targetItem == null || repairable == null) { Abandon = true; return; }

            // --- 阶段 1: 准备工作 (导航 & 拔线) ---
            if (!isDisconnected)
            {
                HandlePreparationStage();
                return;
            }

            // --- 阶段 2: 维修设备 ---
            if (!IsRepairComplete())
            {
                EnsureRepairSubObjective();
            }
            // --- 阶段 3: 恢复连接 ---
            else
            {
                HandleReconnectionStage();
            }
        }

        #region Stage Handlers

        private void HandlePreparationStage()
        {
            // 依赖原版维修逻辑进行导航和找工具
            if (currentSubObjective == null || currentSubObjective.IsCompleted || currentSubObjective.Abandon)
            {
                currentSubObjective = new AIObjectiveRepairItem(character, targetItem, objectiveManager, PriorityModifier, isPriority: true);
                AddSubObjective(currentSubObjective);
            }

            // 到达交互距离后拔线
            if (Vector2.Distance(character.WorldPosition, targetItem.WorldPosition) < targetItem.InteractDistance * 2f)
            {
                if (ExecuteDisconnectAll())
                {
                    isDisconnected = true;
                    // 重置子任务，下一帧将进入纯粹的维修阶段
                    currentSubObjective.Abandon = true;
                    currentSubObjective = null;
                }
            }
        }

        private void EnsureRepairSubObjective()
        {
            if (currentSubObjective == null || currentSubObjective.Abandon || currentSubObjective.IsCompleted)
            {
                currentSubObjective = new AIObjectiveRepairItem(character, targetItem, objectiveManager, PriorityModifier, isPriority: true);
                AddSubObjective(currentSubObjective);
            }
        }

        private void HandleReconnectionStage()
        {
            // 1. 必须先进行基础目标检查
            if (targetItem == null) { isComplete = true; return; }

            bool isJunctionBox = targetItem.HasTag("junctionbox".ToIdentifier()) || targetItem.GetComponent<PowerTransfer>() != null;

            // 2. 增加空引用防御：如果当前没有子任务，先创建一个导航任务
            if (currentSubObjective == null)
            {
                currentSubObjective = new AIObjectiveGoTo(targetItem, character, objectiveManager, closeEnough: targetItem.InteractDistance);
                AddSubObjective(currentSubObjective);
                return; // 立即返回，等待下一帧子任务初始化完毕
            }

            // 3. 安全访问：只有在子任务不为 null 且已完成时才执行重连
            if (currentSubObjective.IsCompleted)
            {
                // 针对接线盒与灯的区分逻辑
                ExecuteReconnectAll();
                isComplete = true;
            }
            else if (currentSubObjective.Abandon)
            {
                currentSubObjective = null; // 清空以便下一帧重建
            }
        }

        #endregion

        #region Core Wire Operations

        private bool ExecuteDisconnectAll()
        {
            // 如果是接线盒，我们直接关闭它的开关，不拆电线
            if (targetItem.HasTag("junctionbox".ToIdentifier()) || targetItem.GetComponent<PowerTransfer>() != null)
            {
                PowerTransferPatch.SetLeverState(targetItem, false);
                PowerTransferPatch.RefreshGrid(targetItem);

                #if SERVER
                // 同步开关状态
                IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.SWITCH_JUNCTIONBOX);
                msg.WriteUInt16(targetItem.ID);
                msg.WriteBoolean(false);
                NetUtil.SendAll(msg, DeliveryMethod.Reliable);
                #endif
                
                DebugConsole.NewMessage($"[AI] 设备 {targetItem.Name} 是接线盒，执行断电开关操作而非拆线。");
                return true; 
            }
            character.Speak(TextManager.Get("dialog.bot.disconnectingpowerwires").Value, null, 0.0f, "diconnectingpowerwire".ToIdentifier(), 10.0f);
            var panel = targetItem.GetComponent<ConnectionPanel>();
            if (panel == null) return true;

            disconnectedWires.Clear();
            foreach (var conn in panel.Connections.Where(c => c.IsPower && c.Wires.Any()))
            {
                foreach (var wire in conn.Wires.ToList())
                {
                    disconnectedWires.Add(new DisconnectedWireInfo(wire, conn));
                    
                    // 物理断开
                    wire.RemoveConnection(conn);
                    conn.DisconnectWire(wire);
                    panel.DisconnectedWires.Add(wire);

                    #if SERVER
                    SendWireSyncMessage(wire.Item, targetItem, conn.Name, isDisconnect: true);
                    #endif
                }
            }

            // 强制电力刷新，避免残留伤害
            targetItem.GetComponent<Powered>()?.Update(0.1f, null);
            return true;
        }

        private void ExecuteReconnectAll()
        {
            // --- 新增：接线盒恢复处理 ---
            if (targetItem.HasTag("junctionbox".ToIdentifier()) || targetItem.GetComponent<PowerTransfer>() != null)
            {
                DebugConsole.NewMessage("swtich on");
                PowerTransferPatch.SetLeverState(targetItem, true);
                PowerTransferPatch.RefreshGrid(targetItem);

                #if SERVER
                IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.SWITCH_JUNCTIONBOX);
                msg.WriteUInt16(targetItem.ID);
                msg.WriteBoolean(true);
                NetUtil.SendAll(msg, DeliveryMethod.Reliable);
                #endif
                return;
            }

            var panel = targetItem.GetComponent<ConnectionPanel>();
            if (panel == null) return;

            foreach (var info in disconnectedWires)
            {
                if (info.Wire == null || info.Wire.Item.Removed) continue;

                int side = info.Wire.Connections[0] == null ? 0 : 1;
                if (info.Wire.Connect(info.TargetConnection, side, addNode: true, sendNetworkEvent: true))
                {
                    info.TargetConnection.ConnectWire(info.Wire);
                    panel.DisconnectedWires.Remove(info.Wire);
                    Powered.ChangedConnections.Add(info.TargetConnection);

                    #if SERVER
                    SendWireSyncMessage(info.Wire.Item, targetItem, info.TargetConnection.Name, isDisconnect: false);
                    #endif
                }
            }

            #if SERVER
            targetItem.CreateServerEvent(panel);
            #endif
            
            targetItem.GetComponent<Powered>()?.Update(0, null);
            disconnectedWires.Clear();
        }

        #endregion

        #region Networking & Helpers

        private bool IsRepairComplete() => !repairable.IsBelowRepairThreshold || targetItem.Condition >= targetItem.MaxCondition * 0.95f;

        #if SERVER
        public static void SendWireSyncMessage(Item wireItem, Item targetItem, string connName, bool isDisconnect)
        {
            IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.WIRE_DISCONNECT_SYNC);
            msg.WriteUInt16(wireItem.ID);
            msg.WriteUInt16(targetItem.ID);
            msg.WriteString(connName);
            msg.WriteBoolean(isDisconnect);
            NetUtil.SendAll(msg, DeliveryMethod.Reliable);
        }
        #endif

        public static void OnReceiveWireSync(object[] args)
        {
            IReadMessage msg = (IReadMessage)args[0];
            if (!(Entity.FindEntityByID(msg.ReadUInt16()) is Item wireItem)) return;
            if (!(Entity.FindEntityByID(msg.ReadUInt16()) is Item targetItem)) return;
            
            string connName = msg.ReadString();
            bool isDisconnect = msg.ReadBoolean();

            var wire = wireItem.GetComponent<Wire>();
            var panel = targetItem.GetComponent<ConnectionPanel>();
            var conn = panel?.Connections.FirstOrDefault(c => c.Name == connName);

            if (wire == null || conn == null) return;

            if (isDisconnect)
            {
                wire.RemoveConnection(conn);
                conn.DisconnectWire(wire);
                panel.DisconnectedWires.Add(wire);
            }
            else
            {
                int side = wire.Connections[0] == null ? 0 : 1;
                wire.Connect(conn, side, addNode: true, sendNetworkEvent: false);
                conn.ConnectWire(wire);
                panel.DisconnectedWires.Remove(wire);
                //wire.Item.Position = targetItem.Position; // 防止客户端电线位置漂移
            }
            Powered.ChangedConnections.Add(conn);
        }
        #endregion
    }
}