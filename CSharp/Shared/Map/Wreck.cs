using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Barotrauma;
using FarseerPhysics.Dynamics;
using FarseerPhysics;
using System.Linq;
using System;

namespace BarotraumaDieHard
{
    
    // 【核心补丁 2】：强制修改物理刚体的属性，允许平移和物理休眠注销
    [HarmonyPatch(typeof(Submarine), "MakeWreck")]
    public static class Submarine_MakeWreck_Patch
    {
        public static void Postfix(Submarine __instance)
        {
            // 如果这个潜艇原本是 Wreck（或者已经被我们伪装了，我们可以通过名字或某些特征二次判断，但最直接的是针对所有非主艇的处理）
            if (__instance.PhysicsBody != null && __instance.PhysicsBody.FarseerBody != null)
            {
                var body = __instance.PhysicsBody.FarseerBody;
                
                // 彻底解除静态限制，转为完全动态物理刚体
                body.BodyType = BodyType.Dynamic;
                // 允许这个庞然大物在受到外力（抓钩、碰撞）时产生真实的物理位移
                body.LinearDamping = 0.5f; // 稍微给点阻尼，防止飘得太厉害
                body.AngularDamping = 0.5f;
            }

            CoroutineManager.Invoke(() => 
            { 
                bool hasSalvageMission = false;
                if (GameMain.GameSession?.Missions != null)
                {
                    foreach (var m in GameMain.GameSession.Missions)
                    {
                        DebugConsole.NewMessage($"{m}");
                        if (m?.Prefab != null)
                        {
                            
                            // 修复点：将原来的 MissionType 改为 Type
                            bool typeMatch = m.Prefab.Type.ToString().Equals("SalvageMission", StringComparison.OrdinalIgnoreCase);
                            bool idMatch = m.Prefab.Identifier.ToString().Contains("salvage", StringComparison.OrdinalIgnoreCase);

                            if (typeMatch || idMatch)
                            {
                                hasSalvageMission = true;
                                break; // 找到匹配的任务就立即跳出循环
                            }
                        }
                    }
                }

                // 如果有打捞任务，则显示声纳标记
                if (hasSalvageMission)
                {
                    __instance.ShowSonarMarker = true;
                }
                else
                {
                    __instance.ShowSonarMarker = false; 
                }
                
            }, delay: 5.0f);
        }
    }
}