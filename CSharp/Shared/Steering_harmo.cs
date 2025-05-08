
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

namespace SteeringMod//todo make a structural namespace DieHard.Item.Components. namespace can't be used in elsewhere
{
    partial class SteeringMod : IAssemblyPlugin
    {
        public Harmony harmony;

        
        public void Initialize()
		{
			harmony = new Harmony("SteeringMod");
#if CLIENT
			var originalCreateGUI = typeof(Steering).GetMethod("CreateGUI", BindingFlags.NonPublic | BindingFlags.Instance);
            var postfixCreateGUI = typeof(SteeringMod).GetMethod("CreateGUI", BindingFlags.Public | BindingFlags.Static);
            harmony.Patch(originalCreateGUI, new HarmonyMethod(postfixCreateGUI), null);
			
			harmony.Patch(
                original: typeof(Steering).GetMethod("UpdateHUDComponentSpecific"),
                postfix: new HarmonyMethod(typeof(SteeringMod).GetMethod("UpdateHUDComponentSpecificPostfix", BindingFlags.Public | BindingFlags.Static))
            );
#endif
            harmony.Patch(
                original: typeof(Steering).GetMethod("Update"),
                postfix: new HarmonyMethod(typeof(SteeringMod).GetMethod("Update", BindingFlags.Public | BindingFlags.Static))
            );
        }

        public void OnLoadCompleted() { }
        public void PreInitPatching() 
        {
/*#if SERVER
            NetUtil.Register(NetEvent.VERTICAL_ENGINE_POWER_CHANGE, OnReceiveVerticalEnginePowerMessage);
#endif*/
        }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }


        private static float lerpedVerticalEnginePower;
        
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
