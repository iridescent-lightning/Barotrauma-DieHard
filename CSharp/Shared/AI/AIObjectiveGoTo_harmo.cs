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
    [HarmonyPatch(typeof(AIObjectiveGoTo))]
    class AIObjectiveGoToDieHard
    {
        [HarmonyPatch("Act")]
        [HarmonyPostfix]
        public static void ActPostfix(float deltaTime, AIObjectiveGoTo __instance)
        {
            AIObjectiveGoTo _ = __instance;

            var BagSlotIndex = _.character.Inventory.FindLimbSlot(InvSlotType.Bag);
            var HandSlotIndex = _.character.Inventory.FindLimbSlot(InvSlotType.RightHand);

            Item itemInBag = _.character.Inventory.GetItemAt(BagSlotIndex);
            Item itemInHand = _.character.Inventory.GetItemAt(HandSlotIndex);

            
            if (_.useScooter && itemInBag != null && itemInBag.HasTag("scooter"))
            {
                // 检查手里拿的东西：如果手里是空的，或者拿的不是水下推进器
                bool handIsFreeOrNotScooter = itemInHand == null || !itemInHand.HasTag("scooter");

                if (handIsFreeOrNotScooter)
                {
                    // 建议增加一个验证：确保 itemInBag 依然在背包里且未被销毁
                    if (itemInBag.Removed) return;
                            
                    _.character.Inventory.TryPutItem(itemInBag, HandSlotIndex, true, false, Character.Controlled, true, true);
                }
            }
    
        }           
    }
}