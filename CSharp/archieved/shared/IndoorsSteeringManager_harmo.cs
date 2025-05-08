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

            harmony.Patch(originalCalculateSteeringSeek, new HarmonyMethod(PrefixCalculateSteeringSeek), null);

            
            
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

            
            try
            {
                if (_.currentPath == null)
                {
                    __result = Vector2.Zero;
                    return false;
                }
            }
            catch (Exception ex)
            {
                DebugConsole.ThrowError("Error while checking path null or unreachable: ", ex);
                __result = Vector2.Zero;
                return false;
            }

            try
            {
                if (_.currentPath.Unreachable)
                {
                    __result = Vector2.Zero;
                    return false;
                }
            }
            catch (Exception ex)
            {
                DebugConsole.ThrowError("Error while checking unreachable: ", ex);
                return false;
            }


             try
            {
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
            }
            catch (Exception ex)
            {
                DebugConsole.ThrowError("Error while handling finished path: ", ex);
                __result = Vector2.Zero;
                return false;
            }

            bool doorsChecked = false;
            try
            {
                _.checkDoorsTimer = Math.Min(_.checkDoorsTimer, _.GetDoorCheckTime());
                if (!_.character.LockHands && _.checkDoorsTimer <= 0.0f)
                {
                    _.CheckDoorsInPath();
                    doorsChecked = true;
                }
            }
            catch (Exception ex)
            {
                DebugConsole.ThrowError("Error while checking doors in path: ", ex);
            }


            try
            {
                if (_.buttonPressTimer > 0 && _.lastDoor.door != null && _.lastDoor.shouldBeOpen && !_.lastDoor.door.IsFullyOpen)
                {
                    _.Reset();
                    __result = Vector2.Zero;
                    return false;
                }
            }
            catch (Exception ex)
            {
                DebugConsole.ThrowError("Error while handling button press and door: ", ex);
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
            try
            {
                if (useLadders)
                {
                    // If character is climbing and horizontal distance is greater than vertical, stop using ladders
                    if (_.character.IsClimbing && Math.Abs(diff.X) - ConvertUnits.ToDisplayUnits(colliderSize.X) > Math.Abs(diff.Y))
                    {
                        useLadders = false;
                    }
                    // If character is not climbing, next node exists, and no ladder at next node, check next node distances
                    else if (!_.character.IsClimbing && _.currentPath.NextNode != null && nextLadder == null)
                    {
                        Vector2 diffToNextNode = _.currentPath.NextNode.WorldPosition - pos;
                        if (Math.Abs(diffToNextNode.X) > Math.Abs(diffToNextNode.Y))
                        {
                            useLadders = false;
                        }
                    }
                    // If character is diving and trying to go down, stop using ladders
                    else if (isDiving && _.steering.Y < 1)
                    {
                        useLadders = false;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugConsole.ThrowError("Error in ladder usage logic: ", ex);
                useLadders = false; // Fallback to avoid using ladders in case of error
            }




            try
{
    // Check if the character is climbing but should stop due to ladder usage logic
    if (_.character.IsClimbing && !useLadders)
    {
        if (_.currentPath.IsAtEndNode && canClimb && ladders != null)
        {
            // Prevent character from releasing the ladders when ending a path in ladders
            useLadders = true;
        }
        else
        {
            _.character.StopClimbing();
        }
    }
}
catch (Exception ex)
{
    DebugConsole.ThrowError("Error in climbing logic: ", ex);
    _.character.StopClimbing(); // Fallback to stop climbing in case of error
}

try
{
    // Handle ladder interaction when character is not already interacting with the correct ladder
    if (useLadders && _.character.SelectedSecondaryItem != ladders.Item)
    {
        if (_.character.CanInteractWith(ladders.Item))
        {
            ladders.Item.TryInteract(_.character, forceSelectKey: true);
        }
        else
        {
            // Try interacting with the previous ladder if it's adjacent to the current one
            var previousLadders = _.currentPath.PrevNode?.Ladders;
            if (previousLadders != null && previousLadders != ladders && _.character.SelectedSecondaryItem != previousLadders.Item &&
                _.character.CanInteractWith(previousLadders.Item) && Math.Abs(previousLadders.Item.WorldPosition.X - ladders.Item.WorldPosition.X) < 5)
            {
                previousLadders.Item.TryInteract(_.character, forceSelectKey: true);
            }
        }
    }
}
catch (Exception ex)
{
    DebugConsole.ThrowError("Error in ladder interaction logic: ", ex);
}



            try
{
    if (_.character.IsClimbing && useLadders)
    {
        try
        {
            if (currentLadder == null && nextLadder != null && _.character.SelectedSecondaryItem == nextLadder.Item)
            {
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
                    diff.X = 0.0f; // Stop horizontal movement while on ladders
                }

                if (Math.Abs(heightDiff) < colliderHeight * 1.25f)
                {
                    if (nextLadder != null && !nextLadderSameAsCurrent)
                    {
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
                        float colliderBottom = _.character.AnimController.Collider.SimPosition.Y;
                        float floorY = _.character.AnimController.FloorY;
                        isAboveFloor = colliderBottom > floorY;
                    }
                    else
                    {
                        float heightFromFloor = _.character.AnimController.GetHeightFromFloor();
                        isAboveFloor = heightFromFloor > -0.1f;
                    }

                    if (isAboveFloor)
                    {
                        if (Math.Abs(diff.Y) < distanceMargin)
                        {
                            _.NextNode(!doorsChecked);
                        }
                        else if (!_.currentPath.IsAtEndNode && 
                                (nextLadder == null || (currentLadder != null && 
                                Math.Abs(currentLadder.Item.WorldPosition.X - nextLadder.Item.WorldPosition.X) > distanceMargin)))
                        {
                            _.character.StopClimbing();
                        }
                    }
                }
                else if (currentLadder != null && _.currentPath.NextNode != null)
                {
                    if (Math.Sign(_.currentPath.CurrentNode.WorldPosition.Y - _.character.WorldPosition.Y) != 
                        Math.Sign(_.currentPath.NextNode.WorldPosition.Y - _.character.WorldPosition.Y))
                    {
                        _.NextNode(!doorsChecked);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            DebugConsole.ThrowError("Error in ladder climbing logic: ", ex);
            _.character.StopClimbing(); // Fallback to stop climbing
        }
    }
    else if (_.character.AnimController.InWater)
    {
        try
        {
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
        catch (Exception ex)
        {
            DebugConsole.ThrowError("Error in swimming logic: ", ex);
        }
    }
    else
    {
        try
{
    Vector2 colliderBottom = _.character.AnimController.GetColliderBottom();
    Vector2 velocity = collider.LinearVelocity;
    float minHeight = 1.6125001f;
    float minWidth = 0.3225f;
    float characterHeight = Math.Max(colliderSize.Y + _.character.AnimController.ColliderHeightFromFloor, minHeight);
    float horizontalDistance = Math.Abs(collider.SimPosition.X - _.currentPath.CurrentNode.SimPosition.X);
    bool isTargetTooHigh = _.currentPath.CurrentNode.SimPosition.Y > colliderBottom.Y + characterHeight;
    bool isTargetTooLow = _.currentPath.CurrentNode.SimPosition.Y < colliderBottom.Y;
    var door = _.currentPath.CurrentNode.ConnectedDoor;
    float margin = MathHelper.Lerp(1, 10, MathHelper.Clamp(Math.Abs(velocity.X) / 5, 0, 1));
    float colliderHeight = collider.Height / 2 + collider.Radius;

    try
    {
        if (_.currentPath.CurrentNode.Stairs == null)
        {
            float heightDiff = _.currentPath.CurrentNode.SimPosition.Y - collider.SimPosition.Y;
            if (heightDiff < colliderHeight)
            {
                diff.Y = 0.0f;
            }
        }
    }
    catch (Exception ex)
    {
        DebugConsole.ThrowError("Error in stairs check: ", ex);
    }

    try
    {
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
    catch (Exception ex)
    {
        DebugConsole.ThrowError("Error in next node stairs check: ", ex);
    }

    try
    {
        

        float targetDistance = Math.Max(colliderSize.X / 2 * margin, minWidth / 2);
        if (_.character == null || _.CurrentPath.CurrentNode == null)
        {
            //_.Reset();
            __result = Vector2.Zero;
            DebugConsole.NewMessage("no character");
                    return false;
        }
        if (horizontalDistance !=null && horizontalDistance < targetDistance && !isTargetTooHigh && !isTargetTooLow)
        {
            if (door is not { CanBeTraversed: false } && (currentLadder == null || nextLadder == null))
            {
                _.NextNode(!doorsChecked);
            }
            
        }
        else
        {
            DebugConsole.NewMessage("No distance");
        }
        
    }
    catch (Exception ex)
    {
        DebugConsole.ThrowError("Error in target distance check: ", ex);// One bug here
    }
}
catch (Exception ex)
{
    DebugConsole.ThrowError("Error in walking logic: ", ex);
}

    }
}
catch (Exception ex)
{
    DebugConsole.ThrowError("Error in movement logic: ", ex);
}

            try
{
    if (_.currentPath.CurrentNode == null)
    {
        __result = Vector2.Zero;
        return false;
    }
    
    __result = ConvertUnits.ToSimUnits(diff);
    return false;
}
catch (Exception ex)
{
    DebugConsole.ThrowError("Error in final result calculation: ", ex);
    __result = Vector2.Zero; // Fallback value in case of an exception. One bug here.
    return false;
}

        }



        public static bool CalculateSteeringSeekPrefix(Vector2 target, float weight, float minGapSize, Func<PathNode, bool> startNodeFilter, Func<PathNode, bool> endNodeFilter, Func<PathNode, bool> nodeFilter, bool checkVisibility, IndoorsSteeringManager __instance, ref Vector2 __result)
        {
            IndoorsSteeringManager _ = __instance;

            bool needsNewPath = _.currentPath == null || _.currentPath.Unreachable || _.currentPath.Finished || _.currentPath.CurrentNode == null;
            if (!needsNewPath && _.character.Submarine != null && _.character.Params.PathFinderPriority > 0.5f)
            {
                // If the target has moved, we need a new path.
                // Different subs are already taken into account before setting the target.
                // Triggers when either the target or we have changed subs, but only once (until the new path has been accepted).
                Vector2 targetDiff = target - _.currentTargetPos;
                if (targetDiff.LengthSquared() > 1)
                {
                    needsNewPath = true;
                }
            }
            //find a new path if one hasn't been found yet or the target is different from the current target
            if (needsNewPath || _.findPathTimer < -1.0f)
            {
                _.IsPathDirty = true;
                if (!needsNewPath && _.currentPath?.CurrentNode is WayPoint wp)
                {
                    if (_.character.Submarine != null && wp.Ladders == null && wp.ConnectedDoor == null && Math.Abs(_.character.AnimController.TargetMovement.Combine()) <= 0)
                    {
                        // Not moving -> need a new path.
                        needsNewPath = true;
                    }
                    if (_.character.Submarine == null && wp.CurrentHull != null)
                    {
                        // Current node inside, while we are outside
                        // -> Check that the current node is not too far (can happen e.g. if someone controls the character in the meanwhile)
                        float maxDist = 200;
                        if (Vector2.DistanceSquared(_.character.WorldPosition, wp.WorldPosition) > maxDist * maxDist)
                        {
                            needsNewPath = true;
                        }
                    }
                }
                if (_.findPathTimer < 0)
                {
                    SkipCurrentPathNodes();
                    _.currentTargetPos = target;
                    Vector2 currentPos = _.host.SimPosition;
                    _.pathFinder.InsideSubmarine = _.character.Submarine != null && !_.character.Submarine.Info.IsRuin;
                    _.pathFinder.ApplyPenaltyToOutsideNodes = _.character.Submarine != null && !_.character.IsProtectedFromPressure;
                    var newPath = _.pathFinder.FindPath(currentPos, target, _.character.Submarine, "(Character: " + _.character.Name + ")", minGapSize, startNodeFilter, endNodeFilter, nodeFilter, checkVisibility: checkVisibility);
                    bool useNewPath = needsNewPath;
                    if (!useNewPath && _.currentPath?.CurrentNode != null && newPath.Nodes.Any() && !newPath.Unreachable)
                    {
                        // Check if the new path is the same as the old, in which case we just ignore it and continue using the old path (or the progress would reset).
                        if (IsIdenticalPath())
                        {
                            useNewPath = false;
                        }
                        else if (!_.character.IsClimbing)
                        {
                            // Use the new path if it has significantly lower cost (don't change the path if it has marginally smaller cost. This reduces navigating backwards due to new path that is calculated from the node just behind us).
                            float t = (float)_.currentPath.CurrentIndex / (_.currentPath.Nodes.Count - 1);
                            useNewPath = newPath.Cost < _.currentPath.Cost * MathHelper.Lerp(0.95f, 0, t);
                            if (!useNewPath && _.character.Submarine != null)
                            {
                                // It's possible that the current path was calculated from a start point that is no longer valid.
                                // Therefore, let's accept also paths with a greater cost than the current, if the current node is much farther than the new start node.
                                // This is a special case for cases e.g. where the character falls and thus needs a new path.
                                useNewPath = Vector2.DistanceSquared(_.character.WorldPosition, _.currentPath.CurrentNode.WorldPosition) > Math.Pow(Vector2.Distance(_.character.WorldPosition, newPath.Nodes.First().WorldPosition) * 3, 2);
                            }
                        }
                        if (!useNewPath && !_.character.CanSeeTarget(_.currentPath.CurrentNode))
                        {
                            // If we are set to disregard the new path, ensure that we can actually see the current node of the old path,
                            // because it's possible that there's e.g. a closed door between us and the current node,
                            // and in that case we'd want to use the new path instead of the old.
                            // There's visibility checks in the pathfinder calls, so the new path should always be ok.
                            useNewPath = true;
                        }

                        bool IsIdenticalPath()
                        {
                            int nodeCount = newPath.Nodes.Count;
                            if (nodeCount == _.currentPath.Nodes.Count)
                            {
                                for (int i = 0; i < nodeCount - 1; i++)
                                {
                                    if (newPath.Nodes[i] != _.currentPath.Nodes[i])
                                    {
                                        return false;
                                    }
                                }
                                return true;
                            }
                            return false;
                        }
                    }
                    if (useNewPath)
                    {
                        if (_.currentPath != null)
                        {
                            _.CheckDoorsInPath();
                        }
                        _.currentPath = newPath;
                    }
                    float priority = MathHelper.Lerp(3, 1, _.character.Params.PathFinderPriority);
                    _.findPathTimer = priority * Rand.Range(1.0f, 1.2f);
                    _.IsPathDirty = false;

                    void SkipCurrentPathNodes()
                    {
                        if (!_.character.AnimController.InWater || _.character.Submarine != null) { return; }
                        if (_.CurrentPath == null || _.CurrentPath.Unreachable || _.CurrentPath.Finished) { return; }
                        if (_.CurrentPath.CurrentIndex < 0 || _.CurrentPath.CurrentIndex >= _.CurrentPath.Nodes.Count - 1) { return; }
                        var lastNode = _.CurrentPath.Nodes.Last();
                        Submarine targetSub = lastNode.Submarine;
                        if (targetSub != null)
                        {
                            float subSize = Math.Max(targetSub.Borders.Size.X, targetSub.Borders.Size.Y) / 2;
                            float margin = 500;
                            if (Vector2.DistanceSquared(_.character.WorldPosition, targetSub.WorldPosition) < MathUtils.Pow2(subSize + margin))
                            {
                                // Don't skip nodes when close to the target submarine.
                                return;
                            }
                        }
                        // Check if we could skip ahead to NextNode when the character is swimming and using waypoints outside.
                        // Do this to optimize the old path before creating and evaluating a new path.
                        // In general, this is to avoid behavior where:
                        // a) the character goes back to first reach CurrentNode when the second node would be closer; or
                        // b) the character moves along the path when they could cut through open space to reduce the total distance.
                        float pathDistance = Vector2.Distance(_.character.WorldPosition, _.CurrentPath.CurrentNode.WorldPosition);
                        pathDistance += _.CurrentPath.GetLength(startIndex: _.CurrentPath.CurrentIndex);
                        for (int i = _.CurrentPath.Nodes.Count - 1; i > _.CurrentPath.CurrentIndex + 1; i--)
                        {
                            var waypoint = _.CurrentPath.Nodes[i];
                            float directDistance = Vector2.DistanceSquared(_.character.WorldPosition, waypoint.WorldPosition);
                            if (directDistance > MathUtils.Pow2(pathDistance) || !_.character.CanSeeTarget(waypoint))
                            {
                                pathDistance -= _.CurrentPath.GetLength(startIndex: i - 1, endIndex: i);
                                continue;
                            }
                            _.CurrentPath.SkipToNode(i);
                            break;
                        }
                    }
                }
            }

            Vector2 diff = _.DiffToCurrentNode();
            if (diff == Vector2.Zero) { __result = Vector2.Zero; return false;}

            __result = Vector2.Normalize(diff) * weight;
            return false;
        }

    }
}