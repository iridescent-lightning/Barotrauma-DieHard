#nullable enable

using Barotrauma.IO;
using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Networking;
using Barotrauma.Extensions;


using Barotrauma;
#if CLIENT
using Barotrauma.Lights;
using Microsoft.Xna.Framework.Graphics;
#endif
using Barotrauma.Extensions;
using System.Reflection;


using HarmonyLib;
using Barotrauma.Items.Components;


namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(GameSession))]
    public class GameSessionDieHard
    {
        


        public static AfflictionPrefab pressurizedhullPrefab;


        // 使用参数类型数组解决 "ambiguous" (歧义) 问题
        [HarmonyPatch("StartRound")]
        [HarmonyPatch(new Type[] { typeof(LevelData), typeof(bool), typeof(SubmarineInfo), typeof(SubmarineInfo) })]
        [HarmonyPostfix]
        public static void StartRound(LevelData levelData, bool mirrorLevel, SubmarineInfo startOutpost, SubmarineInfo endOutpost)
        {
            
            //DebugConsole.NewMessage(Level.Loaded.StartLocation?.Type.ToString());
            pressurizedhullPrefab = AfflictionPrefab.Prefabs["pressurizedhull"];
            

            foreach (Item reactor in Item.ItemList)
            {
                if (reactor.HasTag("reactor".ToIdentifier())) // 注意：原版新架构中 Tag 通常需要转为 Identifier
                {
                    var itemContainers = reactor.GetComponents<ItemContainer>().ToList();
                    
                    // 核心修改 1：安全检查，必须确保该反应堆真的有第 2 个容器（索引 1），否则直接抓取会引起游戏崩溃
                    if (itemContainers.Count > 1)
                    {
                        // 核心修改 2：适配我们之前的 ReactorCacheData 数据结构，并修正 preactor -> reactor 的拼写错误
                        ReactorDieHard.ReactorDataCache[reactor.ID] = new ReactorDieHard.ReactorCacheData
                        {
                            Container = itemContainers[1],
                            Timer = 0f // 初始化计时器为 0
                        };
                    }
                    else
                    {
                        // 调试日志（可选）：如果是原版潜艇或者没有双容器配置的普通反应堆，打印提示并跳过
                        DebugConsole.ThrowError($"[DieHard] 反应堆 {reactor.ID} 没有双容器组件，已跳过初始化。");
                    }
                }
            }
            
            Submarine submarine = GameMain.GameSession?.Submarine ?? Submarine.MainSub;
            if (submarine is null) { return; }


            // 利用反射调用你贴出的原版 BuyUpgrade 私有方法
            MethodInfo buyUpgradeMethod = typeof(UpgradeManager).GetMethod("BuyUpgrade", 
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
			
            if (buyUpgradeMethod == null) return;

            // 3. 寻找或准备好你的自定义升级 Prefab
            // "increasewallhealth" 对应你写的 XML 里的 identifier
            UpgradePrefab wallUpgradePrefab = UpgradePrefab.Prefabs.Find(p => p.Identifier == "doublewallhealth");
            UpgradeCategory wallCategory = UpgradeCategory.Categories.Find(c => c.IsWallUpgrade);
            if (wallUpgradePrefab != null )
            {
				
                // 检测潜艇当前的墙壁是否已经有了这个升级，或者检查战役数据
                // 你的 maxlevel 设定为 6（每次+20%）。如果我们一步到位直接刷满到 5 级（+100% 也就是两倍血）
                int targetLevel = 2; 

                // 获取主潜艇上现有的某个墙壁看看有没有升过级，如果没有，说明是新战役开局或者新换的潜艇
                    var firstWall = Submarine.MainSub.GetWalls(alsoFromConnectedSubs: false).Find(w => w.MaxHealth > 0);
                
                    var existing = firstWall.GetUpgrade(wallUpgradePrefab.Identifier);
                    
                    // 核心保险：只有当当前等级低于目标等级时，才给买升级。这样绝对不会重复叠加！
                    if (existing == null || existing.Level < targetLevel)
                    {
                        // 隐式调用原版的 BuyUpgrade(prefab, category, submarine, level, parentSub)
                        buyUpgradeMethod.Invoke(null, new object[] { 
                            wallUpgradePrefab, 
                            wallCategory, 
                            Submarine.MainSub, 
                            targetLevel, 
                            null 
                        });

                        //DebugConsole.NewMessage($"[DieHard] 战役检测：成功通过原生升级系统将外壳血量永久提升至 200%！");
                    }
                
            }


        }

        
        [HarmonyPatch("EndRound")]
        [HarmonyPostfix]
        public static void EndRound(string endMessage, CampaignMode.TransitionType transitionType, TraitorManager.TraitorResults? traitorResults)
        {

           TurretDieHard.ResetOriginalReloadValue();
           TurretDieHard.ClearReloadDictionary();

            SonarMod.ResetOriginalSonarRange();
            SonarMod.ClearSonarRangeDictionary();


            ReactorDieHard.ClearRactorySecondContainerDictionary();

            CharacterPatch.ClearPressureTimerDictionary();

            ConvertLocationToDestroyed();
            PowerTransferPatch.LeverStates.Clear();
            
        }
    

        






        //This causes the togglecampaignteleport can't view the normal abandoned outposts if teleported directly to that location. Bucause it will always convert to destroyed type.
        public static void ConvertLocationToDestroyed()
        {
            if (GameMain.GameSession?.Campaign == null) return;

            
            // Get the current location (this may vary based on your context)
            Location currentLocation = Level.Loaded.StartLocation;

            if (currentLocation != null)
            {

                foreach (Item it in Item.ItemList)
                {
                    if (it.HasTag("reactor") && !it.InPlayerSubmarine && it.Condition <= 0)
                    {
#if DEBUG
                        DebugConsole.NewMessage("previous outpost's reactor is dead. Converting outpost to Abandoned.");
#endif
                        var allLocationTypes = LocationType.Prefabs; // This might be a collection containing all location types

                        // Find a specific type by its identifier
                        Identifier newTypeIdentifier = new Identifier("Destroyed");
                        LocationType newType = allLocationTypes.FirstOrDefault(lt => lt.Identifier == newTypeIdentifier);

                        if (newType == null)
                        {
                            DebugConsole.ThrowError("Failed to find the location type.");
                        }
                        else
                        {
                            // Use the location type, e.g., assign it to the current location
                            currentLocation.Type = newType;
                        }
                    }
                }
                
            }
        }
    }
}
