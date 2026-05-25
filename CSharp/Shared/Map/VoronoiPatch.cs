using System;
using System.Collections.Generic;
using System.Linq;
using Barotrauma;
using HarmonyLib;
using Microsoft.Xna.Framework;
using FarseerPhysics.Dynamics;
using Voronoi2;
using Barotrauma.Networking;
using FarseerPhysics;
using Networking;


/*-----------------------parameters-----------------------------*/
// int piecesCount = rand.Next(2, 4); 控制分裂体数量
// float shrinkFactor = (float)(rand.NextDouble() * 0.08 + 0.88); // 分裂后新的分裂体缩放程度
// subDistToExplosion 完全湮灭区与衰减起点（爆炸的威力核心）
// destroyChance = MathHelper.Clamp(destroyChance, 0.1f, 0.92f); 4. 碎片留存率硬上限（控制地形的“脆度”）这行代码卡死了碎片的最终死亡概率，直接决定了边缘的残渣丰富度：


namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(Explosion))]
    public class ExplosionDamageWallPatch
    {
        public const string TerrianDestroyerTag = "VoronoiDestroyer";

        [HarmonyPatch(typeof(Explosion), "Explode")]
        [HarmonyPostfix]
        public static void Postfix(Explosion __instance, Vector2 worldPosition, Entity damageSource, Character attacker)
        {
#if CLIENT
            if (GameMain.Client != null) return; // 客户端不自发运行主逻辑，全部听从服务器指令
#endif
            if (Level.Loaded == null) return;

            float worldRange = 1000f; 
            Vector2 simStartPos = ConvertUnits.ToSimUnits(worldPosition);
            float simCheckRadius = ConvertUnits.ToSimUnits(worldRange * 1.5f); 

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

            // 服务器本地执行核心拆分与剔除
            var cellsToDestroy = ServerOrLocalTriggerSubdivision(worldPosition, worldRange);
            if (cellsToDestroy == null || cellsToDestroy.Count == 0) return;

            ExecuteTerrainDestruction(cellsToDestroy, worldPosition);

#if SERVER
            if (GameMain.Server != null)
            {
                // 【核心联机修复 1】：服务器不再发送零散的、客户端找不到的碎片中心！
                // 直接发送权威的【爆炸中心点】和【爆炸物理半径】，让客户端本地去切分大细胞
                IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.DESTROY_MAIN_WALL);
                msg.WriteSingle(worldPosition.X);
                msg.WriteSingle(worldPosition.Y);
                msg.WriteSingle(worldRange); 
                NetUtil.SendAll(msg, DeliveryMethod.Reliable);
            }
#endif
        }

        /// <summary>
        /// 封装通用的受影响细胞查找与切分逻辑（SERVER 和 CLIENT 收到网络包后均调用此方法）
        /// </summary>
        private static List<Voronoi2.VoronoiCell> ServerOrLocalTriggerSubdivision(Vector2 worldPosition, float worldRange)
        {
            var cellsField = typeof(Level).GetField("cells", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var allLevelCells = cellsField?.GetValue(Level.Loaded) as List<Voronoi2.VoronoiCell>;
            if (allLevelCells == null || allLevelCells.Count == 0) return null;

            List<Voronoi2.VoronoiCell> affectedCells = new List<Voronoi2.VoronoiCell>();

            foreach (var cell in allLevelCells)
            {
                if (cell == null || cell.CellType == Voronoi2.CellType.Removed) continue;

                bool isVertexHit = false;
                foreach (var edge in cell.Edges)
                {
                    Vector2 p1World = edge.Point1 + cell.Translation;
                    Vector2 p2World = edge.Point2 + cell.Translation;

                    if (Vector2.Distance(p1World, worldPosition) <= worldRange || 
                        Vector2.Distance(p2World, worldPosition) <= worldRange)
                    {
                        isVertexHit = true;
                        break;
                    }
                }

                Vector2 cellWorldPos = cell.Center + cell.Translation;
                if (!isVertexHit && Vector2.Distance(cellWorldPos, worldPosition) <= worldRange)
                {
                    isVertexHit = true;
                }

                if (isVertexHit)
                {
                    affectedCells.Add(cell);
                }
            }

            if (affectedCells.Count == 0) return null;

            return ProcessCellSubdivision(affectedCells, worldPosition, worldRange);
        }

        private static List<Voronoi2.VoronoiCell> ProcessCellSubdivision(List<Voronoi2.VoronoiCell> affectedCells, Vector2 expPos, float baseRadius)
        {
            var cellsField = typeof(Level).GetField("cells", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var allLevelCells = cellsField?.GetValue(Level.Loaded) as List<Voronoi2.VoronoiCell>;
            
            List<Voronoi2.VoronoiCell> finalDestroyList = new List<Voronoi2.VoronoiCell>();
            List<Voronoi2.VoronoiCell> cellsToProcess = new List<Voronoi2.VoronoiCell>(affectedCells);

            // 【核心联机修复 2】：固定伪随机种子！
            // 严禁使用 new Random()，否则会导致服务器和各个客户端切出来的碎片数量、形状不同步。
            // 使用爆炸位置的坐标哈希作为种子，确保全房间所有人在切同一面墙时几何形状100%镜像一致。
            int seed = (int)(expPos.X * 1000 + expPos.Y);
            Random rand = new Random(seed);

            bool useGap = false; 

            List<Tuple<Voronoi2.VoronoiCell, float>> candidateSubCells = new List<Tuple<Voronoi2.VoronoiCell, float>>();

            foreach (var cell in cellsToProcess)
            {
                if (cell == null) continue;

                Vector2 cellWorldPos = cell.Center + cell.Translation;
                float distToCenter = Vector2.Distance(cellWorldPos, expPos);

                finalDestroyList.Add(cell);

                List<Vector2> sortedVertices = new List<Vector2>();
                float maxCellRadius = 0f;

                foreach (var edge in cell.Edges)
                {
                    if (edge == null) continue;
                    if (!sortedVertices.Contains(edge.Point1))
                    {
                        sortedVertices.Add(edge.Point1);
                        maxCellRadius = Math.Max(maxCellRadius, Vector2.Distance(edge.Point1, cell.Center));
                    }
                    if (!sortedVertices.Contains(edge.Point2))
                    {
                        sortedVertices.Add(edge.Point2);
                        maxCellRadius = Math.Max(maxCellRadius, Vector2.Distance(edge.Point2, cell.Center));
                    }
                }
                
                if (sortedVertices.Count < 3) continue;
                
                Vector2 center = cell.Center;
                sortedVertices = sortedVertices.OrderBy(v => (float)Math.Atan2(v.Y - center.Y, v.X - center.X)).ToList();

                int piecesCount = rand.Next(4, 6); 
                int vertsPerPiece = sortedVertices.Count / piecesCount;
                if (vertsPerPiece < 1) vertsPerPiece = 1;

                float cellInvasionRatio = 1.0f - (distToCenter / (baseRadius + maxCellRadius));
                cellInvasionRatio = MathHelper.Clamp(cellInvasionRatio, 0f, 1f);

                for (int i = 0; i < piecesCount; i++)
                {
                    int startIdx = i * vertsPerPiece;
                    int endIdx = (i == piecesCount - 1) ? sortedVertices.Count : (i + 1) * vertsPerPiece;

                    if (startIdx >= sortedVertices.Count) break;
                    if (endIdx > sortedVertices.Count) endIdx = sortedVertices.Count;
                    if (startIdx >= endIdx) continue;

                    List<Vector2> pieceVertices = new List<Vector2> { center };
                    for (int j = startIdx; j < endIdx; j++)
                    {
                        pieceVertices.Add(sortedVertices[j]);
                    }
                    pieceVertices.Add(sortedVertices[endIdx % sortedVertices.Count]);

                    Vector2 pieceCenter = Vector2.Zero;
                    foreach (var v in pieceVertices) pieceCenter += v;
                    pieceCenter /= pieceVertices.Count;

                    List<Vector2> processedVerts = new List<Vector2>();

                    if (useGap)
                    {
                        float shrinkFactor = (float)(rand.NextDouble() * 0.08 + 0.88); 
                        foreach (var v in pieceVertices)
                        {
                            processedVerts.Add(pieceCenter + (v - pieceCenter) * shrinkFactor);
                        }
                    }
                    else
                    {
                        float microShrink = 0.995f; 
                        foreach (var v in pieceVertices)
                        {
                            processedVerts.Add(pieceCenter + (v - pieceCenter) * microShrink);
                        }
                    }

                    List<Vector2> uniqueVerts = new List<Vector2>();
                    foreach (var v in processedVerts)
                    {
                        if (!uniqueVerts.Any(uv => Vector2.DistanceSquared(uv, v) < 0.09f))
                        {
                            uniqueVerts.Add(v);
                        }
                    }

                    if (uniqueVerts.Count < 3) continue;

                    float minX = uniqueVerts.Min(v => v.X);
                    float maxX = uniqueVerts.Max(v => v.X);
                    float minY = uniqueVerts.Min(v => v.Y);
                    float maxY = uniqueVerts.Max(v => v.Y);
                    if ((maxX - minX) < 3.0f || (maxY - minY) < 3.0f) continue; 

                    try
                    {
                        Vector2[] vertexArray = uniqueVerts.ToArray();
                        Voronoi2.VoronoiCell subCell = new Voronoi2.VoronoiCell(vertexArray)
                        {
                            CellType = cell.CellType,
                            Translation = cell.Translation,
                            OnDestroyed = cell.OnDestroyed
                        };

                        subCell.Edges.Clear();
                        for (int v = 0; v < vertexArray.Length; v++)
                        {
                            Vector2 p1 = vertexArray[v];
                            Vector2 p2 = vertexArray[(v + 1) % vertexArray.Length];
                            var newEdge = new GraphEdge(p1, p2)
                            {
                                Cell1 = subCell,
                                IsSolid = true
                    };
                            subCell.Edges.Add(newEdge);
                        }

                        Vector2 subWorldPos = subCell.Center + subCell.Translation;
                        float subDistToExplosion = Vector2.Distance(subWorldPos, expPos);

                        float destroyChance;
                        if (subDistToExplosion <= baseRadius * 0.4f)
                        {
                            destroyChance = 1.0f;
                        }
                        else
                        {
                            float distanceFactor = 1.0f - ((subDistToExplosion - (baseRadius * 0.4f)) / (baseRadius * 0.8f));
                            destroyChance = (cellInvasionRatio * 0.4f) + (distanceFactor * 0.6f);
                        }
                        destroyChance = MathHelper.Clamp(destroyChance, 0.0f, 1.0f);

                        float randomizedDistance = subDistToExplosion * (float)(1.0 + (rand.NextDouble() - 0.5) * 0.15 * (1.0 - destroyChance));

                        candidateSubCells.Add(new Tuple<Voronoi2.VoronoiCell, float>(subCell, randomizedDistance));
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }

            if (candidateSubCells.Count == 0) return finalDestroyList;

            var sortedCandidates = candidateSubCells.OrderBy(c => c.Item2).ToList();

            double totalDestroyRatio = rand.NextDouble() * 0.3 + 0.55; 
            int destroyCountThreshold = (int)(sortedCandidates.Count * totalDestroyRatio);

            for (int i = 0; i < sortedCandidates.Count; i++)
            {
                Voronoi2.VoronoiCell subCell = sortedCandidates[i].Item1;
                if (subCell == null) continue;

                if (i < destroyCountThreshold)
                {
                    subCell.CellType = Voronoi2.CellType.Removed;
                    finalDestroyList.Add(subCell);
                }
                else
                {
                    allLevelCells.Add(subCell);
                }
            }

            return finalDestroyList;
        }

        public static void OnReceiveDestroyWallMessage(object[] args)
        {
            if (args == null || args.Length == 0) return;
            if (args[0] is not IReadMessage msg) return;

            // 【核心联机修复 3】：客户端解析服务器发送的权威爆炸源头数据
            float expX = msg.ReadSingle();
            float expY = msg.ReadSingle();
            Vector2 worldPosition = new Vector2(expX, expY);
            float worldRange = msg.ReadSingle(); // 读取半径

            if (Level.Loaded == null) return;

#if CLIENT
            if (GameMain.Client != null)
            {
                // 客户端在本地完全镜像模拟一次细胞重组与毁灭逻辑
                List<Voronoi2.VoronoiCell> cellsToDestroy = ServerOrLocalTriggerSubdivision(worldPosition, worldRange);
                
                if (cellsToDestroy != null && cellsToDestroy.Count > 0)
                {
                    ExecuteTerrainDestruction(cellsToDestroy, worldPosition);
                }
            }
#endif
        }

        public static void ExecuteTerrainDestruction(List<Voronoi2.VoronoiCell> cellsToDestroy, Vector2 worldPosition)
        {
            if (cellsToDestroy == null || cellsToDestroy.Count == 0) return;

            var cellsField = typeof(Level).GetField("cells", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var allLevelCells = cellsField?.GetValue(Level.Loaded) as List<Voronoi2.VoronoiCell>;
            if (allLevelCells == null) return;

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

            FarseerPhysics.Dynamics.Body rockBody = null;
            foreach (var cell in cellsToDestroy)
            {
                if (cell.Body != null) { rockBody = cell.Body; break; }
            }

            foreach (var deadCell in cellsToDestroy)
            {
                allLevelCells.Remove(deadCell);
                deadCell.CellType = Voronoi2.CellType.Removed;
                deadCell.OnDestroyed?.Invoke();
                deadCell.OnDestroyed = null;
            }

            try
            {
                foreach (var neighborCell in survivingNeighbors)
                {
                    foreach (var edge in neighborCell.Edges)
                    {
                        if (cellsToDestroy.Contains(edge.Cell1) || cellsToDestroy.Contains(edge.Cell2) || 
                            edge.Cell1?.CellType == Voronoi2.CellType.Removed || edge.Cell2?.CellType == Voronoi2.CellType.Removed)
                        {
                            if (edge.Cell1 != null && (cellsToDestroy.Contains(edge.Cell1) || edge.Cell1.CellType == Voronoi2.CellType.Removed)) edge.Cell1 = null;
                            if (edge.Cell2 != null && (cellsToDestroy.Contains(edge.Cell2) || edge.Cell2.CellType == Voronoi2.CellType.Removed)) edge.Cell2 = null;
                            edge.IsSolid = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugConsole.NewMessage($"[声纳拓扑修复异常] {ex.Message}", Color.Red);
            }

            GameMain.World.ProcessChanges();

#if CLIENT
            SoundPlayer.PlaySound("rockbreak", worldPosition);
            foreach (var deadCell in cellsToDestroy)
            {
                Vector2 cellCenterWorld = ConvertUnits.ToDisplayUnits(deadCell.Center) + deadCell.Translation;
                for (int i = 0; i < 2; i++) 
                {
                    GameMain.ParticleManager.CreateParticle("heavygib", cellCenterWorld + Rand.Vector(15.0f), velocity: Rand.Vector(120.0f));
                }
            }
#endif

            if (rockBody != null)
            {
                GameMain.World.Remove(rockBody);
            }

            List<Vector2[]> newTriangles;
            var newRockBody = CaveGenerator.GeneratePolygons(allLevelCells, Level.Loaded, out newTriangles);
            newRockBody.BodyType = FarseerPhysics.BodyType.Static;
            newRockBody.CollisionCategories = Physics.CollisionLevel;

            try
            {
                var cellGridField = typeof(Level).GetField("cellGrid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var cellGrid = cellGridField?.GetValue(Level.Loaded) as List<Voronoi2.VoronoiCell>[,];
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
                                cellGrid[x, y].Remove(deadCell);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { DebugConsole.NewMessage($"[网格刷新异常] {ex.Message}", Color.Red); }

#if CLIENT
            try
            {
                if (Level.Loaded?.Renderer != null)
                {
                    var levelRenderer = Level.Loaded.Renderer;
                    var vertexBuffersField = typeof(LevelRenderer).GetField("vertexBuffers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var vbs = vertexBuffersField?.GetValue(levelRenderer) as System.Collections.IList;
                    if (vbs != null)
                    {
                        foreach (var vb in vbs) { (vb as IDisposable)?.Dispose(); }
                        vbs.Clear();
                    }

                    var genParams = Level.Loaded.GenerationParams;
                    var wallVerts = CaveGenerator.GenerateWallEdgeVertices(allLevelCells, 0.0f, genParams.WallTextureExpandInwardsAmount, genParams.WallColor, Color.Black, Level.Loaded, 0.9f, true).ToArray();
                    CaveGenerator.GenerateTextureCoordinates(wallVerts, genParams.WallTextureSize);
                    var wallEdgeVerts = CaveGenerator.GenerateWallEdgeVertices(allLevelCells, genParams.WallEdgeExpandOutwardsAmount, genParams.WallEdgeExpandInwardsAmount, genParams.WallColor, genParams.WallColor, Level.Loaded, 0.9f).ToArray();
                    var wallInnerVerts = CaveGenerator.GenerateWallVertices(newTriangles, Color.Black, zCoord: 0.9f).ToArray();

                    levelRenderer.SetVertices(wallVerts, wallEdgeVerts, wallInnerVerts, genParams.WallSprite.Texture, genParams.WallEdgeSprite.Texture);
                }
            }
            catch (Exception ex) { DebugConsole.NewMessage($"[CLIENT 渲染网格重建异常] {ex.Message}"); }

            try
            {
                foreach (Item item in Item.ItemList)
                {
                    if (item == null) continue;
                    var sonar = item.GetComponent<Barotrauma.Items.Components.Sonar>();
                    if (sonar == null) continue;

                    var blipsField = typeof(Barotrauma.Items.Components.Sonar).GetField("sonarBlips", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    (blipsField?.GetValue(sonar) as System.Collections.IList)?.Clear();

                    typeof(Barotrauma.Items.Components.Sonar).GetField("unsentChanges", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(sonar, true);
                    typeof(Barotrauma.Items.Components.Sonar).GetField("sonarViewRenderTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(sonar, null);
                }
            }
            catch (Exception ex) { DebugConsole.NewMessage($"[声纳洗白异常] {ex.Message}"); }
#endif
        }
    }
}