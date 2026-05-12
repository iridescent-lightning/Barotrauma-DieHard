using System;
using Barotrauma;
using Barotrauma.Networking;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Items.Components;
using Networking;
using HarmonyLib;

namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(PowerTransfer))]
    public partial class PowerTransferPatch 
    {

        [HarmonyPatch("CreateGUI")]
        [HarmonyPostfix]
        static void Postfix(PowerTransfer __instance)
        {
            var guiFrame = __instance.GuiFrame;
            if (guiFrame == null) return;

            // 1. 防止重复创建按钮（通过检查 UserData 或 查找子组件）
        // if (guiFrame.RectTransform.GetChildElement("DieHardJBSwitch") != null) return;

            // 2. 参照你之前的布局创建一个容器
            var paddedFrame = new GUIFrame(new RectTransform(new Vector2(0.3f, 1f), guiFrame.RectTransform, Anchor.Center) 
            { 
                RelativeOffset = new Vector2(-0.7f, 0) 
            }, "ItemUI")
            {
                CanBeFocused = false,
                UserData = "DieHardJBSwitch" // 标记
            };

            // 3. 创建开关按钮
            var JBModeSwitch = new GUIButton(new RectTransform(new Vector2(0.6f, 0.6f), paddedFrame.RectTransform, Anchor.Center), 
                string.Empty, style: "SwitchDieHardJBButton")
            {
                ToolTip = TextManager.Get("PowerTransferPatchSwitchTip"),
                // 从我们的静态字典中读取当前状态
                Selected = !GetLeverState(__instance.Item),
                Enabled = true,
                ClickSound = GUISoundType.UISwitch,
                OnClicked = (button, data) =>
                {
                    // 反转状态
                    bool currentLeverState = GetLeverState(__instance.Item);
                    bool newLeverState = !currentLeverState;
                    
                    // 保存状态
                    SetLeverState(__instance.Item, newLeverState);
                    
                    // 更新 UI 表现
                    button.Selected = !newLeverState;

                    // 重点：刷新电力网格缓存 (Flag Connections)
                    RefreshGrid(__instance.Item);

                    // 网络同步：发送到服务器
                    if (GameMain.Client != null)
                    {
                        // 这里调用你原本的 SendJBSwitchMessage 逻辑
                        // 注意：你可能需要把该方法移到一个工具类或静态类中
                        SendJBSwitchMessage(__instance.Item, newLeverState);
                       
                    }

                    return true;
                }
            };
        }


        
    }


}