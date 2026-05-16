﻿using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Items.Components;


using System.Reflection;


using Barotrauma;
using HarmonyLib;
using System.Globalization;

using Networking;



namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(Sonar))]
    partial class SonarMod
    {

        private static Dictionary<int, float>SonarRange = new Dictionary<int, float>();
        private static Dictionary<ushort, float> lastDamageTime = new Dictionary<ushort, float>();
		
		private static float PingFrequency = 0.5f;//how fast a ping spreads
		private static float timeSinceLastPing = 5.1f;
		private static float timeSinceLastDirectionalPing = 5.1f;

        //get user input for the directional ping
        private static float minAfflictionStrength = 0.1f;
        private static float maxAfflictionStrength = 1.0f;

        public static float NewSectorAngle { get; set; } = 120.0f;
        // Calculate the dot product whenever NewSectorAngle is changed
        public static float NewDotProduct => (float)Math.Cos(MathHelper.ToRadians(NewSectorAngle) *0.5f);

        private static float minHertzValue = 30000f; // Minimum hertz value
        private static float maxHertzValue = 500000f; // Maximum hertz value
        private static float hertz = 30000f; // Default hertz value


        /*var originalMouseInPingRing = typeof(Sonar).GetMethod("MouseInDirectionalPingRing", BindingFlags.NonPublic | BindingFlags.Instance);
        var prefixMouseInPingRing = typeof(SonarMod).GetMethod("MouseInDirectionalPingRing", BindingFlags.Public | BindingFlags.Static);
        harmony.Patch(originalMouseInPingRing, new HarmonyMethod(prefixMouseInPingRing), null);
        */
		public void PreInitPatching() 
        { 

        }

		

        [HarmonyPatch(MethodType.Constructor)]
        [HarmonyPatch(new Type[] { typeof(Item), typeof(ContentXElement) })]
        [HarmonyPostfix]
        public static void SonarConstructorPostfix(Item item, ContentXElement element, Sonar __instance)
        {
            
                
                // Store the reload value using the item ID as the key
                
                SonarRange[item.ID] = __instance.Range; // Store original reload value

        }
        

        [HarmonyPatch("Update")]
        [HarmonyPrefix]
         public static bool Update(float deltaTime, Camera cam, Sonar __instance)
        {
            
			Sonar _ = __instance;
            _.UpdateOnActiveEffects(deltaTime);
            
            if (_.UseTransducers)
            {
                foreach (Sonar.ConnectedTransducer transducer in _.connectedTransducers)//directly use Sonar. to get the field that is not in the context if __instance doesn't work.
                {
                    transducer.DisconnectTimer -= deltaTime;
                }
                _.connectedTransducers.RemoveAll(t => t.DisconnectTimer <= 0.0f);
            }

            for (var pingIndex = 0; pingIndex < _.activePingsCount; ++pingIndex)
            {
                _.activePings[pingIndex].State += deltaTime * PingFrequency;
            }

            if (_.currentMode == Sonar.Mode.Active && !_.useDirectionalPing)
            {
				timeSinceLastPing += deltaTime;
				
                if ((_.Voltage >= _.MinVoltage) &&
                    (!_.UseTransducers || _.connectedTransducers.Count > 0))
                {
                    if (_.currentPingIndex != -1)
                    {
                        var activePing = _.activePings[_.currentPingIndex];
                        if (activePing.State > 1.0f)
                        {
                            _.aiPingCheckPending = true;
                            _.currentPingIndex = -1;
                        }
                    }
                    if (_.currentPingIndex == -1 && _.activePingsCount < _.activePings.Length && timeSinceLastPing >= 5f)
                    {
						timeSinceLastPing = 0.0f;//reset
                        _.currentPingIndex = _.activePingsCount++;
                        if (_.activePings[_.currentPingIndex] == null)
                        {
                            _.activePings[_.currentPingIndex] = new Sonar.ActivePing();
                        }
                        _.activePings[_.currentPingIndex].IsDirectional = _.useDirectionalPing;
                        _.activePings[_.currentPingIndex].Direction = _.pingDirection;
                        _.activePings[_.currentPingIndex].State = 0.0f;
                        _.activePings[_.currentPingIndex].PrevPingRadius = 0.0f;
                        if (_.item.AiTarget != null)
                        {
                            _.item.AiTarget.SectorDegrees = _.useDirectionalPing ? Sonar.DirectionalPingSector : 360.0f;
                            _.item.AiTarget.SectorDir = new Vector2(_.pingDirection.X, -_.pingDirection.Y);
                        }
                        _.item.Use(deltaTime);
                    }
                }
                else
                {
                    _.aiPingCheckPending = false;
                }
            }
            //todo the ai target check should be revised to match the new directional ping
            if (_.currentMode == Sonar.Mode.Active && _.useDirectionalPing)
            {
				timeSinceLastDirectionalPing += deltaTime;
				
                if ((_.Voltage >= _.MinVoltage) &&
                    (!_.UseTransducers || _.connectedTransducers.Count > 0))
                {
                    if (_.currentPingIndex != -1)
                    {
                        var activePing = _.activePings[_.currentPingIndex];
                        if (activePing.State > 1.0f)
                        {
                            _.aiPingCheckPending = true;
                            _.currentPingIndex = -1;
                        }
                    }
                    if (_.currentPingIndex == -1 && _.activePingsCount < _.activePings.Length && timeSinceLastDirectionalPing >= 3.0f)
                    {
						timeSinceLastDirectionalPing = 0.0f;//reset
                        _.currentPingIndex = _.activePingsCount++;
                        if (_.activePings[_.currentPingIndex] == null)
                        {
                            _.activePings[_.currentPingIndex] = new Sonar.ActivePing();
                        }
                        _.activePings[_.currentPingIndex].IsDirectional = _.useDirectionalPing;
                        _.activePings[_.currentPingIndex].Direction = _.pingDirection;
                        _.activePings[_.currentPingIndex].State = 0.0f;
                        _.activePings[_.currentPingIndex].PrevPingRadius = 0.0f;
                        if (_.item.AiTarget != null)
                        {
                            _.item.AiTarget.SectorDegrees = _.useDirectionalPing ? SonarMod.NewSectorAngle : 360.0f;
                            _.item.AiTarget.SectorDir = new Vector2(_.pingDirection.X, -_.pingDirection.Y);
                            DebugConsole.NewMessage(_.item.AiTarget.SectorDegrees.ToString());
                        }
                        _.item.Use(deltaTime);
                    }
                }
                else
                {
                    _.aiPingCheckPending = false;
                }
            }

            for (var pingIndex = 0; pingIndex < _.activePingsCount;)
            {
                // 这里的 GetAITargets 是实例方法，需要通过 __instance 调用
                foreach (AITarget aiTarget in _.GetAITargets())
                {
                    float range = MathUtils.InverseLerp(aiTarget.MinSoundRange, aiTarget.MaxSoundRange, __instance.Range * __instance.activePings[pingIndex].State / __instance.zoom);
                    aiTarget.SectorDegrees = _.useDirectionalPing ? SonarMod.NewSectorAngle : 360.0f;
                    aiTarget.SectorDir = new Vector2(_.pingDirection.X, -_.pingDirection.Y);
                    aiTarget.SoundRange = Math.Max(aiTarget.SoundRange, MathHelper.Lerp(aiTarget.MinSoundRange, aiTarget.MaxSoundRange, range));
                }
                if (_.activePings[pingIndex].State > 1.0f)
                {
                    var lastIndex = --_.activePingsCount;
                    var oldActivePing = _.activePings[pingIndex];
                    _.activePings[pingIndex] = _.activePings[lastIndex];
                    _.activePings[lastIndex] = oldActivePing;
                    if (_.currentPingIndex == lastIndex)
                    {
                        _.currentPingIndex = pingIndex;
                    }
                }
                else
                {
                    ++pingIndex;
                }
            }
            

       
            foreach (Character c in Character.CharacterList)
            {
                Vector2 pingSource = _.item.WorldPosition;
                if (__instance.CurrentMode == Sonar.Mode.Active && __instance.useDirectionalPing)
                {
                    if (c.AnimController.CurrentHull != null || !c.Enabled) { continue; }
                    if (!c.IsUnconscious && c.Params.HideInSonar) { continue; }
                    if (_.DetectSubmarineWalls && c.AnimController.CurrentHull == null && _.item.CurrentHull != null) { continue; }

                    float newDotProduct = SonarMod.NewDotProduct;
                    Vector2 pingDirection = __instance.pingDirection; // Accessing the ping direction
                    pingDirection.Y = -pingDirection.Y;
                    foreach (Character target in Character.CharacterList)
                    {
                        float distance = Vector2.Distance(target.WorldPosition, pingSource);

                        if (!target.InWater || target.IsDead || target.CharacterHealth == null || target.CurrentHull != null ||  distance > _.Range * 0.5f) { continue; }

                        float pointDist = ((target.WorldPosition - pingSource) * 1f).LengthSquared();
                        
                        // DebugConsole.NewMessage((distance /100f).ToString());

                        Vector2 dirToTarget = Vector2.Normalize(target.WorldPosition - pingSource);
                        // Check if the target is within the directional ping sector
                        if (Vector2.Dot(dirToTarget, pingDirection) >= newDotProduct)
                        {
                            // Calculate the normalized distance (0 at the source, 1 at max range)
                            float distanceFactor = MathHelper.Clamp(1.0f - (distance / _.Range), 0.0f, 1.0f);

                            float currentHertz = SonarMod.hertz; // Make sure this reflects the slider value
                            float afflictionStrength = MathHelper.Lerp(SonarMod.minAfflictionStrength, SonarMod.maxAfflictionStrength, (currentHertz - SonarMod.minHertzValue) / (maxHertzValue - SonarMod.minHertzValue));

                            // Apply the range-based penalty to affliction strength
                            float adjustedAfflictionStrength = afflictionStrength * distanceFactor;
                            
                            
                            float currentTime = (float)Timing.TotalTime;
                            if (lastDamageTime.TryGetValue(target.ID, out float lastTime) && currentTime - lastTime < 2f)
                            {
                                continue; // 0.5秒内只造成一次伤害
                            }
                            lastDamageTime[target.ID] = currentTime;

                            target.CharacterHealth.ApplyAffliction(target.AnimController.MainLimb, AfflictionPrefab.Prefabs["sonardamage"].Instantiate(adjustedAfflictionStrength * 10f));
                            //DebugConsole.NewMessage($"SonarMod: {target.Name} DamageReceived: " + $"{afflictionStrength}", Color.White);
                            
                            // Debug log to show calculated affliction strength
                            //DebugConsole.NewMessage($"SonarMod: {target.Name} DamageReceived: {adjustedAfflictionStrength}", Color.White);
                            
                        }
                        
                    }
                }
            }

            return false;
        }

			
        
#if CLIENT
        private static void SendApplyDamageMessage(ushort characterId, string afflictionType, float afflictionStrength) 
        {
        IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.APPLY_SONAR_PING_DAMAGE);
        msg.WriteUInt16(characterId);
        msg.WriteString(afflictionType);
        msg.WriteSingle(afflictionStrength);
        NetUtil.SendServer(msg, DeliveryMethod.Reliable);
        }
#endif
        public static void OnReceiveSonarPingApplyDamageMessage(object[] args) 
        {
        IReadMessage msg = (IReadMessage)args[0];
        ushort characterId = msg.ReadUInt16();
        string afflictionType = msg.ReadString();
        float afflictionStrength = msg.ReadSingle();

        Character target = Entity.FindEntityByID(characterId) as Character;
        if (target != null && !target.IsDead) {
            AfflictionPrefab afflictionPrefab = AfflictionPrefab.Prefabs[afflictionType];
            if (afflictionPrefab != null) {
                target.CharacterHealth.ApplyAffliction(target.AnimController.MainLimb, afflictionPrefab.Instantiate(afflictionStrength));
                }
            }
        }
    
        public static void ResetOriginalSonarRange()
		{
			// Iterate through each stored reload value in the dictionary
			foreach (var sonarEntry in SonarRange)
			{
				// Get the turret's item ID and its original reload value
				int itemID = sonarEntry.Key;
				float originalRange = sonarEntry.Value;

				// Find the turret by item ID
				var navItem = Item.ItemList.FirstOrDefault(item => item.ID == itemID);

				// Check if the turret item exists and has a Turret component
				if (navItem != null)
				{
					var sonarComponent = navItem.GetComponent<Sonar>();
					if (sonarComponent != null)
					{
						// Reassign the original reload value to the turret
						sonarComponent.Range = originalRange;
						//DebugConsole.NewMessage($"Reset sonar {itemID}'s range to {originalRange}", Color.Green);
					}
				}
			}
		}


        public static void ClearSonarRangeDictionary()
		{
			
			SonarRange.Clear();
    		//DebugConsole.NewMessage("Turret reload values cleared.", Color.Red);
		}


        public static void OnReceiveChangeRangeMessage(object[] args)
        {
            IReadMessage msg = (IReadMessage)args[0];
            ushort itemId = msg.ReadUInt16(); // Read the item ID
            float newRange = msg.ReadSingle();
            float newSectorAngle = msg.ReadSingle();
            float newhertz = msg.ReadSingle();

            // Find the sonar item by ID
            Item sonarItem = Entity.FindEntityByID(itemId) as Item;
            if (sonarItem != null)
            {
                // Get the Sonar component from the item
                var sonar = sonarItem.GetComponent<Sonar>();
                if (sonar != null)
                {
                    
                    
                    // Update the sonar's range
                    sonar.Range = newRange;
                    NewSectorAngle = newSectorAngle;
                    hertz =newhertz;
                    //DebugConsole.NewMessage($"Sonar range updated to: {newRange}");
                }
            }
        }

    
    
    
    }
}    
    
    
