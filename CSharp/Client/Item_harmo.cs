
using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Barotrauma.MapCreatures.Behavior;
using Barotrauma.Networking;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;

using Barotrauma;
using HarmonyLib;
using System.Globalization;
using System.Reflection;// for bindingflags

using System.IO;

namespace BarotraumaDieHard
{
    public class ItemDieHard : IAssemblyPlugin
    {
        public Harmony harmony;
		public void Initialize()
		{
		    harmony = new Harmony("ItemDieHard");

            // Fix for ambiguous match.
            // Use the correct method signature for the Draw method you want to patch
            var originalDraw = typeof(Item).GetMethod("Draw", BindingFlags.Public | BindingFlags.Instance, null, 
                new Type[] { typeof(SpriteBatch), typeof(bool), typeof(bool), typeof(Color?) }, null);

            var postfixDraw = new HarmonyMethod(typeof(ItemDieHard).GetMethod(nameof(Draw), BindingFlags.Public | BindingFlags.Static));
            
            // Patch the original Draw method with your postfix method
            harmony.Patch(originalDraw, null, postfixDraw);
        }

		public void OnLoadCompleted() { }
		public void PreInitPatching() { }

		public void Dispose()
		{
		  harmony.UnpatchSelf();
		  harmony = null;
		}

        public static void Draw(SpriteBatch spriteBatch, bool editing, bool back, Color? overrideColor, Item __instance)
        {           
            // sprite name, drawed item identifier, at which tag to daw
            DrawCustomSprite("mediumsteelcabinet_open", "mediumsteelcabinet", "draw_container_open", __instance, spriteBatch);
            DrawCustomSprite("mediumwindowedsteelcabinet_open", "mediumwindowedsteelcabinet", "draw_container_open", __instance, spriteBatch);        
            DrawCustomSprite("steelcabinet_open", "steelcabinet", "draw_container_open", __instance, spriteBatch);
               
            DrawCustomSprite("medcabinet_open", "medcabinet", "draw_container_open", __instance, spriteBatch);
            //DrawCustomSprite("seccabinet_open_0", "securesteelcabinet", "draw_container_open", __instance, spriteBatch);
            DrawCustomSprite("seccabinet_open_1", "securesteelcabinet", "draw_container_open", __instance, spriteBatch);
            DrawCustomSprite("toxiccabinet_open", "toxcabinet", "draw_container_open", __instance, spriteBatch);
            DrawCustomSprite("supplycabinet_open", "suppliescabinet", "draw_container_open", __instance, spriteBatch);


            
            DrawCustomSprite("junctionbox_open_nodamage",  "junctionbox", "junctionbox_openlid", __instance, (60f, 100f), spriteBatch);
            DrawCustomSprite("junctionbox_open_damage",  "junctionbox", "junctionbox_openlid", __instance, (10f, 60), spriteBatch);
            DrawCustomSprite("junctionbox_open_broken",  "junctionbox", "junctionbox_openlid", __instance, (0f, 10f), spriteBatch);
            DrawCustomSprite("junctionbox_open_broken",  "junctionbox", "", __instance, (0f, 0f), spriteBatch); // Ok, the item component doesn't read status effect so we have to work around. let's display the open lid by default if it's completely broken.

            DrawCustomSprite("battery_open_nodamage",  "battery", "draw_container_open", __instance, (60f, 100f), spriteBatch);
            DrawCustomSprite("battery_open_damage",  "battery", "draw_container_open", __instance, (10f, 60), spriteBatch);
            DrawCustomSprite("battery_open_broken",  "battery", "draw_container_open", __instance, (0f, 10f), spriteBatch);
            DrawCustomSprite("battery_open_broken",  "battery", "", __instance, (0f, 0f), spriteBatch);










        }   


        public static void DrawCustomSprite(string spriteName, string targetItemIdentifier, string targetItemTag, Item targetItem, SpriteBatch spriteBatch)
        {
            // Try to get the sprite by its name from the dictionary
            if (GameSessionDieHard.customSprites.TryGetValue(spriteName, out Sprite customSprite))
            {
                
                // Check if the item matches the specified identifier and tag
                if (targetItem.Prefab.Identifier == targetItemIdentifier && targetItem.HasTag(targetItemTag))
                {
                    // Calculate the draw position. Need to invert Y-axis.
                    Vector2 drawPosition = new Vector2(targetItem.DrawPosition.X, -targetItem.DrawPosition.Y);

                    // Draw the sprite at the adjusted position
                    customSprite.Draw(
                        spriteBatch, 
                        pos: drawPosition, 
                        color: targetItem.GetSpriteColor(), 
                        rotate: targetItem.Rotation, 
                        scale: targetItem.Scale, 
                        origin: customSprite.Origin,
                        depth: targetItem.GetDrawDepth() - 0.01f
                    );
                    
                    
                }
            }
        }


        public static void DrawCustomSprite(string spriteName, string targetItemIdentifier, string targetItemTag, Item targetItem, (float minCondition, float maxCondition) conditionRange, SpriteBatch spriteBatch)
{
    // Try to get the sprite by its name from the dictionary
    if (GameSessionDieHard.customSprites.TryGetValue(spriteName, out Sprite customSprite))
    {
        // Check if the item matches the specified identifier and tag
        if (targetItem.Prefab.Identifier == targetItemIdentifier && 
            targetItem.HasTag(targetItemTag) && 
            targetItem.Condition >= conditionRange.minCondition && 
            targetItem.Condition <= conditionRange.maxCondition)
        {
            // Calculate the draw position. Need to invert Y-axis.
            Vector2 drawPosition = new Vector2(targetItem.DrawPosition.X, -targetItem.DrawPosition.Y);

            // Draw the sprite at the adjusted position
            customSprite.Draw(
                spriteBatch, 
                pos: drawPosition, 
                color: targetItem.GetSpriteColor(), 
                rotate: targetItem.Rotation, 
                scale: targetItem.Scale, 
                origin: customSprite.Origin,
                depth: targetItem.GetDrawDepth() - 0.01f
            );
        }
    }
}



                
        }


        
}
