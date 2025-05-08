using Barotrauma;
using Barotrauma.Items.Components;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using HarmonyLib;


using System.Globalization;
using System.Reflection;// for bindingflags

namespace BarotraumaDieHard
{

    partial class GapMod : IAssemblyPlugin
    {
        private Harmony harmony;

    
        public void Initialize()
        {
            harmony = new Harmony("GapMod");

            

            var originalUpdateOxygen = typeof(Gap).GetMethod("UpdateOxygen", BindingFlags.NonPublic | BindingFlags.Instance);
            var prefixUpdateOxygen = new HarmonyMethod(typeof(GapMod).GetMethod(nameof(UpdateOxygenPrefix), BindingFlags.Public | BindingFlags.Static));
            harmony.Patch(originalUpdateOxygen, prefixUpdateOxygen, null);


            var originalUpdateRoomToOut = typeof(Gap).GetMethod("UpdateRoomToOut", BindingFlags.NonPublic | BindingFlags.Instance);
            var postfixUpdateRoomToOut = new HarmonyMethod(typeof(GapMod).GetMethod(nameof(UpdateRoomToOutPostfix), BindingFlags.Public | BindingFlags.Static));
            harmony.Patch(originalUpdateRoomToOut, postfixUpdateRoomToOut, null);

            // For client graphic effect patch always remember to only allow patch in client side. Or desynic in multiplayer.
#if CLIENT
            var originalEmitParticles = typeof(Gap).GetMethod("EmitParticles", BindingFlags.NonPublic | BindingFlags.Instance);
            var prefixEmitParticles = new HarmonyMethod(typeof(GapMod).GetMethod(nameof(EmitParticlesPrefix), BindingFlags.Public | BindingFlags.Static));
            harmony.Patch(originalEmitParticles, prefixEmitParticles, null);
#endif
            
        }

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }
        
        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
            
        }
        private static float updateTimer = 0.0f;
        private static float updateInterval = 1f;
        private static float TemperatureDistributionSpeed = 1f;
        private static float GasDistributionspeed = 50f;

        private static float PressureDistributionspeed = 1000f;

        public static bool UpdateOxygenPrefix(Gap __instance, Hull hull1, Hull hull2, float deltaTime)
        {
            
                Gap _ = __instance;

                if (hull1 == null || hull2 == null) { return false; }
                
                // Oxygen Exchange

                if (_.IsHorizontal)
                {
                    // If the water level is above the gap, oxygen doesn't circulate
                    if (Math.Max(hull1.WorldSurface + hull1.WaveY[hull1.WaveY.Length - 1], hull2.WorldSurface + hull2.WaveY[0]) > _.WorldRect.Y) { return false; }
                }

                float totalOxygen = hull1.Oxygen + hull2.Oxygen;
                float totalVolume = hull1.Volume + hull2.Volume;
                
                float deltaOxygen = (totalOxygen * hull1.Volume / totalVolume) - hull1.Oxygen;
                deltaOxygen = MathHelper.Clamp(deltaOxygen, -Hull.OxygenDistributionSpeed * deltaTime, Hull.OxygenDistributionSpeed * deltaTime);

                hull1.Oxygen += deltaOxygen;
                hull2.Oxygen -= deltaOxygen;

                
                // Temperature Exchange

                float totalTempreture = HullMod.GetGas(hull1, "Temperature") + HullMod.GetGas(hull2, "Temperature");
                float averageTemperature = totalTempreture / 2f;  // Calculate average temp
                float deltaTempreture = averageTemperature - HullMod.GetGas(hull1, "Temperature"); // Adjust delta

                deltaTempreture = MathHelper.Clamp(deltaTempreture, -GapMod.TemperatureDistributionSpeed * deltaTime, GapMod.TemperatureDistributionSpeed * deltaTime);

                HullMod.AddGas(hull1, "Temperature", deltaTempreture, 1f); // Old AddGas use the last parameter to time delta time. But since I did delta time here, just put 1f to keep the value.
                HullMod.AddGas(hull2, "Temperature", -deltaTempreture, 1f);

                ExchangeGas(hull1, hull2, "CO2", deltaTime);
                ExchangeGas(hull1, hull2, "CO", deltaTime);
                ExchangeGas(hull1, hull2, "Chlorine", deltaTime);
                ExchangeAirPressure(hull1, hull2, "PressurizedAir", deltaTime);
            
            //ExchangeGas(hull1, hull2, "NobleGas", deltaTime);



            return false;
        }


        public static void ExchangeGas(Hull hull1, Hull hull2, string gasType, float deltaTime)
        {
            
            float gasInHull1 = HullMod.GetGas(hull1, gasType);
            float gasInHull2 = HullMod.GetGas(hull2, gasType);

            

            float totalVolume = hull1.Volume + hull2.Volume;
            float totalGas = gasInHull1 + gasInHull2;

            float deltaGas = (totalGas * hull1.Volume / totalVolume) - gasInHull1;
            deltaGas = MathHelper.Clamp(deltaGas, -GapMod.GasDistributionspeed * deltaTime, GapMod.GasDistributionspeed * deltaTime);

            HullMod.AddGas(hull1, gasType, deltaGas, 1f);
            HullMod.AddGas(hull2, gasType, -deltaGas, 1f);
        }

        // Treat air pressure as normal gas. We will check air pressure ratio in character.
        public static void ExchangeAirPressure(Hull hull1, Hull hull2, string gasType, float deltaTime)
        {
            
            float gasInHull1 = HullMod.GetGas(hull1, gasType);
            float gasInHull2 = HullMod.GetGas(hull2, gasType);

            

            float totalVolume = hull1.Volume + hull2.Volume;
            float totalGas = gasInHull1 + gasInHull2;

            float deltaGas = (totalGas * hull1.Volume / totalVolume) - gasInHull1;
            deltaGas = MathHelper.Clamp(deltaGas, -GapMod.PressureDistributionspeed * deltaTime, GapMod.PressureDistributionspeed * deltaTime);

            // Let us lose more concentrated air if two hulls are connected so we don't instantly pressurize all hulls.
            HullMod.AddGas(hull1, gasType, deltaGas / 10f, 1f);
            HullMod.AddGas(hull2, gasType, -deltaGas * 10f, 1f);
        }


        // This part adds pressure air build up logics.
        public static void UpdateRoomToOutPostfix(float deltaTime, Hull hull1, Gap __instance)
        {
            Gap _ = __instance;
            // Iterate through linked hulls to access their properties
            foreach (var linkedObject in __instance.linkedTo)
            {
                
                if (linkedObject is Hull hull)
                {
                    var gapOpenSum = HullMod.gasMap[hull].GapOpenSum;
                    // Check if the hull's gas level is above a threshold
                    if (gapOpenSum > 0.1)
                    {
                        float normalAirPressureFactor = Math.Max(0, hull.Submarine.RealWorldDepth) / 100f;
                        float normalHullVolume = hull.Volume / 10000f;
                        float normalHullPressure = normalHullVolume * normalAirPressureFactor + 1f;
                        float airPressure = HullMod.GetGas(hull, "PressurizedAir");
                        float hullPressureRatio = airPressure / normalHullPressure; 
    
                        // Apply a reduction of pressurized air as it escapes through the gap
                        HullMod.AddGas(hull, "PressurizedAir", -200f * normalAirPressureFactor *  gapOpenSum, deltaTime);

                        

                        
                        if (hullPressureRatio > 5.0f)
                        {
                            DebugConsole.NewMessage($"target hull: {_.flowTargetHull.RoomName}");
                            DebugConsole.NewMessage($"hull: {hull.RoomName}");
                            // Calculate the force based on pressurized air amount and the gap open sum
                            float forceMultiplier = hullPressureRatio * gapOpenSum * 1000f;

                            // Ensure the force is applied in a direction pushing the water out of the hull
                            Vector2 flowDirection = (hull.WorldPosition - _.WorldPosition); // Direction from hull to target (gap direction)
                            flowDirection.Normalize(); // Normalize to get direction vector
                            Vector2 flowForce = flowDirection * forceMultiplier * deltaTime;

                            // Apply the calculated flow force to the hull
                            hull.WaterVolume -= Math.Min(flowForce.Length() * 10f, hull.WaterVolume); // Ensure water volume doesn't go negative
                            DebugConsole.NewMessage($"water out: {flowForce.Length()}");
                        }
                    }
                }
            }
        }

    }

}

