// This class is patched to allow bots to swap weldingtoolequipment from bag to hands in case of fixing leaks. Bots will still fires one shot before switching.
using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using System;
using System.Linq;

using HarmonyLib;
using System.Reflection;
using System.Xml.Linq;

using Barotrauma;


namespace BarotraumaDieHard
{
    class AIObjectiveGoToDieHard  : IAssemblyPlugin
    {


        public Harmony harmony;
        
        
        public void Initialize()
        {
            harmony = new Harmony("AIObjectiveGoToDieHard");

            var originalGoto = typeof(AIObjectiveGoTo).GetMethod("Act", BindingFlags.NonPublic | BindingFlags.Instance);
            var PostfixGoto = typeof(AIObjectiveGoToDieHard).GetMethod(nameof(GotoPostfix), BindingFlags.Public | BindingFlags.Static);

            harmony.Patch(originalGoto, new HarmonyMethod(PostfixGoto), null);


            
            
        }

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }


        public static void GotoPostfix(float deltaTime, AIObjectiveGoTo __instance)
        {
            AIObjectiveGoTo _ = __instance;

            var BagSlotIndex = _.character.Inventory.FindLimbSlot(InvSlotType.Bag);
            var HandSlotIndex = _.character.Inventory.FindLimbSlot(InvSlotType.RightHand);

            Item itemInBag = _.character.Inventory.GetItemAt(BagSlotIndex);
            Item itemInHand = _.character.Inventory.GetItemAt(HandSlotIndex);

            
            if (_.useScooter && itemInBag != null && itemInBag.HasTag("scooter") && !itemInHand.HasTag("scooter"))
            {
                _.character.Inventory.TryPutItem(itemInBag, HandSlotIndex, true, false, Character.Controlled, true, true);
            }
        }
    }
}