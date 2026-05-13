using System;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using System.Reflection;
using Barotrauma;
using Barotrauma.Items.Components;
#if CLIENT
using Barotrauma.Sounds;
#endif

namespace BarotraumaDieHard

{
    [HarmonyPatch(typeof(Powered))]
    public static class PoweredPatch
    {

        [HarmonyPatch("ValidPowerConnection")]
        [HarmonyPrefix]

        public static bool ValidPowerConnection(Connection conn1, Connection conn2, ref bool __result)

        {
            // 1. 处理蒸汽逻辑
            if (conn1.Name.StartsWith("steam") || conn2.Name.StartsWith("steam"))
            {
                __result = conn1.Name.StartsWith("steam") && conn2.Name.StartsWith("steam") && (
                    conn1.IsOutput != conn2.IsOutput || conn1.Name == "steam" || conn2.Name == "steam"
                );

                return false; // 拦截，不再执行原版逻辑
            }

            // 2. 处理开关与保险丝状态
            // 检查连接点所属的设备
            if (IsConnectionBroken(conn1) || IsConnectionBroken(conn2))
            {
                __result = false;

                return false;
            }

            return true; // 其他情况交给原版逻辑处理，保证兼容性
        }



        private static bool IsConnectionBroken(Connection conn)
        {
            if (conn.Item == null) return false;

            // 获取 PowerTransfer 组件
            var pt = conn.Item.GetComponent<PowerTransfer>();
            if (pt == null) return false;

            // 判定条件 A: 设备物理损坏
            if (conn.Item.Condition <= 0.0f) return true;
            // 判定条件 B: 开关被关闭 (从字典读)
            bool leverState = PowerTransferPatch.GetLeverState(conn.Item);

            if (!leverState) return true;

            // 判定条件 C: 保险丝烧断 (检查 Inventory 第一个槽位)

            var inv = conn.Item.OwnInventory;
            if (inv != null)
            {
                var fuse = inv.GetItemAt(0);

                if (fuse != null && fuse.Condition <= 0.0f) return true;

                // 如果是必须插保险丝才能通电，还要加一行：if (fuse == null) return true;
            }

            return false;

        }

    }

   



}