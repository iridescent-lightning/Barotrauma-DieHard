﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Barotrauma;
using System.Xml.Linq;
using Barotrauma.Items.Components;
using Networking;
using Barotrauma.Networking;

namespace BarotraumaDieHard
{
    public partial class MonsterLootStore
    {
        public static void OnReceiveSellItemMessage(object[] args)
        {
            IReadMessage msg = (IReadMessage)args[0];

            ushort itemId = msg.ReadUInt16();


            Item itemToSell = Entity.FindEntityByID(itemId) as Item;

            // 服务器端的安全检查：确保物品存在且属于发送请求的玩家（或在玩家附近）
            if (itemToSell != null)
            {
                // 1. 服务器执行移除，这会向所有客户端广播同步消息
                Entity.Spawner.AddEntityToRemoveQueue(itemToSell);
            }
        }

    }

    
}