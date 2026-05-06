using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Barotrauma;
using Barotrauma.Lights;
using HarmonyLib;

namespace BarotraumaDieHard.Lights
{
    // 全局逻辑管理器：负责采样和存储亮度数据
    public static class LightDetectionLogic
    {
        private static float sampleTimer;
        private const float SampleInterval = 0.1f; // 采样频率：每秒10次，平衡性能与准确度
        public static Dictionary<Character, float> CharacterBrightnessMap = new Dictionary<Character, float>();
        
        // 核心：从 LightMap 获取指定位置的亮度
        public static void UpdateBrightnessData(LightManager lightManager, Camera cam)
        {
            //sampleTimer -= (float)GameMain.DeltaTime;
            //if (sampleTimer > 0) return;
            //sampleTimer = SampleInterval;

            var lightMap = lightManager.LightMap;
            if (lightMap == null || cam == null) return;

            CharacterBrightnessMap.Clear();

            foreach (Character c in Character.CharacterList)
            {
                if (c.IsDead || !c.Enabled) continue;

                // 1. 世界坐标转屏幕比例坐标
                Vector2 screenPos = cam.WorldToScreen(c.WorldPosition);
                float normX = screenPos.X / cam.Resolution.X;
                float normY = screenPos.Y / cam.Resolution.Y;

                // 2. 只有在屏幕内的角色才进行采样
                if (normX >= 0 && normX <= 1 && normY >= 0 && normY <= 1)
                {
                    int x = (int)(normX * lightMap.Width);
                    int y = (int)(normY * lightMap.Height);
                    
                    x = Math.Clamp(x, 0, lightMap.Width - 1);
                    y = Math.Clamp(y, 0, lightMap.Height - 1);

                    // 3. 同步采样（虽然 GetData 慢，但我们限制了频率和次数）
                    Color[] colorData = new Color[1];
                    lightMap.GetData(0, new Rectangle(x, y, 1, 1), colorData, 0, 1);
                    
                    // Barotrauma 的 LightMap 中，R通道通常代表环境光亮度
                    float brightness = colorData[0].R / 255f;
                    CharacterBrightnessMap[c] = brightness;
                    
                }
            }
        }

        public static float GetCachedBrightness(Character c)
        {
            return CharacterBrightnessMap.TryGetValue(c, out float b) ? b : 0f;
        }
    }

    // --- Harmony 补丁部分 ---

    [HarmonyPatch(typeof(LightManager), "Update")]
    class LightManagerPatch
    {
        // 在光照系统更新后，提取数据
        [HarmonyPostfix]
        public static void Postfix(LightManager __instance)
        {
            if (Screen.Selected != GameMain.GameScreen) return;
            LightDetectionLogic.UpdateBrightnessData(__instance, GameMain.GameScreen.Cam);
        }
    }

    [HarmonyPatch(typeof(Character), "get_DisplayName")]
    class HideNamePatch
    {
        [HarmonyPostfix]
        public static void Postfix(Character __instance, ref string __result)
        {
            // 忽略玩家自己
            if (__instance == Character.Controlled) return;

            float brightness = LightDetectionLogic.GetCachedBrightness(__instance);

            // 阈值1：看不清脸 (0.15)
            if (brightness < 0.15f)
            {
                __result = "???";
            }
        }
    }

    [HarmonyPatch(typeof(Character), "Draw")]
    class HideCharacterPatch
    {
        // 阈值2：完全黑暗 (0.05)，停止渲染角色模型
        [HarmonyPrefix]
        public static bool Prefix(Character __instance)
        {
            if (__instance == Character.Controlled) return true;

            float brightness = LightDetectionLogic.GetCachedBrightness(__instance);

            // 如果亮度极低，直接跳过 Draw 方法（Prefix 返回 false）
            if (brightness < 0.05f)
            {
                return false; 
            }
            return true;
        }
    }
}