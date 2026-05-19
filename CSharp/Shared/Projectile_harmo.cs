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
                   // __instance.item.body.LinearVelocity *= 0.2f;
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

            //被orz的ccd取代
            /*Vector2 currentPos = __instance.item.SimPosition; // 使用物理世界坐标 (SimPosition) 更精准
            Vector2 velocity = __instance.item.body.LinearVelocity;
            // 计算这一帧子弹预计移动的位移向量
            Vector2 movementThisFrame = velocity * deltaTime;
            float moveLength = movementThisFrame.Length() / 2f;

            if (moveLength < 0.01f) return;

            // 射线终点：当前位置 + 这一帧的位移（可以稍微乘个 1.2 做宽限预算）
            Vector2 nextPos = currentPos + movementThisFrame;


            // 在 Farseer 物理世界中拉一条射线检测
            object hitTarget = null;
            Limb hitLimb = null;
            Item hitItem = null;
            Fixture closestFixture = null;
            float closestFraction = 1f; // 1 代表射线的终点，我们要找最接近 0 (起点) 的碰撞

            GameMain.World.RayCast((fixture, point, normal, fraction) =>
            {
                // 性能过滤：如果已经找到了更近的碰撞体，直接无视后面更远的
                if (fraction >= closestFraction) return -1;

                // 情况 A：扫到了盾牌、墙壁、或者其他具有物理结构的 Item 刚体
                if (fixture.Body.UserData is Item targetItem)
                {
                    // 过滤自伤：子弹不能在刚发射时用射线把自己或者枪给打掉
                    if (targetItem == __instance.item || targetItem == __instance.Launcher) return -1;
                    
                    // 过滤：只有能阻挡或能与子弹碰撞的 Item 才具备拦截能力
                    if (fixture.IsSensor) return -1;

                    closestFraction = fraction;
                    closestFixture = fixture;
                    hitItem = targetItem;
                    hitLimb = null; // 刷新最近目标为物体
                    return fraction;
                }


                // 检查：撞到了角色的肢体 (防弹衣挂在这里面)
                if (fixture.Body.UserData is Limb limb && limb.character != null)
                {
                    if (limb.character.IsDead) return -1;

                    closestFraction = fraction;
                    closestFixture = fixture;
                    hitLimb = limb;
                    hitItem = null; // 清空 Item
                    return fraction;
                }
                

                // 情况 C：结构性墙壁、潜艇外壳 (Structure)
                if (fixture.Body.UserData is Structure)
                {
                    closestFraction = fraction;
                    closestFixture = fixture;
                    hitItem = null;
                    hitLimb = null;
                    return fraction;
                }

                return -1; // 继续检测
            }, currentPos, nextPos);

            // 路由 1：最先撞到的是物体 (如手持防爆盾)
            if (hitItem != null && closestFixture != null)
            {
                // 【核心修复】：检查 Item 的 XML 配置中是否带有顶层的偏转属性
                // 通过 targetItem.Prefab.ConfigElement 或者直接通过属性读取
                bool hasItemDeflect = false;
                
                // 检查物品预制件(Prefab)的顶层 XML 元素中是否包含 deflectprojectiles 属性
                var deflectAttr = hitItem.Prefab?.ConfigElement?.GetAttribute("deflectprojectiles");
                if (deflectAttr != null && bool.TryParse(deflectAttr.Value, out bool deflectVal))
                {
                    hasItemDeflect = deflectVal;
                }

                // 如果这就是手持防爆盾等配置了绝对偏转的道具
                if (hasItemDeflect)
                {
                    var hitsField = typeof(Projectile).GetField("hits", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var hitsHashSet = hitsField?.GetValue(__instance) as HashSet<Body>;

                    if (hitsHashSet != null && !hitsHashSet.Contains(closestFixture.Body))
                    {
                        DebugConsole.NewMessage($"[DieHard] 射线盾牌格挡：子弹撞击防爆盾 [{hitItem.Name}] 成功！");
                        
                        hitsHashSet.Add(closestFixture.Body);
                        Vector2 normal = velocity.LengthSquared() > 0.01f ? -Vector2.Normalize(velocity) : Vector2.UnitY;
                        
                        // 1. 调用原版处理（用于扣盾牌耐久、放金属撞击音效）
                        __instance.HandleProjectileCollision(closestFixture, normal, velocity);

                        // 2. 盾牌是绝对格挡，当场扣下子弹，剥夺后续任何高频判定和穿透能力
                        var disableMethod = typeof(Projectile).GetMethod("DisableProjectileCollisions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        disableMethod?.Invoke(__instance, null);

                        if (__instance.item?.body != null)
                        {
                            __instance.item.body.LinearVelocity = Vector2.Zero;
                        }
                        if (__instance.RemoveOnHit)
                        {
                            __instance.IsActive = false;
                        }
                    }
                    return; // 撞到盾牌，绝对不能过穿到后方的人
                }

                // 如果撞到的是普通的、没有偏转属性的普通Item（比如地上的扳手、箱子），放回给原版逻辑处理，不需要强制截断
                return;
            }

            // 场景 2：如果最先撞到的是潜艇墙壁
            if (closestFixture != null && hitLimb == null && hitItem == null)
            {
                // 撞墙了，把控制权还给原版物理，让子弹打在墙上
                Vector2 collisionNormal = velocity.LengthSquared() > 0.01f ? -Vector2.Normalize(velocity) : Vector2.UnitY;
                __instance.HandleProjectileCollision(closestFixture, collisionNormal, velocity);
                return;
            }

            // 路由 3：【核心重构区】射线扫到了肢体 (手持盾牌拦截的真正战场)
            if (hitLimb != null && hitLimb.character != null && closestFixture != null)
            {
                var hitsField = typeof(Projectile).GetField("hits", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var hitsHashSet = hitsField?.GetValue(__instance) as HashSet<Body>;

                // 去重锁：避免同一次物理碰撞重复判定
                if (hitsHashSet != null && hitsHashSet.Contains(hitLimb.body.FarseerBody)) return;

                // --- 逆向嗅探：检查该角色手上是否持有防爆盾 ---
                bool shieldItem = false;
                Character targetCharacter = hitLimb.character;

                // 遍历角色全身上下装备的所有物品（包括手里拿的、口袋里揣的、衣服穿的）
                foreach (Item heldItem in targetCharacter.Inventory.AllItems)
                {
                    // 检查物品预制件(Prefab)的顶层 XML 元素中是否包含 deflectprojectiles="True"
                    var deflectAttr = heldItem.Prefab?.ConfigElement?.GetAttribute("deflectprojectiles");
                    if (deflectAttr != null && bool.TryParse(deflectAttr.Value, out bool deflectVal) && deflectVal)
                    {
                        shieldItem = true;
                    }
                }

                // 【盾牌格挡触发】：如果玩家确实手里举着盾牌
                if (shieldItem)
                {
                    DebugConsole.NewMessage($"[DieHard] 物理检测盾牌：[{targetCharacter.Name}] 手持 [{shieldItem}] 成功拦截了子弹！");
                    
                    // 注意：因为手持盾牌没有物理 Body，这里必须把防住子弹的【肢体刚体】或【子弹本身】加入去重列表，锁死伤害
                    hitsHashSet?.Add(hitLimb.body.FarseerBody);
                    return; // 成功拦截，安全退出，绝不过穿
                }

                // --- 裸肉或防弹衣分支：如果没有手持盾牌拦截，执行正常的原版肉体受击与防弹背心结算 ---
                DebugConsole.NewMessage($"[DieHard] 射线挽救肉体命中：目标 [{targetCharacter.Name}]，部位 [{hitLimb.type}]。防弹衣将自适应解析。");
                
                hitsHashSet?.Add(hitLimb.body.FarseerBody);
                Vector2 normalVec = velocity.LengthSquared() > 0.01f ? -Vector2.Normalize(velocity) : Vector2.UnitY;
                
                __instance.HandleProjectileCollision(closestFixture, normalVec, velocity);
                
                if (hitsHashSet != null && hitsHashSet.Count >= __instance.MaxTargetsToHit)
                {
                    var disableMethod = typeof(Projectile).GetMethod("DisableProjectileCollisions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    disableMethod?.Invoke(__instance, null);
                    if (__instance.RemoveOnHit) __instance.IsActive = false;
                }
            }*/
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
            if (__instance == null || __instance.item == null || user == null) return;
            
            Item itemInRightHand = user.Inventory?.GetItemInLimbSlot(InvSlotType.RightHand);

            if (itemInRightHand != null && itemInRightHand.Prefab.Identifier == "underwatergun") return;
            // 判定条件：如果子弹在水中，或者发射者在水中
            if (__instance.item.InWater || (user != null && user.InWater))
            {
                // 打印调试信息
                // DebugConsole.NewMessage("Water shot detected! Reducing impulse.");

                // 1. 修改发射冲力 (LaunchImpulse)
                // 注意：这会直接影响内部 Launch 逻辑计算出的初速度
                __instance.LaunchImpulse = 15f; 

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
