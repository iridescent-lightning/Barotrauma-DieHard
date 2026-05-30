using System;
using System.Reflection;
using System.Collections.Generic;
using Barotrauma;
using Barotrauma.Items.Components;
using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Linq;
using BarotraumaDieHard.AI;
using Barotrauma.Networking;
using Barotrauma.Extensions;

namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(Reactor))]
    class ReactorDieHard
    {
        // 升级字典：不仅缓存 Container，还把每个反应堆专属的独立计时器存进去
        public class ReactorCacheData
        {
            public ItemContainer Container;
            public float Timer;
        }

        public static Dictionary<int, ReactorCacheData> ReactorDataCache = new Dictionary<int, ReactorCacheData>();
        private static bool inEditor;
        private const float UPDATE_INTERVAL = 0.2f;  // 每秒更新5次
        // === 新增配置参数 ===
        // 控制裂变率没有控制棒时的每秒上涨速度。例如 15f 意味着从 0 上涨到 100 需要大约 6.6 秒。
        private const float FISSION_RISE_SPEED = 5f;

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void UpdatePostfix(float deltaTime, Camera cam, Reactor __instance)
        {
            // 安全检查
            if (__instance.item == null) return;

            int reactorId = __instance.item.ID;

            // 1. 获取或创建属于当前反应堆实例的独立缓存
            if (!ReactorDataCache.TryGetValue(reactorId, out var cache))
            {
                var containers = __instance.item.GetComponents<ItemContainer>().ToList();
                if (containers.Count > 1)
                {
                    cache = new ReactorCacheData { Container = containers[1], Timer = 0f };
                    ReactorDataCache[reactorId] = cache;
                }
                else
                {
                    // 没有第二个容器，属于原版或不相干反应堆，直接放行
                    return;
                }
            }

            // 2. 实例隔离的节流控制（每个反应堆自己过自己的 0.2 秒）
            cache.Timer += deltaTime;
            if (cache.Timer < UPDATE_INTERVAL) return;
            cache.Timer = 0f; // 仅清空当前反应堆的计时器，不影响其他反应堆

            // 3. 核心逻辑执行
            Item controlrod = null;
            if (cache.Container != null && cache.Container.Inventory != null)
            {
                controlrod = cache.Container.Inventory.GetItemAt(0);
            }

            
            bool isOperating = __instance.TargetTurbineOutput > 0f;

            if (isOperating)
            {
                if (controlrod != null)
                {
                    if (controlrod.Condition <= 0)
                    {
                        // 控制棒耗尽，反应堆失控
                        __instance.fissionRate = 100f;
                        __instance.Item.Condition -= 1.5f * UPDATE_INTERVAL;
                    }
                    else if (__instance.item.InPlayerSubmarine)
                    {
                        // 正常消耗控制棒（联机模式下加个同步脏标记，确保通知到服务器/客户端）
                        controlrod.Condition -= 0.02f * UPDATE_INTERVAL;
                    }
                }
                else
                {
                    // 明确没有控制棒时的失控惩罚
                    if (__instance.item.InPlayerSubmarine)
                    {
                        // 联机环境下可以用这条，但注意别刷屏控制台
                        // DebugConsole.NewMessage($"[DieHard] 反应堆 {reactorId} 缺失控制棒！");
                        __instance.AutoTemp = false;
                        __instance.TargetFissionRate = 100f;
                        __instance.fissionRate = Microsoft.Xna.Framework.MathHelper.Min(
                        100f, 
                        __instance.fissionRate + (FISSION_RISE_SPEED * UPDATE_INTERVAL)
                    );
                        __instance.Item.Condition -= 1f * UPDATE_INTERVAL;
                    }
                }
            }

            // 临界崩溃状态触发爆炸辅助
            if (__instance.Item.Condition < 2f && __instance.Item.Condition > 0f && __instance.Temperature > 10f)
            {
                Entity.Spawner.AddItemToSpawnQueue(ItemPrefab.GetItemPrefab("reactorcsexplosionhelper"), __instance.Item.WorldPosition);
            }
        }

        [HarmonyPatch("OnMapLoaded")]
        [HarmonyPostfix]
        public static void OnMapLoadedPostfix(Reactor __instance)
        {
            foreach (Submarine sub in Submarine.Loaded)
            {
                if (sub?.Info?.OutpostGenerationParams != null)
                {
                    inEditor = true;
                }
            }
        }

        // 每次换图或清理战局时调用，防止内存泄漏
        public static void ClearRactorySecondContainerDictionary()
        {
            ReactorDataCache.Clear();
        }


        [HarmonyPatch("CrewAIOperate")]
        [HarmonyPrefix]
        public static bool PrefixCrewAIOperate(Reactor __instance, ref bool __result, float deltaTime, Character character, AIObjectiveOperateItem objective)
        {
            // 1. 安全前置检查：玩家控制、客户端直接放行原版
            if (GameMain.NetworkMember != null && GameMain.NetworkMember.IsClient) { return true; }
            if (character == null || character.IsPlayer) { return true; }

            var item = __instance.Item;
            if (item == null) { return true; }

            character.AIController.SteeringManager.Reset();
            bool shutDown = objective.Option == "shutdown";

            __instance.IsActive = true;

            if (!shutDown)
            {
                // 使用原版属性/方法（避免反射字段），如果这些属性在当前版本私有，再回退用 AccessTools
                float aiUpdateTimer = (float)AccessTools.Field(typeof(Reactor), "aiUpdateTimer").GetValue(__instance);
                float aiUpdateInterval = (float)AccessTools.Field(typeof(Reactor), "AIUpdateInterval").GetValue(__instance);

                float degreeOfSuccess = Math.Min(__instance.DegreeOfSuccess(character), 1.0f);
                float refuelLimit = 0.3f;

                if (degreeOfSuccess > refuelLimit)
                {
                    if (aiUpdateTimer > 0.0f)
                    {
                        aiUpdateTimer -= deltaTime;
                        AccessTools.Field(typeof(Reactor), "aiUpdateTimer").SetValue(__instance, aiUpdateTimer);
                        __result = false;
                        return false; 
                    }

                    AccessTools.Field(typeof(Reactor), "aiUpdateTimer").SetValue(__instance, aiUpdateInterval);

                    // 安全获取燃料消耗率
                    float fuelConsumption = (float)AccessTools.Method(typeof(Reactor), "GetFuelConsumption").Invoke(__instance, null);
                    float minCondition = fuelConsumption * MathUtils.Pow2((degreeOfSuccess - refuelLimit) * 2);

                    bool needMoreFuel = (bool)AccessTools.Method(typeof(Reactor), "NeedMoreFuel").Invoke(__instance, new object[] { 0.5f, minCondition });

                    if (needMoreFuel)
                    {
                        bool outOfFuel = false;
                        var container = item.GetComponent<ItemContainer>();
                        if (objective.SubObjectives.None() && container != null)
                        {
                            // 【安全修复】：不再用反射调用危险的私有泛型方法 AIContainItems
                            // 潜渊症原版的 AIContainItems<T> 实际上就是实例化一个标准的 AIObjectiveContainItems！
                            // 我们直接 new 它，绝对安全且不会触发空指针！
                            var containObjective = new AIObjectiveContainItem(character, item.Prefab.Identifier, container, objective.objectiveManager, spawnItemIfNotFound: !character.IsOnPlayerTeam)
                            {
                                //IsPriority = true
                            };

                            containObjective.Completed += () => ReportFuelRodCount(__instance, character, ref outOfFuel);
                            containObjective.Abandoned += () => ReportFuelRodCount(__instance, character, ref outOfFuel);
                            
                            objective.AddSubObjective(containObjective);
                            character.Speak(TextManager.Get("DialogReactorFuel").Value, null, 0.0f, Tags.ReactorFuel, 30.0f);
                        }
                        __result = outOfFuel;
                        return false;
                    }
                    else
                    {
                        // 保持原版维修逻辑不变
                        if (item.ConditionPercentage <= 0 && AIObjectiveRepairItems.IsValidTarget(item, character))
                        {
                            if (item.Repairables.Average(r => r.DegreeOfSuccess(character)) > 0.4f)
                            {
                                objective.AddSubObjective(new AIObjectiveRepairItem(character, item, objective.objectiveManager, isPriority: true));
                                __result = false;
                                return false;
                            }
                            else
                            {
                                character.Speak(TextManager.Get("DialogReactorIsBroken").Value, identifier: "reactorisbroken".ToIdentifier(), minDurationBetweenSimilar: 30.0f);
                            }
                        }

                        // 核心检查：是否触发你自定义的拔棒子回收命令
                        bool tooMuchFuel = (bool)AccessTools.Method(typeof(Reactor), "TooMuchFuel").Invoke(__instance, null);

                        // 只要有废棒或者多棒，直接把你的任务作为子任务塞进去
                        TriggerCustomRetrieveObjective(__instance, character, objective);
                    }
                }
            }

            // 执行尾部控制台逻辑（安全重构）
            ExecuteOriginalPostLogic(__instance, shutDown, character, objective, Math.Min(__instance.DegreeOfSuccess(character), 1.0f));

            __result = false;
            return false; 
        }

        private static void TriggerCustomRetrieveObjective(Reactor reactor, Character character, AIObjectiveOperateItem objective)
        {
            if (character.AIController is HumanAIController humanAI)
            {
                var objectiveManager = humanAI.ObjectiveManager;

                // 检测是否已经在执行回收了
                bool alreadyRetrieving = objectiveManager.Objectives.Any(o => o is AIObjectiveRetrieveFuelRod) 
                                      || objectiveManager.CurrentOrders.Any(o => o.Objective is AIObjectiveRetrieveFuelRod)
                                      || objective.SubObjectives.Any(so => so is AIObjectiveRetrieveFuelRod);

                if (!alreadyRetrieving)
                {
                    // 检查反应堆里到底有没有需要回收的棒子（判定条件： condition == 0，或者 tooMuchFuel 成立）
                    var container = reactor.Item.GetComponent<ItemContainer>();
                    bool hasTargetRod = container?.Inventory.AllItems.Any(it => it.HasTag(Tags.ReactorFuel) && it.ConditionPercentage <= 0.1f) ?? false;
                    bool tooMuchFuel = (bool)AccessTools.Method(typeof(Reactor), "TooMuchFuel").Invoke(reactor, null);

                    // 只有在真的需要拔棒子时才触发
                    if (hasTargetRod || tooMuchFuel)
                    {
                        var retrieveObjective = new AIObjectiveRetrieveFuelRod(character, objectiveManager, priorityModifier: 2.0f);
                        
                        // 作为子目标塞入，既不破坏原有的 Power up 命令，又能让 Bot 立刻去做回收！
                        objective.AddSubObjective(retrieveObjective);
                        
                        if (character.IsOnPlayerTeam)
                        {
                            character.Speak(
                                TextManager.Get("dialog.bots.retrievefuelrod.start", "Reactor core requires attention. Retrieving hazard gear...").Value, 
                                ChatMessageType.Radio, 
                                identifier: "startretrievefuelrod".ToIdentifier(), 
                                minDurationBetweenSimilar: 15f
                            );
                        }
                    }
                }
            }
        }

        private static void ReportFuelRodCount(Reactor reactor, Character character, ref bool outOfFuel)
        {
            if (!character.IsOnPlayerTeam || Submarine.MainSub == null) { return; }
            int remainingFuelRods = Submarine.MainSub.GetItems(false).Count(i => i.HasTag(Tags.ReactorFuel) && i.Condition > 1);
            if (remainingFuelRods == 0)
            {
                character.Speak(TextManager.Get("DialogOutOfFuelRods").Value, null, 0.0f, "outoffuelrods".ToIdentifier(), 30.0f);
                outOfFuel = true;
            }
            else if (remainingFuelRods < 3)
            {
                character.Speak(TextManager.Get("DialogLowOnFuelRods").Value, null, 0.0f, "lowonfuelrods".ToIdentifier(), 30.0f);
            }
        }

        private static void ExecuteOriginalPostLogic(Reactor instance, bool shutDown, Character character, AIObjectiveOperateItem objective, float degreeOfSuccess)
        {
            // 用属性或安全的公共接口代替不稳定的私有字段抓取
            var lastUser = instance.LastUser; 
            var lastAIUser = instance.LastAIUser;
            bool lastUserWasPlayer = instance.LastUserWasPlayer;

            if (objective.Override)
            {
                if (lastUser != null && lastUser != character && lastUser != lastAIUser)
                {
                    if (lastUser.SelectedItem == instance.Item && character.IsOnPlayerTeam)
                    {
                        character.Speak(TextManager.Get("DialogReactorTaken").Value, null, 0.0f, "reactortaken".ToIdentifier(), 10.0f);
                    }
                }
            }
            else if (lastUserWasPlayer && lastUser != null && lastUser.TeamID == character.TeamID)
            {
                return;
            }

            // 使用原版的公开 Set 方法更新使用者
            instance.LastUser = character;
            instance.LastAIUser = character;

            bool prevAutoTemp = instance.AutoTemp;
            bool prevPowerOn = instance.PowerOn;
            float prevFissionRate = instance.TargetFissionRate;
            float prevTurbineOutput = instance.TargetTurbineOutput;

            if (shutDown)
            {
                instance.PowerOn = false;
                instance.TargetFissionRate = 0.0f;
                instance.TargetTurbineOutput = 0.0f;
                AccessTools.Field(typeof(Reactor), "unsentChanges").SetValue(instance, true);
                return;
            }
            else
            {
                instance.PowerOn = true;
                if (objective.Override || !instance.AutoTemp)
                {
                    if (degreeOfSuccess < 0.5f)
                    {
                        instance.AutoTemp = true;
                    }
                    else
                    {
                        instance.AutoTemp = false;
                        instance.UpdateAutoTemp(MathHelper.Lerp(0.5f, 2.0f, degreeOfSuccess), 1.0f);
                    }
                }

                if (instance.AutoTemp != prevAutoTemp ||
                    prevPowerOn != instance.PowerOn ||
                    Math.Abs(prevFissionRate - instance.TargetFissionRate) > 1.0f ||
                    Math.Abs(prevTurbineOutput - instance.TargetTurbineOutput) > 1.0f)
                {
                    AccessTools.Field(typeof(Reactor), "unsentChanges").SetValue(instance, true);
                }
            }
        }
    }
}