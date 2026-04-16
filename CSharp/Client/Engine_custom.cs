using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Xml.Linq;

using Barotrauma.Items.Components;
using System.Reflection;
using Barotrauma;
using Networking;

namespace BarotraumaDieHard
{
    partial class CustomEngine : Engine
    {
        private GUIScrollBar verticalForceSlider;
        partial void InitProjSpecific(ContentXElement element)
        {
            
            //element.SetAttributeValue("relativesize", "0.2, 0.35");
            //GuiFrame.RectTransform.RelativeSize = new Vector2(0.2f, 0.18f);
            //GuiFrame.RectTransform.MinSize = new Point(450, 400);
            
            
            
        }


        public void CreateGUI()
        {
            
            if (hasVerticalThruster)
            {
                var verticalControlGUI = new GUIFrame(new RectTransform(new Vector2(1f, 0.9f), GuiFrame.RectTransform, Anchor.BottomCenter)
                {
                    RelativeOffset = new Vector2(0, -0.9f)
                }, style: "ItemUI");
                var paddedFrame = new GUIFrame(new RectTransform(new Vector2(0.85f, 0.65f), verticalControlGUI.RectTransform, Anchor.Center), style: null);


                var lightsArea = new GUIFrame(new RectTransform(new Vector2(1, 0.38f), paddedFrame.RectTransform, Anchor.TopLeft), style: null);
                
                /*autoControlIndicator = new GUITickBox(new RectTransform(new Vector2(0.45f, 0.8f), lightsArea.RectTransform, Anchor.Center, Pivot.CenterLeft)
                {
                    RelativeOffset = new Vector2(0.05f, 0)
                }, TextManager.Get("PumpAutoControl", "ReactorAutoControl"), font: GUIStyle.SubHeadingFont, style: "IndicatorLightYellow")
                {
                    Selected = false,
                    Enabled = false,
                    ToolTip = TextManager.Get("AutoControlTip")
                };
                powerIndicator.TextBlock.Wrap = autoControlIndicator.TextBlock.Wrap = true;
                powerIndicator.TextBlock.OverrideTextColor(GUIStyle.TextColorNormal);
                autoControlIndicator.TextBlock.OverrideTextColor(GUIStyle.TextColorNormal);
                GUITextBlock.AutoScaleAndNormalize(powerIndicator.TextBlock, autoControlIndicator.TextBlock);*/


                var sliderArea = new GUIFrame(new RectTransform(new Vector2(1, 0.6f), paddedFrame.RectTransform, Anchor.BottomLeft), style: null);
                LocalizedString powerLabel = TextManager.Get("VerticalEngineForce");

                new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.3f), sliderArea.RectTransform, Anchor.TopCenter), "", textColor: GUIStyle.TextColorNormal, font: GUIStyle.SubHeadingFont, textAlignment: Alignment.Center)
                {
                    AutoScaleHorizontal = true,
                    TextGetter = () => 
                    { 
                        return TextManager.AddPunctuation(':', powerLabel, 
                            TextManager.GetWithVariable("percentageformat", "[value]", ((int)MathF.Round(targetVerticalForce)).ToString())); 
                    }
                };

                verticalForceSlider = new GUIScrollBar(new RectTransform(new Vector2(0.95f, 0.45f), sliderArea.RectTransform, Anchor.Center), barSize: 0.1f, style: "DeviceSlider")
                {
                    Step = 0.05f,
                    BarScroll = 0.5f,
                    Enabled = !isMaintainDepthEnabled,
                    OnMoved = (GUIScrollBar scrollBar, float barScroll) =>
                    {
                        
                        lastReceivedTargetVerticalForce = null; 
                        
                        // 将 0.0~1.0 的滑块值映射到 -100 ~ 100
                        float newVerticalTargetForce = barScroll * 200.0f - 100.0f;
                        
                        // 检查变化量防止频繁发包
                        if (Math.Abs(newVerticalTargetForce - targetVerticalForce) < 0.01) { return false; }

                        targetVerticalForce = newVerticalTargetForce; // 赋值给垂直力变量
                        User = Character.Controlled;

                        if (GameMain.Client != null)
                        {
                            correctionTimer = CorrectionDelay;
                            SendVerticalForceMessage(this.item, targetVerticalForce);
                        }
                        return true;
                    }
                };

            var textsArea = new GUIFrame(new RectTransform(new Vector2(1, 0.25f), sliderArea.RectTransform, Anchor.BottomCenter), style: null);
            var downwardsLabel = new GUITextBlock(new RectTransform(new Vector2(0.4f, 1.0f), textsArea.RectTransform, Anchor.CenterLeft), TextManager.Get("EngineDownwards"),
                textColor: GUIStyle.TextColorNormal, font: GUIStyle.SubHeadingFont, textAlignment: Alignment.CenterLeft);
            var upwardsLabel = new GUITextBlock(new RectTransform(new Vector2(0.4f, 1.0f), textsArea.RectTransform, Anchor.CenterRight), TextManager.Get("EngineUpwards"),
                textColor: GUIStyle.TextColorNormal, font: GUIStyle.SubHeadingFont, textAlignment: Alignment.CenterRight);
            GUITextBlock.AutoScaleAndNormalize(upwardsLabel, downwardsLabel);

            // 左侧：指示灯（使用 GUITickBox 模拟指示灯，Enabled = false 使其不可交互）
        var indicatorLight = new GUITickBox(new RectTransform(new Vector2(0.055f, 0.32f), paddedFrame.RectTransform, Anchor.CenterLeft)
        {
            RelativeOffset = new Vector2(0.016f, -0.3f)
        }, "", style: "IndicatorLightGreen")
        {
            Selected = isMaintainDepthEnabled,
            Enabled = false,  // 不可交互，仅作为指示灯
            CanBeFocused = false
        };
        
            // 左侧：指示灯旁边的标签
            var indicatorLabel = new GUITextBlock(new RectTransform(new Vector2(0.2f, 0.6f), indicatorLight.RectTransform, Anchor.CenterRight)
            {
                RelativeOffset = new Vector2(-1.15f, 0)
            }, TextManager.Get("AutomaticControlled"), font: GUIStyle.SubHeadingFont)
            {
                TextColor = GUIStyle.TextColorNormal
            };


            var maintainDepthTick = new GUITickBox(new RectTransform(new Vector2(1, 0.2f), paddedFrame.RectTransform, Anchor.TopRight)
            {
            RelativeOffset = new Vector2(-0.7f, 0.1f)
            }, TextManager.Get("KeepDepth"))
            {
                Selected = isMaintainDepthEnabled,
                OnSelected = (tick) =>
                {
                    isMaintainDepthEnabled = tick.Selected;

                    // 更新指示灯状态
                if (indicatorLight != null)
                {
                    indicatorLight.Selected = isMaintainDepthEnabled;
                }
                


                    if (isMaintainDepthEnabled)
                    {
                        // 启动瞬间，将当前深度设为目标深度
                        maintainDepthTarget = item.Submarine.WorldPosition.Y;
                    }
                    else
                    {
                        // --- 取消保持深度时的逻辑 ---
                        // 1. 将逻辑上的目标力归零
                        targetVerticalForce = 0.0f;
                        lastReceivedTargetVerticalForce = 0.0f;

                        // 2. 将 UI 滑块拨回中间位置 (0.5f 代表 0 动力)
                        if (verticalForceSlider != null)
                        {
                            verticalForceSlider.BarScroll = 0.5f;
                        }
                    }
                    
                    if (GameMain.Client != null)
                        {
                            correctionTimer = CorrectionDelay;
                            SendMaintainDepthMessage(item, isMaintainDepthEnabled, maintainDepthTarget);
                        }
                    // 
                    return true;
                }
            };
            }
            
        }

        private void SendVerticalForceMessage(Item item, float verticalForce)
        {
            // 创建消息，使用你定义的事件标识符
            IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.VECTORED_ENGINE_FORCECHANGE);

            msg.WriteUInt16(item.ID);           // 必须写入 ID，否则服务器不知道是哪个引擎
            msg.WriteSingle(verticalForce);     // 写入垂直力数值 (-100 到 100)

            // 客户端发送给服务器
            NetUtil.SendServer(msg, DeliveryMethod.Reliable);
        }
        private void SendMaintainDepthMessage(Item item, bool isEnabled, float targetDepth)
{
    IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.VECTORED_ENGINE_MAINTAINDEPTH);

    msg.WriteUInt16(item.ID);
    msg.WriteBoolean(isEnabled);   // 同步开关
    msg.WriteSingle(targetDepth);  // 同步深度目标值

    NetUtil.SendServer(msg, DeliveryMethod.Reliable);
}
    }
}
