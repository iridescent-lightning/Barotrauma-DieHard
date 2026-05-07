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
        
        // Make player always pick item in hand if picked from the ground
		[HarmonyPatch("OnPicked")]
        [HarmonyPostfix]
        static void Postfix(Holdable __instance, Character picker, bool __result)
        {
            // 1. 如果捡起失败，或者捡起者不是玩家（手动操作的人类），则不处理
        if (!__result || picker == null || picker.IsBot) return;

        Item item = __instance.item;

       
            // 尝试装备到手部。TryPutItem 会自动处理是否有空手，并触发动画逻辑
            // 这里的 List 定义了允许强制放置的目标槽位
            picker.Inventory.TryPutItem(item, picker, new List<InvSlotType> { InvSlotType.RightHand, InvSlotType.LeftHand });
        
      
        }
	}
    
}