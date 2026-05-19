using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Barotrauma;
using System;
using Barotrauma.Items.Components;

namespace BarotraumaDieHard
{
    // 我们为你自定义的光标定义一个伪装的枚举强转，方便在代码里复用
    public static class CustomCursor
    {
        // 999 是一个原版绝对不会占用的安全数字
        public const CursorState CabinetHover = (CursorState)100;
    }


    [HarmonyPatch(typeof(GUI), "DrawCursor")]
    class DrawZoomHintCursorPatch
    {
        // 设定触发“较远距离”的屏幕像素阈值（可根据测试自行调整，如 300 像素）
        private const float DISTANCE_THRESHOLD = 900f;

        [HarmonyPrefix]
        public static bool Postfix(SpriteBatch spriteBatch)
        {
            // 1. 基础安全检查：确保窗口激活、处于游戏内（不是主菜单）
            if (!GameMain.WindowActive || Screen.Selected != GameMain.GameScreen) return true;

            // 2. 核心功能点：如果鼠标当前正悬停在任何 GUI 元素上（如物品栏、对话框等），不绘制图标
            if (GUI.MouseOn != null) return true;

            // 3. 获取玩家当前操控的角色
            Character player = Character.Controlled;
            if (player == null || player.IsDead || player.AnimController.IsAiming || player.AnimController.IsAimingMelee) return true;

            // 4. 获取鼠标及角色的屏幕坐标
            Vector2 mouseScreenPos = PlayerInput.LatestMousePosition;
            // 将角色的世界坐标投影到屏幕像素坐标上，不受镜头缩放影响
            Vector2 playerScreenPos = GameMain.GameScreen.Cam.WorldToScreen(player.WorldPosition);

            // 5. 距离判定：计算鼠标和操控角色的屏幕像素距离
            float distance = Vector2.Distance(mouseScreenPos, playerScreenPos);
            //取消：看起来有点烦人
            /*if (distance >= DISTANCE_THRESHOLD)
            {
                // 6. 提取你自制的图标管理库中的图标
                // 推荐在 InitClient 中注册一个专门的提示图标，这里假设使用 "mechanical_slot" 进行测试
                if (Main.customSprites.TryGetValue("farLookIcon", out Sprite hintSprite))
                {
                    // 设置图标相对鼠标指针右下角的偏移量（X往右移24像素，Y往下移20像素）
                    Vector2 iconOffset = new Vector2(24, 20);
                    Vector2 drawPos = mouseScreenPos + iconOffset;

                    // 配合全局 UI 缩放比例，微调你的图标大小（例如乘以一个固定系数 0.5f）
                    float scale = GUI.Scale;

                    // 使用 Barotrauma 原版 Sprite 的 Draw 封装方法进行绘制
                    // 注意：原版大鼠标处于 Deferred 模式绘制，我们在此处叠加。Depth 设为 0.0f 保证其处于最上层
                    hintSprite.Draw(
                        spriteBatch,
                        drawPos,
                        Color.White,
                        origin: Vector2.Zero, // 保持左上角对齐或按需调整
                        rotate: 0f,
                        scale: scale,
                        depth: 0.0f
                    );
                }
            }*/
            // 3. 获取玩家当前鼠标聚焦的物品 (FocusedItem 代表玩家正在看着或鼠标指着的物品)
            Item hoveredItem = player.FocusedItem;

            if (hoveredItem != null)
            {
                // 4. 判定该物品是否为“柜子”
                // 方法 A：通过 Prefab 的 Identifier（标识符）或者标签（Tags）判定
                // 游戏里绝大部分柜子都带有 "container" 标签
                bool isCabinet = hoveredItem.HasTag("container") || 
                                  hoveredItem.Prefab.Identifier.Value.Contains("cabinet") || hoveredItem.HasTag("door") || hoveredItem.HasTag("chair") || hoveredItem.HasTag("bed");

                bool canBePickedUp = hoveredItem.GetComponent<Pickable>() != null && !hoveredItem.HasTag("door") && hoveredItem.body?.Enabled == true;
                bool isEnterableDoor = hoveredItem.HasTag("zdoor");
                bool isLadder = hoveredItem.GetComponent<Ladder>() != null && !hoveredItem.HasTag("zdoor");
                bool isSearchable = hoveredItem.HasTag("junctionbox") || hoveredItem.HasTag("batterycellrecharger") || hoveredItem.HasTag("supercapacitor") || hoveredItem.HasTag("command");

                // 方法 B：或者更严谨一点，检查它有没有 ItemContainer 组件
                // bool isCabinet = hoveredItem.GetComponent<ItemContainer>() != null;

                if (isCabinet)
                {
                    // 5. 改变鼠标图案
                    // 如果你想使用原版自带的手型图标：
                    //GUI.MouseCursor = CursorState.Move;

                    // 如果你想换成你自己塞进原版字典里的自定义图标：
                    // PlayerInput.MouseCursor = (CursorState)999; 
                    // (前提是你在初始化时把自定义贴图塞进了 PlayerInput.MouseCursorSprites 字典里)
                    // 指向柜子了！提取你自制库里的图标
                    if (Main.customSprites.TryGetValue("openPalm", out Sprite myCursor))
                    {
                        // 用你自己的贴图画在鼠标上
                        Vector2 mousePos = PlayerInput.LatestMousePosition;
                        myCursor.Draw(spriteBatch, mousePos, Color.White, origin: myCursor.Origin, scale: GUI.Scale);

                        return false;
                         // 返回 false，直接把原版的默认准星“掐断”不让它画，完美实现变图案！
                    }

                        return true;
                }
                else if (canBePickedUp)
                {
                    if (Main.customSprites.TryGetValue("pickHand", out Sprite myCursor))
                    {
                        // 用你自己的贴图画在鼠标上
                        Vector2 mousePos = PlayerInput.LatestMousePosition;
                        myCursor.Draw(spriteBatch, mousePos, Color.White, origin: myCursor.Origin, scale: GUI.Scale);

                        return false;
                    }
                }
                else if (isEnterableDoor)
                {
                    if (Main.customSprites.TryGetValue("doorHandle", out Sprite myCursor))
                    {
                        // 用你自己的贴图画在鼠标上
                        Vector2 mousePos = PlayerInput.LatestMousePosition;
                        myCursor.Draw(spriteBatch, mousePos, Color.White, origin: myCursor.Origin, scale: GUI.Scale);

                        return false;
                    }
                }
                else if (isLadder)
                {
                    if (Main.customSprites.TryGetValue("ladderClimbIcon", out Sprite myCursor))
                    {
                        // 用你自己的贴图画在鼠标上
                        Vector2 mousePos = PlayerInput.LatestMousePosition;
                        myCursor.Draw(spriteBatch, mousePos, Color.White, origin: myCursor.Origin, scale: GUI.Scale);

                        return false;
                    }
                }
                else if (isSearchable)
                {
                    if (Main.customSprites.TryGetValue("magnifierIcon", out Sprite myCursor))
                    {
                        // 用你自己的贴图画在鼠标上
                        Vector2 mousePos = PlayerInput.LatestMousePosition;
                        myCursor.Draw(spriteBatch, mousePos, Color.White, origin: myCursor.Origin, scale: GUI.Scale);

                        return false;
                    }
                }

                return true;
            }


                return true;

        }
    }
}