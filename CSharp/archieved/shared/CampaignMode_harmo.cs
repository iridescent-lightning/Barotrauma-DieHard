using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Barotrauma.Networking; // used by the server
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

using HarmonyLib;
using System.Reflection;
using System.Xml.Linq;

using Barotrauma;


namespace BarotraumaDieHard
{
    class CampaignModeDieHard  : IAssemblyPlugin
    {


        public Harmony harmony;
        
        
        public void Initialize()
        {
            harmony = new Harmony("CampaignModeDieHard");

            var originalGetLeavingSub = typeof(CampaignMode).GetMethod("GetLeavingSub", BindingFlags.NonPublic | BindingFlags.Static);
            var prefixGetLeavingSub = typeof(CampaignModeDieHard).GetMethod(nameof(GetLeavingSubPrefix), BindingFlags.Public | BindingFlags.Static);

            harmony.Patch(originalGetLeavingSub, new HarmonyMethod(prefixGetLeavingSub), null);


            // This is a solution of ambiguous match found by further specify the method parameters.
            var originalMethod = typeof(CampaignMode).GetMethod("GetAvailableTransition", BindingFlags.Instance | BindingFlags.Public, null, 
                new Type[] { typeof(LevelData).MakeByRefType(), typeof(Submarine).MakeByRefType() }, null);

        }
            
        

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }


        // The prefix patch must also be static
        public static bool GetLeavingSubPrefix(ref Submarine __result)
        {
            // Check if any players are alive and inside the outpost, ignoring submarine docking status
            var leavingPlayers = Character.CharacterList.Where(c => !c.IsDead && (c == Character.Controlled || c.IsRemotePlayer));
            if (leavingPlayers.Any(c => c.Submarine == Level.Loaded.StartOutpost || c.Submarine == Level.Loaded.EndOutpost))
            {
                __result = Submarine.MainSub;
                //return Submarine.MainSub;
            }
            // Example logic for your prefix
            

            // Return false to prevent the original method from running
            return false;
        }

        
        // The prefix method
        public static bool GetAvailableTransitionPrefix(ref CampaignMode.TransitionType __result, out LevelData nextLevel, out Submarine leavingSub)
        {
            leavingSub = Submarine.MainSub; // Assign leavingSub before any checks
            
            if (Level.Loaded == null)
            {
                nextLevel = null;
                leavingSub = null;
                __result = CampaignMode.TransitionType.None;
                return false;
            }
            

            // Example logic: automatically set the transition type based on some conditions
            if (Level.Loaded.Type == LevelData.LevelType.Outpost)
            {
                __result = CampaignMode.TransitionType.ProgressToNextLocation;
                nextLevel = Level.Loaded.LevelData;
                return false; // Skip the original method
            }

            // Default case: let the original method run
            nextLevel = Level.Loaded.LevelData;
            return false;
        }

    }
}
