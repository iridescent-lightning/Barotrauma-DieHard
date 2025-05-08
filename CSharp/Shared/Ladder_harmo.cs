﻿// This class force character to climb with double hands
/*using Microsoft.Xna.Framework;
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
    class LadderMod : IAssemblyPlugin
    {
        public Harmony harmony;
		


		public void Initialize()
		{
		    harmony = new Harmony("LadderMod");

			
			
            var originalEquip = typeof(Ladder).GetMethod("Select", BindingFlags.Public | BindingFlags.Instance);
            var postfixEquip = new HarmonyMethod(typeof(LadderMod).GetMethod(nameof(SelectPostfix), BindingFlags.Public | BindingFlags.Static));
            harmony.Patch(originalEquip, postfixEquip, null);

        }

		public void OnLoadCompleted() { }
		public void PreInitPatching() { }

		public void Dispose()
		{
		  harmony.UnpatchSelf();
		  harmony = null;
		}
		
		//None = 0, Any = 1, RightHand = 2, LeftHand = 4, Head = 8, InnerClothes = 16, OuterClothes = 32, Headset = 64, Card = 128, Bag = 256, HealthInterface = 512
		public static bool SelectPostfix(Character character, Ladder __instance)
		{
			
			var BagSlotIndex = character.Inventory.FindLimbSlot(InvSlotType.Bag);

			Item itemInHand = character.Inventory.GetItemAt(5);
			Item itemInBag = character.Inventory.GetItemAt(BagSlotIndex);

			
			//Item itemInRightHand = character.Inventory.GetItemInLimbSlot(InvSlotType.RightHand);


			if (itemInHand!= null && itemInBag == null)
			{
				character.Inventory.TryPutItem(itemInHand, BagSlotIndex, true, false, Character.Controlled, true, true);
				character.Speak(TextManager.Get("dialogclimbladderwithfullhanditem").Value);
			} 
			else if (itemInHand== null)
			{
				return true;
			}

			if (itemInHand!= null)
			{
				
				return false;
			}
			
			
			
			return true;
		}

		
		
        
	}
    
}*/