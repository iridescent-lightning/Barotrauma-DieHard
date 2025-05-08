using System.Reflection;
using Barotrauma;
using Barotrauma.Items.Components;
using HarmonyLib;

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;


namespace BarotraumaDieHard
{
    class PowerTransferMod : IAssemblyPlugin
    {
        public Harmony harmony;
        
        private static Dictionary<int, FuseMap> fuseInfo = new Dictionary<int, FuseMap>();
        private static float updateTimer = 1f;
        private static float escapedTime;

        

        public void Initialize()
        {
            harmony = new Harmony("PowerTransferMod");

            var originalConstructor = typeof(PowerTransfer).GetConstructor(new[] { typeof(Item), typeof(ContentXElement) });
            var postfix = new HarmonyMethod(typeof(PowerTransferMod).GetMethod(nameof(PowerTransferConstructorPostfix)));
            harmony.Patch(originalConstructor, null, postfix);

            harmony.Patch(
                original: typeof(PowerTransfer).GetMethod("Update"),
                prefix: new HarmonyMethod(typeof(PowerTransferMod).GetMethod(nameof(Updateprefix)))
            );
            
        }

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }

        public struct FuseMap
        {
            public bool brokenFuse;
            public ItemContainer itemContainer;

        }
        
        public static void PowerTransferConstructorPostfix(PowerTransfer __instance)
        {
            PowerTransfer _ = __instance;
            FuseMap fuseMap = new FuseMap();
            fuseMap.itemContainer = _.item.GetComponent<ItemContainer>();
            fuseMap.brokenFuse = false;
            fuseInfo.Add(_.item.ID, fuseMap);
            
        }
        private static void flagConnections(List<Connection> connections) 
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

        public static bool Updateprefix(float deltaTime, Camera cam, PowerTransfer __instance)
        {
            PowerTransfer _ = __instance;

            escapedTime += deltaTime;
            if (escapedTime > updateTimer)
            {
                if (__instance.item.HasTag("junctionbox"))
                {
                    
                    var fuse = fuseInfo[_.item.ID].itemContainer.Inventory.GetItemAt(0);
                    
                    if (fuse == null || fuse.Condition <= 0)
                    {
                        fuseInfo[_.item.ID].brokenFuse = true;
                        
                        
                    }
                    else
                    {
                        fuseInfo[_.item.ID].brokenFuse = false;
                    }
                }
                escapedTime = 0;
            }
            _.RefreshConnections();

            if (Timing.TotalTime > _.extraLoadSetTime + 1.0)
            {
                //Decay the extra load to 0 from either positive or negative
                if (_.extraLoad > 0)
                {
                    _.extraLoad = Math.Max(_.extraLoad - 1000.0f * deltaTime, 0);
                }
                else
                {
                    _.extraLoad = Math.Min(_.extraLoad + 1000.0f * deltaTime, 0);
                }
            }

            if (!_.CanTransfer) { return false; }

            if (_.isBroken)
            {
                _.SetAllConnectionsDirty();
                _.isBroken = false;
            }

            _.ApplyStatusEffects(ActionType.OnActive, deltaTime);

            float powerReadingOut = 0;
            float loadReadingOut = _.ExtraLoad;
            if (_.powerLoad < 0)
            {
                powerReadingOut = -_.powerLoad;
                loadReadingOut = 0;
            }

            if (_.powerOut != null && _.powerOut.Grid != null)
            {
                powerReadingOut = _.powerOut.Grid.Power;
                loadReadingOut = _.powerOut.Grid.Load;
            }

            if (_.prevSentPowerValue != (int)powerReadingOut || _.powerSignal == null)
            {
                _.prevSentPowerValue = (int)Math.Round(powerReadingOut);
                _.powerSignal = _.prevSentPowerValue.ToString();
            }
            if (_.prevSentLoadValue != (int)loadReadingOut || _.loadSignal == null)
            {
                _.prevSentLoadValue = (int)Math.Round(loadReadingOut);
                _.loadSignal = _.prevSentLoadValue.ToString();
            }
            _.item.SendSignal(_.powerSignal, "power_value_out");
            _.item.SendSignal(_.loadSignal, "load_value_out");

            //if the item can't be fixed, don't allow it to break
            if (!_.item.Repairables.Any() || !_.CanBeOverloaded) { return false; }

            float maxOverVoltage = Math.Max(_.OverloadVoltage, 1.0f);

            _.Overload = _.Voltage > maxOverVoltage;

            if (_.Overload && (GameMain.NetworkMember == null || GameMain.NetworkMember.IsServer))
            {
                if (_.overloadCooldownTimer > 0.0f)
                {
                    _.overloadCooldownTimer -= deltaTime;
                    return false;
                }

                //damage the item if voltage is too high (except if running as a client)
                float prevCondition = _.item.Condition;
                //some randomness to prevent all junction boxes from breaking at the same time
                if (Rand.Range(0.0f, 1.0f) < 0.01f)
                {
                    //damaged boxes are more sensitive to overvoltage (also preventing all boxes from breaking at the same time)
                    float conditionFactor = MathHelper.Lerp(5.0f, 1.0f, _.item.Condition / _.item.MaxCondition);
                    _.item.Condition -= deltaTime * Rand.Range(10.0f, 500.0f) * conditionFactor;
                }
                if (_.item.Condition <= 0.0f && prevCondition > 0.0f)
                {
                    _.overloadCooldownTimer = PowerTransfer.OverloadCooldown;
#if CLIENT
                    SoundPlayer.PlaySound("zap", _.item.WorldPosition, hullGuess: _.item.CurrentHull);
                    Vector2 baseVel = Rand.Vector(300.0f);
                    for (int i = 0; i < 10; i++)
                    {
                        var particle = GameMain.ParticleManager.CreateParticle("spark", _.item.WorldPosition,
                            baseVel + Rand.Vector(100.0f), 0.0f, _.item.CurrentHull);
                        if (particle != null) particle.Size *= Rand.Range(0.5f, 1.0f);
                    }
#endif
                    float currentIntensity = GameMain.GameSession?.EventManager != null ?
                        GameMain.GameSession.EventManager.CurrentIntensity : 0.5f;

                    //higher probability for fires if the current intensity is low
                    if (_.FireProbability > 0.0f &&
                        Rand.Range(0.0f, 1.0f) < MathHelper.Lerp(_.FireProbability, _.FireProbability * 0.1f, currentIntensity))
                    {
                        new FireSource(_.item.WorldPosition);
                    }
                }
                
            }
            return false;
        }
    }
}
