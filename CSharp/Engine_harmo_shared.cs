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
    public partial class EngineMod : IAssemblyPlugin
    {
        public  Harmony harmony;

        public static Dictionary<int, EngineMap> engineInfo = new Dictionary<int, EngineMap>();
        public static Dictionary<int, float> lubrication = new Dictionary<int, float>();
        public static float escapedTime = 0f;
        public static float updateInterval = 0.1f;


        public void Initialize()
		{
			harmony = new Harmony("EngineMod");
			
			harmony.Patch(
                original: typeof(Engine).GetMethod("Update"),
                postfix: new HarmonyMethod(typeof(EngineMod).GetMethod(nameof(Update)))
            );

            var originalConstructor = typeof(Engine).GetConstructor(new[] { typeof(Item), typeof(ContentXElement) });
            var postfix = new HarmonyMethod(typeof(EngineMod).GetMethod(nameof(EngineConstructorPostfix)));
            harmony.Patch(originalConstructor, null, postfix);
//#if CLIENT
			var originalInitProjSpecific = typeof(Engine).GetMethod("InitProjSpecific", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(ContentXElement) }, null);
            var postfixSelect = typeof(EngineMod).GetMethod("InitProjSpecificPostfix", BindingFlags.Public | BindingFlags.Static);
            harmony.Patch(originalInitProjSpecific, new HarmonyMethod(postfixSelect), null);
//#endif
			}

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }
        
        public static void EngineConstructorPostfix(Item item, ContentXElement element, Engine __instance)
        {
            Engine _ = __instance;
            if (_ == null) return;
            if (!lubrication.ContainsKey(_.item.ID))
            {
                lubrication.Add(_.item.ID, 100f);
            }
            EngineMap engineMap = new EngineMap()
            {
                lubericant = 100f,
                useElectric = false,
                temperature = 0f,
                hydrogen = 500f,
                hydrogenMax = 1000f,
            };
            engineInfo[_.item.ID] = engineMap;
        }

        public static void Update(float deltaTime, Camera cam, Engine __instance)
		{
            escapedTime += deltaTime;
            if (escapedTime > updateInterval)
            {
                Engine _ = __instance;
                //DebugConsole.NewMessage("Lubrication: " + lubrication[_.item.ID], Color.White);
                if (_.force > 0)
                {
                    if (EngineMod.lubrication[_.item.ID] > 0f)
                    {
                    lubrication[_.item.ID] -= 0.01f * _.force * deltaTime;
                    }

                    if (lubrication[_.item.ID] <= 0f)
                    {
                        
                    // _.force = 0f;
                        _.item.Condition -= 0.1f * deltaTime;
                        //_.item.SendSignal(0, "break", null);
                    }
                }
                
                escapedTime = 0f;
            }
		}
        
        public struct EngineMap
        {
            public float force;
            public float fuelConsumption;
            public bool useElectric;
            public float lubericant;
            public float temperature;
            public float hydrogen;
            public float hydrogenMax;
        }
    }
}
