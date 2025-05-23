﻿using Barotrauma.Extensions;
using Barotrauma.Networking;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Items.Components;
using Barotrauma;

namespace BarotraumaDieHard
{
    partial class HandHeldSonar : Powered, IServerSerializable, IClientSerializable
    {
        public enum BlipTypeDieHard
        {
            Default,
            Disruption,
            Destructible,
            Door,
            LongRange
        }

        private PathFinder pathFinder;

        private readonly bool dynamicDockingIndicator = true;

        private bool unsentChanges;
        private float networkUpdateTimer;

        public GUIButton SonarModeSwitch { get; private set; }
        private GUITickBox activeTickBox, passiveTickBox;
        private GUITextBlock signalWarningText;

        private GUIFrame lowerAreaFrame;

        private GUIScrollBar zoomSlider;

        private GUIButton directionalModeSwitch;
        private Vector2? pingDragDirection = null;

        /// <summary>
        /// Can be null if the property HasMineralScanner is false
        /// </summary>
        

        private Vector2 holderPosition;
        private GUIFrame controlContainer;

        private Vector2 holdingDirection;

        private GUICustomComponent sonarView;

        private Sprite directionalPingBackground;
        private Sprite[] directionalPingButton;

        private Sprite pingCircle, directionalPingCircle;
        private Sprite screenOverlay, screenBackground;

        private Sprite sonarBlip;
        private Sprite lineSprite;

        private readonly Dictionary<Identifier, Tuple<Sprite, Color>> targetIcons = new Dictionary<Identifier, Tuple<Sprite, Color>>();

        private float displayBorderSize;

        private List<SonarBlipDieHard> sonarBlips;

        private float prevPassivePingRadius;

        private Vector2 center;

        /// <summary>
        /// Current scale of the display, taking zoom into account. In other words, the scaling factor of world coordinates to coordinates on the display.
        /// </summary>
        public float DisplayScale
        {
            get;
            private set;
        } = 1.0f;

        private const float DisruptionUpdateInterval = 0.2f;
        private float disruptionUpdateTimer;

        private const float LongRangeUpdateInterval = 10.0f;
        private float longRangeUpdateTimer;

        private float showDirectionalIndicatorTimer;

        private readonly List<LevelObject> nearbyObjects = new List<LevelObject>();
        private const float NearbyObjectUpdateInterval = 1.0f;
        float nearbyObjectUpdateTimer;

        private readonly List<Submarine> connectedSubs = new List<Submarine>();
        private const float ConnectedSubUpdateInterval = 1.0f;
        float connectedSubUpdateTimer;

        private readonly List<(Vector2 pos, float strength)> disruptedDirections = new List<(Vector2 pos, float strength)>();

        private readonly Dictionary<object, CachedDistance> markerDistances = new Dictionary<object, CachedDistance>();

        private readonly Color positiveColor = Color.Green;
        private readonly Color warningColor = Color.Orange;
        private readonly Color negativeColor = Color.Red;
        private readonly Color markerColor = Color.Red;

        public static readonly Vector2 controlBoxSize = new Vector2(0.33f, 0.32f);
        public static readonly Vector2 controlBoxOffset = new Vector2(0.025f, 0);
        private static readonly float sonarAreaSize = 1.09f;

        private static readonly Dictionary<BlipTypeDieHard, Color[]> blipColorGradient = new Dictionary<BlipTypeDieHard, Color[]>()
        {
            {
                BlipTypeDieHard.Default,
                new Color[] { Color.TransparentBlack, new Color(0, 50, 160), new Color(0, 133, 166), new Color(2, 159, 30), new Color(255, 255, 255) }
            },
            {
                BlipTypeDieHard.Disruption,
                new Color[] { Color.TransparentBlack, new Color(254, 68, 19), new Color(255, 220, 62), new Color(255, 255, 255) }
            },
            {
                BlipTypeDieHard.Destructible,
                new Color[] { Color.TransparentBlack, new Color(94, 114, 73) * 0.8f, new Color(255, 236, 151) * 0.8f, new Color(242, 243, 194) * 0.8f }
            },
            {
                BlipTypeDieHard.Door,
                new Color[] { Color.TransparentBlack, new Color(73, 78, 86), new Color(66, 94, 100), new Color(47, 115, 58), new Color(255, 255, 255) }
            },
            {
                BlipTypeDieHard.LongRange,
                new Color[] { Color.TransparentBlack, Color.TransparentBlack, new Color(254, 68, 19) * 0.8f, Color.TransparentBlack }
            }
        };

        private float prevDockingDist;

        public Vector2 DisplayOffset { get; private set; }

        public float DisplayRadius { get; private set; }

        public static Vector2 GUISizeCalculation => Vector2.One * Math.Min(GUI.RelativeHorizontalAspectRatio, 1f) * sonarAreaSize;

        private List<(Vector2 center, List<Item> resources)> MineralClusters { get; set; }

        private readonly List<GUITextBlock> textBlocksToScaleAndNormalize = new List<GUITextBlock>();

        private bool isConnectedToSteering;

        private static LocalizedString caveLabel;
        private static LocalizedString enemyLabel;


        [Serialize(false, IsPropertySaveable.Yes)]
        public bool RightLayout
        {
            get;
            set;
        }

        public override bool RecreateGUIOnResolutionChange => true;

        partial void InitProjSpecific(ContentXElement element)
        {
            System.Diagnostics.Debug.Assert(Enum.GetValues(typeof(BlipTypeDieHard)).Cast<BlipTypeDieHard>().All(t => blipColorGradient.ContainsKey(t)));
            sonarBlips = new List<SonarBlipDieHard>();

            caveLabel =
                TextManager.Get("cave").Fallback( 
                TextManager.Get("missiontype.nest"));

            enemyLabel = TextManager.Get("enemysubmarine");

            foreach (var subElement in element.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    case "pingcircle":
                        pingCircle = new Sprite(subElement);
                        break;
                    case "directionalpingcircle":
                        directionalPingCircle = new Sprite(subElement);
                        break;
                    case "directionalpingbackground":
                        directionalPingBackground = new Sprite(subElement);
                        break;
                    case "directionalpingbutton":
                        if (directionalPingButton == null) { directionalPingButton = new Sprite[3]; }
                        int index = subElement.GetAttributeInt("index", 0);
                        directionalPingButton[index] = new Sprite(subElement);
                        break;
                    case "screenoverlay":
                        screenOverlay = new Sprite(subElement);
                        break;
                    case "screenbackground":
                        screenBackground = new Sprite(subElement);
                        break;
                    case "blip":
                        sonarBlip = new Sprite(subElement);
                        break;
                    case "linesprite":
                        lineSprite = new Sprite(subElement);
                        break;
                    case "icon":
                        var targetIconSprite = new Sprite(subElement);
                        var color = subElement.GetAttributeColor("color", Color.White);
                        targetIcons.Add(subElement.GetAttributeIdentifier("identifier", Identifier.Empty),
                            new Tuple<Sprite, Color>(targetIconSprite, color));
                        break;
                }
            }
            CreateGUI();
        }

        public override void OnResolutionChanged()
        {
            UpdateGUIElements();
        }

        public override void CreateGUI()
        {
            isConnectedToSteering = item.GetComponent<Steering>() != null;
            Vector2 size = isConnectedToSteering ? controlBoxSize : new Vector2(0.46f, 0.4f);

            controlContainer = new GUIFrame(new RectTransform(size, GuiFrame.RectTransform, Anchor.BottomLeft), "ItemUI");
            if (!isConnectedToSteering ) //&& !GUI.IsFourByThree()
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
                    // This cause null object error in server during the process.
                    /*if (GameMain.Client != null)
                    {
                        unsentChanges = true;
                        correctionTimer = CorrectionDelay;
                    }*/
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

            var directionalModeFrame = new GUIFrame(new RectTransform(new Vector2(1, 0.45f), lowerAreaFrame.RectTransform, Anchor.BottomCenter), style: null)
            {
                UserData = UIHighlightAction.ElementId.DirectionalSonarFrame
            };
            

            GuiFrame.CanBeFocused = false;
            
            GUITextBlock.AutoScaleAndNormalize(textBlocksToScaleAndNormalize);

            sonarView = new GUICustomComponent(new RectTransform(Vector2.One * 0.7f, GuiFrame.RectTransform, Anchor.BottomRight, scaleBasis: ScaleBasis.BothHeight),
                (spriteBatch, guiCustomComponent) => { DrawSonarHandHeldSonar(spriteBatch, guiCustomComponent.Rect); }, null);

            signalWarningText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.25f), sonarView.RectTransform, Anchor.Center, Pivot.BottomCenter),
                "", warningColor, GUIStyle.LargeFont, Alignment.Center);

            // Setup layout for nav terminal
            if (isConnectedToSteering || RightLayout)
            {
                controlContainer.RectTransform.AbsoluteOffset = Point.Zero;
                controlContainer.RectTransform.RelativeOffset = controlBoxOffset;
                controlContainer.RectTransform.SetPosition(Anchor.TopRight);
                sonarView.RectTransform.ScaleBasis = ScaleBasis.Smallest;
                
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

       

        private Vector2 GetTransducerPos()
        {
            
                //use the position of the sub if the item is static (no body) and inside a sub
                return item.Submarine != null && item.body == null ? item.Submarine.WorldPosition : item.WorldPosition;
           
        }


        public override void OnItemLoaded()
        {
            base.OnItemLoaded();
            zoomSlider.BarScroll = MathUtils.InverseLerp(MinZoom, MaxZoom, zoom);
            
                
                GUITextBlock.AutoScaleAndNormalize(textBlocksToScaleAndNormalize);
            
            //make the sonarView customcomponent render the steering view so it gets drawn in front of the sonar
            item.GetComponent<Steering>()?.AttachToSonarHUD(sonarView);
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
                
                if (item.Submarine != null)
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

            if ( Level.Loaded != null && !Level.Loaded.Generating)
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
                        var flowBlip = new SonarBlipDieHard(blipPos, Rand.Range(0.5f, 1.0f), 1.0f)
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
                    SonarBlipDieHard sporeBlip = new SonarBlipDieHard(blipPos, Rand.Range(0.1f, 0.5f), 0.5f)
                    {
                        Rotation = Rand.Range(-MathHelper.TwoPi, MathHelper.TwoPi),
                        BlipType = BlipTypeDieHard.Default,
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
                    var flowBlip = new SonarBlipDieHard(blipPos, Rand.Range(0.5f, 1.0f), 1.0f)
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
                // function removed. Set direction in the ping update
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
                PingHandHeldSonar(transducerCenter, transducerCenter,
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
                            var longRangeBlip = new SonarBlipDieHard(transducerCenter + blipDir * Range * 0.9f, Rand.Range(1.9f, 2.1f), Rand.Range(1.0f, 1.5f), BlipTypeDieHard.LongRange)
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
                        PingHandHeldSonar(t.WorldPosition, transducerCenter,
                            t.SoundRange * DisplayScale, 0, DisplayScale, range,
                            passive: true, pingStrength: 0.5f, needsToBeInSector: t);
                        if (t.IsWithinSector(transducerCenter))
                        {
                            sonarBlips.Add(new SonarBlipDieHard(t.WorldPosition, fadeTimer: 1.0f, scale: MathHelper.Clamp(t.SoundRange / 2000, 1.0f, 5.0f)));
                        }
                    }
                }
            }
            prevPassivePingRadius = passivePingRadius;
        }
        
        private void DrawSonarHandHeldSonar(SpriteBatch spriteBatch, Rectangle rect)
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
                    
                    directionalPingButton[buttonSprIndex]?.Draw(spriteBatch, center, MathUtils.VectorToAngle(pingDirection), rect.Width / directionalPingBackground.size.X);
                }

                if (this.Item.ParentInventory != null)
                {
                    holderPosition = this.Item.ParentInventory.Owner.WorldPosition;
                    
                }
                
                // Calculate the angle in radians between character and transducer
                float angleRadians = (float)Math.Atan2(this.Item.WorldPosition.Y - holderPosition.Y, this.Item.WorldPosition.X - holderPosition.X);

                // Convert the angle to a directional vector using the angle in radians
                holdingDirection = new Vector2((float)Math.Cos(angleRadians), (float)Math.Sin(angleRadians));

                    // Calculate the angle in degrees based on holdingDirection
                    float holdingAngleDegrees = MathHelper.ToDegrees((float)Math.Atan2(-holdingDirection.Y, holdingDirection.X));
                    holdingAngleDegrees = (holdingAngleDegrees + 360) % 360; // Normalize angle to be within 0-360 degrees

                    // Convert the angle back to radians
                    float holdingAngleRadians = MathHelper.ToRadians(holdingAngleDegrees);

                    // Set the sonar ping direction based on the holding angle
                    Vector2 sonarDirection = new Vector2((float)Math.Cos(holdingAngleRadians), (float)Math.Sin(holdingAngleRadians));
                    this.pingDirection = sonarDirection;
                }

            if (currentPingIndex != -1)
            {
                var activePing = activePings[currentPingIndex];
                if (activePing.IsDirectional && directionalPingCircle != null)
                {
                    directionalPingCircle.Draw(spriteBatch, center, Color.White * (1.0f - activePing.State),
                        rotate: MathUtils.VectorToAngle(activePing.Direction),
                        scale: DisplayRadius / directionalPingCircle.size.X * activePing.State);
                }
                else
                {
                    pingCircle.Draw(spriteBatch, center, Color.White * (1.0f - activePing.State), 0.0f, (DisplayRadius * 2 / pingCircle.size.X) * activePing.State);
                }
            }

            float signalStrength = 1.0f;

            if (!this.Item.InWater)
            signalStrength = 0.2f;
            

            Vector2 transducerCenter = GetTransducerPos();// + DisplayOffset;

            if (sonarBlips.Count > 0)
            {
                float blipScale = 0.08f * (float)Math.Sqrt(zoom) * (rect.Width / 700.0f);
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive);

                foreach (SonarBlipDieHard sonarBlip in sonarBlips)
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
                Vector2 sector1 = MathUtils.RotatePointAroundTarget(pingDirection * DisplayRadius, Vector2.Zero, MathHelper.ToRadians(DirectionalPingSector * 0.5f));
                Vector2 sector2 = MathUtils.RotatePointAroundTarget(pingDirection * DisplayRadius, Vector2.Zero, MathHelper.ToRadians(-DirectionalPingSector * 0.5f));
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

                if (GameMain.NetworkMember is { } networkMember && GameMain.GameSession?.GameMode is PvPMode)
                {
                    if (networkMember.ServerSettings.TrackOpponentInPvP
                        && Submarine.MainSubs[0] is { } coalitionSub
                        && Submarine.MainSubs[1] is { } separatistSub
                        && Character.Controlled is { } player)
                    {
                        Submarine whichSubToDraw = player.TeamID switch
                        {
                            CharacterTeamType.Team1 => separatistSub,
                            CharacterTeamType.Team2 => coalitionSub,
                            _                       => null
                        };

                        if (whichSubToDraw != null)
                        {
                            DrawOffsetMarker(spriteBatch,
                                       enemyLabel.Value,
                                       Tags.Submarine,
                                       Tags.Enemy,
                                       whichSubToDraw.WorldPosition,
                                       transducerCenter,
                                       distanceThresholds: new Range<float>(start: MetersToUnits(150), end: MetersToUnits(1600)),
                                       offset: new Range<float>(start: MetersToUnits(100), end: MetersToUnits(400)),
                                       minOffset: MetersToUnits(10));

                            static float MetersToUnits(float m)
                                => m / Physics.DisplayToRealWorldRatio;
                        }
                    }
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

            if ( UseMineralScanner && CurrentMode == Mode.Active && MineralClusters != null &&
                (item.CurrentHull == null || !DetectSubmarineWalls) &&
                HasPower)
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
                if (sub.IsAboveLevel) { continue; }

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

        private void DrawOwnSubmarineBorders(SpriteBatch spriteBatch, Vector2 transducerCenter, float signalStrength)
        {
            float simScale = DisplayScale * Physics.DisplayToSimRation;

            foreach (Submarine submarine in Submarine.Loaded)
            {
                if (!connectedSubs.Contains(submarine)) { continue; }
                if (submarine.HullVertices == null) { continue; }

                Vector2 offset = ConvertUnits.ToSimUnits(submarine.WorldPosition - transducerCenter);

                for (int i = 0; i < submarine.HullVertices.Count; i++)
                {
                    Vector2 start = (submarine.HullVertices[i] + offset) * simScale;
                    start.Y = -start.Y;
                    Vector2 end = (submarine.HullVertices[(i + 1) % submarine.HullVertices.Count] + offset) * simScale;
                    end.Y = -end.Y;

                    DrawLine(spriteBatch, start, end, Color.LightBlue * signalStrength * 0.5f, width: 4);
                }
            }
        }

        private void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, int width)
        {
            bool startOutside = start.LengthSquared() > DisplayRadius * DisplayRadius;
            bool endOutside = end.LengthSquared() > DisplayRadius * DisplayRadius;
            if (startOutside && endOutside)
            {
                return;
            }
            else if (startOutside)
            {
                if (MathUtils.GetLineCircleIntersections(Vector2.Zero, DisplayRadius, end, start, true, out Vector2? intersection1, out _) == 1)
                {
                    DrawLineSprite(spriteBatch, center + intersection1.Value, center + end, color, width: width);
                }
            }
            else if (endOutside)
            {
                if (MathUtils.GetLineCircleIntersections(Vector2.Zero, DisplayRadius, start, end, true, out Vector2? intersection1, out _) == 1)
                {
                    DrawLineSprite(spriteBatch, center + start, center + intersection1.Value, color, width: width);
                }
            }
            else
            {
                DrawLineSprite(spriteBatch, center + start, center + end, color, width: width);
            }
        }

        private void DrawLineSprite(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, int width)
        {
            if (lineSprite == null)
            {
                GUI.DrawLine(spriteBatch, start, end, color, width: width);
            }
            else
            {
                Vector2 dir = end - start;
                float angle = (float)Math.Atan2(dir.Y, dir.X);
                lineSprite.Draw(spriteBatch, start, color, origin: lineSprite.Origin, rotate: angle,
                    scale: new Vector2(dir.Length() / lineSprite.size.X, 1.0f));
            }
        }


        private void DrawDockingPorts(SpriteBatch spriteBatch, Vector2 transducerCenter, float signalStrength)
        {
            float scale = DisplayScale;

            Steering steering = item.GetComponent<Steering>();
            

            foreach (DockingPort dockingPort in DockingPort.List)
            {
                if (dockingPort.Item.Submarine.IsAboveLevel) { continue; }
                if (dockingPort.Item.IsHidden) { continue; }
                if (dockingPort.Item.Submarine == null) { continue; }
                if (dockingPort.Item.Submarine.Info.IsWreck) { continue; }
                // docking ports should be shown even if defined as not, if the submarine is the same as the sonar's
                if (!dockingPort.Item.Submarine.ShowSonarMarker && dockingPort.Item.Submarine != item.Submarine && 
                    !dockingPort.Item.Submarine.Info.IsOutpost && !dockingPort.Item.Submarine.Info.IsBeacon) 
                { 
                    continue; 
                }

                //don't show the docking ports of the opposing team on the sonar
                if (item.Submarine != null && 
                    !item.Submarine.IsRespawnShuttle &&
                    !dockingPort.Item.Submarine.IsRespawnShuttle &&
                    !dockingPort.Item.Submarine.Info.IsOutpost &&
                    !dockingPort.Item.Submarine.Info.IsBeacon)
                {
                    // specifically checking for friendlyNPC seems more logical here
                    if (dockingPort.Item.Submarine.TeamID != item.Submarine.TeamID && dockingPort.Item.Submarine.TeamID != CharacterTeamType.FriendlyNPC) { continue; } 
                }

                Vector2 offset = (dockingPort.Item.WorldPosition - transducerCenter) * scale;
                offset.Y = -offset.Y;
                if (offset.LengthSquared() > DisplayRadius * DisplayRadius) { continue; }
                Vector2 size = dockingPort.Item.Rect.Size.ToVector2() * scale;

                if (dockingPort.IsHorizontal)
                {
                    size.X = 0.0f;
                }
                else
                {
                    size.Y = 0.0f;
                }
                GUI.DrawLine(spriteBatch, center + offset - size - Vector2.Normalize(size) * zoom, center + offset + size + Vector2.Normalize(size) * zoom, Color.Black * signalStrength * 0.5f, width: (int)(zoom * 5.0f));
                GUI.DrawLine(spriteBatch, center + offset - size, center + offset + size, positiveColor * signalStrength, width: (int)(zoom * 2.5f));
            }
        }

        

        private void UpdateDisruptions(Vector2 pingSource, float worldPingRadius)
        {
            float worldPingRadiusSqr = worldPingRadius * worldPingRadius;

            disruptedDirections.Clear();

            for (var pingIndex = 0; pingIndex < activePingsCount; ++pingIndex)
            {
                foreach (LevelObject levelObject in nearbyObjects)
                {
                    if (levelObject.ActivePrefab?.SonarDisruption <= 0.0f) { continue; }

                    float disruptionStrength = levelObject.ActivePrefab.SonarDisruption;
                    Vector2 disruptionPos = new Vector2(levelObject.Position.X, levelObject.Position.Y);

                    float disruptionDist = Vector2.Distance(pingSource, disruptionPos);
                    disruptedDirections.Add(((disruptionPos - pingSource) / disruptionDist, disruptionStrength));

                    CreateBlipsForDisruption(disruptionPos, disruptionStrength);
                    
                }
                foreach (AITarget aiTarget in AITarget.List)
                {
                    float disruption = aiTarget.Entity is Character c && !c.IsUnconscious ? c.Params.SonarDisruption : aiTarget.SonarDisruption;
                    if (disruption <= 0.0f || aiTarget.InDetectable) { continue; }
                    float distSqr = Vector2.DistanceSquared(aiTarget.WorldPosition, pingSource);
                    if (distSqr > worldPingRadiusSqr) { continue; }
                    float disruptionDist = (float)Math.Sqrt(distSqr);
                    disruptedDirections.Add(((aiTarget.WorldPosition - pingSource) / disruptionDist, aiTarget.SonarDisruption));
                    CreateBlipsForDisruption(aiTarget.WorldPosition, disruption);
                }
            }

            void CreateBlipsForDisruption(Vector2 disruptionPos, float disruptionStrength)
            {
                disruptionStrength = Math.Min(disruptionStrength, 10.0f);
                Vector2 dir = disruptionPos - pingSource;
                for (int i = 0; i < disruptionStrength * 10.0f; i++)
                {
                    Vector2 pos = disruptionPos + Rand.Vector(Rand.Range(0.0f, Level.GridCellSize * 4 * disruptionStrength));
                    if (Vector2.Dot(pos - pingSource, -dir) > 1.0f - disruptionStrength) { continue; }
                    var blip = new SonarBlipDieHard(
                        pos, 
                        MathHelper.Lerp(0.1f, 1.5f, Math.Min(disruptionStrength, 1.0f)), 
                        Rand.Range(0.2f, 1.0f + disruptionStrength),
                        BlipTypeDieHard.Disruption);
                    sonarBlips.Add(blip);
                }
            }
        }

        public void RegisterExplosion(Explosion explosion, Vector2 worldPosition)
        {
            if (Character.Controlled?.SelectedItem != item) { return; }
            if (explosion.Attack.StructureDamage <= 0 && explosion.Attack.ItemDamage <= 0 && explosion.EmpStrength <= 0) { return; }
            Vector2 transducerCenter = GetTransducerPos();
            if (Vector2.DistanceSquared(worldPosition, transducerCenter) > range * range) { return; }
            int blipCount = MathHelper.Clamp((int)(explosion.Attack.Range / 100.0f), 0, 50);
            for (int i = 0; i < blipCount; i++)
            {
                sonarBlips.Add(new SonarBlipDieHard(
                    worldPosition + Rand.Vector(Rand.Range(0.0f, explosion.Attack.Range)),
                    1.0f,
                    Rand.Range(0.5f, 1.0f),
                    BlipTypeDieHard.Disruption));
            }
            if (explosion.EmpStrength > 0.0f)
            {
                int empBlipCount = MathHelper.Clamp((int)(blipCount * explosion.EmpStrength), 10, 50);
                for (int i = 0; i < empBlipCount; i++)
                {
                    Vector2 dir = Rand.Vector(1.0f);
                    var longRangeBlip = new SonarBlipDieHard(worldPosition, Rand.Range(1.9f, 2.1f), Rand.Range(1.0f, 1.5f), BlipTypeDieHard.LongRange)
                    {
                        Velocity = dir * MathUtils.Round(Rand.Range(4000.0f, 6000.0f), 1000.0f),
                        Rotation = (float)Math.Atan2(-dir.Y, dir.X)
                    };
                    longRangeBlip.Size.Y *= 4.0f;
                    sonarBlips.Add(longRangeBlip);
                }
            }
        }

        private void PingHandHeldSonar(Vector2 pingSource, Vector2 transducerPos, float pingRadius, float prevPingRadius, float displayScale, float range, bool passive,
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
                if (Submarine.RectContains(worldBorders, pingSource) || submarine.Info.OutpostGenerationParams is { AlwaysShowStructuresOnSonar: true })
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
                        
                        //the normal of the edge must be pointing towards the ping source to be visible
                        float cellDot = Vector2.Dot((edge.Center + cell.Translation) - pingSource, edge.GetNormal(cell));
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
                            blipType : cell.IsDestructible ? BlipTypeDieHard.Destructible : BlipTypeDieHard.Default,
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
                        var blip = new SonarBlipDieHard(
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
                if (c.InDetectable) { continue; }
                if (DetectSubmarineWalls && c.AnimController.CurrentHull == null && item.CurrentHull != null) { continue; }

                if (c.AnimController.SimplePhysicsEnabled)
                {
                    float pointDist = ((c.WorldPosition - pingSource) * displayScale).LengthSquared();
                    if (pointDist > DisplayRadius * DisplayRadius) { continue; }

                    if (pointDist > prevPingRadiusSqr && pointDist < pingRadiusSqr)
                    {
                        var blip = new SonarBlipDieHard(
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
                        var blip = new SonarBlipDieHard(
                            limb.WorldPosition + Rand.Vector(limb.Mass / 10.0f),
                            MathHelper.Clamp(limb.Mass, 0.1f, pingStrength),
                            MathHelper.Clamp(limb.Mass * 0.1f, 0.1f, 2.0f));
                        if (!IsVisible(blip)) { continue; }
                        sonarBlips.Add(blip);
                        HintManager.OnSonarSpottedCharacter(Item, c);
                    }
                }
            }

            bool IsVisible(SonarBlipDieHard blip)
            {
                if (!passive && !CheckBlipVisibilityHandHeldSonar(blip, transducerPos)) { return false; }
                if (needsToBeInSector != null)
                {
                    if (!needsToBeInSector.IsWithinSector(blip.Position)) { return false; }
                }
                return true;
            }
        }

        private void CreateBlipsForLine(Vector2 point1, Vector2 point2, Vector2 pingSource, Vector2 transducerPos, float pingRadius, float prevPingRadius,
            float lineStep, float zStep, float range, float pingStrength, bool passive, BlipTypeDieHard blipType = BlipTypeDieHard.Default, AITarget needsToBeInSector = null)
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

                    var blip = new SonarBlipDieHard(pos, fadeTimer, 1.0f + ((displayPointDist + z) / DisplayRadius), blipType);
                    if (!passive && !CheckBlipVisibilityHandHeldSonar(blip, transducerPos)) { continue; }

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

        private void CreateBlipsForSubmarineWalls(Submarine sub, Vector2 pingSource, Vector2 transducerPos, float pingRadius, float prevPingRadius, float range, bool passive)
        {
            foreach (Structure structure in Structure.WallList)
            {
                if (structure.Submarine != sub) { continue; }
                CreateBlips(structure.IsHorizontal, structure.WorldPosition, structure.WorldRect);
            }
            foreach (var door in Door.DoorList)
            {
                if (door.Item.Submarine != sub || door.IsOpen) { continue; }
                CreateBlips(door.IsHorizontal, door.Item.WorldPosition, door.Item.WorldRect, BlipTypeDieHard.Door);
            }

            void CreateBlips(bool isHorizontal, Vector2 worldPos, Rectangle worldRect, BlipTypeDieHard blipType = BlipTypeDieHard.Default)
            {
                Vector2 point1, point2;
                if (isHorizontal)
                {
                    point1 = new Vector2(worldRect.X, worldPos.Y);
                    point2 = new Vector2(worldRect.Right, worldPos.Y);
                }
                else
                {
                    point1 = new Vector2(worldPos.X, worldRect.Y);
                    point2 = new Vector2(worldPos.X, worldRect.Y - worldRect.Height);
                }
                CreateBlipsForLine(
                    point1,
                    point2,
                    pingSource, transducerPos,
                    pingRadius, prevPingRadius, 50.0f, 5.0f, range, 2.0f, passive, blipType);
            }
        }

        private bool CheckBlipVisibilityHandHeldSonar(SonarBlipDieHard blip, Vector2 transducerPos)
        {
            Vector2 pos = (blip.Position - transducerPos) * DisplayScale;
            //pos.Y = -pos.Y; no need to invert

            float posDistSqr = pos.LengthSquared();
            if (posDistSqr > DisplayRadius * DisplayRadius)
            {
                blip.FadeTimer = 0.0f;
                return false;
            }

            Vector2 dir = pos / (float)Math.Sqrt(posDistSqr);

            // Use the holding direction to check if the blip is within the visible range
            if (currentPingIndex != -1 && activePings[currentPingIndex].IsDirectional)
            {
                if (Vector2.Dot(holdingDirection, dir) < DirectionalPingDotProduct)
                {
                    blip.FadeTimer = 0.0f;
                    return false;
                }
            }
            return true;
        }



        /// <summary>
        /// Based largely on existing CheckBlipVisibility() code
        /// </summary>
        private bool CheckResourceMarkerVisibility(Vector2 resourcePos, Vector2 transducerPos)
        {
            var distSquared = Vector2.DistanceSquared(transducerPos, resourcePos);
            if (distSquared > Range * Range)
            {
                return false;
            }
            if (currentPingIndex != -1 && activePings[currentPingIndex].IsDirectional)
            {
                var pos = (resourcePos - transducerPos) * DisplayScale;
                pos.Y = -pos.Y;
                var length = pos.Length();
                var dir = pos / length;
                if (Vector2.Dot(activePings[currentPingIndex].Direction, dir) < DirectionalPingDotProduct)
                {
                    return false;
                }
            }
            return true;
        }

        private void DrawBlip(SpriteBatch spriteBatch, SonarBlipDieHard blip, Vector2 transducerPos, Vector2 center, float strength, float blipScale)
        {
            strength = MathHelper.Clamp(strength, 0.0f, 1.0f);
            
            float distort = 1.0f - item.Condition / item.MaxCondition;
            
            Vector2 pos = (blip.Position - transducerPos) * DisplayScale;
            pos.Y = -pos.Y;

            if (Rand.Range(0.5f, 2.0f) < distort) { pos.X = -pos.X; }
            if (Rand.Range(0.5f, 2.0f) < distort) { pos.Y = -pos.Y; }

            float posDistSqr = pos.LengthSquared();
            if (posDistSqr > DisplayRadius * DisplayRadius)
            {
                blip.FadeTimer = 0.0f;
                return;
            }
            
            if (sonarBlip == null)
            {
                GUI.DrawRectangle(spriteBatch, center + pos, Vector2.One * 4, Color.Magenta, true);
                return;
            }

            Vector2 dir = pos / (float)Math.Sqrt(posDistSqr);
            Vector2 normal = new Vector2(dir.Y, -dir.X);
            float scale = (strength + 3.0f) * blip.Scale * blipScale;
            Color color = ToolBox.GradientLerp(strength, blipColorGradient[blip.BlipType]);

            sonarBlip.Draw(spriteBatch, center + pos, color * blip.Alpha, sonarBlip.Origin, blip.Rotation ?? MathUtils.VectorToAngle(pos),
                blip.Size * scale * 0.5f, SpriteEffects.None, 0);

            pos += Rand.Range(0.0f, 1.0f) * dir + Rand.Range(-scale, scale) * normal;

            sonarBlip.Draw(spriteBatch, center + pos, color * 0.5f * blip.Alpha, sonarBlip.Origin, 0, scale, SpriteEffects.None, 0);
        }

        /// <summary>
        /// Used in DrawOffsetMarker to cache the randomized location of the marker
        /// </summary>
        private readonly Dictionary<Identifier, CachedLocation> cachedLocations = new Dictionary<Identifier, CachedLocation>();

        private void DrawOffsetMarker(SpriteBatch spriteBatch, string label, Identifier iconIdentifier, Identifier targetIdentifier, Vector2 worldPosition, Vector2 transducerPosition, Range<float> distanceThresholds, Range<float> offset, float minOffset)
        {
            Vector2 pos;

            if (!cachedLocations.TryGetValue(targetIdentifier, out CachedLocation cachedLocation))
            {
                cachedLocation = CreateCachedLocation();
                cachedLocations.Add(targetIdentifier, cachedLocation);
                pos = cachedLocation.Location;
            }
            else
            {
                if (Timing.TotalTime > cachedLocation.RecalculationTime)
                {
                    cachedLocation = CreateCachedLocation();
                    cachedLocations[targetIdentifier] = cachedLocation;
                }

                pos = cachedLocation.Location;
            }

            DrawMarker(spriteBatch, label, iconIdentifier, targetIdentifier, pos, transducerPosition, DisplayScale, center, DisplayRadius);

            CachedLocation CreateCachedLocation()
            {
                float distance = Vector2.Distance(worldPosition, transducerPosition);

                float maxOffset = MathHelper.Lerp(offset.Start, offset.End, MathHelper.Clamp((distance - distanceThresholds.Start) / (distanceThresholds.End - distanceThresholds.Start), 0.0f, 1.0f));

                Vector2 randomPos = Rand.Vector(Rand.Range(minOffset, maxOffset));
                return new CachedLocation(worldPosition + randomPos, Timing.TotalTime + Rand.Range(10.0f, 30.0f));
            }
        }

        private void DrawMarker(SpriteBatch spriteBatch, string label, Identifier iconIdentifier, object targetIdentifier, Vector2 worldPosition, Vector2 transducerPosition, float scale, Vector2 center, float radius,
                                bool onlyShowTextOnMouseOver = false)
        {
            float linearDist = Vector2.Distance(worldPosition, transducerPosition);
            float dist = linearDist;
            if (linearDist > Range)
            {
                if (markerDistances.TryGetValue(targetIdentifier, out CachedDistance cachedDistance))
                {
                    if (cachedDistance.ShouldUpdateDistance(transducerPosition, worldPosition))
                    {
                        markerDistances.Remove(targetIdentifier);
                        CalculateDistance();
                    }
                    else
                    {
                        dist = Math.Max(cachedDistance.Distance, linearDist);
                    }
                }
                else
                {
                    CalculateDistance();
                }
            }

            void CalculateDistance()
            {
                pathFinder ??= new PathFinder(WayPoint.WayPointList, false);
                var path = pathFinder.FindPath(ConvertUnits.ToSimUnits(transducerPosition), ConvertUnits.ToSimUnits(worldPosition));
                if (!path.Unreachable)
                {
                    var cachedDistance = new CachedDistance(transducerPosition, worldPosition, path.TotalLength, Timing.TotalTime + Rand.Range(1.0f, 5.0f));
                    markerDistances.Add(targetIdentifier, cachedDistance);
                    dist = path.TotalLength;
                }
                else
                {
                    var cachedDistance = new CachedDistance(transducerPosition, worldPosition, linearDist, Timing.TotalTime + Rand.Range(4.0f, 7.0f));
                    markerDistances.Add(targetIdentifier, cachedDistance);
                }
            }

            Vector2 position = worldPosition - transducerPosition;

            position *= scale;
            position.Y = -position.Y;

            float textAlpha = MathHelper.Clamp(1.5f - dist / 50000.0f, 0.5f, 1.0f);

            Vector2 dir = Vector2.Normalize(position);
            Vector2 markerPos = (linearDist * scale > radius) ? dir * radius : position;
            markerPos += center;

            markerPos.X = (int)markerPos.X;
            markerPos.Y = (int)markerPos.Y;

            float alpha = 1.0f;
            if (!onlyShowTextOnMouseOver)
            {
                if (linearDist * scale < radius)
                {
                    float normalizedDist = linearDist * scale / radius;
                    alpha = Math.Max(normalizedDist - 0.4f, 0.0f);

                    float mouseDist = Vector2.Distance(PlayerInput.MousePosition, markerPos);
                    float hoverThreshold = 150.0f;
                    if (mouseDist < hoverThreshold)
                    {
                        alpha += (hoverThreshold - mouseDist) / hoverThreshold;
                    }
                }
            }
            else
            {
                float mouseDist = Vector2.Distance(PlayerInput.MousePosition, markerPos);
                if (mouseDist > 5)
                {
                    alpha = 0.0f;
                }
            }

            if (iconIdentifier == null || !targetIcons.TryGetValue(iconIdentifier, out var iconInfo) || iconInfo.Item1 == null)
            {
                GUI.DrawRectangle(spriteBatch, new Rectangle((int)markerPos.X - 3, (int)markerPos.Y - 3, 6, 6), markerColor, thickness: 2);
            }
            else
            {
                iconInfo.Item1.Draw(spriteBatch, markerPos, iconInfo.Item2);
            }

            if (alpha <= 0.0f) { return; }

            string wrappedLabel = ToolBox.WrapText(label, 150, GUIStyle.SmallFont.Value);
            wrappedLabel += "\n" + ((int)(dist * Physics.DisplayToRealWorldRatio) + " m");

            Vector2 labelPos = markerPos;
            Vector2 textSize = GUIStyle.SmallFont.MeasureString(wrappedLabel);

            //flip the text to left side when the marker is on the left side or goes outside the right edge of the interface
            if (GuiFrame != null && (dir.X < 0.0f || labelPos.X + textSize.X + 10 > GuiFrame.Rect.X) && labelPos.X - textSize.X > 0) 
            { 
                labelPos.X -= textSize.X + 10; 
            }

            GUI.DrawString(spriteBatch,
                new Vector2(labelPos.X + 10, labelPos.Y),
                wrappedLabel,
                Color.LightBlue * textAlpha * alpha, Color.Black * textAlpha * 0.8f * alpha,
                2, GUIStyle.SmallFont);
        }

        public override void RemoveComponentSpecific()
        {
            base.RemoveComponentSpecific();
            sonarBlip?.Remove();
            pingCircle?.Remove();
            directionalPingCircle?.Remove();
            screenOverlay?.Remove();
            screenBackground?.Remove();
            lineSprite?.Remove();

            foreach (var t in targetIcons.Values)
            {
                t.Item1.Remove();
            }
            targetIcons.Clear();

            MineralClusters = null;
        }

        public void ClientEventWrite(IWriteMessage msg, NetEntityEvent.IData extraData = null)
        {
            msg.WriteBoolean(currentMode == Mode.Active);
            if (currentMode == Mode.Active)
            {
                msg.WriteRangedSingle(zoom, MinZoom, MaxZoom, 8);
                msg.WriteBoolean(useDirectionalPing);
                if (useDirectionalPing)
                {
                    float pingAngle = MathUtils.WrapAngleTwoPi(MathUtils.VectorToAngle(pingDirection));
                    msg.WriteRangedSingle(MathUtils.InverseLerp(0.0f, MathHelper.TwoPi, pingAngle), 0.0f, 1.0f, 8);
                }
                msg.WriteBoolean(UseMineralScanner);
            }
        }
        
        public void ClientEventRead(IReadMessage msg, float sendingTime)
        {
            int msgStartPos = msg.BitPosition;

            bool isActive           = msg.ReadBoolean();
            float zoomT             = 1.0f;
            bool directionalPing    = useDirectionalPing;
            float directionT        = 0.0f;
            bool mineralScanner     = UseMineralScanner;
            if (isActive)
            {
                zoomT = msg.ReadRangedSingle(0.0f, 1.0f, 8);
                directionalPing = msg.ReadBoolean();
                if (directionalPing)
                {
                    directionT = msg.ReadRangedSingle(0.0f, 1.0f, 8);
                }
                mineralScanner = msg.ReadBoolean();
            }

            if (correctionTimer > 0.0f)
            {
                int msgLength = msg.BitPosition - msgStartPos;
                msg.BitPosition = msgStartPos;
                StartDelayedCorrection(msg.ExtractBits(msgLength), sendingTime);
                return;
            }

            CurrentMode = isActive ? Mode.Active : Mode.Passive;
            if (isActive)
            {
                zoomSlider.BarScroll = zoomT;
                zoom = MathHelper.Lerp(MinZoom, MaxZoom, zoomT);
                if (directionalPing)
                {
                    float pingAngle = MathHelper.Lerp(0.0f, MathHelper.TwoPi, directionT);
                    pingDirection = new Vector2((float)Math.Cos(pingAngle), (float)Math.Sin(pingAngle));
                }
                useDirectionalPing = directionalModeSwitch.Selected = directionalPing;

            }
        }

        private void UpdateGUIElements()
        {
            bool isActive = CurrentMode == Mode.Active;
            SonarModeSwitch.Selected = isActive;
            passiveTickBox.Selected = !isActive;
            activeTickBox.Selected = isActive;
            
            
        }
    }

    class SonarBlipDieHard
    {
        public float FadeTimer;
        public Vector2 Position;
        public float Scale;
        public Vector2 Velocity;
        public float? Rotation;
        public Vector2 Size;
        public HandHeldSonar.BlipTypeDieHard BlipType;
        public float Alpha = 1.0f;

        public SonarBlipDieHard(Vector2 pos, float fadeTimer, float scale, HandHeldSonar.BlipTypeDieHard blipType = HandHeldSonar.BlipTypeDieHard.Default)
        {
            Position = pos;
            FadeTimer = Math.Max(fadeTimer, 0.0f);
            Scale = scale;
            Size = new Vector2(0.5f, 1.0f);
            BlipType = blipType;
        }
    }
}