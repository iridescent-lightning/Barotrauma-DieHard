﻿using Barotrauma;
using HarmonyLib;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(Structure))]
    public static class StructurePatch
    {
        // 记录已经爆裂过的 Section，防止重复生成碎片
        private static readonly HashSet<string> explodedSections = new HashSet<string>();

        [HarmonyPatch("UpdateSections")]
        [HarmonyPrefix]
        public static void Prefix(Structure __instance)
        {
            // 只在服务器或单机模式下运行逻辑，防止同步风暴
            if (GameMain.NetworkMember != null && GameMain.NetworkMember.IsClient) { return; }

            for (int i = 0; i < __instance.SectionCount; i++)
            {
                var section = __instance.GetSection(i);
                string sectionKey = $"{__instance.ID}_{i}";

                // 如果损坏达标且尚未爆裂
                if (section.damage >= __instance.MaxHealth && !explodedSections.Contains(sectionKey))
                {
                    SpawnShrapnel(__instance, i);
                    explodedSections.Add(sectionKey);
                }
                // 如果墙被修好了，移除标记允许下次再次破坏时产生碎片
                else if (section.damage < __instance.MaxHealth && explodedSections.Contains(sectionKey))
                {
                    explodedSections.Remove(sectionKey);
                }
            }
        }

        private static void SpawnShrapnel(Structure structure, int sectionIndex)
{
    // 1. 获取该 Section 的中心世界坐标
    Vector2 sectionWorldPos = structure.SectionPosition(sectionIndex, true);
    
    // 2. 找到该墙体段所属或最邻近的 Hull
    // GetHullLocalPos 会帮助判断该点在哪个房间内
    Hull currentHull = Hull.FindHull(sectionWorldPos, null, true);
    if (currentHull == null) return;

    // 3. 计算从墙体指向房间中心的方向向量
    // currentHull.WorldRect.Center 给出房间的几何中心
    Vector2 toHullCenter = currentHull.WorldPosition - sectionWorldPos;
    
    // 4. 标准化并分离出主要飞溅轴
    // 这样可以确保：底部的墙主要向上飞，顶部的墙主要向下飞
    Vector2 splashDir = Vector2.Zero;
    if (structure.IsHorizontal)
    {
        splashDir.Y = toHullCenter.Y > 0 ? -1.0f : 1.0f; // 底部向上，顶部向下
    }
    else
    {
        splashDir.X = toHullCenter.X > 0 ? 1.0f : -1.0f; // 左侧向右，右侧向左
    }

    // 5. 数量随机性：每个 Section 爆裂产生 1-3 个碎片
    int shrapnelCount = Rand.Range(1, 9); 

    for (int i = 0; i < shrapnelCount; i++)
    {
        Entity.Spawner.AddItemToSpawnQueue(
            ItemPrefab.GetItemPrefab("wallshrapnelprojectile"), 
            sectionWorldPos,
            onSpawned: (Item item) =>
            {
                if (item.body == null) return;

                // 6. 方向随机性：在主推力方向上加入 ±30 度的偏差
                float spreadAngle = Rand.Range(-0.52f, 0.52f); 
                Matrix spreadMatrix = Matrix.CreateRotationZ(spreadAngle);
                
                // 7. 速度随机性：力度在 10 到 25 之间波动
                float forceMagnitude = Rand.Range(30.0f, 35.0f);
                Vector2 finalForce = Vector2.Transform(splashDir, spreadMatrix) * forceMagnitude;

                item.body.ApplyLinearImpulse(finalForce);
                
                // 可选：赋予碎片随机的旋转速度，增加视觉真实感
                item.body.AngularVelocity = Rand.Range(-5.0f, 5.0f);
            });
    }
}
    }
}