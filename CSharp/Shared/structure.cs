﻿/*using Barotrauma.Networking;
using Barotrauma.Extensions;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Contacts;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Immutable;
using Barotrauma.Abilities;
#if CLIENT
using Microsoft.Xna.Framework.Graphics;
using Barotrauma.Lights;
#endif




using Barotrauma;
using HarmonyLib;
using System.Globalization;
using System.Reflection;


namespace BarotraumaDieHard
{
  class StructureDieHard : IAssemblyPlugin
  {
    public Harmony harmony;
	private static float LeakThreshold = 0.6f;
	
	
	
    public void Initialize()
    {
        harmony = new Harmony("StructureDieHard");
		
		
        var originalCreateSections = typeof(Structure).GetMethod("CreateSections", BindingFlags.NonPublic | BindingFlags.Instance);
        var prefixCreateSections = new HarmonyMethod(typeof(StructureDieHard).GetMethod(nameof(CreateSectionsPrefix), BindingFlags.Public | BindingFlags.Static));
        harmony.Patch(originalCreateSections, prefixCreateSections, null);

	  
    }
    public void OnLoadCompleted() { }
    public void PreInitPatching() { }

    public void Dispose()
    {
      harmony.UnpatchSelf();
      harmony = null;
    }
	
	public static bool CreateSectionsPrefix(Structure __instance)
    {
        Structure _ = __instance;
        int xsections = 1, ysections = 1;
            int width = _.rect.Width, height = _.rect.Height;

            WallSection[] prevSections = null;
            if (_.Sections != null)
            {
                prevSections = _.Sections.ToArray();
            }
            if (!_.HasBody)
            {
                if (_.FlippedX && _.IsHorizontal)
                {
                    xsections = (int)Math.Ceiling((float)_.rect.Width / _.Prefab.Sprite.SourceRect.Width);
                    width = _.Prefab.Sprite.SourceRect.Width;
                }
                else if (_.FlippedY && !_.IsHorizontal)
                {
                    ysections = (int)Math.Ceiling((float)_.rect.Height / _.Prefab.Sprite.SourceRect.Height);
                    width = _.Prefab.Sprite.SourceRect.Height;
                }
                else
                {
                    xsections = 1;
                    ysections = 1;
                }
                _.Sections = new WallSection[xsections];
            }
            else
            {
                if (_.IsHorizontal)
                {
                    //equivalent to (int)Math.Ceiling((double)rect.Width / WallSectionSize) without the potential for floating point indeterminism
                    xsections = (_.rect.Width + Structure.WallSectionSize - 1) / Structure.WallSectionSize;
                    _.Sections = new WallSection[xsections];
                    width = Structure.WallSectionSize;
                    DebugConsole.NewMessage("WallSectionSize: " + Structure.WallSectionSize.ToString());
                    DebugConsole.NewMessage("Witdth: " + _.rect.Width.ToString());
                    DebugConsole.NewMessage("xsections: " + xsections.ToString());
                }
                else
                {
                    ysections = (_.rect.Height + Structure.WallSectionSize - 1) / Structure.WallSectionSize;
                    _.Sections = new WallSection[ysections];
                    height = Structure.WallSectionSize;
                }
            }

            for (int x = 0; x < xsections; x++)
            {
                for (int y = 0; y < ysections; y++)
                {
                    if (_.FlippedX || _.FlippedY)
                    {
                        Rectangle sectionRect = new Rectangle(
                            _.FlippedX ? _.rect.Right - (x + 1) * width : _.rect.X + x * width,
                            _.FlippedY ? _.rect.Y - _.rect.Height + (y + 1) * height : _.rect.Y - y * height,
                            width, height);

                        if (_.FlippedX)
                        {
                            int over = Math.Max(_.rect.X - sectionRect.X, 0);
                            sectionRect.X += over;
                            sectionRect.Width -= over;
                        }
                        else
                        {
                            sectionRect.Width -= (int)Math.Max(sectionRect.Right - _.rect.Right, 0.0f);
                        }
                        if (_.FlippedY)
                        {
                            int over = Math.Max(sectionRect.Y - _.rect.Y, 0);
                            sectionRect.Y -= over;
                            sectionRect.Height -= over;
                        }
                        else
                        {
                            sectionRect.Height -= (int)Math.Max((_.rect.Y - _.rect.Height) - (sectionRect.Y - sectionRect.Height), 0.0f);
                        }

                        //sectionRect.Height -= (int)Math.Max((rect.Y - rect.Height) - (sectionRect.Y - sectionRect.Height), 0.0f);
                        int xIndex = _.FlippedX && _.IsHorizontal ? (xsections - 1 - x) : x;
                        int yIndex = _.FlippedY && !_.IsHorizontal ? (ysections - 1 - y) : y;
                        _.Sections[xIndex + yIndex] = new WallSection(sectionRect, _);
                    }
                    else
                    {
                        Rectangle sectionRect = new Rectangle(_.rect.X + x * width, _.rect.Y - y * height, width, height);
                        sectionRect.Width -= (int)Math.Max(sectionRect.Right - _.rect.Right, 0.0f);
                        sectionRect.Height -= (int)Math.Max((_.rect.Y - _.rect.Height) - (sectionRect.Y - sectionRect.Height), 0.0f);

                        _.Sections[x + y] = new WallSection(sectionRect, _);
                    }
                }
            }

            if (prevSections != null && _.Sections.Length == prevSections.Length)
            {
                for (int i = 0; i < _.Sections.Length; i++)
                {
                    _.Sections[i].damage = prevSections[i].damage;
                }
            }
        return false;

    }
	
  } 
}
*/