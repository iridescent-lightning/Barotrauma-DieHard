﻿using Microsoft.Xna.Framework;
using Barotrauma;
using Barotrauma.Items.Components;
using System;

namespace BarotraumaDieHard
{
    class ShootingRangeTarget : ItemComponent
    {
        // XML 可调参数
        [Serialize(2.0f, IsPropertySaveable.Yes, description: "移动速度倍率")]
        public float MoveSpeed { get; set; }

        [Serialize(50.0f, IsPropertySaveable.Yes, description: "移动的上下幅度 (像素)")]
        public float MoveRange { get; set; }

        [Serialize(30.0f, IsPropertySaveable.Yes, description: "停止移动并倒下的血量阈值")]
        public float BreakHealth { get; set; }

        private Vector2 initialPhysicsPos;
        private float timer;
        private bool isBroken = false;
		private Random rand = new Random();

        public ShootingRangeTarget(Item item, ContentXElement element) : base(item, element)
        {
            // 允许每帧调用 Update 方法
            IsActive = true; 
        }

        public override void OnItemLoaded()
        {
            base.OnItemLoaded();

			// --- 核心修改：让每个靶子的节奏错落开 ---
            // 给 timer 赋予一个 0 到 2π (一个完整正弦周期) 之间的随机初始值
            // 这样哪怕它们同时生成，它们启动时的位置和方向也完全不同
            timer = (float)(rand.NextDouble() * MathHelper.TwoPi);

            // 记录初始的物理位置（注意：Barotrauma 物理坐标是米，需要转换）
            if (item.body != null)
            {
                initialPhysicsPos = item.body.SimPosition;
                // 确保它是运动学刚体(Kinematic)，否则无法用速度控制其无视重力移动
                item.body.BodyType = FarseerPhysics.BodyType.Kinematic;

				float currentScale = item.Scale;

                // 获取原本的物理宽和高（单位：米）
                float originalWidth = item.body.Width;
                float originalHeight = item.body.Height;

                // 动态重新设置物理体的大小
                // 注意：在 Farseer 物理引擎中，改变宽高需要使用此方法重新生成形状
                //item.body.SetScale(currentScale, currentScale);
            }
        }

        public override void Update(float deltaTime, Camera cam)
        {
            if (item.body == null) return;

            // 检查当前物品血量 (Condition)
            if (item.Condition <= BreakHealth)
            {
                if (!isBroken)
                {
                    HandleTargetBreak();
                }
                return; // 坏了就停止执行移动逻辑
            }

            // --- 正常规律移动逻辑 (正弦波 Sine) ---
            timer += deltaTime * MoveSpeed;
            
            // 计算垂直位移速度 (对位移求导得到速度，能让物理移动更平滑)
            float maxRangeInMeters = MoveRange / 100f; // 像素转米
            float velocityY = (float)Math.Cos(timer) * maxRangeInMeters * MoveSpeed;

            // 设置 Body 的线速度，这样贴图和物理会完美同步移动
            item.body.LinearVelocity = new Vector2(0, velocityY);
            
            // 保证角度摆正
            item.body.TargetRotation = 0f;
        }

        private void HandleTargetBreak()
        {
            //isBroken = true;
            
            // 1. 停止所有物理速度
            item.body.LinearVelocity = Vector2.Zero;

            // 2. 移除碰撞体：将 Body 改为 Static 并禁用碰撞
            // 或者直接让 Body 变成不可碰撞的 Sensor
            item.body.CollisionCategories = FarseerPhysics.Dynamics.Category.None;
            item.body.CollidesWith = FarseerPhysics.Dynamics.Category.None;
            item.body.Enabled = false; // 完全禁用物理体（玩家和子弹将穿透）

            // 3. 图片歪倒：修改物体的渲染旋转角度（弧度制，例如歪倒 75 度）
            float fallAngle = MathHelper.ToRadians(75f);
            item.Rotation = fallAngle;

            // 如果有必要，微调一下歪倒后的贴图偏移，防止悬空
            //item.Sprite.Offset = new Vector2(0, -10); 
        }
    }
}