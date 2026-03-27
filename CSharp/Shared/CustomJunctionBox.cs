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
        public bool BrokenFuse { 
            get => brokenFuse; 
            set 
            { 
                // Only update if the value has changed
                if (value != brokenFuse) {
                    brokenFuse = value;
                    flagConnections(item.Connections);                    
                }
            }
        }

        private bool brokenFuse = false;
        
        private bool leverState = true;

        public CustomJunctionBox(Item item, ContentXElement element) : base(item, element) 
        {
#if CLIENT
            InitProjectSpecificAdditional(element);
#endif
#if SERVER
            NetUtil.Register(NetEvent.SWITCH_JUNCTIONBOX, OnReceiveJBSwitchMessage);
#endif
        }

        // Update check fuse condition and update flag
        public override void Update(float deltaTime, Camera cam) 
        {
            RefreshConnections();

            if (Timing.TotalTime > extraLoadSetTime + 1.0)
            {
                //Decay the extra load to 0 from either positive or negative
                if (extraLoad > 0)
                {
                    extraLoad = Math.Max(extraLoad - 1000.0f * deltaTime, 0);
                }
                else
                {
                    extraLoad = Math.Min(extraLoad + 1000.0f * deltaTime, 0);
                }
            }
            // Check JB inventory
            ItemInventory inv = item.OwnInventory;
            if (inv != null) 
            {

                // Get condition of the first item in the JB inventory
                Item? invItem = inv.GetItemAt(0);
                float itemCond = invItem?.Condition ?? 0.0f;
                
                if (itemCond <= 0f || leverState == false)
                {
                    BrokenFuse = true;
                    
                }
                else
                {
                    BrokenFuse = false;
                }
            }

            if (!CanTransfer) { return; }

            if (isBroken)
            {
                SetAllConnectionsDirty();
                isBroken = false;
            }

            ApplyStatusEffects(ActionType.OnActive, deltaTime);

            float powerReadingOut = 0;
            float loadReadingOut = ExtraLoad;
            if (powerLoad < 0)
            {
                powerReadingOut = -powerLoad;
                loadReadingOut = 0;
            }

            if (powerOut != null && powerOut.Grid != null)
            {
                powerReadingOut = powerOut.Grid.Power;
                loadReadingOut = powerOut.Grid.Load;
            }

            if (prevSentPowerValue != (int)powerReadingOut || powerSignal == null)
            {
                prevSentPowerValue = (int)Math.Round(powerReadingOut);
                powerSignal = prevSentPowerValue.ToString();
            }
            if (prevSentLoadValue != (int)loadReadingOut || loadSignal == null)
            {
                prevSentLoadValue = (int)Math.Round(loadReadingOut);
                loadSignal = prevSentLoadValue.ToString();
            }
            item.SendSignal(powerSignal, "power_value_out");
            item.SendSignal(loadSignal, "load_value_out");

            //if the item can't be fixed, don't allow it to break
            if (!item.Repairables.Any() || !CanBeOverloaded) { return; }

            float maxOverVoltage = Math.Max(OverloadVoltage, 1.0f);

            Overload = Voltage > maxOverVoltage;

            if (Overload && (GameMain.NetworkMember == null || GameMain.NetworkMember.IsServer))
            {
                if (overloadCooldownTimer > 0.0f)
                {
                    overloadCooldownTimer -= deltaTime;
                    return;
                }
                Item? invItem = inv.GetItemAt(0);

                // Overload voltage damage the fuse.
                //92.2 Made the damage only occurs in player's subamrine
                if (item.InPlayerSubmarine)
                {
                    invItem.Condition -= 1f * Rand.Range(0.1f, 1f) * deltaTime;
                }
                
                
                //damage the item if voltage is too high (except if running as a client)
                float prevCondition = item.Condition;
                //some randomness to prevent all junction boxes from breaking at the same time
                if (Rand.Range(0.0f, 1.0f) < 0.01f && invItem.Condition <= 0.0f)
                {
                    //damaged boxes are more sensitive to overvoltage (also preventing all boxes from breaking at the same time)
                    float conditionFactor = MathHelper.Lerp(5.0f, 1.0f, item.Condition / item.MaxCondition);
                    item.Condition -= deltaTime * Rand.Range(10.0f, 500.0f) * conditionFactor;
                }
                if (item.Condition <= 0.0f && prevCondition > 0.0f)
                {
                    overloadCooldownTimer = OverloadCooldown;
#if CLIENT
                    SoundPlayer.PlaySound("zap", item.WorldPosition, hullGuess: item.CurrentHull);
                    Vector2 baseVel = Rand.Vector(300.0f);
                    for (int i = 0; i < 10; i++)
                    {
                        var particle = GameMain.ParticleManager.CreateParticle("spark", item.WorldPosition,
                            baseVel + Rand.Vector(100.0f), 0.0f, item.CurrentHull);
                        if (particle != null) particle.Size *= Rand.Range(0.5f, 1.0f);
                    }
#endif
                    float currentIntensity = GameMain.GameSession?.EventManager != null ?
                        GameMain.GameSession.EventManager.CurrentIntensity : 0.5f;

                    //higher probability for fires if the current intensity is low
                    if (FireProbability > 0.0f &&
                        Rand.Range(0.0f, 1.0f) < MathHelper.Lerp(FireProbability, FireProbability * 0.1f, currentIntensity))
                    {
                        new FireSource(item.WorldPosition);

            
                    }      
                }
            
            }
        }

        // Flag to the power grid that connections need to be updated (Important for the power grid cache to know)
        private void flagConnections(List<Connection> connections) 
        {
            foreach (Connection c in connections)
                {
                    
                    if (c.IsPower)
                    {
                        Powered.ChangedConnections.Add(c);
                        foreach (Connection conn in c.Recipients)
                        {
                            Powered.ChangedConnections.Add(conn);
                        }
                    }
                }
        }

        private void OnReceiveJBSwitchMessage(object[] args)
        {
                IReadMessage msg = (IReadMessage)args[0];
                // Extract data from the message
                ushort itemId = msg.ReadUInt16();
                bool receivedLeverState = msg.ReadBoolean();
                //DebugConsole.Log("Message received");
                // Find the torpedo tube item and update its state
                Item junctionBox = Entity.FindEntityByID(itemId) as Item;
                if (junctionBox != null)
                {
                    var component = junctionBox.GetComponent<CustomJunctionBox>();
                    if (component != null)
                    {
                        component.leverState = receivedLeverState;
                        // Additional logic if needed
                    }
                }
        }
    }
}