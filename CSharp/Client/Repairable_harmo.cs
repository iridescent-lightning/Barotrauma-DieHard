// This partial class has no use
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Barotrauma.Particles;
using Barotrauma.Sounds;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

using HarmonyLib;
using System.Reflection;
using System.Xml.Linq;
using System.Globalization;

using Barotrauma;


namespace BarotraumaDieHard
{
    partial class RepairableDieHard
    {


        /*private static GUIProgressBar waterResistanceBar;


        public static void CreateGUI(Repairable __instance)
        {
            Repairable _ = __instance;

            var paddedFrame = new GUILayoutGroup(new RectTransform(new Vector2(0.8f, 0.75f), _.GuiFrame.RectTransform, Anchor.Center), childAnchor: Anchor.TopCenter)
            {
                Stretch = true,
                RelativeSpacing = 0.05f,
                CanBeFocused = true
            };

            var waterResistanceBarHolder = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.2f), paddedFrame.RectTransform), isHorizontal: true)
            {
                Stretch = false,
                RelativeSpacing = 0.02f
            };

            waterResistanceBar = new GUIProgressBar(new RectTransform(new Vector2(0.6f, 0.33f), waterResistanceBarHolder.RectTransform){RelativeOffset = new Vector2(0, 1.1f)},
                color: GUIStyle.Green, barSize: 0.0f, style: "DeviceProgressBar");


        }
    

        public static void DrawHUDPrefix(SpriteBatch spriteBatch, Character character, Repairable __instance)
        {
            Repairable _ = __instance;

            float defaultMaxCondition = (_.item.MaxCondition / _.item.MaxRepairConditionMultiplier);

            waterResistanceBar.BarSize = _.item.Condition / defaultMaxCondition;

        }*/
    


    }
}