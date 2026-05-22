﻿using HarmonyLib;
using Barotrauma;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BarotraumaDieHard
{
    // 配置类
    public static class BuoyancyConfig
    {
        public enum SubmarineRole
        {
            Scout,      // 侦察型
            Combat,     // 战斗型
            Transport,   // 运输型
            Undefined,
            Shuttle
        }

        // 不同潜艇类型的进水容忍阈值（超过此比例才开始损失浮力）
        public static readonly Dictionary<SubmarineRole, float> FloodToleranceByRole = new Dictionary<SubmarineRole, float>
        {
            { SubmarineRole.Scout, 0.17f },     // 15% 进水才开始下沉
            { SubmarineRole.Combat, 0.15f },    // 10% 进水才开始下沉
            { SubmarineRole.Transport, 0.25f },  // 20% 进水才开始下沉
            { SubmarineRole.Shuttle, 0.1f },
            { SubmarineRole.Undefined, 0f }
        };

        // 默认进水容忍阈值
        public const float DefaultFloodTolerance = 0.15f;

        // 非压载舱进水影响系数（达到阈值后的影响程度）
        // 1.0 = 完全影响，0.5 = 只有一半影响
        public const float NonBallastFloodMultiplier = 0.5f;
    }

    [HarmonyPatch(typeof(SubmarineBody))]
    public class SubmarineBodyPatch
    {
        // 存储每个潜艇的进水容忍阈值
        private static Dictionary<Submarine, float> floodToleranceCache = new Dictionary<Submarine, float>();

        // 获取潜艇的角色类型
        private static BuoyancyConfig.SubmarineRole GetSubmarineRole(Submarine sub)
        {
            if (sub?.Info == null) return BuoyancyConfig.SubmarineRole.Combat;

            string subName = sub.Info.Name?.ToLower() ?? "";
            string subClass = sub.Info.SubmarineClass.ToString().ToLower();

            if (subName.Contains("scout") || subName.Contains("侦察") || subClass.Contains("scout"))
                return BuoyancyConfig.SubmarineRole.Scout;
            
            if (subName.Contains("transport") || subName.Contains("运输") || subClass.Contains("cargo") || subClass.Contains("transport"))
                return BuoyancyConfig.SubmarineRole.Transport;

            if (subClass.Contains("shuttle"))
                return BuoyancyConfig.SubmarineRole.Shuttle;
            
            return BuoyancyConfig.SubmarineRole.Undefined;
        }

        // 获取潜艇的进水容忍阈值
        private static float GetFloodTolerance(Submarine sub)
        {
            if (sub == null) return BuoyancyConfig.DefaultFloodTolerance;

            if (!floodToleranceCache.ContainsKey(sub))
            {
                var role = GetSubmarineRole(sub);
                float tolerance = BuoyancyConfig.FloodToleranceByRole.GetValueOrDefault(role, BuoyancyConfig.DefaultFloodTolerance);
                floodToleranceCache[sub] = tolerance;
            }
            
            return floodToleranceCache[sub];
        }

        // 清除缓存
        public static void ClearCache(Submarine sub = null)
        {
            if (sub == null)
            {
                floodToleranceCache.Clear();
            }
            else
            {
                floodToleranceCache.Remove(sub);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch("CalculateBuoyancy")]
        public static bool CalculateBuoyancyPrefix(SubmarineBody __instance, ref Vector2 __result)
        {
            var submarine = __instance.Submarine;
            if (submarine == null) return true;

            if (__instance.Body.FarseerBody.BodyType == FarseerPhysics.BodyType.Static)
            {
                __result = Vector2.Zero;
                return false;
            }

            if (Submarine.LockY)
            {
                __result = Vector2.Zero;
                return false;
            }

            var connectedSubs = submarine.GetConnectedSubs();
            
            // 压载舱数据
            float ballastWaterVolume = 0f;
            float ballastVolume = 0f;
            
            // 非压载舱数据
            float nonBallastVolume = 0f;
            float realNonBallastWaterVolume = 0f; // 【新增】用于统计非压载舱的真实进水总量
            float nonBallastEffectiveWater = 0f;  // 超额折算后的有效进水

            float floodTolerance = GetFloodTolerance(submarine);

            foreach (Hull hull in Hull.HullList)
            {
                if (hull.Submarine == null || !connectedSubs.Contains(hull.Submarine)) 
                    continue;
                
                if (hull.Submarine.PhysicsBody is not { BodyType: FarseerPhysics.BodyType.Dynamic }) 
                    continue;

                bool isBallast = hull.RoomName != null && 
                                 hull.RoomName.ToLower().Contains("ballast");

                if (isBallast)
                {
                    // 压载舱：完全计入
                    ballastVolume += hull.Volume;
                    ballastWaterVolume += hull.WaterVolume;
                }
                else
                {
                    // 非压载舱
                    nonBallastVolume += hull.Volume;
                    realNonBallastWaterVolume += hull.WaterVolume; // 【记录真实水量】
                    
                    float floodRatio = hull.Volume > 0 ? hull.WaterVolume / hull.Volume : 0;
                    
                    if (floodRatio > floodTolerance)
                    {
                        // 计算超过阈值的真实进水量
                        float excessFloodVolume = (floodRatio - floodTolerance) * hull.Volume;
                        // 应用影响系数打折
                        float effectiveExcess = excessFloodVolume * BuoyancyConfig.NonBallastFloodMultiplier;
                        nonBallastEffectiveWater += effectiveExcess;
                    }
                }
            }
            
            // 计算总有效水量和总有效容积
            float totalEffectiveWater = ballastWaterVolume + nonBallastEffectiveWater;
            float totalEffectiveVolume = ballastVolume + nonBallastVolume;
            
            if (totalEffectiveVolume <= 0f)
            {
                __result = Vector2.Zero;
                return false;
            }

            // 【新增计算】非压载舱的真实整体进水百分比
            float realNonBallastWaterPercentage = nonBallastVolume > 0f ? realNonBallastWaterVolume / nonBallastVolume : 0f;

            // 最终带入算法的有效进水率
            float waterPercentage = totalEffectiveWater / totalEffectiveVolume;
            
            // 浮力计算
            float buoyancy = Barotrauma.SubmarineBody.NeutralBallastPercentage - waterPercentage;
            
            // 限制浮力范围
            buoyancy = MathHelper.Clamp(buoyancy, -0.5f, 0.2f);
            
            float totalMass = connectedSubs.Sum(s => s.SubBody?.Body?.Mass ?? 0f);
            if (totalMass <= 0f)
            {
                __result = Vector2.Zero;
                return false;
            }
            
            float massRatio = __instance.Body.Mass / totalMass;
            float forceY = buoyancy * totalMass * 10f * massRatio;
            
            // ==========================================
            // 🖥️ 全新升级的控制台监视日志
            // ==========================================
            /*DebugConsole.NewMessage(
                $"[Buoyancy] Sub: {submarine.Info.Name} | Class: {submarine.Info.SubmarineClass}\n" +
                $" -> [配置] 进水阈值: {floodTolerance:P0} | 减缓系数: {BuoyancyConfig.NonBallastFloodMultiplier:F1}\n" +
                $" -> [压载] 水量/容积: {ballastWaterVolume:F0}/{ballastVolume:F0}\n" +
                $" -> [常规舱] 真实进水%: {realNonBallastWaterPercentage:P1} ({realNonBallastWaterVolume:F0}/{nonBallastVolume:F0}) | 折算后有效水: {nonBallastEffectiveWater:F0}\n" +
                $" -> [结算] 最终进水率: {waterPercentage:P1} | 净浮力值: {buoyancy:F3} | 垂直总合力: {forceY:F0}", 
                Color.Cyan);*/

            __result = new Vector2(0f, forceY);
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch("Remove")]
        public static void RemovePostfix(SubmarineBody __instance)
        {
            if (__instance?.Submarine != null)
            {
                ClearCache(__instance.Submarine);
            }
        }
    }
}