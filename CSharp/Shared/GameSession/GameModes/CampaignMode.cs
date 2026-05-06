using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Xml.Linq;


using Barotrauma;
using System.Reflection;
using HarmonyLib;
using System.Reflection.PortableExecutable;



namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(CampaignMode))]
    public partial class CampaignModePatch
    {
        // 拦截 GetLeavingSub 方法
        [HarmonyPatch("OutpostNPCAttacked")]
        [HarmonyPostfix]
        public static void OutpostNPCAttacked(CampaignMode __instance, Character npc, Character attacker, AttackResult attackResult)
        {
            Location location = __instance.Map?.CurrentLocation;

            if (npc == null || attacker == null || npc.IsInstigator) { return; }
            if (npc.TeamID != CharacterTeamType.FriendlyNPC) { return; }
            if (!attacker.IsRemotePlayer && attacker != Character.Controlled) { return; }

            if (npc.IsDead)
            {
                location?.Reputation?.AddReputation(-100f);
            }
                
        }

    }
}
