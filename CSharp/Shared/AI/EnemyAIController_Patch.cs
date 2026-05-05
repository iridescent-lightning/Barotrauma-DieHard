//Only in Character we make the AI canseethroughwindows is enough. We don't need logic here
//No. This type is only used for non-human characters.
/*using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using HarmonyLib;
using Barotrauma;

namespace BarotraumaDieHard.AI
{
    [HarmonyPatch(typeof(EnemyAIController))]
    public static class EnemyAIControllerPatch
    {
        [HarmonyPatch("CanSeeTarget")]
        [HarmonyPrefix]
        public static bool Prefix(ISpatialEntity target, EnemyAIController __instance, ref bool __result, ref float ___lastVisibilityCheckTime, ref bool ___canSeeTarget)
        {
            // 模拟原有的缓存计时逻辑
            if (Timing.TotalTime > ___lastVisibilityCheckTime + EnemyAIController.VisibilityCheckStep)
            {
                // 调用 Character 的方法并强制开启视线穿透窗户
                ___canSeeTarget = __instance.Character.CanSeeTarget(target, seeThroughWindows: true);
                ___lastVisibilityCheckTime = (float)Timing.TotalTime;
            }
            __result = ___canSeeTarget;
            return false; // 拦截原方法
        }
    }
}*/
