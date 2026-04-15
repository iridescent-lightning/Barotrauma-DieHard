﻿// This patch makes turret firing rate associated with character skill levels
using Barotrauma.Networking;
using Barotrauma.Particles;
using Barotrauma.Sounds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;


using Barotrauma;
using Barotrauma.Items.Components;
using HarmonyLib;
using System.Globalization;
using System.Reflection;



namespace BarotraumaDieHard
{
    partial class TurretDieHard : IAssemblyPlugin
    {
		
		public static void DrawHUDPostfix(SpriteBatch spriteBatch, Character character, Turret __instance)
        {
            // 只有当前控制炮塔的玩家是自己时才显示
            if (character == null || character != Character.Controlled) return;

            // 1. 获取数据并计算 (保持之前的逻辑)
            float baseReload = GetOriginalReload(__instance.Item.ID);
            float currentReload = __instance.Reload;
            float speedMult = currentReload > 0 ? baseReload / currentReload : 1.0f;
            float bonusPercent = (speedMult - 1.0f) * 100f;

            // 2. 准备文本 (使用 .ToLocalizedString() 以匹配参数要求)
            var skillText = $"武器技能: {character.GetSkillLevel("weapons"):F0}";
            var statText = (bonusPercent >= 0 
                ? $"射速加成: +{bonusPercent:F0}%" 
                : $"操作惩罚: {bonusPercent:F0}%");

            // 3. 设置坐标 (右下角参考位置)
            Vector2 position = new Vector2(GameMain.GraphicsWidth - 300, GameMain.GraphicsHeight - 150);
            Color textColor = bonusPercent >= 0 ? Color.LightGreen : Color.OrangeRed;

            // 4. 修正后的绘制调用
            // 参数顺序：spriteBatch, position (Vector2), text (LocalizedString), color
            GUI.DrawString(spriteBatch, position, skillText, Color.White);
            GUI.DrawString(spriteBatch, position + new Vector2(0, 30), statText, textColor);
        }

        
        

        
	}
    
}