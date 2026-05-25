using System;
using System.Collections.Generic;
using System.Xml.Linq;
using HarmonyLib;
using Barotrauma;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Contacts; // 🌟 引入 Contact 命名空间
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

        var topVertices = new FarseerPhysics.Common.Vertices();
        topVertices.Add(ConvertPixel(new Vector2(-halfBoardWidth, boardBottomY)));
        topVertices.Add(ConvertPixel(new Vector2(halfBoardWidth, boardBottomY)));
        topVertices.Add(ConvertPixel(new Vector2(halfBoardWidth, topTopY)));
        topVertices.Add(ConvertPixel(new Vector2(-halfBoardWidth, topTopY)));
        parts.Add(topVertices);

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

    public class TableData
    {
        public float LeftWidth;
        public float LeftHeight;
        public float LeftOffset;
        public float RightWidth;
        public float RightHeight;
        public float RightOffset;
        public float OriginalBoardHeightSim; 
    }

    // 🌟【联动修正】：维护一个外部弹匣追踪器字典
    public static class MagazineAttachmentConnector
    {
        public static readonly Dictionary<Barotrauma.Item, FarseerPhysics.Dynamics.Fixture> AttachedFixtures = new Dictionary<Barotrauma.Item, FarseerPhysics.Dynamics.Fixture>();
        public static readonly Dictionary<Barotrauma.Item, FarseerPhysics.Common.Vertices> OriginalMagazineShapes = new Dictionary<Barotrauma.Item, FarseerPhysics.Common.Vertices>();
    }

    [HarmonyPatch(typeof(PhysicsBody))]
    public static partial class PhysicsBodyPatch
    {
        [ThreadStatic] private static PhysicsBody.Shape? t_PendingCustomShape;
        [ThreadStatic] private static TableData t_TempTableData;

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

            // 检测此物品是否声明了自定义单向平台属性
            bool isPlatformItem = IsPlatformEnabled(__instance);

            if (currentShape == CustomShapeManager.Table)
            {
                TableData data = t_TempTableData ?? new TableData();
                t_TempTableData = null; 
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

                    // 🌟 如果标记为桌子平台，挂载动态结算器
                    if (isPlatformItem) fixture.BeforeSolve += HandleItemPlatformPreSolve;
                }

                float maxLegHeightPixels = Math.Max(data.LeftHeight, data.RightHeight);

                __instance.Width = width; 
                __instance.Height = height + ConvertUnits.ToSimUnits(maxLegHeightPixels);
                __instance.Radius = radius;

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

                // 🌟 普通多边形（如普通桌子形状）如果带Flag也支持
                if (isPlatformItem) fixture.BeforeSolve += HandleItemPlatformPreSolve;
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

            if (!TableInstanceStorage.TryGetValue(__instance, out TableData data)) return;

            float boardWidthPixels = ConvertUnits.ToDisplayUnits(__instance.Width);
            float boardHeightPixels = ConvertUnits.ToDisplayUnits(data.OriginalBoardHeightSim);

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

            // 获取联动弹匣物品
            Barotrauma.Item parentGunItem = __instance.FarseerBody.UserData as Barotrauma.Item;
            if (parentGunItem == null && __instance.FarseerBody.UserData is PhysicsBody pb)
            {
                parentGunItem = pb.FarseerBody.UserData as Barotrauma.Item;
            }

            Barotrauma.Item containedMagazine = null;
            if (parentGunItem != null && parentGunItem.OwnInventory != null)
            {
                foreach (var containedItem in parentGunItem.OwnInventory.AllItems)
                {
                    if (containedItem != null && containedItem.HasTag("Enable_contained_physics"))
                    {
                        containedMagazine = containedItem;
                        break;
                    }
                }
            }

            // 安全移除旧 Fixtures
            for (int i = __instance.FarseerBody.FixtureList.Count - 1; i >= 0; i--)
            {
                var fixture = __instance.FarseerBody.FixtureList[i];
                __instance.FarseerBody.Remove(fixture); 
            }

            bool isPlatformItem = IsPlatformEnabled(__instance);

            // 重新塞入镜像治具
            foreach (var vertices in parts)
            {
                var polyShape = new FarseerPhysics.Collision.Shapes.PolygonShape(vertices, density);
                var fixture = __instance.FarseerBody.CreateFixture(polyShape, collisionCategory, collidesWith);
                fixture.UserData = __instance;

                if (isPlatformItem) fixture.BeforeSolve += HandleItemPlatformPreSolve;
            }

            // 联动重组弹匣治具
            if (containedMagazine != null && CustomShapeManager.MagazineAttachmentConnector.OriginalMagazineShapes.TryGetValue(containedMagazine, out var origVertices))
            {
                Vector2 worldOffset = containedMagazine.WorldPosition - parentGunItem.WorldPosition;
                if (currentDir < 0) worldOffset.X = -worldOffset.X;

                Vector2 localOffsetSim = ConvertUnits.ToSimUnits(worldOffset);

                var newVertices = new FarseerPhysics.Common.Vertices(origVertices);
                for (int i = 0; i < newVertices.Count; i++)
                {
                    if (currentDir < 0) newVertices[i] = new Vector2(-newVertices[i].X, newVertices[i].Y);
                    newVertices[i] += localOffsetSim;
                }

                var attachedShape = new FarseerPhysics.Collision.Shapes.PolygonShape(newVertices, density);
                var clonedFixture = __instance.FarseerBody.CreateFixture(attachedShape, collisionCategory, collidesWith);
                clonedFixture.UserData = __instance;

                // 🌟 弹匣跟着枪走，如果是平台则同样赋予平台拦截
                if (isPlatformItem) clonedFixture.BeforeSolve += HandleItemPlatformPreSolve;

                CustomShapeManager.MagazineAttachmentConnector.AttachedFixtures[containedMagazine] = clonedFixture;
            }

            __instance.FarseerBody.ResetMassData();
            __instance.FarseerBody.Awake = true;
        }

        [HarmonyPatch(typeof(PhysicsBody), nameof(PhysicsBody.Remove))]
        [HarmonyPrefix]
        public static void RemovePrefix(PhysicsBody __instance)
        {
            if (LastDirections.ContainsKey(__instance)) LastDirections.Remove(__instance);
            if (TableInstanceStorage.ContainsKey(__instance)) TableInstanceStorage.Remove(__instance);
        }

        // 🌟【安全辅助函数】：检查物品是否带有 `isitemplatform` 的标记
        private static bool IsPlatformEnabled(PhysicsBody pb)
        {
            if (pb?.FarseerBody?.UserData is Barotrauma.Item item)
            {
                return item.HasTag("isitemplatform");
            }
            if (pb?.FarseerBody?.UserData is PhysicsBody instance && instance.FarseerBody?.UserData is Barotrauma.Item nestedItem)
            {
                return nestedItem.HasTag("isitemplatform");
            }
            return false;
        }

        /// <summary>
        /// 🌟【神级单向平台解算器】：高精度控制穿透与站立状态
        /// </summary>
        private static void HandleItemPlatformPreSolve(Fixture fixtureA, Fixture fixtureB, Contact contact)
        {
            // 获取碰撞的角色实体
            Barotrauma.Character character = null;
            if (fixtureB.Body.UserData is Barotrauma.Limb limb) character = limb.character;
            else if (fixtureB.Body.UserData is Barotrauma.Character c) character = c;
            else if (fixtureB.Body.UserData is Barotrauma.PhysicsBody pb) character = pb.FarseerBody.UserData as Barotrauma.Character;

            // 如果撞击物理盒的不是人类/生物，允许硬碰撞（例如箱子、墙壁、手榴弹照常砸在上面）
            if (character == null) return;

            // 1. 如果角色变成了 Ragdoll（晕倒、死亡、重力瘫软），允许他的肢体落在上面
            if (character.IsRagdolled) return;

            // 2. 核心状态判定：检查玩家按键输入
            bool isPressingUp   = character.IsKeyHit(Barotrauma.InputType.Up);
            bool isPressingDown = character.IsKeyHit(Barotrauma.InputType.Down);

            // 计算物理边缘坐标 (米单位)
            float tableTopY = fixtureA.Body.Position.Y; 
            float characterBottomY = character.AnimController.Collider.Body.Position.Y - (character.AnimController.Collider.Height / 2f);

            // 3. 【精准阻断机制】：
            // 情况 A：玩家按住了【S / 下键】（想要主动从桌子上落下来）
            // 情况 B：玩家的脚部基准面还在桌子台面边缘下方（属于从下方钻过或从两侧跑过）
            // 情况 C：玩家平地跑步路过，没有尝试按住【W / 上键】登台，且不是从高空坠落下来
            if (isPressingDown || (characterBottomY < tableTopY - 0.05f))
            {
                contact.Enabled = false; // 💥 直接将此帧碰撞接触失效化，达成完美穿透
                return;
            }

            // 情况 D：玩家站在桌重叠区但没有跳起/登台的输入，且角色还在往上运动（避免卡进边缘发生吸附抖动）
            if (!isPressingUp && character.AnimController.Collider.LinearVelocity.Y > 0.1f)
            {
                contact.Enabled = false;
                return;
            }

            // 满足其余所有条件（例如：按住W键主动攀登、或从天花板/二层平台精准垂直下落踩在台面上），不干涉解算，让玩家站立。
        }

        // --- 原文映射保持不变 ---
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

        [HarmonyPatch(typeof(Barotrauma.Inventory), "PutItem")]
        [HarmonyPostfix]
        public static void PutItemPostfix(Barotrauma.Item item)
        {
            if (item?.body == null) return;
            if (item.HasTag("Enable_contained_physics") && item.ParentInventory?.Owner != null && item.ParentInventory.Owner is Item parentItem)
            {
                item.body.Enabled = true;
                item.body.BodyType = FarseerPhysics.BodyType.Dynamic;
                item.body.CollisionCategories = Physics.CollisionItem;
                DebugConsole.NewMessage($"[独立物理] 弹匣 {item.Name} 已成功开启碰撞。");
            }
        }
    }
}