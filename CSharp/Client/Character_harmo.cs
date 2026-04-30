﻿using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Barotrauma.Particles;
using Barotrauma.Sounds;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Barotrauma.Extensions;


using Barotrauma;
using HarmonyLib;
using System.Globalization;
using System.Reflection;



namespace CharacterMod
{
    class CharacterMod : IAssemblyPlugin
    {
        public Harmony harmony;
		
        public static bool hasZoomed = false;

		public void Initialize()
		{
		    harmony = new Harmony("CharacterModClient");

			
			
            harmony.Patch(
                original: typeof(Character).GetMethod("ControlLocalPlayer"),
                postfix: new HarmonyMethod(typeof(CharacterMod).GetMethod(nameof(ControlLocalPlayer))));
        }

		public void OnLoadCompleted() { }
		public void PreInitPatching() { }

		public void Dispose()
		{
		  harmony.UnpatchSelf();
		  harmony = null;
		}
		

		public static void ControlLocalPlayer(float deltaTime, Camera cam, bool moveCam, Character __instance)
		{
			if (!Character.DisableControls && Character.Controlled != null)
			{
				

				var type = typeof(Camera);
				var globalZoomScaleField = type.GetField("globalZoomScale", BindingFlags.NonPublic | BindingFlags.Instance);
				var maxZoomField = type.GetField("maxZoom", BindingFlags.NonPublic | BindingFlags.Instance);
				var minZoomField = type.GetField("minZoom", BindingFlags.NonPublic | BindingFlags.Instance);

				// 1. 初始化设置（只需执行一次）
				if (hasZoomed == false)
				{
					if (maxZoomField != null) maxZoomField.SetValue(cam, 50.0f);
					if (minZoomField != null) minZoomField.SetValue(cam, 0.01f);
					hasZoomed = true;
				}

				/*if (__instance.HasSelectedAnyItem )
				{
					//DebugConsole.NewMessage($"{__instance.SelectedItem}");
					if (__instance.SelectedItem.Prefab.Identifier == "navterminal")
					{
					DebugConsole.NewMessage("true");
					float targetGlobalScale = 10f;
					
					// 使用 Lerp 平滑过渡缩放感，避免瞬间切镜头的眩晕
					float currentGlobalScale = (float)globalZoomScaleField.GetValue(cam);
					float newGlobalScale = MathHelper.Lerp(currentGlobalScale, targetGlobalScale, deltaTime * 5.0f);
					
					globalZoomScaleField.SetValue(cam, newGlobalScale);
					}
				}*/


				// 2. 动态调整缩放倍率
				if (globalZoomScaleField != null)
				{
					// 判断玩家是否在潜艇外（Submarine 为 null 通常代表在开放水域）
					bool isInWater = __instance.Submarine == null;
					
					// 潜艇内用 3.5f，水里用较小值（例如 1.0f），你可以根据需要调整
					float targetGlobalScale = isInWater ? 1.5f : 2f;
					
					// 使用 Lerp 平滑过渡缩放感，避免瞬间切镜头的眩晕
					float currentGlobalScale = (float)globalZoomScaleField.GetValue(cam);
					float newGlobalScale = MathHelper.Lerp(currentGlobalScale, targetGlobalScale, deltaTime * 5.0f);
					
					globalZoomScaleField.SetValue(cam, newGlobalScale);
				}

				// 3. 原有的右键进一步观察逻辑（保持不变）
				if (PlayerInput.SecondaryMouseButtonHeld())
				{
					var cursorWorldPosition = cam.ScreenToWorld(PlayerInput.MousePosition);
					var characterPosition = __instance.WorldPosition;
					float distance = Vector2.Distance(cursorWorldPosition, characterPosition);

					if (!Character.IsMouseOnUI && distance > 400f)
					{
						// 这里修改 OffsetAmount 会让相机向鼠标方向移动，
						// 配合上面的 globalZoomScale，会实现“视野移过去且稍微拉远”的视觉效果
						float currentOffset = cam.OffsetAmount;
						float targetOffset = 1000.0f;
						float lerpFactor = 0.5f;
						cam.OffsetAmount = MathHelper.Lerp(currentOffset, targetOffset, lerpFactor);
					}
				}
			}
		}
            

        
	}

    


    
}