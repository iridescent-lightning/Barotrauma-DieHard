// no use
using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Barotrauma;
using HarmonyLib;
using System.Globalization;
using System.Reflection;

namespace BarotraumaDieHard
{
    public class ItemPrefabDieHard : IAssemblyPlugin
    {
        public Harmony harmony;
		public void Initialize()
		{
		    harmony = new Harmony("ItemPrefabDieHard");

            var originalParseSubElementsClient = typeof(ItemPrefab).GetMethod("ParseSubElementsClient", BindingFlags.NonPublic | BindingFlags.Instance);
            var postfixParseSubElementsClient = new HarmonyMethod(typeof(ItemPrefabDieHard).GetMethod(nameof(ParseSubElementsClientPostfix), BindingFlags.Public | BindingFlags.Static));
            harmony.Patch(originalParseSubElementsClient, postfixParseSubElementsClient, null);
        }

		public void OnLoadCompleted() { }
		public void PreInitPatching() { }

		public void Dispose()
		{
		  harmony.UnpatchSelf();
		  harmony = null;
		}

        
        public static void ParseSubElementsClientPostfix(ContentXElement element, ItemPrefab variantOf, ItemPrefab __instance)
        {
            //DebugConsole.NewMessage("patched");
        }


                
        }


        
}
