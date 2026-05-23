using System.Collections.Generic;
using Definitions;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Combat
{
    public class DuelStartContext
    {
        public CombatEncounter Encounter;
        public List<CardDef> PlayerDeck;
        public string TableId;
        public MatchStateDTO SavedMatchState;
        public string SavedMatchJson;
        public DeckData PlayerPersistentDeck;
        public AsyncOperationHandle<SceneInstance> DuelSceneHandle;
    }
}
