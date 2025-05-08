﻿using FarseerPhysics;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;


using Barotrauma;
using HarmonyLib;
using System.Globalization;
using System.Reflection;
using Barotrauma.Items.Components;



namespace ControllerMod
{
    class ControllerMod : IAssemblyPlugin
    {
        public Harmony harmony;
		
        

		public void Initialize()
		{
		    harmony = new Harmony("ControllerMod");

			
			
            harmony.Patch(
                original: typeof(Controller).GetMethod("SecondaryUse"),
                prefix: new HarmonyMethod(typeof(ControllerMod).GetMethod(nameof(SecondaryUse))));
        }

		public void OnLoadCompleted() { }
		public void PreInitPatching() { }

		public void Dispose()
		{
		  harmony.UnpatchSelf();
		  harmony = null;
		}
		


		public static bool SecondaryUse(Controller __instance, float deltaTime, Character character, ref bool __result)
        {
			Controller _ = __instance;
            if (_.user != character)
            {
                __result =  false;
            }
            if (_.user == null || character.Removed || !_.user.IsAnySelectedItem(_.item) || !character.CanInteractWith(_.item))
            {
                _.user = null;
                __result =  false;
            }
            if (character == null)
            {
                __result =  false;
            }

            _.focusTarget = _.GetFocusTarget();

            if (_.focusTarget == null)
            {
                Vector2 centerPos = new Vector2(_.item.WorldRect.Center.X, _.item.WorldRect.Center.Y);
                Vector2 offset = character.CursorWorldPosition - centerPos;
                offset.Y = -offset.Y;
                _.targetRotation = MathUtils.WrapAngleTwoPi(MathUtils.VectorToAngle(offset));
                __result =  false;
            }

            character.ViewTarget = _.focusTarget;

#if CLIENT
    if (character == Character.Controlled && _.cam != null)
    {
        Barotrauma.Lights.LightManager.ViewTarget = _.focusTarget;
        _.cam.TargetPos = _.focusTarget.WorldPosition;
        _.cam.OffsetAmount = MathHelper.Lerp(_.cam.OffsetAmount, (_.focusTarget as Item).Prefab.OffsetOnSelected * _.focusTarget.OffsetOnSelectedMultiplier, deltaTime * 10.0f);
        _.HideHUDs(true);

        // Define the maximum distance the camera can move from the item
        float maxDistance = 6000.0f; // Adjust this value as needed
        float minDistance = 1.0f; // Adjust this value as needed
        // Get the position of the item
        Vector2 itemPosition = _.Item.WorldPosition; // Replace with the actual method to get the item position

        Vector2 cameraPosition = _.cam.Position;
        //DebugConsole.NewMessage("Camera position: " + cameraPosition.ToString(), Color.White);

        var cursorWorldPosition = _.cam.ScreenToWorld(PlayerInput.MousePosition);
        //DebugConsole.NewMessage("Cursor position: " + cursorWorldPosition.ToString(), Color.White);

        Vector2 directionToCursor = cursorWorldPosition - itemPosition;
        float distanceToCursor = directionToCursor.Length();
        //DebugConsole.NewMessage("Distance to item: " + distanceToCursor.ToString(), Color.White);

       


        // If the cursor is outside the max distance, adjust the camera position
        if (distanceToCursor > maxDistance)
        {
            // Calculate the new camera position to keep it within the max distance
            DebugConsole.NewMessage("Cursor is outside max distance", Color.White);
            
            return false;
        }
        else
        {
            // If within max distance, move the camera directly to the cursor position
            _.cam.Position = cursorWorldPosition;
        }

        Vector2 directionToCamera = cursorWorldPosition - _.cam.Position;
        float distanceToCamera = directionToCamera.Length();
        DebugConsole.NewMessage("Camera position: " + _.cam.Position.ToString(), Color.White);
        DebugConsole.NewMessage("Cursor position: " + cursorWorldPosition.ToString(), Color.White);

        DebugConsole.NewMessage("Distance to cursor: " + distanceToCamera.ToString(), Color.Orange);
        if (distanceToCamera < minDistance)
        {
            // Calculate the new camera position to keep it within the min distance
            DebugConsole.NewMessage("Close enough", Color.White);
            //return false;
        }

        // Optional: debug message
        //DebugConsole.NewMessage("Camera position: " + _.cam.Position.ToString(), Color.White);
    }
    #endif

            if (!character.IsRemotePlayer || character.ViewTarget == _.focusTarget)
            {
                Vector2 centerPos = new Vector2(_.focusTarget.WorldRect.Center.X, _.focusTarget.WorldRect.Center.Y);
                if (_.focusTarget.GetComponent<Turret>() is { } turret)
                {
                    centerPos = new Vector2(_.focusTarget.WorldRect.X + turret.TransformedBarrelPos.X, _.focusTarget.WorldRect.Y - turret.TransformedBarrelPos.Y);
                }
                Vector2 offset = character.CursorWorldPosition - centerPos;
                offset.Y = -offset.Y;
                _.targetRotation = MathUtils.WrapAngleTwoPi(MathUtils.VectorToAngle(offset));
            }
            __result =  true;
			
			return false;
        }


		


            

        
	}

    


    
}