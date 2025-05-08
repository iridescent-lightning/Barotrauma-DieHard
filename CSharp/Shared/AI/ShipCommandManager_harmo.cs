/*using Barotrauma;
using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

using HarmonyLib;
using System.Reflection;

namespace BarotraumaDieHard
{
    class ShipCommandManagerDieHard : IAssemblyPlugin
    {

        public Harmony harmony;
        
        
        public void Initialize()
        {
            harmony = new Harmony("ShipCommandManagerDieHard");

            var originalTryInitializeShipCommandManager = typeof(ShipCommandManager).GetMethod("TryInitializeShipCommandManager", BindingFlags.NonPublic | BindingFlags.Instance);
            var PostfixTryInitializeShipCommandManager = typeof(ShipCommandManagerDieHard).GetMethod(nameof(TryInitializeShipCommandManagerPostfix), BindingFlags.Public | BindingFlags.Static);

            harmony.Patch(originalTryInitializeShipCommandManager, new HarmonyMethod(PostfixTryInitializeShipCommandManager), null);



            var originalUpdateCommandDecision = typeof(ShipCommandManager).GetMethod("UpdateCommandDecision", BindingFlags.NonPublic | BindingFlags.Instance);
            var PostfixUpdateCommandDecision = typeof(ShipCommandManagerDieHard).GetMethod(nameof(UpdateCommandDecision), BindingFlags.Public | BindingFlags.Static);

            harmony.Patch(originalUpdateCommandDecision, new HarmonyMethod(PostfixUpdateCommandDecision), null);
            
            var originalUpdate = typeof(ShipCommandManager).GetMethod("Update", BindingFlags.Public | BindingFlags.Instance);
            var PostfixUpdate = typeof(ShipCommandManagerDieHard).GetMethod(nameof(Update), BindingFlags.Public | BindingFlags.Static);

            harmony.Patch(originalUpdate, new HarmonyMethod(PostfixUpdate), null);
        }

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }


        public static void TryInitializeShipCommandManagerPostfix(ShipCommandManager __instance)
        {
            ShipCommandManager _ = __instance;
            if (_.CommandedSubmarine.GetItems(false).Find(i => i.HasTag("sonar") && !i.NonInteractable) is Item sonarItem && sonarItem.GetComponent<Sonar>() is Sonar sonarComponent)
            {
                var order = new Order(OrderPrefab.Prefabs["operatesonar"], "controlsonar".ToIdentifier(), sonarItem, sonarComponent);
                _.ShipIssueWorkers.Add(new ShipIssueWorkerControlSonar(_, order, sonarComponent));
            }
            
            
        }


        public static void UpdateCommandDecision(float timeSinceLastUpdate)
        {
            DebugConsole.NewMessage("s");
        }

        public static void Update(float deltaTime)
        {
            DebugConsole.NewMessage("s");
        }

    }
}
*/