﻿using HarmonyLib;
using Barotrauma;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using FarseerPhysics;
using Voronoi2;
using Barotrauma.Networking;
using Networking;

namespace BarotraumaDieHard
{
    // 配置类
    public static class BuoyancyConfig
    {
        public enum SubmarineRole
        {
            Scout,      // 侦察型
            Attack,     // 战斗型
            Transport,  // 运输型
            Undefined,
            Shuttle
        }

        // 不同潜艇类型的进水容忍阈值（常规舱整体超过此比例才开始损失浮力）
        public static readonly Dictionary<SubmarineRole, float> FloodToleranceByRole = new Dictionary<SubmarineRole, float>
        {
            { SubmarineRole.Scout, 0.2f },     
            { SubmarineRole.Attack, 0.25f },    
            { SubmarineRole.Transport, 0.35f },  
            { SubmarineRole.Shuttle, 0.1f },
            { SubmarineRole.Undefined, 0.15f } 
        };

        public const float DefaultFloodTolerance = 0.15f;
        public const float NonBallastFloodMultiplier = 0.45f;
    }

    [HarmonyPatch(typeof(SubmarineBody))]
    public class SubmarineBodyPatch
    {
        private static Dictionary<Submarine, float> floodToleranceCache = new Dictionary<Submarine, float>();

        private static BuoyancyConfig.SubmarineRole GetSubmarineRole(Submarine sub)
        {
            if (sub?.Info == null) return BuoyancyConfig.SubmarineRole.Attack;

            string subName = sub.Info.Name?.ToLower() ?? "";
            string subClass = sub.Info.SubmarineClass.ToString().ToLower();

            if (subName.Contains("scout") || subName.Contains("侦察") || subClass.Contains("scout"))
                return BuoyancyConfig.SubmarineRole.Scout;
            
            if (subName.Contains("transport") || subName.Contains("运输") || subClass.Contains("cargo") || subClass.Contains("transport"))
                return BuoyancyConfig.SubmarineRole.Transport;

            if (subClass.Contains("shuttle") || subName.Contains("shuttle"))
                return BuoyancyConfig.SubmarineRole.Shuttle;

            if (subClass.Contains("attack") || subName.Contains("combat") || subName.Contains("战斗"))
                return BuoyancyConfig.SubmarineRole.Attack;
            
            return BuoyancyConfig.SubmarineRole.Undefined;
        }

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

        public static void ClearCache(Submarine sub = null)
        {
            if (sub == null)
                floodToleranceCache.Clear();
            else
                floodToleranceCache.Remove(sub);
        }

        [HarmonyPrefix]
        [HarmonyPatch("CalculateBuoyancy")]
        public static bool CalculateBuoyancyPrefix(SubmarineBody __instance, ref Vector2 __result)
        {
            var submarine = __instance.Submarine;
            if (submarine == null) return true;

            if (__instance.Body.FarseerBody.BodyType == FarseerPhysics.BodyType.Static || Submarine.LockY)
            {
                __result = Vector2.Zero;
                return false;
            }

            var connectedSubs = submarine.GetConnectedSubs();
            
            // 压载舱统计
            float ballastWaterVolume = 0f;
            float ballastVolume = 0f;
            
            // 常规舱统计
            float totalNonBallastVolume = 0f;
            float totalRealNonBallastWater = 0f; 

            float floodTolerance = GetFloodTolerance(submarine);

            foreach (Hull hull in Hull.HullList)
            {
                if (hull.Submarine == null || !connectedSubs.Contains(hull.Submarine)) 
                    continue;
                
                if (hull.Submarine.PhysicsBody is not { BodyType: FarseerPhysics.BodyType.Dynamic }) 
                    continue;

                bool isBallast = hull.RoomName != null && hull.RoomName.ToLower().Contains("ballast");

                if (isBallast)
                {
                    ballastVolume += hull.Volume;
                    ballastWaterVolume += hull.WaterVolume;
                }
                else
                {
                    totalNonBallastVolume += hull.Volume;
                    totalRealNonBallastWater += hull.WaterVolume;
                }
            }
            
            // 【核心修正】：基于常规舱的“总整体进水率”来判断和折算有效积水
            float realNonBallastWaterPercentage = totalNonBallastVolume > 0f ? totalRealNonBallastWater / totalNonBallastVolume : 0f;
            float nonBallastEffectiveWater = 0f;

            if (realNonBallastWaterPercentage > floodTolerance)
            {
                // 整体超过容忍阈值后，计算总超额水量： (当前总比例 - 容忍比例) * 总常规舱容积
                float excessWaterVolume = (realNonBallastWaterPercentage - floodTolerance) * totalNonBallastVolume;
                // 对超额水量应用减缓系数
                nonBallastEffectiveWater = excessWaterVolume * BuoyancyConfig.NonBallastFloodMultiplier;
            }
            // 如果常规舱总进水率没到容忍阈值（例如目前只有 4.1% < 20%），则 nonBallastEffectiveWater 保持为 0，完全不产生下沉负荷！

            // 汇总计算
            float totalEffectiveWater = ballastWaterVolume + nonBallastEffectiveWater;
            float totalEffectiveVolume = ballastVolume + totalNonBallastVolume;
            
            if (totalEffectiveVolume <= 0f)
            {
                __result = Vector2.Zero;
                return false;
            }

            float waterPercentage = totalEffectiveWater / totalEffectiveVolume;
            float buoyancy = Barotrauma.SubmarineBody.NeutralBallastPercentage - waterPercentage;
            
            buoyancy = MathHelper.Clamp(buoyancy, -0.5f, 0.2f);
            
            float totalMass = connectedSubs.Sum(s => s.SubBody?.Body?.Mass ?? 0f);
            if (totalMass <= 0f)
            {
                __result = Vector2.Zero;
                return false;
            }
            
            float massRatio = __instance.Body.Mass / totalMass;
            float forceY = buoyancy * totalMass * 10f * massRatio;
            
            // 打印修正后的精准日志
            /*DebugConsole.NewMessage(
                $"[Buoyancy] Sub: {submarine.Info.Name} | Tor: {floodTolerance:P0}\n" +
                $" -> [常规舱] 真实整体进水: {realNonBallastWaterPercentage:P1} (阈值:{floodTolerance:P0}) | 折算后有效水体积: {nonBallastEffectiveWater:F0}\n" +
                $" -> [结算] 最终进水率: {waterPercentage:P1} | 垂直总合力: {forceY:F0}", 
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

        // ==========================================
        // 地形破坏相关逻辑 (保持原样)
        // ==========================================
        // 触发地形破坏的撞击力硬阈值（可以根据测试反馈自行调整）
        // 原版游戏里撞击力 wallImpact 大于 MinCollisionImpact (通常是 1-2 左右) 就会对潜艇造成伤害
        // 乘了 WallImpactMultiplier(3.0) 之后，我们设定一个合理的“撞碎岩石”所需的冲击力
        public const float MinImpactToDestroyWall = 12.0f; 

        [HarmonyPatch("HandleLevelCollision")]
        [HarmonyPostfix]
        public static void Postfix(object __instance, object impact, VoronoiCell cell)
        {
#if CLIENT
            if (GameMain.Client != null) return;
#endif
            if (Level.Loaded == null || cell == null) return;

            var impactType = impact.GetType();
            Vector2 velocity = (Vector2)(impactType.GetField("Velocity")?.GetValue(impact) ?? Vector2.Zero);
            Vector2 normal = (Vector2)(impactType.GetField("Normal")?.GetValue(impact) ?? Vector2.Zero);
            Vector2 impactPosSim = (Vector2)(impactType.GetField("ImpactPos")?.GetValue(impact) ?? Vector2.Zero);
            //冲击力计算，带上3倍乘数

            float wallImpact = Vector2.Dot(velocity, -normal) * 3.0f;
            if (wallImpact < MinImpactToDestroyWall) return;

            Vector2 worldPosition = ConvertUnits.ToDisplayUnits(impactPosSim);
            // 坑最少300像素半径，最大800
            float worldRange = MathHelper.Clamp(wallImpact * 30f, 300f, 800f);

            var cellsToDestroy = ExplosionDamageWallPatchInvoke(worldPosition, worldRange);
            if (cellsToDestroy == null || cellsToDestroy.Count == 0) return;

            ExplosionDamageWallPatch.ExecuteTerrainDestruction(cellsToDestroy, worldPosition);

#if SERVER
            if (GameMain.Server != null)
            {
                IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.DESTROY_MAIN_WALL); 
                msg.WriteSingle(worldPosition.X);
                msg.WriteSingle(worldPosition.Y);
                msg.WriteSingle(worldRange); 
                NetUtil.SendAll(msg, DeliveryMethod.Reliable);
            }
#endif
        }

        private static List<VoronoiCell> ExplosionDamageWallPatchInvoke(Vector2 worldPos, float range)
        {
            var method = typeof(ExplosionDamageWallPatch).GetMethod("ServerOrLocalTriggerSubdivision", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return method?.Invoke(null, new object[] { worldPos, range }) as List<VoronoiCell>;
        }
    }
}