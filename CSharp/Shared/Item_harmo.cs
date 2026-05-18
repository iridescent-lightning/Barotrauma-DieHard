
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Contacts;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Extensions;
using Barotrauma.MapCreatures.Behavior;
using System.Collections.Immutable;
using Barotrauma.Abilities;
#if CLIENT
using Microsoft.Xna.Framework.Graphics;
#endif

using Barotrauma;
using HarmonyLib;
using System.Globalization;
using System.Reflection;// for bindingflags

using System.IO;

namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(Item))]
    partial class ItemPatch
    {
        /*[HarmonyPatch("Update")]
        [HarmonyPostfix]
    public static void Postfix(Item __instance)
    {
        if(__instance.Condition > 0) return;
        // 1. 检查是否是你想要处理的物品（例如通过 Prefab 名字或标签）
        if (!__instance.HasTag("container")) return;

        // 2. 处理附着组件（Holdable）
        var holdable = __instance.GetComponent<Holdable>();
        if (holdable != null && holdable.Attached)
        {
            holdable.Attached = false; // 断开附着状态
            // 如果需要掉落逻辑，可以调用 Drop
            //holdable.Drop(null);
        }

        // 3. 激活物理引擎
        if (__instance.body != null)
        {
            __instance.body.Enabled = true;
            __instance.body.FarseerBody.BodyType = FarseerPhysics.BodyType.Dynamic;
            
            // 给它一个微小的初速度，让脱落看起来更自然
            __instance.body.ApplyLinearImpulse(new Vector2(0, -10f));
        }

        // 4. 修改 Submarine 属性
        // 如果物品之前属于潜艇的一部分（静态），现在它应该可以自由移动
        //__instance.Submarine = __instance.Submarine; 
    }

        [HarmonyPatch("Update")]
        [HarmonyPostfix]	
        public static void Postfix(Item __instance)
        {
            if (__instance.Prefab.Identifier == "revolver")
            {
                
            }
            
        }*/

                
    }


        
}
