using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Barotrauma.Networking; // used by the server
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

using HarmonyLib;
using System.Reflection;
using System.Xml.Linq;

using Barotrauma;


namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(AIObjectiveCheckStolenItems))]
    class AIObjectiveCheckStolenItemsPatch
    {

        // 用于追踪当前被检查角色持有的违禁武器，防止 Warn 阶段找不到引用
        private static readonly Dictionary<AIObjectiveCheckStolenItems, Item> detectedGuns = new Dictionary<AIObjectiveCheckStolenItems, Item>();

        [HarmonyPatch("Inspect")]
        [HarmonyPostfix]
        public static void InspectPostfix(float deltaTime, AIObjectiveCheckStolenItems __instance)
        {
            // 如果检查还未完成（计时器还在跑），不执行后续逻辑
            // 注意：私有字段 inspectTimer 需要通过反射获取，或者检查状态
            if (__instance.character.SelectedCharacter == null) return;

            var target = __instance.character.SelectedCharacter;
            Item forbiddenGun = target.Inventory.FindItemByTag("longarm", recursive: true);

            if (forbiddenGun != null)
            {
                // 记录发现的枪支
                detectedGuns[__instance] = forbiddenGun;
                
                // 警卫说话警告
                __instance.character.Speak(TextManager.Get("dialogcheckfirearms").Value, identifier: "checkfirearms".ToIdentifier(), minDurationBetweenSimilar: 10f);
                
                // 强制切换到警告状态
                var stateField = typeof(AIObjectiveCheckStolenItems).GetField("currentState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                stateField?.SetValue(__instance, 2); // 2 对应 State.Warn
            }
        }

        [HarmonyPatch("Warn")]
        [HarmonyPrefix] // 使用 Prefix 抢在原始逻辑前执行，以便处理非赃物标签的武器
        public static bool WarnPrefix(float deltaTime, AIObjectiveCheckStolenItems __instance)
        {
            // 检查计时器是否结束
            var warnTimerField = typeof(AIObjectiveCheckStolenItems).GetField("warnTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            float warnTimer = (float)(warnTimerField?.GetValue(__instance) ?? 0f);

            //if (warnTimer > 0.0f) return true; // 让计时器继续跑

            if (detectedGuns.TryGetValue(__instance, out Item gun))
            {
                // 检查玩家是否还拿着这把枪
                if (gun.GetRootInventoryOwner() == __instance.Target)
                {
                    // 还在身上：执行逮捕逻辑
                    __instance.character.Speak(TextManager.Get("dialogcheckfirearms.Arrest").Value);
                    
                    // 调用原始类的私有 Arrest 方法
                    var arrestMethod = typeof(AIObjectiveCheckStolenItems).GetMethod("Arrest", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    arrestMethod?.Invoke(__instance, new object[] { true, true });

                    // 扣除声望（可选，模拟非法携带武器的后果）
                    HumanAIController.ApplyStealingReputationLoss(gun);
                }
                else
                {
                    // 玩家已经把枪扔了或收起来了（如果要求是不能带在身上）
                    __instance.character.Speak(TextManager.Get("dialogcheckstolenitems.comply").Value);
                }

                // 清理记录并结束任务[cite: 1]
                detectedGuns.Remove(__instance);
                __instance.IsCompleted = true;
                return false; // 跳过原始的 Warn 逻辑，因为原始逻辑只查赃物列表
            }

            return true; // 如果没有发现违禁枪支，执行原始的赃物检查逻辑
        }
}
}