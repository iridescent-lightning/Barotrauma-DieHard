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
            // add this tag so no other pump without container won't crash the game.
            if (_.item.HasTag("cspump"))
            {
			    motor = _.item.GetComponent<ItemContainer>().Inventory.GetItemAt(0);
                gasTank = _.item.GetComponent<ItemContainer>().Inventory.GetItemAt(1);
            }


            _.pumpSpeedLockTimer -= deltaTime;
            _.isActiveLockTimer -= deltaTime;

            if (!_.IsActive)
            {
                return false;
            }

            if ((motor == null || motor.Condition <= 0) && _.item.HasTag("cspump"))
			{
				// No motor found, stop the pump
				_.IsActive = false;
				_.flowPercentage = 0.0f;
				_.currFlow = 0.0f;
				
				return false;
			}
            
            // Pressurized Air feature - Simplified Version
            if (_.item.CurrentHull != null && _.item.CurrentHull.IsWetRoom)
            {
                // 1. 获取基础数据
                float hullWaterVolume = _.item.CurrentHull.WaterVolume;
                float totalHullVolume = _.item.CurrentHull.Volume;
                
                // 处理链接的房间（如果是压载舱，通常会有链接）
                foreach (var linked in _.item.CurrentHull.linkedTo)
                {
                    if (linked is Hull linkedHull)
                    {
                        hullWaterVolume += linkedHull.WaterVolume;
                        totalHullVolume += linkedHull.Volume;
                    }
                }

                float hullWaterPercentage = (hullWaterVolume / totalHullVolume) * 100.0f; 
                float currentPressurizedAir = HullMod.GetGas(_.item.CurrentHull, "PressurizedAir");

                // 2. 定义所需压力：这里简化为空气占比（排水越多，需要的压力越高来顶住水）
                // 比如：0% 水位时需要 100 压力，100% 水位时需要 0 压力
                float requiredAir = Math.Max(0, 100f - hullWaterPercentage);

                // 3. 逻辑判断
                if (_.TargetLevel != null)
                {
                    float targetLevel = (float)_.TargetLevel;
                    
                    // --- 排水逻辑 (目标水位 < 当前水位) ---
                    if (targetLevel < hullWaterPercentage)
                    {
                        // 如果压力不足，无法排水，必须从气罐加压
                        if (currentPressurizedAir < requiredAir)
                        {
                            if (gasTank != null && gasTank.Condition > 0)
                            {
                                // 加速充气：补足缺口并额外增加一个基础流量
                                float refillAmount = (requiredAir - currentPressurizedAir) + 30f;
                                HullMod.AddGas(_.item.CurrentHull, "PressurizedAir", refillAmount, deltaTime);
                                gasTank.Condition -= 0.002f * deltaTime; // 消耗气罐
                            }
                            
                            // 压力不够时，强制限制排水速度或停止排水
                            _.FlowPercentage = 0.0f; 
                        }
                        else
                        {
                            // 压力够了，正常排水
                            _.FlowPercentage = (targetLevel - hullWaterPercentage) * 10.0f;
                        }
                    }
                    // --- 吸水逻辑 (目标水位 > 当前水位) ---
                    else
                    {
                        // 吸水时，多余的压力会自然释放（模拟排气阀）
                        if (currentPressurizedAir > requiredAir)
                        {
                            // 吸水自动减压，减压速度随进水速度加快
                            HullMod.AddGas(_.item.CurrentHull, "PressurizedAir", -40f, deltaTime);
                        }
                        
                        // 进水不受压力限制
                        _.FlowPercentage = (targetLevel - hullWaterPercentage) * 10.0f;
                    }
                }
            }
            
            

            

            if (!_.HasPower)
            {
                return false;
            }

            _.UpdateProjSpecific(deltaTime);

            _.ApplyStatusEffects(ActionType.OnActive, deltaTime);

            if (_.item.CurrentHull == null) { return false; }      

            float powerFactor = Math.Min(_.currPowerConsumption <= 0.0f || _.MinVoltage <= 0.0f ? 1.0f : _.Voltage, Pump.MaxOverVoltageFactor);

            _.currFlow = _.flowPercentage / 100.0f * _.item.StatManager.GetAdjustedValueMultiplicative(ItemTalentStats.PumpMaxFlow, _.MaxFlow) * powerFactor;

            if (_.item.GetComponent<Repairable>() is { IsTinkering: true } repairable)
            {
                _.currFlow *= 1f + repairable.TinkeringStrength * Pump.TinkeringSpeedIncrease;
            }

            _.currFlow = _.item.StatManager.GetAdjustedValueMultiplicative(ItemTalentStats.PumpSpeed, _.currFlow);

            //less effective when in a bad condition
            _.currFlow *= MathHelper.Lerp(0.5f, 1.0f, _.item.Condition / _.item.MaxCondition);

            _.item.CurrentHull.WaterVolume += _.currFlow * deltaTime * Timing.FixedUpdateRate; 
            if (_.item.CurrentHull.WaterVolume > _.item.CurrentHull.Volume) { _.item.CurrentHull.Pressure += 30.0f * deltaTime; }


			
			if (Math.Abs(_.currFlow) > 0)
			{
				motor.Condition = motor.Condition - 0.001f;
			}
			return false;
		}
    }
}
