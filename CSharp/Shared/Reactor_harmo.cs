using System;
using System.Reflection;
using System.Collections.Generic;
using Barotrauma;
using Barotrauma.Items.Components;
using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Linq;

namespace BarotraumaDieHard
{
    class ReactorDieHard : IAssemblyPlugin
    {
        public Harmony harmony;
        
        
        public static Dictionary<int, ItemContainer> SecondItemContainerReactors = new Dictionary<int, ItemContainer>();
        
        public void Initialize()
        {
            harmony = new Harmony("ReactorDieHard");

            harmony.Patch(
                original: typeof(Reactor).GetMethod("Update"),
                postfix: new HarmonyMethod(typeof(ReactorDieHard).GetMethod(nameof(UpdatePostfix)))
            );
            harmony.Patch(
                original: typeof(Reactor).GetMethod("OnMapLoaded"),
                postfix: new HarmonyMethod(typeof(ReactorDieHard).GetMethod(nameof(OnMapLoadedPostfix)))
            );
        }

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }

        private static Item coolant;

        public static void UpdatePostfix(float deltaTime, Camera cam, Reactor __instance)
        {

            if (SecondItemContainerReactors.TryGetValue(__instance.item.ID, out ItemContainer itemContainer))
            {
                coolant = itemContainer.Inventory.GetItemAt(0);
            }


            // Check the condition of the coolant and the temperature of the reactor
            if (coolant != null && coolant.Condition <= 0 && __instance.Temperature > 10f)
            {
                __instance.Item.Condition -= 1.5f * deltaTime;
            }
            else if (coolant == null && __instance.Temperature > 10f)
            {
                //DebugConsole.NewMessage(__instance.item.Condition.ToString());
                __instance.Item.Condition -= 1.5f * deltaTime;
            }
            else if (__instance.item.InPlayerSubmarine && __instance.Temperature > 10f)
            {
                coolant.Condition -= 0.05f * deltaTime;
            }

            // Trigger an action if the reactor's condition is critical
            if (__instance.Item.Condition < 2f && __instance.Item.Condition > 0f && __instance.Temperature > 10f)
            {
                Entity.Spawner.AddItemToSpawnQueue(ItemPrefab.GetItemPrefab("reactorcsexplosionhelper"), __instance.Item.WorldPosition);
            }

                
        }


            public static void OnMapLoadedPostfix(Reactor __instance)
            {
                

                    

                
            }

            public static void ClearRactorySecondContainerDictionary()
		    {
			    SecondItemContainerReactors.Clear();
		    }
        
    }
}
