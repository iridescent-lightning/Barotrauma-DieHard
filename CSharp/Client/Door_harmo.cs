using Barotrauma.Extensions;
using Barotrauma.Lights;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;
using FarseerPhysics.Dynamics;
using Barotrauma;
using System.Runtime.CompilerServices;


using HarmonyLib;
using Barotrauma.Items.Components;

namespace BarotraumaDieHard
{

    partial class DoorPatch
    {
        
        [HarmonyPatch("Draw")]
        [HarmonyPostfix]
        public static void Postfix_Draw(SpriteBatch spriteBatch, bool editing, float itemDepth, Door __instance)
        {
            Character character = Character.Controlled;
            if (character == null) return;

            if (character.FocusedItem == __instance.Item)
            {
                // 直接使用物品的绘制位置
                // 在 Draw 补丁中，往往不需要手动加 Submarine.DrawPosition，因为变换矩阵已经应用了
                Vector2 drawPos = __instance.Item.DrawPosition;

                // 核心修正：仅对 Y 轴取反以适应渲染器的坐标系
                drawPos.Y = -drawPos.Y;


                bool shiftDown = character.IsKeyDown(InputType.Crouch);
                if (character.IsKeyDown(InputType.Crouch))
                {
                    if (GameSessionDieHard.customSprites.TryGetValue("door_open", out Sprite customWireSprite))
                    {
                        // 设置原点为图标中心，这样图标会准确居中在门上
                        Vector2 origin = new Vector2(customWireSprite.SourceRect.Width / 2f, customWireSprite.SourceRect.Height / 2f);

                        customWireSprite.Draw(
                            spriteBatch,
                            drawPos, // 直接使用处理后的位置
                            Color.White, 
                            origin: origin, // 建议加上原点
                            rotate: 0f,
                            scale: 0.25f, // 建议加上 GUI 缩放适配
                            depth: 0.01f // 深度确保在最前
                        );
                    }
                    
                }

                
            }
        }
    }
}
