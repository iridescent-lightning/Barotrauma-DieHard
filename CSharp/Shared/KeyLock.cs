﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using System.ComponentModel;
using Networking;

namespace BarotraumaDieHard
{
    class KeyLock : ItemComponent
    {
        private float actionTimer;
        private bool isOperating;
        private Character user;
        private ItemContainer itemContainer;

        [Editable, Serialize("keymediumcab", IsPropertySaveable.Yes, description: "")]
        public string RequiredKey { get; set; }

        [Editable, Serialize("keymaster", IsPropertySaveable.Yes, description: "万能钥匙的Identifier")]
        public string MasterKey { get; set; }

        [Editable, Serialize(1.0f, IsPropertySaveable.Yes, description: "解锁/上锁需要的时间（秒）")]
        public float OperationDuration { get; set; }

        [Editable, Serialize(15f, IsPropertySaveable.Yes, description: "低于这个耐久度，柜子自动坏掉解锁，无法再锁")]
        public float BreakCondition { get; set; }

        [Editable, Serialize(false, IsPropertySaveable.Yes, description: "是否有窗户，没有窗户的柜子上锁后无法查看里面的东西")]
        public bool WindowedCabinet { get; set; }

        public KeyLock(Item item, ContentXElement element)
            : base(item, element)
        {
            IsActive = true;
        }

        public override void OnItemLoaded()
        {
            base.OnItemLoaded();
            // 获取同个物品上的集装箱组件
            itemContainer = item.GetComponent<ItemContainer>();
        }

        // 当玩家按下 E 键或点击柜子时触发
        public override bool Select(Character character)
        {
            if (character == null || itemContainer == null) return false;

            // 如果柜子坏了，直接允许原生交互（如果默认是锁的就解开）
            if (item.Condition <= BreakCondition)
            {
                itemContainer.Locked = false;
                return base.Select(character);
            }

            // 如果已经有人在操作了，不响应其他人
            if (isOperating)
            {
                if (character == user)
                {
                    // 如果已经是正在操作的玩家重复点，不做事，交给 Update 处理
                    return false;
                }
                return false; 
            }

            

            // 手里有钥匙！开始读条
            isOperating = true;
            user = character;
            actionTimer = 0f;

            // 返回 false 以阻断原生的“直接打开柜子集装箱UI”行为
            return false;
        }

        public override void Update(float deltaTime, Camera cam)
        {
            if (!isOperating || user == null || itemContainer == null) return;

            // 中断检查：距离太远、角色倒地死亡、或者松开了钥匙
            if (Vector2.Distance(item.WorldPosition, user.WorldPosition) > 150f || user.IsDead || !HasValidKey(user))
            {
                CancelOperation();
                return;
            }

            actionTimer += deltaTime;

            // 让角色播放使用物品的动作（两手在柜子前搓）
            user.AnimController.UpdateUseItem(false, item.WorldPosition);

#if CLIENT
            // 客户端渲染进度条
            float progressPercentage = Math.Clamp(actionTimer / OperationDuration, 0f, 1f);
            LocalizedString labelText = itemContainer.Locked ? TextManager.Get("progressbar.keyunlocking") : TextManager.Get("progressbar.keylocking");

            user.UpdateHUDProgressBar(
                item.ID,
                item.DrawPosition,
                progressPercentage,
                GUIStyle.Red,
                GUIStyle.Green,
                labelText.Value);
#endif

            // 读条结束
            if (actionTimer >= OperationDuration)
            {
                ToggleLock();
                isOperating = false;
                user = null;
            }
        }

        private bool HasValidKey(Character character)
        {
            if (character == null) return false;
            // 扫描角色当前抓在手里的物品
            return character.HeldItems.Any(heldItem => 
                heldItem.Prefab.Identifier == RequiredKey || heldItem.Prefab.Identifier == MasterKey);
        }

        private void ToggleLock()
        {
            
            // 状态反转：开着就锁，锁着就开
            itemContainer.Locked = !itemContainer.Locked;
            bool isLocked = itemContainer.Locked;
#if CLIENT
            // 成功提示音
            SoundPlayer.PlaySound("guisound_lockCab", item.WorldPosition);

            //SendContainerLockStateMessage(this.item, isLocked);
            
#endif
        }

        private void CancelOperation()
        {
            isOperating = false;
            user = null;
        }


#if CLIENT
        public static void SendContainerLockStateMessage(Item item, bool isLocked)
        {
            
            IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.CONTAINER_LOCK_STATE);
            msg.WriteUInt16(item.ID);
            msg.WriteBoolean(isLocked);

            NetUtil.SendServer(msg, DeliveryMethod.Reliable);
        }
#endif

public static void OnReceiveContainerLockStateMessage(object[] args)
        {
            if (args == null || args.Length == 0) return;

            IReadMessage msg = args[0] as IReadMessage;
            if (msg == null) return;

            // 严格按顺序读取：ItemID -> bool状态
            ushort itemID = msg.ReadUInt16();
            bool isLocked = msg.ReadBoolean();

            // 获取本地对应的物品与组件
            Item item = Entity.FindEntityByID(itemID) as Item;
            if (item == null) return;

            ItemContainer container = item.GetComponent<ItemContainer>();
            if (container == null) return;

            container.Locked = isLocked;
            // 【关键】所有端（服务器和客户端）同步更新字典数据

#if SERVER
            // 如果是在服务器端收到这个数据
            if (GameMain.Server != null)
            {
                

                // 构建广播包，转发给所有人
                IWriteMessage broadcastMsg = NetUtil.CreateNetMsg(NetEvent.CONTAINER_LOCK_STATE);
                broadcastMsg.WriteUInt16(itemID);
                broadcastMsg.WriteBoolean(isLocked);

                // 广播全员
                NetUtil.SendAll(broadcastMsg, DeliveryMethod.Reliable);
            }
#endif
        }

    }
}