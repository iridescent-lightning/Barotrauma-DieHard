using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using System;
using System.Linq;
using FarseerPhysics;

using HarmonyLib;
using System.Reflection;
using System.Xml.Linq;

using Barotrauma;


namespace BarotraumaDieHard
{
    class IndoorsSteeringManagerDieHard  : IAssemblyPlugin
    {


        public Harmony harmony;
        
        
        public void Initialize()
        {
            harmony = new Harmony("IndoorsSteeringManagerDieHard");

            var originalDiffToCurrentNode = typeof(IndoorsSteeringManager).GetMethod("DiffToCurrentNode", BindingFlags.NonPublic | BindingFlags.Instance);
            var PrefixDiffToCurrentNode = typeof(IndoorsSteeringManagerDieHard).GetMethod(nameof(DiffToCurrentNodePrefix), BindingFlags.Public | BindingFlags.Static);

            harmony.Patch(originalDiffToCurrentNode, new HarmonyMethod(PrefixDiffToCurrentNode), null);

            var originalCalculateSteeringSeek = typeof(IndoorsSteeringManager).GetMethod("CalculateSteeringSeek", BindingFlags.NonPublic | BindingFlags.Instance);
            var PrefixCalculateSteeringSeek = typeof(IndoorsSteeringManagerDieHard).GetMethod(nameof(CalculateSteeringSeekPrefix), BindingFlags.Public | BindingFlags.Static);

            harmony.Patch(originalDiffToCurrentNode, new HarmonyMethod(PrefixDiffToCurrentNode), null);

            
            
        }

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }

        // If the original method returns something, you must use reflection no matter if you want to change the value or not
        public static bool DiffToCurrentNodePrefix(IndoorsSteeringManager __instance, ref Vector2 __result)
        {
            IndoorsSteeringManager _ = __instance;

            if (_.currentPath == null || _.currentPath.Unreachable)
            {
                __result = Vector2.Zero;
                return false;
            }
            if (_.currentPath.Finished)
            {
                Vector2 hostPosition = _.host.SimPosition;
                if (_.character != null && _.character.Submarine == null && _.CurrentPath.Nodes?.Count > 0 && _.CurrentPath.Nodes.Last().Submarine != null)
                {
                    hostPosition -= _.CurrentPath.Nodes.Last().Submarine.SimPosition;
                }
                __result = _.currentTargetPos - hostPosition;
                return false;
            }

            bool doorsChecked = false;
            _.checkDoorsTimer = Math.Min(_.checkDoorsTimer, _.GetDoorCheckTime());
            if (!_.character.LockHands && _.checkDoorsTimer <= 0.0f)
            {
                _.CheckDoorsInPath();
                doorsChecked = true;
            }
            if (_.buttonPressTimer > 0 && _.lastDoor.door != null && _.lastDoor.shouldBeOpen && !_.lastDoor.door.IsFullyOpen)
            {
                // We have pressed the button and are waiting for the door to open -> Hold still until we can press the button again.
                _.Reset();
                __result = Vector2.Zero;
                return false;
            }
            Vector2 pos = _.host.WorldPosition;
            Vector2 diff = _.currentPath.CurrentNode.WorldPosition - pos;
            bool isDiving = _.character.AnimController.InWater && _.character.AnimController.HeadInWater;
            // Only humanoids can climb ladders
            bool canClimb = _.character.AnimController is HumanoidAnimController && !_.character.LockHands;
            Ladder currentLadder = _.GetCurrentLadder();
            Ladder nextLadder = _.GetNextLadder();
            var ladders = currentLadder ?? nextLadder;
            bool useLadders = canClimb && ladders != null;
            var collider = _.character.AnimController.Collider;
            Vector2 colliderSize = collider.GetSize();
            if (useLadders)
            {
                if (_.character.IsClimbing && Math.Abs(diff.X) - ConvertUnits.ToDisplayUnits(colliderSize.X) > Math.Abs(diff.Y))
                {
                    // If the current node is horizontally farther from us than vertically, we don't want to keep climbing the ladders.
                    useLadders = false;
                }
                else if (!_.character.IsClimbing && _.currentPath.NextNode != null && nextLadder == null)
                {
                    Vector2 diffToNextNode = _.currentPath.NextNode.WorldPosition - pos;
                    if (Math.Abs(diffToNextNode.X) > Math.Abs(diffToNextNode.Y))
                    {
                        // If the next node is horizontally farther from us than vertically, we don't want to start climbing.
                        useLadders = false;
                    }
                }
                else if (isDiving && _.steering.Y < 1)
                {
                    // When diving, only use ladders to get upwards (towards the surface), otherwise we can just ignore them.
                    useLadders = false;
                }
            }
            if (_.character.IsClimbing && !useLadders)
            {
                if (_.currentPath.IsAtEndNode && canClimb && ladders != null)
                {
                    // Don't release the ladders when ending a path in ladders.
                    useLadders = true;
                }
                else
                {
                    _.character.StopClimbing();
                }
            }
            if (useLadders && _.character.SelectedSecondaryItem != ladders.Item)
            {
                if (_.character.CanInteractWith(ladders.Item))
                {
                    ladders.Item.TryInteract(_.character, forceSelectKey: true);
                }
                else
                {
                    // Cannot interact with the current (or next) ladder,
                    // Try to select the previous ladder, unless it's already selected, unless the previous ladder is not adjacent to the current ladder.
                    // The intention of this code is to prevent the bots from dropping from the "double ladders".
                    var previousLadders = _.currentPath.PrevNode?.Ladders;
                    if (previousLadders != null && previousLadders != ladders && _.character.SelectedSecondaryItem != previousLadders.Item &&
                        _.character.CanInteractWith(previousLadders.Item) && Math.Abs(previousLadders.Item.WorldPosition.X - ladders.Item.WorldPosition.X) < 5)
                    {
                        previousLadders.Item.TryInteract(_.character, forceSelectKey: true);
                    }
                }
            }
            if (_.character.IsClimbing && useLadders)
            {
                if (currentLadder == null && nextLadder != null && _.character.SelectedSecondaryItem == nextLadder.Item)
                {
                    // Climbing a ladder but the path is still on the node next to the ladder -> Skip the node.
                    _.NextNode(!doorsChecked);
                }
                else
                {
                    bool nextLadderSameAsCurrent = currentLadder == nextLadder;
                    float colliderHeight = collider.Height / 2 + collider.Radius;
                    float heightDiff = _.currentPath.CurrentNode.SimPosition.Y - collider.SimPosition.Y;
                    float distanceMargin = ConvertUnits.ToDisplayUnits(colliderSize.X);
                    if (currentLadder != null && nextLadder != null)
                    {
                        //climbing ladders -> don't move horizontally
                        diff.X = 0.0f;
                    }
                    if (Math.Abs(heightDiff) < colliderHeight * 1.25f)
                    {
                        if (nextLadder != null && !nextLadderSameAsCurrent)
                        {
                            // Try to change the ladder (hatches between two submarines)
                            if (_.character.SelectedSecondaryItem != nextLadder.Item && _.character.CanInteractWith(nextLadder.Item))
                            {
                                if (nextLadder.Item.TryInteract(_.character, forceSelectKey: true))
                                {
                                    _.NextNode(!doorsChecked);
                                }
                            }
                        }
                        bool isAboveFloor;
                        if (diff.Y < 0)
                        {
                            // When climbing down, let's use the collider bottom to prevent getting stuck at the bottom of the ladders.
                            float colliderBottom = _.character.AnimController.Collider.SimPosition.Y;
                            float floorY = _.character.AnimController.FloorY;
                            isAboveFloor = colliderBottom > floorY;
                        }
                        else
                        {
                            // When climbing up, let's use the lowest collider (feet).
                            // We need some margin, because if a hatch has closed, it's possible that the height from floor is slightly negative,
                            // when a foot is still below the platform.
                            float heightFromFloor = _.character.AnimController.GetHeightFromFloor();
                            isAboveFloor = heightFromFloor > -0.1f;
                        }
                        if (isAboveFloor)
                        {
                            if (Math.Abs(diff.Y) < distanceMargin)
                            {
                                _.NextNode(!doorsChecked);
                            }
                            else if (!_.currentPath.IsAtEndNode && (nextLadder == null || (currentLadder != null && Math.Abs(currentLadder.Item.WorldPosition.X - nextLadder.Item.WorldPosition.X) > distanceMargin)))
                            {
                                // Can't skip the node -> Release the ladders, because the next node is not on a ladder or it's horizontally too far.
                                _.character.StopClimbing();
                            }
                        }
                    }
                    else if (currentLadder != null && _.currentPath.NextNode != null)
                    {
                        if (Math.Sign(_.currentPath.CurrentNode.WorldPosition.Y - _.character.WorldPosition.Y) != Math.Sign(_.currentPath.NextNode.WorldPosition.Y - _.character.WorldPosition.Y))
                        {
                            //if the current node is below the character and the next one is above (or vice versa)
                            //and both are on ladders, we can skip directly to the next one
                            //e.g. no point in going down to reach the starting point of a path when we could go directly to the one above
                            _.NextNode(!doorsChecked);
                        }
                    }
                }
            }
            else if (_.character.AnimController.InWater)
            {
                // Swimming
                var door = _.currentPath.CurrentNode.ConnectedDoor;
                if (door == null || door.CanBeTraversed)
                {
                    float margin = MathHelper.Lerp(1, 5, MathHelper.Clamp(collider.LinearVelocity.Length() / 10, 0, 1));
                    float targetDistance = Math.Max(Math.Max(colliderSize.X, colliderSize.Y) / 2 * margin, 0.5f);
                    float horizontalDistance = Math.Abs(_.character.WorldPosition.X - _.currentPath.CurrentNode.WorldPosition.X);
                    float verticalDistance = Math.Abs(_.character.WorldPosition.Y - _.currentPath.CurrentNode.WorldPosition.Y);
                    if (_.character.CurrentHull != _.currentPath.CurrentNode.CurrentHull)
                    {
                        verticalDistance *= 2;
                    }
                    float distance = horizontalDistance + verticalDistance;
                    if (ConvertUnits.ToSimUnits(distance) < targetDistance)
                    {
                        _.NextNode(!doorsChecked);
                    }
                }
            }
            else
            {
                // Walking horizontally
                Vector2 colliderBottom = _.character.AnimController.GetColliderBottom();
                Vector2 velocity = collider.LinearVelocity;
                // If the character is very short, it would fail to use the waypoint nodes because they are always too high.
                // If the character is very thin, it would often fail to reach the waypoints, because the horizontal distance is too small.
                // Both values are based on the human size. So basically anything smaller than humans are considered as equal in size.
                float minHeight = 1.6125001f;
                float minWidth = 0.3225f;
                // Cannot use the head position, because not all characters have head or it can be below the total height of the character
                float characterHeight = Math.Max(colliderSize.Y + _.character.AnimController.ColliderHeightFromFloor, minHeight);
                float horizontalDistance = Math.Abs(collider.SimPosition.X - _.currentPath.CurrentNode.SimPosition.X);
                bool isTargetTooHigh = _.currentPath.CurrentNode.SimPosition.Y > colliderBottom.Y + characterHeight;
                bool isTargetTooLow = _.currentPath.CurrentNode.SimPosition.Y < colliderBottom.Y;
                var door = _.currentPath.CurrentNode.ConnectedDoor;
                float margin = MathHelper.Lerp(1, 10, MathHelper.Clamp(Math.Abs(velocity.X) / 5, 0, 1));
                float colliderHeight = collider.Height / 2 + collider.Radius;
                if (_.currentPath.CurrentNode.Stairs == null)
                {
                    float heightDiff = _.currentPath.CurrentNode.SimPosition.Y - collider.SimPosition.Y;
                    if (heightDiff < colliderHeight)
                    {
                        // Original comment:
                        //the waypoint is between the top and bottom of the collider, no need to move vertically.
                        // Note that the waypoint can be below collider too! This might be incorrect.
                        diff.Y = 0.0f;
                    }
                }
                else
                {
                    // In stairs
                    bool isNextNodeInSameStairs = _.currentPath.NextNode?.Stairs == _.currentPath.CurrentNode.Stairs;
                    if (!isNextNodeInSameStairs)
                    {
                        margin = 1;
                        if (_.currentPath.CurrentNode.SimPosition.Y < colliderBottom.Y + _.character.AnimController.ColliderHeightFromFloor * 0.25f)
                        {
                            isTargetTooLow = true;
                        }
                    }
                }
                float targetDistance = Math.Max(colliderSize.X / 2 * margin, minWidth / 2);
                if (horizontalDistance < targetDistance && !isTargetTooHigh && !isTargetTooLow)
                {
                    if (door is not { CanBeTraversed: false } && (currentLadder == null || nextLadder == null))
                    {
                        _.NextNode(!doorsChecked);
                    }
                }
            }
            if (_.currentPath.CurrentNode == null)
            {
                __result = Vector2.Zero;
                return false;
            }
            __result = ConvertUnits.ToSimUnits(diff);
            return false;
        }



        public static bool CalculateSteeringSeekPrefix(Vector2 target, float weight, float minGapSize, Func<PathNode, bool> startNodeFilter, Func<PathNode, bool> endNodeFilter, Func<PathNode, bool> nodeFilter, bool checkVisibility, IndoorsSteeringManager __instance, ref Vector2 __result)
        {
            if (__instance.CurrentPath == null || __instance.CurrentPath.CurrentNode == null)
            {
                DebugConsole.NewMessage("Null Path while steering seek.");
                return false;
            }
            return true;
        }

    }
}