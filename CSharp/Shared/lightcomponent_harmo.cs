using Microsoft.Xna.Framework;
using System;
using Barotrauma.Networking;
using Barotrauma.Extensions;
#if CLIENT
using Microsoft.Xna.Framework.Graphics;
using Barotrauma.Lights;
#endif
using Barotrauma;
using Barotrauma.Items.Components;

using Barotrauma.Extensions;
using Barotrauma;
using HarmonyLib;

namespace LampMod
{
	class CustomLamp : IAssemblyPlugin
	{
		public Harmony harmony;
		private static float defaultFlicker;
		private static float defaultFlickerSpeed;
		private static double randomDouble;
		
		public float Flicker { get; set; }
		public float FlickerSpeed { get; set; }
		
		public void Initialize()
		{
		  harmony = new Harmony("CustomLamp");

		  harmony.Patch(
			original: typeof(LightComponent).GetMethod("Update"),
			postfix: new HarmonyMethod(typeof(CustomLamp).GetMethod("Update"))
		  );
		  
		  
		  
			Random random = new Random();
			defaultFlicker = Flicker;
			defaultFlickerSpeed = FlickerSpeed;
			randomDouble = random.NextDouble();
		}
		public void OnLoadCompleted() { }
		public void PreInitPatching() { }

		public void Dispose()
		{
		  harmony.UnpatchSelf();
		  harmony = null;
		}
		
		
		public static void Update(float deltaTime, Camera cam, LightComponent __instance)
		{
			LightComponent _ = __instance;

			if (_.item.Condition / _.item.MaxCondition < 0.3 &&_.item.HasTag("lamp"))
			{
				_.Flicker = 0.2f + (float)(randomDouble * 0.1f - 0.05f);
				_.FlickerSpeed = 0.3f + (float)(randomDouble * 0.1f - 0.05f);
			}
			else
			{
				_.Flicker = defaultFlicker;
				_.FlickerSpeed = defaultFlickerSpeed;
			}

		}
	}
}
