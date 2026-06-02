/*using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Barotrauma;
using Barotrauma.Items.Components;
using Barotrauma.Extensions;
using Barotrauma.Networking;
using HarmonyLib;
using Microsoft.Xna.Framework;
using FarseerPhysics;
using FarseerPhysics.Dynamics;

namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(Hull), "UpdateCheats")]
    public static class Patch_Hull_ExplosionDestroyer
    {
        [HarmonyPrefix]
        public static bool Prefix(float deltaTime, Camera cam)
        {
            if (PlayerInput.SecondaryDoubleClicked())
            {
                Vector2 mouseWorldPos = cam.ScreenToWorld(PlayerInput.MousePosition);
                Hull targetHull = Screen.Selected is { IsEditor: true } ? 
                    Hull.FindHullUnoptimized(mouseWorldPos) : 
                    Hull.FindHull(mouseWorldPos);

                if (targetHull != null && !targetHull.IdFreed && targetHull.Submarine != null)
                {
                    Submarine mainSub = targetHull.Submarine;
                    Rectangle targetRect = targetHull.Rect; 
                    Rectangle worldRect = targetHull.WorldRect;
                    Vector2 hullWorldCenter = new Vector2(worldRect.X + worldRect.Width / 2f, worldRect.Y - worldRect.Height / 2f);

                    try
                    {
                        // ===== 1. 精准收集受影响的实体 =====
                        List<Structure> structuresToMove = Structure.WallList
                            .Where(w => w.Submarine == mainSub && targetRect.Intersects(w.Rect)).ToList();

                        List<Item> itemsToMove = Item.ItemList
                            .Where(i => i.CurrentHull == targetHull || (i.Submarine == mainSub && targetRect.Contains(i.Position))).ToList();

                        List<Gap> gapsToRemove = Gap.GapList
                            .Where(g => g.Submarine == mainSub && g.linkedTo != null && g.linkedTo.Contains(targetHull)).ToList();

                        // ===== 2. 🔥【核心修复：拔除套娃】创建一个绝对不加载任何文件的纯空 Info =====
                        SubmarineInfo chunkInfo = new SubmarineInfo(""); // 传空字符串，阻止游戏去读主艇文件克隆全艇！
                        chunkInfo.Name = "DebrisChunk";
                        //chunkInfo.IsOutpost = false;

                        // 强改子艇类型为 Wreck (残骸)
                        PropertyInfo typeProp = typeof(SubmarineInfo).GetProperty("SubmarineType", BindingFlags.Public | BindingFlags.Instance) 
                                                ?? typeof(SubmarineInfo).GetProperty("Type", BindingFlags.Public | BindingFlags.Instance);
                        if (typeProp != null && typeProp.CanWrite) { typeProp.SetValue(chunkInfo, SubmarineType.Wreck); }

                        // 此时实例化的 debrisSub 内部是完全绝对纯净、没有任何自动生成墙体的
                        Submarine debrisSub = new Submarine(chunkInfo);

                        // ===== 3. 空间大平移（迁移真实的物件） =====
                        // A. 转移 Hull
                        MigrateEntity(targetHull, mainSub, debrisSub);
                        targetHull.Rect = new Rectangle(-targetRect.Width / 2, targetRect.Height / 2, targetRect.Width, targetRect.Height);

                        // B. 转移墙体
                        foreach (Structure wall in structuresToMove)
                        {
                            Vector2 relativePos = wall.Position - new Vector2(targetRect.X + targetRect.Width / 2f, targetRect.Y - targetRect.Height / 2f);
                            MigrateEntity(wall, mainSub, debrisSub);
                            wall.Rect = new Rectangle((int)relativePos.X, (int)relativePos.Y, wall.Rect.Width, wall.Rect.Height);
                            
                            MethodInfo moveMethod = typeof(Structure).GetMethod("OnMoved", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                            moveMethod?.Invoke(wall, null);
                        }

                        // C. 转移物品
                        foreach (Item item in itemsToMove)
                        {
                            Vector2 relativePos = item.Position - new Vector2(targetRect.X + targetRect.Width / 2f, targetRect.Y - targetRect.Height / 2f);
                            MigrateEntity(item, mainSub, debrisSub);
                            item.CurrentHull = targetHull;

                            if (item.body != null)
                            {
                                item.body.Submarine = debrisSub;
                                item.body.BodyType = BodyType.Dynamic;
                                Vector2 itemWorldPos = hullWorldCenter + relativePos;
                                item.body.SetTransform(ConvertUnits.ToSimUnits(itemWorldPos), 0f);
                            }
                            else
                            {
                                item.Rect = new Rectangle((int)relativePos.X, (int)relativePos.Y, item.Rect.Width, item.Rect.Height);
                            }
                        }

                        // D. 清理旧 Gap 接口
                        foreach (Gap gap in gapsToRemove)
                        {
                            RemoveFromSubmarineLists(mainSub, gap);
                            gap.Remove();
                        }

                        // ===== 4. 🔥【核心修复：缝合缺口 Gap，消灭空气墙】=====
                        // 正如你所说，没有 Gap 的边缘会变成死结界。我们要在老艇被挖掉的边缘，手动制造向公海敞开的合法 Gap！
                        // 这样老艇边缘剩下的 Hull 就能正确判断“前方是海水”，从而允许人自由游进游出，不会被卡进地板。
                        CreateOpenOceanGapsForWreck(mainSub, targetRect);

                        // ===== 5. 强制新碎艇物理体重构 =====
                        if (!Submarine.Loaded.Contains(debrisSub))
                        {
                            Submarine.Loaded.Add(debrisSub);
                        }

                        if (debrisSub.PhysicsBody == null)
                        {
                            MethodInfo createPhysicsBodyMethod = typeof(Submarine).GetMethod("CreatePhysicsBody", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                            if (createPhysicsBodyMethod != null)
                            {
                                createPhysicsBodyMethod.Invoke(debrisSub, new object[createPhysicsBodyMethod.GetParameters().Length]);
                            }
                        }

                        if (debrisSub.PhysicsBody != null)
                        {
                            debrisSub.PhysicsBody.FarseerBody.BodyType = BodyType.Dynamic;
                            debrisSub.PhysicsBody.SetTransform(ConvertUnits.ToSimUnits(hullWorldCenter), 0f);
                            debrisSub.PhysicsBody.LinearVelocity = mainSub.Velocity + (mainSub.WorldPosition - hullWorldCenter) * 0.08f;
                        }

                        // ===== 6. 物理网格树双向完全重构 =====
                        mainSub.PhysicsBody?.ResetDynamics();

                        MethodInfo refreshSubEntities = typeof(Submarine).GetMethod("RefreshSubEntities", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        refreshSubEntities?.Invoke(mainSub, null);
                        refreshSubEntities?.Invoke(debrisSub, null);

                        MethodInfo calculateDimensions = typeof(Submarine).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                            .FirstOrDefault(m => m.Name == "CalculateDimensions");
                        if (calculateDimensions != null)
                        {
                            object[] dimsParams = new object[calculateDimensions.GetParameters().Length];
                            if (dimsParams.Length == 1 && calculateDimensions.GetParameters()[0].ParameterType == typeof(bool)) { dimsParams[0] = true; }
                            calculateDimensions.Invoke(mainSub, dimsParams);
                            calculateDimensions.Invoke(debrisSub, dimsParams);
                        }

                        // 爆炸冲击力
                        if (mainSub.PhysicsBody != null)
                        {
                            Vector2 pushForce = (mainSub.WorldPosition - hullWorldCenter);
                            pushForce.Normalize();
                            mainSub.PhysicsBody.ApplyLinearImpulse(ConvertUnits.ToSimUnits(pushForce * 220f));
                        }

                        DebugConsole.NewMessage($"[分离成功] 成功斩断全艇克隆！边缘缝合完成，缺口自由通行！", Color.Lime);
                    }
                    catch (Exception ex)
                    {
                        DebugConsole.ThrowError($"[分离失败] 逻辑异常: {ex.Message}\n{ex.StackTrace}");
                    }

                    return false; 
                }
            }
            return true;
        }

        /// <summary>
        /// 🔥 在主艇的挖掘缺口边缘动态生成连通深海的 Gap，彻底消除边缘卡死和角色无法进入的问题
        /// </summary>
        private static void CreateOpenOceanGapsForWreck(Submarine mainSub, Rectangle holeRect)
        {
            try
            {
                // 分别在被挖掉的长方形房间的 四个边缘 寻找老艇残存的、和这个洞挨着的 Hull
                // 如果挨着，就必须在这条边上缝合一个全新的开放 Gap (连通到空，即公海)
                foreach (Hull remainingHull in Hull.HullList.Where(h => h.Submarine == mainSub))
                {
                    Rectangle r = remainingHull.Rect;

                    // 检查左右上下是否有邻接面
                    bool adjacentLeft = Math.Abs(r.Right - holeRect.Left) < 16 && r.Y < holeRect.Y + holeRect.Height && r.Y - r.Height > holeRect.Y - holeRect.Height;
                    bool adjacentRight = Math.Abs(r.Left - holeRect.Right) < 16 && r.Y < holeRect.Y + holeRect.Height && r.Y - r.Height > holeRect.Y - holeRect.Height;
                    bool adjacentTop = Math.Abs((r.Y - r.Height) - holeRect.Y) < 16 && r.X < holeRect.Right && r.X + r.Width > holeRect.Left;
                    bool adjacentBottom = Math.Abs(r.Y - (holeRect.Y - holeRect.Height)) < 16 && r.X < holeRect.Right && r.X + r.Width > holeRect.Left;

                    if (adjacentLeft || adjacentRight || adjacentTop || adjacentBottom)
                    {
                        // 构造一个完美贴合边界的连通空海 Gap
                        Rectangle gapRect = Rectangle.Empty;
                        if (adjacentLeft)  gapRect = new Rectangle(holeRect.Left - 8, holeRect.Y, 16, holeRect.Height);
                        if (adjacentRight) gapRect = new Rectangle(holeRect.Right - 8, holeRect.Y, 16, holeRect.Height);
                        if (adjacentTop)   gapRect = new Rectangle(holeRect.Left, holeRect.Y + 8, holeRect.Width, 16);
                        if (adjacentBottom) gapRect = new Rectangle(holeRect.Left, holeRect.Y - holeRect.Height + 8, holeRect.Width, 16);

                        if (gapRect != Rectangle.Empty)
                        {
                            Gap newOceanGap = new Gap(gapRect, isHorizontal: (adjacentLeft || adjacentRight), mainSub);
                            // 关键设置：让 Gap 的一侧连着老艇剩余的房间，另一侧为 null（游戏底层判定为直接连接大洋公海）
                            newOceanGap.linkedTo[0] = remainingHull;
                            newOceanGap.linkedTo[1] = null; 
                            
                            // 开启通行判定
                            //newOceanGap.PassThroughType = Gap.GapPassThroughType.Passed;
                            newOceanGap.Open = 1.0f;

                            // 塞进老艇的子实体更新列表中
                            FieldInfo subEntitiesField = typeof(Submarine).GetField("subEntities", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                            if (subEntitiesField?.GetValue(mainSub) is System.Collections.IList list && !list.Contains(newOceanGap))
                            {
                                list.Add(newOceanGap);
                            }
         

        #region 🛠️ 反射平移辅助
        private static void MigrateEntity(MapEntity entity, Submarine source, Submarine target)
        {
            if (entity == null) return;
            RemoveFromSubmarineLists(source, entity);

            PropertyInfo subProp = typeof(MapEntity).GetProperty("Submarine", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (subProp != null && subProp.CanWrite) { subProp.SetValue(entity, target); }
            else
            {
                FieldInfo subField = typeof(MapEntity).GetField("submarine", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                                     ?? typeof(MapEntity).GetField("_submarine", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                subField?.SetValue(entity, target);
            }

            string listName = entity is Hull ? "hulls" : (entity is Structure ? "structures" : (entity is Item ? "items" : "subEntities"));
            FieldInfo targetField = typeof(Submarine).GetField(listName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                                    ?? typeof(Submarine).GetField("subEntities", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (targetField != null && targetField.GetValue(target) is System.Collections.IList targetList)
            {
                if (!targetList.Contains(entity)) targetList.Add(entity);
            }
        }

        private static void RemoveFromSubmarineLists(Submarine sub, MapEntity entity)
        {
            if (sub == null || entity == null) return;
            string[] lists = { "subEntities", "hulls", "structures", "items" };
            foreach (string name in lists)
            {
                FieldInfo field = typeof(Submarine).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field != null && field.GetValue(sub) is System.Collections.IList list)
                {
                    if (list.Contains(entity)) list.Remove(entity);
                }
            }
        }
        #endregion
    }
}*/