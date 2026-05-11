public static void UpdatePostfix(float deltaTime, Camera cam, Projectile __instance)
        {
            
            
            // less effective than statuseffect
        /*float range = 25f; // 对应 XML 中的 Range
        Vector2 currentPos = __instance.item.WorldPosition;

        foreach (Item nearbyItem in Item.ItemList)
        {
            // 1. 基础过滤：已移除的、或者你自己（防止炸弹炸自己）
            if (nearbyItem.Removed || nearbyItem == __instance.item) { continue; }

            // 2. 距离检查 (复刻你提供的 CheckDistance 逻辑)
            float xDiff = Math.Abs(nearbyItem.WorldPosition.X - currentPos.X);
            if (xDiff > range) { continue; }
            float yDiff = Math.Abs(nearbyItem.WorldPosition.Y - currentPos.Y);
            if (yDiff > range) { continue; }

            if (xDiff * xDiff + yDiff * yDiff < range * range)
            {
                
                // 3. 目标筛选：无 Body 且包含特定 Tag
                if (nearbyItem.body == null && nearbyItem.HasTag("damage_by_passing_bullet"))
                {
                    // 4. 执行伤害或其他逻辑
                    // 注意：deltaTime 确保伤害平滑
                    nearbyItem.Condition -= 400.0f * deltaTime;
                    DebugConsole.NewMessage($"{nearbyItem}");
                    
                    // 如果你想触发目标身上的 OnUse 效果
                    // nearbyItem.ApplyStatusEffects(ActionType.OnUse, deltaTime, user: _user);
                }
            }
        }*/


// ------------------------- AABB scan-----------------------------------
// 记录每颗子弹上一帧的位置
        private static Dictionary<Projectile, Vector2> lastPositions = new Dictionary<Projectile, Vector2>();
        // 记录每颗子弹已经处理过的物品，防止一发子弹对同一个灯造成多次伤害
        private static Dictionary<Projectile, HashSet<Item>> processedItems = new Dictionary<Projectile, HashSet<Item>>();
        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void Update(Projectile __instance, float deltaTime, Camera cam)
        {
            if (!__instance.IsActive || __instance.item.body == null)
            {
                // 子弹消失时清理内存
                lastPositions.Remove(__instance);
                processedItems.Remove(__instance);
                return;
            }

            Vector2 currentPos = __instance.item.SimPosition;

            // 如果字典里没有，说明是发射的第一帧，记录当前位置并跳过扫描
            if (!lastPositions.TryGetValue(__instance, out Vector2 prevPos))
            {
                
                lastPositions[__instance] = currentPos;
                return;
            }

            // 1. 计算扫描区域（包含上一帧到这一帧的位移轨迹）
            float padding = 100f; // 扫描半径
            Vector2 min = new Vector2(MathHelper.Min(currentPos.X, prevPos.X) - padding, MathHelper.Min(currentPos.Y, prevPos.Y) - padding);
            Vector2 max = new Vector2(MathHelper.Max(currentPos.X, prevPos.X) + padding, MathHelper.Max(currentPos.Y, prevPos.Y) + padding);

            if (!processedItems.ContainsKey(__instance)) processedItems[__instance] = new HashSet<Item>();

            // 2. 执行 AABB 查询
            // 1. 先构造 AABB 矩形区域
var areaToQuery = new FarseerPhysics.Collision.AABB(min, max);

// 2. 调用 QueryAABB，只传入两个参数：回调函数 和 AABB对象
GameMain.World.QueryAABB(fixture =>
{
    
    if (fixture.Body.UserData is Item targetItem)
    {
        if (targetItem == __instance.item) return true;

        if (targetItem.HasTag("damage_by_passing_bullet") && !processedItems[__instance].Contains(targetItem))
        {
            
            OnItemPassedBy(__instance, targetItem);
            processedItems[__instance].Add(targetItem);
        }
    }
    return true;
}, ref areaToQuery); // 注意：某些版本可能需要加 ref 关键字

            // 3. 更新位置记录，供下一帧使用
            lastPositions[__instance] = currentPos;
        }
        public static void OnItemPassedBy(Projectile projectile, Item targetItem)
        {
            
            // 造成损坏
            targetItem.Condition -= 100f;

            // 产生火花特效 (由子弹飞向的反方向喷射)
            Vector2 sparkVelocity = -projectile.item.body.LinearVelocity * 0.1f;
            GameMain.ParticleManager.CreateParticle("spark", targetItem.WorldPosition, sparkVelocity, 0, targetItem.CurrentHull);

            // 如果是电子设备，产生电弧
            if (targetItem.HasTag("junctionbox") || targetItem.HasTag("lamp"))
            {
                
                GameMain.ParticleManager.CreateParticle("heavyarc", targetItem.WorldPosition, Vector2.Zero, 0, targetItem.CurrentHull);
                
                // 也可以添加碎玻璃粒子
                GameMain.ParticleManager.CreateParticle("shrapnel", targetItem.WorldPosition, Vector2.UnitY, 0, targetItem.CurrentHull);
            }
        }