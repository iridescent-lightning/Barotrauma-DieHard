
using Barotrauma.Extensions;
using Barotrauma.Networking;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

using Barotrauma.Items.Components;

using Barotrauma;
using HarmonyLib;
using System.Globalization;
using System.Reflection;// for bindingflags

using Networking;

namespace BarotraumaDieHard//todo make a structural namespace DieHard.Item.Components. namespace can't be used in elsewhere
{
    partial class SteeringPatch
    {
        

        private static GUIComponent engineStatusContainer;
        private static GUITickBox engineStatusLight;
        private static GUITextBlock powerConText;

        private static GUITextBlock verticalEnginePowerText;

        private static GUIComponent verticalSpeedContainer;
        private static GUIScrollBar powerSlider;
        
        
        
        
        [HarmonyPatch("CreateGUI")]
        [HarmonyPostfix]
        public static void CreateGUI(Steering __instance)
        {
            if (__instance.GuiFrame == null) return;
            engineStatusContainer = new GUIFrame(new RectTransform(new Vector2(0.25f, 0.25f), __instance.GuiFrame.RectTransform, Anchor.BottomRight)
            {
                RelativeOffset =  new Vector2(0.1f, -0.25f)
            }, "ItemUI");
            var paddedengineStatusContainer = new GUIFrame(new RectTransform(engineStatusContainer.Rect.Size - GUIStyle.ItemFrameMargin, engineStatusContainer.RectTransform, Anchor.Center, isFixedSize: false)
            {
                AbsoluteOffset = GUIStyle.ItemFrameOffset
            }, style: null);
            engineStatusLight = new GUITickBox(new RectTransform(new Vector2(0.25f, 0.25f), paddedengineStatusContainer.RectTransform, Anchor.TopLeft){RelativeOffset = new Vector2(0.0f, 0.0f)},
                TextManager.Get("EngineStatus"), font: GUIStyle.SubHeadingFont, style: "IndicatorLightPower")
            {
                CanBeFocused = false
            };
            var powerconsuptionLabel = new GUITextBlock(new RectTransform(new Vector2(0.5f, 0.25f), paddedengineStatusContainer.RectTransform, Anchor.TopCenter){RelativeOffset = new Vector2(0.1f, 0.25f)},
                TextManager.Get("EnginePowerConsumption"), font: GUIStyle.SubHeadingFont, textAlignment: Alignment.CenterRight);
                //textBlocksToScaleAndNormalize.Add(powerconsuptionLabel);

            var digitalBackground = new GUIFrame(new RectTransform(new Vector2(0.75f, 0.4f), paddedengineStatusContainer.RectTransform, Anchor.TopCenter){RelativeOffset = new Vector2(0.1f, 0.5f)}, style: "DigitalFrameDark");

            powerConText = new GUITextBlock(new RectTransform(new Vector2(0.9f, 0.95f), digitalBackground.RectTransform, Anchor.Center), 
                "", font: GUIStyle.DigitalFont, textColor: GUIStyle.TextColorDark)
            {
                TextAlignment = Alignment.CenterRight,
                //ToolTip = TextManager.Get("SonarHertzTip"), // Update tooltip as necessary
                TextGetter = () => powerConsumptionEngine.ToString("F1")// Display the current hertz value
            };
            
            
            verticalSpeedContainer = new GUIFrame(new RectTransform(new Vector2(0.25f, 0.25f), __instance.GuiFrame.RectTransform, Anchor.BottomRight)
            {
                RelativeOffset =  new Vector2(-0.22f, 0.07f)
            }, "ItemUI");

            var paddedverticalSpeedContainer = new GUIFrame(new RectTransform(verticalSpeedContainer.Rect.Size - GUIStyle.ItemFrameMargin, verticalSpeedContainer.RectTransform, Anchor.Center, isFixedSize: false)
            {
                AbsoluteOffset = GUIStyle.ItemFrameOffset
            }, style: null);

            var forceText = new GUITextBlock(new RectTransform(new Vector2(0.25f, 0.1f), paddedverticalSpeedContainer.RectTransform)
            {RelativeOffset = new Vector2(0.3f, 0f) },
            "Vertical Engine Output", null, // null to use default color
            font: GUIStyle.SubHeadingFont,
            textAlignment: Alignment.Center
            );

            var digitalBackgroundVerticalEngine = new GUIFrame(new RectTransform(new Vector2(0.65f, 0.3f), paddedverticalSpeedContainer.RectTransform, Anchor.TopCenter){RelativeOffset = new Vector2(0.0f, 0.3f)}, style: "DigitalFrameDark");

            verticalEnginePowerText = new GUITextBlock(new RectTransform(new Vector2(0.9f, 0.95f), digitalBackgroundVerticalEngine.RectTransform, Anchor.Center), 
                "", font: GUIStyle.DigitalFont, textColor: GUIStyle.TextColorDark)
            {
                TextAlignment = Alignment.CenterRight,
                ToolTip = TextManager.Get("VerticalEngineDisplayTip"), 
                TextGetter = () => lerpedVerticalEnginePower.ToString("F1")// Display the current hertz value
            };

            powerSlider = new GUIScrollBar(new RectTransform(new Vector2(0.5f, 0.25f), paddedverticalSpeedContainer.RectTransform, Anchor.BottomLeft)
            {
                RelativeOffset = new Vector2(0.3f, 0.1f)
            }, barSize: 0.15f, isHorizontal: true, style: "DeviceSlider")
            {
                Step = 0.05f,
                OnMoved = (scrollbar, scroll) =>
                {
                    
                   lerpedVerticalEnginePower = MathHelper.Lerp(0, 100f, scroll);
                    if (GameMain.Client != null)
                    {
                        // it seems that this isn't necessary for all scroll bars, but I'll leave it here just in case. Without it, the scroll bar can still update during multiplayer.
                        __instance.item.CreateClientEvent(__instance);
                        SendVerticalEnginePowerMessage(__instance.item, lerpedVerticalEnginePower);
                    }
                    return true;
                }
            };
            
        }
        
        private static float flickerTimer;
        private static readonly float flickerFrequency = 0.5f;
        private static float powerConsumptionEngine = 0f;

        [HarmonyPatch("UpdateHUDComponentSpecific")]
        [HarmonyPostfix]
        
        public static void UpdateHUDComponentSpecificPostfix(Steering __instance, float deltaTime, Camera cam)
        {
            
            Steering _ = __instance;
            if (engineStatusContainer == null) return;
            Engine? engine = _.item.GetConnectedComponents<Engine>().FirstOrDefault();
            if (engine != null)
            {
                powerConsumptionEngine = engine.GetCurrentPowerConsumption(engine.powerIn);
                //DebugConsole.NewMessage(powerConsumptionEngine.ToString("F1"));
            }
            
            if (engine != null && engine.item.Condition > 0.0f)
            {
                engineStatusLight.Selected = engine.IsActive;// light up if engine is active
            }
            else if (engine != null && engine.item.Condition <= 0.0f)
            {
                flickerTimer += deltaTime;
                if (flickerTimer > flickerFrequency)
                {
                    
                    engineStatusLight.Selected = !engineStatusLight.Selected; // Toggle the light state
                    flickerTimer = 0;
                }
                
            }
            else
            {
                engineStatusLight.Selected = false;
            }
        }

        
        private static void SendVerticalEnginePowerMessage(Item item, float verticalEnginePower)
        {
            IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.VERTICAL_ENGINE_POWER_CHANGE);

            msg.WriteUInt16(item.ID); // ID of the oxygen generator item
            msg.WriteSingle(verticalEnginePower); 
            NetUtil.SendServer(msg, DeliveryMethod.Reliable);
            
        }
        
    }
}
