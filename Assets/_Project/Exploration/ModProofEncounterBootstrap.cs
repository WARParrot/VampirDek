using System;
using System.Collections.Generic;
using System.Linq;
using Combat;
using Definitions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Exploration
{
    public static class ModProofEncounterBootstrap
    {
        public const string EncounterId = "MOD_PROOF_BLOOD_CULT_INITIATE";
        public const string TableId = "MOD_PROOF_BLOOD_CULT_TABLE";
        private const string HubSceneName = "Modded Duel Hub";

        public static void EnsureEncounterPoints(string sourceLabel, Scene hostScene, int maxTables = int.MaxValue)
        {
            var encounters = GetModdedEncounters()
                .Take(Mathf.Max(1, maxTables))
                .ToList();

            if (encounters.Count == 0)
            {
                Debug.Log("[ModProofEncounterBootstrap] No modded encounters are loaded; not creating mod duel hub tables.");
                return;
            }

            var hubScene = GetOrCreateHubScene(hostScene);
            var player = UnityEngine.Object.FindAnyObjectByType<ExplorationController>();
            var origin = player != null
                ? player.transform.position + player.transform.forward * 3.5f + Vector3.up * 0.5f
                : new Vector3(2f, 0.5f, 2f);

            var spawned = 0;
            for (var i = 0; i < encounters.Count; i++)
            {
                var encounter = encounters[i];
                var tableId = TableIdFor(encounter.EncounterId);
                var existing = UnityEngine.Object.FindObjectsOfType<EncounterPoint>(true)
                    .FirstOrDefault(p => p != null && p.UniqueTableId == tableId);
                if (existing != null)
                    continue;

                var row = i / 3;
                var col = i % 3;
                var position = origin + new Vector3((col - 1) * 2.4f, 0f, row * 2.1f);
                CreateTable(encounter, tableId, position, hubScene);
                spawned++;
            }

            Debug.Log($"[ModProofEncounterBootstrap] Mod duel hub ready from '{sourceLabel}': {encounters.Count} mod encounter table(s), {spawned} newly spawned, scene '{HubSceneName}'.");
        }

        public static string SpawnDevHubFromConsole(string[] args)
        {
            var limit = int.MaxValue;
            if (args != null && args.Length > 0 && int.TryParse(args[0], out var parsed))
                limit = Mathf.Max(1, parsed);

            EnsureEncounterPoints("DevConsole", SceneManager.GetActiveScene(), limit);
            return $"Requested modded duel hub tables (limit: {(limit == int.MaxValue ? "all" : limit.ToString())}). Use scenes to verify the '{HubSceneName}' additive scene.";
        }

        private static IReadOnlyList<CombatEncounter> GetModdedEncounters()
        {
            var all = EncounterDatabase.GetAllEncounters();
            var modded = all
                .Where(e => e != null && IsLikelyModEncounter(e.EncounterId))
                .OrderBy(e => e.EncounterId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (modded.Count == 0)
            {
                var proof = EncounterDatabase.GetEncounter(EncounterId);
                if (proof != null)
                    modded.Add(proof);
            }

            return modded;
        }

        private static bool IsLikelyModEncounter(string encounterId)
        {
            if (string.IsNullOrWhiteSpace(encounterId))
                return false;

            return encounterId.StartsWith("MOD_", StringComparison.OrdinalIgnoreCase)
                || encounterId.StartsWith("mod.", StringComparison.OrdinalIgnoreCase)
                || encounterId.Contains("_MOD_", StringComparison.OrdinalIgnoreCase);
        }

        private static Scene GetOrCreateHubScene(Scene hostScene)
        {
            var scene = SceneManager.GetSceneByName(HubSceneName);
            if (scene.IsValid() && scene.isLoaded)
                return scene;

            return SceneManager.CreateScene(HubSceneName);
        }

        private static void CreateTable(CombatEncounter encounter, string tableId, Vector3 position, Scene hubScene)
        {
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = $"Mod Encounter Table - {encounter.EncounterId}";
            table.transform.position = position;
            table.transform.localScale = new Vector3(1.4f, 0.7f, 1.4f);
            AssignEncounterLayer(table);

            var collider = table.GetComponent<Collider>();
            if (collider != null)
                collider.isTrigger = true;

            if (hubScene.IsValid())
                SceneManager.MoveGameObjectToScene(table, hubScene);

            var seat = new GameObject("ModEncounterCameraSeat");
            seat.transform.SetParent(table.transform, false);
            seat.transform.localPosition = new Vector3(0f, 1.6f, -2.5f);
            seat.transform.LookAt(table.transform.position + Vector3.up * 0.6f);
            AssignEncounterLayer(seat);

            var point = table.AddComponent<EncounterPoint>();
            point.ConfigureRuntime(
                encounter.EncounterId,
                tableId,
                DeckDatabase.GetDeck("FallbackDeck") ?? CreateRuntimeFallbackDeck(),
                seat.transform,
                null,
                "interaction.start_mod_duel",
                $"Press [E] to challenge mod encounter {encounter.EncounterId}");
        }


        private static void AssignEncounterLayer(GameObject gameObject)
        {
            var encounterLayer = LayerMask.NameToLayer("Encounter");
            if (encounterLayer < 0)
            {
                Debug.LogWarning("[ModProofEncounterBootstrap] Layer 'Encounter' is not defined. Runtime mod table remains on Default layer and may not be found by the exploration encounter mask.");
                return;
            }

            gameObject.layer = encounterLayer;
        }

        private static string TableIdFor(string encounterId)
        {
            if (string.Equals(encounterId, EncounterId, StringComparison.OrdinalIgnoreCase))
                return TableId;

            return "MOD_TABLE_" + encounterId;
        }

        private static DeckData CreateRuntimeFallbackDeck()
        {
            var deck = ScriptableObject.CreateInstance<DeckData>();
            deck.name = "RuntimeModProofFallbackDeck";
            deck.CardNames = new List<string> { "Town", "Human", "Human", "Building", "Vampire" };
            deck.Cards = new List<CardDef>();

            foreach (var cardId in deck.CardNames)
            {
                var card = CardDatabase.GetCard(cardId);
                if (card != null)
                    deck.Cards.Add(card);
            }

            return deck;
        }
    }
}
