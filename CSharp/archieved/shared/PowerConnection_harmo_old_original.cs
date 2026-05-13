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

        // 2. 处理保险丝与设备状态
        CustomJunctionBox device = conn1.Item.GetComponent<CustomJunctionBox>();
        if (device != null && device.BrokenFuse)
        {
            __result = false;
            return false; // 保险丝断了，直接判定连接无效
        }

        if (conn1.Item.Condition <= 0.0f || conn2.Item.Condition <= 0.0f)
        {
            __result = false;
            return false; // 设备坏了，断电
        }

        return true; // 其他情况交给原版逻辑处理，保证兼容性
    }
}
    

}