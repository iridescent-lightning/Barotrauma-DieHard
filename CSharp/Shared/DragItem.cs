using System.Runtime.CompilerServices;
using Barotrauma;
using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Linq;
using System.Collections.Generic;
using System;

namespace BarotraumaDieHard
{
    public static class DragItemManager
    {
        // 用于记录哪个角色正在拖拽哪个物品
        public static ConditionalWeakTable<Character, Item> DraggedItems = new ConditionalWeakTable<Character, Item>();

        // 用于强引用备份物品原本的物理碰撞掩码
        private static Dictionary<Item, FarseerPhysics.Dynamics.Category> originalCollidesWith = new Dictionary<Item, FarseerPhysics.Dynamics.Category>();

        public static void OnStartDragItem(Character character, Item item)
        {
            if (character == null || item == null) return;

            if (DraggedItems.TryGetValue(character, out Item oldItem))
            {
                OnStopDragItem(character);
            }

            DraggedItems.Add(character, item);

            if (item.body?.FarseerBody != null)
            {
                var currentMask = item.body.FarseerBody.CollidesWith;
                
                if (!originalCollidesWith.ContainsKey(item))
                {
                    originalCollidesWith[item] = currentMask;
                }

                var newMask = currentMask & ~Physics.CollisionCharacter;
                item.body.FarseerBody.SetCollidesWith(newMask);
            }
        }

        public static void OnStopDragItem(Character character)
        {
            if (character == null) return;

            if (DraggedItems.TryGetValue(character, out Item item))
            {
                DraggedItems.Remove(character);

                if (item?.body?.FarseerBody != null)
                {
                    if (originalCollidesWith.TryGetValue(item, out var oldMask))
                    {
                        item.body.FarseerBody.SetCollidesWith(oldMask);
                        originalCollidesWith.Remove(item);
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(Character), "Control")]
    public class Patch_Character_Control
    {
        [HarmonyPostfix]
        public static void Postfix(Character __instance, float deltaTime, Camera cam)
        {
            if (__instance != Character.Controlled || __instance.IsDead || !__instance.CanMove) return;
            if (Screen.Selected != GameMain.GameScreen) return; 
            


            if (__instance.IsKeyDown(InputType.Grab))
            {
                if (DragItemManager.DraggedItems.TryGetValue(__instance, out Item currentItem))
                {
                    DragItemManager.OnStopDragItem(__instance);
                    return;
                }

                if (__instance.SelectedCharacter != null) return;

                Item targetItem = FindItemToDrag(__instance, cam);
                if (targetItem != null)
                {
                    DragItemManager.OnStartDragItem(__instance, targetItem);
                }
            }
        }

        private static Item FindItemToDrag(Character character, Camera cam)
        {
            if (cam == null) return null;

            Vector2 mouseWorldPos = character.CursorWorldPosition;
            Vector2 charWorldPos = character.WorldPosition;
            Item hitItem = null;

            float mouseClickRadiusPixels = 100.0f; 
            float maxDragDistancePixels = 250.0f;  

            var possibleItems = Item.ItemList
                .Where(i => i.body != null && i.body.Enabled && !i.Removed)
                .Where(i => CanDragThisItem(character, i))
                .OrderBy(i => Vector2.DistanceSquared(i.WorldPosition, mouseWorldPos));

            foreach (var item in possibleItems)
            {
                float distToMouseSq = Vector2.DistanceSquared(item.WorldPosition, mouseWorldPos);

                if (distToMouseSq <= mouseClickRadiusPixels * mouseClickRadiusPixels)
                {
                    float distToPlayerSq = Vector2.DistanceSquared(item.WorldPosition, charWorldPos);

                    if (distToPlayerSq <= maxDragDistancePixels * maxDragDistancePixels)
                    {
                        hitItem = item;
                        break;
                    }
                }
            }

            /*if (hitItem == null)
            {
                float maxBlindGrabDistancePixels = 120.0f; 
                
                hitItem = Item.ItemList
                    .Where(i => i.body != null && i.body.Enabled && !i.Removed)
                    .Where(i => CanDragThisItem(character, i))
                    .Where(i => Vector2.DistanceSquared(i.WorldPosition, charWorldPos) <= maxBlindGrabDistancePixels * maxBlindGrabDistancePixels)
                    .OrderBy(i => Vector2.DistanceSquared(i.WorldPosition, charWorldPos))
                    .FirstOrDefault();
            }*/
            
            return hitItem;
        }

        private static bool CanDragThisItem(Character character, Item item)
        {
            if (item.body == null || !item.body.Enabled) return false;
            if (item.Container != null) return false; 
            if (item.ParentInventory != null) return false; 
            if (!item.HasTag("CanBeDragged".ToIdentifier())) return false;
            if (item.body.BodyType == FarseerPhysics.BodyType.Static) return false;

            var blockedBody = Submarine.CheckVisibility(character.SimPosition, item.SimPosition, ignoreSubs: true);
            if (blockedBody != null) return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(HumanoidAnimController), "UpdateAnim")]
    public class Patch_HumanoidAnimController_UpdateAnim
    {
        [HarmonyPostfix]
        public static void Postfix(HumanoidAnimController __instance, float deltaTime)
        {
            Character character = __instance.Character;
            if (character == null) return;

            if (!DragItemManager.DraggedItems.TryGetValue(character, out Item targetItem)) return;

            if (!character.CanMove || character.IsDead || 
                targetItem == null || targetItem.body == null || !targetItem.body.Enabled || targetItem.Removed ||
                targetItem.Container != null || targetItem.ParentInventory != null)
            {
                DragItemManager.OnStopDragItem(character);
                return;
            }

            Limb leftHand = __instance.GetLimb(LimbType.LeftHand);
            Limb rightHand = __instance.GetLimb(LimbType.RightHand);
            Limb torso = __instance.GetLimb(LimbType.Torso);
            if (leftHand == null || rightHand == null || torso == null) return;

            // 1. 视线与障碍物检查
            Vector2 sourceSimPos = rightHand.SimPosition;
            Vector2 targetSimPos = targetItem.body.SimPosition;

            var blockedBody = Submarine.CheckVisibility(sourceSimPos, targetSimPos, ignoreSubs: true);
            if (blockedBody != null)
            {
                DragItemManager.OnStopDragItem(character);
                return;
            }

            // 2. 距离过远自动断开
            float dist = Vector2.Distance(character.SimPosition, targetItem.SimPosition);
            if (dist > 1.6f)
            {
                DragItemManager.OnStopDragItem(character);
                return;
            }

            // 3. 速度惩罚
            float massRatio = __instance.Mass / targetItem.body.Mass;
            __instance.TargetMovement *= MathHelper.Clamp(massRatio, 0.4f, 1.0f);

            // 4. 接管手部物理动画
            rightHand.Disabled = true;
            if (!__instance.InWater)
            {
                leftHand.Disabled = true; 
            }

            // 5. 计算手部应该被锁定的理想仿真坐标位置
            Vector2 shoulderPos = torso.SimPosition; 
            Vector2 dirToItem = targetSimPos - shoulderPos;
            if (dirToItem.LengthSquared() > 0.001f)
            {
                dirToItem.Normalize();
            }
            
            float armLength = 0.7f; 
            Vector2 idealHandPos = shoulderPos + dirToItem * Math.Min(dist, armLength);

            rightHand.PullJointEnabled = true;
            rightHand.PullJointMaxForce = 10000.0f; 
            rightHand.PullJointWorldAnchorB = idealHandPos; 

            if (!__instance.InWater && leftHand != null)
            {
                leftHand.PullJointEnabled = true;
                leftHand.PullJointMaxForce = 10000.0f;
                leftHand.PullJointWorldAnchorB = idealHandPos;
            }

            // 6. 物理拉扯补正（吸附手部）
            Vector2 holdTargetPos = rightHand.SimPosition; 
            Vector2 posError = holdTargetPos - targetSimPos; 

            float pullStrength = 150.0f; 
            targetItem.body.ApplyForce(posError * pullStrength * targetItem.body.Mass);

            // 动量注入（消除延迟与下坠）
            if (posError.LengthSquared() > 0.01f)
            {
                Vector2 velocityCorrection = (posError / deltaTime) * 0.4f; 
                targetItem.body.LinearVelocity = character.AnimController.Collider.LinearVelocity + velocityCorrection;
            }
            else
            {
                targetItem.body.LinearVelocity = character.AnimController.Collider.LinearVelocity;
            }

            // ==================== 【新增功能：右键控制任意旋转】 ====================
            
            // 检查玩家当前是否按住了瞄准（右键键位映射：InputType.Aim）
            if (character.IsKeyDown(InputType.Aim))
            
            {
                targetItem.body.ApplyTorque(10f * targetItem.body.Mass);
            }
            else
            {
                // 如果没有按住右键，继续保持上一版稳定的锁死逻辑：强力压制物品的角速度，防止在大地上拖行时打滚翻倒
                targetItem.body.AngularVelocity *= 0.5f;
            }
        }
    }
}