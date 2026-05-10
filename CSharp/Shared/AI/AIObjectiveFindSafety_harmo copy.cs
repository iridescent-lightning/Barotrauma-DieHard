using Barotrauma;
using Barotrauma.Items.Components;
using BarotraumaDieHard.Items;
using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Linq;
using System.Runtime.CompilerServices;

namespace BarotraumaDieHard.AI
{
    [HarmonyPatch(typeof(AIObjectiveFindSafety))]
    class AIObjectiveFindSafetyDieHard
    {
        // 使用弱引用表，为每个 AI 实例分配独立的子目标存储空间
        private static readonly ConditionalWeakTable<AIObjectiveFindSafety, StrongBox<AIObjective>> radSuitObjectives = 
            new ConditionalWeakTable<AIObjectiveFindSafety, StrongBox<AIObjective>>();
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
            if (__instance.resetPriority) return true;

            bool needsRadSuit = __instance.character.CurrentHull != null && 
                                RadioactiveFuelRod.DangerousFuelRods.Any(r => r.CurrentHull == __instance.character.CurrentHull);

            if (needsRadSuit && !HumanAIControllerPatch.HasRadiationSuit(__instance.character))
            {
                // 获取该实例专属的子目标引用
                var subObjBox = radSuitObjectives.GetOrCreateValue(__instance);

                __instance.character.Speak(TextManager.Get("dialog.bots.dangerousfuelrod").Value, 
                    identifier: "dialog.bots.dangerousfuelrod".ToIdentifier(), 
                    minDurationBetweenSimilar: 10.0f);

                __instance.TryAddSubObjective(ref subObjBox.Value,
                    constructor: () => new AIObjectiveFindAndEquipRadiationSuit(__instance.character, __instance.objectiveManager),
                    onAbandon: () => { __instance.searchHullTimer = 0; },
                    onCompleted: () => { __instance.resetPriority = true; });

                if (subObjBox.Value != null && !subObjBox.Value.IsCompleted)
                {
                    // 关键：既然正在找辐射服，就拦截掉原版的“找安全房间”逻辑
                    return true; 
                }
            }
            return true;
        }

    }
}