using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Barotrauma.Networking; 
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

using HarmonyLib;
using System.Reflection;
using System.Xml.Linq;

using Barotrauma;
using BarotraumaDieHard.Items;



namespace BarotraumaDieHard.AI
{
    class AIObjectiveFindSafetyDieHard  : IAssemblyPlugin
    {
        public Harmony harmony;
        public void Initialize()
        {
            harmony = new Harmony("AIObjectiveFindSafetyDieHard");

            var originalGetPriority = typeof(AIObjectiveFindSafety).GetMethod("GetPriority", BindingFlags.NonPublic | BindingFlags.Instance);
            var prefixGetPriority = typeof(AIObjectiveFindSafetyDieHard).GetMethod(nameof(GetPriorityPrefix), BindingFlags.Public | BindingFlags.Static);

            harmony.Patch(originalGetPriority, new HarmonyMethod(prefixGetPriority), null);


            var originalAct = typeof(AIObjectiveFindSafety).GetMethod("Act", BindingFlags.NonPublic | BindingFlags.Instance);
            var prefixAct = typeof(AIObjectiveFindSafetyDieHard).GetMethod(nameof(ActPrefix), BindingFlags.Public | BindingFlags.Static);

            harmony.Patch(originalAct, new HarmonyMethod(prefixAct), null);
            
        }

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }


        private static AIObjectiveFindAndEquipRadiationSuit findAndEquipRadiationSuit;


        // No change. There used to be a reset to allow bots to execute finding radiationsuit. But I intergrated it into the Act so it is now a subobjective of the FindSafety.
        // I still keep the patch in case of future usage.
        public static bool GetPriorityPrefix(AIObjectiveFindSafety __instance, ref float __result)
        {
            AIObjectiveFindSafety _ = __instance;

            if (_.character.CurrentHull == null)
            {
                _.Priority = (
                    _.objectiveManager.HasOrder<AIObjectiveGoTo>(o => o.Priority > 0) ||
                    _.objectiveManager.HasActiveObjective<AIObjectiveRescue>() ||
                    _.objectiveManager.Objectives.Any(o => (o is AIObjectiveCombat || o is AIObjectiveReturn) && o.Priority > 0))
                    && ((!_.character.IsLowInOxygen && _.character.IsImmuneToPressure)|| HumanAIController.HasDivingSuit(_.character)) ? 0 : AIObjectiveManager.EmergencyObjectivePriority - 10;
            }
            else
            {
                bool isSuffocatingInDivingSuit = _.character.IsLowInOxygen && !_.character.AnimController.HeadInWater && HumanAIController.HasDivingSuit(_.character, requireOxygenTank: false);
                static bool IsSuffocatingWithoutDivingGear(Character c) => c.IsLowInOxygen && c.AnimController.HeadInWater && !HumanAIController.HasDivingGear(c, requireOxygenTank: true);

                if (isSuffocatingInDivingSuit || (!_.objectiveManager.HasActiveObjective<AIObjectiveFindDivingGear>() && IsSuffocatingWithoutDivingGear(_.character)))
                {
                    _.Priority = AIObjectiveManager.MaxObjectivePriority;
                }
                else if (_.NeedMoreDivingGear(_.character.CurrentHull, AIObjectiveFindDivingGear.GetMinOxygen(_.character)))
                {
                    if (_.objectiveManager.FailedToFindDivingGearForDepth &&
                        HumanAIController.HasDivingSuit(_.character, requireSuitablePressureProtection: false))
                    {
                        //we have a suit that's not suitable for the pressure,
                        //but we've failed to find a better one
                        // shit, not much we can do here, let's just allow the bot to get on with their current objective
                        _.Priority = 0;
                    }
                    else
                    {
                        _.Priority = AIObjectiveManager.MaxObjectivePriority;
                    }
                }
                else if ((_.objectiveManager.IsCurrentOrder<AIObjectiveGoTo>() || _.objectiveManager.IsCurrentOrder<AIObjectiveReturn>()) &&
                         _.character.Submarine != null && !_.character.IsOnFriendlyTeam(_.character.Submarine.TeamID))
                {
                    // Ordered to follow, hold position, or return back to main sub inside a hostile sub
                    // -> ignore find safety unless we need to find a diving gear
                    _.Priority = 0;
                }
                else if (_.objectiveManager.Objectives.Any(o => o is AIObjectiveCombat && o.Priority > 0))
                {
                    _.Priority = 0;
                }
                else if (_.objectiveManager.Objectives.Any(o => o is AIObjectiveFindAndEquipRadiationSuit && o.Priority > 0))
                {
                    _.Priority = 0;
                }
                _.Priority = MathHelper.Clamp(_.Priority, 0, AIObjectiveManager.MaxObjectivePriority);
                if (_.divingGearObjective != null && !_.divingGearObjective.IsCompleted && _.divingGearObjective.CanBeCompleted)
                {
                    // Boost the priority while seeking the diving gear
                    _.Priority = Math.Max(_.Priority, Math.Min(AIObjectiveManager.EmergencyObjectivePriority - 1, AIObjectiveManager.MaxObjectivePriority));
                }
            }
            __result = _.Priority;
            return false;
        }


        public static bool ActPrefix(float deltaTime, AIObjectiveFindSafety __instance)
        {
            AIObjectiveFindSafety _ = __instance;
            if (_.resetPriority) { return false; }
            var currentHull = _.character.CurrentHull;
            bool dangerousPressure =  (currentHull == null || currentHull.LethalPressure > 0) && !_.character.IsProtectedFromPressure;
            bool shouldActOnSuffocation = _.character.IsLowInOxygen && !_.character.AnimController.HeadInWater && HumanAIController.HasDivingSuit(_.character, requireOxygenTank: false);
            
            // I added the whole logic here: fuelrod in fuelrodlist, -> hull safety check is 0 -> find safety -> check if caused by fuel rod in case find safety triggered by other hazards
            bool needsRadiationSuit = false;
            // Making bots say about the fuel rod before they do any action
            foreach (Item item in RadioactiveFuelRod.DangerousFuelRods)
            {
                if (item.HasTag("reactorfuel") && item.CurrentHull == _.character.CurrentHull)
                {
                    // Bot detects dangerous fuel rod
                    _.character.Speak(TextManager.Get("dialog.bots.dangerousfuelrod").Value, null, 0.0f, "dialog.bots.dangerousfuelrod".ToIdentifier(), 20.0f);
                    needsRadiationSuit = true;
                    break; // Exit the loop after triggering the objective
                }
                
            }
            

            if (!_.character.LockHands && (!dangerousPressure || shouldActOnSuffocation || _.cannotFindSafeHull))
            {

                
                if (needsRadiationSuit && !HumanAIControllerDieHard.HasRadiationSuit(_.character))
                {
                    // Seems that cannotFindDivingGear is used to reset bots attempts. Let us just use it for convenience. 
                    if (_.cannotFindDivingGear && _.retryCounter < _.findDivingGearAttempts)
                    {
                        _.retryTimer = _.retryResetTime;
                        _.retryCounter++;
                        needsRadiationSuit = !needsRadiationSuit;
                        _.RemoveSubObjective(ref AIObjectiveFindSafetyDieHard.findAndEquipRadiationSuit);
                    }

                    // Add logic for finding radiation gear
                    if (needsRadiationSuit && AIObjectiveFindSafetyDieHard.findAndEquipRadiationSuit == null)
                    {
                        _.cannotFindDivingGear = false;
                        _.RemoveSubObjective(ref _.goToObjective);
                        _.TryAddSubObjective(ref AIObjectiveFindSafetyDieHard.findAndEquipRadiationSuit,
                        constructor: () => new BarotraumaDieHard.AI.AIObjectiveFindAndEquipRadiationSuit(_.character, _.objectiveManager),
                        onAbandon: () =>
                        {
                            _.searchHullTimer = Math.Min(1, _.searchHullTimer);
                            _.cannotFindDivingGear = true;
                        },
                        onCompleted: () =>
                        {
                            _.resetPriority = true;
                            _.searchHullTimer = Math.Min(1, _.searchHullTimer);
                            _.RemoveSubObjective(ref _.divingGearObjective);
                        });
                    }
                }


                bool needsDivingGear = _.HumanAIController.NeedsDivingGear(currentHull, out bool needsDivingSuit);
                bool needsEquipment = shouldActOnSuffocation;
                if (needsDivingSuit)
                {
                    needsEquipment = !HumanAIController.HasDivingSuit(_.character, AIObjectiveFindDivingGear.GetMinOxygen(_.character));
                }
                else if (needsDivingGear)
                {
                    needsEquipment = !HumanAIController.HasDivingGear(_.character, AIObjectiveFindDivingGear.GetMinOxygen(_.character));
                }
                if (needsEquipment)
                {
                    if (_.cannotFindDivingGear && _.retryCounter < _.findDivingGearAttempts)
                    {
                        _.retryTimer = _.retryResetTime;
                        _.retryCounter++;
                        needsDivingSuit = !needsDivingSuit;
                        _.RemoveSubObjective(ref _.divingGearObjective);
                    }
                    if (_.divingGearObjective == null)
                    {
                        _.cannotFindDivingGear = false;
                        _.RemoveSubObjective(ref _.goToObjective);
                        _.TryAddSubObjective(ref _.divingGearObjective,
                        constructor: () => new AIObjectiveFindDivingGear(_.character, needsDivingSuit, _.objectiveManager),
                        onAbandon: () =>
                        {
                            _.searchHullTimer = Math.Min(1, _.searchHullTimer);
                            _.cannotFindDivingGear = true;
                            // Don't reset the diving gear objective, because it's possible that there is no diving gear -> seek a safe hull and then reset so that we can check again.
                        },
                        onCompleted: () =>
                        {
                            _.resetPriority = true;
                            _.searchHullTimer = Math.Min(1, _.searchHullTimer);
                            _.RemoveSubObjective(ref _.divingGearObjective);
                        });
                    }
                }
            }
            if (_.divingGearObjective == null || !_.divingGearObjective.CanBeCompleted)
            {
                if (_.currentHullSafety < HumanAIController.HULL_SAFETY_THRESHOLD)
                {
                    _.searchHullTimer = Math.Min(1, _.searchHullTimer);
                }
                if (_.searchHullTimer > 0.0f)
                {
                    _.searchHullTimer -= deltaTime;
                }
                else
                {
                    AIObjectiveFindSafety.HullSearchStatus hullSearchStatus = _.FindBestHull(out Hull potentialSafeHull, allowChangingSubmarine: _.character.TeamID != CharacterTeamType.FriendlyNPC);
                    if (hullSearchStatus != AIObjectiveFindSafety.HullSearchStatus.Finished)
                    {
                        _.UpdateSimpleEscape(deltaTime);
                        return false;
                    }
                    _.searchHullTimer = AIObjectiveFindSafety.SearchHullInterval * Rand.Range(0.9f, 1.1f);
                    _.previousSafeHull = _.currentSafeHull;
                    _.currentSafeHull = potentialSafeHull;
                    _.cannotFindSafeHull = _.currentSafeHull == null || _.NeedMoreDivingGear(_.currentSafeHull);
                    _.currentSafeHull ??= _.previousSafeHull;
                    if (_.currentSafeHull != null && _.currentSafeHull != currentHull)
                    {
                        if (_.goToObjective?.Target != _.currentSafeHull)
                        {
                            _.RemoveSubObjective(ref _.goToObjective);
                        }
                        _.TryAddSubObjective(ref _.goToObjective,
                        constructor: () => new AIObjectiveGoTo(_.currentSafeHull, _.character, _.objectiveManager, getDivingGearIfNeeded: true)
                        {
                            SpeakIfFails = false,
                            AllowGoingOutside =
                                _.character.IsProtectedFromPressure ||
                                _.character.CurrentHull == null || 
                                _.character.CurrentHull.IsAirlock ||
                                _.character.CurrentHull.LeadsOutside(_.character)
                        },
                        onCompleted: () =>
                        {
                            if (_.currentHullSafety > HumanAIController.HULL_SAFETY_THRESHOLD ||
                                _.HumanAIController.NeedsDivingGear(currentHull, out bool needsSuit) && (needsSuit ? HumanAIController.HasDivingSuit(_.character) : HumanAIController.HasDivingMask(_.character)))
                            {
                                _.resetPriority = true;
                                _.searchHullTimer = Math.Min(1, _.searchHullTimer);
                            }
                            _.RemoveSubObjective(ref _.goToObjective);
                            if (_.cannotFindDivingGear)
                            {
                                // If diving gear objective failed, let's reset it here.
                                _.RemoveSubObjective(ref _.divingGearObjective);
                            }
                        },
                        onAbandon: () =>
                        {
                            // Don't ignore any hulls if outside, because apparently it happens that we can't find a path, in which case we just want to try again.
                            // If we ignore the hull, it might be the only airlock in the target sub, which ignores the whole sub.
                            // If the target hull is inside a submarine that is not our main sub, just ignore it normally when it cannot be reached. This happens with outposts.
                            if (_.goToObjective != null)
                            {
                                if (_.goToObjective.Target is Hull hull)
                                {
                                    if (currentHull != null || !Submarine.MainSubs.Contains(hull.Submarine))
                                    {
                                        _.HumanAIController.UnreachableHulls.Add(hull);
                                    }
                                }
                            }
                            _.RemoveSubObjective(ref _.goToObjective);
                        });
                    }
                    else
                    {
                        _.RemoveSubObjective(ref _.goToObjective);
                    }
                }
                if (_.subObjectives.Any(so => so.CanBeCompleted)) { return false; }
                _.UpdateSimpleEscape(deltaTime);

                bool inFriendlySub = 
                    _.character.IsInFriendlySub || 
                    (_.character.IsEscorted && _.character.IsInPlayerSub);
                if (_.cannotFindSafeHull && !inFriendlySub && _.character.IsOnPlayerTeam && _.objectiveManager.Objectives.None(o => o is AIObjectiveReturn))
                {
                    if (OrderPrefab.Prefabs.TryGet("return".ToIdentifier(), out OrderPrefab orderPrefab))
                    {
                        _.objectiveManager.AddObjective(new AIObjectiveReturn(_.character, _.character, _.objectiveManager));
                    }
                }
            }

            return false;
        }
    }
}