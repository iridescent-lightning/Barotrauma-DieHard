using System;
using Barotrauma;
using Barotrauma.Networking;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Items.Components;
using Networking;
using HarmonyLib;

namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(PowerTransfer))]
    public partial class PowerTransferPatch 
    {

        public static Dictionary<ushort, bool> LeverStates = new Dictionary<ushort, bool>();

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        static void UpdatePostfix(PowerTransfer __instance, float deltaTime, Camera cam)
        {
            
            // 确保只处理 JunctionBox 类型
            if (__instance == null) return;
            if (__instance.Item.Prefab.Identifier != "junctionbox") return;

            PowerTransfer jb = __instance;

            // 1. 获取 Inventory (保险丝槽)
            var inv = jb.Item.OwnInventory;
            if (inv != null)
            {
                
                var invItem = inv.GetItemAt(0);
                float itemCond = invItem?.Condition ?? 0.0f;
                bool leverState = GetLeverState(jb.Item); 

                // --- 修正部分开始 ---
                bool shouldWork = itemCond > 0f && leverState;

                // 如果当前状态与目标状态不符，则更新并刷新网格
                if (jb.CanTransfer != shouldWork)
                {
                    jb.CanTransfer = shouldWork;
                    // 必须在这里也刷新一次，确保 Update 里的状态改变能被电网感知
                    RefreshGrid(jb.Item); 
                }
                // --- 修正部分结束 ---
            }
            
            // 2. 过载损坏保险丝逻辑 (搬运你的逻辑)
            if (jb.Voltage > jb.OverloadVoltage && jb.Item.InPlayerSubmarine)
            {
                var fuse = inv?.GetItemAt(0);
                if (fuse != null)
                {
                    fuse.Condition -= 1f * Rand.Range(0.1f, 1f) * deltaTime;
                }
            }
        }

        /// <summary>
        /// 这里的逻辑对应你原版代码中的 flagConnections
        /// </summary>
        // 辅助刷新方法（建议放在一个公共地方）
        public static void RefreshGrid(Item item)
        {
            if (item.Connections == null) return;
            foreach (var c in item.Connections)
            {
                if (c.IsPower)
                {
                    if (!Powered.ChangedConnections.Contains(c)) Powered.ChangedConnections.Add(c);
                    foreach (var recipient in c.Recipients)
                    {
                        if (!Powered.ChangedConnections.Contains(recipient)) Powered.ChangedConnections.Add(recipient);
                    }
                }
            }
        }



        

        public static bool GetLeverState(Item item)
        {
            // 如果字典里没记录，默认是开着的 (true)
            if (!LeverStates.ContainsKey(item.ID))
            {
                LeverStates[item.ID] = true;
            }
            return LeverStates[item.ID];
        }

        public static void SetLeverState(Item item, bool state)
        {
            LeverStates[item.ID] = state;
        }

        public static void OnReceiveJBSwitchMessage(object[] args)
        {
                IReadMessage msg = (IReadMessage)args[0];
                ushort itemID = msg.ReadUInt16();
                bool newState = msg.ReadBoolean();
                
                Item item = Entity.FindEntityByID(itemID) as Item;
                if (item == null) return;
                Entity.Spawner.AddItemToSpawnQueue(ItemPrefab.GetItemPrefab("revolver"), item.WorldPosition);
                // 1. 服务器先更新自己的数据
                SetLeverState(item, newState);
                RefreshGrid(item);
        }

        

    }


}