#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Items.Components;
using System.Collections.Generic; // for Dictionary

using Barotrauma.Items.Components;
using System.Reflection;
using Barotrauma;
using HarmonyLib;




namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(Upgrade))]
    public class UpgradePatch
    {
        [HarmonyPatch("FindItemComponent")]
        [HarmonyPrefix]
        public static bool FindItemComponentPrefix(Item item, string name, ref ISerializableEntity[] __result)
        {
            // 1. 尝试从原版潜渊症命名空间获取类型
            Type? type = Type.GetType($"Barotrauma.Items.Components.{name}", false, true);

            // 2. 如果原版没有，则专门在你自己的 Mod 程序集中查找
            if (type == null)
            {
                // 获取当前正在执行代码的程序集，即你的 BarotraumaDieHard.dll
                Assembly myAssembly = Assembly.GetExecutingAssembly();
                
                // 尝试在你指定的命名空间下查找
                type = myAssembly.GetType($"BarotraumaDieHard.{name}", false, true);
                
                // 如果你的组件没有放在该命名空间根目录，可以进行受限的本地扫描
                if (type == null)
                {
                    type = myAssembly.GetTypes().FirstOrDefault(t => 
                        t.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && 
                        typeof(ItemComponent).IsAssignableFrom(t));
                }
            }

            // 3. 执行逻辑
            if (type != null && typeof(ItemComponent).IsAssignableFrom(type))
            {
                var components = item.Components.Where(ic => ic.GetType() == type);
                if (components.Any())
                {
                    __result = components.Cast<ISerializableEntity>().ToArray();
                    return false; // 找到匹配项，拦截原方法
                }
            }

            // 重要：如果找不到匹配的组件，一定要返回 true，否则游戏原版的升级逻辑会被彻底掐断
            return true; 
        }
    }
}
