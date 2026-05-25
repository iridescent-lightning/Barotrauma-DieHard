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

        // 1. 转为仿真单位并射线探测
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

        // 5. 【服务器专属广播】：如果是服务器，把这些要摧毁的细胞坐标拆解发送给所有客户端
#if SERVER
        if (GameMain.Server != null)
        {
            IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.DESTROY_MAIN_WALL);
            
            // 【修正】拆解写入爆炸中心点的 X 和 Y
            msg.WriteSingle(worldPosition.X);
            msg.WriteSingle(worldPosition.Y);
            
            // 写入被炸毁的细胞数量
            msg.WriteInt32(cellsToDestroy.Count);
            
            // 【修正】拆解写入每一个细胞的 Center.X 和 Center.Y 
            foreach (var cell in cellsToDestroy)
            {
                msg.WriteSingle(cell.Center.X);
                msg.WriteSingle(cell.Center.Y);
            }

            // 发送给全员（Reliable 确保不丢包）
            NetUtil.SendAll(msg, DeliveryMethod.Reliable);
            DebugConsole.NewMessage($"[SERVER] 成功向所有客户端同步了 {cellsToDestroy.Count} 个细胞的销毁指令", Color.Green);
        }
#endif
    }

    /// <summary>
    /// 网络消息接收端
    /// </summary>
    public static void OnReceiveDestroyWallMessage(object[] args)
    {
        if (args == null || args.Length == 0) return;
        if (args[0] is not IReadMessage msg) return;

        // 1. 【修正】严格按照写入顺序重组读取：先读出爆炸坐标
        float expX = msg.ReadSingle();
        float expY = msg.ReadSingle();
        Vector2 worldPosition = new Vector2(expX, expY);

        int cellCount = msg.ReadInt32();
        
        // 【修正】循环读取出每个细胞的 X 和 Y 并还原为 Vector2 列表
        List<Vector2> targetCellCenters = new List<Vector2>();
        for (int i = 0; i < cellCount; i++)
        {
            float cx = msg.ReadSingle();
            float cy = msg.ReadSingle();
            targetCellCenters.Add(new Vector2(cx, cy));
        }

        // 2. 客户端本地安全防御
        if (Level.Loaded == null) return;

        // 3. 找出客户端本地对应的细胞实例
        var cellsField = typeof(Level).GetField("cells", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var allLevelCells = cellsField?.GetValue(Level.Loaded) as List<Voronoi2.VoronoiCell>;
        if (allLevelCells == null) return;

        List<Voronoi2.VoronoiCell> cellsToDestroy = new List<Voronoi2.VoronoiCell>();
        
        // 根据重组的 Center 坐标匹配客户端本地的 Cell
        foreach (var cell in allLevelCells)
        {
            if (cell == null || cell.CellType == Voronoi2.CellType.Removed) continue;
            
            // 允许微小的浮点数误差
            if (targetCellCenters.Any(c => Vector2.DistanceSquared(c, cell.Center) < 0.1f))
            {
                cellsToDestroy.Add(cell);
            }
        }

        // 4. 【同步核心】：客户端在本地执行一模一样的物理与渲染切片逻辑
#if CLIENT
        if (GameMain.Client != null)
        {
            ExecuteTerrainDestruction(cellsToDestroy, worldPosition);
        }
#endif
    }

    /// <summary>
    /// 核心粉碎与渲染、物理网格重构方法（双端通用）
    /// </summary>
    public static void ExecuteTerrainDestruction(List<Voronoi2.VoronoiCell> cellsToDestroy, Vector2 worldPosition)
    {
        if (cellsToDestroy == null || cellsToDestroy.Count == 0) return;

        var cellsField = typeof(Level).GetField("cells", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var allLevelCells = cellsField?.GetValue(Level.Loaded) as List<Voronoi2.VoronoiCell>;
        if (allLevelCells == null) return;

        // 1. 找出谁和这些即将毁灭的方块挨着（幸存的邻居细胞）
        // 这一步是修复声纳线条最核心的源头！
        HashSet<Voronoi2.VoronoiCell> survivingNeighbors = new HashSet<Voronoi2.VoronoiCell>();
        foreach (var deadCell in cellsToDestroy)
        {
            foreach (var edge in deadCell.Edges)
            {
                var neighbor = edge.AdjacentCell(deadCell);
                if (neighbor != null && !cellsToDestroy.Contains(neighbor) && neighbor.CellType != Voronoi2.CellType.Removed)
                {
                    survivingNeighbors.Add(neighbor);
                }
            }
        }

        // 2. 获取当前场景残留的真岩壁旧 Body 并将其移除
        FarseerPhysics.Dynamics.Body rockBody = null;
        foreach (var cell in cellsToDestroy)
        {
            if (cell.Body != null)
            {
                rockBody = cell.Body;
                break;
            }
        }

        // 3. 从主列表中剔除被摧毁的细胞
        foreach (var deadCell in cellsToDestroy)
        {
            allLevelCells.Remove(deadCell);
            deadCell.CellType = Voronoi2.CellType.Removed;
            deadCell.OnDestroyed?.Invoke();
            deadCell.OnDestroyed = null;
        }

        // ====================================================================
        // 【声纳绘制破局补丁：强行重写断裂面的拓扑边缘属性】
        // ====================================================================
        try
        {
            foreach (var neighborCell in survivingNeighbors)
            {
                foreach (var edge in neighborCell.Edges)
                {
                    // 如果这条边的另一侧曾经是刚刚被炸掉的死细胞
                    if (cellsToDestroy.Contains(edge.Cell1) || cellsToDestroy.Contains(edge.Cell2) || 
                        edge.Cell1?.CellType == Voronoi2.CellType.Removed || edge.Cell2?.CellType == Voronoi2.CellType.Removed)
                    {
                        // A. 斩断旧羁绊：让原本指向被炸毁细胞的引用变为空（指向公海）
                        if (edge.Cell1 != null && (cellsToDestroy.Contains(edge.Cell1) || edge.Cell1.CellType == Voronoi2.CellType.Removed)) edge.Cell1 = null;
                        if (edge.Cell2 != null && (cellsToDestroy.Contains(edge.Cell2) || edge.Cell2.CellType == Voronoi2.CellType.Removed)) edge.Cell2 = null;

                        // B. 极其关键：告诉原版声纳系统的 Ping 循环——这是一条外露的“实心固体表面”！
                        edge.IsSolid = true;

                        // C. 重新计算该边缘的几何中点，确保声纳 Vector2.Dot 法线判定时不会报错或出现负值
                        //edge.Center = (edge.Point1 + edge.Point2) / 2.0f;
                    }
                }
            }
            DebugConsole.NewMessage($"[声纳拓扑重组] 成功为 {survivingNeighbors.Count} 个断裂面邻居方块赋予了崭新的声纳高亮反射边缘！", Color.Cyan);
        }
        catch (Exception ex)
        {
            DebugConsole.NewMessage($"[声纳拓扑修复异常] {ex.Message}", Color.Red);
        }

        GameMain.World.ProcessChanges();

        // 播放效果
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

        // 4. 彻底在物理世界移除旧刚体并重建
        if (rockBody != null)
        {
            GameMain.World.Remove(rockBody);
        }

        List<Vector2[]> newTriangles;
        var newRockBody = CaveGenerator.GeneratePolygons(allLevelCells, Level.Loaded, out newTriangles);
        newRockBody.BodyType = FarseerPhysics.BodyType.Static;
        newRockBody.CollisionCategories = Physics.CollisionLevel;

        // 5. 强行剔除声纳快速网格索引矩阵（使射线可以无阻碍打进缺口）
        try
        {
            if (Level.Loaded != null)
            {
                var cellGridField = typeof(Level).GetField("cellGrid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (cellGridField != null)
                {
                    var cellGrid = cellGridField.GetValue(Level.Loaded) as List<Voronoi2.VoronoiCell>[,];
                    if (cellGrid != null)
                    {
                        int width = cellGrid.GetLength(0);
                        int height = cellGrid.GetLength(1);

                        for (int x = 0; x < width; x++)
                        {
                            for (int y = 0; y < height; y++)
                            {
                                if (cellGrid[x, y] == null) continue;
                                foreach (var deadCell in cellsToDestroy)
                                {
                                    if (cellGrid[x, y].Contains(deadCell))
                                    {
                                        cellGrid[x, y].Remove(deadCell);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            DebugConsole.NewMessage($"[网格网格刷新异常] {ex.Message}", Color.Red);
        }

        // 6. 重新计算渲染网格顶点（CLIENT）
#if CLIENT
        try
        {
            if (Level.Loaded?.Renderer != null)
            {
                var levelRenderer = Level.Loaded.Renderer;

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

                var wallEdgeVerts = CaveGenerator.GenerateWallEdgeVertices(
                    allLevelCells,
                    genParams.WallEdgeExpandOutwardsAmount,
                    genParams.WallEdgeExpandInwardsAmount,
                    outerColor: genParams.WallColor,
                    innerColor: genParams.WallColor,
                    Level.Loaded,
                    zCoord: 0.9f
                ).ToArray();

                var wallInnerVerts = CaveGenerator.GenerateWallVertices(
                    newTriangles,
                    Color.Black,
                    zCoord: 0.9f
                ).ToArray();

                var wallTexture = genParams.WallSprite.Texture;
                var edgeTexture = genParams.WallEdgeSprite.Texture;

                levelRenderer.SetVertices(wallVerts, wallEdgeVerts, wallInnerVerts, wallTexture, edgeTexture);
            }
        }
        catch (Exception ex)
        {
            DebugConsole.NewMessage($"[CLIENT 渲染网格重建异常] {ex.Message}");
        }

        // 7. 完全清洗所有潜艇内的声纳屏幕烘焙图元贴图，逼迫下一帧立刻按照新 Edges 扫描！
        try
        {
            foreach (Item item in Item.ItemList)
            {
                if (item == null) continue;

                var sonar = item.GetComponent<Barotrauma.Items.Components.Sonar>();
                if (sonar == null) continue;

                var blipsField = typeof(Barotrauma.Items.Components.Sonar).GetField("sonarBlips", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (blipsField != null)
                {
                    var blips = blipsField.GetValue(sonar) as System.Collections.IList;
                    blips?.Clear(); 
                }

                var unsentChangesField = typeof(Barotrauma.Items.Components.Sonar).GetField("unsentChanges", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (unsentChangesField != null)
                {
                    unsentChangesField.SetValue(sonar, true);
                }

                var viewTargetField = typeof(Barotrauma.Items.Components.Sonar).GetField("sonarViewRenderTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                   ?? typeof(Barotrauma.Items.Components.Sonar).GetField("screenRenderTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (viewTargetField != null)
                {
                    viewTargetField.SetValue(sonar, null);
                }
            }
        }
        catch (Exception ex)
        {
            DebugConsole.NewMessage($"[声纳洗白异常] {ex.Message}");
        }
#endif
    }
}
}