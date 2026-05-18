﻿using HarmonyLib;
using Barotrauma;
using Microsoft.Xna.Framework;

namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(Structure))]
    public class StructureLeakPatch
    {
        private const float NewBigGapThreshold = 0.85f;
        private const float NewSmallGapOpenness = 0.35f;
        private const float NewLargeGapOpenness = 0.75f;

        [HarmonyPatch("SetDamage")]
        [HarmonyPostfix]
        public static void SetDamagePostfix(Structure __instance, int sectionIndex)
        {
            var section = __instance.GetSection(sectionIndex);
            if (section?.gap == null) return;

            float maxHealth = __instance.MaxHealth;
            if (maxHealth <= 0f) return;

            float damage = section.damage;
            float damageRatio = damage / maxHealth;
            
            float newGapOpen;
            
            if (damageRatio >= NewBigGapThreshold)
            {
                // 90%-100%：大漏水
                float t = (damageRatio - NewBigGapThreshold) / (1f - NewBigGapThreshold);
                newGapOpen = MathHelper.Lerp(NewSmallGapOpenness, NewLargeGapOpenness, t);
            }
            else
            {
                // 0%-90%：小漏水
                float t = damageRatio / NewBigGapThreshold;
                newGapOpen = MathHelper.Lerp(0f, NewSmallGapOpenness, t);
            }
            
            // 直接修改开口度
            section.gap.Open = newGapOpen;
        }

        
    }
}