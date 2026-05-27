/*楼梯异常减速

using System;
using HarmonyLib;
using Barotrauma;
using FarseerPhysics; // 🌟 修复一：确保引入 ConvertUnits 所在的命名空间
using FarseerPhysics.Dynamics;
using Microsoft.Xna.Framework;

namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(Ragdoll), "GetFloorY")]
    public static class FixGetFloorYCrashPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Ragdoll __instance, Vector2 simPosition, bool ignoreStairs, ref float __result,
            ref bool ___onGround, ref Structure ___Stairs, ref Fixture ___floorFixture, ref Vector2 ___floorNormal, ref float ___standOnFloorY)
        {
            ___onGround = false;
            ___floorFixture = null;
            Vector2 rayStart = simPosition;
            
            // 获取私有属性/字段
            float height = __instance.ColliderHeightFromFloor;
            if (__instance.HeadPosition.HasValue && MathUtils.IsValid(__instance.HeadPosition.Value)) { height = Math.Max(height, __instance.HeadPosition.Value); }
            if (__instance.TorsoPosition.HasValue && MathUtils.IsValid(__instance.TorsoPosition.Value)) { height = Math.Max(height, __instance.TorsoPosition.Value); }

            Vector2 rayEnd = rayStart - new Vector2(0.0f, height * 2f);
            Vector2 colliderBottomDisplay = ConvertUnits.ToDisplayUnits(__instance.GetColliderBottom());

            Fixture standOnFloorFixture = null;
            float standOnFloorFraction = 1f;
            float closestFraction = 1f;
            Vector2 localFloorNormal = ___floorNormal;

            // 🌟 修复二：用局部变量倒手，避开 ref 参数在匿名函数里使用的限制
            Structure localStairs = ___Stairs; 

            GameMain.World.RayCast((fixture, point, normal, fraction) =>
            {
                switch (fixture.CollisionCategories)
                {
                    case Physics.CollisionStairs:
                        if (__instance.InWater && __instance.TargetMovement.Y < 0.5f) { return -1; }

                        if (__instance.Character.SelectedBy == null && fraction < standOnFloorFraction)
                        {
                            if (fixture.Body.UserData is Structure structure)
                            {
                                // 🌟 内部全部使用本地变量 localStairs
                                if (colliderBottomDisplay.Y >= structure.Rect.Y - structure.Rect.Height + 30 || __instance.TargetMovement.Y > 0.5f || localStairs != null)
                                {
                                    standOnFloorFraction = fraction;
                                    standOnFloorFixture = fixture;
                                }
                            }
                        }

                        if (ignoreStairs) { return -1; }
                        break;

                    case Physics.CollisionPlatform:
                        float platformRectY = 0f;
                        bool isValidPlatform = false;

                        if (fixture.Body.UserData is Structure platformStructure)
                        {
                            platformRectY = platformStructure.Rect.Y;
                            isValidPlatform = true;
                        }
                        else if (fixture.Body.UserData is Item platformItem)
                        {
                            platformRectY = platformItem.Rect.Y;
                            isValidPlatform = true;
                        }
                        else if (fixture.Body.UserData is PhysicsBody pb && pb.FarseerBody?.UserData is Item itemPart)
                        {
                            platformRectY = itemPart.Rect.Y;
                            isValidPlatform = true;
                        }

                        if (!isValidPlatform) return -1;

                        if (!__instance.IgnorePlatforms && fraction < standOnFloorFraction)
                        {
                            // 🌟 内部全部使用本地变量 localStairs
                            if (colliderBottomDisplay.Y >= platformRectY - 16 || (__instance.TargetMovement.Y > 0.0f && localStairs == null))
                            {
                                standOnFloorFraction = fraction;
                                standOnFloorFixture = fixture;
                            }
                        }

                        // 🌟 内部全部使用本地变量 localStairs
                        if (colliderBottomDisplay.Y < platformRectY - 16 && (__instance.TargetMovement.Y <= 0.0f || localStairs != null)) return -1;
                        if (__instance.IgnorePlatforms && __instance.TargetMovement.Y < -0.5f || __instance.Collider.Position.Y < ConvertUnits.ToSimUnits(platformRectY)) return -1;
                        break;

                    case Physics.CollisionWall:
                    case Physics.CollisionLevel:
                        if (!fixture.CollidesWith.HasFlag(Physics.CollisionCharacter)) { return -1; }
                        if (fixture.Body.UserData is Submarine && __instance.Character.Submarine != null) { return -1; }
                        if (fixture.IsSensor) { return -1; }
                        if (fraction < standOnFloorFraction)
                        {
                            standOnFloorFraction = fraction;
                            standOnFloorFixture = fixture;
                        }
                        break;

                    default:
                        return -1;
                }

                if (fraction < closestFraction)
                {
                    localFloorNormal = normal;
                    closestFraction = fraction;
                }

                return closestFraction;
            }, rayStart, rayEnd, Physics.CollisionStairs | Physics.CollisionPlatform | Physics.CollisionWall | Physics.CollisionLevel);

            // 🌟 修复三：RayCast 完成后，把局部变量计算出来的最终结果写回到 Harmony 的 ref 参数中
            ___Stairs = localStairs; 
            ___floorNormal = localFloorNormal;

            if (standOnFloorFixture != null && !__instance.IsHangingWithRope)
            {
                ___floorFixture = standOnFloorFixture;
                ___standOnFloorY = rayStart.Y + (rayEnd.Y - rayStart.Y) * standOnFloorFraction;

                const float Tolerance = 0.1f;
                float standHeight = __instance.Collider.Height * 0.5f + __instance.Collider.Radius + __instance.ColliderHeightFromFloor;

                if (rayStart.Y - ___standOnFloorY <= standHeight + Tolerance)
                {
                    ___onGround = true;
                    if (standOnFloorFixture.CollisionCategories == Physics.CollisionStairs)
                    {
                        ___Stairs = standOnFloorFixture.Body.UserData as Structure;
                    }
                }
            }

            if (closestFraction >= 1) 
            {
                ___floorNormal = Vector2.UnitY;
                if (__instance.CurrentHull == null)
                {
                    __result = -1000.0f;
                }
                else
                {
                    float hullBottom = __instance.CurrentHull.Rect.Y - __instance.CurrentHull.Rect.Height;
                    foreach (var gap in __instance.CurrentHull.ConnectedGaps)
                    {
                        if (!gap.IsRoomToRoom || gap.Open < 1.0f || gap.ConnectedDoor != null || gap.IsHorizontal) { continue; }
                        if (__instance.WorldPosition.X > gap.WorldRect.X && __instance.WorldPosition.X < gap.WorldRect.Right && gap.WorldPosition.Y < __instance.WorldPosition.Y)
                        {
                            var lowerHull = gap.linkedTo[0] == __instance.CurrentHull ? gap.linkedTo[1] : gap.linkedTo[0];    
                            hullBottom = Math.Min(hullBottom, lowerHull.Rect.Y - lowerHull.Rect.Height);
                        }
                    }
                    __result = ConvertUnits.ToSimUnits(hullBottom);
                }
            }
            else
            {
                __result = rayStart.Y + (rayEnd.Y - rayStart.Y) * closestFraction;
            }

            return false; // 拦截成功
        }
    }
}*/