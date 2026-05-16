#nullable enable

using Barotrauma.IO;
using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Networking;
using Barotrauma.Extensions;


using Barotrauma;
#if CLIENT
using Barotrauma.Lights;
using Microsoft.Xna.Framework.Graphics;
#endif
using Barotrauma.Extensions;
using System.Reflection;


using HarmonyLib;
using Barotrauma.Items.Components;


namespace BarotraumaDieHard
{
    [HarmonyPatch(typeof(GameSession))]
    public class GameSessionDieHard
    {
        


        public static AfflictionPrefab pressurizedhullPrefab;


        // 使用参数类型数组解决 "ambiguous" (歧义) 问题
        [HarmonyPatch("StartRound")]
        [HarmonyPatch(new Type[] { typeof(LevelData), typeof(bool), typeof(SubmarineInfo), typeof(SubmarineInfo) })]
        [HarmonyPostfix]
        public static void StartRound(LevelData levelData, bool mirrorLevel, SubmarineInfo startOutpost, SubmarineInfo endOutpost)
        {
            //DebugConsole.NewMessage(Level.Loaded.StartLocation?.Type.ToString());
            pressurizedhullPrefab = AfflictionPrefab.Prefabs["pressurizedhull"];
            

            foreach (Item preactor in Item.ItemList)
            {
                if (preactor.HasTag("reactor"))
                {
                    var ItemContainers = preactor.GetComponents<ItemContainer>().ToList();;
                    ReactorDieHard.SecondItemContainerReactors[preactor.ID] = ItemContainers[1];
                }
            }

#if CLIENT

            

#endif

        }

        
        [HarmonyPatch("EndRound")]
        [HarmonyPostfix]
        public static void EndRound(string endMessage, CampaignMode.TransitionType transitionType, TraitorManager.TraitorResults? traitorResults)
        {

           TurretDieHard.ResetOriginalReloadValue();
           TurretDieHard.ClearReloadDictionary();

            SonarMod.ResetOriginalSonarRange();
            SonarMod.ClearSonarRangeDictionary();


            ReactorDieHard.ClearRactorySecondContainerDictionary();

            CharacterPatch.ClearPressureTimerDictionary();

            ConvertLocationToDestroyed();
            PowerTransferPatch.LeverStates.Clear();
            
        }
    

        






        //This causes the togglecampaignteleport can't view the normal abandoned outposts if teleported directly to that location. Bucause it will always convert to destroyed type.
        public static void ConvertLocationToDestroyed()
        {
            if (GameMain.GameSession?.Campaign == null) return;

            
            // Get the current location (this may vary based on your context)
            Location currentLocation = Level.Loaded.StartLocation;

            if (currentLocation != null)
            {

                foreach (Item it in Item.ItemList)
                {
                    if (it.HasTag("reactor") && !it.InPlayerSubmarine && it.Condition <= 0)
                    {
#if DEBUG
                        DebugConsole.NewMessage("previous outpost's reactor is dead. Converting outpost to Abandoned.");
#endif
                        var allLocationTypes = LocationType.Prefabs; // This might be a collection containing all location types

                        // Find a specific type by its identifier
                        Identifier newTypeIdentifier = new Identifier("Destroyed");
                        LocationType newType = allLocationTypes.FirstOrDefault(lt => lt.Identifier == newTypeIdentifier);

                        if (newType == null)
                        {
                            DebugConsole.ThrowError("Failed to find the location type.");
                        }
                        else
                        {
                            // Use the location type, e.g., assign it to the current location
                            currentLocation.Type = newType;
                        }
                    }
                }
                
            }
        }
    }
}
