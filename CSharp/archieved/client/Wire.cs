//depth problem
using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Barotrauma.Items.Components;
using Barotrauma;

using HarmonyLib;

namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(Wire))]
    public class WirePatch
    {
        /*
        这是一个非常典型的 C# 访问权限问题。在 Wire.cs 的源码中，nodes 变量并没有 public 修饰符，这意味着它是 private 或 protected 的，外部类无法直接访问。  

既然你是在写 Harmony Patch，你有两种主要方式来获取这个列表：

方法一：使用 Harmony 的 AccessTools (推荐)
这是最标准且性能较好的做法。你可以通过 Harmony 提供的工具直接访问私有字段。
*/
        private static readonly FieldInfo nodesField = AccessTools.Field(typeof(Wire), "nodes");
        [HarmonyPatch("Draw")]
        [HarmonyPatch(new Type[] { typeof(SpriteBatch), typeof(bool), typeof(float), typeof(Color?) })]
        [HarmonyPostfix]
        public static void DrawPostfix(Wire __instance, SpriteBatch spriteBatch, bool editing, float itemDepth, Color? overrideColor)
        {

            var Nodes = nodesField.GetValue(__instance) as List<Vector2>;
            // 1. 基础过滤：如果隐藏或没有节点，则不处理
    if (__instance.Hidden ||  Nodes.Count < 2) return;

    // 2. 获取绘制偏移（复用源码中的逻辑）
    // 注意：如果 GetDrawOffset 是私有的，可能需要反射或通过 __instance.Item 重新计算
    Vector2 drawOffset = __instance.Item.Submarine == null ? 
                         Vector2.Zero : 
                         __instance.Item.Submarine.DrawPosition + __instance.Item.Submarine.HiddenSubPosition;

    // 3. 获取颜色与深度
    Color jointColor = overrideColor ?? __instance.Item.Color;
    float depth = itemDepth > 0 ? itemDepth : __instance.Item.SpriteDepth;

    // 4. 在每个节点处绘制圆点关节
    foreach (Vector2 nodePos in Nodes)
    {
        Vector2 drawPos = nodePos + drawOffset;
        drawPos.Y = -drawPos.Y; // 翻转 Y 轴以匹配渲染坐标系

        // 使用 GUI 提供的圆形或矩形填充方法，或者使用特定的圆形 Sprite
        // 这里的 Width 建议略大于线段宽度 (Width * 1.1f)
        float jointSize = __instance.Width * 15.0f; // 15.0f 是基准比例，可根据效果调整
        Color darkerColor = Color.Lerp(jointColor, Color.Black, 0.5f);
        GUI.DrawRectangle(spriteBatch, 
            drawPos - new Vector2(jointSize / 2), 
            new Vector2(jointSize), 
            darkerColor, 
            isFilled: true, 
            depth: depth + 0.1f); //让它在下面绘制
    }
        }

    }
}