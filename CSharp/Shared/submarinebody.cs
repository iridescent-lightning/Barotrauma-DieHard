﻿/*using Barotrauma.Extensions;
using Barotrauma.IO;
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using Voronoi2;



using System.Reflection;
using Barotrauma;
using HarmonyLib;
using Barotrauma.Extensions;
using System.Globalization;
using System.Xml.Linq;
using System.Threading;//Thread.Sleep(100);

namespace SubmarineMod
{
  class SubmarineMod : IAssemblyPlugin
  {
    public Harmony harmony;
	private static float updateTimer = 60f;
	private static float elapsedTime = 57f;
	private static float totalDensity;
	
	
	
    public void Initialize()
    {
      harmony = new Harmony("SubmarineMod");
		
		
      harmony.Patch(
        original: typeof(SubmarineBody).GetMethod("Update"),
        prefix: new HarmonyMethod(typeof(SubmarineMod).GetMethod("Update"))
      );
	  
	  
    }
    public void OnLoadCompleted() { }
    public void PreInitPatching() { }

    public void Dispose()
    {
      harmony.UnpatchSelf();
      harmony = null;
    }
	
	
	//wtf, i added a static then the program works
    public static void Update(float deltaTime, 
    Submarine __instance)
   {
	//DebugConsole.NewMessage("s");
	
	
	
	elapsedTime += deltaTime;
	if (elapsedTime > updateTimer)
	{
		var itemsInSub = __instance.GetItems(true);
		DebugConsole.NewMessage(itemsInSub.ToString());
		foreach (Item item in itemsInSub)
		{
			if (item.body != null)
			{
				totalDensity = totalDensity + item.body.Density;
			}
		}
	elapsedTime = 0.0f;
	}
  }
}
}*/