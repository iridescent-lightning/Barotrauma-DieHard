﻿using FarseerPhysics;
using FarseerPhysics.Dynamics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using Barotrauma;
using Barotrauma.Items.Components;
using HarmonyLib;
using Voronoi2;

namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(RepairTool))]
    public static class DrillRepairToolPatch
    {
        // 维护独立的开凿进度（Item -> 累计时间秒数）
        private static readonly Dictionary<Item, float> DrillProgressRegistry = new Dictionary<Item, float>();

        // 成功钻好一个炸药固定孔所需的持续工作时间
        public const float RequiredDrillTime = 4.0f;

        [HarmonyPatch(nameof(RepairTool.Use))]
        [HarmonyPostfix]
        public static void Postfix(RepairTool __instance, float deltaTime, Character character)
        {
            // ------------------------------------------------====================
            // 1. 严格的安全检查
            // ----------------------------------------------------------------====
            if (__instance.Item == null || Level.Loaded == null || character == null) return;
            if (__instance.Item.Prefab.Identifier != "handdrill") return;

            // ------------------------------------------------====================
            // 2. 废弃官方私有字段，自主进行严格的射线探测（杜绝隔空大后期钻墙 Bug）
            // ----------------------------------------------------------------====
            // 获取工具当前的发射起点与朝向（利用 RepairTool 自身的 Range 属性限制距离）
            Vector2 startWorldPos = __instance.Item.WorldPosition;
            Vector2 startSimPos = ConvertUnits.ToSimUnits(startWorldPos);
            Holdable holdable = __instance.Item.GetComponent<Holdable>();
            
            // 计算射线的终点（根据角色瞄准方向延伸 Range 距离）
            float rangeSim = ConvertUnits.ToSimUnits(150f);
            Vector2 aimDir = new Vector2((float)Math.Cos(holdable.AimAngle), (float)Math.Sin(holdable.AimAngle));
            if (character.AnimController.Dir < 1.0f) aimDir.X = -aimDir.X; // 修正左右朝向
            
            Vector2 endSimPos = startSimPos + aimDir * rangeSim;

            Fixture closestWallFixture = null;
            Vector2 hitSimPos = Vector2.Zero;

            // 物理射线检测：只寻找 CollisionLevel 级别的地形岩壁
            GameMain.World.RayCast((f, point, normal, fraction) =>
            {
                if (f.CollisionCategories == Physics.CollisionLevel)
                {
                    closestWallFixture = f;
                    hitSimPos = point;
                    return fraction; // 锁定最近的目标
                }
                return -1;
            }, startSimPos, endSimPos);

            // 如果当前帧没有真真切切地碰到岩壁，或者距离超出了，立刻重置进度并退出！
            if (closestWallFixture == null || hitSimPos == Vector2.Zero)
            {
                DrillProgressRegistry.Remove(__instance.Item);
                return;
            }

            // 提取 VoronoiCell
            VoronoiCell targetedCell = closestWallFixture.UserData as VoronoiCell;
            if (targetedCell == null || targetedCell.CellType == CellType.Removed)
            {
                DrillProgressRegistry.Remove(__instance.Item);
                return;
            }

            // ------------------------------------------------====================
            // 3. 步进进度条
            // ----------------------------------------------------------------====
            if (!DrillProgressRegistry.ContainsKey(__instance.Item))
            {
                DrillProgressRegistry[__instance.Item] = 0f;
            }

            DrillProgressRegistry[__instance.Item] += deltaTime;
            float currentProgress = DrillProgressRegistry[__instance.Item];

            // ------------------------------------------------====================
            // 4. 客户端独占：特效与可见 HUD 渲染
            // ----------------------------------------------------------------====
#if CLIENT
            if (GameMain.NetworkMember == null || GameMain.NetworkMember.IsClient)
            {
                // 仅对自己控制的角色绘制本地进度条
                if (character == Character.Controlled)
                {
                    character.UpdateHUDProgressBar(
                        __instance.Item, 
                        ConvertUnits.ToDisplayUnits(hitSimPos), 
                        currentProgress / RequiredDrillTime, 
                        Color.DarkCyan * 0.6f, 
                        Color.Lime,
                        textTag: "progressbar.drilling"
                    );
                }

                // 喷射粒子
                Vector2 hitDisplayPos = ConvertUnits.ToDisplayUnits(hitSimPos);
                if (Rand.Range(0f, 1f) < 0.6f)
                {
                    GameMain.ParticleManager.CreateParticle("Drillspark", hitDisplayPos, Rand.Vector(120f), 0.0f, character.AnimController?.CurrentHull);
                }
                if (Rand.Range(0f, 1f) < 0.4f)
                {
                    Vector2 gravelVelocity = new Vector2(Rand.Range(-30f, 30f), Rand.Range(-50f, -10f));
                    GameMain.ParticleManager.CreateParticle("Drillsmoke", hitDisplayPos, gravelVelocity, 0.0f, character.AnimController?.CurrentHull);
                }
            }
#endif
            if (character?.AnimController != null && Rand.Range(0f, 1f) < 0.5f)
                {
                    // 顺着骨骼系统寻找角色的右手或持物手，动态灌入一个瞬时推力（Impulse）
                    var handLimb = character.AnimController.GetLimb(LimbType.RightHand) ?? character.AnimController.GetLimb(LimbType.LeftHand);
                    if (handLimb?.body != null)
                    {
                        // 施加一个极小、高频方向交替的仿真力，让手臂看起来因为反冲力而在痉挛、麻木
                        Vector2 armJolt = Rand.Vector(0.5f); 
                        handLimb.body.ApplyLinearImpulse(armJolt);
                    }
                }

            // ------------------------------------------------====================
            // 5. 核心修复：服务器权威实体生成（网络安全）
            // ----------------------------------------------------------------====
            if (currentProgress >= RequiredDrillTime)
            {
                // 无论如何，进度满了都在本地先清除计数，防止死循环触发
                DrillProgressRegistry.Remove(__instance.Item);

                // 核心卡点：只有单人游戏(null) 或 多人联机的服务器端(IsServer) 才能获准调用生成逻辑！
                if (GameMain.NetworkMember == null || GameMain.NetworkMember.IsServer)
                {
                    ItemPrefab socketPrefab = ItemPrefab.GetItemPrefab("bore");
                    if (socketPrefab != null)
                    {
                        // 修正：计算碰撞点的世界像素坐标（WorldPosition）
                        Vector2 spawnWorldPos = ConvertUnits.ToDisplayUnits(hitSimPos);

                        // 由服务器将其压入队列，Entity.Spawner 内部在检测到是服务器时，
                        // 会自动创建网络事件（NetworkEvent）并将生成的数据同步发给每一个联机客户端！
                        Entity.Spawner.AddItemToSpawnQueue(socketPrefab, spawnWorldPos, onSpawned: (Item spawnedEntity) =>
                        {
                            if (spawnedEntity == null) return;

                            // 联机下如果需要在各个客户端传递 UserData，建议使用自定义网络网包。
                            // 本地单机状态下可以正常直接挂载：
                            //socketEntity.UserData = targetedCell;

                            #if SERVER
                            DebugConsole.NewMessage($"[服务器] 钻孔成功！成功生成并网络同步 bore。物体ID: {spawnedEntity.ID}", Color.Lime);
                            #endif
                        });
                    }
                }
            }
        }
    }
}