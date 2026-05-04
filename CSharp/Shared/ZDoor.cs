using Barotrauma.Networking;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using FarseerPhysics.Dynamics;
using Barotrauma;
#if CLIENT
using Barotrauma.Lights;
#endif
using Barotrauma.Extensions;
using System.Runtime.CompilerServices;


using HarmonyLib;
using Barotrauma.Items.Components;

namespace BarotraumaDieHard
{
    public static class ZDoorLogic
    {
        // 处理传送的核心方法
        public static void Teleport(Item door, Character user)
        {
            
            if (door == null || user == null) return;

            // 1. 寻找被连线的项目 (linkedTo)
            // 我们找第一个拥有相同标签或同样是门的连线物件
            Item targetGate = door.linkedTo.OfType<Item>().FirstOrDefault();

            if (targetGate != null)
            {
                
                Vector2 destination = targetGate.WorldPosition;
                //延迟一帧执行，不如AI在计算GetDiffAndAdvance时会导致游戏崩溃
                CoroutineManager.Invoke(() =>
                {
                    if (user == null || user.Removed) return;

                    user.TeleportTo(destination);

                    if (user.AIController is HumanAIController ai)
                    {
                        if (ai?.SteeringManager != null)
                        {
                            //ai.ClearObjectives();   
                            ai.SteeringManager.Reset();
                        }
                    }

                    if (Character.Controlled == user)
                    {
                        var cam = GameMain.GameScreen?.Cam;
                        if (cam != null)
                        {
                            cam.Position = destination;
                            cam.UpdateTransform(interpolate: false);
                        }
                    }

                }, 0.0f);

                // 4. 播放声音（可选）
                // 也可以直接在 XML 的 StatusEffect 里写 Sound 节点
                if (door.Prefab.Identifier == "enterable_door1")
                {
                    // 播放自定义声音逻辑
                }
            }
        }
    }

    // 使用 Harmony 补丁拦截使用行为
    [HarmonyLib.HarmonyPatch(typeof(Item), "TryInteract")]
    class ItemUsePatch
    {
        public static void Postfix(Item __instance, Character user, bool __result)
        {
            if (!__result) return;

            // 检查这个 Item 是否是我们的传送门
            if (__instance.HasTag("zdoor")) 
            {
                
                ZDoorLogic.Teleport(__instance, user);
            }
        }
    }
}

