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

            return true; // 其他所有情况，放行给原版代码处理
        }


    }
}