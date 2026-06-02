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
        private static readonly Dictionary<string, Action<Projectile, Item, Vector2>> EffectHandlers = 
            new Dictionary<string, Action<Projectile, Item, Vector2>>
        {
            { "lamp", (p, target, vel) => SpawnLampEffects(target, vel) },
            { "junctionbox", (p, target, vel) => SpawnJunctionBoxEffects(target, vel) }
        };
        #endif

        private static Dictionary<Projectile, HashSet<Item>> processedItems = new Dictionary<Projectile, HashSet<Item>>();
        private static readonly Identifier DamageByBulletTag = "damage_by_passing_bullet".ToIdentifier();

        // ⭐【性能核心优化点 1】⭐
        // 建立一个专属的、只存放带有 "damage_by_passing_bullet" 标签物品的精简列表
        private static readonly List<Item> BulletDamageableItems = new List<Item>();
        private static float _cleanupTimer = 0f;

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void Update(Projectile __instance, float deltaTime)
        {
            if (!__instance.IsActive || __instance.item.Removed) 
            {
                processedItems.Remove(__instance);
                return;
            }

            Vector2 currentWorldPos = __instance.item.WorldPosition;
            var currentSub = __instance.item.Submarine;

            // ⭐【性能核心优化点 2】⭐
            // 定时动态维护我们的精简列表（每 2 秒刷新一次，过滤掉被删掉的物品，或者新生成的物品）
            _cleanupTimer += deltaTime;
            if (_cleanupTimer > 2.0f || BulletDamageableItems.Count == 0)
            {
                _cleanupTimer = 0f;
                BulletDamageableItems.Clear();
                for (int i = 0; i < Item.ItemList.Count; i++)
                {
                    var it = Item.ItemList[i];
                    if (it != null && !it.Removed && it.HasTag(DamageByBulletTag))
                    {
                        BulletDamageableItems.Add(it);
                    }
                }
            }

            // ⭐【性能核心优化点 3】⭐
            // 抛弃原先对 Item.ItemList（几千上万个）的全局遍历，改为只遍历我们精简后的列表（通常只有几十或上百个）
            // 并使用 standard for 循环代替 foreach，实现零 GC 内存垃圾分配
            for (int i = 0; i < BulletDamageableItems.Count; i++)
            {
                Item targetItem = BulletDamageableItems[i];

                // 健壮性安全检查
                if (targetItem == null || targetItem.Removed) continue;
                
                // 空间过滤：必须在同一个潜艇内
                if (targetItem.Submarine != currentSub) continue;
                
                // 距离过滤
                Vector2 diff = targetItem.WorldPosition - currentWorldPos;
                if (diff.LengthSquared() < 1600f) 
                {
                    if (!processedItems.ContainsKey(__instance)) 
                        processedItems[__instance] = new HashSet<Item>();

                    if (!processedItems[__instance].Contains(targetItem))
                    {
                        OnItemPassedBy(__instance, targetItem);
                        processedItems[__instance].Add(targetItem);
                    }
                }
            }
        }
        
        public static void OnItemPassedBy(Projectile projectile, Item targetItem)
        {
            float fireProb = Rand.Range(0,100f);
            Vector2 bulletVelocity = projectile.item.body.LinearVelocity;
            float randomedDamge = Rand.Range(0f, 100f);
            
            if(targetItem.HasTag("container"))
            {
                targetItem.Condition -= 5f;
            }
            else
            {
                targetItem.Condition -= randomedDamge;
            }
            
#if CLIENT
            // 使用 for 循环或特定优化来匹配 Tag 特效（这里因为 keys 很少，原逻辑尚可，但改用 for 性能更好）
            var keys = EffectHandlers.Keys.ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                string tag = keys[i];
                if (targetItem.HasTag(tag))
                {
                    EffectHandlers[tag](projectile, targetItem, bulletVelocity);
                    return;
                }
            }
#endif
        }

#if CLIENT
        private static void SpawnLampEffects(Item target, Vector2 bulletVel)
        {
            Vector2 spawnPos = target.WorldPosition;
            Hull hull = target.CurrentHull;

            if (target.Condition <= 0)
            {
                GameMain.ParticleManager.CreateParticle("ElectricShock_brokenlamparc", spawnPos, Vector2.Zero, 0, hull);
            }
            
            for (int i = 0; i < 12; i++)
            {
                float angle = Rand.Range(0f, MathHelper.TwoPi);
                Vector2 vel = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * Rand.Range(100f, 400f);
                GameMain.ParticleManager.CreateParticle("iceshards_glass", spawnPos, vel, angle, hull, Rand.Range(0.3f, 0.6f));
            }
        }

        private static void SpawnJunctionBoxEffects(Item target, Vector2 bulletVel)
        {
            Hull hull = target.CurrentHull;
            Vector2 spawnPos = target.WorldPosition;
            if (target.Condition <= 0)
            {
                GameMain.ParticleManager.CreateParticle("TeslaExplosion", spawnPos, Vector2.Zero, 0, hull);
            }
            SpawnDirectionalSparks(spawnPos, bulletVel, target.CurrentHull);
        }

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

        [HarmonyPatch("Launch")]
        [HarmonyPrefix]
        public static void LaunchPrefix(Projectile __instance, Character user)
        {
            if (__instance == null || __instance.item == null || user == null) return;
            
            Item itemInRightHand = user.Inventory?.GetItemInLimbSlot(InvSlotType.RightHand);
            if (itemInRightHand != null && itemInRightHand.Prefab.Identifier == "underwatergun") return;

            if (__instance.item.InWater || user.InWater)
            {
                __instance.LaunchImpulse = 15f; 
            }
        }
    }

    [HarmonyPatch(typeof(Projectile), "HandleProjectileCollision")]
    public class ProjectileCollisionPatch
    {
        public static void Postfix(Projectile __instance, Fixture target, Vector2 collisionNormal, Vector2 velocity)
        {
            if (target.Body.UserData is Limb targetLimb && targetLimb.character != null)
            {
#if CLIENT
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
            Vector2 bulletDir = Vector2.Normalize(bulletVel);

            CreateBloodBurst(spawnPos, bulletDir, hull, limb.character, 6, 12, 200f, 500f);

            if (isHeadshot)
            {
                Vector2 reverseDir = -bulletDir;
                CreateBloodBurst(spawnPos, reverseDir, hull, limb.character, 10, 18, 100f, 300f, 1.5f);
                GameMain.ParticleManager.CreateParticle("blooddrop", spawnPos, reverseDir * 50f, 0f, hull, 2.0f);
            }
        }

        private static void SpawnBloodEffects(Item target, Vector2 vel) { } // 保留桩函数防止潜藏引用冲突

        private static void CreateBloodBurst(Vector2 pos, Vector2 baseDir, Hull hull, Character character, int min, int max, float minSpeed, float maxSpeed, float scaleMult = 1.0f)
        {
            int count = Rand.Range(min, max);
            for (int i = 0; i < count; i++)
            {
                float spread = Rand.Range(-0.5f, 0.5f);
                Vector2 finalDir = baseDir + new Vector2(baseDir.Y, -baseDir.X) * spread;

                GameMain.ParticleManager.CreateParticle(
                    "blood",
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