﻿/*using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Barotrauma.Sounds;


using Barotrauma;
using HarmonyLib;
using System.Reflection;



namespace BarotraumaDieHard
{
    partial class DieHardRope : IAssemblyPlugin
    {
        

		
		
		public static bool DrawRopePrefix(SpriteBatch spriteBatch, Vector2 startPos, Vector2 endPos, int width, Color? overrideColor, Rope __instance)
	{
		// Create an array of points for the rope segments
		int segments = 10; // Adjust this value for more or fewer segments
		Vector2[] ropePoints = new Vector2[segments + 1];
		
		// Calculate the total length of the rope
		float ropeLength = Vector2.Distance(startPos, endPos);
		float segmentLength = ropeLength / segments;

		// Calculate the points for the rope with sag effect
		for (int i = 0; i <= segments; i++)
		{
			float t = (float)i / segments;
			// Interpolate between startPos and endPos
			Vector2 point = Vector2.Lerp(startPos, endPos, t);
			
			// Adding sag using a sine wave for realism
			point.Y += (float)Math.Sin(t * Math.PI) * 10f; // Adjust the sag height as needed

			ropePoints[i] = point;
		}

		// Draw the segments
		for (int i = 0; i < segments; i++)
		{
			Vector2 startSegment = ropePoints[i];
			Vector2 endSegment = ropePoints[i + 1];

			// Draw a simple line segment
			GUI.DrawLine(spriteBatch, startSegment, endSegment, overrideColor ?? __instance.SpriteColor, depth: __instance.item.GetDrawDepth(), width: width);
		}

		// Prevent the original method from executing
		return false;
	}

		
            

        
	}

}*/