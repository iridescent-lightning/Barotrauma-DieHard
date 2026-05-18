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

namespace BarotraumaDieHard
{
    class CustomOxygenGenerator : OxygenGenerator
    {
        private ItemContainer[] itemContainers; // Change to an array
        private ItemContainer firstItemContainer; // Add a second ItemContainer
        private ItemContainer secondItemContainer; // Add a second ItemContainer

        private bool[] isChargingOxygenTank;

        private Item oxygenCandle;
        

        private float minGeneratedAmountFactor = 0.1f;
        private float maxGeneratedAmountFactor = 1.0f;

        private float newGeneratedAmountFactor = 1.0f;
        
        private float powerConsumptionDisplay;
        private float o2Production;
        private bool turnedOn = true;
        private float recycledAmount;

        [Editable, Serialize(500.0f, IsPropertySaveable.Yes, description: "How much CO2 gas it can reduced.", alwaysUseInstanceValues: true)]
        public float RecycledAmount
        {
            get { return recycledAmount; }
            set { recycledAmount = MathHelper.Clamp(value, 0.0f, 2000.0f); }
        }

        public float CurrRecycleFlow
        {
            get;
            private set;
        }

        private float purifyingAmount;
        [Editable, Serialize(50.0f, IsPropertySaveable.Yes, description: "How much toxic gas it can reduced.", alwaysUseInstanceValues: true)]
        public float PurifyingAmount
        {
            get { return purifyingAmount; }
            set { purifyingAmount = MathHelper.Clamp(value, -100.0f, 100.0f); }
        }
        public float CurrPurifyingFlow
        {
            get;
            private set;
        }

        private float heatingAmount;
        [Editable, Serialize(25.0f, IsPropertySaveable.Yes, description: "How much heat it can generate. Setting too high will cause unexpected consequence.", alwaysUseInstanceValues: true)]
        public float HeatingAmount
        {
            get { return heatingAmount; }
            set { heatingAmount = MathHelper.Clamp(value, -10000.0f, 10000.0f); }
        }
        public float CurrHeatingFlow
        {
            get;
            private set;
        }
        public float CurrAirPressureRegulatingFlow
        {
            get;
            private set;
        }



        public bool HasPower => IsActive && Voltage >= MinVoltage;
#if CLIENT
		private GUIFrame mainFrame;
		public GUIButton PowerButton;
        private GUITickBox powerLight;
        private GUIScrollBar powerSlider;
        private GUITextBlock powerConText;
        private GUITextBlock o2ProductionText;
#endif
        public override void OnItemLoaded()
        {
            base.OnItemLoaded();

            // Retrieve all ItemContainer components attached to the item
            itemContainers = item.GetComponents<ItemContainer>()
                                 .OfType<ItemContainer>()
                                 .ToArray();


            // Check if there are any ItemContainers
            if (itemContainers == null || itemContainers.Length < 2)
            {
                DebugConsole.LogError("Not enough ItemContainer components found on the item.");
                return;  // Return or handle the error appropriately
            }

            firstItemContainer = itemContainers[0];
            // Access the second ItemContainer from the array
            secondItemContainer = itemContainers[1];

            
            
#if CLIENT
            powerSlider.BarScroll = 1.0f;
    
            // Calculate the new generated amount factor based on the scrollbar's position
            newGeneratedAmountFactor = MathHelper.Lerp(minGeneratedAmountFactor, maxGeneratedAmountFactor, powerSlider.BarScroll);
            
#endif            
        }
        
        public CustomOxygenGenerator(Item item, ContentXElement element)
            : base(item, element)
        {
            ventUpdateTimer = Rand.Range(0.0f, VentUpdateInterval);
            IsActive = true;

            // Inside the constructor or initialization method:
            isChargingOxygenTank = new bool[5]; // For 5 switches
            
#if CLIENT
            if (GuiFrame == null) { return; }
            if (mainFrame == null)
            {
                mainFrame = new GUIFrame(new RectTransform(new Vector2(0.95f, 0.55f), GuiFrame.RectTransform, Anchor.TopCenter), null);
            }


            new GUIFrame(new RectTransform(new Vector2(1.0f, 0.01f), mainFrame.RectTransform, Anchor.BottomCenter){RelativeOffset = new Vector2(0, 0)}, style: "HorizontalLine");

            var powerLightArea = new GUIFrame(new RectTransform(new Vector2(0.1f, 0.1f), mainFrame.RectTransform, Anchor.TopLeft){RelativeOffset = new Vector2(0.02f, 0.1f)}, style: null);
            powerLight = new GUITickBox(new RectTransform(Vector2.One, powerLightArea.RectTransform, Anchor.Center),
                TextManager.Get("PowerLabel"), font: GUIStyle.SubHeadingFont, style: "IndicatorLightPower")
            {
                CanBeFocused = false
            };

            

            var powerButtonArea = new GUIFrame(new RectTransform(new Vector2(0.175f, 0.35f), mainFrame.RectTransform, Anchor.CenterLeft)
            {
                RelativeOffset = new Vector2(0.02f, 0.0f)
            }, style: "PowerButtonFrame");

            

            PowerButton = new GUIButton(new RectTransform(new Vector2(1.1f, 1.1f), powerButtonArea.RectTransform, Anchor.Center)
            {
                RelativeOffset = new Vector2(0, 0.0f)
            }, style: "PowerButton")
            {
                //UserData = UIHighlightAction.ElementId.PowerButton,
                OnClicked = (button, data) =>
                {
                    turnedOn = !turnedOn;
                    //IsActive = !IsActive;
                    if (GameMain.Client != null)
                    {
                        correctionTimer = CorrectionDelay;
                        SendToggleOxygenGeneratorMessage(item, turnedOn);
                    }
                    powerLight.Selected = IsActive;
                    return true;
                }
            };

            powerSlider = new GUIScrollBar(new RectTransform(new Vector2(0.5f, 0.15f), mainFrame.RectTransform, Anchor.BottomLeft)
            {
                RelativeOffset = new Vector2(0.45f, 0.1f)
            }, barSize: 0.15f, isHorizontal: true, style: "DeviceSlider")
            {
                OnMoved = (scrollbar, scroll) =>
                {
                    
                    // Calculate the new generated amount factor based on the scrollbar's position
                    newGeneratedAmountFactor = MathHelper.Lerp(minGeneratedAmountFactor, maxGeneratedAmountFactor, scroll);
                    //DebugConsole.NewMessage(newGeneratedAmountFactor.ToString());
                    
                    
                    
                    if (GameMain.Client != null)
                    {
                        //unsentChanges = true;
                        correctionTimer = Sonar.CorrectionDelay;
                        SendGeneratedAmountFactorMessage(item, newGeneratedAmountFactor);
                    }
                    return true;
                }
            };
            var powerSliderText = new GUITextBlock(new RectTransform(new Vector2(0.9f, 0.95f), powerSlider.RectTransform, Anchor.CenterLeft){RelativeOffset = new Vector2(-1f,0)}, 
                TextManager.Get("oxygeneratornui.powerslidertext"), font: GUIStyle.SubHeadingFont, textAlignment: Alignment.CenterRight);

            var digitalBackground = new GUIFrame(new RectTransform(new Vector2(0.35f, 0.2f), mainFrame.RectTransform, Anchor.CenterRight){RelativeOffset = new Vector2(0.04f, 0.1f)}, style: "DigitalFrameDark");

            var powerconsuptionLabel = new GUITextBlock(new RectTransform(new Vector2(0.5f, 0.25f), digitalBackground.RectTransform, Anchor.CenterLeft){RelativeOffset = new Vector2(-0.5f, 0)},
                TextManager.Get("KW"), font: GUIStyle.SubHeadingFont, textAlignment: Alignment.CenterRight);
                //textBlocksToScaleAndNormalize.Add(powerconsuptionLabel);
            powerConText = new GUITextBlock(new RectTransform(new Vector2(0.9f, 0.95f), digitalBackground.RectTransform, Anchor.Center), 
                "", font: GUIStyle.DigitalFont, textColor: GUIStyle.TextColorDark)
            {
                TextAlignment = Alignment.CenterRight,
                //ToolTip = TextManager.Get("SonarHertzTip"), // Update tooltip as necessary
                TextGetter = () => powerConsumptionDisplay.ToString("F1")// Display the current hertz value
            };

            var digitalBackgroundO2 = new GUIFrame(new RectTransform(new Vector2(0.35f, 0.2f), mainFrame.RectTransform, Anchor.CenterRight){RelativeOffset = new Vector2(0.04f, -0.2f)}, style: "DigitalFrameDark");

            var o2ProductionLabel = new GUITextBlock(new RectTransform(new Vector2(0.5f, 0.25f), digitalBackgroundO2.RectTransform, Anchor.CenterLeft){RelativeOffset = new Vector2(-0.5f, 0)},
                TextManager.Get("O2"), font: GUIStyle.SubHeadingFont, textAlignment: Alignment.CenterRight);

            o2ProductionText = new GUITextBlock(new RectTransform(new Vector2(0.9f, 0.95f), digitalBackgroundO2.RectTransform, Anchor.Center), 
                "", font: GUIStyle.DigitalFont, textColor: GUIStyle.TextColorDark)
            {
                TextAlignment = Alignment.CenterRight,
                //ToolTip = TextManager.Get("SonarHertzTip"), // Update tooltip as necessary
                TextGetter = () => o2Production.ToString("F0")// Display the current hertz value
            };



            var secondMainFrame = new GUIFrame(new RectTransform(new Vector2(0.95f, 0.3f), GuiFrame.RectTransform, Anchor.TopCenter) { RelativeOffset = new Vector2(0, 0.6f) }, null);

            // Create a shared parent frame to position all switches independently of each other
            var parentFrame = new GUIFrame(new RectTransform(new Vector2(0.15f, 0.6f), secondMainFrame.RectTransform, Anchor.Center) { RelativeOffset = new Vector2(-0.4f, 0.1f) }, null);

            var switchOffsets = new Vector2[] { Vector2.Zero, new Vector2(1.3f, 0), new Vector2(2.6f, 0), new Vector2(3.9f, 0), new Vector2(5.2f, 0) };

            for (int i = 0; i < 5; i++)
            {
                int index = i;
                // Create an indicator area above each switch
                var indicatorArea = new GUIFrame(new RectTransform(new Vector2(1, 0.2f), parentFrame.RectTransform, Anchor.BottomCenter)
                {
                    RelativeOffset = switchOffsets[i] - new Vector2(0, 0.5f) // Adjust position above each button
                }, style: null);

                // Add a light indicator above the switch
                var refillOxygenIndicator = new GUITickBox(new RectTransform(Vector2.One, indicatorArea.RectTransform, Anchor.Center),
                    TextManager.Get("oxygeneratornui.refill"), font: GUIStyle.SubHeadingFont, style: "IndicatorLightPower")
                {
                    CanBeFocused = false,
                    Selected = false // Start unlit
                };

                // Create each switch button
                var refillOxygenSwitch = new GUIButton(
                    new RectTransform(new Vector2(1, 1), parentFrame.RectTransform, Anchor.Center)
                    {
                        RelativeOffset = switchOffsets[i]
                    },
                    string.Empty,
                    style: "SwitchDieHardClassic"
                )
                {
                    Selected = false,
                    Enabled = true,
                    ClickSound = GUISoundType.UISwitch,
                    OnClicked = (button, data) =>
                    {
                        button.Selected = !button.Selected;

            // Toggle the indicator light based on the button's selected state
            refillOxygenIndicator.Selected = button.Selected;

            // Send a message to the server to update the charging status for this slot
            bool startCharging = button.Selected;
            // Update local charging state for immediate feedback
            isChargingOxygenTank[index] = startCharging;
            
            if (GameMain.Client != null)
                    {
                        //unsentChanges = true;
                        correctionTimer = Sonar.CorrectionDelay;
                        SendToggleChargingOxygenTankMessage(item, index, startCharging);
                    }

                        return true;
                    }
                };
            }

#endif
        }

        public override void Update(float deltaTime, Camera cam)
        {
            // 1. 初始化流量显示
    CurrRecycleFlow = 0.0f;
    CurrFlow = 0.0f;
    CurrPurifyingFlow = 0.0f;
    CurrHeatingFlow = 0.0f;
    CurrAirPressureRegulatingFlow = 0.0f;

    if (item.CurrentHull == null) { return; }

    // 2. 检查关键停止条件：断电 OR 手动关闭 OR 没氧烛 OR 氧烛烧完
    oxygenCandle = secondItemContainer.Inventory.GetItemAt(0) as Item;
    
    // 【关键修改】：将电力检查（HasPower）整合进停止条件
    // 原版的 HasPower 逻辑通常是：IsActive && Voltage >= MinVoltage
    bool canWork = turnedOn && HasPower && oxygenCandle != null && oxygenCandle.Condition > 0;

    if (!canWork)
    {
        // 强制清空所有产出
        UpdateVents(0, 0, 0, 0, 0, deltaTime);
        return; // 彻底停止后续计算
    }
             
                    int activeRefillSlots = 0;

                    for (int i = 0; i < 5; i++)
                    {
                        if (isChargingOxygenTank[i] && oxygenCandle != null && oxygenCandle.Condition > 0)
                        {
                            // Get the oxygen tank in the corresponding slot (adjust slot index as needed)
                            Item oxygenTank = firstItemContainer.Inventory.GetItemAt(i) as Item;
                            if (oxygenTank != null && oxygenTank.Condition < oxygenTank.MaxCondition)
                            {
                                // Increase the condition of the oxygen tank, representing a recharge
                                oxygenTank.Condition += 0.3f * deltaTime;  // Adjust rate as necessary
                                
                                
                            }
                            activeRefillSlots++;  // Count the active refill slot
                            oxygenCandle.Condition -= 0.1f * deltaTime; // Candle burns down gradually
                        }
                    }

                    // Calculate flow reduction factor: 5% reduction per active refill slot
                    float refillPenaltyFactor = 1.0f - (activeRefillSlots * 0.1f);

                    // Apply the penalty factor to each flow type
                    CurrFlow = Math.Min(PowerConsumption > 0 ? Voltage : 1.0f, MaxOverVoltageFactor) * generatedAmount * 10.0f;
                    float conditionMult = item.Condition / item.MaxCondition;

                    CurrFlow *= conditionMult * conditionMult * newGeneratedAmountFactor * refillPenaltyFactor;
                    
                    CurrRecycleFlow = Math.Min(PowerConsumption > 0 ? Voltage : 1.0f, MaxOverVoltageFactor) * recycledAmount;
                    CurrRecycleFlow *= conditionMult * conditionMult * newGeneratedAmountFactor;
                    
                    CurrPurifyingFlow = Math.Min(PowerConsumption > 0 ? Voltage : 1.0f, MaxOverVoltageFactor) * purifyingAmount;
                    CurrPurifyingFlow *= conditionMult * conditionMult * newGeneratedAmountFactor;

                    CurrHeatingFlow = Math.Min(PowerConsumption > 0 ? Voltage : 1.0f, MaxOverVoltageFactor) * heatingAmount;
                    CurrHeatingFlow *= conditionMult * conditionMult * newGeneratedAmountFactor;

                    CurrAirPressureRegulatingFlow = Math.Min(PowerConsumption > 0 ? Voltage : 1.0f, MaxOverVoltageFactor) * recycledAmount;
                    CurrAirPressureRegulatingFlow *= conditionMult * conditionMult * newGeneratedAmountFactor;

                    UpdateVents(CurrFlow, CurrRecycleFlow, CurrPurifyingFlow, CurrHeatingFlow, CurrAirPressureRegulatingFlow, deltaTime);


                if (item.InPlayerSubmarine)
                {
                    oxygenCandle.Condition = oxygenCandle.Condition - 0.01f * newGeneratedAmountFactor * deltaTime;
                }
                
            
            item.SendSignal(CurrFlow.ToString(), "oxygen_generated_amount_out");

                
            //DebugConsole.NewMessage(this.CurrFlow.ToString("F0"));

        }

        private void UpdateVents(float deltaOxygen, float deltaCO2, float deltaPurify, float deltaHeat, float deltaAirPressure, float deltaTime)
        {
            if (ventList == null || ventUpdateTimer < 0.0f)
            {
                GetVents();
                ventUpdateTimer = VentUpdateInterval;
            }
            ventUpdateTimer -= deltaTime;

            if (!ventList.Any() || totalHullVolume <= 0.0f) { return; }

            foreach ((Vent vent, float hullVolume) in ventList)
            {
                if (vent.Item.CurrentHull == null) { continue; }

                // 获取该 vent 对应的实例数据
                var data = VentMod.GetVentData(vent);

                // 独立赋值，不再互相覆盖
                vent.OxygenFlow = deltaOxygen * (hullVolume / totalHullVolume);
                data.Co2Flow = deltaCO2 * (hullVolume / totalHullVolume); 
                data.PurifyingFlow = deltaPurify * (hullVolume / totalHullVolume);
                data.HeatFlow = deltaHeat * (hullVolume / totalHullVolume);
                data.DeltaAirPressure = deltaAirPressure * (hullVolume / totalHullVolume);

                vent.IsActive = true;
            }
        }


#if CLIENT
        private float flickerTimer;
        private readonly float flickerFrequency = 1;

        private float isActiveLockTimer;
        
        public override void UpdateHUDComponentSpecific(Character character, float deltaTime, Camera cam)
        {
            
            PowerButton.Enabled = isActiveLockTimer <= 0.0f;
            if (HasPower)
            {
                flickerTimer = 0;
                powerLight.Selected = IsActive;
            }
            else if (IsActive)
            {
                flickerTimer += deltaTime;
                if (flickerTimer > flickerFrequency)
                {
                    flickerTimer = 0;
                    powerLight.Selected = !powerLight.Selected;
                }
            }
            else
            {
                flickerTimer = 0;
                powerLight.Selected = false;
            }
            
            powerConsumptionDisplay = this.GetCurrentPowerConsumption(this.powerIn) * newGeneratedAmountFactor;
            o2Production = this.CurrFlow;
            
        }

        
        private void SendToggleOxygenGeneratorMessage(Item item, bool turnedOn)
        {
            IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.CUSTOM_OXYGENGENERATOR_TOGGLE);

            msg.WriteUInt16(item.ID); // ID of the oxygen generator item
            msg.WriteBoolean(turnedOn); // New IsActive state
            NetUtil.SendServer(msg, DeliveryMethod.Reliable);
        }

        private void SendGeneratedAmountFactorMessage(Item item, float newGeneratedAmountFactor)
        {
            IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.CUSTOM_OXYGENGENERATOR_GENERATEDAMOUNTFACTOR);

            msg.WriteUInt16(item.ID); // ID of the oxygen generator item
            msg.WriteSingle(newGeneratedAmountFactor); // New generated amount factor
            NetUtil.SendServer(msg, DeliveryMethod.Reliable);
        }


        private void SendToggleChargingOxygenTankMessage(Item item, int index, bool startCharging)
        {
            IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.CUSTOM_OXYGENGENERATOR_REFILLTOGGLE);
            msg.WriteUInt16(item.ID);        // ID of the oxygen generator item
            msg.WriteInt32(index);           // Index of the oxygen tank slot
            msg.WriteBoolean(startCharging); // Charging state to set
            NetUtil.SendServer(msg, DeliveryMethod.Reliable);
        }
#endif
        public override float GetCurrentPowerConsumption(Connection connection = null)
        {
            if (connection != this.powerIn || !IsActive || !turnedOn)
            {
                return 0;
            }

            float consumption = powerConsumption * newGeneratedAmountFactor;

            //consume more power when in a bad condition
            item.GetComponent<Repairable>()?.AdjustPowerConsumption(ref consumption);
            return consumption;
        }

        public static void OnReceiveToggleOxygenGeneratorMessage(object[] args)
        {
            IReadMessage msg = (IReadMessage)args[0];
            ushort itemId = msg.ReadUInt16();
            bool isActive = msg.ReadBoolean();

            Item oxygenGeneratorItem = Entity.FindEntityByID(itemId) as Item;
            if (oxygenGeneratorItem != null)
            {
                var oxygenGenerator = oxygenGeneratorItem.GetComponent<CustomOxygenGenerator>();
                if (oxygenGenerator != null)
                {
                    oxygenGenerator.turnedOn = isActive;
                    // Additional logic if needed
                }
            }
        }

        public static void OnReceiveGeneratedAmountFactorMessage(object[] args)
        {
            IReadMessage msg = (IReadMessage)args[0];
            ushort itemId = msg.ReadUInt16();
            float newGeneratedAmountFactor = msg.ReadSingle();

            Item oxygenGeneratorItem = Entity.FindEntityByID(itemId) as Item;
            if (oxygenGeneratorItem != null)
            {
                var oxygenGenerator = oxygenGeneratorItem.GetComponent<CustomOxygenGenerator>();
                if (oxygenGenerator != null)
                {
                    oxygenGenerator.newGeneratedAmountFactor = newGeneratedAmountFactor;
                    
                }
            }

        }


        public static void OnReceiveToggleChargingOxygenTankMessage(object[] args)
        {
            IReadMessage msg = (IReadMessage)args[0];
            ushort itemId = msg.ReadUInt16();
            int slotIndex = msg.ReadInt32();
            bool startCharging = msg.ReadBoolean();

            Item oxygenGeneratorItem = Entity.FindEntityByID(itemId) as Item;
            
            if (oxygenGeneratorItem != null)
            {
                var oxygenGenerator = oxygenGeneratorItem.GetComponent<CustomOxygenGenerator>();
                if (oxygenGenerator != null)
                {
                    // Update charging state
                    oxygenGenerator.isChargingOxygenTank[slotIndex] = startCharging;
                    
                }
            }
        }
    }
}
