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
	[HarmonyPatch(typeof(Attack))]
    public static class AttackPatch
    {
        // 目标 A：DoDamageToLimb
        [HarmonyPatch("DoDamageToLimb")]
        [HarmonyPostfix]
        public static void HandleArmorDamageOnAttack(
            Character attacker, 
            Limb targetLimb, 
            Attack __instance, 
            ref AttackResult __result) 
        {
            //DebugConsole.NewMessage("AttackPatch: DoDamageToLimb triggered!");
            
            if (targetLimb?.character?.Inventory == null) return;

            var inventory = targetLimb.character.Inventory;
            var armor = inventory.GetItemInLimbSlot(InvSlotType.OuterClothes);
            var innerCloth = inventory.GetItemInLimbSlot(InvSlotType.InnerClothes);
            var headWear = inventory.GetItemInLimbSlot(InvSlotType.Head);

            if (targetLimb.type == LimbType.Torso && armor != null)
            {
                ApplyDamage(armor, 10f, 15f);
            }
            else if (targetLimb.type == LimbType.Torso && armor == null && innerCloth != null && innerCloth.HasTag("clothing"))
            {
                ApplyDamage(innerCloth, 10f, 15f);
            }
            else if (targetLimb.type == LimbType.Head && headWear != null)
            {
                ApplyDamage(headWear, 10f, 30f);
            }
        }

        // 目标 B：SetUser
        [HarmonyPatch("SetUser")]
        [HarmonyPostfix]
        public static void SetUser(Character user)
        {
            // 这里现在不会去尝试寻找 attacker 了
        }

        private static void ApplyDamage(Item item, float min, float max)
        {
            item.Condition -= Rand.Range(min, max);
            if (item.Condition <= 0f)
            {
                Entity.Spawner.AddEntityToRemoveQueue(item);
            }
        }
    }
    
}