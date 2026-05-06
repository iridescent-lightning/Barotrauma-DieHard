using Barotrauma;
using Barotrauma.Items.Components;
using BarotraumaDieHard.Items;
using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Linq;

namespace BarotraumaDieHard.AI
{
    [HarmonyPatch(typeof(AIObjectiveFindSafety))]
    class AIObjectiveFindSafetyDieHard
    {
        private static AIObjectiveFindAndEquipRadiationSuit findAndEquipRadiationSuit;


        // 使用 Postfix 代替完全重写，逻辑更稳健
        [HarmonyPatch("GetPriority")]
        [HarmonyPostfix]
        public static void GetPriorityPostfix(AIObjectiveFindSafety __instance, ref float __result)
        {
            // 如果已经在战斗或忽略安全，不强制提升
            if (__result <= 0 && !__instance.objectiveManager.HasActiveObjective<AIObjectiveCombat>())
            {
                // 检测当前房间是否有危险燃料棒
                if (__instance.character.CurrentHull != null && 
                    RadioactiveFuelRod.DangerousFuelRods.Any(r => r.CurrentHull == __instance.character.CurrentHull))
                {
                    // 提升到紧急优先级
                    __result = MathHelper.Max(__result, AIObjectiveManager.EmergencyObjectivePriority - 5);
                }
            }
        }

        [HarmonyPatch("Act")]
        [HarmonyPrefix]
        public static bool ActPrefix(float deltaTime, AIObjectiveFindSafety __instance)
        {
            var _ = __instance;
            if (_.resetPriority) return true; // 返回 true 让原方法处理 reset

            // 1. 辐射检测逻辑封装
            bool needsRadSuit = _.character.CurrentHull != null && 
                               RadioactiveFuelRod.DangerousFuelRods.Any(r => r.CurrentHull == _.character.CurrentHull);

            if (needsRadSuit)
            {
                // 自动喊话（增加 CD 检查防止刷屏）
                _.character.Speak(TextManager.Get("dialog.bots.dangerousfuelrod").Value, 
                    identifier: "dialog.bots.dangerousfuelrod".ToIdentifier(), 
                    minDurationBetweenSimilar: 10.0f);

                // 2. 注入辐射服子目标
                if (!HumanAIControllerPatch.HasRadiationSuit(_.character))
                {
                    _.TryAddSubObjective(ref findAndEquipRadiationSuit,
                        constructor: () => new AIObjectiveFindAndEquipRadiationSuit(_.character, _.objectiveManager),
                        onAbandon: () => { _.searchHullTimer = 0; },
                        onCompleted: () => { _.resetPriority = true; });
                    
                    // 如果正在执行寻找辐射服，暂时中断原有的 Act 逻辑（防止逻辑冲突）
                    if (findAndEquipRadiationSuit != null && !findAndEquipRadiationSuit.IsCompleted)
                    {
                        return false; 
                    }
                }
            }

            // 返回 true，让原有的潜水服、寻找安全房间逻辑继续运行
            // 这样你就不需要手动复制那几百行代码了
            return true;
        }

    }
}