﻿using Barotrauma;
using HarmonyLib;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(Structure))]
    public static class StructurePatch
    {
        // 使用 Postfix，在原版的 UpdateSections 执行完（即物理体已经生成好）后再进行修改
        [HarmonyPatch("UpdateSections")]
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Structure), "UpdateSections")]
[HarmonyPrefix]
public static void Prefix(Structure __instance)
{
    // 遍历所有 Section，寻找刚刚被彻底摧毁（但还没处理掉落）的段
    for (int i = 0; i < __instance.SectionCount; i++)
    {
        var section = __instance.GetSection(i);
        
        // 逻辑：如果这一段损坏达 100%，且我们还没给它生成过掉落物
        if (section.damage >= __instance.MaxHealth )
        {
            // 1. 获取这一段的物理位置和尺寸
            Vector2 sectionWorldPos = __instance.SectionPosition(i, true);
            Rectangle sectionRect = section.rect;

            // 2. 这里的核心逻辑：检测“连续性”
            // 向上/向下搜索连续的损坏 Section，计算出这一整块“掉落矩形”的大小
            Rectangle dropArea = CalculateBrokenArea(__instance, i);

            // 3. 生成一个新的 Item 实体（掉落物）
            // 注意：不要直接改原墙体的 Body，而是生成新物体
            Entity.Spawner.AddItemToSpawnQueue(ItemPrefab.GetItemPrefab("metalcrate"),
                sectionWorldPos, 
                onSpawned: (Item item) => 
                {
                    // 此时 item 已经是 Item 类型，不需要再进行 as 转换
                    SetItemAppearance(item, __instance, dropArea);
                }
            );

            // 4. 彻底禁用原墙体这一段的物理碰撞，防止它挡住新生成的掉落物。不需要，損壞的墻體自動沒有碰撞
           // __instance.SetSectionDamage(i, __instance.MaxHealth); 
        }
    }
}


private static Rectangle CalculateBrokenArea(Structure str, int startIndex)
{
    int left = startIndex;
    int right = startIndex;

    // 向左寻找连续损坏的 section
    while (left > 0 && str.GetSection(left - 1).damage >= str.MaxHealth) left--;
    // 向右寻找连续损坏的 section
    while (right < str.SectionCount - 1 && str.GetSection(right + 1).damage >= str.MaxHealth) right++;

    // 计算这几段 Section 合并后的总矩形[cite: 3]
    Rectangle rect = str.GetSection(left).rect;
    for (int j = left + 1; j <= right; j++)
    {
        rect = Rectangle.Union(rect, str.GetSection(j).rect);
    }
    return rect;
}

private static void SetItemAppearance(Item item, Structure structure, Rectangle dropArea)
{
    // 检查空引用，防止游戏崩溃
    if (item == null || structure?.Prefab?.Sprite == null) return;

    // 1. 获取原墙体的 Sprite
    Sprite wallSprite = structure.Prefab.Sprite;

    // 2. 将柜子的 Sprite 替换为墙体的 Sprite
    // 注意：Barotrauma 的 Item 渲染通常使用 item.Sprite
    //item.Sprite = wallSprite;

    // 3. 计算切片位置
    // 因为 Structure 支持缩放，我们需要将世界坐标下的 dropArea 映射回贴图坐标
    float scale = structure.Scale;
    Rectangle baseSource = wallSprite.SourceRect;

    // 计算相对于墙体左上角的像素偏移
    int offsetX = (int)((dropArea.X - structure.rect.X) / scale);
    int offsetY = (int)((structure.rect.Y - dropArea.Y) / scale); // Y轴通常在渲染时是反向的

    // 计算切片的宽度和高度
    int width = (int)(dropArea.Width / scale);
    int height = (int)(dropArea.Height / scale);

    // 4. 设置 Sprite 的裁剪区域
    // 这样这个 Item 在绘制时只会显示原贴图中对应的那一小块
    item.Sprite.SourceRect = new Rectangle(
        baseSource.X + offsetX,
        baseSource.Y + offsetY,
        width,
        height
    );

    // 5. 确保渲染深度正确，防止碎片埋在墙里看全不见
    item.Sprite.Depth = structure.GetDrawDepth() - 0.001f; 
}
    }
}