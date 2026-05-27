
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Contacts;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Extensions;
using Barotrauma.MapCreatures.Behavior;
using System.Collections.Immutable;
using Barotrauma.Abilities;
#if CLIENT
using Microsoft.Xna.Framework.Graphics;
#endif

using Barotrauma;
using HarmonyLib;
using System.Globalization;
using System.Reflection;// for bindingflags

using System.IO;

namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(Item))]
    partial class ItemPatch
    {
        
        [HarmonyPatch(MethodType.Constructor)]
        [HarmonyPatch(new Type[] { typeof(Rectangle), typeof(ItemPrefab), typeof(Submarine), typeof(bool), typeof(ushort) })]
        [HarmonyPostfix]
        public static void Postfix(Item __instance)
        {
            if (__instance == null) return;          
               
            __instance.OnDeselect = (character) =>
            {                    
                __instance.RemoveTag("draw_container_open");
                __instance.RemoveTag("junctionbox_openlid");

                if (__instance.Prefab.Identifier == "steelcabinet")
                {
                    //DebugConsole.NewMessage("removed");
                    #if CLIENT
                        SoundPlayer.PlaySound("sfx_largesteelcab_close", __instance.WorldPosition, hullGuess: __instance.CurrentHull);
                    #endif
                }
                else if (__instance.Prefab.Identifier == "mediumsteelcabinet" || __instance.Prefab.Identifier == "mediumwindowedsteelcabinet" )
                {
                    #if CLIENT
                        SoundPlayer.PlaySound("sfx_mediumsteelcab_close", __instance.WorldPosition, hullGuess: __instance.CurrentHull);
                    #endif
                }
                else if (__instance.Prefab.Identifier == "securesteelcabinet" )
                {
                    #if CLIENT
                        SoundPlayer.PlaySound("sfx_sec_idcardclose", __instance.WorldPosition, hullGuess: __instance.CurrentHull);
                    #endif
                }
                else if (__instance.Prefab.Identifier == "medcabinet" )
                {
                    #if CLIENT
                        SoundPlayer.PlaySound("sfx_medcontainer_close", __instance.WorldPosition, hullGuess: __instance.CurrentHull);
                    #endif
                }
                else if (__instance.Prefab.Identifier == "medcabinet" )
                {
                    #if CLIENT
                        SoundPlayer.PlaySound("sfx_medcontainer_close", __instance.WorldPosition, hullGuess: __instance.CurrentHull);
                    #endif
                }
                
            };

            __instance.OnInteract = () =>
            {
                DebugConsole.NewMessage("interact");
                __instance.AddTag("draw_container_open");

            };

            
            
        }
    

                
    }


        
}
