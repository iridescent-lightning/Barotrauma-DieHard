using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Barotrauma;
using Barotrauma.Items.Components;
using FarseerPhysics;

namespace ModElevatorNamespace
{
    class Elevator : ItemComponent
    {
        [Serialize(150.0f, IsPropertySaveable.Yes, description: "电梯移动速度 (像素/秒)")]
        public float MoveSpeed { get; set; }

        [Serialize(true, IsPropertySaveable.Yes, description: "是否输出调试信息到接线板")]
        public bool EnableDebugOutput { get; set; }

        private enum MoveDirection { None, Up, Down }
        private MoveDirection currentDir = MoveDirection.None;

        private List<Item> topStoppers = new List<Item>();
        private List<Item> bottomStoppers = new List<Item>();

        // 累积剩余位移（用于处理取整后的余数）
        private float remainingMovement = 0f;

        // 调试用：记录上次打印时间
        private float debugTimer = 0f;
        private float debugOutputTimer = 0f;
        private Vector2 lastPosition = Vector2.Zero;

        // 连接点
        private Connection debugOutConnection;

        public Elevator(Item item, ContentXElement element) : base(item, element)
        {
            IsActive = true;
            FindStoppers();
            SetupCollision();
            lastPosition = item.WorldPosition;
        }

        public override void OnItemLoaded()
        {
            // 查找调试输出连接点
            foreach (var connection in item.Connections)
            {
                if (connection.Name == "debug_out")
                {
                    debugOutConnection = connection;
                    break;
                }
            }
        }

        // 设置碰撞属性
        private void SetupCollision()
        {
            if (item.body != null)
                ModifyBody(item.body);

            var door = item.GetComponent<Door>();
            if (door?.Body != null)
                ModifyBody(door.Body);
        }

        private void ModifyBody(PhysicsBody body)
        {
            body.CollisionCategories = Physics.CollisionWall;
            body.CollidesWith = Physics.CollisionWall | Physics.CollisionLevel | Physics.CollisionCharacter 
                              | Physics.CollisionItem | Physics.CollisionProjectile | Physics.CollisionPlatform;
            
            if (body.FarseerBody != null)
            {
                body.FarseerBody.Friction = 0.8f;
            }
        }

        // 查找限位器
        private void FindStoppers()
        {
            topStoppers.Clear();
            bottomStoppers.Clear();
            
            foreach (Item it in Item.ItemList)
            {
                if (it.Prefab.Identifier == "lifttopstop")
                    topStoppers.Add(it);
                else if (it.Prefab.Identifier == "liftbottomstop")
                    bottomStoppers.Add(it);
            }
        }

        // 接收信号
        // 接收信号
public override void ReceiveSignal(Signal signal, Connection connection)
{
    if (connection.Name == "moveup")
    {
        if (signal.value == "1")
        {
            // 上升信号
            if (currentDir == MoveDirection.Down)
            {
                currentDir = MoveDirection.None;
            }
            else
            {
                currentDir = MoveDirection.Up;
                remainingMovement = 0f;
                SendDebugOutput($"收到上升信号，速度: {MoveSpeed} px/秒");
            }
        }
        else if (signal.value == "0")
        {
            // 上升信号断开，如果正在上升则立即停止
            if (currentDir == MoveDirection.Up)
            {
                SendDebugOutput($"上升信号断开，立即停止");
                StopMovement();
            }
        }
    }
    else if (connection.Name == "movedown")
    {
        if (signal.value == "1")
        {
            // 下降信号
            if (currentDir == MoveDirection.Up)
            {
                currentDir = MoveDirection.None;
            }
            else
            {
                currentDir = MoveDirection.Down;
                remainingMovement = 0f;
                SendDebugOutput($"收到下降信号，速度: {MoveSpeed} px/秒");
            }
        }
        else if (signal.value == "0")
        {
            // 下降信号断开，如果正在下降则立即停止
            if (currentDir == MoveDirection.Down)
            {
                SendDebugOutput($"下降信号断开，立即停止");
                StopMovement();
            }
        }
    }
}

        // 主更新循环
        public override void Update(float deltaTime, Camera cam)
        {
            // 没有移动指令时停止
            if (currentDir == MoveDirection.None)
                return;

            // 每帧查找限位器
            FindStoppers();

            // 计算并执行移动
            Vector2 moveVector = CalculateMovement(deltaTime);
            
            if (moveVector != Vector2.Zero)
            {
                ExecuteMovement(moveVector, deltaTime);
            }
            
            // 定期打印调试信息（每秒打印2次）
            debugTimer += deltaTime;
            if (debugTimer >= 0.5f)
            {
                debugTimer = 0f;
                //PrintDebugInfo(deltaTime);
            }
            
            // 定期输出调试信息到接线板（每秒1次）
            debugOutputTimer += deltaTime;
            if (EnableDebugOutput && debugOutputTimer >= 3.0f)
            {
                debugOutputTimer = 0f;
                OutputDebugInfoToConnection(deltaTime);
            }
        }

        // 找到下一个限位器（在移动方向上的最近限位器）
        private Item GetNextStopper(List<Item> stoppers, MoveDirection direction, float currentY)
        {
            Item nextStopper = null;
            float closestDistance = float.MaxValue;
            
            foreach (var stopper in stoppers)
            {
                float stopperY = stopper.WorldPosition.Y;
                float distance = 0;
                
                if (direction == MoveDirection.Up)
                {
                    // 向上移动：找位置在上方且最近的限位器
                    if (stopperY > currentY)
                    {
                        distance = stopperY - currentY;
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            nextStopper = stopper;
                        }
                    }
                }
                else if (direction == MoveDirection.Down)
                {
                    // 向下移动：找位置在下方且最近的限位器
                    if (stopperY < currentY)
                    {
                        distance = currentY - stopperY;
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            nextStopper = stopper;
                        }
                    }
                }
            }
            
            return nextStopper;
        }

        // 检查是否到达限位器
        private bool CheckReachedStopper(Item targetStopper)
        {
            if (targetStopper == null) return false;
            
            float distance = Vector2.Distance(targetStopper.WorldPosition, item.WorldPosition);
            return distance < 15f;
        }

        // 计算移动向量（返回整数位移）
        private Vector2 CalculateMovement(float deltaTime)
        {
            // 计算理论位移（浮点数）
            float theoreticalMove = MoveSpeed * deltaTime;
            
            // 加上之前剩余的余数
            float totalMove = theoreticalMove + remainingMovement;
            
            // 取整（向下取整，保留余数）
            int intMove = (int)Math.Floor(Math.Abs(totalMove));
            
            // 根据方向确定正负
            if (currentDir == MoveDirection.Down)
            {
                intMove = -intMove;
            }
            
            // 计算新的余数
            remainingMovement = Math.Abs(totalMove) - Math.Abs(intMove);
            if (currentDir == MoveDirection.Down)
            {
                remainingMovement = -remainingMovement;
            }
            
            float step = intMove;
            if (step == 0) return Vector2.Zero;
            
            // 获取下一个限位器
            Item nextStopper = null;
            if (currentDir == MoveDirection.Up)
            {
                nextStopper = GetNextStopper(topStoppers, MoveDirection.Up, item.WorldPosition.Y);
            }
            else if (currentDir == MoveDirection.Down)
            {
                nextStopper = GetNextStopper(bottomStoppers, MoveDirection.Down, item.WorldPosition.Y);
            }
            
            // 如果没有限位器，继续移动
            if (nextStopper == null)
            {
                item.SendSignal("0", "Dock_State");
                return new Vector2(0, step);
            }
            
            // 检查是否到达目标限位器
            if (CheckReachedStopper(nextStopper))
            {
                // 到达限位器，停止
                string message = $"到达 {(currentDir == MoveDirection.Up ? "顶部" : "底部")} 限位器！";
                SendDebugOutput(message);
                //DebugConsole.NewMessage($"[限位器] {message}");
                StopMovement();
                return Vector2.Zero;
            }
            
            // 检查是否会超过限位器
            float newY = item.WorldPosition.Y + step;
            float stopperY = nextStopper.WorldPosition.Y;
            
            if (currentDir == MoveDirection.Up && newY >= stopperY)
            {
                // 会超过限位器，直接移动到限位器位置
                float remainingDistance = stopperY - item.WorldPosition.Y;
                int finalMove = (int)Math.Floor(remainingDistance);
                if (finalMove != 0)
                {
                    item.SendSignal("0", "Dock_State");
                    return new Vector2(0, finalMove);
                }
                else
                {
                    StopMovement();
                    return Vector2.Zero;
                }
            }
            else if (currentDir == MoveDirection.Down && newY <= stopperY)
            {
                // 会超过限位器，直接移动到限位器位置
                float remainingDistance = item.WorldPosition.Y - stopperY;
                int finalMove = (int)Math.Floor(remainingDistance);
                if (finalMove != 0)
                {
                    item.SendSignal("0", "Dock_State");
                    return new Vector2(0, -finalMove);
                }
                else
                {
                    StopMovement();
                    return Vector2.Zero;
                }
            }
            
            // 正常移动
            item.SendSignal("0", "Dock_State");
            return new Vector2(0, step);
        }

        // 停止移动
        private void StopMovement()
        {
            currentDir = MoveDirection.None;
            remainingMovement = 0f;
            item.SendSignal("1", "Dock_State");
            SendDebugOutput($"已停止，最终位置: {item.WorldPosition.Y:F1}");
            //DebugConsole.NewMessage($"[电梯] 已停止，最终位置: {item.WorldPosition}");
        }

        // 统一执行移动
        private void ExecuteMovement(Vector2 moveVector, float deltaTime)
        {
            // 只在下降时修复角色地板检测
            if (currentDir == MoveDirection.Down)
            {
                FixCharacterFloor(moveVector);
            }
            
            // 移动电梯（moveVector已经是整数）
            item.Move(moveVector, ignoreContacts: false);
            
            // 移动连接的控制器
            MoveLinkedItems(moveVector);
        }

        // 发送调试输出到接线板
        private void SendDebugOutput(string message)
        {
            if (!EnableDebugOutput) return;
            if (debugOutConnection != null)
            {
                item.SendSignal(new Signal(message), debugOutConnection);
            }
        }

        // 输出调试信息到接线板
        private void OutputDebugInfoToConnection(float deltaTime)
        {
            if (debugOutConnection == null) return;
            
            Vector2 currentPos = item.WorldPosition;
            
            // 构建调试信息字符串
            string direction = currentDir == MoveDirection.Up ? "UP" : (currentDir == MoveDirection.Down ? "DOWN" : "STOP");
            string debugInfo = $"DIR:{direction}|SPD:{MoveSpeed}|Y:{currentPos.Y:F0}|REM:{remainingMovement:F2}";
            
            // 添加限位器距离信息
            if (currentDir == MoveDirection.Up)
            {
                var next = GetNextStopper(topStoppers, MoveDirection.Up, currentPos.Y);
                if (next != null)
                {
                    float distance = next.WorldPosition.Y - currentPos.Y;
                    debugInfo += $"|DST:{distance:F0}";
                }
            }
            else if (currentDir == MoveDirection.Down)
            {
                var next = GetNextStopper(bottomStoppers, MoveDirection.Down, currentPos.Y);
                if (next != null)
                {
                    float distance = currentPos.Y - next.WorldPosition.Y;
                    debugInfo += $"|DST:{distance:F0}";
                }
            }
            if (currentDir == MoveDirection.None) return;
            
            
            
            item.SendSignal(new Signal(debugInfo), debugOutConnection);
        }

        // 打印调试信息到控制台
        private void PrintDebugInfo(float deltaTime)
        {
            if (currentDir == MoveDirection.None) return;
            
            // 计算电梯实际速度
            Vector2 currentPos = item.WorldPosition;
            Vector2 velocity = (currentPos - lastPosition) / deltaTime;
            lastPosition = currentPos;
            
            float theoreticalMove = MoveSpeed * deltaTime;
            float actualMove = Math.Abs(velocity.Y * deltaTime);
            
            DebugConsole.NewMessage($"========== 电梯调试信息 ==========");
            DebugConsole.NewMessage($"方向: {(currentDir == MoveDirection.Up ? "↑ 上升" : "↓ 下降")}");
            DebugConsole.NewMessage($"目标速度: {MoveSpeed} px/秒");
            DebugConsole.NewMessage($"理论位移: {theoreticalMove:F3} px/帧");
            DebugConsole.NewMessage($"实际位移: {actualMove:F3} px/帧");
            DebugConsole.NewMessage($"累积余数: {remainingMovement:F3} px");
            DebugConsole.NewMessage($"实际速度: {Math.Abs(velocity.Y):F1} px/秒");
            DebugConsole.NewMessage($"当前位置: ({currentPos.X:F1}, {currentPos.Y:F1})");
            DebugConsole.NewMessage($"DeltaTime: {deltaTime:F4} 秒");
            
            // 显示下一个限位器
            if (currentDir == MoveDirection.Up)
            {
                var next = GetNextStopper(topStoppers, MoveDirection.Up, currentPos.Y);
                if (next != null)
                {
                    float distance = next.WorldPosition.Y - currentPos.Y;
                    //DebugConsole.NewMessage($"下一个顶部限位器距离: {distance:F1} px");
                }
            }
            else if (currentDir == MoveDirection.Down)
            {
                var next = GetNextStopper(bottomStoppers, MoveDirection.Down, currentPos.Y);
                if (next != null)
                {
                    float distance = currentPos.Y - next.WorldPosition.Y;
                    //DebugConsole.NewMessage($"下一个底部限位器距离: {distance:F1} px");
                }
            }
            
            // 打印连接的物品信息
            if (item.linkedTo != null && item.linkedTo.Count > 0)
            {
                DebugConsole.NewMessage($"--- 连接的物品 ({item.linkedTo.Count}个) ---");
                foreach (var linkedEntity in item.linkedTo)
                {
                    if (linkedEntity is Item linkedItem)
                    {
                        DebugConsole.NewMessage($"物品: {linkedItem.Prefab.Identifier}");
                        DebugConsole.NewMessage($"  位置: ({linkedItem.WorldPosition.X:F1}, {linkedItem.WorldPosition.Y:F1})");
                    }
                }
            }
            
            DebugConsole.NewMessage($"==================================");
        }

        // 修复角色地板检测 - 下降时让玩家能站在电梯上
        private void FixCharacterFloor(Vector2 moveVector)
        {
            // 检测区域：电梯上方一定范围
            float width = 130f;
            float detectHeight = 200f;
            
            RectangleF detectArea = new RectangleF(
                item.WorldPosition.X - width / 2f,
                item.WorldPosition.Y - 5f,
                width,
                detectHeight
            );
            
            int fixedCount = 0;
            foreach (Character character in Character.CharacterList)
            {
                if (character.Removed || character.IsDead || character.AnimController == null)
                    continue;
                
                // 只处理正常状态的角色
                if (!character.IsRagdolled && detectArea.Contains(character.WorldPosition.X, character.WorldPosition.Y))
                {
                    // 强制设置地板位置为电梯顶部
                    float floorOffset = 100f;
                    Vector2 floorPos = new Vector2(item.WorldPosition.X, item.WorldPosition.Y - floorOffset);
                    
                    character.AnimController.GetFloorY(floorPos);
                    character.AnimController.ForceRefreshFloorY();
                    fixedCount++;
                }
            }
            
            if (fixedCount > 0 && debugTimer < 0.1f)
            {
                string msg = $"修正了 {fixedCount} 个角色的地板检测";
                SendDebugOutput(msg);
                //DebugConsole.NewMessage($"[地板修复] {msg}");
            }
        }

        // 移动连接的物品
        private void MoveLinkedItems(Vector2 moveVector)
        {
            if (item.linkedTo == null) return;
            
            foreach (var linkedEntity in item.linkedTo)
            {
                if (linkedEntity is Item linkedItem)
                {
                    // 直接使用整数位移移动
                    linkedItem.Move(moveVector, ignoreContacts: true);
                }
            }
        }
    }
}