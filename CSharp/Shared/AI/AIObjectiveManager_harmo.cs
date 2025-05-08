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
    class AIObjectiveManagerDieHard  : IAssemblyPlugin
    {


        public Harmony harmony;
        
        
        public void Initialize()
        {
            harmony = new Harmony("AIObjectiveManagerDieHard");

            var originalCreateObjective = typeof(AIObjectiveManager).GetMethod("CreateObjective", BindingFlags.Public | BindingFlags.Instance);
            var prefixCreateObjective = typeof(AIObjectiveManagerDieHard).GetMethod(nameof(CreateObjectivePrefix), BindingFlags.Public | BindingFlags.Static);

            harmony.Patch(originalCreateObjective, new HarmonyMethod(prefixCreateObjective), null);


           
            
        }

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }


        public static bool CreateObjectivePrefix(Order order, float priorityModifier, AIObjectiveManager __instance, ref AIObjective __result)
        {
            AIObjectiveManager _ = __instance;
            if (order == null || order.IsDismissal) { __result = null; return false;}
            AIObjective newObjective;
            switch (order.Identifier.Value.ToLowerInvariant())
            {
                case "follow":
                    if (order.OrderGiver == null) { __result = null; return false;}
                    newObjective = new AIObjectiveGoTo(order.OrderGiver, _.character, _, repeat: true, priorityModifier: priorityModifier)
                    {
                        CloseEnough = Rand.Range(80f, 100f),
                        CloseEnoughMultiplier = Math.Min(1 + _.HumanAIController.CountBotsInTheCrew(c => c.ObjectiveManager.HasOrder<AIObjectiveGoTo>(o => o.Target == order.OrderGiver)) * Rand.Range(0.8f, 1f), 4),
                        ExtraDistanceOutsideSub = 100,
                        ExtraDistanceWhileSwimming = 100,
                        AllowGoingOutside = true,
                        IgnoreIfTargetDead = true,
                        IsFollowOrder = true,
                        Mimic = _.character.IsOnPlayerTeam,
                        DialogueIdentifier = "dialogcannotreachplace".ToIdentifier()
                    };
                    break;
                case "wait":
                    newObjective = new AIObjectiveGoTo(order.TargetSpatialEntity ?? _.character, _.character, _, repeat: true, priorityModifier: priorityModifier)
                    {
                        AllowGoingOutside = true,
                        IsWaitOrder = true,
                        DebugLogWhenFails = false,
                        SpeakIfFails = false,
                        CloseEnough = 100
                    };
                    break;
                case "return":
                    newObjective = new AIObjectiveReturn(_.character, order.OrderGiver, _, priorityModifier: priorityModifier);
                    newObjective.Completed += () => _.DismissSelf(order);
                    break;
                case "fixleaks":
                    newObjective = new AIObjectiveFixLeaks(_.character, _, priorityModifier: priorityModifier, prioritizedHull: order.TargetEntity as Hull);
                    break;
                case "chargebatteries":
                    newObjective = new AIObjectiveChargeBatteries(_.character, _, order.Option, priorityModifier);
                    break;
                case "rescue":
                    newObjective = new AIObjectiveRescueAll(_.character, _, priorityModifier);
                    break;
                case "repairsystems":
                case "repairmechanical":
                case "repairelectrical":
                    newObjective = new AIObjectiveRepairItems(_.character, _, priorityModifier: priorityModifier, prioritizedItem: order.TargetEntity as Item)
                    {
                        RelevantSkill = order.AppropriateSkill,
                    };
                    break;
                case "pumpwater":
                    if (order.TargetItemComponent is Pump targetPump)
                    {
                        if (!order.TargetItemComponent.Item.IsInteractable(_.character)) { __result = null; return false;}
                        newObjective = new AIObjectiveOperateItem(targetPump, _.character, _, order.Option, false, priorityModifier: priorityModifier)
                        {
                            Override = order.OrderGiver is { IsCommanding: true }
                        };
                        newObjective.Completed += () => _.DismissSelf(order);
                    }
                    else
                    {
                        newObjective = new AIObjectivePumpWater(_.character, _, order.Option, priorityModifier: priorityModifier);
                    }
                    break;
                case "extinguishfires":
                    newObjective = new AIObjectiveExtinguishFires(_.character, _, priorityModifier);
                    break;
                case "fightintruders":
                    newObjective = new AIObjectiveFightIntruders(_.character, _, priorityModifier);
                    break;
                case "assaultenemy":
                    newObjective = new AIObjectiveFightIntruders(_.character, _, priorityModifier)
                    {
                        TargetCharactersInOtherSubs = true
                    };
                    break;
                case "steer":
                    var steering = (order?.TargetEntity as Item)?.GetComponent<Steering>();
                    if (steering != null) { steering.PosToMaintain = steering.Item.Submarine?.WorldPosition; }
                    if (order.TargetItemComponent == null) { __result = null; return false;}
                    if (!order.TargetItemComponent.Item.IsInteractable(_.character)) { __result = null; return false;}
                    newObjective = new AIObjectiveOperateItem(order.TargetItemComponent, _.character, _, order.Option,
                        requireEquip: false, useController: order.UseController, controller: order.ConnectedController, priorityModifier: priorityModifier)
                    {
                        Repeat = true,
                        // Don't override unless it's an order by a player
                        Override = order.OrderGiver != null && order.OrderGiver.IsCommanding 
                    };
                    break;
                case "setchargepct":
                    newObjective = new AIObjectiveOperateItem(order.TargetItemComponent, _.character, _, order.Option, false, priorityModifier: priorityModifier)
                    {
                        Override = !_.character.IsDismissed,
                        completionCondition = () =>
                        {
                            if (float.TryParse(order.Option.Value, out float pct))
                            {
                                var targetRatio = Math.Clamp(pct, 0f, 1f);
                                var currentRatio = (order.TargetItemComponent as PowerContainer).RechargeRatio;
                                return  Math.Abs(targetRatio - currentRatio) < 0.05f;
                                
                            }
                            return  true;
                            
                        }
                    };
                    break;
                case "getitem":
                    newObjective = new AIObjectiveGetItem(_.character, order.TargetEntity as Item ?? order.TargetItemComponent?.Item, _, false, priorityModifier: priorityModifier)
                    {
                        MustBeSpecificItem = true
                    };
                    break;
                case "cleanupitems":
                    if (order.TargetEntity is Item targetItem)
                    {
                        if (targetItem.HasTag(Tags.AllowCleanup) && targetItem.ParentInventory == null && targetItem.OwnInventory != null)
                        {
                            // Target all items inside the container
                            newObjective = new AIObjectiveCleanupItems(_.character, _, targetItem.OwnInventory.AllItems, priorityModifier);
                        }
                        else
                        {
                            newObjective = new AIObjectiveCleanupItems(_.character, _, targetItem, priorityModifier);
                        }
                    }
                    else
                    {
                        newObjective = new AIObjectiveCleanupItems(_.character, _, priorityModifier: priorityModifier);
                    }
                    break;
                case "escapehandcuffs":
                    newObjective = new AIObjectiveEscapeHandcuffs(_.character, _, priorityModifier: priorityModifier);
                    break;
                case "findthieves":
                    newObjective = new AIObjectiveFindThieves(_.character, _, priorityModifier: priorityModifier);
                    break;
                case "prepareforexpedition":
                    newObjective = new AIObjectivePrepare(_.character, _, order.GetTargetItems(order.Option), order.RequireItems)
                    {
                        KeepActiveWhenReady = true,
                        CheckInventory = true,
                        Equip = false,
                        FindAllItems = true,
                        RequireNonEmpty = false
                    };
                    break;
                case "findweapon":
                    AIObjectivePrepare prepareObjective;
                    if (order.TargetEntity is Item tItem)
                    {
                        prepareObjective = new AIObjectivePrepare(_.character, _, targetItem: tItem);
                    }
                    else
                    {
                        prepareObjective = new AIObjectivePrepare(_.character, _, order.GetTargetItems(order.Option), order.RequireItems)
                        {
                            CheckInventory = false,
                            EvaluateCombatPriority = true,
                            FindAllItems = false,
                            RequireNonEmpty = true
                        };
                    }
                    prepareObjective.KeepActiveWhenReady = false;
                    prepareObjective.Equip = true;
                    newObjective = prepareObjective;
                    newObjective.Completed += () => _.DismissSelf(order);
                    break;
                case "loaditems":
                    newObjective = new AIObjectiveLoadItems(_.character, _, order.Option, order.GetTargetItems(order.Option), order.TargetEntity as Item, priorityModifier);
                    break;
                case "deconstructitems":
                    newObjective = new AIObjectiveDeconstructItems(_.character, _, priorityModifier);
                    break;
                case "inspectnoises":
                    newObjective = new AIObjectiveInspectNoises(_.character, _, priorityModifier);
                    break;


                //Modded part: adding more orders.
                case "findandequipradiationsuit":
                    newObjective = new AIObjectiveFindAndEquipRadiationSuit(__instance.character, __instance, priorityModifier);
                    break;
                    
                default:
                    if (order.TargetItemComponent == null) { __result = null; return false;}
                    if (!order.TargetItemComponent.Item.IsInteractable(_.character)) { __result = null; return false;}
                    newObjective = new AIObjectiveOperateItem(order.TargetItemComponent, _.character, _, order.Option,
                        requireEquip: false, useController: order.UseController, controller: order.ConnectedController, priorityModifier: priorityModifier)
                    {
                        Repeat = true,
                        // Don't override unless it's an order by a player
                        Override = order.OrderGiver != null && order.OrderGiver.IsCommanding
                    };
                    if (newObjective.Abandon) { __result = null; return false;}
                    break;
            }
            if (newObjective != null)
            {
                newObjective.Identifier = order.Identifier;
            }
            newObjective.IgnoreAtOutpost = order.IgnoreAtOutpost;
            __result = newObjective;
            return false;
        }


    }
}