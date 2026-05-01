﻿using FarseerPhysics;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Contacts;
using FarseerPhysics.Dynamics.Joints;
using Microsoft.Xna.Framework;
using Barotrauma.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Networking;
using LimbParams = Barotrauma.RagdollParams.LimbParams;
using JointParams = Barotrauma.RagdollParams.JointParams;
using Barotrauma.Abilities;

using Barotrauma;
using HarmonyLib;


namespace BarotraumaDieHard//todo make a structural namespace DieHard.Item.Components. namespace can't be used in elsewhere
{
    [HarmonyPatch(typeof(Limb))]
    class LimbPatch
    {
        //你尝试补丁的是一个“属性（Property）”，但在 Harmony 特性中没有指定补丁的目标是属性的 Getter 方法。
        /*在 C# 中，属性 CanBeSeveredAlive 实际上是由一个名为 get_CanBeSeveredAlive 
        的方法实现的。如果你只写 [HarmonyPatch("CanBeSeveredAlive")]，Harmony 会去寻找一个同名的普通方法，
        找不到自然会报 Undefined target method（未定义目标方法）。
        */
        // 关键修正：指定 MethodType 为 Getter
        [HarmonyPatch("CanBeSeveredAlive", MethodType.Getter)]
        [HarmonyPrefix]
        public static bool CanBeSeveredAlivePrefix(ref bool __result, Limb __instance)
        {
            
            //DebugConsole.NewMessage("LimbMod CanBeSeveredAlivePostfix");
            
                __result = true;
            return false;
        }
    }
}
