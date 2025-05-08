﻿/*using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Barotrauma.Sounds;
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
    class ConnectionPanelDieHard : IAssemblyPlugin
    {
        public Harmony harmony;

		public void Initialize()
		{
		    harmony = new Harmony("ConnectionPanelDieHard");

			
			
            var originalDrawConnections = typeof(ConnectionPanel).GetMethod("DrawConnections", BindingFlags.NonPublic | BindingFlags.Instance);
            var prefixDrawConnections = new HarmonyMethod(typeof(ConnectionPanelDieHard).GetMethod(nameof(DrawConnectionsPostfix), BindingFlags.Public | BindingFlags.Static));
            harmony.Patch(originalDrawConnections, prefixDrawConnections, null);
        }

		public void OnLoadCompleted() { }
		public void PreInitPatching() { }

		public void Dispose()
		{
		  harmony.UnpatchSelf();
		  harmony = null;
		}
		
		public static bool DrawConnectionsPostfix(SpriteBatch spriteBatch, GUICustomComponent container, ConnectionPanel __instance)
		{
			ConnectionPanel _ = __instance;
			if (_.user != Character.Controlled || _.user == null) { return false; }

            ConnectionPanel.HighlightedWire = null;
			
            Connection.DrawConnections(spriteBatch, _, _.dragArea.Rect, _.user, out (Vector2 tooltipPos, LocalizedString text) tooltip);
            foreach (UISprite sprite in GUIStyle.GetComponentStyle("ConnectionPanelFront").Sprites[GUIComponent.ComponentState.None])
            {
                sprite.Draw(spriteBatch, _.GuiFrame.Rect, Color.White, SpriteEffects.None);
            }
            if (!tooltip.text.IsNullOrEmpty())
            {
                GUIComponent.DrawToolTip(spriteBatch, tooltip.text, tooltip.tooltipPos);
            }
			return false;
		}

	}
    
}
*/