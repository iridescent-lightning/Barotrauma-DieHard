﻿using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;


using Barotrauma;
using HarmonyLib;
using System.Globalization;
using System.Reflection;



namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(CharacterHealth))]
    public partial class CharacterHealthPatch
    {
        [HarmonyPatch("DrawHUD")]
        [HarmonyPostfix]
        public static void DrawStaminaBar_Postfix(CharacterHealth __instance, SpriteBatch spriteBatch)
        {
            // 仅在本地玩家查看自己的健康界面或HUD时绘制
            if (Character.Controlled == null || __instance.Character != Character.Controlled) return;
            if (__instance.Character.IsDead) return;

            // 1. 获取疲劳值 (假设 Affliction 名字为 fatigue)
            // 如果你使用的是自定义变量，请替换此处逻辑
            var fatigueAffliction = __instance.GetAffliction("fatigue");
            float fatigueAmount = fatigueAffliction?.Strength ?? 0f;
            float maxFatigue = fatigueAffliction?.Prefab.MaxStrength ?? 100f;

            // 计算体力百分比 (0.0 到 1.0)，0表示全红/空，1表示全蓝/满
            float staminaPercent = MathHelper.Clamp(1.0f - (fatigueAmount / maxFatigue), 0f, 1f);

            // 2. 获取原版血条的引用来确定位置
            // healthBar 是 CharacterHealth 的私有字段，通过反射获取
            var healthBarField = typeof(CharacterHealth).GetField("healthBar", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (healthBarField == null) return;
            
            GUIProgressBar healthBar = (GUIProgressBar)healthBarField.GetValue(__instance);
            if (healthBar == null || !healthBar.Visible) return;

            // 3. 定义体力条的尺寸和位置 (放在血条上方 10 像素处)
            int barHeight = 12;
            int spacing = -10;
            Rectangle baseRect = healthBar.Rect;
            Rectangle staminaRect = new Rectangle(baseRect.X, baseRect.Y - barHeight - spacing, (int)(baseRect.Width * staminaPercent), barHeight);
            Rectangle bgRect = new Rectangle(baseRect.X, baseRect.Y - barHeight - spacing, baseRect.Width, barHeight);

            // 4. 计算颜色过渡：满时蓝色 (Color.Blue)，缺时绿色 (Color.Green)
            Color staminaColor = Color.Lerp(Color.Red, Color.Green, staminaPercent);

            // 5. 执行绘制
            // 绘制背景 (半透明黑)
            GUI.DrawRectangle(spriteBatch, bgRect, Color.Black * 0.5f, isFilled: true);
            // 绘制体力槽
            GUI.DrawRectangle(spriteBatch, staminaRect, staminaColor * 0.8f, isFilled: true);
            // 绘制边框
            GUI.DrawRectangle(spriteBatch, bgRect, Color.White * 0.5f, isFilled: false, thickness: 1);
        }
    }
}