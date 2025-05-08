using System.Collections.Generic;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using System;// fix for Math
using System.Linq;
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Barotrauma.Extensions;
using Barotrauma;
using HarmonyLib;
using System.Globalization;
using TorpedoMod;
using System.Reflection;//for FieldInfo



using FarseerPhysics;//for convert units

#if CLIENT
using Microsoft.Xna.Framework.Graphics;
#endif

namespace FireControlSonar
{
    class FireControlSonar : Sonar
    {
        
        // private List<Item> linkedItems; // Change to a List
		private const float PingFrequency = 0.5f;//how fast a ping spreads
		private float timeSinceLastPing = 1.1f;

		private static float NewSectorAngleFireControl { get; set; } = 30.0f;

        private const float MaxTorpedoDistance = 50000f;
        private HashSet<int> torpedoesReportedMissing = new HashSet<int>(); // To keep track of torpedoes that have been reported as missing

        // Calculate the dot product whenever NewSectorAngle is changed
        private static float NewDotProductFireControl => (float)Math.Cos(MathHelper.ToRadians(NewSectorAngleFireControl) *0.5f);


        public static FieldInfo pingDirectionField = AccessTools.Field(typeof(FireControlSonar), "pingDirection");
        public override void OnItemLoaded()
        {
            base.OnItemLoaded();
            
            // Populate linkedItems directly from item.linkedTo
            // linkedItems = item.linkedTo.Cast<Item>().ToList();
        }

        public FireControlSonar(Item item, ContentXElement element)
            : base(item, element)
        {
        }
#if CLIENT
        private void DrawSonar(SpriteBatch spriteBatch, Rectangle rect)
        {
            displayBorderSize = 0.2f;
            center = rect.Center.ToVector2();
            DisplayRadius = (rect.Width / 2.0f) * (1.0f - displayBorderSize);
            DisplayScale = DisplayRadius / range * zoom;

            screenBackground?.Draw(spriteBatch, center, 0.0f, rect.Width / screenBackground.size.X);

            if (useDirectionalPing)
            {
                directionalPingBackground?.Draw(spriteBatch, center, 0.0f, rect.Width / directionalPingBackground.size.X);
                if (directionalPingButton != null)
                {
                    int buttonSprIndex = 0;
                    if (pingDragDirection != null)
                    {
                        buttonSprIndex = 2;
                    }
                    else if (MouseInDirectionalPingRing(rect, true))
                    {
                        buttonSprIndex = 1;
                    }
                    directionalPingButton[buttonSprIndex]?.Draw(spriteBatch, center, MathUtils.VectorToAngle(pingDirection), rect.Width / directionalPingBackground.size.X);
                }
            }

            if (currentPingIndex != -1)
            {
                var activePing = activePings[currentPingIndex];
                if (activePing.IsDirectional && directionalPingCircle != null)
                {
                    //directional ping circle, pure visual effect. Changed to green
                    directionalPingCircle.Draw(spriteBatch, center, Color.Green * (1.0f - activePing.State),
                        rotate: MathUtils.VectorToAngle(activePing.Direction),
                        scale: DisplayRadius / directionalPingCircle.size.X * activePing.State);
                }
                else//this has been disabled. FireControlSonar is not an onmi-sonar
                {
                    pingCircle.Draw(spriteBatch, center, Color.White * (1.0f - activePing.State), 0.0f, (DisplayRadius * 2 / pingCircle.size.X) * activePing.State);
                }
            }

            float signalStrength = 1.0f;
            if (UseTransducers)
            {
                signalStrength = 0.0f;
                foreach (ConnectedTransducer connectedTransducer in connectedTransducers)
                {
                    signalStrength = Math.Max(signalStrength, connectedTransducer.SignalStrength);
                }
            }

            Vector2 transducerCenter = GetTransducerPos();// + DisplayOffset;

            if (sonarBlips.Count > 0)
            {
                float blipScale = 0.08f * (float)Math.Sqrt(zoom) * (rect.Width / 700.0f);
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive);

                foreach (SonarBlip sonarBlip in sonarBlips)
                {
                    DrawBlip(spriteBatch, sonarBlip, transducerCenter + DisplayOffset, center, sonarBlip.FadeTimer / 2.0f * signalStrength, blipScale);
                }

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            }

            if (item.Submarine != null && !DetectSubmarineWalls)
            {
                transducerCenter += DisplayOffset;
                DrawDockingPorts(spriteBatch, transducerCenter, signalStrength);
                DrawOwnSubmarineBorders(spriteBatch, transducerCenter, signalStrength);
            }
            else
            {
                DisplayOffset = Vector2.Zero;
            }

            float directionalPingVisibility = useDirectionalPing && currentMode == Mode.Active ? 1.0f : showDirectionalIndicatorTimer;
            if (directionalPingVisibility > 0.0f)
            {
                Vector2 sector1 = MathUtils.RotatePointAroundTarget(pingDirection * DisplayRadius, Vector2.Zero, MathHelper.ToRadians(NewSectorAngleFireControl * 0.5f));
                Vector2 sector2 = MathUtils.RotatePointAroundTarget(pingDirection * DisplayRadius, Vector2.Zero, MathHelper.ToRadians(-NewSectorAngleFireControl * 0.5f));
                DrawLine(spriteBatch, Vector2.Zero, sector1, Color.LightCyan * 0.2f * directionalPingVisibility, width: 3);
                DrawLine(spriteBatch, Vector2.Zero, sector2, Color.LightCyan * 0.2f * directionalPingVisibility, width: 3);
            }

            if (GameMain.DebugDraw)
            {
                GUI.DrawString(spriteBatch, rect.Location.ToVector2(), sonarBlips.Count.ToString(), Color.White);
            }

            screenOverlay?.Draw(spriteBatch, center, 0.0f, rect.Width / screenOverlay.size.X);

            if (signalStrength <= 0.5f)
            {
                signalWarningText.Text = TextManager.Get(signalStrength <= 0.0f ? "SonarNoSignal" : "SonarSignalWeak");
                signalWarningText.Color = signalStrength <= 0.0f ? negativeColor : warningColor;
                signalWarningText.Visible = true;
                return;
            }
            else
            {
                signalWarningText.Visible = false;
            }

            foreach (AITarget aiTarget in AITarget.List)
            {
                if (aiTarget.InDetectable) { continue; }
                if (aiTarget.SonarLabel.IsNullOrEmpty() || aiTarget.SoundRange <= 0.0f) { continue; }

                if (Vector2.DistanceSquared(aiTarget.WorldPosition, transducerCenter) < aiTarget.SoundRange * aiTarget.SoundRange)
                {
                    DrawMarker(spriteBatch,
                        aiTarget.SonarLabel.Value,
                        aiTarget.SonarIconIdentifier,
                        aiTarget,
                        aiTarget.WorldPosition, transducerCenter,
                        DisplayScale, center, DisplayRadius * 0.975f);
                }
            }

            if (GameMain.GameSession == null) { return; }

            if (Level.Loaded != null)
            {
                if (Level.Loaded.StartLocation?.Type is { ShowSonarMarker: true })
                {
                    DrawMarker(spriteBatch,
                        Level.Loaded.StartLocation.DisplayName.Value,
                        (Level.Loaded.StartOutpost != null ? "outpost" : "location").ToIdentifier(),
                        "startlocation",
                        Level.Loaded.StartExitPosition, transducerCenter,
                        DisplayScale, center, DisplayRadius);
                }

                if (Level.Loaded is { EndLocation.Type.ShowSonarMarker: true, Type: LevelData.LevelType.LocationConnection })
                {
                    DrawMarker(spriteBatch,
                        Level.Loaded.EndLocation.DisplayName.Value,
                        (Level.Loaded.EndOutpost != null ? "outpost" : "location").ToIdentifier(),
                        "endlocation",
                        Level.Loaded.EndExitPosition, transducerCenter,
                        DisplayScale, center, DisplayRadius);
                }

                for (int i = 0; i < Level.Loaded.Caves.Count; i++)
                {
                    var cave = Level.Loaded.Caves[i];
                    if (cave.MissionsToDisplayOnSonar.None()) { continue; }
                    DrawMarker(spriteBatch,
                        caveLabel.Value,
                        "cave".ToIdentifier(),
                        "cave" + i,
                        cave.StartPos.ToVector2(), transducerCenter,
                        DisplayScale, center, DisplayRadius);
                }
            }

            int missionIndex = 0;
            foreach (Mission mission in GameMain.GameSession.Missions)
            {
                int i = 0;
                foreach ((LocalizedString label, Vector2 position) in mission.SonarLabels)
                {
                    if (!string.IsNullOrEmpty(label.Value))
                    {
                        DrawMarker(spriteBatch,
                            label.Value,
                            mission.SonarIconIdentifier,
                            "mission" + missionIndex + ":" + i,
                            position, transducerCenter,
                            DisplayScale, center, DisplayRadius * 0.95f);
                    }
                    i++;
                }
                missionIndex++;
            }

            if (HasMineralScanner && UseMineralScanner && CurrentMode == Mode.Active && MineralClusters != null &&
                (item.CurrentHull == null || !DetectSubmarineWalls))
            {
                foreach (var c in MineralClusters)
                {
                    var unobtainedMinerals = c.resources.Where(i => i != null && i.GetComponent<Holdable>() is { Attached: true });
                    if (unobtainedMinerals.None()) { continue; }
                    if (!CheckResourceMarkerVisibility(c.center, transducerCenter)) { continue; }
                    var i = unobtainedMinerals.FirstOrDefault();
                    if (i == null) { continue; }

                    bool disrupted = false;
                    foreach ((Vector2 disruptPos, float disruptStrength) in disruptedDirections)
                    {
                        float dot = Vector2.Dot(Vector2.Normalize(c.center - transducerCenter), disruptPos);
                        if (dot > 1.0f - disruptStrength)
                        {
                            disrupted = true;
                            break;
                        }
                    }
                    if (disrupted) { continue; }

                    DrawMarker(spriteBatch,
                        i.Name, "mineral".ToIdentifier(), "mineralcluster" + i,
                        c.center, transducerCenter,
                        DisplayScale, center, DisplayRadius * 0.95f,
                        onlyShowTextOnMouseOver: true);
                }
            }

            foreach (Submarine sub in Submarine.Loaded)
            {
                if (!sub.ShowSonarMarker) { continue; }
                if (connectedSubs.Contains(sub)) { continue; }
                if (Level.Loaded != null && sub.WorldPosition.Y > Level.Loaded.Size.Y) { continue; }

                if (item.Submarine != null || Character.Controlled != null)
                {
                    //hide enemy team
                    if (sub.TeamID == CharacterTeamType.Team1 && (item.Submarine?.TeamID == CharacterTeamType.Team2 || Character.Controlled?.TeamID == CharacterTeamType.Team2))
                    {
                        continue;
                    }
                    else if (sub.TeamID == CharacterTeamType.Team2 && (item.Submarine?.TeamID == CharacterTeamType.Team1 || Character.Controlled?.TeamID == CharacterTeamType.Team1))
                    {
                        continue;
                    }
                }

                DrawMarker(spriteBatch,
                    sub.Info.DisplayName.Value,
                    (sub.Info.HasTag(SubmarineTag.Shuttle) ? "shuttle" : "submarine").ToIdentifier(),
                    sub,
                    sub.WorldPosition, transducerCenter, 
                    DisplayScale, center, DisplayRadius * 0.95f);
            }

            if (GameMain.DebugDraw)
            {
                var steering = item.GetComponent<Steering>();
                steering?.DebugDrawHUD(spriteBatch, transducerCenter, DisplayScale, DisplayRadius, center);
            }
        }
        
        public override void UpdateHUDComponentSpecific(Character character, float deltaTime, Camera cam)
        {
            showDirectionalIndicatorTimer -= deltaTime;
            if (GameMain.Client != null)
            {
                if (unsentChanges)
                {
                    if (networkUpdateTimer <= 0.0f)
                    {
                        item.CreateClientEvent(this);
                        correctionTimer = CorrectionDelay;
                        networkUpdateTimer = 0.1f;
                        unsentChanges = false;
                    }
                }
                networkUpdateTimer -= deltaTime;
            }

            connectedSubUpdateTimer -= deltaTime;
            if (connectedSubUpdateTimer <= 0.0f)
            {
                connectedSubs.Clear();
                if (UseTransducers)
                {
                    foreach (var transducer in connectedTransducers)
                    {
                        if (transducer.Transducer.Item.Submarine == null) { continue; }
                        if (connectedSubs.Contains(transducer.Transducer.Item.Submarine)) { continue; }
                        connectedSubs.AddRange(transducer.Transducer.Item.Submarine.GetConnectedSubs());
                    }
                }
                else if (item.Submarine != null)
                {
                    connectedSubs.AddRange(item.Submarine?.GetConnectedSubs());
                }
                connectedSubUpdateTimer = ConnectedSubUpdateInterval;
            }

            Steering steering = item.GetComponent<Steering>();
            if (sonarView.Rect.Contains(PlayerInput.MousePosition) && 
                (GUI.MouseOn == null || GUI.MouseOn == sonarView || sonarView.IsParentOf(GUI.MouseOn) || GUI.MouseOn == steering?.GuiFrame || (steering?.GuiFrame?.IsParentOf(GUI.MouseOn) ?? false)))
            {
                float scrollSpeed = PlayerInput.ScrollWheelSpeed / 1000.0f;
                if (Math.Abs(scrollSpeed) > 0.0001f)
                {
                    zoomSlider.BarScroll += PlayerInput.ScrollWheelSpeed / 1000.0f;
                    zoomSlider.OnMoved(zoomSlider, zoomSlider.BarScroll);
                }
            }

            Vector2 transducerCenter = GetTransducerPos();

            if (steering != null && steering.DockingModeEnabled && steering.ActiveDockingSource != null)
            {
                Vector2 worldFocusPos = (steering.ActiveDockingSource.Item.WorldPosition + steering.DockingTarget.Item.WorldPosition) / 2.0f;
                DisplayOffset = Vector2.Lerp(DisplayOffset, worldFocusPos - transducerCenter, 0.1f);
            }
            else
            {
                DisplayOffset = Vector2.Lerp(DisplayOffset, Vector2.Zero, 0.1f);
            }
            transducerCenter += DisplayOffset;

            float distort = MathHelper.Clamp(1.0f - item.Condition / item.MaxCondition, 0.0f, 1.0f);
            for (int i = sonarBlips.Count - 1; i >= 0; i--)
            {
                sonarBlips[i].FadeTimer -= deltaTime * MathHelper.Lerp(0.5f, 2.0f, distort);
                sonarBlips[i].Position += sonarBlips[i].Velocity * deltaTime;

                if (sonarBlips[i].FadeTimer <= 0.0f) { sonarBlips.RemoveAt(i); }
            }

            //sonar view can only get focus when the cursor is inside the circle
            sonarView.CanBeFocused = 
                Vector2.DistanceSquared(sonarView.Rect.Center.ToVector2(), PlayerInput.MousePosition) <
                (sonarView.Rect.Width / 2 * sonarView.Rect.Width / 2);

            if (HasMineralScanner && Level.Loaded != null && !Level.Loaded.Generating)
            {
                if (MineralClusters == null)
                {
                    MineralClusters = new List<(Vector2, List<Item>)>();
                    Level.Loaded.PathPoints.ForEach(p => p.ClusterLocations.ForEach(c => AddIfValid(c)));
                    Level.Loaded.AbyssResources.ForEach(c => AddIfValid(c));

                    void AddIfValid(Level.ClusterLocation c)
                    {
                        if (c.Resources == null) { return; }
                        if (c.Resources.None(i => i != null && !i.Removed && i.Tags.Contains("ore"))) { return; }
                        var pos = Vector2.Zero;
                        foreach (var r in c.Resources)
                        {
                            pos += r.WorldPosition;
                        }
                        pos /= c.Resources.Count;
                        MineralClusters.Add((center: pos, resources: c.Resources));
                    }
                }
                else
                {
                    MineralClusters.RemoveAll(c => c.resources == null || c.resources.None() || c.resources.All(i => i == null || i.Removed));
                }
            }

            if (UseTransducers && connectedTransducers.Count == 0)
            {
                return;
            }

            if (Level.Loaded != null)
            {
                nearbyObjectUpdateTimer -= deltaTime;
                if (nearbyObjectUpdateTimer <= 0.0f)
                {
                    nearbyObjects.Clear();
                    foreach (var nearbyObject in Level.Loaded.LevelObjectManager.GetAllObjects(transducerCenter, range * zoom))
                    {
                        if (!nearbyObject.VisibleOnSonar) { continue; }
                        float objectRange = range + nearbyObject.SonarRadius;
                        if (Vector2.DistanceSquared(transducerCenter, nearbyObject.WorldPosition) < objectRange * objectRange)
                        {
                            nearbyObjects.Add(nearbyObject);
                        }
                    }
                    nearbyObjectUpdateTimer = NearbyObjectUpdateInterval;
                }

                List<LevelTrigger> ballastFloraSpores = new List<LevelTrigger>();
                Dictionary<LevelTrigger, Vector2> levelTriggerFlows = new Dictionary<LevelTrigger, Vector2>();
                for (var pingIndex = 0; pingIndex < activePingsCount; ++pingIndex)
                {
                    var activePing = activePings[pingIndex];
                    float pingRange = range * activePing.State / zoom;
                    foreach (LevelObject levelObject in nearbyObjects)
                    {
                        if (levelObject.Triggers == null) { continue; }
                        //gather all nearby triggers that are causing the water to flow into the dictionary
                        foreach (LevelTrigger trigger in levelObject.Triggers)
                        {
                            Vector2 flow = trigger.GetWaterFlowVelocity();
                            //ignore ones that are barely doing anything (flow^2 <= 1)
                            if (flow.LengthSquared() >= 1.0f && !levelTriggerFlows.ContainsKey(trigger))
                            {
                                levelTriggerFlows.Add(trigger, flow);
                            }
                            if (!trigger.InfectIdentifier.IsEmpty && 
                                Vector2.DistanceSquared(transducerCenter, trigger.WorldPosition) < pingRange / 2 * pingRange / 2)
                            {
                                ballastFloraSpores.Add(trigger);
                            }
                        }
                    }
                }

                foreach (KeyValuePair<LevelTrigger, Vector2> triggerFlow in levelTriggerFlows)
                {
                    LevelTrigger trigger = triggerFlow.Key;
                    Vector2 flow = triggerFlow.Value;

                    float flowMagnitude = flow.Length();
                    if (Rand.Range(0.0f, 1.0f) < flowMagnitude / 1000.0f)
                    {
                        float edgeDist = Rand.Range(0.0f, 1.0f);
                        Vector2 blipPos = trigger.WorldPosition + Rand.Vector(trigger.ColliderRadius * edgeDist);
                        Vector2 blipVel = flow;

                        //go through other triggers in range and add the flows of the ones that the blip is inside
                        foreach (KeyValuePair<LevelTrigger, Vector2> triggerFlow2 in levelTriggerFlows)
                        {
                            LevelTrigger trigger2 = triggerFlow2.Key;
                            if (trigger2 != trigger && Vector2.DistanceSquared(blipPos, trigger2.WorldPosition) < trigger2.ColliderRadius * trigger2.ColliderRadius)
                            {
                                Vector2 trigger2flow = triggerFlow2.Value;
                                if (trigger2.ForceFalloff) trigger2flow *= 1.0f - Vector2.Distance(blipPos, trigger2.WorldPosition) / trigger2.ColliderRadius;
                                blipVel += trigger2flow;
                            }
                        }
                        var flowBlip = new SonarBlip(blipPos, Rand.Range(0.5f, 1.0f), 1.0f)
                        {
                            Velocity = blipVel * Rand.Range(1.0f, 5.0f),
                            Size = new Vector2(MathHelper.Lerp(0.4f, 5f, flowMagnitude / 500.0f), 0.2f),
                            Rotation = (float)Math.Atan2(-blipVel.Y, blipVel.X)
                        };
                        sonarBlips.Add(flowBlip);
                    }
                }

                foreach (LevelTrigger spore in ballastFloraSpores)
                {
                    Vector2 blipPos = spore.WorldPosition + Rand.Vector(spore.ColliderRadius * Rand.Range(0.0f, 1.0f));
                    SonarBlip sporeBlip = new SonarBlip(blipPos, Rand.Range(0.1f, 0.5f), 0.5f)
                    {
                        Rotation = Rand.Range(-MathHelper.TwoPi, MathHelper.TwoPi),
                        BlipType = BlipType.Default,
                        Velocity = Rand.Vector(100f, Rand.RandSync.Unsynced)
                    };

                    sonarBlips.Add(sporeBlip);
                }

                float outsideLevelFlow = 0.0f;
                if (transducerCenter.X < 0.0f)
                {
                    outsideLevelFlow = Math.Abs(transducerCenter.X * 0.001f);
                }
                else if (transducerCenter.X > Level.Loaded.Size.X)
                {
                    outsideLevelFlow = -(transducerCenter.X - Level.Loaded.Size.X) * 0.001f;
                }

                if (Rand.Range(0.0f, 100.0f) < Math.Abs(outsideLevelFlow))
                {
                    Vector2 blipPos = transducerCenter + Rand.Vector(Rand.Range(0.0f, range));
                    var flowBlip = new SonarBlip(blipPos, Rand.Range(0.5f, 1.0f), 1.0f)
                    {
                        Velocity = Vector2.UnitX * outsideLevelFlow * Rand.Range(50.0f, 100.0f),
                        Size = new Vector2(Rand.Range(0.4f, 5f), 0.2f),
                        Rotation = 0.0f
                    };
                    sonarBlips.Add(flowBlip);                    
                }
            }

            if (steering != null && steering.DockingModeEnabled && steering.ActiveDockingSource != null)
            {
                float dockingDist = Vector2.Distance(steering.ActiveDockingSource.Item.WorldPosition, steering.DockingTarget.Item.WorldPosition);
                if (prevDockingDist > steering.DockingAssistThreshold && dockingDist <= steering.DockingAssistThreshold)
                {
                    zoomSlider.BarScroll = 0.25f;
                    zoom = Math.Max(zoom, MathHelper.Lerp(MinZoom, MaxZoom, zoomSlider.BarScroll));
                }
                else if (prevDockingDist > steering.DockingAssistThreshold * 0.75f && dockingDist <= steering.DockingAssistThreshold * 0.75f)
                {
                    zoomSlider.BarScroll = 0.5f;
                    zoom = Math.Max(zoom, MathHelper.Lerp(MinZoom, MaxZoom, zoomSlider.BarScroll));
                }
                else if (prevDockingDist > steering.DockingAssistThreshold * 0.5f && dockingDist <= steering.DockingAssistThreshold * 0.5f)
                {
                    zoomSlider.BarScroll = 0.25f;
                    zoom = Math.Max(zoom, MathHelper.Lerp(MinZoom, MaxZoom, zoomSlider.BarScroll));
                }
                prevDockingDist = Math.Min(dockingDist, prevDockingDist);
            }
            else
            {
                prevDockingDist = float.MaxValue;
            }

            if (steering != null && directionalPingButton != null)
            {
                steering.SteerRadius = useDirectionalPing && pingDragDirection != null ?
                    -1.0f :
                    PlayerInput.PrimaryMouseButtonDown() || !PlayerInput.PrimaryMouseButtonHeld() ?
                        (float?)((sonarView.Rect.Width / 2) - (directionalPingButton[0].size.X * sonarView.Rect.Width / screenBackground.size.X)) :
                        null;                
            }

            if (useDirectionalPing)
            {
                Vector2 newDragDir = Vector2.Normalize(PlayerInput.MousePosition - sonarView.Rect.Center.ToVector2());
                if (MouseInDirectionalPingRing(sonarView.Rect, true) && PlayerInput.PrimaryMouseButtonDown())
                {
                    pingDragDirection = newDragDir;
                }

                if (pingDragDirection != null && PlayerInput.PrimaryMouseButtonHeld())
                {
                    float newAngle = MathUtils.WrapAngleTwoPi(MathUtils.VectorToAngle(newDragDir));
                    SetPingDirection(new Vector2((float)Math.Cos(newAngle), (float)Math.Sin(newAngle)));
                }
                else
                {
                    pingDragDirection = null;
                }
            }
            else
            {
                pingDragDirection = null;
            }
            
            disruptionUpdateTimer -= deltaTime;
            for (var pingIndex = 0; pingIndex < activePingsCount; ++pingIndex)
            {
                var activePing = activePings[pingIndex];
                float pingRadius = DisplayRadius * activePing.State / zoom;
                if (disruptionUpdateTimer <= 0.0f) { UpdateDisruptions(transducerCenter, pingRadius / DisplayScale); }               
                Ping(transducerCenter, transducerCenter,
                    pingRadius, activePing.PrevPingRadius, DisplayScale, range / zoom, passive: false, pingStrength: 2.0f);
                activePing.PrevPingRadius = pingRadius;
            }
            if (disruptionUpdateTimer <= 0.0f)
            {
                disruptionUpdateTimer = DisruptionUpdateInterval;
            }

            longRangeUpdateTimer -= deltaTime;
            if (longRangeUpdateTimer <= 0.0f)
            {
                foreach (Character c in Character.CharacterList)
                {
                    if (c.AnimController.CurrentHull != null || !c.Enabled) { continue; }
                    if (c.Params.HideInSonar) { continue; }

                    if (!c.IsUnconscious && c.Params.DistantSonarRange > 0.0f &&
                        ((c.WorldPosition - transducerCenter) * DisplayScale).LengthSquared() > DisplayRadius * DisplayRadius)
                    {
                        Vector2 targetVector = c.WorldPosition - transducerCenter;
                        if (targetVector.LengthSquared() > MathUtils.Pow2(c.Params.DistantSonarRange)) { continue; }
                        float dist = targetVector.Length();
                        Vector2 targetDir = targetVector / dist;
                        int blipCount = (int)MathHelper.Clamp(c.Mass, 50, 200);
                        for (int i = 0; i < blipCount; i++)
                        {
                            float angle = Rand.Range(-0.5f, 0.5f);
                            Vector2 blipDir = MathUtils.RotatePoint(targetDir, angle);
                            Vector2 invBlipDir = MathUtils.RotatePoint(targetDir, -angle);
                            var longRangeBlip = new SonarBlip(transducerCenter + blipDir * Range * 0.9f, Rand.Range(1.9f, 2.1f), Rand.Range(1.0f, 1.5f), BlipType.LongRange)
                            {
                                Velocity = -invBlipDir * (MathUtils.Round(Rand.Range(8000.0f, 15000.0f), 2000.0f) - Math.Abs(angle * angle * 10000.0f)),
                                Rotation = (float)Math.Atan2(-invBlipDir.Y, invBlipDir.X),
                                Alpha = MathUtils.Pow2((c.Params.DistantSonarRange - dist) / c.Params.DistantSonarRange)
                            };
                            longRangeBlip.Size.Y *= 5.0f;
                            sonarBlips.Add(longRangeBlip);
                        }
                    }
                }
                longRangeUpdateTimer = LongRangeUpdateInterval;
            }

            if (currentMode == Mode.Active && currentPingIndex != -1)
            {
                return;
            }

            float passivePingRadius = (float)(Timing.TotalTime % 1.0f);
            if (passivePingRadius > 0.0f)
            {
                if (activePingsCount == 0) { disruptedDirections.Clear(); }
                //emit "pings" from nearby sound-emitting AITargets to reveal what's around them
                foreach (AITarget t in AITarget.List)
                {
                    if (t.Entity is Character c && !c.IsUnconscious && c.Params.HideInSonar) { continue; }
                    if (t.SoundRange <= 0.0f || float.IsNaN(t.SoundRange) || float.IsInfinity(t.SoundRange)) { continue; }

                    float distSqr = Vector2.DistanceSquared(t.WorldPosition, transducerCenter);
                    if (distSqr > t.SoundRange * t.SoundRange * 2) { continue; }

                    float dist = (float)Math.Sqrt(distSqr);
                    if (dist > prevPassivePingRadius * Range && dist <= passivePingRadius * Range && Rand.Int(sonarBlips.Count) < 500)
                    {
                        Ping(t.WorldPosition, transducerCenter,
                            t.SoundRange * DisplayScale, 0, DisplayScale, range,
                            passive: true, pingStrength: 0.5f, needsToBeInSector: t);
                        if (t.IsWithinSector(transducerCenter))
                        {
                            sonarBlips.Add(new SonarBlip(t.WorldPosition, fadeTimer: 1.0f, scale: MathHelper.Clamp(t.SoundRange / 2000, 1.0f, 5.0f)));
                        }
                    }
                }
            }
            prevPassivePingRadius = passivePingRadius;
        }

        public override void CreateGUI()
        {
            isConnectedToSteering = item.GetComponent<Steering>() != null;
            Vector2 size = isConnectedToSteering ? controlBoxSize : new Vector2(0.46f, 0.4f);

            controlContainer = new GUIFrame(new RectTransform(size, GuiFrame.RectTransform, Anchor.BottomLeft), "ItemUI");
            if (!isConnectedToSteering ) //&&!GUI.IsFourByThree()
            {
                controlContainer.RectTransform.MaxSize = new Point((int)(380 * GUI.xScale), (int)(300 * GUI.yScale));
            }
            var paddedControlContainer = new GUIFrame(new RectTransform(controlContainer.Rect.Size - GUIStyle.ItemFrameMargin, controlContainer.RectTransform, Anchor.Center)
            {
                AbsoluteOffset = GUIStyle.ItemFrameOffset
            }, style: null);
            // Based on the height difference to the steering control box so that the elements keep the same size
            float extraHeight = 0.0694f;
            var sonarModeArea = new GUIFrame(new RectTransform(new Vector2(1, 0.4f + extraHeight), paddedControlContainer.RectTransform, Anchor.TopCenter), style: null);
            SonarModeSwitch = new GUIButton(new RectTransform(new Vector2(0.2f, 1), sonarModeArea.RectTransform), string.Empty, style: "SwitchVertical")
            {
                UserData = UIHighlightAction.ElementId.SonarModeSwitch,
                Selected = false,
                Enabled = true,
                ClickSound = GUISoundType.UISwitch,
                OnClicked = (button, data) =>
                {
                    button.Selected = !button.Selected;
                    CurrentMode = button.Selected ? Mode.Active : Mode.Passive;
                    useDirectionalPing = button.Selected;
                    if (GameMain.Client != null)
                    {
                        unsentChanges = true;
                        correctionTimer = CorrectionDelay;
                    }
                    return true;
                }
            };
            var sonarModeRightSide = new GUIFrame(new RectTransform(new Vector2(0.7f, 0.8f), sonarModeArea.RectTransform, Anchor.CenterLeft)
            {
                RelativeOffset = new Vector2(SonarModeSwitch.RectTransform.RelativeSize.X, 0)
            }, style: null);
            passiveTickBox = new GUITickBox(new RectTransform(new Vector2(1, 0.45f), sonarModeRightSide.RectTransform, Anchor.TopLeft),
                TextManager.Get("SonarPassive"), font: GUIStyle.SubHeadingFont, style: "IndicatorLightRedSmall")
            {
                UserData = UIHighlightAction.ElementId.PassiveSonarIndicator,
                ToolTip = TextManager.Get("SonarTipPassive"),
                Selected = true,
                Enabled = false
            };
            activeTickBox = new GUITickBox(new RectTransform(new Vector2(1, 0.45f), sonarModeRightSide.RectTransform, Anchor.BottomLeft),
                TextManager.Get("SonarActive"), font: GUIStyle.SubHeadingFont, style: "IndicatorLightRedSmall")
            {
                UserData = UIHighlightAction.ElementId.ActiveSonarIndicator,
                ToolTip = TextManager.Get("SonarTipActive"),
                Selected = false,
                Enabled = false
            };
            passiveTickBox.TextBlock.OverrideTextColor(GUIStyle.TextColorNormal);
            activeTickBox.TextBlock.OverrideTextColor(GUIStyle.TextColorNormal);

            textBlocksToScaleAndNormalize.Clear();
            textBlocksToScaleAndNormalize.Add(passiveTickBox.TextBlock);
            textBlocksToScaleAndNormalize.Add(activeTickBox.TextBlock);

            lowerAreaFrame = new GUIFrame(new RectTransform(new Vector2(1, 0.4f + extraHeight), paddedControlContainer.RectTransform, Anchor.BottomCenter), style: null);
            var zoomContainer = new GUIFrame(new RectTransform(new Vector2(1, 0.45f), lowerAreaFrame.RectTransform, Anchor.TopCenter), style: null);
            var zoomText = new GUITextBlock(new RectTransform(new Vector2(0.3f, 0.6f), zoomContainer.RectTransform, Anchor.CenterLeft),
                TextManager.Get("SonarZoom"), font: GUIStyle.SubHeadingFont, textAlignment: Alignment.CenterRight);
            textBlocksToScaleAndNormalize.Add(zoomText);
            zoomSlider = new GUIScrollBar(new RectTransform(new Vector2(0.5f, 0.8f), zoomContainer.RectTransform, Anchor.CenterLeft)
            {
                RelativeOffset = new Vector2(0.35f, 0)
            }, barSize: 0.15f, isHorizontal: true, style: "DeviceSlider")
            {
                OnMoved = (scrollbar, scroll) =>
                {
                    zoom = MathHelper.Lerp(MinZoom, MaxZoom, scroll);
                    if (GameMain.Client != null)
                    {
                        unsentChanges = true;
                        correctionTimer = CorrectionDelay;
                    }
                    return true;
                }
            };

            new GUIFrame(new RectTransform(new Vector2(0.8f, 0.01f), paddedControlContainer.RectTransform, Anchor.Center), style: "HorizontalLine")
            { 
                UserData = "horizontalline" 
            };
//------change here to hide the directional sonar button------------//
            var directionalModeFrame = new GUIFrame(new RectTransform(new Vector2(1, 0.45f), lowerAreaFrame.RectTransform, Anchor.BottomCenter), style: null)
            {
                UserData = UIHighlightAction.ElementId.DirectionalSonarFrame
            };
            directionalModeSwitch = new GUIButton(new RectTransform(new Vector2(0.0f, 0.0f), directionalModeFrame.RectTransform, Anchor.CenterLeft), string.Empty, style: "SwitchHorizontal")
            {
                OnClicked = (button, data) =>
                {
                    useDirectionalPing = !useDirectionalPing;
                    button.Selected = useDirectionalPing;
                    if (GameMain.Client != null)
                    {
                        unsentChanges = true;
                        correctionTimer = CorrectionDelay;
                    }
                    return true;
                }
            };
            var directionalModeSwitchText = new GUITextBlock(new RectTransform(new Vector2(0.7f, 1), directionalModeFrame.RectTransform, Anchor.CenterRight),
                TextManager.Get(""), GUIStyle.TextColorNormal, GUIStyle.SubHeadingFont, Alignment.CenterLeft);
            //textBlocksToScaleAndNormalize.Add(directionalModeSwitchText);

            if (HasMineralScanner)
            {
                mineralScannerSwitch = null;//AddMineralScannerSwitchToGUI();
            }
            else
            {
                mineralScannerSwitch = null;
            }
//------change here to hide the directional sonar button------------//
            GuiFrame.CanBeFocused = false;
            
            GUITextBlock.AutoScaleAndNormalize(textBlocksToScaleAndNormalize);

            sonarView = new GUICustomComponent(new RectTransform(Vector2.One * 0.7f, GuiFrame.RectTransform, Anchor.BottomRight, scaleBasis: ScaleBasis.BothHeight),
                (spriteBatch, guiCustomComponent) => { DrawSonar(spriteBatch, guiCustomComponent.Rect); }, null);

            signalWarningText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.25f), sonarView.RectTransform, Anchor.Center, Pivot.BottomCenter),
                "", warningColor, GUIStyle.LargeFont, Alignment.Center);

            // Setup layout for nav terminal
            if (isConnectedToSteering || RightLayout)
            {
                controlContainer.RectTransform.AbsoluteOffset = Point.Zero;
                controlContainer.RectTransform.RelativeOffset = controlBoxOffset;
                controlContainer.RectTransform.SetPosition(Anchor.TopRight);
                sonarView.RectTransform.ScaleBasis = ScaleBasis.Smallest;
                if (HasMineralScanner) { PreventMineralScannerOverlap(); }
                sonarView.RectTransform.SetPosition(Anchor.CenterLeft);
                sonarView.RectTransform.Resize(GUISizeCalculation);
                GUITextBlock.AutoScaleAndNormalize(textBlocksToScaleAndNormalize);
            }
            else if (GUI.RelativeHorizontalAspectRatio > 0.75f)
            {
                sonarView.RectTransform.RelativeOffset = new Vector2(0.13f * GUI.RelativeHorizontalAspectRatio, 0);
                sonarView.RectTransform.SetPosition(Anchor.BottomRight);
            }
            var handle = GuiFrame.GetChild<GUIDragHandle>();
            if (handle != null)
            {
                handle.RectTransform.Parent = controlContainer.RectTransform;
                handle.RectTransform.Resize(Vector2.One);
                handle.RectTransform.SetAsFirstChild();
            }
        }

        private void Ping(Vector2 pingSource, Vector2 transducerPos, float pingRadius, float prevPingRadius, float displayScale, float range, bool passive,
            float pingStrength = 1.0f, AITarget needsToBeInSector = null)
        {
            float prevPingRadiusSqr = prevPingRadius * prevPingRadius;
            float pingRadiusSqr = pingRadius * pingRadius;
                        
            //inside a hull -> only show the edges of the hull
            if (item.CurrentHull != null && DetectSubmarineWalls)
            {
                CreateBlipsForLine(
                    new Vector2(item.CurrentHull.WorldRect.X, item.CurrentHull.WorldRect.Y), 
                    new Vector2(item.CurrentHull.WorldRect.Right, item.CurrentHull.WorldRect.Y), 
                    pingSource, transducerPos,
                    pingRadius, prevPingRadius, 50.0f, 5.0f, range, 2.0f, passive, needsToBeInSector: needsToBeInSector);

                CreateBlipsForLine(
                    new Vector2(item.CurrentHull.WorldRect.X, item.CurrentHull.WorldRect.Y - item.CurrentHull.Rect.Height),
                    new Vector2(item.CurrentHull.WorldRect.Right, item.CurrentHull.WorldRect.Y - item.CurrentHull.Rect.Height),
                    pingSource, transducerPos,
                    pingRadius, prevPingRadius, 50.0f, 5.0f, range, 2.0f, passive, needsToBeInSector: needsToBeInSector);

                CreateBlipsForLine(
                    new Vector2(item.CurrentHull.WorldRect.X, item.CurrentHull.WorldRect.Y),
                    new Vector2(item.CurrentHull.WorldRect.X, item.CurrentHull.WorldRect.Y - item.CurrentHull.Rect.Height),
                    pingSource, transducerPos,
                    pingRadius, prevPingRadius, 50.0f, 5.0f, range, 2.0f, passive, needsToBeInSector: needsToBeInSector);

                CreateBlipsForLine(
                    new Vector2(item.CurrentHull.WorldRect.Right, item.CurrentHull.WorldRect.Y),
                    new Vector2(item.CurrentHull.WorldRect.Right, item.CurrentHull.WorldRect.Y - item.CurrentHull.Rect.Height),
                    pingSource, transducerPos,
                    pingRadius, prevPingRadius, 50.0f, 5.0f, range, 2.0f, passive, needsToBeInSector: needsToBeInSector);

                return;
            }

            foreach (Submarine submarine in Submarine.Loaded)
            {
                if (submarine.HullVertices == null) { continue; }
                if (!DetectSubmarineWalls)
                {
                    if (connectedSubs.Contains(submarine)) { continue; }                    
                }

                //display the actual walls if the ping source is inside the sub (but not inside a hull, that's handled above)
                //only relevant in the end levels or maybe custom subs with some kind of non-hulled parts
                Rectangle worldBorders = submarine.GetDockedBorders();
                worldBorders.Location += submarine.WorldPosition.ToPoint();
                if (Submarine.RectContains(worldBorders, pingSource))
                {
                    CreateBlipsForSubmarineWalls(submarine, pingSource, transducerPos, pingRadius, prevPingRadius, range, passive);
                    continue;
                }

                for (int i = 0; i < submarine.HullVertices.Count; i++)
                {
                    Vector2 start = ConvertUnits.ToDisplayUnits(submarine.HullVertices[i]);
                    Vector2 end = ConvertUnits.ToDisplayUnits(submarine.HullVertices[(i + 1) % submarine.HullVertices.Count]);

                    if (item.Submarine == submarine)
                    {
                        start += Rand.Vector(500.0f);
                        end += Rand.Vector(500.0f);
                    }

                    CreateBlipsForLine(
                        start + submarine.WorldPosition,
                        end + submarine.WorldPosition,
                        pingSource, transducerPos,
                        pingRadius, prevPingRadius,
                        200.0f, 2.0f, range, 1.0f, passive, 
                        needsToBeInSector: needsToBeInSector);
                }
            }

            if (Level.Loaded != null && (item.CurrentHull == null || !DetectSubmarineWalls))
            {
                if (Level.Loaded.Size.Y - pingSource.Y < range)
                {
                    CreateBlipsForLine(
                        new Vector2(pingSource.X - range, Level.Loaded.Size.Y),
                        new Vector2(pingSource.X + range, Level.Loaded.Size.Y),
                        pingSource, transducerPos,
                        pingRadius, prevPingRadius,
                        250.0f, 150.0f, range, pingStrength, passive, 
                        needsToBeInSector: needsToBeInSector);
                }
                if (pingSource.Y - Level.Loaded.BottomPos < range)
                {
                    CreateBlipsForLine(
                        new Vector2(pingSource.X - range, Level.Loaded.BottomPos),
                        new Vector2(pingSource.X + range, Level.Loaded.BottomPos),
                        pingSource, transducerPos,
                        pingRadius, prevPingRadius,
                        250.0f, 150.0f, range, pingStrength, passive, 
                        needsToBeInSector: needsToBeInSector);
                }

                List<Voronoi2.VoronoiCell> cells = Level.Loaded.GetCells(pingSource, 7);
                foreach (Voronoi2.VoronoiCell cell in cells)
                {
                    foreach (Voronoi2.GraphEdge edge in cell.Edges)
                    {
                        if (!edge.IsSolid) { continue; }
                        float cellDot = Vector2.Dot(cell.Center - pingSource, (edge.Center + cell.Translation) - cell.Center);
                        if (cellDot > 0) { continue; }

                        float facingDot = Vector2.Dot(
                            Vector2.Normalize(edge.Point1 - edge.Point2),
                            Vector2.Normalize(cell.Center - pingSource));

                        CreateBlipsForLine(
                            edge.Point1 + cell.Translation,
                            edge.Point2 + cell.Translation,
                            pingSource, transducerPos,
                            pingRadius, prevPingRadius,
                            350.0f, 3.0f * (Math.Abs(facingDot) + 1.0f), range, pingStrength, passive,
                            blipType : cell.IsDestructible ? BlipType.Destructible : BlipType.Default,
                            needsToBeInSector: needsToBeInSector);
                    }
                }
            }

            foreach (Item item in Item.SonarVisibleItems)
            {
                System.Diagnostics.Debug.Assert(item.Prefab.SonarSize > 0.0f);
                if (item.CurrentHull == null)
                {
                    float pointDist = ((item.WorldPosition - pingSource) * displayScale).LengthSquared();
                    if (pointDist > prevPingRadiusSqr && pointDist < pingRadiusSqr)
                    {
                        var blip = new SonarBlip(
                            item.WorldPosition + Rand.Vector(item.Prefab.SonarSize),
                            MathHelper.Clamp(item.Prefab.SonarSize, 0.1f, pingStrength),
                            MathHelper.Clamp(item.Prefab.SonarSize * 0.1f, 0.1f, 10.0f));
                        if (!IsVisible(blip)) { continue; }
                        sonarBlips.Add(blip);
                    }
                }
            }

            foreach (Character c in Character.CharacterList)
            {
                if (c.AnimController.CurrentHull != null || !c.Enabled) { continue; }
                if (!c.IsUnconscious && c.Params.HideInSonar) { continue; }
                if (DetectSubmarineWalls && c.AnimController.CurrentHull == null && item.CurrentHull != null) { continue; }

                if (c.AnimController.SimplePhysicsEnabled)
                {
                    float pointDist = ((c.WorldPosition - pingSource) * displayScale).LengthSquared();
                    if (pointDist > DisplayRadius * DisplayRadius) { continue; }

                    if (pointDist > prevPingRadiusSqr && pointDist < pingRadiusSqr)
                    {
                        var blip = new SonarBlip(
                            c.WorldPosition,
                            MathHelper.Clamp(c.Mass, 0.1f, pingStrength),
                            MathHelper.Clamp(c.Mass * 0.03f, 0.1f, 2.0f));
                        if (!IsVisible(blip)) { continue; }
                        sonarBlips.Add(blip);
                        HintManager.OnSonarSpottedCharacter(Item, c);
                    }
                    continue;
                }

                foreach (Limb limb in c.AnimController.Limbs)
                {
                    if (!limb.body.Enabled) { continue; }

                    float pointDist = ((limb.WorldPosition - pingSource) * displayScale).LengthSquared();
                    if (limb.SimPosition == Vector2.Zero || pointDist > DisplayRadius * DisplayRadius) { continue; }

                    if (pointDist > prevPingRadiusSqr && pointDist < pingRadiusSqr)
                    {
                        var blip = new SonarBlip(
                            limb.WorldPosition + Rand.Vector(limb.Mass / 10.0f),
                            MathHelper.Clamp(limb.Mass, 0.1f, pingStrength),
                            MathHelper.Clamp(limb.Mass * 0.1f, 0.1f, 2.0f));
                        if (!IsVisible(blip)) { continue; }
                        sonarBlips.Add(blip);
                        HintManager.OnSonarSpottedCharacter(Item, c);
                    }
                }
            }

            bool IsVisible(SonarBlip blip)
            {
                if (!passive && !CheckBlipVisibility(blip, transducerPos)) { return false; }
                if (needsToBeInSector != null)
                {
                    if (!needsToBeInSector.IsWithinSector(blip.Position)) { return false; }
                }
                return true;
            }
        }

        private void CreateBlipsForLine(Vector2 point1, Vector2 point2, Vector2 pingSource, Vector2 transducerPos, float pingRadius, float prevPingRadius,
            float lineStep, float zStep, float range, float pingStrength, bool passive, BlipType blipType = BlipType.Default, AITarget needsToBeInSector = null)
        {
            lineStep /= zoom;
            zStep /= zoom;
            range *= DisplayScale;
            float length = (point1 - point2).Length();
            Vector2 lineDir = (point2 - point1) / length;
            for (float x = 0; x < length; x += lineStep * Rand.Range(0.8f, 1.2f))
            {
                if (Rand.Int(sonarBlips.Count) > 500) { continue; }

                Vector2 point = point1 + lineDir * x;

                //ignore if outside the display
                Vector2 transducerDiff = point - transducerPos;
                Vector2 transducerDisplayDiff = transducerDiff * DisplayScale / zoom;
                if (transducerDisplayDiff.LengthSquared() > DisplayRadius * DisplayRadius) { continue; }

                //ignore if the point is not within the ping
                Vector2 pointDiff = point - pingSource;
                Vector2 displayPointDiff = pointDiff * DisplayScale / zoom;
                float displayPointDistSqr = displayPointDiff.LengthSquared();
                if (displayPointDistSqr < prevPingRadius * prevPingRadius || displayPointDistSqr > pingRadius * pingRadius) { continue; }

                //ignore if direction is disrupted
                float transducerDist = transducerDiff.Length();
                Vector2 pingDirection = transducerDiff / transducerDist;
                bool disrupted = false;
                foreach ((Vector2 disruptPos, float disruptStrength) in disruptedDirections)
                {
                    float dot = Vector2.Dot(pingDirection, disruptPos);
                    if (dot >  1.0f - disruptStrength)
                    {
                        disrupted = true;
                        break;
                    }
                }
                if (disrupted) { continue; }

                float displayPointDist = (float)Math.Sqrt(displayPointDistSqr);
                float alpha = pingStrength * Rand.Range(1.5f, 2.0f);
                for (float z = 0; z < DisplayRadius - transducerDist * DisplayScale; z += zStep)
                {
                    Vector2 pos = point + Rand.Vector(150.0f / zoom) + pingDirection * z / DisplayScale;
                    float fadeTimer = alpha * (1.0f - displayPointDist / range);

                    if (needsToBeInSector != null)
                    {
                        if (!needsToBeInSector.IsWithinSector(pos)) { continue; }
                    }

                    var blip = new SonarBlip(pos, fadeTimer, 1.0f + ((displayPointDist + z) / DisplayRadius), blipType);
                    if (!passive && !CheckBlipVisibility(blip, transducerPos)) { continue; }

                    int minDist = (int)(200 / zoom);
                    sonarBlips.RemoveAll(b => b.FadeTimer < fadeTimer && Math.Abs(pos.X - b.Position.X) < minDist && Math.Abs(pos.Y - b.Position.Y) < minDist);

                    sonarBlips.Add(blip);
                    zStep += 0.5f / zoom;

                    if (z == 0)
                    {
                        alpha = Math.Min(alpha - 0.5f, 1.5f);
                    }
                    else
                    {
                        alpha -= 0.1f;
                    }

                    if (alpha < 0) { break; }
                }
            }
        }

        private bool CheckBlipVisibility(SonarBlip blip, Vector2 transducerPos)
        {
            Vector2 pos = (blip.Position - transducerPos) * DisplayScale;
            pos.Y = -pos.Y;

            float posDistSqr = pos.LengthSquared();
            if (posDistSqr > DisplayRadius * DisplayRadius)
            {
                blip.FadeTimer = 0.0f;
                return false;
            }

            Vector2 dir = pos / (float)Math.Sqrt(posDistSqr);
            if (currentPingIndex != -1 && activePings[currentPingIndex].IsDirectional)
            {
                if (Vector2.Dot(activePings[currentPingIndex].Direction, dir) < NewDotProductFireControl)
                {
                    blip.FadeTimer = 0.0f;
                    return false;
                }
            }
            return true;
        }

#endif		
         public override void Update(float deltaTime, Camera cam)
        {
            UpdateOnActiveEffects(deltaTime);

            if (currentMode == Sonar.Mode.Active && useDirectionalPing && Voltage > MinVoltage)
            {
                Terminal terminal = item.GetComponent<Terminal>();

                List<Item> torpedoes = TorpedoTube.torpedoeList;
                foreach (Item item in torpedoes)
                {
                    Vector2 torpedoPosition = item.WorldPosition; // Reference point for distance calculation
                    
                    
                        if (Vector2.DistanceSquared(torpedoPosition, this.item.WorldPosition) > MaxTorpedoDistance * MaxTorpedoDistance || item.Removed)
                        {
                            if (!torpedoesReportedMissing.Contains(item.ID))
                            {
                                terminal.ShowOnDisplay(item.ID.ToString() + ": Signal lost", false, Color.Red, false);
                                torpedoesReportedMissing.Add(item.ID); // Add to the set so the message won't repeat
                            }
                            
                            // Remove torpedo from the list if it's too far away or has been removed
                            //TorpedoTube.torpedoeList.RemoveAt(i);
                            continue;
                        }

                        var pingDirectionValue = (Vector2)pingDirectionField.GetValue(this);
                        if (item != null && item.body != null && pingDirectionValue != null && item.IsContained == false)
                        {
                            // Convert the direction vector to an angle
                            float angle = MathF.Atan2(-pingDirectionValue.Y, pingDirectionValue.X);

                            // Normalize the angle and convert it back to a vector
                            Vector2 normalizedDirection = new Vector2(MathF.Cos(angle), MathF.Sin(angle));

                            // Apply force to the torpedo in the normalized direction
                            item.body.ApplyForce(normalizedDirection * 100, 20); 
                            item.Rotation = angle;
                            //DebugConsole.NewMessage("Applying force in direction: " + normalizedDirection);
                            terminal.ShowOnDisplay(item.ID.ToString() +": " + item.WorldPosition.ToString(), false, Color.Green, false);
                            
                        }
                    
                        
                    
                }
            }

            if (UseTransducers)
            {
                foreach (ConnectedTransducer transducer in connectedTransducers)
                {
                    transducer.DisconnectTimer -= deltaTime;
                }
                connectedTransducers.RemoveAll(t => t.DisconnectTimer <= 0.0f);
            }

            for (var pingIndex = 0; pingIndex < activePingsCount; ++pingIndex)
            {
                activePings[pingIndex].State += deltaTime * PingFrequency;
            }

            if (currentMode == Mode.Active)
            {
				timeSinceLastPing += deltaTime;
				
                if ((Voltage >= MinVoltage) &&
                    (!UseTransducers || connectedTransducers.Count > 0))
                {
                    if (currentPingIndex != -1)
                    {
                        var activePing = activePings[currentPingIndex];
                        if (activePing.State > 1.0f)
                        {
                            aiPingCheckPending = true;
                            currentPingIndex = -1;
                        }
                    }
                    if (currentPingIndex == -1 && activePingsCount < activePings.Length && timeSinceLastPing >= 1f)
                    {
						timeSinceLastPing = 0.0f;//reset
                        currentPingIndex = activePingsCount++;
                        if (activePings[currentPingIndex] == null)
                        {
                            activePings[currentPingIndex] = new ActivePing();
                        }
                        activePings[currentPingIndex].IsDirectional = useDirectionalPing;
                        activePings[currentPingIndex].Direction = pingDirection;
                        activePings[currentPingIndex].State = 0.0f;
                        activePings[currentPingIndex].PrevPingRadius = 0.0f;
                        if (item.AiTarget != null)
                        {
                            item.AiTarget.SectorDegrees = useDirectionalPing ? DirectionalPingSector : 360.0f;
                            item.AiTarget.SectorDir = new Vector2(pingDirection.X, -pingDirection.Y);
                        }
                        item.Use(deltaTime);
                    }
                }
                else
                {
                    aiPingCheckPending = false;
                }
            }

            for (var pingIndex = 0; pingIndex < activePingsCount;)
            {
                if (item.AiTarget != null)
                {
                    float range = MathUtils.InverseLerp(item.AiTarget.MinSoundRange, item.AiTarget.MaxSoundRange, Range * activePings[pingIndex].State / zoom);
                    item.AiTarget.SoundRange = Math.Max(item.AiTarget.SoundRange, MathHelper.Lerp(item.AiTarget.MinSoundRange, item.AiTarget.MaxSoundRange, range));
                }
                if (activePings[pingIndex].State > 1.0f)
                {
                    var lastIndex = --activePingsCount;
                    var oldActivePing = activePings[pingIndex];
                    activePings[pingIndex] = activePings[lastIndex];
                    activePings[lastIndex] = oldActivePing;
                    if (currentPingIndex == lastIndex)
                    {
                        currentPingIndex = pingIndex;
                    }
                }
                else
                {
                    ++pingIndex;
                }
            }
        }


        // Override other methods as needed
    }

}