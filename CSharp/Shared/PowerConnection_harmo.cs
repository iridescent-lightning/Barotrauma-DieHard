using System;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using System.Reflection;
using Barotrauma;
using Barotrauma.Items.Components;
#if CLIENT
using Barotrauma.Sounds;
#endif

namespace BarotraumaDieHard
{
    partial class PoweredMod  : IAssemblyPlugin
    {
        public Harmony harmony;
        public void Initialize()
        {
            harmony = new Harmony("PowerConnection");

            harmony.Patch(
                original: typeof(Powered).GetMethod("ValidPowerConnection"),
                prefix: new HarmonyMethod(typeof(PoweredMod).GetMethod(nameof(ValidPowerConnection)))
            );
            
        }

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }


        public static bool ValidPowerConnection(Connection conn1, Connection conn2)
        {
            if (conn1.Name.StartsWith("steam") || conn2.Name.StartsWith("steam")) 
            {
                    return conn1.Name.StartsWith("steam") && conn2.Name.StartsWith("steam") && (
                        conn1.IsOutput != conn2.IsOutput || 
                        conn1.Name == "steam" || 
                        conn2.Name == "steam" 
                    );
            }
            CustomJunctionBox device = conn1.Item.GetComponent<CustomJunctionBox>();
            return 
                conn1.IsPower && conn2.IsPower && 
                conn1.Item.Condition > 0.0f && conn2.Item.Condition > 0.0f &&
                (conn1.Item.HasTag(Tags.JunctionBox) || conn2.Item.HasTag(Tags.JunctionBox) || conn1.Item.HasTag(Tags.DockingPort) || conn2.Item.HasTag(Tags.DockingPort) || conn1.IsOutput != conn2.IsOutput)
                && (device == null || !device.BrokenFuse);
        }


    }
    

}