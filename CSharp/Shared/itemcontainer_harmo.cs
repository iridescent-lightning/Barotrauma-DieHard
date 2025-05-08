
using System.Reflection;
using Barotrauma;
using HarmonyLib;
using Microsoft.Xna.Framework;


using Barotrauma.Abilities;
using Barotrauma.Extensions;
using Barotrauma.Networking;

using Barotrauma.Items.Components;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

using FarseerPhysics;
using System;
using System.Collections.Immutable;


namespace ItemContainerMod
{
  class ItemContainerMod : IAssemblyPlugin
  {
    public Harmony harmony;
	//set up the system time
	private static DateTime lastUpdateTime = DateTime.MinValue;
    private static readonly TimeSpan updateInterval = TimeSpan.FromSeconds(0.5f); // 0.5-second interval

	
    public void Initialize()
    {
      harmony = new Harmony("ItemContainerMod");

      
		var originalSelect = typeof(ItemContainer).GetMethod("Select", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Character) }, null);
    	var prefixSelect = typeof(ItemContainerMod).GetMethod("SelectPrefix", BindingFlags.Public | BindingFlags.Static);

		harmony.Patch(originalSelect, new HarmonyMethod(prefixSelect), null);

		harmony.Patch(
                original: typeof(ItemContainer).GetMethod("Equip"),
                prefix: new HarmonyMethod(typeof(ItemContainerMod).GetMethod(nameof(Equip)))
            );
    }
    
    public void OnLoadCompleted() { }
    public void PreInitPatching() { }

    public void Dispose()
    {
      harmony.UnpatchSelf();
      harmony = null;
    }
	
	public static bool SelectPrefix(Character character, ItemContainer __instance)
	{
		ItemContainer _ = __instance;
		
		if (!_.AllowAccess) { return false; }
            if (_.item.Container != null) { return false; }
            if (_.AccessOnlyWhenBroken)
            {
                if (_.item.Condition > 0)
                {
                    return false;
                }
            }
            if (_.AutoInteractWithContained && character.SelectedItem == null)
            {
                foreach (Item contained in _.Inventory.AllItems)
                {
                    if (contained.TryInteract(character))
                    {
                        character.FocusedItem = contained;
                        return false;
                    }
                }
            }
			//use the system time if you have no access to deltaTime
			if (DateTime.UtcNow - lastUpdateTime < updateInterval)
            {
                //do nothing
            }
			else
			{
				//a template will fill in sound effects here
				if (_.item.HasTag("steelcabinetsfx"))
				{
					#if CLIENT
						SoundPlayer.PlaySound("interactive_large_container", _.item.WorldPosition, hullGuess: _.item.CurrentHull);
					#endif
				}
				else if (_.item.HasTag("mediumsteelcabinetsfx"))
				{
					//DebugConsole.NewMessage("mediumsteelcabinetsfx");
					#if CLIENT
						SoundPlayer.PlaySound("interactive_medium_container", _.item.WorldPosition, hullGuess: _.item.CurrentHull);
					#endif
				}
				else if (_.item.HasTag("extinguisherholder"))
				{
					#if CLIENT
						SoundPlayer.PlaySound("interactive_large_container", _.item.WorldPosition, hullGuess: _.item.CurrentHull);
					#endif
				}
				else if (_.item.HasTag("suppliescontainer"))
				{
					#if CLIENT
						SoundPlayer.PlaySound("interactive_emergencycab", _.item.WorldPosition, hullGuess: _.item.CurrentHull);
					#endif
				}
				else if (_.item.HasTag("securecontainer"))
				{
					#if CLIENT
						SoundPlayer.PlaySound("interactive_securitycab_open", _.item.WorldPosition, hullGuess: _.item.CurrentHull);
					#endif
				}
				else if (_.item.HasTag("medcontainer"))
				{
					#if CLIENT
						SoundPlayer.PlaySound("interactive_med_container_open", _.item.WorldPosition, hullGuess: _.item.CurrentHull);
					#endif
				}


				lastUpdateTime = DateTime.UtcNow;
			}
			
			
            var abilityItem = new AbilityItemContainer(_.item);
            character.CheckTalents(AbilityEffectType.OnOpenItemContainer, abilityItem);

            if (_.item.ParentInventory?.Owner == character)
            {
				
                //can't select ItemContainers in the character's inventory (the inventory is drawn by hovering the cursor over the inventory slot, not as a GUIFrame)
                return false;
            }
            else
            {
				/*In your SelectPrefix method, it looks like you want to execute the original Select method of the ItemContainer class unless certain conditions are met. With Harmony, the return value of the prefix method determines whether the original method is called. If the prefix method returns true, Harmony proceeds to call the original method. If it returns false, the original method is skipped.*/
				return true;
               // return base.Select(character);
            }
			
			return false;
	}
	
	
    
	public static bool Equip(Character character, ItemContainer  __instance)
        {
			ItemContainer _ = __instance;
            _.IsActive = true;
			//slot => slot.only checks for equipping to hands, which is exactly what we want
            if (character != null && character.HasEquippedItem(_.item, predicate: slot => slot.HasFlag(InvSlotType.LeftHand) || slot.HasFlag(InvSlotType.RightHand)))
            {
                _.SetContainedActive(true);
				//a template will fill in sound effects here
				//DebugConsole.NewMessage("Equip");
            }
            else
            {
                _.SetContainedActive(false);
            }
			return false;
        }
	
  }
}
