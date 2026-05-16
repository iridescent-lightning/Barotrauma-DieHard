﻿using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;


using Barotrauma;
using HarmonyLib;
using System.Globalization;
using System.Reflection;
using System.Net.NetworkInformation;



namespace BarotraumaDieHard
{
	[HarmonyPatch(typeof(Connection))]
    class ConnectionPatch
    {
        

		public static Sprite MyMechanicalSprite;
		[HarmonyPatch("DrawConnection")]
		[HarmonyPrefix]
		public static bool DrawConnectionPrefix(Connection __instance, SpriteBatch spriteBatch, ConnectionPanel panel, Vector2 position, Vector2 labelPos)
		{
			// 检查是否是目标接口
			if (__instance.Name.Contains("mechanical", System.StringComparison.InvariantCultureIgnoreCase))
			{
				
				string text = __instance.DisplayName.Value.ToUpperInvariant();
				
				// 从你已有的字典里获取贴图
				if (Main.customSprites.TryGetValue("mechanical_slot", out Sprite customSprite))
				{
					// 1. 绘制黄色标签
					DrawMechanicalLabel(spriteBatch, text, labelPos);
					

					// 2. 计算你的插槽参数 (用于绘制和高亮检测)
            float finalScale = (35.0f * GUI.Scale) / customSprite.SourceRect.Width;
            Vector2 origin = new Vector2(customSprite.SourceRect.Width / 2f, customSprite.SourceRect.Height / 2f);
            
            // 计算逻辑尺寸和矩形 (用于高亮和拖拽检测)
            Vector2 scaledSize = customSprite.size * finalScale;
            // Rectangle 参数: (x, y, width, height) 中心点减去一半尺寸
            Rectangle detectionArea = new Rectangle(
                (int)(position.X - scaledSize.X / 2f),
                (int)(position.Y - scaledSize.Y / 2f),
                (int)scaledSize.X,
                (int)scaledSize.Y
            );

            // 3. 自定义高亮检测 (方形区域检测)
            bool isMouseOn = detectionArea.Contains(PlayerInput.MousePosition);
            
            // 绘制方形贴图
            customSprite.Draw(spriteBatch, position, Color.White, origin: origin, scale: finalScale);

            if (isMouseOn)
            {
                // 如果鼠标在方形内，使用你自定义的方形高亮 (或者原版的方形 glowSprite)
                DrawMechanicalHighlight(spriteBatch, detectionArea);
                
                // 处理鼠标拖拽拔线逻辑 (复刻原版核心逻辑)
                // 原版代码：if (isMouseOn) { // 处理拔线... }
                // 我们在Prefix中无法直接触及原版的拔线具体方法，但可以设置静态变量引导原版逻辑
                // 或者在这里处理自定义拖拽开始 (这取决于你如何注册 DraggingConnected)
            }

					return false; // 拦截原版绘制
				}
			}
			return true; // 其他情况执行原版逻辑
		}
		
		// 辅助方法：绘制特定样式的标签
		private static void DrawMechanicalLabel(SpriteBatch spriteBatch, string text, Vector2 labelPos)
		{
			if (GUIStyle.GetComponentStyle("ConnectionPanelLabel")?.Sprites.Values.First().First() is UISprite labelSprite)
			{
				Vector2 textSize = GUIStyle.SmallFont.MeasureString(text);
				Rectangle labelArea = new Rectangle(labelPos.ToPoint(), textSize.ToPoint());
				labelArea.Inflate((int)(10 * GUI.Scale), (int)(3.2 * GUI.Scale));

				// 保留背景，但颜色改为更深的暗蓝色或深红，以衬托黄色文字
				labelSprite.Draw(spriteBatch, labelArea, Color.Brown * 0.7f); 
			}

			// 文字保持黄色
			GUI.DrawString(spriteBatch, labelPos + Vector2.UnitY, text, Color.Black * 0.8f, font: GUIStyle.SmallFont);
			GUI.DrawString(spriteBatch, labelPos, text, Color.White, font: GUIStyle.SmallFont);
		}


		// 辅助方法：绘制方形高亮框
		private static void DrawMechanicalHighlight(SpriteBatch spriteBatch, Rectangle area)
		{
			// 如果你有方形高亮 Sprite：
			// GameSessionDieHard.customSprites["mechanical_slot_glow"].Draw(spriteBatch, area, Color.White * 0.5f);
			
			// 或者简单的画一个方形边框
			GUI.DrawRectangle(spriteBatch, area, Color.Green * 0.6f, isFilled: false, thickness: 2);
		}

		[HarmonyPatch("DrawWire")]
		[HarmonyPostfix]
		public static void DrawWirePostfix(SpriteBatch spriteBatch, Wire wire, Vector2 end, Vector2 start, Wire equippedWire, ConnectionPanel panel, LocalizedString label)
		{
			// 1. 静态方法没有 __instance，直接删掉该参数。
			// 2. 基础防崩：如果线缆或面板不存在，直接退回原版逻辑
			if (wire == null || panel == null) { return ; }

			// 3. 核心：通过 wire 找到它在当前 panel 上的接口 (Connection)
			// 线缆有两个端点，我们要找的是连接在当前这个面板上的那一头
			var connection = wire.Connections.FirstOrDefault(c => c != null && c.Item == panel.Item);

			// 4. 判断该接口是否为机械接口
			if (connection != null && connection.Name.Contains("mechanical", StringComparison.InvariantCultureIgnoreCase))
			{
				if (Main.customSprites.TryGetValue("water_pipe", out Sprite customWireSprite))
				{
					// --- 开始自定义绘制逻辑 ---
					Vector2 diff = end - start;
					float angle = (float)Math.Atan2(diff.Y, diff.X);
					float length = diff.Length();

					// 设置管道/线缆的起点中心
					Vector2 origin = new Vector2(0, customWireSprite.SourceRect.Height / 2f);
					
					// X轴拉伸至两点间距，Y轴跟随全局UI缩放
					Vector2 scale = new Vector2(length / customWireSprite.SourceRect.Width, GUI.Scale * 0.4f);

					// 绘制
					customWireSprite.Draw(
						spriteBatch, 
						start, 
						Color.White, 
						origin: origin, 
						rotate: angle, 
						scale: scale, 
						depth: 0.95f);

					//return false; // 拦截成功，不再运行原版画细线的逻辑
				}
			}

			//return true; // 如果不是机械接口，正常执行原版画线逻辑
		}

        
	}

    


    
}