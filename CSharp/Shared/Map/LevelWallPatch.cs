using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Barotrauma;
using HarmonyLib;
using Microsoft.Xna.Framework;
using FarseerPhysics.Dynamics;
using Voronoi2;

using Barotrauma.Networking;
using FarseerPhysics;
using Barotrauma.Extensions;
using System.Runtime.CompilerServices;
using Barotrauma.Items.Components;
using Networking;

namespace BarotraumaDieHard
{
    [HarmonyPatch]
    public static class LevelWallDestructionPatch
    {
        // 使用字典来为原版 LevelWall 动态扩展“方块血量”
        private static readonly Dictionary<VoronoiCell, float> CellHealthRegistry = new Dictionary<VoronoiCell, float>();
        private const float DefaultCellHealth = 200.0f; // 挖开一块普通石头需要的伤害

        [HarmonyPatch(typeof(DestructibleLevelWall), nameof(DestructibleLevelWall.AddDamage), new Type[] { typeof(float), typeof(Vector2) })]
        [HarmonyPrefix]
        public static bool PrefixDestructibleAddDamage(float damage, Vector2 worldPosition)
        {
            // 让原版的动态墙体（原本就可破坏的资源矿、冰墙等）保持原样
            return true; 
        }

        /// <summary>
        /// 核心：给普通 LevelWall 施加伤害的通用接口
        /// </summary>
        public static void DamageLevelWall(LevelWall wall, float damage, Vector2 worldPosition)
        {
            DebugConsole.NewMessage("try damage");
            if (wall == null || wall.Cells == null || wall.Cells.Count == 0) return;
            if (wall is DestructibleLevelWall) return; // 让可破坏墙体走它自己的原版逻辑

            // 1. 将世界坐标转为仿真单位坐标
            Vector2 simPos = ConvertUnits.ToSimUnits(worldPosition);

            // 2. 精准定位被命中的方块（Cell）
            VoronoiCell targetCell = null;
            foreach (var cell in wall.Cells)
            {
                if (cell.IsPointInside(simPos))
                {
                    targetCell = cell;
                    break;
                }
            }

            // 如果没精确点中内部，取最近的
            if (targetCell == null)
            {
                targetCell = wall.Cells.OrderBy(c => Vector2.DistanceSquared(c.Center, simPos)).FirstOrDefault();
            }

            if (targetCell == null) return;

            // 3. 扣除血量
            if (!CellHealthRegistry.ContainsKey(targetCell))
            {
                CellHealthRegistry[targetCell] = DefaultCellHealth;
            }

            CellHealthRegistry[targetCell] -= damage;

            // 4. 血量归零，挖掉这一块
            if (CellHealthRegistry[targetCell] <= 0)
            {
                CellHealthRegistry.Remove(targetCell);
                ExtractAndRebuildCell(wall, targetCell, worldPosition);
            }
        }

        /// <summary>
        /// 核心重构算法：剥离单个 Cell 并通知 Farseer 物理与 XNA 渲染重构
        /// </summary>
        private static void ExtractAndRebuildCell(LevelWall wall, VoronoiCell deadCell, Vector2 worldPosition)
        {
            var level = Traverse.Create(wall).Field("level").GetValue<Level>();
            var color = Traverse.Create(wall).Field("color").GetValue<Color>();

            // 1. 移除方块并触发原版注销委托
            wall.Cells.Remove(deadCell);
            deadCell.CellType = CellType.Removed;
            deadCell.OnDestroyed?.Invoke();
            deadCell.OnDestroyed = null;

            // 2. 播放挖掘特效
            TriggerDigEffects(deadCell, worldPosition);

            // 3. 彻底清理旧墙体在物理世界的刚体
            if (wall.Body != null)
            {
                GameMain.World.Remove(wall.Body);
                DebugConsole.NewMessage("wall body removed");
            }

#if CLIENT
            // 4. 客户端清理：利用 Harmony 销毁旧的 VertexBuffer 防止渲染出错
            var vertexBufferField = AccessTools.Field(typeof(LevelWall), "VertexBuffer");
            if (vertexBufferField != null)
            {
                var vb = vertexBufferField.GetValue(wall) as IDisposable;
                vb?.Dispose();
                vertexBufferField.SetValue(wall, null);
            }
#endif

            // 如果整面墙被挖空了，直接从地图里移除这个对象
            if (wall.Cells.Count == 0)
            {
                DebugConsole.NewMessage("wall object deleted");
                level?.UnsyncedExtraWalls?.Remove(wall);
                return;
            }

            // 5. 拓扑分裂检测（防止挖断墙壁导致物理拉伸 Bug）
            List<List<VoronoiCell>> islands = FindConnectedIslands(wall.Cells);

            // 6. 将第一座岛留给当前墙体对象重构
            // 【已修复：修改为官方底层需要的 List<Vector2[]>】
            List<Vector2[]> remainingTriangles;
            var newBody = CaveGenerator.GeneratePolygons(islands[0], level, out remainingTriangles);
            
            // 更改属性
            Traverse.Create(wall).Property("Cells").SetValue(islands[0]);
            Traverse.Create(wall).Property("Body").SetValue(newBody);

#if CLIENT
            // 重新调用原版的私有渲染网格生成方法
            AccessTools.Method(typeof(LevelWall), "GenerateVertices")?.Invoke(wall, null);
#endif

            // 7. 如果产生了悬空的“碎石孤岛”，为它们创建全新的独立物理墙体
            for (int i = 1; i < islands.Count; i++)
            {
                var islandVerts = islands[i].SelectMany(c => c.Edges.Select(e => e.Point1)).Distinct().ToList();
                if (islandVerts.Count >= 3)
                {
                    // 动态实例化一面新墙并塞进关卡
                    LevelWall newIslandWall = new LevelWall(islandVerts, color, level, giftWrap: true, createBody: true);
                    level?.UnsyncedExtraWalls?.Add(newIslandWall);
                }
            }
        }

        // BFS 洪泛算法：检查方块因被挖走而导致的断裂分裂
        private static List<List<VoronoiCell>> FindConnectedIslands(List<VoronoiCell> allCells)
        {
            List<List<VoronoiCell>> islands = new List<List<VoronoiCell>>();
            HashSet<VoronoiCell> visited = new HashSet<VoronoiCell>();

            foreach (var cell in allCells)
            {
                if (visited.Contains(cell)) continue;

                List<VoronoiCell> island = new List<VoronoiCell>();
                Queue<VoronoiCell> queue = new Queue<VoronoiCell>();

                queue.Enqueue(cell);
                visited.Add(cell);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    island.Add(current);

                    foreach (var edge in current.Edges)
                    {
                        VoronoiCell neighbor = (edge.Cell1 == current) ? edge.Cell2 : edge.Cell1;
                        if (neighbor != null && allCells.Contains(neighbor) && !visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }
                islands.Add(island);
            }
            return islands;
        }

        private static void TriggerDigEffects(VoronoiCell cell, Vector2 worldPosition)
        {
#if CLIENT
            SoundPlayer.PlaySound("rockbreak", worldPosition);
            Vector2 cellCenterWorld = ConvertUnits.ToDisplayUnits(cell.Center) + cell.Translation;
            for (int i = 0; i < 6; i++)
            {
                GameMain.ParticleManager.CreateParticle("dustparticles", 
                    cellCenterWorld + Rand.Vector(15.0f), 
                    velocity: Rand.Vector(120.0f));
            }
#endif
        }
    }

    

}