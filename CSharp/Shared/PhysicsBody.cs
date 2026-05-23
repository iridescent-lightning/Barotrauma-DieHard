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
    public const Barotrauma.PhysicsBody.Shape Table = (Barotrauma.PhysicsBody.Shape)102; // 新增：复合桌子形状

    /// <summary>
    /// 生成单体多边形顶点（兼容原有逻辑）
    /// </summary>
    public static FarseerPhysics.Common.Vertices CreateCustomVertices(Barotrauma.PhysicsBody.Shape shape, float width, float height, float radius)
    {
        var vertices = new FarseerPhysics.Common.Vertices();

        if (shape == Triangle)
        {
            //DebugConsole.NewMessage("is triangle");
            float centroidY = height / 3f;
            vertices.Add(new Vector2(0f, height - centroidY));           // 顶
            vertices.Add(new Vector2(-width / 2f, 0f - centroidY));     // 左下
            vertices.Add(new Vector2(width / 2f, 0f - centroidY));      // 右下
        }

        return vertices;
    }

    /// <summary>
    /// 核心扩展：为复合桌子生成多组独立的物理部件顶点
    /// 所有部件的坐标都必须相对于桌子的物理质心 (0,0) 进行偏置
    /// </summary>
    public static List<FarseerPhysics.Common.Vertices> CreateTableParts(float boardWidth, float boardHeight, float legWidth = 10f, float legHeight = 40f, float legOffsetFromEdge = 10f)
{
    var parts = new List<FarseerPhysics.Common.Vertices>();

    // 1. 计算由独立部件拼接出来的组合总高度
    float totalHeight = boardHeight + legHeight;

    // 2. 为了让整张桌子的整体物理中心完美锁定在 (0,0)，计算各个部件的 Y 轴分界线：
    // 整体最顶端 Y = totalHeight / 2
    // 整体最底端 Y = -totalHeight / 2
    float topTopY = totalHeight / 2f;               // 桌板顶部 Y
    float boardBottomY = topTopY - boardHeight;     // 桌板底部 Y (同时也是桌腿顶部 Y)
    float legBottomY = -totalHeight / 2f;           // 桌腿底部 Y

    // 3. X 轴依然以桌板宽度居中
    float halfBoardWidth = boardWidth / 2f;

    // --- 部件 A: 桌面 (严格逆时针：左下 -> 右下 -> 右上 -> 左上) ---
    var topVertices = new FarseerPhysics.Common.Vertices();
    topVertices.Add(ConvertPixel(new Vector2(-halfBoardWidth, boardBottomY))); // 左下
    topVertices.Add(ConvertPixel(new Vector2(halfBoardWidth, boardBottomY)));  // 右下
    topVertices.Add(ConvertPixel(new Vector2(halfBoardWidth, topTopY)));     // 右上
    topVertices.Add(ConvertPixel(new Vector2(-halfBoardWidth, topTopY)));    // 左上
    parts.Add(topVertices);

    // --- 部件 B: 左桌腿 (严格逆时针) ---
    var leftLegVertices = new FarseerPhysics.Common.Vertices();
    float leftLegLeftX = -halfBoardWidth + legOffsetFromEdge;
    float leftLegRightX = leftLegLeftX + legWidth;

    leftLegVertices.Add(ConvertPixel(new Vector2(leftLegLeftX, legBottomY)));  // 左下
    leftLegVertices.Add(ConvertPixel(new Vector2(leftLegRightX, legBottomY))); // 右下
    leftLegVertices.Add(ConvertPixel(new Vector2(leftLegRightX, boardBottomY)));// 右上
    leftLegVertices.Add(ConvertPixel(new Vector2(leftLegLeftX, boardBottomY))); // 左上
    parts.Add(leftLegVertices);

    // --- 部件 C: 右桌腿 (严格逆时针) ---
    var rightLegVertices = new FarseerPhysics.Common.Vertices();
    float rightLegRightX = halfBoardWidth - legOffsetFromEdge;
    float rightLegLeftX = rightLegRightX - legWidth;

    rightLegVertices.Add(ConvertPixel(new Vector2(rightLegLeftX, legBottomY)));  // 左下
    rightLegVertices.Add(ConvertPixel(new Vector2(rightLegRightX, legBottomY))); // 右下
    rightLegVertices.Add(ConvertPixel(new Vector2(rightLegRightX, boardBottomY)));// 右上
    rightLegVertices.Add(ConvertPixel(new Vector2(rightLegLeftX, boardBottomY))); // 左上
    parts.Add(rightLegVertices);

    return parts;

    // 内部本地像素转物理米函数
    Vector2 ConvertPixel(Vector2 pixelVec)
    {
        return ConvertUnits.ToSimUnits(pixelVec);
    }
}

[HarmonyPatch(typeof(PhysicsBody))]
public static partial class PhysicsBodyPatch
{
    [ThreadStatic] private static PhysicsBody.Shape? t_PendingCustomShape;
    // 线程静态变量：用于临时缓存从 XML 读取的自定义桌子微调参数（单位：米）
    [ThreadStatic] private static float t_LegWidth;
    [ThreadStatic] private static float t_LegHeight;
    [ThreadStatic] private static float t_LegOffset;

    [HarmonyPatch(typeof(PhysicsBody), MethodType.Constructor, new Type[] { typeof(XElement), typeof(Vector2), typeof(float), typeof(float?), typeof(Category), typeof(Category), typeof(bool) })]
    [HarmonyPrefix]
    public static void ConstructorPrefix(XElement element)
    {
        if (element == null) return;
        
        // 【核心防呆】：如果传进来的是顶级节点（如 <DebugTable> 或 <Item>），
        // 我们主动往深处找一下有没有叫 "Body" 或 "PhysicsBody" 的子节点
        XElement targetElement = element;
        if (element.Name.LocalName.ToLowerInvariant() != "body" && element.Name.LocalName.ToLowerInvariant() != "physicsbody")
        {
            var subBody = element.Element("Body") ?? element.Element("PhysicsBody");
            if (subBody != null)
            {
                targetElement = subBody; // 锁定制导，直接去读 <Body> 节点内部的属性！
            }
        }
        
        // 从正确的节点上获取 customshape
        string customShapeStr = targetElement.GetAttributeString("customshape", "").ToLowerInvariant();
        
        if (customShapeStr == "triangle")
        {
            //DebugConsole.NewMessage("trianle found");
            t_PendingCustomShape = CustomShapeManager.Triangle;
        }
        else if (customShapeStr == "hexagon")
        {
            t_PendingCustomShape = CustomShapeManager.Hexagon;
        }
        else if (customShapeStr == "table")
        {
            t_PendingCustomShape = CustomShapeManager.Table;
            
            // 直接读取纯像素值存储到全局缓存变量中，不要在这一步进行米转换！
            t_LegWidth = targetElement.GetAttributeFloat("legwidth", 12f);
            t_LegHeight = targetElement.GetAttributeFloat("legheight", 40f);
            t_LegOffset = targetElement.GetAttributeFloat("legoffset", 15f);
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

        // 1. 初始化刚体壳
        __instance.FarseerBody = GameMain.World.CreateBody();
        __instance.FarseerBody.BodyType = bodyType;
        __instance.FarseerBody.UserData = __instance; 
        __instance.CollisionCategories = collisionCategory;
        __instance.CollidesWith = collidesWith;

        if (currentShape == CustomShapeManager.Table)
        {
            // 2. 将传入的桌板米单位还原为桌板像素单位
            float boardWidthPixels = ConvertUnits.ToDisplayUnits(width);
            float boardHeightPixels = ConvertUnits.ToDisplayUnits(height);

            // 注意：此时这里的 t_LegWidth 和 t_LegHeight 我们应该在 ConstructorPrefix 保持存储像素值！
            // 3. 生成独立定义的复合部件
            var parts = CustomShapeManager.CreateTableParts(boardWidthPixels, boardHeightPixels, t_LegWidth, t_LegHeight, t_LegOffset);
            
            foreach (var vertices in parts)
            {
                var shape = new FarseerPhysics.Collision.Shapes.PolygonShape(vertices, density);
                var fixture = __instance.FarseerBody.CreateFixture(shape, collisionCategory, collidesWith);
                fixture.UserData = __instance; 
            }

            // 4. 【核心赋值】：重新计算并把整个物体的逻辑总宽高反馈给引擎
            // 总高度 = 桌板高 + 腿高
            __instance.Width = width; 
            __instance.Height = height + ConvertUnits.ToSimUnits(t_LegHeight);
            __instance.Radius = radius;
        }
        else
        {
            // 兼容原有三角形和六边形
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

    [HarmonyPatch(nameof(PhysicsBody.GetLocalFront))]
    [HarmonyPrefix]
    public static bool GetLocalFrontPrefix(PhysicsBody __instance, float? spritesheetRotation, ref Vector2 __result)
    {
        // 增加对 Table 形状的拦截判定
        if (__instance.BodyShape != CustomShapeManager.Triangle && 
            __instance.BodyShape != CustomShapeManager.Hexagon &&
            __instance.BodyShape != CustomShapeManager.Table)
        {
            return true;
        }

        Vector2 localFront = Vector2.Zero;

        if (__instance.BodyShape == CustomShapeManager.Triangle)
        {
            float centroidY = __instance.Height / 3f;
            localFront = new Vector2(0f, __instance.Height - centroidY);
        }
        else if (__instance.BodyShape == CustomShapeManager.Table)
        {
            // 对于桌子，它的“最前端/交互核心面”一般定义为“桌面正中心”
            // 由于我们的质心在 (0,0)，桌面的正表面在 Y 的最顶端，即总高度的一半
            // （注：此处的 Height 已经是转换为物理米单位之后的数值）
            localFront = new Vector2(0f, __instance.Height * 0.5f);
        }

        __result = spritesheetRotation.HasValue ? PhysicsBody.RotateVector(localFront, spritesheetRotation.Value) : localFront;
        return false; 
    }

        [HarmonyPatch(nameof(PhysicsBody.GetMaxExtent))]
        [HarmonyPrefix]
        public static bool GetMaxExtentPrefix(PhysicsBody __instance, ref float __result)
        {
            // 增加对 Table 形状的拦截判定
            if (__instance.BodyShape != CustomShapeManager.Triangle && 
                __instance.BodyShape != CustomShapeManager.Hexagon &&
                __instance.BodyShape != CustomShapeManager.Table)
            {
                return true;
            }

            // 桌子本质上还是由外围的 Width 和 Height 包裹的复合多边形
            // 它的最长外部半径依然是质心 (0,0) 到任意一个最远边角（如桌面左上角）的斜边距离
            __result = new Vector2(__instance.Width * 0.5f, __instance.Height * 0.5f).Length();
            return false;
        }

        [HarmonyPatch(nameof(PhysicsBody.GetSize))]
        [HarmonyPrefix]
        public static bool GetSizePrefix(PhysicsBody __instance, ref Vector2 __result)
        {
            // 增加对 Table 形状的拦截判定
            if (__instance.BodyShape != CustomShapeManager.Triangle && 
                __instance.BodyShape != CustomShapeManager.Hexagon &&
                __instance.BodyShape != CustomShapeManager.Table)
            {
                return true;
            }

            // 返回包含整张桌子（包含桌腿和桌面）的完整外部逻辑像素尺寸
            __result = new Vector2(__instance.Width, __instance.Height);
            return false;
        }

        
    }
}