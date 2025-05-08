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
    class ItemComponentDieHard : IAssemblyPlugin
    {
        public Harmony harmony;

        public void Initialize()
        {
            harmony = new Harmony("ItemContainerMod");

            var originalOverrideRequiredItems = typeof(ItemComponent).GetMethod("OverrideRequiredItems", BindingFlags.NonPublic | BindingFlags.Instance);
            var prefixOverrideRequiredItems = new HarmonyMethod(typeof(ItemComponentDieHard).GetMethod(nameof(OverrideRequiredItemsPrefix), BindingFlags.Public | BindingFlags.Static));
            harmony.Patch(originalOverrideRequiredItems, prefixOverrideRequiredItems, null);
        }

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }

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
