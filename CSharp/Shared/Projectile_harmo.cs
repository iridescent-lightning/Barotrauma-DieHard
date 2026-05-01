using Barotrauma.Networking;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Contacts;
using FarseerPhysics.Dynamics.Joints;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Voronoi2;

using Barotrauma.Items.Components;
using System.Reflection;
using Barotrauma;
using HarmonyLib;




namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(Projectile))]
    public class ProjectilePatch
    {


        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void UpdatePostfix(float deltaTime, Camera cam, Projectile __instance)
        {
            
            
            // less effective than statuseffect
        /*float range = 25f; // 对应 XML 中的 Range
        Vector2 currentPos = __instance.item.WorldPosition;

        foreach (Item nearbyItem in Item.ItemList)
        {
            // 1. 基础过滤：已移除的、或者你自己（防止炸弹炸自己）
            if (nearbyItem.Removed || nearbyItem == __instance.item) { continue; }

            // 2. 距离检查 (复刻你提供的 CheckDistance 逻辑)
            float xDiff = Math.Abs(nearbyItem.WorldPosition.X - currentPos.X);
            if (xDiff > range) { continue; }
            float yDiff = Math.Abs(nearbyItem.WorldPosition.Y - currentPos.Y);
            if (yDiff > range) { continue; }

            if (xDiff * xDiff + yDiff * yDiff < range * range)
            {
                
                // 3. 目标筛选：无 Body 且包含特定 Tag
                if (nearbyItem.body == null && nearbyItem.HasTag("damage_by_passing_bullet"))
                {
                    // 4. 执行伤害或其他逻辑
                    // 注意：deltaTime 确保伤害平滑
                    nearbyItem.Condition -= 400.0f * deltaTime;
                    DebugConsole.NewMessage($"{nearbyItem}");
                    
                    // 如果你想触发目标身上的 OnUse 效果
                    // nearbyItem.ApplyStatusEffects(ActionType.OnUse, deltaTime, user: _user);
                }
            }
        }*/

        }
    }
}
