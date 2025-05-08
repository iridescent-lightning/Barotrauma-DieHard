using Barotrauma.Items.Components;
using Barotrauma.Networking;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Contacts;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Extensions;
using Barotrauma.MapCreatures.Behavior;
using System.Collections.Immutable;
using Barotrauma.Abilities;

#if CLIENT
using Microsoft.Xna.Framework.Graphics;
#endif

using Barotrauma.Items.Components;
using Barotrauma;
using HarmonyLib;
using System.Reflection;// for bindingflags


namespace ItemModNameSpace//todo make a structural namespace DieHard.Item.Components. namespace can't be used in elsewhere
{
    class ItemMod : IAssemblyPlugin
    {
        public  Harmony harmony;
		

        public void Initialize()
		{
			harmony = new Harmony("ItemMod");
			

            harmony.Patch(
                original: typeof(Item).GetMethod("Update"),
                postfix: new HarmonyMethod(typeof(ItemMod).GetMethod(nameof(UpdatePostfix)))
            );
			
				
			}

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }
        public static void UpdatePostfix(Item __instance, float deltaTime, Camera cam)
        {
            if (__instance.InWater)
            {

                
                Projectile projectile = __instance.GetComponent<Projectile>();
        
                if (projectile != null)
                {
                    DebugConsole.NewMessage("Projectile in water");
                    projectile.LaunchImpulse = 3f;
                }
                
                
            }
        }
        
        
    }
}
