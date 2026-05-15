using Barotrauma.Networking;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.MapCreatures.Behavior;
using Barotrauma.Items.Components;
using Barotrauma.Extensions;

using HarmonyLib;
using Barotrauma;


namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(Hull))]
    class HullMod
    {
        // Kind of buggy for some light mass items.
        /*[HarmonyPatch("ApplyFlowForces")]
        [HarmonyPrefix]
        public static bool ApplyFlowForces(Hull __instance, float deltaTime, Item item)
        {
            if (item.body.Mass <= 0.0f)
            {
                return false;
            }
            foreach (var gap in __instance.ConnectedGaps.Where(gap => gap.Open > 0))
            {
                var distance = MathHelper.Max(Vector2.DistanceSquared(item.Position, gap.Position) / 1000, 1f);
                Vector2 force = (gap.LerpedFlowForce / (distance / 15)) * deltaTime;
                if (force.LengthSquared() > 0.01f)
                {
                    item.body.ApplyForce(force * 100);
                }
            }
            return false;
        }*/

        //this is used to assign gas from the GasInfo class to each hull
        public static Dictionary<Hull, GasInfo> gasMap = new Dictionary<Hull, GasInfo>();

        [HarmonyPatch(MethodType.Constructor)]
        [HarmonyPatch(new Type[] { typeof(Rectangle), typeof(Submarine), typeof(ushort) })]
        [HarmonyPostfix]
        public static void HullConstructorPostfix(Hull __instance)
    {
        
        //DebugConsole.Log("Hull constructor has been patched.");
        float volume = __instance.Volume;

        //init each gas for each hull here
        GasInfo gasInfo = new GasInfo
            {
                Temperature = 300.0f, // Example value
                CO2 = 0f,
                CO = 0f,
                Nitrogen = Rand.Range(0f, 100.0f),
                NobleGas = Rand.Range(0f, 100.0f),
                Chlorine = 0f,
                PressurizedAir = 0f,
                // Pressurized Air
                GapOpenSum = 0.0f,
                OriginalAmbientLight = __instance.AmbientLight // 保存原始颜色
            };
            gasMap[__instance] = gasInfo;
        //__instance.ToxicGasPercentage = volume <= 0.0f ? 100.0f : __instance.toxicGas / volume * 100.0f;
    }
        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void Update(Hull __instance, float deltaTime, Camera cam)
        {
            Hull hull = __instance;
            if (hull == null) { return; }

            // Fire
            if (hull.FireSources.Count > 0)
            {
                AddGas(hull, "Temperature", 5f, deltaTime);
                AddGas(hull, "CO2", 20f, deltaTime);
                AddGas(hull, "CO", 10f, deltaTime);
            }

            // Temperature
            if (GetGas(hull, "Temperature") > 273.15f && hull.WaterPercentage > 0.3f)
            {
                AddGas(hull, "Temperature", -2.5f, deltaTime);
            }
            else if (GetGas(hull, "Temperature") > 273.15f)
            {
                AddGas(hull, "Temperature", -0.1f, deltaTime);
            }
            else if (GetGas(hull, "Temperature") > 318.15f && hull.WaterPercentage > 0.3f)
            {
                AddGas(hull, "Temperature", -5f, deltaTime);
            }

            //Pressurized Air
            if (GetGas(hull, "PressurizedAir") > 0)
            {
                AddGas(hull, "PressurizedAir", -20f, deltaTime);
            }

            // Calculate and store GapOpenSum
            float gapOpenSum = hull.ConnectedGaps
            .Where(g => g.linkedTo.Count == 1 && !g.IsHidden)
            .Sum(g => g.Open);
            

    // 温度颜色显示和GapOpenSum存储合并成一个TryGetValue
    if (gasMap.TryGetValue(hull, out GasInfo gasInfo))
    {
        // 存储 GapOpenSum
        gasInfo.GapOpenSum = gapOpenSum;
        
        // 温度颜色显示
        float temp = GetGas(hull, "Temperature");
        float tempCelsius = temp - 273.15f;
        
        // 定义安全温度范围
        float safeLower = 10f;
        float safeUpper = 30f;
        float maxColorTemp = 50f;
        float minColorTemp = -10f;

        Color coldColor = new Color(100, 150, 255, 120);  // 柔和的淡蓝色
        Color hotColor = new Color(255, 100, 100, 60);
        
        float t = 0f;
        Color targetColor;
        
        if (temp < 293.15f)
        {
            // 寒冷区域：从原始颜色渐变到蓝色
            t = MathHelper.Clamp((safeLower - tempCelsius) / (safeLower - minColorTemp), 0f, 1f);
            targetColor = Color.Lerp(gasInfo.OriginalAmbientLight, coldColor, t);
        }
        else if (temp > 303.15f)
        {
            // 炎热区域：从原始颜色渐变到红色
            t = MathHelper.Clamp((tempCelsius - safeUpper) / (maxColorTemp - safeUpper), 0f, 1f);
            targetColor = Color.Lerp(gasInfo.OriginalAmbientLight, hotColor, t);
        }
        else
        {
            // 安全温度范围：恢复原始颜色
            targetColor = gasInfo.OriginalAmbientLight;
        }
        
        hull.AmbientLight = targetColor;
        
        // 更新字典中的结构体
        gasMap[hull] = gasInfo;
    }

        }
        

        // these methods are used to get and set gas for each hull. They interact with the GasInfo class but are called from Hull class.
        public static float GetGas(Hull hull, string gasType)
        {
            if (gasMap.TryGetValue(hull, out GasInfo gasInfo))
            {
                return gasInfo.GetGasAmount(gasType);
            }
            return 0.0f; // Default value if not set
        }

        public static void SetGas(Hull hull, string gasType, float value)
        {
            if (gasMap.TryGetValue(hull, out GasInfo gasInfo))
            {
                gasInfo.SetGasAmount(gasType, value);
                gasMap[hull] = gasInfo;
            }
        }

        public static void AddGas(Hull hull, string gasType, float amount, float deltaTime)
        {
            if (gasMap.TryGetValue(hull, out GasInfo gasInfo))
            {
                // Apply delta time to the amount
                float adjustedAmount = amount * deltaTime;

                // Get current gas amount and calculate new amount
                float currentGasAmount = gasInfo.GetGasAmount(gasType);
                float newGasAmount = currentGasAmount + adjustedAmount;

                // Ensure the new gas amount does not go below zero
                newGasAmount = Math.Max(newGasAmount, 0.0f);

                // Set the new gas amount
                gasInfo.SetGasAmount(gasType, newGasAmount);

                // Update the gas information in the map
                gasMap[hull] = gasInfo;
            }
        }



        // This is the GasInfo class. It is used to store gas information for each hull.
        public struct GasInfo
        {
            public float Temperature;
            public float CO2;
            public float CO;
            public float Nitrogen;
            public float Chlorine;
            public float NobleGas;
            public float PressurizedAir;
            public float GapOpenSum;
            public Color OriginalAmbientLight;

            public float GetGasAmount(string gasType)
            {
                switch (gasType)
                {
                    case "Temperature":
                        return Temperature;
                    case "CO2":
                        return CO2;
                    case "CO":
                        return CO;
                    case "Nitrogen":
                        return Nitrogen;
                    case "Chlorine":
                        return Chlorine;
                    case "NobleGas":
                        return NobleGas;
                    case "PressurizedAir":
                        return PressurizedAir;
                    default:
                        return 0.0f;
                }
            }

            public void SetGasAmount(string gasType, float value)
            {
                switch (gasType)
                {
                    case "Temperature":
                        Temperature = value;
                        break;
                    case "CO2":
                        CO2 = value;
                        break;
                    case "CO":
                        CO = value;
                        break;
                    case "Nitrogen":
                        Nitrogen = value;
                        break;
                    case "Chlorine":
                        Chlorine = value;
                        break;
                    case "NobleGas":
                        NobleGas = value;
                        break;
                    case "PressurizedAir":
                        PressurizedAir = value;
                        break;
                }
            }

            public void AddGas(string gasType, float amount)
            {
                switch (gasType)
                {
                    case "Temperature":
                        Temperature += amount;
                        break;
                    case "CO2":
                        CO2 += amount;
                        break;
                    case "CO":
                        CO += amount;
                        break;
                    case "Nitrogen":
                        Nitrogen += amount;
                        break;
                    case "Chlorine":
                        Chlorine += amount;
                        break;
                    case "NobleGas":
                        NobleGas += amount;
                        break;
                    case "PressurizedAir":
                        PressurizedAir += amount;
                        break;
                }
            }
        }
    }
}
