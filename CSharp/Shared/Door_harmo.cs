using Barotrauma.Networking;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using FarseerPhysics.Dynamics;
using Barotrauma;
#if CLIENT
using Barotrauma.Lights;
#endif
using Barotrauma.Extensions;


using HarmonyLib;
using Barotrauma.Items.Components;

namespace DoorMod
{
    class DoorMod : IAssemblyPlugin
    {
        public Harmony harmony;
        
        public void Initialize()
        {
            harmony = new Harmony("DoorMod");

            harmony.Patch(
                original: typeof(Door).GetMethod("Update"),
                postfix: new HarmonyMethod(typeof(DoorMod).GetMethod(nameof(Update)))
            );
        }

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }

        
        public static void Update(float deltaTime, Camera cam, Door __instance)
        {
            //DebugConsole.NewMessage(__instance.stuck.ToString());
            if (__instance.LinkedGap != null)
            {
                // Calculate the door's condition as a percentage
                float conditionPercentage = __instance.item.Condition / __instance.item.MaxCondition;

                // If the door has received more than 50% damage
                if (conditionPercentage < 0.5f)
                {
                    // Calculate gap openness
                    // The more damaged and stuck the door is, the more open the gap should be
                    float gapOpenness = 1.0f - (conditionPercentage * 2.0f);
                    gapOpenness = MathHelper.Clamp(gapOpenness - (__instance.stuck / 100.0f), 0.0f, 1.0f);

                    // Set the openness of the gap
                    __instance.LinkedGap.Open = gapOpenness;
                }
            }
        } 
    }
}
