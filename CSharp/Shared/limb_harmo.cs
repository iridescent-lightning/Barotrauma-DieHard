﻿using FarseerPhysics;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Contacts;
using FarseerPhysics.Dynamics.Joints;
using Microsoft.Xna.Framework;
using Barotrauma.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Networking;
using LimbParams = Barotrauma.RagdollParams.LimbParams;
using JointParams = Barotrauma.RagdollParams.JointParams;
using Barotrauma.Abilities;

using Barotrauma;
using HarmonyLib;


namespace LimbModNamespace//todo make a structural namespace DieHard.Item.Components. namespace can't be used in elsewhere
{
    class LimbMod : IAssemblyPlugin
    {
        public  Harmony harmony;


        public void Initialize()
		{
			harmony = new Harmony("LimbMod");
			
			var original = AccessTools.PropertyGetter(typeof(Limb), "CanBeSeveredAlive");
            var prefix = new HarmonyMethod(typeof(LimbMod).GetMethod(nameof(CanBeSeveredAlivePostfix)));

            harmony.Patch(original, prefix: prefix);
			
				
			}

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }

        public static bool CanBeSeveredAlivePostfix(ref bool __result, Limb __instance)
        {
            
            //DebugConsole.NewMessage("LimbMod CanBeSeveredAlivePostfix");
            
                __result = true;
            return false;
        }
    }
}
