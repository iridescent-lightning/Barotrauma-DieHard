using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using static Barotrauma.AIObjectiveFindSafety;
using System.Collections.Immutable;
using System.Diagnostics;
using FarseerPhysics;
using Barotrauma;
using HarmonyLib;
using Barotrauma.Networking;

[HarmonyPatch(typeof(AIObjectiveCombat), "Update")]
public class CombatDoorPatch
{
    public static void Prefix(AIObjectiveCombat __instance, float deltaTime)
    {
        Character character = __instance.character;
        Character target = __instance.Enemy;
        if (target == null || character == null) return;

        // 射线检测
        var obstacle = Submarine.PickBody(
            character.SimPosition, 
            target.SimPosition, 
            collisionCategory: Physics.CollisionWall | Physics.CollisionItem);

        if (obstacle?.UserData is Item item && item.GetComponent<Barotrauma.Items.Components.Door>() != null && item.GetComponent<Barotrauma.Items.Components.Door>().HasWindow)
        {
            //__instance.holdFireTimer = 5.0f; 
            // 如果能访问到 aimTimer，将其重置
            character.Speak(TextManager.Get("dialog.bots.spottedenemybehinddoor").Value, ChatMessageType.Default, 0.0f, "dialog.bots.spottedenemybehinddoor".ToIdentifier(), 10.0f
                    );
            AccessTools.Field(typeof(AIObjectiveCombat), "aimTimer")?.SetValue(__instance, 1.0f);

            // --- 关键修改 2: 强制执行移动逻辑 ---
            //已经自动创建
            /*
            if (!__instance.subObjectives.Any(so => so is AIObjectiveGoTo))
            {
                // 创建前往敌人的子目标
                var gotoObj = new AIObjectiveGoTo(target, character, __instance.objectiveManager, repeat: false);
                __instance.AddSubObjective(gotoObj);

                // --- 关键修改 3: 强制重置当前目标状态 ---
                // 这会迫使 AIObjectiveManager 重新排序并优先处理新的子目标
                __instance.objectiveManager.SortObjectives();
            }*/
        }
    }
}