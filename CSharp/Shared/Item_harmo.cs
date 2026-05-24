
/*using Barotrauma.Items.Components;
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
        [HarmonyPatch(new Type[] { typeof(ItemPrefab), typeof(Vector2), typeof(Submarine), typeof(ushort), typeof(bool) })]
        [HarmonyPostfix]
        public static void ItemCOnstructorPostfix(Item __instance)
        {
            DebugConsole.NewMessage($"{__instance.MaxCondition}");
            if (__instance == null) return;

            if (__instance.Prefab.Identifier == "hatch" || __instance.Prefab.Identifier == "hatchwbuttons")
            {
                __instance.MaxCondition *= 3f;

                DebugConsole.NewMessage($"{__instance.MaxCondition}");
            }

            
        }

                
    }


        
}*/
