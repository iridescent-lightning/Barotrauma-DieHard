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
            // some doors are placed in a way that the item isn't inside a hull.
            if (door.item.FindHull() == null) return false;
            // There is a bug that when bots try to go to the airlock, they will still speak. They have no issue open the door though.
            // Should be fixed automatically once the pressure check is added.


            float normalAirPressure = Math.Max(0, door.item.Submarine.RealWorldDepth);


            if (door.item.FindHull().RoomName.ToString().Contains("ballast", System.StringComparison.OrdinalIgnoreCase) && door.item.FindHull().IsWetRoom && HullMod.GetGas(door.item.FindHull(), "PressurizedAir") > normalAirPressure * 1.75f)
            {
                // DebugConsole.NewMessage($"room name: {door.item.FindHull().RoomName}");
                __instance.character.Speak(TextManager.Get("dialog.bots.cannotaccesswetroomdoor").Value, null, 0.0f, "cannotaccesswetroomdoor".ToIdentifier(), 30.0f);
                __result = false;
                return false;
            }
            return true;
        }

        
    }
}