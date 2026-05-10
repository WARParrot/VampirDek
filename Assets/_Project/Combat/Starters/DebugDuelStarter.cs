using Cysharp.Threading.Tasks;
using Core;
using Combat;
using Combat.UI;
using Definitions;
using System.Collections.Generic;
using UnityEngine;

public class DebugDuelStarter : MonoBehaviour
{
    public CombatEncounter Encounter;
    public DeckData PlayerDeck;
    public GameObject DuelUI;

    async void Start()
    {
        await UniTask.WaitUntil(() => GlobalServices.Resolver != null);

        var duelGO = new GameObject("DuelManager");
        var duelManager = duelGO.AddComponent<DuelManager>();
        DuelManagerProxy.Instance = duelManager;
        var context = new DuelStartContext
        {
            Encounter = Encounter,
            PlayerDeck = PlayerDeck.Cards,
            TableId = "TestDuel"
        };
        await GlobalServices.Director.PushModeAsync(duelManager, context);
        if (DuelUI != null) DuelUI.SetActive(true);
    }
}
