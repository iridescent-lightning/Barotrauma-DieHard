
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

using Barotrauma;
using HarmonyLib;
using System.Globalization;
using System.Reflection;// for bindingflags

using System.IO;

namespace BarotraumaDieHard
{
    partial class ItemDieHard : IAssemblyPlugin
    {
        public Harmony harmony;
		public void Initialize()
{
    harmony = new Harmony("ItemDieHard");
#if CLIENT
    // 更新参数列表，加上最后的 typeof(float?)
    var originalDraw = typeof(Item).GetMethod("Draw", BindingFlags.Public | BindingFlags.Instance, null, 
        new Type[] { typeof(SpriteBatch), typeof(bool), typeof(bool), typeof(Color?), typeof(float?) }, null);

    if (originalDraw == null)
    {
        DebugConsole.ThrowError("[ItemDieHard] 找不到 Item.Draw 方法，请检查游戏版本兼容性。");
        return;
    }

    var postfixDraw = new HarmonyMethod(typeof(ItemDieHard).GetMethod(nameof(Draw), BindingFlags.Public | BindingFlags.Static));
    
    harmony.Patch(originalDraw, null, postfixDraw);
#endif
}

		public void OnLoadCompleted() { }
		public void PreInitPatching() { }

		public void Dispose()
		{
		  harmony.UnpatchSelf();
		  harmony = null;
		}




                
        }


        
}
