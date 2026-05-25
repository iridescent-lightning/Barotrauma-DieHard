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
    
    // 【核心修复：完美挂载到官方的 RangedStructureDamage 静态方法上】
    [HarmonyPatch(typeof(Explosion))]
    public class ExplosionDamageWallPatch
    {

        // 允许摧毁泰森多边形地形的特定炸药标签
        public const string TerrianDestroyerTag = "VoronoiDestroyer";

    [HarmonyPatch(typeof(Explosion), "Explode")]
    [HarmonyPostfix]
    public static void Postfix(Explosion __instance, Vector2 worldPosition, Entity damageSource, Character attacker)
    {
        // 【关键防御】：如果是联机模式下的客户端，绝对不能在 Postfix 里直接计算，必须等服务器发包！
#if CLIENT
        if (GameMain.Client != null) return;
#endif

        if (Level.Loaded == null) return;

        Item realExplodeItem = null;
        if (damageSource is Item sourceItem && sourceItem?.Prefab.Identifier != "c4block") return;

            
        float worldRange = 1600f;
        // 1. 转为仿真单位并射线探测
        Vector2 simStartPos = ConvertUnits.ToSimUnits(worldPosition);
        float simCheckRadius = ConvertUnits.ToSimUnits(worldRange);

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
        float checkRangeDisplay = worldRange;
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
            //DebugConsole.NewMessage($"[SERVER] 成功向所有客户端同步了 {cellsToDestroy.Count} 个细胞的销毁指令", Color.Green);
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
            //DebugConsole.NewMessage($"[声纳拓扑重组] 成功为 {survivingNeighbors.Count} 个断裂面邻居方块赋予了崭新的声纳高亮反射边缘！", Color.Cyan);
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