using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using HarmonyLib;
using Barotrauma;

namespace BarotraumaDieHard
{
    class OutpostGeneratorDieHard : IAssemblyPlugin
    {
        public Harmony harmony;
        
        public void Initialize()
        {
            harmony = new Harmony("OutpostGeneratorDieHard");

            harmony.Patch(
                original: typeof(OutpostGenerator).GetMethod("SpawnNPCs"),
                prefix: new HarmonyMethod(typeof(OutpostGeneratorDieHard).GetMethod(nameof(SpawnNPCsPrefix)))
            );
        }

        public void OnLoadCompleted() { }
        public void PreInitPatching() { }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            harmony = null;
        }


// killed npc still has items on them.
/*
        public static bool SpawnNPCsPrefix(Location location, Submarine outpost)
{
    if (outpost?.Info?.OutpostGenerationParams == null) { return false; }

    List<(HumanPrefab HumanPrefab, CharacterInfo CharacterInfo)> selectedCharacters
        = new List<(HumanPrefab HumanPrefab, CharacterInfo CharacterInfo)>();

    List<FactionPrefab> factions = new List<FactionPrefab>();
    if (location?.Faction != null) { factions.Add(location.Faction.Prefab); }
    if (location?.SecondaryFaction != null) { factions.Add(location.SecondaryFaction.Prefab); }

    var humanPrefabs = outpost.Info.OutpostGenerationParams.GetHumanPrefabs(factions, outpost, Rand.RandSync.ServerAndClient);
    foreach (HumanPrefab humanPrefab in humanPrefabs)
    {
        if (humanPrefab is null) { continue; }

        var characterInfo = humanPrefab.CreateCharacterInfo(Rand.RandSync.ServerAndClient);

        selectedCharacters.Add((humanPrefab, characterInfo));
    }

    foreach ((var humanPrefab, var characterInfo) in selectedCharacters)
    {
        Rand.SetSyncedSeed(ToolBox.StringToInt(characterInfo.Name));

        ISpatialEntity gotoTarget = SpawnAction.GetSpawnPos(SpawnAction.SpawnLocationType.Outpost, SpawnType.Human, humanPrefab.GetModuleFlags(), humanPrefab.GetSpawnPointTags());
        if (gotoTarget == null)
        {
            gotoTarget = outpost.GetHulls(true).GetRandom(Rand.RandSync.ServerAndClient);
        }

        characterInfo.TeamID = CharacterTeamType.FriendlyNPC;
        var npc = Character.Create(characterInfo.SpeciesName, SpawnAction.OffsetSpawnPos(gotoTarget.WorldPosition, 100.0f), ToolBox.RandomSeed(8), characterInfo, hasAi: true, createNetworkEvent: true);
        npc.AnimController.FindHull(gotoTarget.WorldPosition, setSubmarine: true);
        npc.TeamID = CharacterTeamType.FriendlyNPC;
        npc.HumanPrefab = humanPrefab;
        outpost.Info.AddOutpostNPCIdentifierOrTag(npc, humanPrefab.Identifier);

        foreach (Identifier tag in humanPrefab.GetTags())
        {
            outpost.Info.AddOutpostNPCIdentifierOrTag(npc, tag);
        }

        if (GameMain.NetworkMember?.ServerSettings != null && !GameMain.NetworkMember.ServerSettings.KillableNPCs)
        {
            npc.CharacterHealth.Unkillable = true;
        }

        humanPrefab.GiveItems(npc, outpost, gotoTarget as WayPoint, Rand.RandSync.ServerAndClient);
        foreach (Item item in npc.Inventory.FindAllItems(it => it != null, recursive: true))
        {
            item.AllowStealing = outpost.Info.OutpostGenerationParams.AllowStealing;
            item.SpawnedInCurrentOutpost = true;
        }

        humanPrefab.InitializeCharacter(npc, gotoTarget);

        // Immediately kill the character if they were previously marked as dead at this location
        if (location != null && location.KilledCharacterIdentifiers.Contains(characterInfo.GetIdentifier()))
        {
            
            npc.Kill(CauseOfDeathType.Unknown, null, log: false);
            
        }
    }

    return false; // Prevents the original method from running
}
*/


        public static bool SpawnNPCsPrefix(Location location, Submarine outpost)
        {
            if (outpost?.Info?.OutpostGenerationParams == null) { return false; }

            List<(HumanPrefab HumanPrefab, CharacterInfo CharacterInfo)> selectedCharacters
                = new List<(HumanPrefab HumanPrefab, CharacterInfo CharacterInfo)>();

            List<FactionPrefab> factions = new List<FactionPrefab>();
            if (location?.Faction != null) { factions.Add(location.Faction.Prefab); }
            if (location?.SecondaryFaction != null) { factions.Add(location.SecondaryFaction.Prefab); }

            var humanPrefabs = outpost.Info.OutpostGenerationParams.GetHumanPrefabs(factions, outpost, Rand.RandSync.ServerAndClient);
            foreach (HumanPrefab humanPrefab in humanPrefabs)
            {
                if (humanPrefab is null) { continue; }
                
                var characterInfo = humanPrefab.CreateCharacterInfo(Rand.RandSync.ServerAndClient);
                
                // Skip this character if they were killed at this location
                if (location != null && location.KilledCharacterIdentifiers.Contains(characterInfo.GetIdentifier()))
                {
                    // DebugConsole.NewMessage("npc was killed");
                    
                    continue; // Exclude killed characters
                }
                
                selectedCharacters.Add((humanPrefab, characterInfo));
            }

            foreach ((var humanPrefab, var characterInfo) in selectedCharacters)
            {
                Rand.SetSyncedSeed(ToolBox.StringToInt(characterInfo.Name));

                ISpatialEntity gotoTarget = SpawnAction.GetSpawnPos(SpawnAction.SpawnLocationType.Outpost, SpawnType.Human, humanPrefab.GetModuleFlags(), humanPrefab.GetSpawnPointTags());
                if (gotoTarget == null)
                {
                    gotoTarget = outpost.GetHulls(true).GetRandom(Rand.RandSync.ServerAndClient);
                }
                
                characterInfo.TeamID = CharacterTeamType.FriendlyNPC;
                var npc = Character.Create(characterInfo.SpeciesName, SpawnAction.OffsetSpawnPos(gotoTarget.WorldPosition, 100.0f), ToolBox.RandomSeed(8), characterInfo, hasAi: true, createNetworkEvent: true);
                npc.AnimController.FindHull(gotoTarget.WorldPosition, setSubmarine: true);
                npc.TeamID = CharacterTeamType.FriendlyNPC;
                npc.HumanPrefab = humanPrefab;
                outpost.Info.AddOutpostNPCIdentifierOrTag(npc, humanPrefab.Identifier);
                
                foreach (Identifier tag in humanPrefab.GetTags())
                {
                    outpost.Info.AddOutpostNPCIdentifierOrTag(npc, tag);
                }

                if (GameMain.NetworkMember?.ServerSettings != null && !GameMain.NetworkMember.ServerSettings.KillableNPCs)
                {
                    npc.CharacterHealth.Unkillable = true;
                }

                humanPrefab.GiveItems(npc, outpost, gotoTarget as WayPoint, Rand.RandSync.ServerAndClient);
                foreach (Item item in npc.Inventory.FindAllItems(it => it != null, recursive: true))
                {
                    item.AllowStealing = outpost.Info.OutpostGenerationParams.AllowStealing;
                    item.SpawnedInCurrentOutpost = true;
                }

                humanPrefab.InitializeCharacter(npc, gotoTarget);
            }

            return false; // Prevents the original method from running
        }


    }
}