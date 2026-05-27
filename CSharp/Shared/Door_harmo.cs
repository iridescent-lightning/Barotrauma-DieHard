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
using Networking;

namespace BarotraumaDieHard
{
    public class DoorData
    {
        public bool IsHalfOpenTarget;   // 标记该门是否处于半开目标状态
        public Vector2 OriginSimPos;    // 缓存门的原始物理世界坐标，避免每帧重复计算
        public bool IsCoordsCached;     // 坐标是否已被成功缓存
        public bool LastPhysicsApplied; // 记录上一次物理转换状态，避免重复 SetTransform
    }

    [HarmonyPatch(typeof(Door))]
    partial class DoorPatch
    {
        private static readonly ConditionalWeakTable<Door, DoorData> doorStateTable = new ConditionalWeakTable<Door, DoorData>();

        [HarmonyPatch("Select")]
        [HarmonyPrefix]
        public static bool Prefix_Select(Character character, Door __instance, ref bool __result)
        {
            if (__instance.IsBroken || character == null) return true;

            // 检查玩家是否按住特定按键
            if (character.IsKeyDown(InputType.Crouch))
            {
                var data = doorStateTable.GetOrCreateValue(__instance);
                bool newHalfOpenState = !data.IsHalfOpenTarget;

                if (GameMain.IsSingleplayer)
                {
                    data.IsHalfOpenTarget = newHalfOpenState;
                }
                else
                {
#if CLIENT
                    SendDoorHalfOpenMessage(__instance.Item, newHalfOpenState);
#endif
                }

                __result = true;
                return false; 
            }

            // 普通点击：如果处于半开，点击则恢复正常
            if (doorStateTable.TryGetValue(__instance, out var d) && d.IsHalfOpenTarget)
            {
                if (GameMain.IsSingleplayer)
                {
                    d.IsHalfOpenTarget = false;
                }
                else
                {
#if CLIENT
                    SendDoorHalfOpenMessage(__instance.Item, false);
#endif
                }
            }

            return true;
        }

        [HarmonyPatch("Update")]
        [HarmonyPrefix]
        public static bool Prefix_Update(float deltaTime, Camera cam, Door __instance)
        {
            if (__instance?.Item == null) return true;

            // ⭐【优化点 1】极其廉价的快速通道拦截：如果字典里压根没有这个门，或者它不是半开目标
            // 直接 return true 交还给原版，不执行任何矩阵转换、不计算坐标、不查字典创建新数据
            if (!doorStateTable.TryGetValue(__instance, out var data) || !data.IsHalfOpenTarget)
            {
                // 如果之前为了半开改动过物理位置，现在被关闭了，需要且仅需要还原一次
                if (data != null && data.LastPhysicsApplied && __instance.Body != null && data.IsCoordsCached)
                {
                    __instance.Body.SetTransform(data.OriginSimPos, 0f);
                    data.LastPhysicsApplied = false;
                }
                return true; 
            }

            // 如果门坏了，强制关闭半开状态
            if (__instance.IsBroken)
            {
                data.IsHalfOpenTarget = false;
                if (data.LastPhysicsApplied && __instance.Body != null && data.IsCoordsCached)
                {
                    __instance.Body.SetTransform(data.OriginSimPos, 0f);
                    data.LastPhysicsApplied = false;
                }
                return true;
            }

            // ⭐【优化点 2】静态原始物理坐标缓存化（懒加载）
            // 门的初始坐标在常规情况下是固定死绝对不会变的，只需要在门第一次进入半开状态时计算一次即可！
            if (!data.IsCoordsCached)
            {
                data.OriginSimPos = ConvertUnits.ToSimUnits(new Vector2(
                    __instance.Item.Rect.Center.X, 
                    __instance.Item.Rect.Y - __instance.Item.Rect.Height / 2f
                ));
                data.IsCoordsCached = true;
            }

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
                Vector2 targetSimPos = data.OriginSimPos; // 直接读取缓存的原始物理位置，零开销

                if (__instance.OpenState > 0.2f)
                {
                    if (__instance.IsHorizontal) 
                    {
                        float shiftX = ConvertUnits.ToSimUnits(__instance.Item.Rect.Width * __instance.OpenState * 1.05f);
                        targetSimPos = new Vector2(data.OriginSimPos.X - shiftX, data.OriginSimPos.Y);
                    }
                    else 
                    {
                        float shiftY = ConvertUnits.ToSimUnits(__instance.Item.Rect.Height * __instance.OpenState * 1.25f);
                        targetSimPos = new Vector2(data.OriginSimPos.X, data.OriginSimPos.Y + shiftY);
                    }
                }
                
                __instance.Body.SetTransform(targetSimPos, 0f);
                data.LastPhysicsApplied = true; // 标记我们修改过物理结构，用于之后复原
            }

            __instance.item.SendSignal("3", "state_out");
            return false; // 拦截原版 Update
        }

        [HarmonyPatch("IsPositionOnWindow")]
        [HarmonyPrefix]
        static bool Prefix(ref bool __result, Door __instance)
        {
            if (__instance != null && __instance.HasWindow)
            {
                __result = true;
                return false;
            }
            return true;
        }

        // ==========================================
        //            网络同步通道 (NETWORKING)
        // ==========================================
#if CLIENT
        public static void SendDoorHalfOpenMessage(Item item, bool isHalfOpen)
        {
            if (item == null) return;
            IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.SYNC_DOOR_HALF_OPEN);
            msg.WriteUInt16(item.ID);
            msg.WriteBoolean(isHalfOpen);
            NetUtil.SendServer(msg, DeliveryMethod.Reliable);
        }
#endif

        public static void OnReceiveDoorHalfOpenMessage(object[] args)
        {
            if (args == null || args.Length == 0) return;

            IReadMessage msg = args[0] as IReadMessage;
            if (msg == null) return;

            ushort itemID = msg.ReadUInt16();
            bool targetState = msg.ReadBoolean();

            Item item = Entity.FindEntityByID(itemID) as Item;
            if (item == null) return;

            Door door = item.GetComponent<Door>();
            if (door == null) return;

            var data = doorStateTable.GetOrCreateValue(door);
            data.IsHalfOpenTarget = targetState;

#if SERVER
            if (GameMain.Server != null)
            {
                DebugConsole.NewMessage($"[SERVER] 门半开状态同步: ID={itemID}, 状态={targetState}", Color.Green);
                IWriteMessage broadcastMsg = NetUtil.CreateNetMsg(NetEvent.SYNC_DOOR_HALF_OPEN);
                broadcastMsg.WriteUInt16(itemID);
                broadcastMsg.WriteBoolean(targetState);
                NetUtil.SendAll(broadcastMsg, DeliveryMethod.Reliable);
            }
#endif
        }
    }
}