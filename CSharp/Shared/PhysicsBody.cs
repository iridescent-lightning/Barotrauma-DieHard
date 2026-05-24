﻿using System;
using System.Collections.Generic;
using System.Xml.Linq;
using HarmonyLib;
using Barotrauma;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using Microsoft.Xna.Framework; 

using Barotrauma.Extensions;

public static class CustomShapeManager
{
    public const Barotrauma.PhysicsBody.Shape Triangle = (Barotrauma.PhysicsBody.Shape)100;
    public const Barotrauma.PhysicsBody.Shape Hexagon = (Barotrauma.PhysicsBody.Shape)101; 
    public const Barotrauma.PhysicsBody.Shape Table = (Barotrauma.PhysicsBody.Shape)102; 

    public static FarseerPhysics.Common.Vertices CreateCustomVertices(Barotrauma.PhysicsBody.Shape shape, float width, float height, float radius)
    {
        var vertices = new FarseerPhysics.Common.Vertices();
        if (shape == Triangle)
        {
            float centroidY = height / 3f;
            vertices.Add(new Vector2(0f, height - centroidY));           
            vertices.Add(new Vector2(-width / 2f, 0f - centroidY));     
            vertices.Add(new Vector2(width / 2f, 0f - centroidY));      
        }
        return vertices;
    }

    public static List<FarseerPhysics.Common.Vertices> CreateTableParts(
        float boardWidth, float boardHeight, 
        float leftLegWidth, float leftLegHeight, float leftLegOffset,
        float rightLegWidth, float rightLegHeight, float rightLegOffset)
    {
        var parts = new List<FarseerPhysics.Common.Vertices>();

        float maxLegHeight = Math.Max(leftLegHeight, rightLegHeight);
        float totalHeight = boardHeight + maxLegHeight;

        float topTopY = totalHeight / 2f;               
        float boardBottomY = topTopY - boardHeight;     
        
        float leftLegBottomY = boardBottomY - leftLegHeight;
        float rightLegBottomY = boardBottomY - rightLegHeight;

        float halfBoardWidth = boardWidth / 2f;

        // --- 部件 A: 主横板 ---
        var topVertices = new FarseerPhysics.Common.Vertices();
        topVertices.Add(ConvertPixel(new Vector2(-halfBoardWidth, boardBottomY)));
        topVertices.Add(ConvertPixel(new Vector2(halfBoardWidth, boardBottomY)));
        topVertices.Add(ConvertPixel(new Vector2(halfBoardWidth, topTopY)));
        topVertices.Add(ConvertPixel(new Vector2(-halfBoardWidth, topTopY)));
        parts.Add(topVertices);

        // --- 部件 B: 左腿 ---
        if (leftLegWidth > 0 && leftLegHeight > 0)
        {
            var leftLegVertices = new FarseerPhysics.Common.Vertices();
            float leftLegLeftX = -halfBoardWidth + leftLegOffset;
            float leftLegRightX = leftLegLeftX + leftLegWidth;

            leftLegVertices.Add(ConvertPixel(new Vector2(leftLegLeftX, leftLegBottomY)));
            leftLegVertices.Add(ConvertPixel(new Vector2(leftLegRightX, leftLegBottomY)));
            leftLegVertices.Add(ConvertPixel(new Vector2(leftLegRightX, boardBottomY)));
            leftLegVertices.Add(ConvertPixel(new Vector2(leftLegLeftX, boardBottomY)));
            parts.Add(leftLegVertices);
        }

        // --- 部件 C: 右腿 ---
        if (rightLegWidth > 0 && rightLegHeight > 0)
        {
            var rightLegVertices = new FarseerPhysics.Common.Vertices();
            float rightLegRightX = halfBoardWidth - rightLegOffset;
            float rightLegLeftX = rightLegRightX - rightLegWidth;

            rightLegVertices.Add(ConvertPixel(new Vector2(rightLegLeftX, rightLegBottomY)));
            rightLegVertices.Add(ConvertPixel(new Vector2(rightLegRightX, rightLegBottomY)));
            rightLegVertices.Add(ConvertPixel(new Vector2(rightLegRightX, boardBottomY)));
            rightLegVertices.Add(ConvertPixel(new Vector2(rightLegLeftX, boardBottomY)));
            parts.Add(rightLegVertices);
        }

        return parts;

        Vector2 ConvertPixel(Vector2 pixelVec) => ConvertUnits.ToSimUnits(pixelVec);
    }

    // 新增：专门用来持久化存储每个独立物理实体的初始XML数据，防止实例污染
    public class TableData
    {
        public float LeftWidth;
        public float LeftHeight;
        public float LeftOffset;
        public float RightWidth;
        public float RightHeight;
        public float RightOffset;
        public float OriginalBoardHeightSim; // 备份纯桌板的物理米单位高度，避免逆向减法误差
    }

    [HarmonyPatch(typeof(PhysicsBody))]
    public static partial class PhysicsBodyPatch
    {
        [ThreadStatic] private static PhysicsBody.Shape? t_PendingCustomShape;
        
        // 临时缓存，仅用于从前置构造函数带入到 CreateBody
        [ThreadStatic] private static TableData t_TempTableData;

        // 核心改动：用普通字典死死绑定实例，绝不在 Update 里读线程静态变量
        private static readonly Dictionary<PhysicsBody, TableData> TableInstanceStorage = new Dictionary<PhysicsBody, TableData>();
        private static readonly Dictionary<PhysicsBody, float> LastDirections = new Dictionary<PhysicsBody, float>();

        [HarmonyPatch(typeof(PhysicsBody), MethodType.Constructor, new Type[] { typeof(XElement), typeof(Vector2), typeof(float), typeof(float?), typeof(Category), typeof(Category), typeof(bool) })]
        [HarmonyPrefix]
        public static void ConstructorPrefix(XElement element)
        {
            if (element == null) return;
            
            XElement targetElement = element;
            if (element.Name.LocalName.ToLowerInvariant() != "body" && element.Name.LocalName.ToLowerInvariant() != "physicsbody")
            {
                var subBody = element.Element("Body") ?? element.Element("PhysicsBody");
                if (subBody != null) targetElement = subBody;
            }
            
            string customShapeStr = targetElement.GetAttributeString("customshape", "").ToLowerInvariant();
            
            if (customShapeStr == "triangle")
            {
                t_PendingCustomShape = CustomShapeManager.Triangle;
            }
            else if (customShapeStr == "hexagon")
            {
                t_PendingCustomShape = CustomShapeManager.Hexagon;
            }
            else if (customShapeStr == "table")
            {
                t_PendingCustomShape = CustomShapeManager.Table;
                
                float defaultWidth = targetElement.GetAttributeFloat("legwidth", 12f);
                float defaultHeight = targetElement.GetAttributeFloat("legheight", 40f);
                float defaultOffset = targetElement.GetAttributeFloat("legoffset", 15f);

                t_TempTableData = new TableData
                {
                    LeftWidth = targetElement.GetAttributeFloat("leftlegwidth", defaultWidth),
                    LeftHeight = targetElement.GetAttributeFloat("leftlegheight", defaultHeight),
                    LeftOffset = targetElement.GetAttributeFloat("leftlegoffset", defaultOffset),
                    RightWidth = targetElement.GetAttributeFloat("rightlegwidth", defaultWidth),
                    RightHeight = targetElement.GetAttributeFloat("rightlegheight", defaultHeight),
                    RightOffset = targetElement.GetAttributeFloat("rightlegoffset", defaultOffset)
                };
            }
        }

        [HarmonyPatch(nameof(PhysicsBody.IsValidShape))]
        [HarmonyPrefix]
        public static bool IsValidShapePrefix(float radius, float height, float width, ref bool __result)
        {
            if (t_PendingCustomShape.HasValue)
            {
                __result = width > 0 && height > 0;
                return false; 
            }
            return true; 
        }

        [HarmonyPatch(nameof(PhysicsBody.DefineBodyShape))]
        [HarmonyPrefix]
        public static bool DefineBodyShapePrefix(float radius, float width, float height, ref PhysicsBody.Shape __result)
        {
            if (t_PendingCustomShape.HasValue)
            {
                __result = t_PendingCustomShape.Value;
                return false; 
            }
            return true;
        }

        [HarmonyPatch("CreateBody")]
        [HarmonyPrefix]
        public static bool CreateBodyPrefix(PhysicsBody __instance, float width, float height, float radius, float density, BodyType bodyType, Category collisionCategory, Category collidesWith, bool findNewContacts)
        {
            if (!t_PendingCustomShape.HasValue) return true; 

            PhysicsBody.Shape currentShape = t_PendingCustomShape.Value;
            t_PendingCustomShape = null; 

            AccessTools.Field(typeof(PhysicsBody), "bodyShape").SetValue(__instance, currentShape);

            __instance.FarseerBody = GameMain.World.CreateBody();
            __instance.FarseerBody.BodyType = bodyType;
            __instance.FarseerBody.UserData = __instance; 
            __instance.CollisionCategories = collisionCategory;
            __instance.CollidesWith = collidesWith;

            if (currentShape == CustomShapeManager.Table)
            {
                TableData data = t_TempTableData ?? new TableData();
                t_TempTableData = null; // 用完立刻置空
                
                // 备份绝对精确的初始桌板米单位高度
                data.OriginalBoardHeightSim = height; 

                float boardWidthPixels = ConvertUnits.ToDisplayUnits(width);
                float boardHeightPixels = ConvertUnits.ToDisplayUnits(height);

                var parts = CustomShapeManager.CreateTableParts(
                    boardWidthPixels, boardHeightPixels, 
                    data.LeftWidth, data.LeftHeight, data.LeftOffset,
                    data.RightWidth, data.RightHeight, data.RightOffset
                );
                
                foreach (var vertices in parts)
                {
                    var shape = new FarseerPhysics.Collision.Shapes.PolygonShape(vertices, density);
                    var fixture = __instance.FarseerBody.CreateFixture(shape, collisionCategory, collidesWith);
                    fixture.UserData = __instance; 
                }

                float maxLegHeightPixels = Math.Max(data.LeftHeight, data.RightHeight);

                __instance.Width = width; 
                __instance.Height = height + ConvertUnits.ToSimUnits(maxLegHeightPixels);
                __instance.Radius = radius;

                // 核心持久化绑定：将当前实例的数据塞进全局持久化字典
                TableInstanceStorage[__instance] = data;
            }
            else
            {
                __instance.Width = width;
                __instance.Height = height;
                __instance.Radius = radius;

                var vertices = CustomShapeManager.CreateCustomVertices(currentShape, width, height, radius);
                var shape = new FarseerPhysics.Collision.Shapes.PolygonShape(vertices, density);
                var fixture = __instance.FarseerBody.CreateFixture(shape, collisionCategory, collidesWith);
                fixture.UserData = __instance;
            }

            return false; 
        }

        [HarmonyPatch(nameof(PhysicsBody.Update))]
        [HarmonyPostfix]
        public static void Postfix(PhysicsBody __instance)
        {
            if (__instance.BodyShape != CustomShapeManager.Table || __instance.FarseerBody == null) return;

            float currentDir = __instance.Dir; 

            if (!LastDirections.TryGetValue(__instance, out float lastDir))
            {
                lastDir = 1.0f; 
                LastDirections[__instance] = currentDir;
            }

            if (Math.Abs(currentDir - lastDir) < 0.01f) return;

            LastDirections[__instance] = currentDir;

            // 从全局字典取出属于当前桌子实例的专属数据
            if (!TableInstanceStorage.TryGetValue(__instance, out TableData data)) return;

            float boardWidthPixels = ConvertUnits.ToDisplayUnits(__instance.Width);
            float boardHeightPixels = ConvertUnits.ToDisplayUnits(data.OriginalBoardHeightSim);

            // 判定方向：镜像交换左右数据
            float leftWidth  = currentDir > 0 ? data.LeftWidth   : data.RightWidth;
            float leftHeight = currentDir > 0 ? data.LeftHeight  : data.RightHeight;
            float leftOffset = currentDir > 0 ? data.LeftOffset  : data.RightOffset;

            float rightWidth  = currentDir > 0 ? data.RightWidth   : data.LeftWidth;
            float rightHeight = currentDir > 0 ? data.RightHeight  : data.LeftHeight;
            float rightOffset = currentDir > 0 ? data.RightOffset  : data.LeftOffset;

            var parts = CustomShapeManager.CreateTableParts(
                boardWidthPixels, boardHeightPixels,
                leftWidth, leftHeight, leftOffset,
                rightWidth, rightHeight, rightOffset
            );

            var collisionCategory = __instance.CollisionCategories;
            var collidesWith = __instance.CollidesWith;
            float density = 1.0f;

            if (__instance.FarseerBody.FixtureList.Count > 0)
            {
                density = __instance.FarseerBody.FixtureList[0].Shape.Density;
            }

            // 🌟【联动修复准备】：在清空旧治具前，先找出寄生在这把枪里的弹匣物品
            Barotrauma.Item parentGunItem = __instance.FarseerBody.UserData as Barotrauma.Item;
            if (parentGunItem == null && __instance.FarseerBody.UserData is PhysicsBody pBody)
            {
                // 有时候 UserData 绑定的是 PhysicsBody，我们需要借此拿到真实的 Item 实例
                parentGunItem = pBody.FarseerBody.UserData as Barotrauma.Item;
            }

            Barotrauma.Item containedMagazine = null;
            if (parentGunItem != null && parentGunItem.OwnInventory != null)
            {
                // 遍历枪内部的物品，找到那个带独立物理标签的弹匣
                foreach (var containedItem in parentGunItem.OwnInventory.AllItems)
                {
                    if (containedItem != null && containedItem.HasTag("Enable_contained_physics"))
                    {
                        containedMagazine = containedItem;
                        break;
                    }
                }
            }

            // 安全移除旧 Fixtures（这会把之前的旧弹匣治具一起删掉）
            for (int i = __instance.FarseerBody.FixtureList.Count - 1; i >= 0; i--)
            {
                var fixture = __instance.FarseerBody.FixtureList[i];
                __instance.FarseerBody.Remove(fixture); 
            }

            // 重新塞入枪支自身的镜像治具
            foreach (var vertices in parts)
            {
                var polyShape = new FarseerPhysics.Collision.Shapes.PolygonShape(vertices, density);
                var fixture = __instance.FarseerBody.CreateFixture(polyShape, collisionCategory, collidesWith);
                fixture.UserData = __instance;
            }

            // 🌟【核心修复】：如果枪里有弹匣，在这里为它重新注入完美翻转后的新治具！
            if (containedMagazine != null && OriginalMagazineShapes.TryGetValue(containedMagazine, out var origVertices))
            {
                // 计算当前翻转方向下的相对偏移量
                Vector2 worldOffset = containedMagazine.WorldPosition - parentGunItem.WorldPosition;
                
                // 💥【物理镜像关键点】：如果枪翻转了，弹匣在 X 轴上的相对偏移需要取反！
                // 这样弹匣碰撞箱才会跟着枪的美术贴图一起跑到左边或右边
                if (currentDir < 0)
                {
                    worldOffset.X = -worldOffset.X;
                }

                Vector2 localOffsetSim = ConvertUnits.ToSimUnits(worldOffset);

                // 根据原始顶点和新偏移克隆新形状
                var newVertices = new FarseerPhysics.Common.Vertices(origVertices);
                for (int i = 0; i < newVertices.Count; i++)
                {
                    // 如果枪向左(Dir < 0)，弹匣碰撞箱自身的形状顶点可能也需要水平镜像
                    if (currentDir < 0)
                    {
                        newVertices[i] = new Vector2(-newVertices[i].X, newVertices[i].Y);
                    }
                    newVertices[i] += localOffsetSim;
                }

                var attachedShape = new FarseerPhysics.Collision.Shapes.PolygonShape(newVertices, density);
                var clonedFixture = __instance.FarseerBody.CreateFixture(attachedShape, collisionCategory, collidesWith);
                clonedFixture.UserData = __instance;

                // 重新更新全局追踪字典，确保拔出时能正常销毁
                AttachedFixtures[containedMagazine] = clonedFixture;
            }

            // 强制重设 Farseer 质心与物理质量，防止位置偏置
            __instance.FarseerBody.ResetMassData();
            __instance.FarseerBody.Awake = true;
        }

        [HarmonyPatch(typeof(PhysicsBody), nameof(PhysicsBody.Remove))]
        [HarmonyPrefix]
        public static void RemovePrefix(PhysicsBody __instance)
        {
            // 物体被删除时，清理全局字典，杜绝内存泄漏
            if (LastDirections.ContainsKey(__instance)) LastDirections.Remove(__instance);
            if (TableInstanceStorage.ContainsKey(__instance)) TableInstanceStorage.Remove(__instance);
        }

        // --- 以下原版代码保持正常映射即可 ---
        [HarmonyPatch(nameof(PhysicsBody.GetLocalFront))]
        [HarmonyPrefix]
        public static bool GetLocalFrontPrefix(PhysicsBody __instance, float? spritesheetRotation, ref Vector2 __result)
        {
            if (__instance.BodyShape != CustomShapeManager.Triangle && 
                __instance.BodyShape != CustomShapeManager.Hexagon &&
                __instance.BodyShape != CustomShapeManager.Table) return true;

            Vector2 localFront = Vector2.Zero;
            if (__instance.BodyShape == CustomShapeManager.Triangle)
            {
                float centroidY = __instance.Height / 3f;
                localFront = new Vector2(0f, __instance.Height - centroidY);
            }
            else if (__instance.BodyShape == CustomShapeManager.Table)
            {
                localFront = new Vector2(0f, __instance.Height * 0.5f);
            }
            __result = spritesheetRotation.HasValue ? PhysicsBody.RotateVector(localFront, spritesheetRotation.Value) : localFront;
            return false; 
        }

        [HarmonyPatch(nameof(PhysicsBody.GetMaxExtent))]
        [HarmonyPrefix]
        public static bool GetMaxExtentPrefix(PhysicsBody __instance, ref float __result)
        {
            if (__instance.BodyShape != CustomShapeManager.Triangle && 
                __instance.BodyShape != CustomShapeManager.Hexagon &&
                __instance.BodyShape != CustomShapeManager.Table) return true;
            __result = new Vector2(__instance.Width * 0.5f, __instance.Height * 0.5f).Length();
            return false;
        }

        [HarmonyPatch(nameof(PhysicsBody.GetSize))]
        [HarmonyPrefix]
        public static bool GetSizePrefix(PhysicsBody __instance, ref Vector2 __result)
        {
            if (__instance.BodyShape != CustomShapeManager.Triangle && 
                __instance.BodyShape != CustomShapeManager.Hexagon &&
                __instance.BodyShape != CustomShapeManager.Table) return true;
            __result = new Vector2(__instance.Width, __instance.Height);
            return false;
        }

        // 追踪寄生治具的全局字典
    public static readonly Dictionary<Barotrauma.Item, FarseerPhysics.Dynamics.Fixture> AttachedFixtures = 
        new Dictionary<Barotrauma.Item, FarseerPhysics.Dynamics.Fixture>();

    // 🌟 新增：专门用来持久化备份弹匣刚出生时的原始顶点形状，防止翻转逻辑因重复计算而变形变形
    public static readonly Dictionary<Barotrauma.Item, FarseerPhysics.Common.Vertices> OriginalMagazineShapes = 
        new Dictionary<Barotrauma.Item, FarseerPhysics.Common.Vertices>();

    [HarmonyPatch(typeof(Barotrauma.Inventory), "PutItem")]
    [HarmonyPostfix]
    public static void PutItemPostfix(Barotrauma.Item item)
    {
        if (item == null) return;

        if (item.HasTag("Enable_contained_physics") && item.ParentInventory?.Owner is Barotrauma.Item parentGun)
        {
            if (item.body == null || parentGun.body == null || parentGun.body.FarseerBody == null || !parentGun.HasTag("gun")) return;
            if (AttachedFixtures.ContainsKey(item)) return;

            if (item.body.FarseerBody.FixtureList.Count > 0)
            {
                var origFixture = item.body.FarseerBody.FixtureList[0];
                if (origFixture.Shape is FarseerPhysics.Collision.Shapes.PolygonShape origPoly)
                {
                    // 🌟 备份原始顶点数据，供枪支 Flip 刷新的时时候用
                    if (!OriginalMagazineShapes.ContainsKey(item))
                    {
                        OriginalMagazineShapes[item] = new FarseerPhysics.Common.Vertices(origPoly.Vertices);
                    }

                    float currentDir = parentGun.body.Dir;
                    Vector2 worldOffset = item.WorldPosition - parentGun.WorldPosition;
                    
                    // 如果装载时枪已经是反向的，应用反向坐标偏移
                    if (currentDir < 0)
                    {
                        worldOffset.X = -worldOffset.X;
                    }

                    Vector2 localOffsetSim = ConvertUnits.ToSimUnits(worldOffset);

                    var newVertices = new FarseerPhysics.Common.Vertices(origPoly.Vertices);
                    for (int i = 0; i < newVertices.Count; i++)
                    {
                        if (currentDir < 0)
                        {
                            newVertices[i] = new Vector2(-newVertices[i].X, newVertices[i].Y);
                        }
                        newVertices[i] += localOffsetSim;
                    }

                    var attachedShape = new FarseerPhysics.Collision.Shapes.PolygonShape(newVertices, origPoly.Density);
                    var clonedFixture = parentGun.body.FarseerBody.CreateFixture(
                        attachedShape, 
                        parentGun.body.CollisionCategories, 
                        parentGun.body.CollidesWith
                    );
                    clonedFixture.UserData = parentGun.body;

                    AttachedFixtures[item] = clonedFixture;
                    item.body.Enabled = false;

                    parentGun.body.FarseerBody.ResetMassData();
                    parentGun.body.FarseerBody.Awake = true;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Barotrauma.Inventory), "RemoveItem")]
    [HarmonyPostfix]
    public static void RemoveItemPostfix(Barotrauma.Item item)
    {
        if (item == null) return;

        if (AttachedFixtures.TryGetValue(item, out var clonedFixture))
        {
            var parentBody = clonedFixture.Body;
            if (parentBody != null)
            {
                parentBody.Remove(clonedFixture);
                parentBody.ResetMassData();
                parentBody.Awake = true;
            }

            AttachedFixtures.Remove(item);
            // 拔出时清理形状备份字典
            if (OriginalMagazineShapes.ContainsKey(item)) OriginalMagazineShapes.Remove(item);

            if (item.body != null)
            {
                item.body.Enabled = true;
                item.body.BodyType = FarseerPhysics.BodyType.Dynamic;
                item.body.CollisionCategories = Barotrauma.Physics.CollisionItem;
                item.body.FarseerBody.Awake = true;
            }
        }
    }
    }
}