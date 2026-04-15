using Microsoft.Xna.Framework;
using System;
using System.Globalization;
using System.Xml.Linq;
using Barotrauma.Networking;
using System.Collections.Generic; // for Dictionary

using Barotrauma.Items.Components;
using System.Reflection;
using Barotrauma;
using HarmonyLib;


using System.Runtime.CompilerServices;

namespace BarotraumaDieHard
{
    partial class EngineMod : IAssemblyPlugin
    {
        public  Harmony harmony;

			

       


        public void Initialize()
		{
			harmony = new Harmony("EngineMod");
			
			harmony.Patch(
                original: typeof(Engine).GetMethod("Update"),
                postfix: new HarmonyMethod(typeof(EngineMod).GetMethod(nameof(Update)))
            );


            /*var originalInitProjSpecific = typeof(Engine).GetMethod("InitProjSpecific", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(ContentXElement) }, null);
            var postfixSelect = typeof(EngineMod).GetMethod("InitProjSpecificPostfix", BindingFlags.Public | BindingFlags.Static);
            harmony.Patch(originalInitProjSpecific, new HarmonyMethod(postfixSelect), null);*/

            
		}

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }
        

        public static void Update(float deltaTime, Camera cam, Engine __instance)
		{
           

           __instance.item.SendSignal(__instance.GetCurrentPowerConsumption(__instance.powerIn).ToString("F1"), "PowerConsumptionOut");
		}

        /*public static void InitProjSpecificPostfix(ContentXElement element, Engine __instance)
        {
            //获取或创建这个引擎对应的额外数据

            
            //比如根据 Tag 初始化
            if (__instance.Item.HasTag("engine"))
            {
                var extraData = __instance.GetExtraData();*/
            
            // 从 XML 节点中获取名为 "HasVerticalThruster" 的属性
            // GetAttributeBool 是 Barotrauma 提供的方便的扩展方法
            /*extraData.HasVerticalThruster = element.GetAttributeBool("HasVerticalThruster", true);

            if (extraData.HasVerticalThruster)
            {
                DebugConsole.NewMessage("成功从 XML 读取到垂直推进器属性！");
            }
                
            }*/

            /* 在滚动条的回调里
            var vSlider = new GUIScrollBar(...)
            {
                OnMoved = (scrollBar, barScroll) =>
                {
                    extraData.VerticalTargetForce = barScroll * 200f - 100f;
                    return true;
                }
            };
        }*/
        //附加额外属性信息的方法，可能有用。保留范例
       
    }


    public class EngineExtraData
    {
        public bool HasVerticalThruster = false;
        public float VerticalTargetForce = 0f;
        public float CurrentVerticalForce = 0f;
        
    }


    public static class EngineExtensions
    {
        // 这里的含义是：将 Engine 实例 与 EngineExtraData 实例绑定
        public static readonly ConditionalWeakTable<Engine, EngineExtraData> Data = new();

        // 为了方便调用，写一个扩展方法
        public static EngineExtraData GetExtraData(this Engine engine)
        {
            // 如果表里没这个引擎，它会自动创建一个新的 EngineExtraData
            return Data.GetOrCreateValue(engine);
        }
    }
}
