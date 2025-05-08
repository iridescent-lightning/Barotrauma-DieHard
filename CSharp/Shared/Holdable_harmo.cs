﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Barotrauma.IO;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Barotrauma.Abilities;
using System.Collections.Immutable;


using Barotrauma;
using HarmonyLib;
using System.Globalization;
using System.Reflection;



namespace BarotraumaDieHard
{
    class HoldableMod : IAssemblyPlugin
    {
        public Harmony harmony;
		


		public void Initialize()
		{
		    harmony = new Harmony("HoldableMod");

			
			
            var originalEquip = typeof(Holdable).GetMethod("Equip", BindingFlags.Public | BindingFlags.Instance);
            var postfixEquip = new HarmonyMethod(typeof(HoldableMod).GetMethod(nameof(EquipPostfix), BindingFlags.Public | BindingFlags.Static));
            harmony.Patch(originalEquip, postfixEquip, null);

        }

		public void OnLoadCompleted() { }
		public void PreInitPatching() { }

		public void Dispose()
		{
		  harmony.UnpatchSelf();
		  harmony = null;
		}
		
		public static void EquipPostfix(Character character, Holdable __instance)
		{
			Holdable _ = __instance;
			
#if CLIENT
	 	if (_.item.HasTag("gun"))
		{
			SoundPlayer.PlaySound("interactive_rifle_equip", _.item.WorldPosition, hullGuess: _.item.CurrentHull);
		}
			
#endif
		}
        
		
		
        
	}
    
}