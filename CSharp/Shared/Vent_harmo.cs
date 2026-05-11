using System;
using System.Xml.Linq;

using Barotrauma.Items.Components;
using Barotrauma.Extensions;
using Barotrauma;
using HarmonyLib;
using System.Runtime.CompilerServices;

namespace BarotraumaDieHard
{

    public class VentData
    {
        public float Co2Flow = 0f;
        public float PurifyingFlow = 0f;
        public float HeatFlow = 0f;
        public float DeltaAirPressure = 0f;
    }


    [HarmonyPatch(typeof(Vent))]
    class VentMod
    {
        // 建立 Vent 实例与 VentData 的映射关系
        public static readonly ConditionalWeakTable<Vent, VentData> VentExtension = 
            new ConditionalWeakTable<Vent, VentData>();

        // 辅助方法：获取或创建数据实例
        public static VentData GetVentData(Vent vent)
        {
            return VentExtension.GetOrCreateValue(vent);
        }

        private static float updateTimer = 0.0f;
        private static float updateInterval = 0.1f;

        [HarmonyPatch("Update")]
        [HarmonyPrefix]
        public static bool Update(float deltaTime, Camera cam, Vent __instance)
        {
            if (__instance.item.CurrentHull == null || __instance.item.InWater) { return false; }

            // 获取当前 Vent 实例独有的数据
            var data = GetVentData(__instance);

            // 使用该实例的数据进行计算
            if (__instance.oxygenFlow > 0.0f)
            {
                __instance.ApplyStatusEffects(ActionType.OnActive, deltaTime);
            }

            __instance.item.CurrentHull.Oxygen += __instance.oxygenFlow * deltaTime;

            // 注意：这里不再使用全局静态变量，而是使用 data.Co2Flow
            HullMod.AddGas(__instance.item.CurrentHull, "CO2", -data.Co2Flow, deltaTime);
            HullMod.AddGas(__instance.item.CurrentHull, "CO", -data.PurifyingFlow, deltaTime);
            HullMod.AddGas(__instance.item.CurrentHull, "Chlorine", -data.PurifyingFlow, deltaTime);
            HullMod.AddGas(__instance.item.CurrentHull, "PressurizedAir", -data.DeltaAirPressure, deltaTime);

            if (HullMod.GetGas(__instance.item.CurrentHull, "Temperature") < 300f)
            {
                HullMod.AddGas(__instance.item.CurrentHull, "Temperature", data.HeatFlow * 1f, deltaTime);
            }
            
            return false; // 跳过原版 Update
        }
    }
}
