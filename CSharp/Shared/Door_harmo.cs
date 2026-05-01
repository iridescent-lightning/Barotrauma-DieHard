using Barotrauma.Networking;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using FarseerPhysics.Dynamics;
using Barotrauma;
#if CLIENT
using Barotrauma.Lights;
#endif
using Barotrauma.Extensions;
using System.Runtime.CompilerServices;


using HarmonyLib;
using Barotrauma.Items.Components;

namespace BarotraumaDieHard
{

    public class DoorData
    {
        public bool IsHalfOpenTarget; // 标记该门是否处于半开目标状态
    }

    [HarmonyPatch(typeof(Door))]
    partial class DoorPatch
    {


        // 使用 ConditionalWeakTable 自动管理内存，当门被销毁时，对应的数据也会释放
        private static ConditionalWeakTable<Door, DoorData> doorStateTable = new ConditionalWeakTable<Door, DoorData>();

        [HarmonyPatch("Select")]
        [HarmonyPrefix]
        // 交互逻辑：通过按下不同的按键（如 Shift+点击）或循环状态来实现切换
        public static bool Prefix_Select(Character character, Door __instance, ref bool __result)
        {
            if (__instance.IsBroken) return true;

            // 检查玩家是否按住特定按键（例如“运行”键/Shift）来触发半开
            bool shiftDown = character.IsKeyDown(InputType.Crouch);
            if (character.IsKeyDown(InputType.Crouch))
            {

            var data = doorStateTable.GetOrCreateValue(__instance);
                data.IsHalfOpenTarget = !data.IsHalfOpenTarget; // 切换半开状态

               

                __result = true;
                return true; 
            }

            // 如果是普通点击，清除半开标记
            if (doorStateTable.TryGetValue(__instance, out var d))
            {
                d.IsHalfOpenTarget = false;
            }
            //Kind of annonying
/*#if CLIENT   
                 
                        BarotraumaDieHard.CustomHintManager.DisplayHint("half_opened_door_turtorial".ToIdentifier());
#endif*/

            return true;
        }

        
        /*public static void Update(float deltaTime, Camera cam, Door __instance)
        {
            //DebugConsole.NewMessage(__instance.stuck.ToString());
            if (__instance.LinkedGap != null)
            {
                // Calculate the door's condition as a percentage
                float conditionPercentage = __instance.item.Condition / __instance.item.MaxCondition;

                // If the door has received more than 50% damage
                if (conditionPercentage < 0.5f)
                {
                    // Calculate gap openness
                    // The more damaged and stuck the door is, the more open the gap should be
                    float gapOpenness = 1.0f - (conditionPercentage * 2.0f);
                    gapOpenness = MathHelper.Clamp(gapOpenness - (__instance.stuck / 100.0f), 0.0f, 1.0f);

                    // Set the openness of the gap
                    __instance.LinkedGap.Open = gapOpenness;
                }
            }
        } */
        [HarmonyPatch("Update")]
        [HarmonyPrefix]
        public static bool Prefix_Update(float deltaTime, Camera cam, Door __instance)
        {
            // 1. 无论是否在半开逻辑中，我们都需要知道原始位置
            Vector2 originSimPos = ConvertUnits.ToSimUnits(new Vector2(__instance.Item.Rect.Center.X, __instance.Item.Rect.Y - __instance.Item.Rect.Height / 2f));

            // 检查标记
            if (doorStateTable.TryGetValue(__instance, out var data) && data.IsHalfOpenTarget && !__instance.IsBroken)
            {
                // --- 自动移动逻辑 ---
                float targetState = 0.5f;
                float diff = targetState - __instance.OpenState;
                if (Math.Abs(diff) > 0.01f)
                {
                    float moveAmount = (diff > 0 ? __instance.OpeningSpeed : -__instance.ClosingSpeed) * deltaTime;
                    __instance.OpenState += moveAmount;
                }
                else
                {
                    __instance.OpenState = targetState;
                }

                // 同步缝隙
                if (__instance.LinkedGap != null) { __instance.LinkedGap.Open = __instance.OpenState; }
                
                // --- 物理碰撞体逻辑 ---
                if (__instance.Body != null)
                {
                    __instance.Body.Enabled = true;
                    Vector2 targetSimPos = originSimPos;

                    // 只有当 OpenState 足够大时才偏移
                    if (__instance.OpenState > 0.2f)
                    {
                        if (__instance.IsHorizontal) 
                        {
                            float shiftX = ConvertUnits.ToSimUnits(__instance.Item.Rect.Width * __instance.OpenState * 1.05f);
                            targetSimPos = new Vector2(originSimPos.X - shiftX, originSimPos.Y);
                        }
                        else 
                        {
                            float shiftY = ConvertUnits.ToSimUnits(__instance.Item.Rect.Height * __instance.OpenState * 1.25f);
                            targetSimPos = new Vector2(originSimPos.X, originSimPos.Y + shiftY);
                        }
                    }
                    
                    // 关键：在这里应用变换（即便偏移是0，也能保证它回到原位）
                    __instance.Body.SetTransform(targetSimPos, 0f);
                }

                __instance.item.SendSignal("3", "state_out");
                return false; 
            }
            else
            {
                // 关键补丁：如果半开标记消失了（门恢复正常），必须把碰撞体坐标拉回来
                // 否则碰撞体由于之前 SetTransform 过，会一直悬在半空
                if (__instance.Body != null)
                {
                    // 检查当前位置是否偏离了原始位置，如果偏离了，重置它
                    if (Vector2.DistanceSquared(__instance.Body.SimPosition, originSimPos) > 0.001f)
                    {
                        __instance.Body.SetTransform(originSimPos, 0f);
                    }
                }
            }

            return true; 
        }
    }
}
