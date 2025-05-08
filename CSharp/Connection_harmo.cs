using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Xml.Linq;
using Barotrauma;

using HarmonyLib;
using Barotrauma.Items.Components;

namespace BarotraumaDieHard
{
    class ConnectionMod : IAssemblyPlugin
    {
        public Harmony harmony;
        
        public void Initialize()
        {
            harmony = new Harmony("ConnectionMod");

            harmony.Patch(
                original: typeof(Connection).GetMethod("ConnectWire"),
                postfix: new HarmonyMethod(typeof(ConnectionMod).GetMethod(nameof(ConnectWire)))
            );
        }

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }

        
        
        public static void ConnectWire(Wire __instance)
        {
            Wire _ = __instance;
            
            DebugConsole.NewMessage(_.Item.ToString());
            if (_.Item?.Prefab.Identifier == "pipe")
            {
                DebugConsole.NewMessage("Wire connected");
                //return;
            }
            else
            {
                DebugConsole.NewMessage("Wire not connected");
                return;
            }
        }
    }
}
