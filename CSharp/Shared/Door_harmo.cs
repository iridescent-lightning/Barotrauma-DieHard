using Barotrauma.Networking;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using FarseerPhysics.Dynamics;
using Barotrauma;
#if CLIENT
using Barotrauma.Lights;
#endif
using Barotrauma.Extensions;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Barotrauma.Items.Components;
using Networking; // 确保引入了你的 NetUtil 命名空间

namespace BarotraumaDieHard
{
    public class DoorData
    {
        public bool IsHalfOpenTarget; // 标记该门是否处于半开目标状态
    }

    [HarmonyPatch(typeof(Door))]
    partial class DoorPatch
    {
        // 使用 ConditionalWeakTable 自动管理内存
        private static ConditionalWeakTable<Door, DoorData> doorStateTable = new ConditionalWeakTable<Door, DoorData>();

        

        [HarmonyPatch("Select")]
        [HarmonyPrefix]
        public static bool Prefix_Select(Character character, Door __instance, ref bool __result)
        {
            if (__instance.IsBroken || character == null) return true;

            // 检查玩家是否按住特定按键（如蹲伏键/Crouch，对应 Shift/Ctrl 取决于键位）
            if (character.IsKeyDown(InputType.Crouch))
            {
                var data = doorStateTable.GetOrCreateValue(__instance);
                bool newHalfOpenState = !data.IsHalfOpenTarget; // 翻转本地预测状态

                if (GameMain.IsSingleplayer)
                {
                    // 单机模式：直接生效
                    data.IsHalfOpenTarget = newHalfOpenState;
                }
                else
                {
#if CLIENT
                    // 联机模式：不要直接修改本地字典，而是向服务器发送“申请包”
                    SendDoorHalfOpenMessage(__instance.Item, newHalfOpenState);
#endif
                }

                __result = true;
                return false; // 拦截原版的“开/关门”动作
            }

            // 如果是普通点击，说明玩家想正常开/关门
            if (doorStateTable.TryGetValue(__instance, out var d))
            {
                if (d.IsHalfOpenTarget)
                {
                    if (GameMain.IsSingleplayer)
                    {
                        d.IsHalfOpenTarget = false;
                    }
                    else
                    {
                        #if CLIENT
                        // 联机模式：向服务器申请关闭半开状态，恢复正常
                        SendDoorHalfOpenMessage(__instance.Item, false);
                        #endif
                    }
                }
            }

            return true;
        }

        [HarmonyPatch("Update")]
        [HarmonyPrefix]
        public static bool Prefix_Update(float deltaTime, Camera cam, Door __instance)
        {
            if (__instance == null || __instance.Item == null) return true;

            // 1. 获取门的原始物理位置
            Vector2 originSimPos = ConvertUnits.ToSimUnits(new Vector2(__instance.Item.Rect.Center.X, __instance.Item.Rect.Y - __instance.Item.Rect.Height / 2f));

            // 检查半开标记
            if (doorStateTable.TryGetValue(__instance, out var data) && data.IsHalfOpenTarget && !__instance.IsBroken)
            {
                // --- 自动移动逻辑 ---
                float targetState = 0.5f;
                float diff = targetState - __instance.OpenState;
                if (Math.Abs(diff) > 0.01f)
                {
                    float moveAmount = (diff > 0 ? __instance.OpeningSpeed : -__instance.ClosingSpeed) * deltaTime;
                    __instance.OpenState += moveAmount;
                }
                else
                {
                    __instance.OpenState = targetState;
                }

                // 同步缝隙
                if (__instance.LinkedGap != null) { __instance.LinkedGap.Open = __instance.OpenState; }
                
                // --- 物理碰撞体逻辑 ---
                if (__instance.Body != null)
                {
                    __instance.Body.Enabled = true;
                    Vector2 targetSimPos = originSimPos;

                    if (__instance.OpenState > 0.2f)
                    {
                        if (__instance.IsHorizontal) 
                        {
                            float shiftX = ConvertUnits.ToSimUnits(__instance.Item.Rect.Width * __instance.OpenState * 1.05f);
                            targetSimPos = new Vector2(originSimPos.X - shiftX, originSimPos.Y);
                        }
                        else 
                        {
                            float shiftY = ConvertUnits.ToSimUnits(__instance.Item.Rect.Height * __instance.OpenState * 1.25f);
                            targetSimPos = new Vector2(originSimPos.X, originSimPos.Y + shiftY);
                        }
                    }
                    
                    __instance.Body.SetTransform(targetSimPos, 0f);
                }

                __instance.item.SendSignal("3", "state_out");
                return false; // 拦截原版 Update，防止原版把 OpenState 强行掰回 0 或 1
            }
            else
            {
                // 如果没有半开标记，把碰撞体坐标拉回来
                if (__instance.Body != null)
                {
                    if (Vector2.DistanceSquared(__instance.Body.SimPosition, originSimPos) > 0.001f)
                    {
                        __instance.Body.SetTransform(originSimPos, 0f);
                    }
                }
            }

            return true; 
        }

        [HarmonyPatch("IsPositionOnWindow")]
        [HarmonyPrefix]
        static bool Prefix(ref bool __result, Door __instance)
        {
            if (__instance.HasWindow)
            {
                __result = true;
            }
            return false;
        }

        // ==========================================
        //             网络同步通道 (NETWORKING)
        // ==========================================

        /// <summary>
        /// 发送端：客户端申请改变某个门的半开状态
        /// </summary>
#if CLIENT
        public static void SendDoorHalfOpenMessage(Item item, bool isHalfOpen)
        {
            
            IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.SYNC_DOOR_HALF_OPEN);
            msg.WriteUInt16(item.ID);
            msg.WriteBoolean(isHalfOpen);

            NetUtil.SendServer(msg, DeliveryMethod.Reliable);
        }
#endif

        /// <summary>
        /// 接收端：服务器接收申请并广播 / 客户端接收服务器的最终确认广播
        /// </summary>
        public static void OnReceiveDoorHalfOpenMessage(object[] args)
        {
            if (args == null || args.Length == 0) return;

            IReadMessage msg = args[0] as IReadMessage;
            if (msg == null) return;

            // 严格按顺序读取：ItemID -> bool状态
            ushort itemID = msg.ReadUInt16();
            bool targetState = msg.ReadBoolean();

            // 获取本地对应的物品与组件
            Item item = Entity.FindEntityByID(itemID) as Item;
            if (item == null) return;

            Door door = item.GetComponent<Door>();
            if (door == null) return;

            // 【关键】所有端（服务器和客户端）同步更新字典数据
            var data = doorStateTable.GetOrCreateValue(door);
            data.IsHalfOpenTarget = targetState;
#if SERVER
            // 如果是在服务器端收到这个数据
            if (GameMain.Server != null)
            {
                // 服务器控制台打印调试信息
                DebugConsole.NewMessage($"[SERVER] 门半开状态同步: ID={itemID}, 状态={targetState}", Color.Green);

                // 构建广播包，转发给所有人
                IWriteMessage broadcastMsg = NetUtil.CreateNetMsg(NetEvent.SYNC_DOOR_HALF_OPEN);
                broadcastMsg.WriteUInt16(itemID);
                broadcastMsg.WriteBoolean(targetState);

                // 广播全员
                NetUtil.SendAll(broadcastMsg, DeliveryMethod.Reliable);
            }
#else
             if (GameMain.Client != null)
            {
                // 客户端控制台打印，按 F3 可见，用于验证闭环
                //DebugConsole.NewMessage($"[CLIENT] 收到服务器门的广播! ID={itemID}, 状态={targetState}", Color.Cyan);
            }
#endif
        }
    }
}