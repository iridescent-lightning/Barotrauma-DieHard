using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using System;
using System.Linq;
using FarseerPhysics;

using HarmonyLib;
using System.Reflection;
using System.Xml.Linq;

using Barotrauma;
using BarotraumaDieHard;


namespace BarotraumaDieHard.AI
{
    class IndoorsSteeringManagerDieHard  : IAssemblyPlugin
    {


        public Harmony harmony;
        
        
        public void Initialize()
        {
            harmony = new Harmony("IndoorsSteeringManagerDieHard");

            var originalCanAccessDoor = typeof(IndoorsSteeringManager).GetMethod("CanAccessDoor", BindingFlags.Public | BindingFlags.Instance);
            var prefixCanAccessDoor = typeof(IndoorsSteeringManagerDieHard).GetMethod(nameof(CanAccessDoorPrefix), BindingFlags.Public | BindingFlags.Static);

            harmony.Patch(originalCanAccessDoor, new HarmonyMethod(prefixCanAccessDoor), null);
            
        }

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }


        public static bool CanAccessDoorPrefix(Door door, Func<Controller, bool> buttonFilter, IndoorsSteeringManager __instance, ref bool __result)
        {
            // 获取门所在的房间
            Hull doorHull = door.item.FindHull();
            
            // 基础检查：如果找不到房间或不是压载舱，直接跳过拦截逻辑（允许通行）
            if (doorHull == null) return true;

            // 检查逻辑：
            // 1. 是否是压载舱 (Ballast)
            // 2. 是否是湿室 (IsWetRoom)
            // 3. 关键：压力是否超过了安全阈值 (这里假设 30 为高压警戒线)
            bool isBallast = doorHull.RoomName.ToString().Contains("ballast", StringComparison.OrdinalIgnoreCase);
            float currentPressure = HullMod.GetGas(doorHull, "PressurizedAir");

            // 设定一个固定的高压阈值，不再受深度影响
            const float SafetyPressureThreshold = 30.0f;

            if (isBallast && doorHull.IsWetRoom && currentPressure > SafetyPressureThreshold)
            {
                // 只有在满足上述所有条件时，才阻止机器人并让其说话
                __instance.character.Speak(
                    TextManager.Get("dialog.bots.cannotaccesswetroomdoor").Value, 
                    null, 0.0f, "cannotaccesswetroomdoor".ToIdentifier(), 30.0f);
                    
                __result = false; // 告诉路径搜索：此路不通
                return false;     // 拦截原方法
            }

            // 其他情况（低压或非压载舱）允许原方法执行
            return true;
        }

        
    }
}