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
	[HarmonyPatch(typeof(Holdable))] 
    [HarmonyPatch("Equip")]
    class HoldablePatch
    {
		
		public static void Postfix(Character character, Holdable __instance)
		{
			Holdable _ = __instance;
			
#if CLIENT

		if (_.item.Prefab.Identifier == "revolver")
		{
			
			SoundPlayer.PlaySound("interactive_revolver_equip", _.item.WorldPosition, hullGuess: _.item.CurrentHull);	
		}
	 	else if (_.item.HasTag("gun"))
		{
			SoundPlayer.PlaySound("interactive_rifle_equip", _.item.WorldPosition, hullGuess: _.item.CurrentHull);
		}
			
#endif
		}
        
	}
    
}