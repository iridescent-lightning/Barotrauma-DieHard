﻿using System;
using System.Collections.Generic;
using System.Xml.Linq;
using HarmonyLib;
using Barotrauma;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using Microsoft.Xna.Framework; 
using Microsoft.Xna.Framework.Graphics;
using Barotrauma.Extensions;



[HarmonyPatch(typeof(PhysicsBody))]
public static partial class PhysicsBodyPatch
{

        [HarmonyPatch(nameof(PhysicsBody.DebugDraw))]
        [HarmonyPrefix]
        public static bool DebugDrawPrefix(PhysicsBody __instance, SpriteBatch spriteBatch, Color color, bool forceColor)
        {
            // 允许自定义形状进入绘制范围
            if (__instance.BodyShape != CustomShapeManager.Triangle && 
                __instance.BodyShape != CustomShapeManager.Hexagon &&
                __instance.BodyShape != CustomShapeManager.Table)
            {
                return true; 
            }

            if (__instance.FarseerBody == null || __instance.FarseerBody.FixtureList == null)
            {
                return false;
            }

            Color drawColor = forceColor ? color : Color.LightGreen * 0.6f; 

            // 1. 获取原版已完美对齐 Hull 内外的混合渲染原点
            Vector2 baseDrawPos = __instance.DrawPosition;
            
            // 2. 顺应原版，将原点整体反转至 Y 轴向下的屏幕空间
            Vector2 screenOrigin = baseDrawPos.FlipY(); 

            // 3. 【核心修正】：去掉先前的负号。
            // 因为我们在下面提前把局部顶点的 Y 轴改成了向下，
            // 此时的二维旋转矩阵方向需要恢复为正常的物理角度，以顺应图像空间的旋转方向。
            float rotation = __instance.DrawRotation;

            foreach (var fixture in __instance.FarseerBody.FixtureList)
            {
                if (fixture.Shape is FarseerPhysics.Collision.Shapes.PolygonShape polygon)
                {
                    int vertexCount = polygon.Vertices.Count;
                    if (vertexCount < 3) continue;

                    for (int i = 0; i < vertexCount; i++)
                    {
                        // 4. 拿到物理局部顶点（米）
                        Vector2 localSimVertex1 = polygon.Vertices[i];
                        Vector2 localSimVertex2 = polygon.Vertices[(i + 1) % vertexCount];

                        // 5. 转换为局部像素单位
                        Vector2 localDisplayVertex1 = ConvertUnits.ToDisplayUnits(localSimVertex1);
                        Vector2 localDisplayVertex2 = ConvertUnits.ToDisplayUnits(localSimVertex2);

                        // 6. 将局部顶点 Y 轴取反，彻底融入 Y 轴向下的屏幕空间
                        localDisplayVertex1.Y = -localDisplayVertex1.Y;
                        localDisplayVertex2.Y = -localDisplayVertex2.Y;

                        // 7. 应用左右镜像
                        localDisplayVertex1.X *= __instance.Dir;
                        localDisplayVertex2.X *= __instance.Dir;

                        // 8. 配合正确的 rotation 角度进行矩阵旋转
                        Vector2 rotatedVertex1 = PhysicsBody.RotateVector(localDisplayVertex1, rotation);
                        Vector2 rotatedVertex2 = PhysicsBody.RotateVector(localDisplayVertex2, rotation);

                        // 9. 叠加对齐
                        Vector2 finalPoint1 = screenOrigin + rotatedVertex1;
                        Vector2 finalPoint2 = screenOrigin + rotatedVertex2;

                        // 10. 完美绘制
                        Barotrauma.GUI.DrawLine(spriteBatch, finalPoint1, finalPoint2, drawColor, width: 2, depth: 0.1f);
                    }
                }
            }

            return false; 
        }
}
