/*using Barotrauma.Extensions;
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
    class AIObjectiveRepairItemsDieHard  : IAssemblyPlugin
    {


        public Harmony harmony;
        
        
        public void Initialize()
        {
            harmony = new Harmony("AIObjectiveRepairItemsDieHard");

            harmony.Patch(
                original: typeof(AIObjectiveRepairItems).GetMethod("IsValidTarget"),
                prefix: new HarmonyMethod(typeof(AIObjectiveRepairItemsDieHard).GetMethod(nameof(IsValidTargetPrefix)))
            );
            
        }

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }


        public static bool IsValidTargetPrefix(Item item, Character character, AIObjectiveRepairItem __instance, ref bool __result)
{
    
    
    if (item == null) 
    { 
        
        __result = false; 
        return false; 
    }
    if (item.IgnoreByAI(character)) 
    { 
        
        __result = false; 
        return false; 
    }
    if (!item.IsInteractable(character)) 
    { 
        
        __result = false; 
        return false; 
    }
    if (item.IsFullCondition) 
    { 
        
        __result = false; 
        return false; 
    }
    if (item.Submarine == null || character.Submarine == null) 
    { 
        
        __result = false; 
        return false; 
    }
    if (item.IsClaimedByBallastFlora) 
    { 
        
        __result = false; 
        return false; 
    } 
    if (character.IsOnPlayerTeam && item.Submarine.Info.IsOutpost) 
    { 
        
        __result = false; 
        return false; 
    }
    if (!character.Submarine.IsEntityFoundOnThisSub(item, includingConnectedSubs: true)) 
    { 
        
        __result = false; 
        return false; 
    }
    
    if (item.Repairables.None()) 
    {        
        __result = false; 
        return false; 
    }

    __result = true; // Set __result to true if all checks pass
    return false;
}


    }
}*/