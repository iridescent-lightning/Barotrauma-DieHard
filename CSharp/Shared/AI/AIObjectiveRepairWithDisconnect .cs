using Barotrauma;
using Barotrauma.Items.Components;
using Barotrauma.Extensions;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using Barotrauma.Networking;
using HarmonyLib;
using Networking;

namespace BarotraumaDieHard
{
    /// <summary>
    /// AI任务：维修前断开所有输入电线，维修完成后恢复
    /// </summary>
    public class AIObjectiveRepairWithDisconnect : AIObjective
    {
        public override Identifier Identifier { get; set; } = "repair.with.disconnect".ToIdentifier();
        
        private readonly Item targetItem;
        private readonly Repairable repairable;
        
        // 存储断开的电线信息
        private List<DisconnectedWireInfo> disconnectedWires = new List<DisconnectedWireInfo>();
        
        private AIObjective currentSubObjective = null;
        private bool isDisconnected = false;
        private bool isComplete = false;
        
        private class DisconnectedWireInfo
        {
            public Wire Wire;
            public Connection TargetConnection;
            public int TargetConnectionIndex;
            
            public DisconnectedWireInfo(Wire wire, Connection target)
            {
                Wire = wire;
                
                TargetConnection = target;
                
                TargetConnectionIndex = target?.ConnectionPanel?.Connections?.IndexOf(target) ?? -1;
            }
        }
        
        public AIObjectiveRepairWithDisconnect(Character character, Item item, AIObjectiveManager objectiveManager, float priorityModifier = 1)
            : base(character, objectiveManager, priorityModifier)
        {
            targetItem = item;
            repairable = item.GetComponent<Repairable>();
        }
        
        public override bool CheckObjectiveState()
        {
            return isComplete;
        }
        
        public override void Act(float deltaTime)
        {
            if (targetItem == null || repairable == null)
            {
                Abandon = true;
                return;
            }
            
            
            // 第一阶段：断开所有输入电线
            if (!isDisconnected)
            {
                DebugConsole.NewMessage("is connected");
                ExecuteDisconnectStage();
                return;
            }
            
            // 第二阶段：维修
            if (!IsRepairComplete())
            {
                ExecuteRepairStage();
            }
            else
            {
                // 第三阶段：恢复所有电线
                ExecuteReconnectStage();
            }
        }
        
        private void ExecuteDisconnectStage()
        {
            DebugConsole.NewMessage($"ExecuteDisconnectStage, isDisconnected: {isDisconnected}");
            
            if (isDisconnected) return;
            
            // 直接断开所有输入电线（不需要导航）
            if (disconnectedWires.Count == 0)
            {
                DebugConsole.NewMessage("Finding wires to disconnect...");
                if (DisconnectAllInputWires())
                {
                    DebugConsole.NewMessage($"Found {disconnectedWires.Count} wires, disconnecting immediately...");
                    PerformDisconnectAll();
                }
            }
            
            // 标记为已断开，进入维修阶段
            isDisconnected = true;
            DebugConsole.NewMessage("Disconnect stage complete");
        }
        
        private bool DisconnectAllInputWires()
        {
            disconnectedWires.Clear();
            var connectionPanel = targetItem.GetComponent<ConnectionPanel>();
            if (connectionPanel == null) return false;

            foreach (var connection in connectionPanel.Connections)
            {
                // 在 DisconnectAllInputWires 中，我们只需要断开 Target 这一端
                if (connection.IsPower && connection.Wires.Count > 0)
                {
                    var wires = connection.Wires.ToList();
                    foreach (var wire in wires)
                    {
                        // 存储信息
                        disconnectedWires.Add(new DisconnectedWireInfo(wire, connection));

                        // 1. 重要：清空电线端的引用 (拔掉插头)
                        // 这样 wire.connections[i] 就会变成 null，之后的 Connect 才能成功
                        wire.RemoveConnection(connection); 

                        // 2. 加入面板底部显示区域
                        targetItem.GetComponent<ConnectionPanel>()?.DisconnectedWires.Add(wire);

                        // 3. 断开接线柱端的引用
                        connection.DisconnectWire(wire);
                        
                        DebugConsole.NewMessage($"[AI断开] 已彻底清除电线 {wire.Item.ID} 的端点引用");
                    }
                }
            }
            return disconnectedWires.Count > 0;
        }
        
        private void PerformDisconnectAll()
        {
            foreach (var wireInfo in disconnectedWires)
            {
                // 关键：只断开连接，不要触碰 wireInfo.Wire.nodes
                // DisconnectWire 会移除连接引用，但会保留电线在物理世界的位置
                wireInfo.TargetConnection.DisconnectWire(wireInfo.Wire);

                // 必须加入此集合，否则电线可能会掉落在地或消失
                var targetPanel = targetItem.GetComponent<ConnectionPanel>();
                if (targetPanel != null)
                {
                    targetPanel.DisconnectedWires.Add(wireInfo.Wire);
                }
#if SERVER
                if (GameMain.Server != null)
                {
                    // 参数：电线Item, 目标物品Item, 接线柱名称, 是否是断开(true)
                    SendWireDisconnectMessage(wireInfo.Wire.Item, targetItem, wireInfo.TargetConnection.Name, true);
                }
#endif
            }
        }

        
#if SERVER
        public static void SendWireDisconnectMessage(Item wireItem, Item targetItem, string connectionName, bool isDisconnect)
        {
            IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.WIRE_DISCONNECT_SYNC);
            msg.WriteUInt16(wireItem.ID);
            msg.WriteUInt16(targetItem.ID);
            msg.WriteString(connectionName); // 写入接线柱名称，如 "power_in"
            msg.WriteBoolean(isDisconnect);

            // Server 发送给所有客户端
            NetUtil.SendAll(msg, DeliveryMethod.Reliable);
        }
#endif
        
        private bool IsRepairComplete()
        {
            // 检查维修是否完成
            return repairable.IsBelowRepairThreshold == false || 
                   targetItem.Condition >= targetItem.MaxCondition * 0.95f;
        }
        
        private void ExecuteRepairStage()
        {
            // 创建或使用现有的维修任务
            if (currentSubObjective == null || currentSubObjective.Abandon)
            {
                var repairObj = new AIObjectiveRepairItem(
                    character, 
                    targetItem, 
                    objectiveManager, 
                    priorityModifier: PriorityModifier,
                    isPriority: true
                );
                
                repairObj.Completed += () => { };
                repairObj.Abandoned += () => 
                {
                    // 放弃维修时也要恢复电线
                    ExecuteReconnectStage();
                    Abandon = true;
                };
                
                currentSubObjective = repairObj;
                AddSubObjective(currentSubObjective);
            }
            
            // 如果维修子任务完成了但维修还没完成，需要重新创建
            if (currentSubObjective.IsCompleted && !IsRepairComplete())
            {
                currentSubObjective = null;
            }
        }
        
        private void ExecuteReconnectStage()
        {
            // 如果没有断开的电线，直接完成
            if (disconnectedWires.Count == 0)
            {
                isComplete = true;
                return;
            }
            
            // 导航到目标物品位置
            if (currentSubObjective == null || currentSubObjective.Abandon)
            {
                currentSubObjective = new AIObjectiveGoTo(
                    targetItem,
                    character,
                    objectiveManager,
                    closeEnough: targetItem.InteractDistance
                );
                AddSubObjective(currentSubObjective);
            }
            
            if (currentSubObjective.IsCompleted)
            {
                character.Speak(TextManager.Get("dialog.bot.repairwithdisconnect").Value, null, 0.0f, "complete".ToIdentifier(), 10.0f);
                PerformReconnectAll();
                isComplete = true;
            }
        }
        
        private void PerformReconnectAll()
        {
            foreach (var wireInfo in disconnectedWires)
            {
                if (wireInfo.Wire == null || wireInfo.Wire.Item.Removed) continue;

                // 1. 自动寻找电线空置的端点索引 (0 或 1)
                int emptySide = -1;
                if (wireInfo.Wire.Connections[0] == null) emptySide = 0;
                else if (wireInfo.Wire.Connections[1] == null) emptySide = 1;

                if (emptySide == -1)
                {
                    // 如果两头都不是空的，说明这根线已经连在别处了，尝试断开目标端再重连
                    DebugConsole.NewMessage("电线两端已满，尝试强制重置一端...");
                    // 这里可以加一个强制逻辑，或者跳过
                    continue;
                }

                // 2. 调用 Connect 建立【电线 -> 接线柱】的引用
                // 注意：这里必须传入我们找到的 emptySide
                bool success = wireInfo.Wire.Connect(wireInfo.TargetConnection, emptySide, addNode: true, sendNetworkEvent: true);

                if (success)
                {
                    // 3. 【最重要的一步】建立【接线柱 -> 电线】的反向引用
                    // Wire.Connect 源码里明确说了它不负责接线柱端的 Add，必须手动补上
                    if (!wireInfo.TargetConnection.Wires.Contains(wireInfo.Wire))
                    {
                        wireInfo.TargetConnection.ConnectWire(wireInfo.Wire);
                    }

                    // 4. 从 UI 底部的断开列表移除
                    var targetPanel = targetItem.GetComponent<ConnectionPanel>();
                    targetPanel?.DisconnectedWires.Remove(wireInfo.Wire);
                    
                    DebugConsole.NewMessage($"[AI成功] 电线已插回 {targetItem.Name} 的 {wireInfo.TargetConnection.Name}");
                }
                else
                {
                    DebugConsole.NewMessage($"[AI失败] Wire.Connect 返回 false。端点索引: {emptySide}", Microsoft.Xna.Framework.Color.Red);
                }
            }
            #if SERVER
    targetItem.CreateServerEvent(targetItem.GetComponent<ConnectionPanel>());
#endif
            // 强制电力网格刷新
            targetItem.GetComponent<Powered>()?.Update(0, null);
            disconnectedWires.Clear();
        }



        public static void OnReceiveWireSync(object[] args)
        {
            IReadMessage msg = (IReadMessage)args[0];
            ushort wireId = msg.ReadUInt16();
            ushort targetId = msg.ReadUInt16();
            // 错误 1 修复：使用 ReadString 而不是 ReadFormatted
            string connName = msg.ReadString(); 
            bool isDisconnect = msg.ReadBoolean();

            Item wireItem = Entity.FindEntityByID(wireId) as Item;
            Item targetItem = Entity.FindEntityByID(targetId) as Item;
            
            if (wireItem == null || targetItem == null) return;

            Wire wire = wireItem.GetComponent<Wire>();
            ConnectionPanel panel = targetItem.GetComponent<ConnectionPanel>();
            Connection conn = panel?.Connections.FirstOrDefault(c => c.Name == connName);

            if (wire == null || conn == null) return;

            if (isDisconnect)
            {
                wire.RemoveConnection(conn);
                conn.DisconnectWire(wire);
                panel.DisconnectedWires.Add(wire);
            }
            else
            {
                // 错误 2 修复：wire.Connect 需要一个 int 类型的端点索引
                // 我们寻找电线哪一头是空的 (null)，就连哪一头
                int emptyIndex = wire.Connections[0] == null ? 0 : 1;

                // 如果两头都满了（这在同步逻辑中不应该发生，除非逻辑冲突），
                // 我们可以强制覆盖 0，或者直接返回
                wire.Connect(conn, emptyIndex, addNode: true, sendNetworkEvent: false);
                
                // 建立反向引用
                conn.ConnectWire(wire);
                panel.DisconnectedWires.Remove(wire);
            }

            // 客户端也需要标记电网脏数据，以更新 UI 和电力逻辑
            Powered.ChangedConnections.Add(conn);
        }

    }

}