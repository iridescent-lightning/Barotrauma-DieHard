using System.Collections.Generic;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using System;// fix for Math
using System.Linq;
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Barotrauma.Extensions;
using Barotrauma;

#if CLIENT
using Microsoft.Xna.Framework.Graphics;
#endif

namespace BarotraumaDieHard
{
    class HanheldSonar : Sonar
    {
        // private List<Item> linkedItems; // Change to a List
		private const float PingFrequency = 0.5f;//how fast a ping spreads
		private float timeSinceLastPing = 1.1f;
		
        public override void OnItemLoaded()
        {
            base.OnItemLoaded();
            
            // Populate linkedItems directly from item.linkedTo
            // linkedItems = item.linkedTo.Cast<Item>().ToList();
        }

        public HanheldSonar(Item item, ContentXElement element)
            : base(item, element)
        {
        }
#if CLIENT
        public override void CreateGUI()
        {
            isConnectedToSteering = item.GetComponent<Steering>() != null;
            Vector2 size = isConnectedToSteering ? controlBoxSize : new Vector2(0.46f, 0.4f);

            controlContainer = new GUIFrame(new RectTransform(size, GuiFrame.RectTransform, Anchor.BottomLeft), "ItemUI");
            if (!isConnectedToSteering) //&& !GUI.IsFourByThree()
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

            var directionalModeFrame = new GUIFrame(new RectTransform(new Vector2(1, 0.45f), lowerAreaFrame.RectTransform, Anchor.BottomCenter), style: null)
            {
                UserData = UIHighlightAction.ElementId.DirectionalSonarFrame
            };
            directionalModeSwitch = new GUIButton(new RectTransform(new Vector2(0.3f, 0.8f), directionalModeFrame.RectTransform, Anchor.CenterLeft), string.Empty, style: "SwitchHorizontal")
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
                TextManager.Get("SonarDirectionalPing"), GUIStyle.TextColorNormal, GUIStyle.SubHeadingFont, Alignment.CenterLeft);
            textBlocksToScaleAndNormalize.Add(directionalModeSwitchText);

            if (HasMineralScanner)
            {
                AddMineralScannerSwitchToGUI();
            }
            else
            {
                mineralScannerSwitch = null;
            }

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
#endif		
         public override void Update(float deltaTime, Camera cam)
        {
            UpdateOnActiveEffects(deltaTime);

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