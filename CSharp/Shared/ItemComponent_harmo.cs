using System.Reflection;
using Barotrauma;
using HarmonyLib;
using Barotrauma.Items.Components;

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Barotrauma.Extensions;
using Barotrauma.IO;
using Barotrauma.Networking;
#if CLIENT
using Microsoft.Xna.Framework.Graphics;
using Barotrauma.Sounds;
#endif

namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(ItemComponent))]
    class ItemComponentDieHard
    {
        [HarmonyPatch("OverrideRequiredItems")]
        [HarmonyPrefix]	
        public static bool OverrideRequiredItemsPrefix(ContentXElement element, ItemComponent __instance)
        {

           // DebugConsole.NewMessage(element.Name.ToString());
            if (element.Name.ToString() == "Repairable")
            {
                
                return false;
            }

            return true;
        }

    }
}
