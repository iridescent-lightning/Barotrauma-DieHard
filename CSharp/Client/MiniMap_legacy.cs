﻿using Barotrauma.Extensions;
using Barotrauma;
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Networking;

using FarseerPhysics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace BarotraumaDieHard
{
    partial class MiniMapLegacy : Powered
    {
        public enum MonitorMode
        {
            NormalMap, // 传统状态监视器地图
            Buoyancy   // 浮力实时监控页面
        }
        private MonitorMode currentMode = MonitorMode.NormalMap; // 默认显示地图
        private List<GUIButton> modeButtons = new List<GUIButton>(); // 存储按钮以便控制选中状态
        private GUIFrame submarineContainer;

        private GUIFrame hullInfoFrame;

        private GUITextBlock hullNameText, hullBreachText, hullAirQualityText, hullWaterText, hullCO2Text, hullCOText, hullChlorineText, lockRoomHitText, temperatureText;

        private string noPowerTip = "";

        private readonly List<Submarine> displayedSubs = new List<Submarine>();

        private Point prevResolution;

        private bool showBuoyancyInfo = false; // 是否显示浮力监视器

        partial void InitProjSpecific(ContentXElement element)
        {
            noPowerTip = TextManager.Get("SteeringNoPowerTip").Value;
            CreateGUI();
        }

        public override void CreateGUI()
        {
            GuiFrame.RectTransform.RelativeOffset = new Vector2(0.05f, 0.0f);
            GuiFrame.CanBeFocused = true;
            new GUICustomComponent(new RectTransform(GuiFrame.Rect.Size - GUIStyle.ItemFrameMargin, GuiFrame.RectTransform, Anchor.Center) { AbsoluteOffset = GUIStyle.ItemFrameOffset },
                DrawHUDBack, null);
            submarineContainer = new GUIFrame(new RectTransform(new Vector2(0.95f, 0.9f), GuiFrame.RectTransform, Anchor.Center), style: null);

            new GUICustomComponent(new RectTransform(GuiFrame.Rect.Size - GUIStyle.ItemFrameMargin, GuiFrame.RectTransform, Anchor.Center) { AbsoluteOffset = GUIStyle.ItemFrameOffset },
                DrawHUDFront, null)
            {
                CanBeFocused = false
            };

            hullInfoFrame = new GUIFrame(new RectTransform(new Vector2(0.13f, 0.13f), GUI.Canvas, minSize: new Point(250, 150)),
                style: "GUIToolTip")
            {
                CanBeFocused = false
            };
            var hullInfoContainer = new GUILayoutGroup(new RectTransform(new Vector2(0.9f, 0.9f), hullInfoFrame.RectTransform, Anchor.Center))
            {
                Stretch = true,
                RelativeSpacing = 0.05f
            };

            hullNameText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.4f), hullInfoContainer.RectTransform), "") { Wrap = true };
            hullBreachText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.3f), hullInfoContainer.RectTransform), "") { Wrap = true };
            hullAirQualityText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.3f), hullInfoContainer.RectTransform), "") { Wrap = true };
            hullWaterText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.3f), hullInfoContainer.RectTransform), "") { Wrap = true };

            //moded part
            hullCO2Text = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.3f), hullInfoContainer.RectTransform), "") { Wrap = true };
            hullCOText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.3f), hullInfoContainer.RectTransform), "") { Wrap = true };
            hullChlorineText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.3f), hullInfoContainer.RectTransform), "") { Wrap = true };
            
            temperatureText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.6f), hullInfoContainer.RectTransform), "") { Wrap = true };
            lockRoomHitText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.9f), hullInfoContainer.RectTransform), "") { Wrap = true };

            hullInfoFrame.Children.ForEach(c =>
            {
                c.CanBeFocused = false;
                c.Children.ForEach(c2 => c2.CanBeFocused = false);
            });

            // ==============================================================
            // ======== 新增：创建最新原版风格的页面切换按钮布局 ========
            // ==============================================================
            var buttonGroup = new GUILayoutGroup(new RectTransform(new Vector2(0.35f, 0.05f), GuiFrame.RectTransform, Anchor.TopLeft)
            {
                AbsoluteOffset = new Point(25, 20) // 略微放大偏移，规避顶部边框
            }, isHorizontal: true, childAnchor: Anchor.CenterLeft)
            {
                RelativeSpacing = 0.02f
            };

            // 1. 创建“系统状态”切换按钮
            var mapBtn = new GUIButton(new RectTransform(new Vector2(0.48f, 1.0f), buttonGroup.RectTransform), TextManager.Get("MiniMapModeNormal").Value, style: "GUIButton")
            {
                Selected = true,
                OnClicked = (btn, obj) =>
                {
                    SwitchMonitorMode(MonitorMode.NormalMap);
                    return true;
                }
            };
            modeButtons.Add(mapBtn);

            // 2. 创建“浮力监控”切换按钮
            var buoyancyBtn = new GUIButton(new RectTransform(new Vector2(0.48f, 1.0f), buttonGroup.RectTransform), TextManager.Get("MiniMapModeBuoyancy").Value, style: "GUIButton")
            {
                OnClicked = (btn, obj) =>
                {
                    SwitchMonitorMode(MonitorMode.Buoyancy);
                    return true;
                }
            };
            modeButtons.Add(buoyancyBtn);
            
            if (submarineContainer != null)
            {
                // 地图页面时向下偏移，腾出顶部空间给导航按钮
                submarineContainer.RectTransform.RelativeOffset = Vector2.Zero;
            }
        }

        public override void AddToGUIUpdateList(int order = 0)
        {
            base.AddToGUIUpdateList(order);
            if (currentMode == MonitorMode.NormalMap)
            {
                hullInfoFrame?.AddToGUIUpdateList(order: order + 1);
            }
        }

        private void CreateHUD()
        {
            prevResolution = new Point(GameMain.GraphicsWidth, GameMain.GraphicsHeight);
            submarineContainer?.ClearChildren();

            if (item.Submarine == null) { return; }

            item.Submarine.CreateMiniMap(submarineContainer);
            displayedSubs.Clear();
            displayedSubs.Add(item.Submarine);
            displayedSubs.AddRange(item.Submarine.DockedTo);
        }

        public override void UpdateHUDComponentSpecific(Character character, float deltaTime, Camera cam)
        {
            if ((item.Submarine == null && displayedSubs.Count > 0) ||                                       
                !displayedSubs.Contains(item.Submarine) ||                                                   
                prevResolution.X != GameMain.GraphicsWidth || prevResolution.Y != GameMain.GraphicsHeight || 
                item.Submarine.DockedTo.Any(s => !displayedSubs.Contains(s)) ||                              
                !submarineContainer.Children.Any() ||                                                        
                displayedSubs.Any(s => s != item.Submarine && !item.Submarine.DockedTo.Contains(s)))         
            {
                CreateHUD();
            }
            
            float distort = 1.0f - item.Condition / item.MaxCondition;
            foreach (HullData hullData in hullDatas.Values)
            {
                hullData.DistortionTimer -= deltaTime;
                if (hullData.DistortionTimer <= 0.0f)
                {
                    hullData.Distort = Rand.Range(0.0f, 1.0f) < distort * distort;
                    if (hullData.Distort)
                    {
                        hullData.Oxygen = Rand.Range(0.0f, 100.0f);
                        hullData.Water = Rand.Range(0.0f, 1.0f);
                    }
                    hullData.DistortionTimer = Rand.Range(1.0f, 10.0f);
                }
            }
        }

        private void DrawHUDBack(SpriteBatch spriteBatch, GUICustomComponent container)
        {
            // 【核心修正】如果切换到了浮力监控页面，直接不绘制任何底层房间色块、锁定指示器与潜艇外轮廓
            if (currentMode == MonitorMode.Buoyancy)
            {
                hullInfoFrame.Visible = false;
                return;
            }

            float flashSpeed = 5.0f; 
            float flashIntensity = (float)Math.Sin(Timing.TotalTime * flashSpeed) * 0.5f + 0.5f;

            Hull mouseOnHull = null;
            hullInfoFrame.Visible = false;
            
            foreach (Hull hull in Hull.HullList)
            {
                var hullFrame = submarineContainer.Children.FirstOrDefault()?.FindChild(hull);
                if (hullFrame == null) { continue; }

                bool allDoorsLocked = true; 

                foreach (Gap gap in hull.ConnectedGaps)
                {
                    if (gap.IsRoomToRoom)
                    {
                        var door = gap.ConnectedDoor;
                        if (door != null && door.Item.InPlayerSubmarine && !door.IsJammed)
                        {
                            allDoorsLocked = false; 
                            break; 
                        }
                    }
                }

                if (allDoorsLocked && hull.Submarine == Submarine.MainSub)
                {
                    Rectangle lockedIndicatorRect = new Rectangle(hullFrame.Rect.X, hullFrame.Rect.Y, 10, 10);
                    spriteBatch.Draw(GUI.WhiteTexture, lockedIndicatorRect, Color.Red);
                }

                if (GUI.MouseOn == hullFrame || hullFrame.IsParentOf(GUI.MouseOn))
                {
                    mouseOnHull = hull;
                    hullFrame.Color = Color.White; 
                    
                    if (PlayerInput.PrimaryMouseButtonClicked())
                    {
                        if (PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl)) 
                        {
                            UnlockAllDoors(mouseOnHull); 
                        }
                        else
                        {
                            OnHullClick(mouseOnHull); 
                        }
                    }
                }

                if (item.Submarine == null || !hasPower)
                {
                    hullFrame.Color = Color.DarkCyan * 0.3f;
                    if (hullFrame.Children != null && hullFrame.Children.Any()) 
                    {
                        hullFrame.Children.First().Color = Color.DarkCyan * 0.3f;
                    }
                }

                DrawPersonalIndicators(spriteBatch, hull);
            }

            if (Voltage < MinVoltage) return;

            float scale = 1.0f;
            HashSet<Submarine> subs = new HashSet<Submarine>();
            foreach (Hull hull in Hull.HullList)
            {
                if (hull.Submarine == null) { continue; }
                var hullFrame = submarineContainer.Children.FirstOrDefault()?.FindChild(hull);
                if (hullFrame == null) { continue; }

                hullFrame.Visible = true;
                if (!submarineContainer.Rect.Contains(hullFrame.Rect))
                {
                    if (hull.Submarine.Info.Type != SubmarineType.Player) 
                    {
                        hullFrame.Visible = false;
                        continue; 
                    }
                }

                hullDatas.TryGetValue(hull, out HullData hullData);
                if (hullData == null)
                {
                    hullData = new HullData();
                    GetLinkedHulls(hull, hullData.LinkedHulls);
                    hullDatas.Add(hull, hullData);
                }
                
                Color neutralColor = Color.DarkCyan;
                if (hull.IsWetRoom)
                {
                    neutralColor = new Color(9, 80, 159);
                }

                if (hullData.Distort && hullFrame.Children != null && hullFrame.Children.Any())
                {
                    hullFrame.Children.First().Color = Color.Lerp(Color.Black, Color.DarkGray * 0.5f, Rand.Range(0.0f, 1.0f));
                    hullFrame.Color = neutralColor * 0.5f;
                    continue;
                }
                
                subs.Add(hull.Submarine);
                scale = Math.Min(
                    hullFrame.Parent.Rect.Width / (float)hull.Submarine.Borders.Width, 
                    hullFrame.Parent.Rect.Height / (float)hull.Submarine.Borders.Height);
                
                Color borderColor = neutralColor;
                
                float? gapOpenSum = 0.0f;
                if (ShowHullIntegrity)
                {
                    gapOpenSum = hull.ConnectedGaps.Where(g => !g.IsRoomToRoom).Sum(g => g.Open);
                    borderColor = Color.Lerp(neutralColor, GUIStyle.Red, Math.Min((float)gapOpenSum, 1.0f));
                }

                float? oxygenAmount = null;
                if (!RequireOxygenDetectors || hullData?.Oxygen != null)
                {
                    oxygenAmount = RequireOxygenDetectors ? hullData.Oxygen : hull.OxygenPercentage;
                    GUI.DrawRectangle(
                        spriteBatch, hullFrame.Rect, 
                        Color.Lerp(GUIStyle.Red * 0.5f, GUIStyle.Green * 0.3f, (float)oxygenAmount / 100.0f), 
                        true);
                }

                float? waterAmount = null;
                if (!RequireWaterDetectors || hullData.Water != null)
                {
                    waterAmount = RequireWaterDetectors ? hullData.Water : Math.Min(hull.WaterVolume / hull.Volume, 1.0f);
                    if (hullFrame.Rect.Height * waterAmount > 3.0f)
                    {
                        Rectangle waterRect = new Rectangle(
                            hullFrame.Rect.X, (int)(hullFrame.Rect.Y + hullFrame.Rect.Height * (1.0f - waterAmount)),
                            hullFrame.Rect.Width, (int)(hullFrame.Rect.Height * waterAmount));

                        waterRect.Inflate(-3, -3);

                        GUI.DrawRectangle(spriteBatch, waterRect, new Color(85, 136, 147), true);
                        GUI.DrawLine(spriteBatch, new Vector2(waterRect.X, waterRect.Y), new Vector2(waterRect.Right, waterRect.Y), Color.LightBlue);
                    }
                }

                if (mouseOnHull == hull || hullData.LinkedHulls.Contains(mouseOnHull))
                {
                    if (hullFrame.Children != null && hullFrame.Children.Any())
                    {
                        borderColor = Color.Lerp(borderColor, Color.White, 0.5f);
                        hullFrame.Children.First().Color = Color.White;
                        hullFrame.Color = borderColor;
                    }
                }
                else
                {
                    if (hullFrame.Children != null && hullFrame.Children.Any())
                    {
                        hullFrame.Children.First().Color = neutralColor * 0.8f;
                    }
                }

                if (mouseOnHull == hull)
                {
                    hullInfoFrame.RectTransform.ScreenSpaceOffset = hullFrame.Rect.Center;
                    if (hullInfoFrame.Rect.Right > GameMain.GraphicsWidth) { hullInfoFrame.RectTransform.ScreenSpaceOffset -= new Point(hullInfoFrame.Rect.Width, 0); }
                    if (hullInfoFrame.Rect.Bottom > GameMain.GraphicsHeight) { hullInfoFrame.RectTransform.ScreenSpaceOffset -= new Point(0, hullInfoFrame.Rect.Height); }

                    hullInfoFrame.Visible = true;
                    hullNameText.Text = hull.DisplayName;

                    foreach (Hull linkedHull in hullData.LinkedHulls)
                    {
                        gapOpenSum += linkedHull.ConnectedGaps.Where(g => !g.IsRoomToRoom).Sum(g => g.Open);
                        oxygenAmount += linkedHull.OxygenPercentage;
                        waterAmount += Math.Min(linkedHull.WaterVolume / linkedHull.Volume, 1.0f);
                    }
                    oxygenAmount /= (hullData.LinkedHulls.Count + 1);
                    waterAmount /= (hullData.LinkedHulls.Count + 1);

                    hullBreachText.Text = gapOpenSum > 0.1f ? TextManager.Get("MiniMapHullBreach") : "";
                    hullBreachText.TextColor = GUIStyle.Red;

                    hullAirQualityText.Text = oxygenAmount == null ? TextManager.Get("MiniMapAirQualityUnavailable") :
                        TextManager.AddPunctuation(':', TextManager.Get("MiniMapAirQuality"), + (int)oxygenAmount + " %");
                    hullAirQualityText.TextColor = oxygenAmount == null ? GUIStyle.Red : Color.Lerp(GUIStyle.Red, Color.LightGreen, (float)oxygenAmount / 100.0f);

                    hullWaterText.Text = waterAmount == null ? TextManager.Get("MiniMapWaterLevelUnavailable") : 
                        TextManager.AddPunctuation(':', TextManager.Get("MiniMapWaterLevel"), (int)(waterAmount * 100.0f) + " %");
                    hullWaterText.TextColor = waterAmount == null ? GUIStyle.Red : Color.Lerp(Color.LightGreen, GUIStyle.Red, (float)waterAmount);
                    
                    float co2Amount = HullMod.GetGas(hull, "CO2");
                    float coAmount = HullMod.GetGas(hull, "CO");
                    float chlorineAmount = HullMod.GetGas(hull, "Chlorine");
                    float temperature = HullMod.GetGas(hull, "Temperature");

                    float celsiusTemperature = (float)temperature - 273.15f;
                    string formattedTemperature = celsiusTemperature.ToString("0.0") + " °C";

                    temperatureText.Text = temperature == null ? TextManager.Get("MiniMapAirQualityUnavailable") :
                    TextManager.AddPunctuation(':', TextManager.Get("MiniMapTemperature"), formattedTemperature);

                    hullCO2Text.Text = co2Amount == null ? TextManager.Get("MiniMapAirQualityUnavailable") :
                    TextManager.AddPunctuation(':', TextManager.Get("MiniMapCO2"), (int)co2Amount + " ppm");
                    hullCO2Text.TextColor = co2Amount == null ? GUIStyle.Red : Color.Lerp(GUIStyle.Green, Color.Red, (float)co2Amount / 100.0f);

                    hullCOText.Text = coAmount == null ? TextManager.Get("MiniMapAirQualityUnavailable") :
                        TextManager.AddPunctuation(':', TextManager.Get("MiniMapCO"), (int)coAmount + " ppm");
                    hullCOText.TextColor = coAmount == null ? GUIStyle.Red : Color.Lerp(GUIStyle.Green, Color.Red, (float)coAmount / 100.0f);

                    hullChlorineText.Text = chlorineAmount == null ? TextManager.Get("MiniMapAirQualityUnavailable") :
                        TextManager.AddPunctuation(':', TextManager.Get("MiniMapChlorine"), (int)chlorineAmount + " ppm");
                    hullChlorineText.TextColor = chlorineAmount == null ? GUIStyle.Red : Color.Lerp(GUIStyle.Green, Color.Red, (float)chlorineAmount / 100.0f);

                    lockRoomHitText.Text = TextManager.Get("MiniMapLockRoomHit");
                }
                
                hullFrame.Color = borderColor;
            }
            
            foreach (Submarine sub in subs)
            {
                if (sub.HullVertices == null || sub.Info.IsOutpost) { continue; }
                
                Rectangle worldBorders = sub.GetDockedBorders();
                worldBorders.Location += sub.WorldPosition.ToPoint();
                
                scale = Math.Min(
                    submarineContainer.Rect.Width / (float)worldBorders.Width,
                    submarineContainer.Rect.Height / (float)worldBorders.Height) * 0.9f;

                float displayScale = ConvertUnits.ToDisplayUnits(scale);
                Vector2 offset = ConvertUnits.ToSimUnits(sub.WorldPosition - new Vector2(worldBorders.Center.X, worldBorders.Y - worldBorders.Height / 2));
                Vector2 center = container.Rect.Center.ToVector2();
                
                for (int i = 0; i < sub.HullVertices.Count; i++)
                {
                    Vector2 start = (sub.HullVertices[i] + offset) * displayScale;
                    start.Y = -start.Y;
                    Vector2 end = (sub.HullVertices[(i + 1) % sub.HullVertices.Count] + offset) * displayScale;
                    end.Y = -end.Y;
                    GUI.DrawLine(spriteBatch, center + start, center + end, Color.DarkCyan * Rand.Range(0.3f, 0.35f), width: (int)(10 * GUI.Scale));
                }
            }
        }

        private void DrawHUDFront(SpriteBatch spriteBatch, GUICustomComponent container)
        {
            if (!HasPower || item.Submarine == null) return;

            // ==============================================================
            // 情况 A：系统状态常规地图
            // ==============================================================
            if (currentMode == MonitorMode.NormalMap)
            {
                // [此处为你保留的传统地图前端文字绘制预留区，由于你删去了大部分，此处为空即可]
                return; 
            }

            // ==============================================================
            // 情况 B：浮力监控控制台模式（独立紧凑排版，使用中型字体防止文字溢出）
            // ==============================================================
            if (currentMode == MonitorMode.Buoyancy)
            {
                var submarine = item.Submarine;
                var connectedSubs = submarine.GetConnectedSubs();
                
                float ballastWaterVolume = 0f;
                float ballastVolume = 0f;
                float nonBallastVolume = 0f;
                float realNonBallastWaterVolume = 0f;
                float nonBallastEffectiveWater = 0f;

                float floodTolerance = 0.15f; 
                

                if (item.Submarine.Info.SubmarineClass == SubmarineClass.Scout)
                {
                    floodTolerance = 0.17f;
                }
                else if (item.Submarine.Info.SubmarineClass == SubmarineClass.Attack)
                {
                    floodTolerance = 0.15f;
                }
                else if (item.Submarine.Info.SubmarineClass == SubmarineClass.Transport)
                {
                    floodTolerance = 0.25f;
                }
                else if (item.Submarine.Info.SubmarineClass == SubmarineClass.Undefined)
                {
                    floodTolerance = 0.1f;
                }
                else
                {
                    floodTolerance = 0f;
                }

                float nonBallastMultiplier = BarotraumaDieHard.BuoyancyConfig.NonBallastFloodMultiplier; 

                foreach (Hull hull in Hull.HullList)
                {
                    if (hull.Submarine == null || !connectedSubs.Contains(hull.Submarine)) continue;
                    if (hull.Submarine.PhysicsBody is not { BodyType: FarseerPhysics.BodyType.Dynamic }) continue;

                    bool isBallast = hull.RoomName != null && hull.RoomName.ToLower().Contains("ballast");

                    if (isBallast)
                    {
                        ballastVolume += hull.Volume;
                        ballastWaterVolume += hull.WaterVolume;
                    }
                    else
                    {
                        nonBallastVolume += hull.Volume;
                        realNonBallastWaterVolume += hull.WaterVolume;
                        
                        float floodRatio = hull.Volume > 0 ? hull.WaterVolume / hull.Volume : 0;
                        if (floodRatio > floodTolerance)
                        {
                            float excessFloodVolume = (floodRatio - floodTolerance) * hull.Volume;
                            nonBallastEffectiveWater += excessFloodVolume * nonBallastMultiplier;
                        }
                    }
                }

                float totalEffectiveWater = ballastWaterVolume + nonBallastEffectiveWater;
                float totalEffectiveVolume = ballastVolume + nonBallastVolume;

                if (totalEffectiveVolume > 0f)
                {
                    float realNonBallastWaterPercentage = nonBallastVolume > 0f ? realNonBallastWaterVolume / nonBallastVolume : 0f;
                    float waterPercentage = totalEffectiveWater / totalEffectiveVolume;
                    float buoyancy = Barotrauma.SubmarineBody.NeutralBallastPercentage - waterPercentage;
                    buoyancy = MathHelper.Clamp(buoyancy, -0.5f, 0.2f);
                    
                    float totalMass = connectedSubs.Sum(s => s.SubBody?.Body?.Mass ?? 0f);
                    float massRatio = submarine.SubBody != null ? submarine.SubBody.Body.Mass / (totalMass > 0 ? totalMass : 1f) : 1f;
                    float forceY = buoyancy * totalMass * 10f * massRatio;

                    // --- 多语言状态诊断文本处理 ---
                    string statusString;
                    Color statusColor;
                    if (forceY < -1200f)
                    {
                        statusString = TextManager.Get("MiniMapBuoyancyStatusSevere").Value;
                        statusColor = GUIStyle.Red;
                    }
                    else if (forceY < -200f)
                    {
                        statusString = TextManager.Get("MiniMapBuoyancyStatusWarning").Value;
                        statusColor = Color.Orange;
                    }
                    else if (forceY > 500f)
                    {
                        statusString = TextManager.Get("MiniMapBuoyancyStatusSurfacing").Value;
                        statusColor = Color.Cyan;
                    }
                    else
                    {
                        statusString = TextManager.Get("MiniMapBuoyancyStatusBalanced").Value;
                        statusColor = Color.LightGreen;
                    }

                    // --- 多语言排版拼接 ---
                    string textHeader = $"=== [ {TextManager.Get("MiniMapBuoyancyHeader").Value} ] ===";
                    
                    string textShipInfo = TextManager.Get("MiniMapBuoyancyShipInfo").Value
                        .Replace("[subname]", submarine.Info.Name)
                        .Replace("[subclass]", submarine.Info.SubmarineClass.ToString())
                        .Replace("[tolerance]", (floodTolerance * 100).ToString("F0"))
                        .Replace("[multiplier]", nonBallastMultiplier.ToString("F2"));
                    
                    string textData1 = $" [ {TextManager.Get("MiniMapBuoyancyCategoryTitle").Value} ]\n" +
                                    TextManager.Get("MiniMapBuoyancyBallastWater").Value.Replace("[water]", ballastWaterVolume.ToString("F0")).Replace("[vol]", ballastVolume.ToString("F0")) + "\n" +
                                    TextManager.Get("MiniMapBuoyancyNonBallastWater").Value.Replace("[pct]", (realNonBallastWaterPercentage * 100).ToString("F1")).Replace("[water]", realNonBallastWaterVolume.ToString("F0")).Replace("[vol]", nonBallastVolume.ToString("F0")) + "\n" +
                                    TextManager.Get("MiniMapBuoyancyEffectiveWater").Value.Replace("[water]", nonBallastEffectiveWater.ToString("F0"));
                    
                    string textData2 = $" [ {TextManager.Get("MiniMapBuoyancyResultTitle").Value} ]\n" +
                                    TextManager.Get("MiniMapBuoyancyTotalWaterPct").Value.Replace("[pct]", (waterPercentage * 100).ToString("F1")) + "\n" +
                                    TextManager.Get("MiniMapBuoyancyNetValue").Value.Replace("[buoyancy]", buoyancy.ToString("F3")).Replace("[neutral]", Barotrauma.SubmarineBody.NeutralBallastPercentage.ToString("F2")) + "\n" +
                                    TextManager.Get("MiniMapBuoyancyForceY").Value.Replace("[force]", forceY.ToString("F0"));

                    // 定位整个画面的安全显示起始区域
                    Vector2 basePos = GuiFrame.Rect.Location.ToVector2() + new Vector2(35f, 75f);
                    float lineSpacing = 26f * GUI.Scale;

                    // 1. 绘制标题
                    DrawStringWithShadow(spriteBatch, basePos, textHeader, Color.Green, GUIStyle.SmallFont);
                    basePos.Y += lineSpacing * 1.5f;

                    // 2. 绘制基础状态
                    DrawStringWithShadow(spriteBatch, basePos, textShipInfo, Color.Green, GUIStyle.SmallFont);
                    basePos.Y += lineSpacing * 2.5f;

                    // 3. 绘制详细数据
                    DrawStringWithShadow(spriteBatch, basePos, textData1, Color.Green, GUIStyle.SmallFont);
                    basePos.Y += lineSpacing * 2.5f;

                    DrawStringWithShadow(spriteBatch, basePos, textData2, Color.Green, GUIStyle.SmallFont);
                    basePos.Y += lineSpacing * 2.5f;

                    // 4. 绘制最底部的状态评级提示
                    DrawStringWithShadow(spriteBatch, basePos, "--------------------------------------------------------", Color.Green, GUIStyle.SmallFont);
                    basePos.Y += lineSpacing;
                    //DrawStringWithShadow(spriteBatch, basePos, $"{TextManager.Get("MiniMapBuoyancyDiagnosis").Value} : {statusString}", statusColor, GUIStyle.SmallFont);
                }
            }
        }

        // 辅助阴影绘制函数，确保强光或复杂背景下文字极度清晰
        private void DrawStringWithShadow(SpriteBatch spriteBatch, Vector2 pos, string text, Color color, GUIFont font)
        {
            GUI.DrawString(spriteBatch, pos + Vector2.One, text, Color.Black * 0.85f, Color.Transparent, font: font);
            GUI.DrawString(spriteBatch, pos, text, color, Color.Transparent, font: font);
        }

        float xAdjuster = 1.0f; 
        float yAdjuster = 1.0f; 
        float xOffset = 20.0f;   
        float yOffset = 15.0f;   

        private void DrawPersonalIndicators(SpriteBatch spriteBatch, Hull hull)
        {
            var hullFrame = submarineContainer.Children.FirstOrDefault()?.FindChild(hull);
            if (hullFrame == null) { return; }

            Vector2 hullWorldSize = hull.Rect.Size.ToVector2();
            Vector2 hullScreenSize = hullFrame.Rect.Size.ToVector2();
            Vector2 scaleRatio = hullScreenSize / hullWorldSize;

            foreach (Character character in Character.CharacterList)
            {
                if (character.CurrentHull != hull || character.CurrentHull.Submarine != Submarine.MainSub) { continue; }

                Vector2 relativePos = (character.WorldPosition - hull.WorldPosition) / hullWorldSize;
                Vector2 indicatorPos = new Vector2(
                    hullFrame.Rect.X + (hullFrame.Rect.Width * relativePos.X * scaleRatio.X * xAdjuster) + xOffset,
                    hullFrame.Rect.Y + (hullFrame.Rect.Height * (1 - relativePos.Y) * scaleRatio.Y * yAdjuster) + yOffset 
                );

                Rectangle indicatorRect = new Rectangle((int)indicatorPos.X - 5, (int)indicatorPos.Y - 5, 10, 10);
                spriteBatch.Draw(GUI.WhiteTexture, indicatorRect, Color.Green);
            }
        }

        private void OnHullClick(Hull clickedHull)
        {
            foreach (Gap gap in clickedHull.ConnectedGaps)
            {
                if (gap.IsRoomToRoom)
                {
                    var door = gap.ConnectedDoor; 
                    if (door != null && door.Item.InPlayerSubmarine)
                    {
                        if (door.OpenState == 0f)
                        {
                            door.IsJammed = true;
                            SendDoorJammedMessage(door.Item, true); 
                        }
                        else if (door.OpenState == 1f)
                        {
                            door.SetState(false, true, true, false); 
                            door.IsJammed = true;
                            SendDoorJammedMessage(door.Item, true); 
                        }
                    }
                }
            }
        }

        private void UnlockAllDoors(Hull clickedHull)
        {
            foreach (Gap gap in clickedHull.ConnectedGaps)
            {
                if (gap.IsRoomToRoom)
                {
                    var door = gap.ConnectedDoor; 
                    if (door != null && door.Item.InPlayerSubmarine)
                    {
                        door.IsJammed = false; 
                        SendDoorJammedMessage(door.Item, false); 
                    }
                }
            }
        }

        private void GetLinkedHulls(Hull hull, List<Hull> linkedHulls)
        {
            foreach (var linkedEntity in hull.linkedTo)
            {
                if (linkedEntity is Hull linkedHull)
                {
                    if (linkedHulls.Contains(linkedHull)) { continue; }
                    linkedHulls.Add(linkedHull);
                    GetLinkedHulls(linkedHull, linkedHulls);
                }
            }
        }

        private static void SendDoorJammedMessage(Item item, bool isJammed)
        {
            IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.DOOR_JAMMED_STATE_CHANGE);
            msg.WriteUInt16(item.ID); 
            msg.WriteBoolean(isJammed); 
            NetUtil.SendServer(msg, DeliveryMethod.Reliable);
        }

        private void SwitchMonitorMode(MonitorMode newMode)
        {
            currentMode = newMode;

            if (modeButtons.Count >= 2)
            {
                modeButtons[0].Selected = (currentMode == MonitorMode.NormalMap);
                modeButtons[1].Selected = (currentMode == MonitorMode.Buoyancy);
            }

            // 核心控制：当切换到浮力页面时，将地图底层渲染挂载的实体容器完全关闭，杜绝偏移干扰
            if (submarineContainer != null)
            {
                submarineContainer.Visible = (currentMode == MonitorMode.NormalMap);
            }
            if (hullInfoFrame != null)
            {
                hullInfoFrame.Visible = (currentMode == MonitorMode.NormalMap);
            }
        }
    }
}