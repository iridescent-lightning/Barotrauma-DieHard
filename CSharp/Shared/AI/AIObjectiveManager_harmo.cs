// Exploring. No real feature built from it.
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



namespace BarotraumaDieHard.AI
{
    [HarmonyPatch(typeof(AIObjectiveManager))]
    class AIObjectiveManagerDieHard
    {

        [HarmonyPatch("CreateObjective")]
        [HarmonyPrefix]
        public static bool CreateObjectivePrefix(Order order, float priorityModifier, AIObjectiveManager __instance, ref AIObjective __result)
        {
            // 仅处理你新增的特殊 ID
            if (order?.Identifier.Value.ToLowerInvariant() == "findandequipradiationsuit")
            {
                __result = new AIObjectiveFindAndEquipRadiationSuit(__instance.character, __instance, priorityModifier);
                __result.Identifier = order.Identifier;
                __result.IgnoreAtOutpost = order.IgnoreAtOutpost;
                return false; // 拦截，不再跑原版代码
            }
            else if (order?.Identifier.Value.ToLowerInvariant() == "retrievefuelrod")
            {
                __result = new AIObjectiveRetrieveFuelRod(__instance.character, __instance, priorityModifier);
                __result.Identifier = order.Identifier;
                __result.IgnoreAtOutpost = order.IgnoreAtOutpost;
                return false; // 拦截，不再跑原版代码
            }
            else if (order?.Identifier.Value.ToLowerInvariant() == "operatejunctionbox")
            {
                Item targetJB = order.TargetEntity as Item;
                if (targetJB == null)
                {
                    // 尝试寻找全船最近的一个接线盒（且状态需要切换的）
                    targetJB = Item.ItemList.FindAll(i => i.HasTag("junctionbox") || i.GetComponent<PowerTransfer>() != null)
                        .OrderBy(i => Vector2.Distance(__instance.character.WorldPosition, i.WorldPosition))
                        .FirstOrDefault();
                }
                if (targetJB != null)
                {
                    if (!targetJB.IsInteractable(__instance.character)) { return false; }

                    // --- 核心逻辑修改：自动翻转 ---
                    // 获取当前接线盒的状态
                    bool currentState = PowerTransferPatch.GetLeverState(targetJB);
                    // 目标状态设为当前状态的反转
                    bool targetState = !currentState; 

                    // 实例化时，Option 传空即可，因为我们不需要子选项
                    var operateObjective = new AIObjectiveOperateJunctionBox(
                        __instance.character, 
                        targetJB, 
                        __instance, 
                        targetState, 
                        priorityModifier);

                    operateObjective.Completed += () => __instance.DismissSelf(order);
                    
                    __result = operateObjective;
                    return false;
                }
            }

            return true; // 其他所有情况，放行给原版代码处理
        }


    }
}