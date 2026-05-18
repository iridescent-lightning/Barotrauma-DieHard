﻿using HarmonyLib;
using Barotrauma;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(Structure))]
    public class StructureLeakPatch
    {
        private const float NewBigGapThreshold = 0.9f;
        private const float NewSmallGapOpenness = 0.25f;
        private const float NewLargeGapOpenness = 0.75f;
        
        // 记录每个墙体部分上次爆炸时的伤害值
        private static Dictionary<(int, int), float> lastExplosionDamage = new Dictionary<(int, int), float>();
        private const float MinDamageIncreaseForExplosion = 0.08f; // 需要增加8%伤害才能再次爆炸

        [HarmonyPatch("SetDamage")]
        [HarmonyPrefix]
        public static void SetDamagePrefix(Structure __instance, int sectionIndex, float damage, 
            ref bool createExplosionEffect)
        {
            var section = __instance.GetSection(sectionIndex);
            if (section == null) return;
            
            float maxHealth = __instance.MaxHealth;
            if (maxHealth <= 0f) return;
            
            float oldDamage = section.damage;
            float newDamage = MathHelper.Clamp(damage, 0f, maxHealth);
            
            // 计算旧的和新的开口度
            float oldRatio = oldDamage / maxHealth;
            float newRatio = newDamage / maxHealth;
            
            float oldGapOpen = GetGapOpen(oldRatio);
            float newGapOpen = GetGapOpen(newRatio);
            
            // 如果开口度变化不大，禁用爆炸效果
            float gapDelta = newGapOpen - oldGapOpen;
            
            // 获取上次爆炸时的伤害
            var key = (__instance.ID, sectionIndex);
            lastExplosionDamage.TryGetValue(key, out float lastDamage);
            
            float damageIncreaseRatio = (newDamage - lastDamage) / maxHealth;
            
            // 只有当开口度显著增加且伤害增加足够大时才允许爆炸
            if (gapDelta < 0.2f || damageIncreaseRatio < MinDamageIncreaseForExplosion)
            {
                createExplosionEffect = false;
            }
            else
            {
                lastExplosionDamage[key] = newDamage;
            }
        }

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
            
            float newGapOpen = GetGapOpen(damageRatio);
            
            section.gap.Open = newGapOpen;
        }

        private static float GetGapOpen(float damageRatio)
        {
            if (damageRatio >= NewBigGapThreshold)
            {
                float t = (damageRatio - NewBigGapThreshold) / (1f - NewBigGapThreshold);
                return MathHelper.Lerp(NewSmallGapOpenness, NewLargeGapOpenness, t);
            }
            else
            {
                float t = damageRatio / NewBigGapThreshold;
                return MathHelper.Lerp(0f, NewSmallGapOpenness, t);
            }
        }
    }
}