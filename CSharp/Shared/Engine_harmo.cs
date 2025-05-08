using Microsoft.Xna.Framework;
using System;
using System.Globalization;
using System.Xml.Linq;
using Barotrauma.Networking;
using System.Collections.Generic; // for Dictionary

using Barotrauma.Items.Components;
using System.Reflection;
using Barotrauma;
using HarmonyLib;


namespace BarotraumaDieHard
{
    class EngineMod : IAssemblyPlugin
    {
        public  Harmony harmony;

       


        public void Initialize()
		{
			harmony = new Harmony("EngineMod");
			
			harmony.Patch(
                original: typeof(Engine).GetMethod("Update"),
                postfix: new HarmonyMethod(typeof(EngineMod).GetMethod(nameof(Update)))
            );

            
		}

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }
        

        public static void Update(float deltaTime, Camera cam, Engine __instance)
		{
           

           __instance.item.SendSignal(__instance.GetCurrentPowerConsumption(__instance.powerIn).ToString("F1"), "PowerConsumptionOut");
		}
        
       
    }
}
