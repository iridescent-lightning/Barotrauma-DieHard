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
            // --- 核心修改：特赦逻辑 ---
        if (character.AIController is HumanAIController humanAI)
        {
            // 如果 AI 已经在执行我们的“断电维修”任务，直接允许，不再检查带电状态
            var currentObj = humanAI.ObjectiveManager.GetCurrentObjective();
            if (currentObj?.Identifier == "repair.with.disconnect".ToIdentifier())
            {
                __result = true; 
                return false; // 拦截，不走后面的带电检查
            }
        }

        
            // 获取维修组件
            var repairable = item.GetComponent<Repairable>();
            if (item.Condition > repairable.RepairThreshold) return false;
            // 核心安全检查：如果带电，直接拦截原版逻辑
            if (repairable != null && RepairableDieHard.IsDeviceElectrified(repairable) && item.Condition < item.MaxCondition)
            {
#if CLIENT
                string localizedSpeech = TextManager.GetWithVariable(
                    "dialog.bots.brokendeviceconnectedtopoweredjunctionbox", 
                    "[itemname]", 
                    item.Name
                ).Value;

                character.Speak(localizedSpeech, null, 0.0f, "safetywarning".ToIdentifier(), 30.0f);
#endif
                    
                __result = false; // 告知 AI：这个目标不合法
                return false;    // 【关键】返回 false 以拦截原版 ViableForRepair 的执行
            }

            // 如果不带电，则返回 true，允许原版方法继续执行（去检查火灾、敌人、技能等）
            return true; 
        }
    }
}