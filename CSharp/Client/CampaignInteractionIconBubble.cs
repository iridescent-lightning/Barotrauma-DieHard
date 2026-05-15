﻿using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HarmonyLib;
using Barotrauma;

namespace BarotraumaDieHard
{
    //-----------画在头上的图标------------------
    // === 补丁 1: 记录当前正在绘制的角色实例 ===
    // 涵盖了 DrawFront (头顶气泡) 和 DrawInteractionIcon (远距离/边缘图标)
    [HarmonyPatch(typeof(Character))]
    [HarmonyPatch("DrawFront")]
    public class CharacterDrawIconPatch
    {
        public static Character CurrentDrawingCharacter;

        [HarmonyPrefix]
        public static void Prefix(Character __instance)
        {
            CurrentDrawingCharacter = __instance;
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            CurrentDrawingCharacter = null;
        }
    }

    // === 补丁 2: 样式重定向 ===
    [HarmonyPatch(typeof(GUIStyle))]
    [HarmonyPatch("GetComponentStyle", new System.Type[] { typeof(Identifier) })]
    public class GUIStylePatch
    {
        [HarmonyPostfix]
        public static void Postfix(Identifier identifier, ref GUIComponentStyle __result)
        {
            // 只有当补丁 1 抓到了正在被绘制的角色，且该角色是你的怪物商人时才执行
            if (CharacterDrawIconPatch.CurrentDrawingCharacter != null)
            {
                var character = CharacterDrawIconPatch.CurrentDrawingCharacter;

                if (character.JobIdentifier == "monster_merchant")
                {
                    string styleName = identifier.Value;

                    // 同时拦截：
                    // 1. CampaignInteractionBubble.Store (近距离头顶气泡)
                    // 2. CampaignInteractionIcon.Store (远距离指示器/屏幕边缘图标)
                    if (styleName == "CampaignInteractionBubble.Store")
                    {
                        // 直接从字典取，避免递归调用 GetComponentStyle 导致死循环
                        if (GUIStyle.ComponentStyles.TryGet("CampaignInteractionBubble.StoreMonster".ToIdentifier(), out var customStyle))
                        {
                            __result = customStyle;
                        }
                    }
                }
            }
        }
    }

//--------------------------方位指示图标-----------------------------
    [HarmonyPatch(typeof(CharacterHUD))]
    [HarmonyPatch("Draw")]
    public class HUDOverridePatch
    {
        // 用于暂存商人及其原始交互类型
        private static readonly System.Collections.Generic.Dictionary<Character, CampaignMode.InteractionType> originalStates 
            = new System.Collections.Generic.Dictionary<Character, CampaignMode.InteractionType>();

        [HarmonyPrefix]
        public static void Prefix()
        {
            originalStates.Clear();
            
            foreach (Character npc in Character.CharacterList)
            {
                // 找到你的怪物商人，且它当前正处于“商店”模式
                if (npc.JobIdentifier == "monster_merchant" && npc.CampaignInteractionType == CampaignMode.InteractionType.Store)
                {
                    // 1. 记录原始状态
                    originalStates[npc] = npc.CampaignInteractionType;
                    
                    // 2. 强制改为 None！这样原版的 foreach 就会直接 continue，不再画那个购物车。
                    npc.CampaignInteractionType = CampaignMode.InteractionType.None;
                }
            }
        }

        [HarmonyPostfix]
        public static void Postfix(Character character, SpriteBatch spriteBatch, Camera cam)
        {
            // 1. 恢复现场：把所有改掉的状态还回去，确保下一帧逻辑正常
            foreach (var entry in originalStates)
            {
                entry.Key.CampaignInteractionType = entry.Value;
                
                // 2. 既然原版没画，现在由我们亲手画出绿色的图标
                // 注意：npc 现在已经恢复为 Store 状态了
                DrawCustomIcon(entry.Key, spriteBatch, cam, character);
            }
            // 3. 为所有 Electrical_bug 绘制指示图标
            foreach (Character npc in Character.CharacterList)
            {
                if (npc.SpeciesName.Value == "Electrical_bug" && !npc.IsDead)
                {
                    DrawElectricalBugIcon(npc, spriteBatch, cam, character);
                }
            }
        }

        // 我们自己建立的绘制方法，完全模仿原版逻辑
        private static void DrawCustomIcon(Character npc, SpriteBatch spriteBatch, Camera cam, Character observer)
        {
            // 1. 获取你的自定义样式
            if (!GUIStyle.ComponentStyles.TryGet("CampaignInteractionIndicator.StoreMonster".ToIdentifier(), out var style)) return;

            // 2. 计算可见范围 (模仿原版)
            Hull currentHull = npc.CurrentHull;
            Range<float> visibleRange = new Range<float>(
                currentHull == observer.CurrentHull ? 500.0f : 100.0f, 
                float.PositiveInfinity);

            // 3. 计算透明度 (模仿原版)
            float dist = Vector2.Distance(observer.WorldPosition, npc.WorldPosition);
            float distFactor = 1.0f - MathHelper.Clamp((dist - 1000.0f) / (3000.0f - 1000.0f), 0, 1);
            float alpha = MathHelper.Lerp(0.3f, 1.0f, distFactor);

            // 4. 获取 Label
            LocalizedString label = npc.Info?.Title;

            // 5. 调用最稳妥的 DrawIndicator 重载 (不带 Nullable 的那个)
            // 如果报错参数不匹配，请根据你当前游戏版本的 GUI.DrawIndicator 补齐参数
            GUI.DrawIndicator(
                spriteBatch,
                npc.DrawPosition,
                cam,
                visibleRange,
                style.GetDefaultSprite(),
                style.Color * alpha,
                label: label);
        }

        private static void DrawElectricalBugIcon(Character npc, SpriteBatch spriteBatch, Camera cam, Character observer)
        {
            // 1. 获取你的自定义样式
            if (!GUIStyle.ComponentStyles.TryGet("PositionIndicator.ElectricalBug".ToIdentifier(), out var style)) return;

            // 2. 计算可见范围 (模仿原版)
            Hull currentHull = npc.CurrentHull;
            Range<float> visibleRange = new Range<float>(
                currentHull == observer.CurrentHull ? 500.0f : 100.0f, 
                float.PositiveInfinity);

            // 3. 计算透明度 (模仿原版)
            float dist = Vector2.Distance(observer.WorldPosition, npc.WorldPosition);
            float distFactor = 1.0f - MathHelper.Clamp((dist - 1000.0f) / (3000.0f - 1000.0f), 0, 1);
            float alpha = MathHelper.Lerp(0.3f, 1.0f, distFactor);

            // 4. 获取 Label
            LocalizedString label = npc.Info?.Title;

            // 5. 调用最稳妥的 DrawIndicator 重载 (不带 Nullable 的那个)
            // 如果报错参数不匹配，请根据你当前游戏版本的 GUI.DrawIndicator 补齐参数
            GUI.DrawIndicator(
                spriteBatch,
                npc.DrawPosition,
                cam,
                visibleRange,
                style.GetDefaultSprite(),
                style.Color * alpha,
                label: label);
        }
    }    
}