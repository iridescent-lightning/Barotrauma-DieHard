﻿using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Extensions;


using Barotrauma;
using HarmonyLib;
using System.Globalization;
using System.Reflection;


namespace BarotraumaDieHard
{
    class AttackDieHard : IAssemblyPlugin
    {
        public Harmony harmony;
		
        public static bool hasZoomed = false;

		public void Initialize()
		{
		    harmony = new Harmony("AttackDieHard");

			
			
            var originalDoDamageToLimb = typeof(Attack).GetMethod("DoDamageToLimb", BindingFlags.Public | BindingFlags.Instance);
            var postfixDoDamageToLimb = new HarmonyMethod(typeof(AttackDieHard).GetMethod(nameof(DoDamageToLimbPostfix), BindingFlags.Public | BindingFlags.Static));
            harmony.Patch(originalDoDamageToLimb, postfixDoDamageToLimb, null);
        }

		public void OnLoadCompleted() { }
		public void PreInitPatching() { }

		public void Dispose()
		{
		  harmony.UnpatchSelf();
		  harmony = null;
		}
		
		
		public static void DoDamageToLimbPostfix(Character attacker, Limb targetLimb, Vector2 worldPosition, float deltaTime, bool playSound, PhysicsBody sourceBody, Limb sourceLimb, Attack __instance)
		{
			

			var armor = targetLimb.character.Inventory.GetItemInLimbSlot(InvSlotType.OuterClothes);
			var innerCloth = targetLimb.character.Inventory.GetItemInLimbSlot(InvSlotType.InnerClothes);
			var headWear = targetLimb.character.Inventory.GetItemInLimbSlot(InvSlotType.Head);

			if (targetLimb.type == LimbType.Torso && armor != null)
			{
				 // DebugConsole.NewMessage("executed.");
				armor.Condition = armor.Condition - Rand.Range(10f, 40f);
					if (armor.Condition <= 0f)
					{
						Entity.Spawner.AddEntityToRemoveQueue(armor);
					}
			}
			else if (targetLimb.type == LimbType.Torso && armor == null && innerCloth != null && innerCloth.HasTag("clothing"))
			{
				innerCloth.Condition = innerCloth.Condition - Rand.Range(10f, 40f);
					if (innerCloth.Condition <= 0f)
					{
						Entity.Spawner.AddEntityToRemoveQueue(innerCloth);
					}
			}
			else if (targetLimb.type == LimbType.Head && headWear != null)
			{
				headWear.Condition = headWear.Condition - Rand.Range(10f, 100f);
					if (headWear.Condition <= 0f)
					{
						Entity.Spawner.AddEntityToRemoveQueue(headWear);
					}
			}

		}

		
        

		
        
	}
    
}