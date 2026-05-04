using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Barotrauma.Extensions;
using Barotrauma;

using Barotrauma.Abilities;
using System.Collections.Immutable;
using System.Globalization;
using HarmonyLib;


namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(Mission))]
    public static class MissionPatch
    {
        // 使用一个静态字段记录上一次检查的任务状态，防止重复触发
        private static int lastProcessedState = -1;
        private static Mission lastProcessedMission = null;
        // 拦截任务结束方法
        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void UpdatePostfix(Mission __instance, float deltaTime)
        {
            // 仅在服务端或单机模式处理逻辑
            if (GameMain.NetworkMember is { IsClient: true }) { return; }

            // 1. 检查任务类型是否为 Monster
            if (__instance.Prefab.Type != "Monster".ToIdentifier()) { return; }

            // 2. 状态监测逻辑：
            // 当任务状态发生改变，且当前状态为 1 (通常代表目标已达成但未结算)
            if (__instance.State != lastProcessedState || __instance != lastProcessedMission)
            {
                if (__instance.State == 1 && !__instance.Completed) 
                {
                    // 触发后续事件
                    TriggerFetchHintMission(__instance);
                    
                    // 记录状态，防止同一任务在同一状态下反复触发
                    lastProcessedState = __instance.State;
                    lastProcessedMission = __instance;
                }
            }
        }

        private static void TriggerFetchHintMission(Mission mission)
        {
            // 寻找事件预制件[cite: 5]
            Identifier eventId = "fetchmissionhint".ToIdentifier();
            EventPrefab eventPrefab = EventPrefab.FindEventPrefab(eventId, Identifier.Empty, mission.Prefab.ContentPackage);

            if (eventPrefab != null && GameMain.GameSession?.EventManager != null)
            {
                // 创建并激活事件实例[cite: 5]
                var newEvent = eventPrefab.CreateInstance(GameMain.GameSession.EventManager.RandomSeed);
                newEvent.TriggeringMission = mission;
                GameMain.GameSession.EventManager.ActivateEvent(newEvent);

                #if SERVER
                GameMain.Server?.SendChatMessage("生物特征匹配成功，后续任务指引已下达。", Barotrauma.Networking.ChatMessageType.Server);
                #else
                DebugConsole.NewMessage("任务状态更新：后续指引已激活", Microsoft.Xna.Framework.Color.Orange);
                #endif
            }
        }
    }
}
