using System;
using Barotrauma;
using Barotrauma.Networking;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Items.Components;
using Networking;

namespace BarotraumaDieHard
{
    partial class CustomJunctionBox : PowerTransfer 
    {
        
        public GUIButton JBModeSwitch;
        private void InitProjectSpecificAdditional(XElement element)
        {
            var paddedFrame = new GUIFrame(new RectTransform(new Vector2(0.3f, 1f), GuiFrame.RectTransform, Anchor.Center) { RelativeOffset = new Vector2(-0.7f, 0) },
                "ItemUI")
            {
                CanBeFocused = false
            };

            JBModeSwitch = new GUIButton(new RectTransform(new Vector2(0.6f, 0.6f), paddedFrame.RectTransform, Anchor.Center){ RelativeOffset = new Vector2(0,0) }, string.Empty, style: "SwitchDieHardJBButton")
            {
                //UserData = UIHighlightAction.ElementId.torpedoModeSwitch,
                ToolTip = TextManager.Get("CustomJunctionBoxSwitchTip"),
                Selected = false,
                Enabled = true,
                ClickSound = GUISoundType.UISwitch,
                OnClicked = (button, data) =>
                {
                    button.Selected = !button.Selected;
                    this.leverState = !this.leverState;

                    
                    if (GameMain.Client != null)
                    {
                        
                        SendJBSwitchMessage(item, this.leverState);
                    }
                    

                    return true;
                }
            };
        }

        private void SendJBSwitchMessage(Item item, bool leverState)
        {
            IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.SWITCH_JUNCTIONBOX);

            msg.WriteUInt16(item.ID); // ID of the torpedo tube
            msg.WriteBoolean(leverState); // Boolean representing the mode
            NetUtil.SendServer(msg, DeliveryMethod.Reliable);
        }

        
    }
}