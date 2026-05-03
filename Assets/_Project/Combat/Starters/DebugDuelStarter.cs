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
    public List<CardDef> PlayerDeckCards;
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
            PlayerDeck = PlayerDeckCards,
            TableId = "TestDuel"
        };
        await GlobalServices.Director.PushModeAsync(duelManager, context);
        var handUI = DuelUI.GetComponentInChildren<HandUIManager>();
        if (handUI == null) handUI = DuelUI.GetComponent<HandUIManager>();
            handUI?.SetDuelManager(duelManager);
        if (DuelUI != null) DuelUI.SetActive(true);
        
        var boardView = DuelUI.GetComponentInChildren<BoardView>();
        boardView?.SetDuelManager(duelManager);
    }
}
