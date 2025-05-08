﻿using Barotrauma.Networking;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using LimbParams = Barotrauma.RagdollParams.LimbParams;
using ColliderParams = Barotrauma.RagdollParams.ColliderParams;

using Barotrauma.Extensions;
using Barotrauma;
using HarmonyLib;
//moved out. no need to change water force
namespace PhysicsBodyMod
{
	class PhysicsBodyMod : IAssemblyPlugin
	{
		public Harmony harmony;
		public static float Mass { get; set; }
		public static float LinearVelocity { get; set; }
		public static float Height { get; set; }
		public static float Radius { get; set; }
		
		public void Initialize()
		{
		  harmony = new Harmony("PhysicsBodyMod");

		  harmony.Patch(
			original: typeof(PhysicsBody).GetMethod("ApplyWaterForces"),
			prefix: new HarmonyMethod(typeof(PhysicsBodyMod).GetMethod("ApplyWaterForces"))
		  );
		  
		  
		  
			
		}
		public void OnLoadCompleted() { }
		public void PreInitPatching() { }

		public void Dispose()
		{
		  harmony.UnpatchSelf();
		  harmony = null;
		}
		
		
		public static bool ApplyWaterForces(PhysicsBody __instance)
		{	
			//buoyancy
			Vector2 buoyancy = new Vector2(0, __instance.Mass * 9.6f);

            Vector2 dragForce = Vector2.Zero;

            float speedSqr = __instance.LinearVelocity.LengthSquared();
            if (speedSqr > 0.00001f)
            {
                //drag
                float speed = (float)Math.Sqrt(speedSqr);
                Vector2 velDir = __instance.LinearVelocity / speed ;

                float vel = speed * 2.0f;
                float drag = vel * vel * Math.Max(Height + Radius * 2, Height);
                dragForce = Math.Min(drag, Mass * 500.0f) * -velDir;
            }

            __instance.ApplyForce(dragForce + buoyancy);
            __instance.ApplyTorque(__instance.FarseerBody.AngularVelocity * __instance.FarseerBody.Mass * -0.08f);
		
			return false;
		}
	}
}