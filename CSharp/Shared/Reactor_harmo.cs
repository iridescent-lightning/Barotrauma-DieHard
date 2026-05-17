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
    [HarmonyPatch(typeof(Reactor))]
    class ReactorDieHard
    {
        
        
        public static Dictionary<int, ItemContainer> SecondItemContainerReactors = new Dictionary<int, ItemContainer>();
        

        private static Item controlrod;
        private static bool inEditor;

        [HarmonyPatch("Update")]
        [HarmonyPostfix]

        public static void UpdatePostfix(float deltaTime, Camera cam, Reactor __instance)
        {
            
            //if(inEditor && !__instance.Item.InPlayerSubmarine) return;
            if (SecondItemContainerReactors.TryGetValue(__instance.item.ID, out ItemContainer itemContainer))
            {
                controlrod = itemContainer.Inventory.GetItemAt(0);
            }

            //DebugConsole.NewMessage(__instance.TargetTurbineOutput.ToString());

            

            // Check the condition of the controlrod and the temperature of the reactor
            if (__instance.TargetTurbineOutput > 0f)
            {
                if (controlrod != null)
                {
                    if (controlrod.Condition <= 0)
                    {
                        // 控制棒耗尽，反应堆失控
                        __instance.fissionRate = 100f;
                        __instance.Item.Condition -= 1.5f * deltaTime;
                    }
                    else if (__instance.item.InPlayerSubmarine)
                    {
                        // 正常消耗控制棒
                        controlrod.Condition -= 0.05f * deltaTime;
                    }
                }
                else if (__instance.TargetTurbineOutput > 0f)
                {
                    // 没有控制棒且温度过高
                    __instance.fissionRate = 100f;
                    __instance.Item.Condition -= 1.5f * deltaTime;
                }
            }

            // Trigger an action if the reactor's condition is critical
            if (__instance.Item.Condition < 2f && __instance.Item.Condition > 0f && __instance.Temperature > 10f)
            {
                Entity.Spawner.AddItemToSpawnQueue(ItemPrefab.GetItemPrefab("reactorcsexplosionhelper"), __instance.Item.WorldPosition);
            }

                
        }

        [HarmonyPatch("OnMapLoaded")]
        [HarmonyPostfix]
        public static void OnMapLoadedPostfix(Reactor __instance)
        {
            
            foreach (Submarine sub in Submarine.Loaded)
                        {
                            if (sub?.Info?.OutpostGenerationParams != null)
                            {
                                inEditor = true;
                                
                            }
                        }
        }

            public static void ClearRactorySecondContainerDictionary()
		    {
			    SecondItemContainerReactors.Clear();
		    }
        
    }
}
