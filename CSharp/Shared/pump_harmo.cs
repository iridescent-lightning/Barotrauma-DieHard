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
    class CustomPump : IAssemblyPlugin
    {
        public  Harmony harmony;
		private static Item motor;
        private static Item gasTank;

        public void Initialize()
		{
			harmony = new Harmony("CustomPump");
			
			harmony.Patch(
                original: typeof(Pump).GetMethod("Update"),
                prefix: new HarmonyMethod(typeof(CustomPump).GetMethod(nameof(Update)))
            );
			
				
			}

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }

        

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
                                float refillAmount = (requiredAir - currentAir) + 30f;
                                HullMod.AddGas(_.item.CurrentHull, "PressurizedAir", refillAmount, deltaTime);
                                gasTank.Condition -= 0.002f * deltaTime;
                            }
                            
                            // 关键：修改 flowPercentage，让原版按这个速度排水（这里设为0表示压力不够排不动）
                            _.flowPercentage = 0.0f; 
                        }
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
