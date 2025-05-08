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
				if (hasZoomed == false)
				{
                	// Access the private field 'globalZoomScale' using reflection
					var globalZoomScaleField = typeof(Camera).GetField("globalZoomScale", BindingFlags.NonPublic | BindingFlags.Instance);
					if (globalZoomScaleField != null)
					{
						// Set the 'globalZoomScale' to 2.5
						globalZoomScaleField.SetValue(cam, 2.5f);
					}
					hasZoomed = true;
				}

				if (PlayerInput.SecondaryMouseButtonHeld())
				{

					var cursorWorldPosition = cam.ScreenToWorld(PlayerInput.MousePosition);
					var characterPosition = __instance.WorldPosition;
					float distance = Vector2.Distance(cursorWorldPosition, characterPosition);
					//DebugConsole.NewMessage(distance.ToString());

					if (!Character.IsMouseOnUI && distance > 400f)
					{
					float currentOffset = cam.OffsetAmount;
					float targetOffset = 1000.0f;
					float lerpFactor = 0.5f;
					cam.OffsetAmount = MathHelper.Lerp(currentOffset, targetOffset, lerpFactor);
					}
				}

				/*if (PlayerInput.SecondaryMouseButtonHeld())
				{

					var cursorWorldPosition = cam.ScreenToWorld(PlayerInput.MousePosition);
                	cam.Position = cursorWorldPosition;
					DebugConsole.NewMessage("Mouse position: " + PlayerInput.MousePosition.ToString(), Color.White);
				}*/
        	}
		}
            

        
	}

    


    
}