﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Barotrauma;
using Barotrauma.Items.Components;

using HarmonyLib;
using System.Globalization;
using System.Reflection;


namespace BarotraumaDieHard
{
    // 自定义机械水泵组件
    class MechanicalPump : Powered
    {
        [Editable, Serialize(500.0f, IsPropertySaveable.Yes, description: "每秒最大泵送水量.")]
        public float MaxFlow { get; set; }

        // 当前流量百分比：-100 (全速抽入) 到 100 (全速排出)
        private float flowPercentage;
        [Editable, Serialize(0.0f, IsPropertySaveable.Yes, description: "流量百分比 (-100 到 100). 正数代表从 In 流向 Out.")]
        public float FlowPercentage
        {
            get => flowPercentage;
            set => flowPercentage = MathHelper.Clamp(value, -100.0f, 100.0f);
        }

        private bool isOn = false;
        [Editable, Serialize(false, IsPropertySaveable.Yes, description: "水泵是否开启.")]
        public bool IsOn
        {
            get => isOn;
            set => isOn = value;
        }

        public MechanicalPump(Item item, ContentXElement element) : base(item, element) 
        {
            IsActive = true;
        }

        public override void Update(float deltaTime, Camera cam)
{


    // 1. 基础状态检查：开关、电压、耐久、信号速度
    if (!IsOn || Voltage < MinVoltage || item.Condition <= 0 || Math.Abs(FlowPercentage) < 0.1f) return;

    // 2. 获取物理连接
    var inConn = item.Connections.FirstOrDefault(c => c.Name.Contains("mechanical_in"));
    var outConn = item.Connections.FirstOrDefault(c => c.Name.Contains("mechanical_out"));
    if (inConn == null || outConn == null) return;

    // 3. 确定方向和对应的物品/房间
    // 我们定义：FlowPercentage > 0 时，从 In(源) 抽往 Out(目标)
    Connection sourceConn = FlowPercentage > 0 ? inConn : outConn;
    Connection targetConn = FlowPercentage > 0 ? outConn : inConn;

    // 获取连接的第一个物品（作为喷嘴/水口）
    Item sourceNozzle = sourceConn.Recipients.FirstOrDefault()?.Item;
    Item targetNozzle = targetConn.Recipients.FirstOrDefault()?.Item;

    // 如果没连喷嘴，或者喷嘴没在房间里，无法工作
    if (sourceNozzle?.CurrentHull == null || targetNozzle?.CurrentHull == null) return;
    if (sourceNozzle.CurrentHull == targetNozzle.CurrentHull) return;

    // --- 核心优化检测 ---

    // 1. 检测抽水口是否在水里 (InWater)
    // 如果 FlowPercentage > 0，则检测 mechanical_in 连的那个喷嘴是否在水里
    if (!sourceNozzle.InWater) return;

    // 2. 检测目标房间是否已满
    Hull targetHull = targetNozzle.CurrentHull;
    if (targetHull.WaterVolume >= targetHull.Volume) return;

    // --- 3. 执行泵水 ---
    float powerFactor = Math.Abs(FlowPercentage) / 100.0f;
    float conditionFactor = item.Condition / item.MaxCondition;
    float actualFlow = MaxFlow * powerFactor * conditionFactor * deltaTime;

    // 限制实际流量，防止抽成负数或溢出
    Hull sourceHull = sourceNozzle.CurrentHull;
    actualFlow = Math.Min(actualFlow, sourceHull.WaterVolume);
    actualFlow = Math.Min(actualFlow, targetHull.Volume - targetHull.WaterVolume);

    if (actualFlow > 0)
    {
        sourceHull.WaterVolume -= actualFlow;
        targetHull.WaterVolume += actualFlow;
    }
}

        private Hull GetConnectedHull(Connection conn)
        {
            foreach (Connection connectedConn in conn.Recipients)
            {
                if (connectedConn.Item.CurrentHull != null) return connectedConn.Item.CurrentHull;
            }
            return null;
        }

        public override void ReceiveSignal(Signal signal, Connection connection)
        {
            if (connection.Name == "set_speed")
            {
                
                if (float.TryParse(signal.value, out float val))
                {
                    DebugConsole.NewMessage("rev");
                    FlowPercentage = val;
                }
            }
            else if (connection.Name == "set_state")
            {
                IsOn = signal.value != "0";
            }
        }


        public override float GetCurrentPowerConsumption(Connection connection = null)
        {
            //There shouldn't be other power connections to this
            if (connection != this.powerIn || !IsActive)
            {
                return 0;
            }
            
            CurrPowerConsumption = (IsOn && Math.Abs(FlowPercentage) > 0.1f) ? powerConsumption : 0;
            //pumps consume more power when in a bad condition
            item.GetComponent<Repairable>()?.AdjustPowerConsumption(ref currPowerConsumption);

            return currPowerConsumption;
        }
    }
}