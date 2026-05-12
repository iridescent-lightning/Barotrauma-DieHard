using Barotrauma.Networking;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Contacts;
using FarseerPhysics.Dynamics.Joints;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Voronoi2;

using Barotrauma.Items.Components;
using System.Reflection;
using Barotrauma;
using HarmonyLib;




namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(Projectile))]
    public class ProjectilePatch
    {
        #if CLIENT
        // 定义一个委托字典，根据标签执行不同的特效逻辑： 特效管理器
        private static readonly Dictionary<string, Action<Projectile, Item, Vector2>> EffectHandlers = 
            new Dictionary<string, Action<Projectile, Item, Vector2>>
        {
            { "lamp", (p, target, vel) => SpawnLampEffects(target, vel) },
            { "junctionbox", (p, target, vel) => SpawnJunctionBoxEffects(target, vel) }
            //{ "flesh", (p, target, vel) => SpawnBloodEffects(target, vel) } // 假设以后想打碎尸体或肉块
        };
        #endif

        // 让子弹一次飞过只能触发一次判断
        private static Dictionary<Projectile, HashSet<Item>> processedItems = new Dictionary<Projectile, HashSet<Item>>();

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void Update(Projectile __instance, float deltaTime)
        {
            if (!__instance.IsActive || __instance.item.Removed) 
        {
            processedItems.Remove(__instance);
            return;
        }

            // 1. 获取子弹的当前像素坐标
            Vector2 currentWorldPos = __instance.item.WorldPosition;
            
            // 2. 只有在同一个潜艇/空间内才检测 (性能优化)
            var currentSub = __instance.item.Submarine;

            // 3. 遍历所有物体（Item.ItemList 是全局静态列表）
            foreach (Item targetItem in Item.ItemList)
            {
                // 性能过滤：先判断标签
                if (!targetItem.HasTag("damage_by_passing_bullet")) continue;
                
                // 空间过滤：必须在同一个潜艇内
                if (targetItem.Submarine != currentSub) continue;
                
                // 距离过滤：使用 LengthSquared (平方距离) 比 Distance (开平方) 快得多
                // 40像素范围 = 1600
                
                Vector2 diff = targetItem.WorldPosition - currentWorldPos;
                if (diff.LengthSquared() < 1600f) 
                {
                    // 检查逻辑记录，防止单发子弹重复破坏同一个灯
                    if (!processedItems.ContainsKey(__instance)) 
                        processedItems[__instance] = new HashSet<Item>();

                    if (!processedItems[__instance].Contains(targetItem))
                    {
                        // 命中逻辑
                        OnItemPassedBy(__instance, targetItem);
                        processedItems[__instance].Add(targetItem);
                    }
                }
            }
        }
        public static void OnItemPassedBy(Projectile projectile, Item targetItem)
        {
            float fireProb = Rand.Range(0,100f);
            // 获取子弹速度
            Vector2 bulletVelocity = projectile.item.body.LinearVelocity;

            float randomedDamge = Rand.Range(0f, 100f);
            
            // 造成损坏
            if(targetItem.HasTag("container"))
            {
                targetItem.Condition -= 5f;
            }
            else
            {targetItem.Condition -= randomedDamge;}

            
#if CLIENT
            // 自动匹配并执行特效
            foreach (var tag in EffectHandlers.Keys)
            {
                if (targetItem.HasTag(tag))
                {
                    EffectHandlers[tag](projectile, targetItem, bulletVelocity);
                    return;
                    // 如果一个物体只需要一种特效，可以在这里 return;
                }
            }
#endif
        }
#if CLIENT
        // --- 灯具毁灭特效 ---
        private static void SpawnLampEffects(Item target, Vector2 bulletVel)
        {
            Vector2 spawnPos = target.WorldPosition;
            Hull hull = target.CurrentHull;

            if (target.Condition <= 0)
            {
                // 1. 电弧一闪
            GameMain.ParticleManager.CreateParticle("ElectricShock_brokenlamparc", spawnPos, Vector2.Zero, 0, hull);
            }
            
            // 2. 玻璃碎裂 (全向)
            for (int i = 0; i < 12; i++)
            {
                float angle = Rand.Range(0f, MathHelper.TwoPi);
                Vector2 vel = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * Rand.Range(100f, 400f);
                GameMain.ParticleManager.CreateParticle("iceshards_glass", spawnPos, vel, angle, hull, Rand.Range(0.3f, 0.6f));
            }
        }

        // --- 接线盒损坏特效 ---
        private static void SpawnJunctionBoxEffects(Item target, Vector2 bulletVel)
        {
            Hull hull = target.CurrentHull;
            Vector2 spawnPos = target.WorldPosition;
            // 接线盒可能冒烟、喷射大量金属火花，但没有玻璃
            //GameMain.ParticleManager.CreateParticle("blacksmoke", spawnPos, Vector2.UnitY * 20f, 0, target.CurrentHull);
            if (target.Condition <= 0)
            {
            GameMain.ParticleManager.CreateParticle("TeslaExplosion", spawnPos, Vector2.Zero, 0, hull);
            }
            // 定向火花喷射 (使用你之前的向量旋转逻辑)
            SpawnDirectionalSparks(spawnPos, bulletVel, target.CurrentHull);
        }

        // --- 通用的定向火花逻辑 (抽取出来复用) ---
        private static void SpawnDirectionalSparks(Vector2 pos, Vector2 bulletVel, Hull hull)
        {
            Vector2 bulletDir = -Vector2.Normalize(bulletVel);
            for (int i = 0; i < 15; i++)
            {
                float spreadAngle = MathHelper.ToRadians(Rand.Range(-25f, 25f));
                Vector2 sparkDir = new Vector2(
                    bulletDir.X * (float)Math.Cos(spreadAngle) - bulletDir.Y * (float)Math.Sin(spreadAngle),
                    bulletDir.X * (float)Math.Sin(spreadAngle) + bulletDir.Y * (float)Math.Cos(spreadAngle)
                );
                GameMain.ParticleManager.CreateParticle("spark", pos, sparkDir * Rand.Range(300f, 700f), 0, hull);
            }
        }
        
#endif

        // 拦截 Launch 方法，这是子弹获得初速度的瞬间
        [HarmonyPatch("Launch")]
        [HarmonyPrefix]
        public static void LaunchPrefix(Projectile __instance,  Character user)
        {
            if (__instance == null || __instance.item == null) return;

            // 判定条件：如果子弹在水中，或者发射者在水中
            if (__instance.item.InWater || (user != null && user.InWater))
            {
                // 打印调试信息
                // DebugConsole.NewMessage("Water shot detected! Reducing impulse.");

                // 1. 修改发射冲力 (LaunchImpulse)
                // 注意：这会直接影响内部 Launch 逻辑计算出的初速度
                __instance.LaunchImpulse *= 0.7f; 

                // 2. 增加阻力 (Drag)
                // 让子弹在水中不仅初速慢，而且很快停下来
                //__instance.Item.body.Drag *= 2.0f;
            }
        }
    }

//---------------------溅血效果-----------------------------
    [HarmonyPatch(typeof(Projectile), "HandleProjectileCollision")]
    public class ProjectileCollisionPatch
    {
        public static void Postfix(Projectile __instance, Fixture target, Vector2 collisionNormal, Vector2 velocity)
        {
            // 1. 检查是否击中了生物的肢体
            if (target.Body.UserData is Limb targetLimb && targetLimb.character != null)
            {
                Character targetChar = targetLimb.character;

                // 2. 只有在客户端生成粒子特效
    #if CLIENT
                // 2. 检查是否为头部击中
                bool isHeadshot = targetLimb.type == LimbType.Head;
                SpawnBloodEffects(targetLimb, velocity, isHeadshot);
    #endif
            }
        }

    #if CLIENT
    private static void SpawnBloodEffects(Limb limb, Vector2 bulletVel, bool isHeadshot)
    {
        Vector2 spawnPos = limb.WorldPosition;
        Hull hull = limb.character.CurrentHull;
        
        // 基础方向：子弹前进的方向
        Vector2 bulletDir = Vector2.Normalize(bulletVel);

        // --- 效果 A: 常规/顺向喷血 (入口伤/贯穿) ---
        CreateBloodBurst(spawnPos, bulletDir, hull, limb.character, 6, 12, 200f, 500f);

        // --- 效果 B: 爆头特有的反向喷血 (入口飞溅/冲击波) ---
        if (isHeadshot)
        {
            // 反方向：子弹飞来的方向
            Vector2 reverseDir = -bulletDir;
            
            // 数量稍多一些，速度稍微慢一点，模拟向后扩散的浓稠血雾
            CreateBloodBurst(spawnPos, reverseDir, hull, limb.character, 10, 18, 100f, 300f, 1.5f);
            
            // 可选：增加重型电弧缩小版模拟脑电冲击，或者额外的碎片
            GameMain.ParticleManager.CreateParticle("blooddrop", spawnPos, reverseDir * 50f, 0f, hull, 2.0f);
        }
    }

    private static void CreateBloodBurst(Vector2 pos, Vector2 baseDir, Hull hull, Character character, int min, int max, float minSpeed, float maxSpeed, float scaleMult = 1.0f)
    {
        int count = Rand.Range(min, max);
        for (int i = 0; i < count; i++)
        {
            // 扩散角
            float spread = Rand.Range(-0.5f, 0.5f);
            Vector2 finalDir = baseDir + new Vector2(baseDir.Y, -baseDir.X) * spread;

            GameMain.ParticleManager.CreateParticle(
                character.SpeciesName.Contains("human") ? "blood" : "blood",
                pos,
                finalDir * Rand.Range(minSpeed, maxSpeed),
                0f,
                hull,
                Rand.Range(0.5f, 1.2f) * scaleMult
            );
        }
    }
#endif
    }
}
