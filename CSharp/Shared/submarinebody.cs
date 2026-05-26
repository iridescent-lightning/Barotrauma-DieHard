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
            Combat,     // 战斗型
            Transport,   // 运输型
            Undefined,
            Shuttle
        }

        // 不同潜艇类型的进水容忍阈值（超过此比例才开始损失浮力）
        public static readonly Dictionary<SubmarineRole, float> FloodToleranceByRole = new Dictionary<SubmarineRole, float>
        {
            { SubmarineRole.Scout, 0.25f },     // 15% 进水才开始下沉
            { SubmarineRole.Combat, 0.2f },    // 10% 进水才开始下沉
            { SubmarineRole.Transport, 0.35f },  // 20% 进水才开始下沉
            { SubmarineRole.Shuttle, 0.15f },
            { SubmarineRole.Undefined, 0f }
        };

        // 默认进水容忍阈值
        public const float DefaultFloodTolerance = 0.15f;

        // 非压载舱进水影响系数（达到阈值后的影响程度）
        // 1.0 = 完全影响，0.5 = 只有一半影响
        public const float NonBallastFloodMultiplier = 0.45f;
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


        // 触发地形破坏的撞击力硬阈值（可以根据测试反馈自行调整）
        // 原版游戏里撞击力 wallImpact 大于 MinCollisionImpact (通常是 1-2 左右) 就会对潜艇造成伤害
        // 乘了 WallImpactMultiplier(3.0) 之后，我们设定一个合理的“撞碎岩石”所需的冲击力
        public const float MinImpactToDestroyWall = 12.0f; 

        [HarmonyPatch("HandleLevelCollision")]
        [HarmonyPostfix]
        public static void Postfix(object __instance, object impact, VoronoiCell cell)
        {
#if CLIENT
            // 依旧是网络同步核心原则：客户端不自发运行主破坏逻辑，全听服务器的
            if (GameMain.Client != null) return;
#endif
            if (Level.Loaded == null || cell == null) return;

            // 1. 利用反射或由于依赖项直接获取 impact 对象的属性
            // 考虑到不同开发环境里 Impact 可能是嵌套结构，我们通过反射安全获取其物理数据
            var impactType = impact.GetType();
            Vector2 velocity = (Vector2)(impactType.GetField("Velocity")?.GetValue(impact) ?? Vector2.Zero);
            Vector2 normal = (Vector2)(impactType.GetField("Normal")?.GetValue(impact) ?? Vector2.Zero);
            Vector2 impactPosSim = (Vector2)(impactType.GetField("ImpactPos")?.GetValue(impact) ?? Vector2.Zero);

            // 2. 模拟原版的 wallImpact 计算公式
            float wallImpact = Vector2.Dot(velocity, -normal);
            const float WallImpactMultiplier = 3.0f;
            wallImpact *= WallImpactMultiplier;

            // 冲击力不够，不把墙撞出坑
            if (wallImpact < MinImpactToDestroyWall) return;

            // 3. 将物理引擎的 Sim 单位坐标转换为游戏渲染的 Display/World 坐标
            Vector2 worldPosition = ConvertUnits.ToDisplayUnits(impactPosSim);

            // 4. 根据撞击力度动态决定“撞出来的坑”的大小（半径）
            // 比如：最少 300 像素半径，最多 800 像素半径
            float worldRange = MathHelper.Clamp(wallImpact * 30f, 300f, 800f);

            // 5. 借用你在 ExplosionDamageWallPatch 里写好的权威切分逻辑
            // 反射或直接调用（如果它们在同一个命名空间下且是 public/internal）
            var cellsToDestroy = ExplosionDamageWallPatchInvoke(worldPosition, worldRange);
            if (cellsToDestroy == null || cellsToDestroy.Count == 0) return;

            // 6. 执行服务器/单机本地的地形重建
            ExplosionDamageWallPatch.ExecuteTerrainDestruction(cellsToDestroy, worldPosition);

#if SERVER
            // 7. 【网络同步】：通知全房间客户端，在这个坐标发生了多大半径的潜艇撞击，让他们本地切分
            if (GameMain.Server != null)
            {
                IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.DESTROY_MAIN_WALL); // 复用爆炸的网络事件 ID
                msg.WriteSingle(worldPosition.X);
                msg.WriteSingle(worldPosition.Y);
                msg.WriteSingle(worldRange); 
                NetUtil.SendAll(msg, DeliveryMethod.Reliable);
            }
#endif
        }

        /// <summary>
        /// 私有辅助方法：通过反射或直接访问安全调用你之前写好的 Subdivision 逻辑
        /// </summary>
        private static List<VoronoiCell> ExplosionDamageWallPatchInvoke(Vector2 worldPos, float range)
        {
            // 如果你的 ServerOrLocalTriggerSubdivision 改为 public，这里可以直接安全调用：
            // return ExplosionDamageWallPatch.ServerOrLocalTriggerSubdivision(worldPos, range);

            // 如果保持 private，则使用下方反射：
            var method = typeof(ExplosionDamageWallPatch).GetMethod("ServerOrLocalTriggerSubdivision", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return method?.Invoke(null, new object[] { worldPos, range }) as List<VoronoiCell>;
        }
    
    }
}