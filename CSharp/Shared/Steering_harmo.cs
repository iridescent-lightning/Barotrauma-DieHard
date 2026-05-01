
using Barotrauma.Networking;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Voronoi2;

using Barotrauma.Items.Components;

using Barotrauma;
using HarmonyLib;
using System.Globalization;
using System.Reflection;// for bindingflags

using Networking;

namespace BarotraumaDieHard//todo make a structural namespace DieHard.Item.Components. namespace can't be used in elsewhere
{
    [HarmonyPatch(typeof(Steering))]
    partial class SteeringPatch
    {


/*#if SERVER
            NetUtil.Register(NetEvent.VERTICAL_ENGINE_POWER_CHANGE, OnReceiveVerticalEnginePowerMessage);
#endif*/



        private static float lerpedVerticalEnginePower;

        
        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        
        public static void Update(Steering __instance, float deltaTime, Camera cam)
        {
            //DebugConsole.NewMessage("lerpedVerticalEnginePower: " + lerpedVerticalEnginePower);
            __instance.item.SendSignal(lerpedVerticalEnginePower.ToString("F1"), "vertical_engine_power_out");

            if (__instance.AutoPilot && !__instance.MaintainPos)
            {
                
                __instance.sonar.useDirectionalPing = true;
                __instance.sonar.CurrentMode = Sonar.Mode.Active;

            }

        }


        private void OnReceiveVerticalEnginePowerMessage(object[] args)
        {
            IReadMessage msg = (IReadMessage)args[0];
            ushort itemId = msg.ReadUInt16();
            float lerpedVerticalEnginePower = msg.ReadSingle();

            Item navigationTerminal = Entity.FindEntityByID(itemId) as Item;
            if (navigationTerminal != null)
            {
                navigationTerminal.SendSignal(lerpedVerticalEnginePower.ToString("F1"), "vertical_engine_power_out");
                

            }
        }

    }
}
