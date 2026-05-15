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
            try
            {
                // 基础空值检查
                if (item == null || character == null)
                {
                    return true; // 让原版逻辑处理
                }

                // 获取维修组件，增加空值检查
                var repairable = item.GetComponent<Repairable>();
                if (repairable == null)
                {
                    return true;
                }

                // 检查维修阈值
                if (item.Condition > repairable.RepairThreshold)
                {
                    __result = false;
                    return false;
                }

                // --- 特赦逻辑：增加完整的空值检查 ---
                if (character.AIController is HumanAIController humanAI && 
                    humanAI.ObjectiveManager != null)
                {
                    var currentObj = humanAI.ObjectiveManager.GetCurrentObjective();
                    // 安全地比较 Identifier，避免在联机初始化时调用扩展方法
                    if (currentObj != null && 
                        currentObj.Identifier != null &&
                        currentObj.Identifier.Value == "repair.with.disconnect")
                    {
                        __result = true;
                        return false;
                    }
                }
                
                // 核心安全检查
                if (RepairableDieHard.IsDeviceElectrified(repairable) && 
                    item.Condition < item.MaxCondition)
                {
        #if CLIENT
                    // 客户端显示警告，但需要确保在服务器环境不会崩溃
                    if (GameMain.Client != null) // 只在客户端环境下执行
                    {
                        string localizedSpeech = TextManager.GetWithVariable(
                            "dialog.bots.brokendeviceconnectedtopoweredjunctionbox", 
                            "[itemname]", 
                            item.Name
                        ).Value;
                        character.Speak(localizedSpeech, null, 0.0f, "safetywarning".ToIdentifier(), 30.0f);
                    }
        #endif
                    __result = false;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                // 记录错误但不崩溃
                DebugConsole.Log($"Error in AIObjectiveRepairItemsPatch: {ex.Message}");
                return true; // 出错时让原版逻辑处理
            }
        }
    }
}