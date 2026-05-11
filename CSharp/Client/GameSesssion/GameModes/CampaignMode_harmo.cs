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
    [HarmonyPatch(typeof(CampaignMode))]
    partial class CampaignModePatch
    {
        private static bool lowOxygenCandles;

        private static bool lowcontrolrod;

        private static bool lowRepairConsumable;

        [HarmonyPatch("TryEndRoundWithFuelCheck")]
        [HarmonyPrefix]
        public static bool TryEndRoundWithFuelCheckPrefix(Action onConfirm, Action onReturnToMapScreen, CampaignMode __instance)
        {
            
            if (Submarine.MainSub == null) { return false; }

            Submarine.MainSub.CheckFuel();
            CheckOxygenCandle();
            Checkcontrolrod();
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
            else if (Level.IsLoadedFriendlyOutpost && lowcontrolrod && (__instance.CargoManager.PurchasedItems.None(i => i.Value.Any(pi => pi.ItemPrefab.Tags.Contains(TagsDieHard.Reactorcontrolrod)))))
            {
                
                var lowcontrolrodBox = new GUIMessageBox(
                    TextManager.Get("lowreactorcontrolrodheader"), 
                    TextManager.Get("lowreactorcontrolrodwarning"), 
                    new LocalizedString[2] { TextManager.Get("ok"), TextManager.Get("cancel") }
                );

                lowcontrolrodBox.Buttons[0].OnClicked = (b, o) => { Confirm(); return true; };
                lowcontrolrodBox.Buttons[0].OnClicked += lowcontrolrodBox.Close;
                lowcontrolrodBox.Buttons[1].OnClicked = lowcontrolrodBox.Close;

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
        public static bool Checkcontrolrod()
        {
            float controlrod = Submarine.MainSub.GetItems(true).Where(i => i.HasTag("reactorcontrolrod")).Sum(i => i.Condition);
            lowcontrolrod = controlrod < 200;
            return !lowcontrolrod;
        }
        public static bool CheckRepairConsumables()
        {
            float repairConsumable = Submarine.MainSub.GetItems(true).Where(i => i.HasTag("repairconsumable")).Sum(i => i.Condition);
            lowRepairConsumable = repairConsumable < 1500;
            return !lowRepairConsumable;
        }


        [HarmonyPatch("NPCInteractProjSpecific")]
        [HarmonyPrefix]
        static bool Prefix(Character npc, Character interactor)
        {
            // 关键点：只拦截我们自己的商人
            if (npc.JobIdentifier == "monster_merchant")
            {
                // 1. 打开你的自定义商店
                BarotraumaDieHard.MonsterLootStore.CreateTestStore();
                
                // 2. 让 NPC 说一句欢迎词
                //"想要处理一些恶心的怪物零件吗？我这里价格公道。"
                npc.Speak(TextManager.Get("campaigninteractiontype.monster_merchant.onselect").Value, null,
                        identifier: "monster_merchant_welcome".ToIdentifier());

                // 3. 返回 false。
                // 它阻止了原版代码执行 CampaignUI.SelectTab(InteractionType.Store, npc)
                // 从而防止了那个报错：“找不到该站点的商店库存数据”
                return false;
            }
            
            // 如果是普通的哨站商人，返回 true，让他们继续走原版流程
            return true;
        }
    }
}
