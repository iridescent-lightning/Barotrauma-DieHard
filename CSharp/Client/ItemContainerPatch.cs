﻿using System;
using System.Reflection;
using Barotrauma;
using Barotrauma.Items.Components;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BarotraumaMod.Patches
{
    [HarmonyPatch(typeof(ItemContainer))]
    public static class DrawContainedItemsPatch
    {
        // 使用 Prefix 补丁，返回 false 以完全拦截并替代原版方法
		[HarmonyPatch("DrawContainedItems")]
        [HarmonyPrefix]
        public static bool Prefix(ItemContainer __instance, SpriteBatch spriteBatch, float itemDepth, Color? overrideColor)
        {
            // 1. 仿照原版开头的安全检查（部分字段在局部文件未提供，此处通过反射或原类公开属性获取）
            // 注意：__instance.item 是基类 ItemComponent 的公开字段
            var item = __instance.Item; 

			

			if (__instance.Item?.HasTag("ItemsUseInventoryPlacement") == false)
            {
                return true; 
            }
            
            // hideItems 是私有字段，需要使用 Traverse 获取
            bool hideItems = Traverse.Create(__instance).Field<bool>("hideItems").Value;
            if (hideItems || (item.body != null && !item.body.Enabled)) 
            {
                return false; // 拦截原版，直接返回
            }

            var rootBody = item.RootContainer?.body ?? item.body;

            // 2. 获取位置和间距信息（通过反射调用私有方法/获取私有字段）
            var traverse = Traverse.Create(__instance);
            
            // 调用私有方法 GetContainedPosition 
            object[] getPosArgs = new object[] { true, null, null, null, null };
            Vector2 transformedItemPos = (Vector2)typeof(ItemContainer)
                .GetMethod("GetContainedPosition", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(__instance, getPosArgs);

            Vector2 transformedItemIntervalHorizontal = (Vector2)getPosArgs[1];
            Vector2 transformedItemIntervalVertical = (Vector2)getPosArgs[2];
            bool flippedX = (bool)getPosArgs[3];
            bool flippedY = (bool)getPosArgs[4];

            // 获取私有或共有属性
            Vector2 itemInterval = traverse.Property<Vector2>("ItemInterval").Value;
            int itemsPerRow = traverse.Property<int>("ItemsPerRow").Value;
            float itemRotation = traverse.Property<float>("ItemRotation").Value;
            bool autoInteractWithContained = traverse.Property<bool>("AutoInteractWithContained").Value;
            float[] containedSpriteDepths = traverse.Field<float[]>("containedSpriteDepths").Value;

            // 3. 执行你提供的全新槽位对应绘制逻辑
            bool isWiringMode = SubEditorScreen.TransparentWiringMode && SubEditorScreen.IsWiringMode();
            int total = 0;

            for (int i = 0; i < __instance.Inventory.Capacity; i++)
            {
                Item containedItem = __instance.Inventory.GetItemAt(i);
                if (containedItem is null) { continue; }
                if (containedItem.Sprite == null) { continue; }

                // 自动互动高亮逻辑
                if (autoInteractWithContained && Screen.Selected is not { IsEditor: true })
                {
                    containedItem.IsHighlighted = item.IsHighlighted;
                    item.IsHighlighted = false;
                }

                // 布局原点计算
                Vector2 origin = containedItem.Sprite.Origin;
                if (flippedX) { origin.X = containedItem.Sprite.SourceRect.Width - origin.X; }
                if (flippedY) { origin.Y = containedItem.Sprite.SourceRect.Height - origin.Y; }

                // 计算当前物品应该在的格子坐标
                Vector2 currentItemPos = transformedItemPos;
				//int targetPosition = __instance.ItemsUseInventoryPlacement ? i : total;
                int targetPosition = i;
                
                if (Math.Abs(itemInterval.X) > 0.001f && Math.Abs(itemInterval.Y) > 0.001f)
                {
                    // 轴向都有间隔 -> 采用网格布局
                    currentItemPos += transformedItemIntervalHorizontal * (targetPosition % itemsPerRow);
                    currentItemPos += transformedItemIntervalVertical * (targetPosition / itemsPerRow);
                }
                else
                {
                    // 单向间隔
                    // 原版里如果是单向，是用 (horizontal + vertical) * index
                    currentItemPos += (transformedItemIntervalHorizontal + transformedItemIntervalVertical) * targetPosition;
                }
                total++;

                // 深度计算
                float containedSpriteDepth = __instance.ContainedSpriteDepth < 0.0f ? containedItem.Sprite.Depth : __instance.ContainedSpriteDepth;
                if (targetPosition < containedSpriteDepths.Length)
                {
                    containedSpriteDepth = containedSpriteDepths[targetPosition];
                }
                containedSpriteDepth = itemDepth + (containedSpriteDepth - (item.Sprite?.Depth ?? item.SpriteDepth)) / 10000.0f;

                // 翻转效果
                SpriteEffects spriteEffects = SpriteEffects.None;
                float spriteRotation = itemRotation;
                
                // 由于重写移除了 ContainedItem 包装类，若原版有自定义旋转，可以通过物品自身属性或默认零旋转
                // 这里采用默认的布局旋转
                bool flipX = rootBody is { Dir: -1 } || flippedX;
                if (flipX)
                {
                    spriteEffects |= SpriteEffects.FlipHorizontally;
                }
                if (flippedY)
                {
                    spriteEffects |= SpriteEffects.FlipVertically;
                }

                // 绘制物品主体
                containedItem.Sprite.Draw(
                    spriteBatch,
                    new Vector2(currentItemPos.X, -currentItemPos.Y),
                    overrideColor ?? (isWiringMode ? containedItem.GetSpriteColor(withHighlight: true) * 0.15f : containedItem.GetSpriteColor(withHighlight: true)),
                    origin,
                    -(containedItem.body == null ? 0.0f : containedItem.body.DrawRotation),
                    containedItem.Scale,
                    spriteEffects,
                    depth: containedSpriteDepth);

                // 绘制装饰性贴图
                containedItem.DrawDecorativeSprites(spriteBatch, currentItemPos, flipX, flippedY, (containedItem.body == null ? 0.0f : containedItem.body.DrawRotation), 
                    containedSpriteDepth, overrideColor);

                // 递归绘制子容器内的物品
                foreach (ItemContainer ic in containedItem.GetComponents<ItemContainer>())
                {
                    if (ic.hideItems) continue;
                    ic.DrawContainedItems(spriteBatch, containedSpriteDepth, overrideColor);
                }
            }

            return false; // 返回 false 阻止原版方法运行
        }

	
    }
}