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
    public class UpgradePatch : IAssemblyPlugin
    {
        public  Harmony harmony;
        public void Initialize()
		{
			harmony = new Harmony("UpgradePatch");
			
			 var originalMethod = typeof(Upgrade).GetMethod("FindItemComponent", 
                    BindingFlags.NonPublic | BindingFlags.Static);
            var prefixMethod = typeof(UpgradePatch).GetMethod("FindItemComponentPrefix", 
                    BindingFlags.Public | BindingFlags.Static);
            harmony.Patch(originalMethod, new HarmonyMethod(prefixMethod));


            
		}

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }

        public static bool FindItemComponentPrefix(Item item, string name, ref ISerializableEntity[] __result)
        {
            
            // 先尝试原版查找
            Type? type = Type.GetType($"Barotrauma.Items.Components.{name.ToLowerInvariant()}", false, true);
            
            if (type == null)
            {
                // 在你的命名空间中查找
                DebugConsole.NewMessage("Try finding");
                type = Type.GetType($"BarotraumaDieHard.{name}", false, true);
                
                if (type == null)
                {
                    // 尝试在所有程序集中查找匹配名称的类型
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        type = assembly.GetType($"BarotraumaDieHard.{name}");
                        if (type != null) break;
                        
                        type = assembly.GetTypes().FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase) 
                            && typeof(ItemComponent).IsAssignableFrom(t));
                        if (type != null) break;
                    }
                }
            }
            
            if (type != null && typeof(ItemComponent).IsAssignableFrom(type))
            {
                var components = item.Components.Where(ic => ic.GetType() == type);
                if (components.Any())
                {
                    __result = components.Cast<ISerializableEntity>().ToArray();
                    return false; // 跳过原方法
                }
            }
            
            return false; // 继续原方法
        }
    }
}
