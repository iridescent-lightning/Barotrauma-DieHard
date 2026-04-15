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


using System.Runtime.CompilerServices;

namespace BarotraumaDieHard
{
    class UpgradeMod : IAssemblyPlugin
    {
        public  Harmony harmony;

        public void Initialize()
		{
			harmony = new Harmony("UpgradeMod");
			
			harmony.Patch(
                original: typeof(Upgrade).GetMethod("ApplyUpgrade"),
                prefix: new HarmonyMethod(typeof(UpgradeMod).GetMethod(nameof(ApplyUpgradePrefix)))
            );


            
		}

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }

        public static bool ApplyUpgradePrefix(Upgrade __instance)
{   
    DebugConsole.NewMessage("searching");
    
    // 遍历当前升级影响的所有组件
    foreach (var keyValuePair in __instance.TargetComponents)
    {
        
        var (entity, properties) = keyValuePair;

        foreach (PropertyReference propertyReference in properties)
        {
            
            // --- 核心修改点：拦截自定义属性 HasVerticalThruster ---
            if (propertyReference.Name.Value.Equals("VectoredThruster", StringComparison.OrdinalIgnoreCase))
                {
                    if (entity is CustomEngine engine)
                    {
                        DebugConsole.NewMessage("forcing");
                        engine.VectoredThruster = true;
                        
                    }
                    // 处理完自定义属性后，跳过本次循环，防止进入下方的“找不到属性”报错逻辑
                    continue; 
                }

            // --- 原版属性处理逻辑 ---
            if (entity.SerializableProperties.TryGetValue(propertyReference.Name, out SerializableProperty? property) && property != null)
            {
                object? originalValue = property.GetValue(entity);
                propertyReference.SetOriginalValue(originalValue);
                
                // 计算并设置新值
                object newValue = Convert.ChangeType(
                    propertyReference.CalculateUpgrade(__instance.Level), 
                    originalValue.GetType(), 
                    CultureInfo.InvariantCulture);
                
                property.SetValue(entity, newValue);
            }
            else
            {
                // 找不到属性时的模糊匹配逻辑
                string matchingString = string.Empty;
                int closestMatch = int.MaxValue;
                foreach (var (propertyName, _) in entity.SerializableProperties)
                {
                    int match = ToolBox.LevenshteinDistance(propertyName.Value, propertyReference.Name.Value);
                    if (match < closestMatch)
                    {
                        matchingString = propertyName.Value ?? "";
                        closestMatch = match;
                    }
                }

                DebugConsole.ThrowError($"The upgrade \"{__instance.Prefab.Name}\" cannot be applied to {entity.Name} because it does not contain the property \"{propertyReference.Name}\". \n" +
                                        $"Did you mean \"{matchingString}\"?");
            }
        }
    }

    // 返回 false 拦截原版方法，因为我们已经完整复刻并扩展了它的功能
    return false;
}
        
       
    }


}
