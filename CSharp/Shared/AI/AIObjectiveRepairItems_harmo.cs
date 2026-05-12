using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Barotrauma.Networking; // used by the server
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

using HarmonyLib;
using Barotrauma;

namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(AIObjectiveRepairItems))]
public class AIObjectiveRepairItemsPatch
{
    [HarmonyPatch("IsValidTarget")]
    [HarmonyPatch(new Type[] { typeof(Item), typeof(Character) })]
    [HarmonyPrefix]
    public static bool IsValidTargetPrefix(Item item, Character character, ref bool __result)
    {
        
        // 获取维修组件
        var repairable = item.GetComponent<Repairable>();
        
        // 核心安全检查：如果带电，直接拦截原版逻辑
        if (repairable != null && RepairableDieHard.IsDeviceElectrified(repairable))
        {
            string localizedSpeech = TextManager.GetWithVariable(
    "dialog.bots.brokendeviceconnectedtopoweredjunctionbox", 
    "[itemname]", 
    item.Name
).Value;
            character.Speak(localizedSpeech, null, 0.0f, "safetywarning".ToIdentifier(), 30.0f);
                
            __result = false; // 告知 AI：这个目标不合法
            return false;    // 【关键】返回 false 以拦截原版 ViableForRepair 的执行
        }

        // 如果不带电，则返回 true，允许原版方法继续执行（去检查火灾、敌人、技能等）
        return true; 
    }
}
}