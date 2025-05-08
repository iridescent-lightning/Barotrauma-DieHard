using Barotrauma.Extensions;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


using Barotrauma;


using System.Reflection;
using HarmonyLib;
using Barotrauma.Items.Components;


namespace BarotraumaDieHard
{
    class CampaignModeDieHard : IAssemblyPlugin
    {
        public Harmony harmony;
        
        public void Initialize()
        {
            harmony = new Harmony("CampaignModeDieHard");


            var originalTryEndRoundWithFuelCheck = typeof(CampaignMode).GetMethod("TryEndRoundWithFuelCheck", BindingFlags.NonPublic | BindingFlags.Instance);
            var prefixTryEndRoundWithFuelCheck = new HarmonyMethod(typeof(CampaignModeDieHard).GetMethod(nameof(TryEndRoundWithFuelCheckPrefix), BindingFlags.Public | BindingFlags.Static));
            harmony.Patch(originalTryEndRoundWithFuelCheck, prefixTryEndRoundWithFuelCheck, null);
        }

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }

        private static bool lowOxygenCandles;

        private static bool lowCoolant;

        private static bool lowRepairConsumable;

        public static bool TryEndRoundWithFuelCheckPrefix(Action onConfirm, Action onReturnToMapScreen, CampaignMode __instance)
        {
            
            if (Submarine.MainSub == null) { return false; }

            Submarine.MainSub.CheckFuel();
            CheckOxygenCandle();
            CheckCoolant();
            CheckRepairConsumables();
            bool lowFuel = Submarine.MainSub.Info.LowFuel;
            if (__instance.PendingSubmarineSwitch != null)
            {
                lowFuel = __instance.TransferItemsOnSubSwitch ? (lowFuel && __instance.PendingSubmarineSwitch.LowFuel) : __instance.PendingSubmarineSwitch.LowFuel;

            }
            // Check if oxygen candles are low and show the dialog.
            if (Level.IsLoadedFriendlyOutpost && lowOxygenCandles && (__instance.CargoManager.PurchasedItems.None(i => i.Value.Any(pi => pi.ItemPrefab.Tags.Contains(TagsDieHard.OxygenGeneratorCandle)))))
            {
                
                var lowOxygenCandleBox = new GUIMessageBox(
                    TextManager.Get("lowoxygencandleheader"), 
                    TextManager.Get("lowoxygencandlewarning"), 
                    new LocalizedString[2] { TextManager.Get("ok"), TextManager.Get("cancel") }
                );

                lowOxygenCandleBox.Buttons[0].OnClicked = (b, o) => { Confirm(); return true; };
                lowOxygenCandleBox.Buttons[0].OnClicked += lowOxygenCandleBox.Close;
                lowOxygenCandleBox.Buttons[1].OnClicked = lowOxygenCandleBox.Close;

            }
            else if (Level.IsLoadedFriendlyOutpost && lowCoolant && (__instance.CargoManager.PurchasedItems.None(i => i.Value.Any(pi => pi.ItemPrefab.Tags.Contains(TagsDieHard.ReactorCoolant)))))
            {
                
                var lowCoolantBox = new GUIMessageBox(
                    TextManager.Get("lowreactorcoolantheader"), 
                    TextManager.Get("lowreactorcoolantwarning"), 
                    new LocalizedString[2] { TextManager.Get("ok"), TextManager.Get("cancel") }
                );

                lowCoolantBox.Buttons[0].OnClicked = (b, o) => { Confirm(); return true; };
                lowCoolantBox.Buttons[0].OnClicked += lowCoolantBox.Close;
                lowCoolantBox.Buttons[1].OnClicked = lowCoolantBox.Close;

            }
            else if (Level.IsLoadedFriendlyOutpost && lowRepairConsumable && (__instance.CargoManager.PurchasedItems.None(i => i.Value.Any(pi => pi.ItemPrefab.Tags.Contains(TagsDieHard.RepairConsumable)))))
            {
                
                var lowRepairConsumableBox = new GUIMessageBox(
                    TextManager.Get("lowrepairconsumableheader"), 
                    TextManager.Get("lowrepairconsumablewarning"), 
                    new LocalizedString[2] { TextManager.Get("ok"), TextManager.Get("cancel") }
                );

                lowRepairConsumableBox.Buttons[0].OnClicked = (b, o) => { Confirm(); return true; };
                lowRepairConsumableBox.Buttons[0].OnClicked += lowRepairConsumableBox.Close;
                lowRepairConsumableBox.Buttons[1].OnClicked = lowRepairConsumableBox.Close;

            }
            else if (Level.IsLoadedFriendlyOutpost && lowFuel && (__instance.CargoManager.PurchasedItems.None(i => i.Value.Any(pi => pi.ItemPrefab.Tags.Contains(Tags.ReactorFuel)))))
            {
                var extraConfirmationBox =
                    new GUIMessageBox(TextManager.Get("lowfuelheader"),
                    TextManager.Get("lowfuelwarning"),
                    new LocalizedString[2] { TextManager.Get("ok"), TextManager.Get("cancel") });
                extraConfirmationBox.Buttons[0].OnClicked = (b, o) => { Confirm(); return true; };
                extraConfirmationBox.Buttons[0].OnClicked += extraConfirmationBox.Close;
                extraConfirmationBox.Buttons[1].OnClicked = extraConfirmationBox.Close;
            }
            else
            {
                Confirm();
            }

            void Confirm()
            {
                var availableTransition = __instance.GetAvailableTransition(out _, out _);
                if (Character.Controlled != null &&
                    availableTransition == CampaignMode.TransitionType.ReturnToPreviousLocation &&
                    Character.Controlled?.Submarine == Level.Loaded?.StartOutpost)
                {
                    onConfirm();
                }
                else if (Character.Controlled != null &&
                    availableTransition == CampaignMode.TransitionType.ProgressToNextLocation &&
                    Character.Controlled?.Submarine == Level.Loaded?.EndOutpost)
                {
                    onConfirm();
                }
                else
                {
                    onReturnToMapScreen();
                }
            }

            return false;
        }


        public static bool CheckOxygenCandle()
        {
            float oxygenCandle = Submarine.MainSub.GetItems(true).Where(i => i.HasTag("oxygencandle")).Sum(i => i.Condition);
            lowOxygenCandles = oxygenCandle < 200;
            return !lowOxygenCandles;
        }
        public static bool CheckCoolant()
        {
            float coolant = Submarine.MainSub.GetItems(true).Where(i => i.HasTag("reactorcoolant")).Sum(i => i.Condition);
            lowCoolant = coolant < 200;
            return !lowCoolant;
        }
        public static bool CheckRepairConsumables()
        {
            float repairConsumable = Submarine.MainSub.GetItems(true).Where(i => i.HasTag("repairconsumable")).Sum(i => i.Condition);
            lowRepairConsumable = repairConsumable < 1500;
            return !lowRepairConsumable;
        }
    }
}
