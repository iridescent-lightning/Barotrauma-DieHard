﻿using System;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Barotrauma;
using Barotrauma.Networking;

namespace BarotraumaDieHard
{
    // ==================== HARMONY PATCHES ====================

    /// <summary>
    /// 补丁 1：拦截任务更新逻辑，利用 Tag 识别并执行物理距离判定
    /// </summary>
    [HarmonyPatch(typeof(SalvageMission), "UpdateMissionSpecific")]
    class Patch_SalvageMission_Update
    {
        private const float CompletionDistance = 4000f; // 判定成功的物理距离阈值

        static void Postfix(SalvageMission __instance, float deltaTime)
        {
            // 核心修改：不再锁死 ID，只要 XML 的 tags 属性里包含 "salvagewreck" 就进行拦截
            if (__instance.Prefab?.Tags == null || !__instance.Prefab.Tags.Contains("salvagewreck")) return;
            
            // 如果任务已经被判定成功，则跳过
            if (__instance.State > 0 && __instance.Completed) return;

            // 寻找当前关卡被 Lua 赋予物理实体的沉船
            if (Level.Loaded?.Wrecks == null) return;

            foreach (var wreck in Level.Loaded.Wrecks)
            {
                if (wreck == null || wreck.Removed) continue;

                // 确定终点位置（前哨站或关卡右边界）
                Vector2 destinationPos = Level.Loaded.EndOutpost != null ? 
                    Level.Loaded.EndOutpost.WorldPosition : 
                    new Vector2(Level.Loaded.EndPosition.X, Level.Loaded.EndPosition.Y);

                float distance = Vector2.Distance(wreck.WorldPosition, destinationPos);

                // 如果沉船被成功拖曳到了终点范围内
                if (distance <= CompletionDistance)
                {
                    if (__instance.State == 0)
                    {
                        __instance.State = 1; // 标记状态为准备就绪
                        
                        string msg = $"【前哨站无线电】已检测到打捞目标 {wreck.Info.Name} 进入回收区！离开当前关卡即可完成结算。";
                        #if SERVER
                        GameMain.Server?.SendDirectChatMessage(
                            ChatMessage.Create("无线电", msg, ChatMessageType.Radio, null), null);
                        #else
                        GameMain.Client?.AddChatMessage(msg, ChatMessageType.Radio);
                        #endif
                    }
                    return;
                }
            }

            // 防呆：如果沉船被冲走或在中途脱落，重置任务就绪状态
            if (__instance.State == 1)
            {
                __instance.State = 0;
            }
        }
    }

    /// <summary>
    /// 补丁 2：重写胜负决定出口，通过 Tag 识别并强制返回完成状态
    /// </summary>
    [HarmonyPatch(typeof(SalvageMission), "DetermineCompleted")]
    class Patch_SalvageMission_DetermineCompleted
    {
        static bool Prefix(SalvageMission __instance, CampaignMode.TransitionType transitionType, ref bool __result)
        {
            // 同样通过 Tag 识别
            if (__instance.Prefab?.Tags != null && __instance.Prefab.Tags.Contains("DieHardreclaimwreck"))
            {
                // 如果在 Update 中沉船已经到位（State == 1），直接跳过原版背包检查，强行宣布胜利
                if (__instance.State == 1)
                {
                    __result = true;
                    return false; // 拦截并阻止原版逻辑运行
                }
            }
            return true; // 其他不带该 tag 的普通打捞任务不受影响，正常放行
        }
    }
}