using Barotrauma.MapCreatures.Behavior;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using System;
using System.Globalization;
using System.Linq;

using Barotrauma.Items.Components;
using Barotrauma.Extensions;
using Barotrauma;
using HarmonyLib;


namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(Pump))]
    class PumpPatch
    {

		private static Item motor;
        private static Item gasTank;
        
        [HarmonyPatch("Update")]
        [HarmonyPrefix]
        public static bool Update(float deltaTime, Camera cam, Pump __instance)
        {
            Pump _ = __instance;

            // 1. 获取容器内的物品（只针对有特定标签的泵）
            Item motor = null;
            Item gasTank = null;

            if (_.item.HasTag("cspump"))
            {
                var container = _.item.GetComponent<ItemContainer>();
                if (container != null && container.Inventory != null)
                {
                    motor = container.Inventory.GetItemAt(0);
                    gasTank = container.Inventory.GetItemAt(1);
                }

                // --- 电机逻辑 ---
                // 如果电机缺失或损坏，强制关泵
                if (motor == null || motor.Condition <= 0)
                {
                    _.IsActive = false;
                    _.flowPercentage = 0.0f;
                    // 这里不需要 return false，让原版去处理后续的停止逻辑和网络同步
                }
                else
                {
                    // 如果有电机且正在运转，消耗电机耐久
                    if (_.IsActive && Math.Abs(_.currFlow) > 0.01f)
                    {
                        motor.Condition -= 0.001f * deltaTime;
                    }
                }
            }

            // --- 气压与排水逻辑 ---
            //bool isPressurizing = false; //是否正在加压
            if (_.IsActive && _.item.CurrentHull != null && _.item.CurrentHull.IsWetRoom)
            {
                float currentWaterPct = (_.item.CurrentHull.WaterVolume / _.item.CurrentHull.Volume) * 100.0f;
                float currentAir = HullMod.GetGas(_.item.CurrentHull, "PressurizedAir");
                float requiredAir = Math.Max(0, 100f - currentWaterPct);

                if (_.TargetLevel != null)
                {
                    float targetLevel = (float)_.TargetLevel;

                    // 排水模式 (目标水位 < 当前水位)
                    if (targetLevel < currentWaterPct)
                    {
                        if (currentAir < requiredAir)
                        {
                            // 压力不足：尝试从气罐充气
                            if (gasTank != null && gasTank.Condition > 0)
                            {
                                //isPressurizing = true;
                                float refillAmount = (requiredAir - currentAir) + 30f;
                                HullMod.AddGas(_.item.CurrentHull, "PressurizedAir", refillAmount, deltaTime);
                                gasTank.Condition -= 0.002f * deltaTime;
                                #if CLIENT
    // 只有当正在加压（isPressurizing）且处于客户端时运行
    
        float particlesPerSec = 100f; // 对应 XML 中的 particlespersecond
        float particleInterval = 1.0f / particlesPerSec;
        
        // 注意：在静态补丁中处理计时器比较麻烦，这里使用一个简化的增量逻辑
        // 实际开发中建议将 particleTimer 存放在实例的自定义数据中
        // 这里假设每帧至少生成 1-2 个粒子作为演示
        int spawnCount = (int)(deltaTime * particlesPerSec);
        for (int i = 0; i < spawnCount; i++)
        {
            // 计算随机速度（模拟灭火器 1000.0 到 1650.0 的喷射感）
            float speed = Barotrauma.Rand.Range(500.0f, 1000.0f);
            // 喷射方向：假设向上喷射 (Vector2.UnitY)，你可以根据 item.Rotation 修改
            Vector2 velocity = Vector2.UnitY * speed; 

            // 创建粒子 "extinguisher" (灭火器粒子)
            GameMain.ParticleManager.CreateParticle(
                "extinguisher", 
                _.item.WorldPosition, // 产生位置：水泵中心
                velocity, 
                0.0f, 
                _.item.CurrentHull);
        }
    
#endif
                            }
                            
                            // 关键：修改 flowPercentage，让原版按这个速度排水（这里设为0表示压力不够排不动）
                            _.flowPercentage = 0.0f; 
                        }
                        // --- [新增逻辑：释放粒子效果] ---

                    }
                    // 吸水模式 (目标水位 > 当前水位)
                    else
                    {
                        // 吸水时自动释放压力
                        if (currentAir > requiredAir)
                        {
                            HullMod.AddGas(_.item.CurrentHull, "PressurizedAir", -40f, deltaTime);
                        }
                    }
                }
            }

            // 返回 true 是解决不同步的金钥匙
            // 这意味着原版 Update 接手：计算最终 currFlow、更新 WaterVolume、最重要的是调用 UpdateNetworking()
            return true; 
        }
    }
}
