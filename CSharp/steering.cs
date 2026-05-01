using System.Collections.Generic;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using System;// fix for Math
using System.Linq;
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Barotrauma.Extensions;
using Barotrauma;
using SonarMod;//need to have this or GetComponent<CustomSonar>();won;t find
using System.Globalization;
using FarseerPhysics;

namespace BarotraumaDieHard
{
    class CustomSteering : Steering
    {
        // private List<Item> linkedItems; // Change to a List
		private Sonar sonar;
		
        public override void OnItemLoaded()
        {
            base.OnItemLoaded();
            sonar = item.GetComponent<CustomSonar>();
            // Populate linkedItems directly from item.linkedTo
            // linkedItems = item.linkedTo.Cast<Item>().ToList();
        }

        public CustomSteering(Item item, ContentXElement element)
            : base(item, element)
        {
        }

//i included all methods that have sonar variable. so the game doesn't crush when it can't find new customsonar.
		public override void Update(float deltaTime, Camera cam)
        {
            if (!searchedConnectedDockingPort)
            {
                FindConnectedDockingPort();
            }
            networkUpdateTimer -= deltaTime;
            if (unsentChanges)
            {
                if (networkUpdateTimer <= 0.0f)
                {
#if CLIENT
                    if (GameMain.Client != null)
                    {
                        item.CreateClientEvent(this);
                        correctionTimer = CorrectionDelay;
                    }
#endif
#if SERVER
                    item.CreateServerEvent(this);
#endif

                    networkUpdateTimer = 0.1f;
                    unsentChanges = false;
                }
            }

            controlledSub = item.Submarine;
            var sonar = item.GetComponent<Sonar>();
            if (sonar != null && sonar.UseTransducers)
            {
                controlledSub = sonar.ConnectedTransducers.Any() ? sonar.ConnectedTransducers.First().Item.Submarine : null;
            }

            if (Voltage < MinVoltage) { return; }

            if (user != null && user.Removed)
            {
                user = null;
            }

            ApplyStatusEffects(ActionType.OnActive, deltaTime);

            float userSkill = 0.0f;
            if (user != null && controlledSub != null &&
                (user.SelectedItem == item || item.linkedTo.Contains(user.SelectedItem)))
            {
                userSkill = user.GetSkillLevel("helm") / 100.0f;
            }

            // override autopilot pathing while the AI rams, and go full speed ahead
            if (AIRamTimer > 0f && controlledSub != null)
            {
                AIRamTimer -= deltaTime;
                TargetVelocity = GetSteeringVelocity(AITacticalTarget, 0f);
            }
            else if (AutoPilot)
            {
                //signals override autopilot for a duration of one second
                if (lastReceivedSteeringSignalTime < Timing.TotalTime - 1)
                {
                    UpdateAutoPilot(deltaTime);
                    float throttle = 1.0f;
                    if (controlledSub != null)
                    {
                        //if the sub is heading in the correct direction, throttle the speed according to the user's skill
                        //if it's e.g. sinking due to extra water, don't throttle, but allow emptying up the ballast completely
                        throttle = MathHelper.Clamp(Vector2.Dot(controlledSub.Velocity, TargetVelocity) / 100.0f, 0.0f, 1.0f);
                    }
                    float maxSpeed = MathHelper.Lerp(AutoPilotMaxSpeed, AIPilotMaxSpeed, userSkill) * 100.0f;
                    TargetVelocity = TargetVelocity.ClampLength(MathHelper.Lerp(100.0f, maxSpeed, throttle));
                }
            }
            else
            {
                showIceSpireWarning = false;
                if (user != null && user.Info != null && 
                    user.SelectedItem == item && 
                    controlledSub != null && controlledSub.Velocity.LengthSquared() > 0.01f)
                {
                    IncreaseSkillLevel(user, deltaTime);
                }

                Vector2 velocityDiff = steeringInput - targetVelocity;
                if (velocityDiff != Vector2.Zero)
                {
                    if (steeringAdjustSpeed >= 0.99f)
                    {
                        TargetVelocity = steeringInput;
                    }
                    else
                    {
                        float steeringChange = 1.0f / (1.0f - steeringAdjustSpeed);
                        steeringChange *= steeringChange * 10.0f;

                        TargetVelocity += Vector2.Normalize(velocityDiff) * 
                            Math.Min(steeringChange * deltaTime, velocityDiff.Length());
                    }
                }
            }

            float velX = targetVelocity.X;
            if (controlledSub != null && controlledSub.FlippedX) { velX *= -1; }
            item.SendSignal(new Signal(velX.ToString(CultureInfo.InvariantCulture), sender: user), "velocity_x_out");

            float velY = MathHelper.Lerp((neutralBallastLevel * 100 - 50) * 2, -100 * Math.Sign(targetVelocity.Y), Math.Abs(targetVelocity.Y) / 100.0f);
            item.SendSignal(new Signal(velY.ToString(CultureInfo.InvariantCulture), sender: user), "velocity_y_out");

            // converts the controlled sub's velocity to km/h and sends it. 
            if (controlledSub is { } sub)
            {
                item.SendSignal(new Signal((ConvertUnits.ToDisplayUnits(sub.Velocity.X * Physics.DisplayToRealWorldRatio) * 3.6f).ToString("0.0000", CultureInfo.InvariantCulture), sender: user), "current_velocity_x");
                item.SendSignal(new Signal((ConvertUnits.ToDisplayUnits(sub.Velocity.Y * Physics.DisplayToRealWorldRatio) * -3.6f).ToString("0.0000", CultureInfo.InvariantCulture), sender: user), "current_velocity_y");

                Vector2 pos = new Vector2(sub.WorldPosition.X * Physics.DisplayToRealWorldRatio, sub.RealWorldDepth);
                if (sonar != null && sonar.UseTransducers && sonar.CenterOnTransducers && sonar.ConnectedTransducers.Any())
                {
                    pos = Vector2.Zero;
                    foreach (var connectedTransducer in sonar.ConnectedTransducers)
                    {
                        pos += connectedTransducer.Item.WorldPosition;
                    }
                    pos /= sonar.ConnectedTransducers.Count();
                    pos = new Vector2(
                        pos.X * Physics.DisplayToRealWorldRatio,
                        Level.Loaded?.GetRealWorldDepth(pos.Y) ?? (-pos.Y * Physics.DisplayToRealWorldRatio));
                }

                item.SendSignal(new Signal(pos.X.ToString("0.0000", CultureInfo.InvariantCulture), sender: user), "current_position_x");
                item.SendSignal(new Signal(pos.Y.ToString("0.0000", CultureInfo.InvariantCulture), sender: user), "current_position_y");
            }

            // if our tactical AI pilot has left, revert back to maintaining position
            if (navigateTactically && (user == null || user.SelectedItem != item))
            {
                navigateTactically = false;
                AIRamTimer = 0f;
                SetMaintainPosition();
            }
        }
		
		public override bool CrewAIOperate(float deltaTime, Character character, AIObjectiveOperateItem objective)
        {
            character.AIController.SteeringManager.Reset();
            if (objective.Override)
            {
                if (user != character && user != null && user.SelectedItem == item && character.IsOnPlayerTeam)
                {
                    character.Speak(TextManager.Get("DialogSteeringTaken").Value, null, 0.0f, "steeringtaken".ToIdentifier(), 10.0f);
                }
            }
            user = character;

            if (Item.ConditionPercentage <= 0 && AIObjectiveRepairItems.IsValidTarget(Item, character))
            {
                if (Item.Repairables.Average(r => r.DegreeOfSuccess(character)) > 0.4f)
                {
                    objective.AddSubObjective(new AIObjectiveRepairItem(character, Item, objective.objectiveManager, isPriority: true));
                    return false;
                }
                else
                {
                    character.Speak(TextManager.Get("DialogNavTerminalIsBroken").Value, identifier: "navterminalisbroken".ToIdentifier(), minDurationBetweenSimilar: 30.0f);
                }
            }

            if (!AutoPilot)
            {
                unsentChanges = true;
                AutoPilot = true;
            }
            IncreaseSkillLevel(user, deltaTime);
            if (objective.Option == "maintainposition")
            {
                if (objective.Override)
                {
                    SetMaintainPosition();
                }
            }
            else if (!Level.IsLoadedOutpost)
            {
                if (objective.Option == "navigateback")
                {
                    if (DockingSources.Any(d => d.Docked))
                    {
                        item.SendSignal("1", "toggle_docking");
                    }

                    if (objective.Override)
                    {
                        if (MaintainPos || LevelEndSelected || !LevelStartSelected || navigateTactically)
                        {
                            unsentChanges = true;
                        }

                        SetDestinationLevelStart();
                    }
                }
                else if (objective.Option == "navigatetodestination")
                {
                    if (DockingSources.Any(d => d.Docked))
                    {
                        item.SendSignal("1", "toggle_docking");
                    }

                    if (objective.Override)
                    {
                        if (MaintainPos || !LevelEndSelected || LevelStartSelected || navigateTactically)
                        {
                            unsentChanges = true;
                        }

                        SetDestinationLevelEnd();
                    }
                }
                else if (objective.Option == "navigatetactical")
                {
                    if (DockingSources.Any(d => d.Docked))
                    {
                        item.SendSignal("1", "toggle_docking");
                    }

                    if (objective.Override)
                    {
                        if (MaintainPos || LevelEndSelected || LevelStartSelected || !navigateTactically)
                        {
                            unsentChanges = true;
                        }

                        SetDestinationTactical();
                    }
                }
            }

            sonar?.CrewAIOperate(deltaTime, character, objective);
            if (!MaintainPos && showIceSpireWarning && character.IsOnPlayerTeam)
            {
                character.Speak(TextManager.Get("dialogicespirespottedsonar").Value, null, 0.0f, "icespirespottedsonar".ToIdentifier(), 60.0f);
            }
            return false;
        }
         


        // Override other methods as needed
    }

}