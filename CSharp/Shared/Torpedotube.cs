using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Barotrauma.Extensions;
using Barotrauma;


using Networking;

#if CLIENT
using Microsoft.Xna.Framework.Graphics;
#endif

namespace BarotraumaDieHard//todo make a structural namespace DieHard.Item.Components. namespace can't be used in elsewhere
{
    class TorpedoTube : ItemContainer 
    {
		public enum Mode
        {
            Active,
            Passive
        }
        
        public enum TorpedoTubeNetMsg
        {
            ArmTorpedo
            // Add other message types as needed
        }


        private Mode TorpedoMode;
        private bool unsentChanges;
        private float correctionTimer;

        public static List<Item> torpedoeList = new List<Item>();
		
        /*public override void OnItemLoaded()
        {
            base.OnItemLoaded();
        }*/

        public TorpedoTube(Item item, ContentXElement element)
            : base(item, element)
        {
            TorpedoMode = Mode.Passive; // default mode
            unsentChanges = false;
            correctionTimer = 0.0f;
            IsActive = true;

        }



#if CLIENT
        public override void CreateGUI()
        {
            var content = new GUIFrame(new RectTransform(GuiFrame.Rect.Size - GUIStyle.ItemFrameMargin, GuiFrame.RectTransform, Anchor.Center) { AbsoluteOffset = GUIStyle.ItemFrameOffset },
                style: null)
            {
                CanBeFocused = false
            };

            LocalizedString labelText = GetUILabel();
            GUITextBlock label = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.0f), content.RectTransform, Anchor.TopCenter),
                labelText, font: GUIStyle.SubHeadingFont, textAlignment: Alignment.CenterLeft, wrap: true)
                {
                    IgnoreLayoutGroups = true
                };
            
            int buttonSize = GUIStyle.ItemFrameTopBarHeight;
            Point margin = new Point(buttonSize / 4, buttonSize / 6);

            GUILayoutGroup buttonArea = new GUILayoutGroup(new RectTransform(new Point(GuiFrame.Rect.Width - margin.X * 2, buttonSize - margin.Y * 2), GuiFrame.RectTransform, Anchor.TopCenter) { AbsoluteOffset = new Point(0, margin.Y) }, 
                isHorizontal: true, childAnchor: Anchor.TopRight)
            {
                AbsoluteSpacing = margin.X / 2
            };
            if (Inventory.Capacity > 1)
            {
                new GUIButton(new RectTransform(Vector2.One, buttonArea.RectTransform, scaleBasis: ScaleBasis.Smallest), style: "SortItemsButton")
                {
                    ToolTip = TextManager.Get("SortItemsAlphabetically"),
                    OnClicked = (btn, userdata) =>
                    {
                        SortItems();
                        return true;
                    }
                };
                new GUIButton(new RectTransform(Vector2.One, buttonArea.RectTransform, scaleBasis: ScaleBasis.Smallest), style: "MergeStacksButton")
                {
                    ToolTip = TextManager.Get("MergeItemStacks"),
                    OnClicked = (btn, userdata) =>
                    {
                        MergeStacks();
                        return true;
                    }
                };

            }
            
            var torpedoModeArea = new GUIFrame(new RectTransform(new Vector2(0.25f, 0.25f), content.RectTransform, Anchor.TopLeft), style: null);

            var powerLight = new GUITickBox(new RectTransform(new Vector2(0.25f, 0.25f), torpedoModeArea.RectTransform, Anchor.Center){RelativeOffset = new Vector2(0.15f, 0.0f)},
                TextManager.Get("Arm"), font: GUIStyle.SubHeadingFont, style: "IndicatorLightPower")
            {
                CanBeFocused = false
            };

            var torpedoModeSwitch = new GUIButton(new RectTransform(new Vector2(1f, 1), torpedoModeArea.RectTransform){ RelativeOffset = new Vector2(-0.25f, 0.10f) }, string.Empty, style: "SwitchVertical")
            {
                //UserData = UIHighlightAction.ElementId.torpedoModeSwitch,
                Selected = false,
                Enabled = true,
                ClickSound = GUISoundType.UISwitch,
                OnClicked = (button, data) =>
                {
                    button.Selected = !button.Selected;
                    TorpedoMode = button.Selected ? Mode.Active : Mode.Passive;
                    this.Arm(item);
                    powerLight.Selected = TorpedoMode == Mode.Active;// learn this
                    
                    if (GameMain.Client != null)
                    {
                        unsentChanges = true;
                        correctionTimer = CorrectionDelay;
                        SendArmTorpedoMessage(item, TorpedoMode);
                    }
                    

                    return true;
                }
            };
            

            float minInventoryAreaSize = 0.5f;
            guiCustomComponent = new GUICustomComponent(
                new RectTransform(new Vector2(1.0f, label == null ? 1.0f : Math.Max(1.0f - label.RectTransform.RelativeSize.Y, minInventoryAreaSize)), content.RectTransform, Anchor.BottomCenter),
                onDraw: (SpriteBatch spriteBatch, GUICustomComponent component) => { Inventory.Draw(spriteBatch); },
                onUpdate: null)
            {
                CanBeFocused = true
            };

            // Expand the frame vertically if it's too small to fit the text
            if (label != null && label.RectTransform.RelativeSize.Y > 0.5f)
            {
                int newHeight = (int)(GuiFrame.Rect.Height + (2 * (label.RectTransform.RelativeSize.Y - 0.5f) * content.Rect.Height));
                if (newHeight > GuiFrame.RectTransform.MaxSize.Y)
                {
                    Point newMaxSize = GuiFrame.RectTransform.MaxSize;
                    newMaxSize.Y = newHeight;
                    GuiFrame.RectTransform.MaxSize = newMaxSize;
                }
                GuiFrame.RectTransform.Resize(new Point(GuiFrame.Rect.Width, newHeight));
                content.RectTransform.Resize(GuiFrame.Rect.Size - GUIStyle.ItemFrameMargin);
                label.CalculateHeightFromText();
                guiCustomComponent.RectTransform.Resize(new Vector2(1.0f, Math.Max(1.0f - label.RectTransform.RelativeSize.Y, minInventoryAreaSize)));
            }

            Inventory.RectTransform = guiCustomComponent.RectTransform;
        }
#endif		
		 
		
        public void Arm(Item instance)
        {
                var itemContainer = instance.GetComponent<ItemContainer>();
                Item firstItem = itemContainer.Inventory.AllItems.FirstOrDefault();
                if (firstItem == null)
                {
                    item.SendSignal(TextManager.Get("TorpedoTubeEmpty").ToString(), "status_out");
                    
                    return;
                }
                if (TorpedoMode == Mode.Active)
                {
                    DebugConsole.NewMessage(firstItem.ToString());
                    torpedoeList.Add(firstItem);
                    item.SendSignal(TextManager.Get("TorpedoArm").ToString(), "status_out");
                }
                else
                {
                    item.SendSignal(TextManager.Get("TorpedoDisarm").ToString(), "status_out");
                    torpedoeList.Remove(firstItem);
                }
            
        }


#if CLIENT
        private void SendArmTorpedoMessage(Item item, Mode mode)
        {
            IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.TORPEDOTUBE_ARM);

            msg.WriteUInt16(item.ID); // ID of the torpedo tube
            msg.WriteBoolean(mode == Mode.Active); // Boolean representing the mode
            NetUtil.SendServer(msg, DeliveryMethod.Reliable);
        }
#endif


        public static void OnReceiveArmTorpedoMessage(object[] args)
        {
            IReadMessage msg = (IReadMessage)args[0];
            // Extract data from the message
            ushort itemId = msg.ReadUInt16();
            bool isActive = msg.ReadBoolean();
            
            // Find the torpedo tube item and update its state
            Item tubeItem = Entity.FindEntityByID(itemId) as Item;
            if (tubeItem != null)
            {
                var torpedoTube = tubeItem.GetComponent<TorpedoTube>();
                if (torpedoTube != null)
                {
                    torpedoTube.TorpedoMode = isActive ? Mode.Active : Mode.Passive;
                    torpedoTube.Arm(tubeItem);
                    
                }
            }
        }



    }

}
