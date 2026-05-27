using System;
using System.Reflection;
using Barotrauma;
using HarmonyLib;
using System.Diagnostics;
using System.Collections.Generic;

namespace BarotraumaDieHard
{
    public static class ModProfiler
    {
        private static readonly Dictionary<string, (double TotalMs, long CallCount)> ProfileData = new();
        private static int frameCounter = 0;

        public static void AutoHookAllUpdates(Harmony harmonyInstance)
        {
            var patchedMethods = harmonyInstance.GetPatchedMethods();

            // 提前获取 patchMethod 的反射信息，避免在循环里重复获取损耗性能
            FieldInfo patchMethodField = typeof(Patch).GetField("patchMethod", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var originalMethod in patchedMethods)
            {
                Patches patchInfo = Harmony.GetPatchInfo(originalMethod);
                if (patchInfo == null) continue;

                if (!originalMethod.Name.Contains("Update") && !originalMethod.Name.Contains("Think")) continue;

                // 1. 遍历并利用反射处理 Postfixes
                foreach (var postfix in patchInfo.Postfixes)
                {
                    if (postfix.owner != harmonyInstance.Id) continue;
                    
                    // 关键修复：用反射绕过 internal/private 权限限制
                    MethodInfo actualPatchMethod = patchMethodField != null 
                        ? (MethodInfo)patchMethodField.GetValue(postfix) 
                        : postfix.patchMethod; // 备用降级逻辑

                    if (actualPatchMethod != null)
                    {
                        HookMethodViaHarmony(harmonyInstance, actualPatchMethod, "Postfix");
                    }
                }

                // 2. 遍历并利用反射处理 Prefixes
                foreach (var prefix in patchInfo.Prefixes)
                {
                    if (prefix.owner != harmonyInstance.Id) continue;
                    
                    // 关键修复：用反射绕过 internal/private 权限限制
                    MethodInfo actualPatchMethod = patchMethodField != null 
                        ? (MethodInfo)patchMethodField.GetValue(prefix) 
                        : prefix.patchMethod;

                    if (actualPatchMethod != null)
                    {
                        HookMethodViaHarmony(harmonyInstance, actualPatchMethod, "Prefix");
                    }
                }
            }

            // 3. 注册 think 钩子输出报告
            GameMain.LuaCs.Hook.Add("think", "DieHardProfilerOutput", (args) =>
            {
                frameCounter++;
                if (frameCounter >= 300)
                {
                    frameCounter = 0;
                    PrintPerformanceReport();
                }
                return null;
            });

            LuaCsLogger.Log("[DieHard Profiler] 自动化性能监控网已通过反射安全铺设完毕！正在静默监听...");
        }

        public static void HookMethodViaHarmony(Harmony harmony, MethodInfo targetYourMethod, string type)
        {
            if (targetYourMethod == null) return;

            string keyName = $"{targetYourMethod.DeclaringType?.Name}.{targetYourMethod.Name} ({type})";

            MethodInfo profilerPrefix = typeof(ModProfiler).GetMethod(nameof(ProfilerGlobalPrefix), BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo profilerPostfix = typeof(ModProfiler).GetMethod(nameof(ProfilerGlobalPostfix), BindingFlags.NonPublic | BindingFlags.Static);

            var harmonyMethodPrefix = new HarmonyMethod(profilerPrefix);
            harmonyMethodPrefix.before = new[] { keyName }; 

            harmony.Patch(targetYourMethod, 
                prefix: harmonyMethodPrefix, 
                postfix: new HarmonyMethod(profilerPostfix)
            );
        }

        private static void ProfilerGlobalPrefix(out Stopwatch __state)
        {
            __state = Stopwatch.StartNew();
        }

        private static void ProfilerGlobalPostfix(Stopwatch __state, MethodInfo __originalMethod)
        {
            if (__state == null) return;
            __state.Stop();

            string keyName = $"{__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}";

            lock (ProfileData)
            {
                if (!ProfileData.TryGetValue(keyName, out var data))
                {
                    data = (0, 0);
                }
                ProfileData[keyName] = (data.TotalMs + __state.Elapsed.TotalMilliseconds, data.CallCount + 1);
            }
        }

        private static void PrintPerformanceReport()
        {
            lock (ProfileData)
            {
                if (ProfileData.Count == 0) return;

                DebugConsole.NewMessage("\n==== [DieHard Mod 性能耗时排行榜 (每5秒全自动统计)] ====", Microsoft.Xna.Framework.Color.Orange);
                
                var sortedData = new List<KeyValuePair<string, (double TotalMs, long CallCount)>>(ProfileData);
                sortedData.Sort((x, y) => y.Value.TotalMs.CompareTo(x.Value.TotalMs));

                foreach (var kvp in sortedData)
                {
                    double avgMs = kvp.Value.TotalMs / kvp.Value.CallCount;
                    
                    if (kvp.Value.TotalMs > 1.0)
                    {
                        Microsoft.Xna.Framework.Color color = avgMs > 0.5 ? Microsoft.Xna.Framework.Color.Red : Microsoft.Xna.Framework.Color.LightGreen;
                        DebugConsole.NewMessage($"- 脚本 [{kvp.Key}]: 5秒总耗时 {kvp.Value.TotalMs:F2}ms | 触发了 {kvp.Value.CallCount} 次 | 单次平均耗时: {avgMs:F4}ms", color);
                    }
                }
                
                ProfileData.Clear();
                DebugConsole.NewMessage("==================================================\n", Microsoft.Xna.Framework.Color.Orange);
            }
        }
    }
}