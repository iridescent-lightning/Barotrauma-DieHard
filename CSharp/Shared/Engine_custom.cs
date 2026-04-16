using Microsoft.Xna.Framework;
using System;
using System.Globalization;
using System.Xml.Linq;
using Barotrauma.Networking;

using Barotrauma.Items.Components;
using System.Reflection;
using Barotrauma;
using Networking;

namespace BarotraumaDieHard
{
    partial class CustomEngine : Engine
    {
        public bool hasVerticalThruster;
        public bool uiCreated = false;
        private float targetVerticalForce = 0; // 滚动条的目标值

        private float? lastReceivedTargetVerticalForce;
        public float verticalForce;       // 实际当前的力（平滑插值用）



        public float maintainDepthTarget;// 目标深度

        public bool isMaintainDepthEnabled { get; set; } // 开关

        private float lastDepthError; // 用于计算微分（防止过度晃动）

        [Editable, Serialize(false, IsPropertySaveable.Yes)]
        public bool VectoredThruster
        {
            get {return hasVerticalThruster; }
            set { hasVerticalThruster = value; }
        }
        public CustomEngine(Item item, ContentXElement element) : base(item, element) 
        {

            InitProjSpecific(element);
            


            #if SERVER
            NetUtil.Register(NetEvent.VECTORED_ENGINE_FORCECHANGE, OnReceiveVerticalForceMessage);
            NetUtil.Register(NetEvent.VECTORED_ENGINE_MAINTAINDEPTH, OnReceiveMaintainDepthMessage);
            #endif
        }

        partial void InitProjSpecific(ContentXElement element);

        public override void Update(float deltaTime, Camera cam)
        {
            base.Update(deltaTime, cam); // 这一行恢复了原本引擎的所有逻辑
            //DebugConsole.NewMessage($"{this.VectoredThruster}");
            #if CLIENT
            // 如果 UI 容器准备好了但还没构建我们的自定义部分
            if (!uiCreated && GuiFrame != null)
            {
                CreateGUI();
                uiCreated = true;
            }
            #endif

            // --- 自动深度保持逻辑 ---
            if (isMaintainDepthEnabled && item.Submarine != null && hasVerticalThruster)
            {
                // 1. 计算深度偏差 (Barotrauma 中 Y 轴正方向是向上，所以深度通常是负值)
                // 注意：潜艇坐标系和世界坐标系的转换
                float currentY = item.Submarine.WorldPosition.Y;
                float error = maintainDepthTarget - currentY;

                // 2. 简单的比例控制 (P) + 阻尼 (针对速度的补偿)
                // 这里的 0.5f 是感应灵敏度，-0.1f 是为了抵消惯性
                float controlOutput = (error * 0.1f) + (item.Submarine.Velocity.Y * -5.0f);

                // 3. 将输出映射到引擎的力范围 (-100 到 100)
                targetVerticalForce = MathHelper.Clamp(controlOutput, -100.0f, 100.0f);
                
                // 自动模式下不需要 user 手动输入
                User = null; 
            }
            if (lastReceivedTargetVerticalForce.HasValue)
                {
                    targetVerticalForce = lastReceivedTargetVerticalForce.Value;
                }

            //垂直功能部分
            verticalForce = MathHelper.Lerp(verticalForce, (Voltage < MinVoltage) ? 0.0f : targetVerticalForce, deltaTime * 10.0f);
            if (Math.Abs(verticalForce) > 1.0f)
            {
                
                
                float condition = item.MaxCondition <= 0.0f ? 0.0f : item.Condition / item.MaxCondition;
                float voltageFactor = MinVoltage <= 0.0f ? 1.0f : Math.Min(Voltage, MaxOverVoltageFactor);
                float currVerticalForce = verticalForce * MathF.Pow(voltageFactor, PowerToForceExponent);
                // Broken engine makes more noise.
                float noise = Math.Abs(currVerticalForce) * MathHelper.Lerp(1.5f, 1f, condition);
                UpdateAITargets(noise);
                //arbitrary multiplier that was added to changes in submarine mass without having to readjust all engines
                float verticalforceMultiplier = 0.1f;
                if (User != null)
                {
                    verticalforceMultiplier *= MathHelper.Lerp(0.5f, 2.0f, (float)Math.Sqrt(User.GetSkillLevel(Tags.HelmSkill) / 100));
                }
                currVerticalForce *= item.StatManager.GetAdjustedValueMultiplicative(ItemTalentStats.EngineMaxSpeed, MaxForce) * verticalforceMultiplier;

                currVerticalForce *= item.StatManager.GetAdjustedValueMultiplicative(ItemTalentStats.EngineMaxSpeed, MaxForce) * 0.1f; // 复用系数
                currVerticalForce *= MathHelper.Lerp(0.5f, 2.0f, condition);

                // 只提供垂直動力 *0.25 to make the vertical force less powerful.
                Vector2 forceVector = new Vector2(0, currVerticalForce)*0.25f;

                // 4. 应用力
                item.Submarine.ApplyForce(forceVector * deltaTime * Timing.FixedUpdateRate);

                // ... 螺旋桨损伤和粒子效果 ...
#if CLIENT
                float particleInterval = 1.0f / particlesPerSec;
                particleTimer += deltaTime;
                while (particleTimer > particleInterval)
                {
                    Vector2 particleVel = -forceVector.ClampLength(5000.0f) / 5.0f;
                    GameMain.ParticleManager.CreateParticle("bubbles", item.WorldPosition + PropellerPos * item.Scale,
                        particleVel * Rand.Range(0.8f, 1.1f),
                        0.0f, item.CurrentHull);
                    particleTimer -= particleInterval;
                }
#endif
                
            }
        }

        private void OnReceiveVerticalForceMessage(object[] args)
        {
            IReadMessage msg = (IReadMessage)args[0];
            
            // 1. 严格按照发送顺序读取
            ushort itemId = msg.ReadUInt16();
            float receivedVerticalForce = msg.ReadSingle();

            // 2. 查找对应的物品
            Item engineItem = Entity.FindEntityByID(itemId) as Item;
            if (engineItem != null)
            {
                // 3. 获取你的自定义引擎组件
                var customEngine = engineItem.GetComponent<CustomEngine>();
                if (customEngine != null)
                {
                    // 更新目标垂直力
                    customEngine.targetVerticalForce = receivedVerticalForce;

                }
            }
        }

        private void OnReceiveMaintainDepthMessage(object[] args)
{
    IReadMessage msg = (IReadMessage)args[0];
    
    ushort itemId = msg.ReadUInt16();
    bool receivedEnabled = msg.ReadBoolean();
    float receivedDepth = msg.ReadSingle();

    Item engineItem = Entity.FindEntityByID(itemId) as Item;
    if (engineItem != null)
    {
        var customEngine = engineItem.GetComponent<CustomEngine>();
        if (customEngine != null)
        {
            // 只需要更新这两个控制变量
            customEngine.isMaintainDepthEnabled = receivedEnabled;
            customEngine.maintainDepthTarget = receivedDepth;

            // 额外处理：如果关闭了功能，顺便把目标力清空，防止残余推力
            if (!receivedEnabled)
            {
                customEngine.targetVerticalForce = 0.0f;
            }
        }
    }
}
    
    }
}
