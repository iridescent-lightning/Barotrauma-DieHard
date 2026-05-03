﻿using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FarseerPhysics;
using System.Collections.Generic;
using System;
namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(Structure))]
    public static class StructureRenderPatch
    {
        // 拦截 Draw 方法，在渲染前修改绘制参数
        [HarmonyPatch("Draw")]
        [HarmonyPatch(new Type[] { typeof(SpriteBatch), typeof(bool), typeof(bool), typeof(Effect) })]
        [HarmonyPrefix]
        public static bool Prefix(Structure __instance, SpriteBatch spriteBatch, bool editing, bool back, Effect damageEffect)
        {
            // 如果是在编辑器中，或者没有物理体，或者是全毁状态，走原版逻辑
            if (editing || __instance.Bodies == null || __instance.Bodies.Count == 0 || __instance.IsHidden)
            {
                return true;
            }

            // 检查第一个物理体是否已经是 Dynamic（假设补丁已经将其改为了 Dynamic）
            var mainBody = __instance.Bodies[0];
            if (mainBody.BodyType != BodyType.Dynamic)
            {
                return true; // 还是静态的，走原版逻辑
            }

            // --- 开始接管动态渲染逻辑 ---
            
            // 1. 获取颜色和透明度
            Color color = __instance.SpriteColor;
            if (__instance.IsHighlighted) color = GUIStyle.Orange * 0.5f;

            // 2. 获取偏移量（考虑潜艇位移）
            Vector2 drawOffset = __instance.Submarine == null ? Vector2.Zero : __instance.Submarine.DrawPosition;

            // 3. 遍历物理体进行绘制
            // 注意：当墙体断裂为多段时，每一段物理体对应一个 Bodies 成员
            foreach (var body in __instance.Bodies)
            {
                // 将物理引擎坐标转换为屏幕显示坐标
                Vector2 bodyDisplayPos = ConvertUnits.ToDisplayUnits(body.Position);
                
                // 获取物理体的旋转
                float rotation = -body.Rotation;

                // 重点：计算当前物理体对应的贴图切片（Tiled Sprite）
                // 由于 Structure 原版使用 DrawTiled 绘制一整块，
                // 在动态模式下，我们需要根据 Body 的位置重新计算 DrawTiled 的起点
                
                // 这里的 pos 需要 Y 轴翻转以匹配 XNA 坐标系
                Vector2 renderPos = new Vector2(bodyDisplayPos.X + drawOffset.X, -(bodyDisplayPos.Y + drawOffset.Y));

                // 绘制贴图
                __instance.Prefab.Sprite.DrawTiled(
                    spriteBatch,
                    renderPos,
                    __instance.rect.Size.ToVector2(), // 这里暂时使用原大小，复杂实现需要计算具体 Body 覆盖的 Section
                    rotation: rotation,
                    color: color,
                    textureScale: __instance.TextureScale * __instance.Scale,
                    depth: __instance.GetDrawDepth()
                );
            }

            return false; // 跳过原版 Draw 执行，防止贴图重影
        }
    }
}