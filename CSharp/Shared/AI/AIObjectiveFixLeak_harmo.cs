// This class is patched to allow bots to swap weldingtoolequipment from bag to hands in case of fixing leaks.
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
    class AIObjectiveFixLeakDieHard  : IAssemblyPlugin
    {


        public Harmony harmony;
        
        
        public void Initialize()
        {
            harmony = new Harmony("AIObjectiveFixLeakDieHard");

            var originalAct = typeof(AIObjectiveFixLeak).GetMethod("Act", BindingFlags.NonPublic | BindingFlags.Instance);
            var PostfixAct = typeof(AIObjectiveFixLeakDieHard).GetMethod(nameof(ActPostfix), BindingFlags.Public | BindingFlags.Static);

            harmony.Patch(originalAct, new HarmonyMethod(PostfixAct), null);


            
            
        }

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }


        public static void ActPostfix(float deltaTime, AIObjectiveFixLeak __instance)
        {
            AIObjectiveFixLeak _ = __instance;

            var BagSlot = _.character.Inventory.FindLimbSlot(InvSlotType.Bag);
            var rightHandSlot = _.character.Inventory.FindLimbSlot(InvSlotType.RightHand);
            var leftHandSlot = _.character.Inventory.FindLimbSlot(InvSlotType.LeftHand);
            Item itemInBag = _.character.Inventory.GetItemAt(BagSlot);
            Item itemInRightHand = _.character.Inventory.GetItemAt(rightHandSlot);
            Item itemInLeftHand = _.character.Inventory.GetItemAt(leftHandSlot);

            if (itemInBag != null && itemInBag.HasTag("weldingequipment"))
            {
                if ((itemInLeftHand != null && !itemInLeftHand.HasTag("weldingequipment") && (itemInRightHand != null && !itemInRightHand.HasTag("weldingequipment"))))
                {
                    // putting item in the left hand won't work. Bots will fail at unequip item holding at one hand. Making bots put item into the right hand will work.
                    _.character.Unequip(itemInRightHand);
                    _.character.Unequip(itemInLeftHand);
                    //_.character.Inventory.TryPutItem(itemInBag, leftHandSlot, true, false, Character.Controlled, true, true);
                    _.character.Inventory.TryPutItem(itemInBag, rightHandSlot, true, false, Character.Controlled, true, true);
                }
                else if ((itemInRightHand != null && !itemInRightHand.HasTag("weldingequipment")))
                {
                    _.character.Unequip(itemInRightHand);
                    _.character.Inventory.TryPutItem(itemInBag, rightHandSlot, true, false, Character.Controlled, true, true);
                }
                // However, putting item into the left hand here seems to work well.
                else if ((itemInLeftHand != null && !itemInLeftHand.HasTag("weldingequipment")))
                {
                    _.character.Unequip(itemInLeftHand);
                    _.character.Inventory.TryPutItem(itemInBag, leftHandSlot, true, false, Character.Controlled, true, true);
                }
                else if (itemInRightHand == null && itemInLeftHand == null)
                {
                    _.character.Inventory.TryPutItem(itemInBag, rightHandSlot, true, false, Character.Controlled, true, true);
                }
            }
        }
    }
}