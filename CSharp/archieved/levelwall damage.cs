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

    // 【核心修复：完美挂载到官方的 RangedStructureDamage 静态方法上】
    [HarmonyPatch(typeof(Explosion))]
    public class ExplosionDamageWallPatch
    {

[HarmonyPatch(typeof(Explosion), "RangedStructureDamage")]
    [HarmonyPostfix]
    public static void Postfix(Vector2 worldPosition, float worldRange, float damage, float levelWallDamage)
    {
        // 【关键防御】：如果是联机模式下的客户端，绝对不能在 Postfix 里直接计算，必须等服务器发包！
#if CLIENT
        if (GameMain.Client != null) return;
#endif

        if (Level.Loaded == null) return;

        // 1. 转为仿真单位并射线探测（与你原本逻辑一致）
        Vector2 simStartPos = ConvertUnits.ToSimUnits(worldPosition);
        float simCheckRadius = ConvertUnits.ToSimUnits(worldRange + 9999960.0f);

        FarseerPhysics.Dynamics.Body rockBody = null;
        int rayCount = 16;
        for (int i = 0; i < rayCount; i++)
        {
            float angle = (MathHelper.TwoPi / rayCount) * i;
            Vector2 direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
            Vector2 simEndPos = simStartPos + direction * simCheckRadius;

            var hitBodies = Submarine.PickBodies(simStartPos, simEndPos, collisionCategory: FarseerPhysics.Dynamics.Category.All);
            foreach (var body in hitBodies)
            {
                if (body != null && body.BodyType == FarseerPhysics.BodyType.Static)
                {
                    rockBody = body;
                    break;
                }
            }
            if (rockBody != null) break;
        }

        if (rockBody == null) return;

        // 2. 获取全局细胞池
        var cellsField = typeof(Level).GetField("cells", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var allLevelCells = cellsField?.GetValue(Level.Loaded) as List<Voronoi2.VoronoiCell>;
        if (allLevelCells == null || allLevelCells.Count == 0) return;

        // 3. 数学粗筛出需要被摧毁的细胞
        float checkRangeDisplay = worldRange + 1700.0f;
        List<Voronoi2.VoronoiCell> cellsToDestroy = new List<Voronoi2.VoronoiCell>();

        foreach (var cell in allLevelCells)
        {
            if (cell == null || cell.CellType == Voronoi2.CellType.Removed) continue;

            Vector2 cellWorldPos = cell.Center + cell.Translation;
            float dist = Vector2.Distance(cellWorldPos, worldPosition);

            if (dist <= checkRangeDisplay)
            {
                cellsToDestroy.Add(cell);
            }
        }

        if (cellsToDestroy.Count == 0) return;

        // 4. 【本地立刻执行】：如果是单机模式，或者是服务器本地，立刻执行物理移除与重构
        ExecuteTerrainDestruction(cellsToDestroy, worldPosition);

        // 5. 【服务器专属广播】：如果是服务器，把这些要摧毁的细胞坐标发送给所有客户端
#if SERVER
        if (GameMain.Server != null)
        {
            IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.DESTROY_MAIN_WALL);
            
            // 写入爆炸中心点，用于客户端播放声音和特效
            msg.WriteVector2(worldPosition);
            
            // 写入被炸毁的细胞数量
            msg.WriteInt32(cellsToDestroy.Count);
            
            // 写入每一个细胞的 Center 坐标（作为辨别依据）
            foreach (var cell in cellsToDestroy)
            {
                msg.WriteVector2(cell.Center); 
            }

            // 发送给全员（Reliable 确保不丢包）
            NetUtil.SendAll(msg, DeliveryMethod.Reliable);
            DebugConsole.NewMessage($"[SERVER] 成功向所有客户端同步了 {cellsToDestroy.Count} 个细胞的销毁指令", Color.Green);
        }
#endif
    }

        /// <summary>
    /// 网络消息接收端（所有客户端和服务器收到包后都会进这里，但服务器通过上面防御了重复处理）
    /// </summary>
    public static void OnReceiveDestroyWallMessage(object[] args)
    {
        if (args == null || args.Length == 0) return;
        if (args[0] is not IReadMessage msg) return;

        // 1. 严格按照写入顺序读取
        Vector2 worldPosition = msg.ReadVector2();
        int cellCount = msg.ReadInt32();
        
        List<Vector2> targetCellCenters = new List<Vector2>();
        for (int i = 0; i < cellCount; i++)
        {
            targetCellCenters.Add(msg.ReadVector2());
        }

        // 2. 客户端网络安全防御：如果由于某种原因服务器在单机环境里触发（虽然不会），或者数据损坏，直接返回
        if (Level.Loaded == null) return;

        // 3. 找出客户端本地对应的细胞实例
        var cellsField = typeof(Level).GetField("cells", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var allLevelCells = cellsField?.GetValue(Level.Loaded) as List<Voronoi2.VoronoiCell>;
        if (allLevelCells == null) return;

        List<Voronoi2.VoronoiCell> cellsToDestroy = new List<Voronoi2.VoronoiCell>();
        
        // 根据服务器传来的 Center 坐标匹配客户端本地的 Cell 实例
        foreach (var cell in allLevelCells)
        {
            if (cell == null || cell.CellType == Voronoi2.CellType.Removed) continue;
            
            // 允许微小的浮点数误差
            if (targetCellCenters.Any(c => Vector2.DistanceSquared(c, cell.Center) < 0.1f))
            {
                cellsToDestroy.Add(cell);
            }
        }

        // 4. 【同步核心】：客户端在本地也执行一模一样的物理与渲染切片逻辑
#if CLIENT
        if (GameMain.Client != null)
        {
            // 仅在联机客户端下才进入，因为服务器在 Postfix 里已经自己执行过了本地重构
            ExecuteTerrainDestruction(cellsToDestroy, worldPosition);
        }
#endif
    }

    /// <summary>
    /// 核心粉碎与渲染、物理网格重构方法（双端公用核心）
    /// </summary>
    public static void ExecuteTerrainDestruction(List<Voronoi2.VoronoiCell> cellsToDestroy, Vector2 worldPosition)
    {
        if (cellsToDestroy == null || cellsToDestroy.Count == 0) return;

        var cellsField = typeof(Level).GetField("cells", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var allLevelCells = cellsField?.GetValue(Level.Loaded) as List<Voronoi2.VoronoiCell>;
        if (allLevelCells == null) return;

        // 1. 获取当前场景残留的真岩壁旧 Body 并将其移除
        FarseerPhysics.Dynamics.Body rockBody = null;
        foreach (var cell in cellsToDestroy)
        {
            if (cell.Body != null)
            {
                rockBody = cell.Body; // 只要能抓到一个，说明它就是全图主刚体
                break;
            }
        }

        // 2. 移除方块并触发原版注销
        foreach (var deadCell in cellsToDestroy)
        {
            allLevelCells.Remove(deadCell);
            deadCell.CellType = Voronoi2.CellType.Removed;
            deadCell.OnDestroyed?.Invoke();
            deadCell.OnDestroyed = null;
        }
        GameMain.World.ProcessChanges();

        // 播放效果（仅客户端或单机执行）
#if CLIENT
        SoundPlayer.PlaySound("rockbreak", worldPosition);
        foreach (var deadCell in cellsToDestroy)
        {
            Vector2 cellCenterWorld = ConvertUnits.ToDisplayUnits(deadCell.Center) + deadCell.Translation;
            for (int i = 0; i < 4; i++)
            {
                GameMain.ParticleManager.CreateParticle("heavygib", cellCenterWorld + Rand.Vector(15.0f), velocity: Rand.Vector(120.0f));
            }
        }
#endif

        // 3. 彻底在物理世界移除旧刚体
        if (rockBody != null)
        {
            GameMain.World.Remove(rockBody);
        }

        // 4. 重新计算并建立全图干净的静态刚体
        List<Vector2[]> newTriangles;
        var newRockBody = CaveGenerator.GeneratePolygons(allLevelCells, Level.Loaded, out newTriangles);
        newRockBody.BodyType = FarseerPhysics.BodyType.Static;
        newRockBody.CollisionCategories = Physics.CollisionLevel;

        // 5. 核心修正：完美同步并重新计算客户端的高仿官方渲染顶点
#if CLIENT
        try
        {
            if (Level.Loaded?.Renderer != null)
            {
                var levelRenderer = Level.Loaded.Renderer;

                // 释放并清空旧的显存缓冲
                var vertexBuffersField = typeof(LevelRenderer).GetField("vertexBuffers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (vertexBuffersField != null)
                {
                    var vbs = vertexBuffersField.GetValue(levelRenderer) as System.Collections.IList;
                    if (vbs != null)
                    {
                        foreach (var vb in vbs)
                        {
                            (vb as IDisposable)?.Dispose();
                        }
                        vbs.Clear();
                    }
                }

                var genParams = Level.Loaded.GenerationParams;

                // A. 重新生成实际黑色墙面纹理顶点
                var wallVerts = CaveGenerator.GenerateWallEdgeVertices(
                    allLevelCells,
                    expandOutwards: 0.0f,
                    expandInwards: genParams.WallTextureExpandInwardsAmount,
                    outerColor: genParams.WallColor,
                    innerColor: Color.Black,
                    Level.Loaded,
                    zCoord: 0.9f,
                    preventExpandThroughCell: true
                ).ToArray();

                CaveGenerator.GenerateTextureCoordinates(wallVerts, genParams.WallTextureSize);

                // B. 生成外边缘贴图顶点
                var wallEdgeVerts = CaveGenerator.GenerateWallEdgeVertices(
                    allLevelCells,
                    genParams.WallEdgeExpandOutwardsAmount,
                    genParams.WallEdgeExpandInwardsAmount,
                    outerColor: genParams.WallColor,
                    innerColor: genParams.WallColor,
                    Level.Loaded,
                    zCoord: 0.9f
                ).ToArray();

                // C. 利用刚刚重新计算得出的 newTriangles 生成最内层纯黑遮罩
                var wallInnerVerts = CaveGenerator.GenerateWallVertices(
                    newTriangles,
                    Color.Black,
                    zCoord: 0.9f
                ).ToArray();

                var wallTexture = genParams.WallSprite.Texture;
                var edgeTexture = genParams.WallEdgeSprite.Texture;

                // 提交给渲染器显卡重建缓冲，贴图彻底实时刷新
                levelRenderer.SetVertices(wallVerts, wallEdgeVerts, wallInnerVerts, wallTexture, edgeTexture);
            }
        }
        catch (Exception ex)
        {
            DebugConsole.NewMessage($"[CLIENT 渲染重构异常] {ex.Message}");
        }
#endif
    }
    }
}