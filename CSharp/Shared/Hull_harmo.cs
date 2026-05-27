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
        // 保持原版的全局映射字典
        public static Dictionary<Hull, GasInfo> gasMap = new Dictionary<Hull, GasInfo>();

        [HarmonyPatch(MethodType.Constructor)]
        [HarmonyPatch(new Type[] { typeof(Rectangle), typeof(Submarine), typeof(ushort) })]
        [HarmonyPostfix]
        public static void HullConstructorPostfix(Hull __instance)
        {
            float volume = __instance.Volume;

            GasInfo gasInfo = new GasInfo
            {
                Temperature = 300.0f, 
                CO2 = 0f,
                CO = 0f,
                Nitrogen = Rand.Range(0f, 100.0f),
                NobleGas = Rand.Range(0f, 100.0f),
                Chlorine = 0f,
                PressurizedAir = 0f,
                GapOpenSum = 0.0f,
                OriginalAmbientLight = __instance.AmbientLight 
            };
            gasMap[__instance] = gasInfo;
        }

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void Update(Hull __instance, float deltaTime, Camera cam)
        {
            if (__instance == null) { return; }

            // ⭐【优化点 1】只进行一次 TryGetValue 提取引用，后续所有数据直接在内存中读写，干掉所有字符串查表开销
            if (!gasMap.TryGetValue(__instance, out GasInfo gasInfo)) return;

            // 1. 处理着火逻辑 (直接操作字段，比调用 AddGas 快数百倍)
            if (__instance.FireSources.Count > 0)
            {
                gasInfo.Temperature += 5f * deltaTime;
                gasInfo.CO2 += 20f * deltaTime;
                gasInfo.CO += 10f * deltaTime;
            }

            // 2. 处理温度物理衰减逻辑
            float currentTemp = gasInfo.Temperature;
            float waterPerc = __instance.WaterPercentage;

            if (currentTemp > 273.15f && waterPerc > 0.3f)
            {
                gasInfo.Temperature = Math.Max(0.0f, currentTemp - 1f * deltaTime);
            }
            else if (currentTemp > 273.15f)
            {
                gasInfo.Temperature = Math.Max(0.0f, currentTemp - 0.1f * deltaTime);
            }
            else if (currentTemp > 363.15f)
            {
                gasInfo.Temperature = Math.Max(0.0f, currentTemp - 0.35f * deltaTime);
            }
            else if (currentTemp > 318.15f && waterPerc > 0.3f)
            {
                gasInfo.Temperature = Math.Max(0.0f, currentTemp - 5f * deltaTime);
            }

            // 3. 处理高压空气衰减
            if (gasInfo.PressurizedAir > 0f)
            {
                gasInfo.PressurizedAir = Math.Max(0.0f, gasInfo.PressurizedAir - 20f * deltaTime);
            }

            // ⭐【优化点 2】彻底重构原先的 LINQ 表达式 (.Where.Sum)
            // 替换成标准的 for 循环遍历。这样做完全免去了每帧高频分配迭代器对象的巨额堆内存开销
            float gapOpenSum = 0.0f;
            var connectedGaps = __instance.ConnectedGaps;
            if (connectedGaps != null)
            {
                int gapCount = connectedGaps.Count;
                for (int i = 0; i < gapCount; i++)
                {
                    var g = connectedGaps[i];
                    if (g != null && g.linkedTo != null && g.linkedTo.Count == 1 && !g.IsHidden)
                    {
                        gapOpenSum += g.Open;
                    }
                }
            }

            // 存储更新后的 GapOpenSum
            gasInfo.GapOpenSum = gapOpenSum;
            
            // 4. 温度颜色显示逻辑优化
            float tempCelsius = gasInfo.Temperature - 273.15f;
            
            float safeLower = 10f;
            float safeUpper = 30f;
            float maxColorTemp = 50f;
            float minColorTemp = -10f;

            Color coldColor = new Color(100, 150, 255, 120);  
            Color hotColor = new Color(255, 100, 100, 60);
            
            Color targetColor;
            
            if (gasInfo.Temperature < 293.15f)
            {
                float t = MathHelper.Clamp((safeLower - tempCelsius) / (safeLower - minColorTemp), 0f, 1f);
                targetColor = Color.Lerp(gasInfo.OriginalAmbientLight, coldColor, t);
            }
            else if (gasInfo.Temperature > 303.15f)
            {
                float t = MathHelper.Clamp((tempCelsius - safeUpper) / (maxColorTemp - safeUpper), 0f, 1f);
                targetColor = Color.Lerp(gasInfo.OriginalAmbientLight, hotColor, t);
            }
            else
            {
                targetColor = gasInfo.OriginalAmbientLight;
            }
            
            __instance.AmbientLight = targetColor;
            
            // ⭐【优化点 3】由于 GasInfo 是结构体（值类型），最后重新赋回字典完成数据写回
            gasMap[__instance] = gasInfo;
        }
        

        // ====================================================================
        //  保留对外的桩函数，防止外部其他脚本依赖报错。内部也改用最速通道优化
        // ====================================================================
        public static float GetGas(Hull hull, string gasType)
        {
            if (hull != null && gasMap.TryGetValue(hull, out GasInfo gasInfo))
            {
                return gasInfo.GetGasAmount(gasType);
            }
            return 0.0f;
        }

        public static void SetGas(Hull hull, string gasType, float value)
        {
            if (hull != null && gasMap.TryGetValue(hull, out GasInfo gasInfo))
            {
                gasInfo.SetGasAmount(gasType, value);
                gasMap[hull] = gasInfo;
            }
        }

        public static void AddGas(Hull hull, string gasType, float amount, float deltaTime)
        {
            if (hull != null && gasMap.TryGetValue(hull, out GasInfo gasInfo))
            {
                float currentGasAmount = gasInfo.GetGasAmount(gasType);
                float newGasAmount = Math.Max(currentGasAmount + (amount * deltaTime), 0.0f);
                gasInfo.SetGasAmount(gasType, newGasAmount);
                gasMap[hull] = gasInfo;
            }
        }

        // 保持数据结构不变，兼容旧数据
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
                    case "Temperature": return Temperature;
                    case "CO2": return CO2;
                    case "CO": return CO;
                    case "Nitrogen": return Nitrogen;
                    case "Chlorine": return Chlorine;
                    case "NobleGas": return NobleGas;
                    case "PressurizedAir": return PressurizedAir;
                    default: return 0.0f;
                }
            }

            public void SetGasAmount(string gasType, float value)
            {
                switch (gasType)
                {
                    case "Temperature": Temperature = value; break;
                    case "CO2": CO2 = value; break;
                    case "CO": CO = value; break;
                    case "Nitrogen": Nitrogen = value; break;
                    case "Chlorine": Chlorine = value; break;
                    case "NobleGas": NobleGas = value; break;
                    case "PressurizedAir": PressurizedAir = value; break;
                }
            }
        }
    }
}