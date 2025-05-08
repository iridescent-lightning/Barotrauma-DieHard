﻿using Barotrauma.Items.Components;
using Barotrauma.Extensions;
using Barotrauma.Networking;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using Microsoft.Xna.Framework;
using System;
using System.Linq;


using Barotrauma;
using HarmonyLib;
using System.Reflection;



namespace BarotraumaDieHard
{
    partial class DieHardRope : IAssemblyPlugin
    {
        public Harmony harmony;
		
        

		public void Initialize()
		{
		    harmony = new Harmony("DieHardRope");
#if CLIENT
            var originalDrawRope = typeof(Rope).GetMethod("DrawRope", BindingFlags.NonPublic | BindingFlags.Instance);
            var prefixDrawRope = new HarmonyMethod(typeof(DieHardRope).GetMethod(nameof(DrawRopePrefix), BindingFlags.Public | BindingFlags.Static));
            harmony.Patch(originalDrawRope, prefixDrawRope, null);
#endif

			harmony.Patch(
                original: typeof(Rope).GetMethod("Update"),
                prefix: new HarmonyMethod(typeof(DieHardRope).GetMethod(nameof(Update)))
            );


        }

		public void OnLoadCompleted() { }
		public void PreInitPatching() { }

		public void Dispose()
		{
		  harmony.UnpatchSelf();
		  harmony = null;
		}
		
		public static bool Update(float deltaTime, Camera cam, Rope __instance)
		{
			return true;
		}

		
            

        
	}

}