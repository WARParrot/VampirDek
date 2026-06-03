using Definitions;
using UnityEngine;
using UnityEngine.UI;
using Combat;
using System.Collections;

public class PhaseConfirmationButton : MonoBehaviour
{
    [SerializeField] private TutorialSystem _tutorialSystem;

    private Button _button;
    private DuelManager _duelManager;

    void Start()
    {
        StartCoroutine(initButton());
    }

    IEnumerator initButton()
    {
        Debug.Log("[PhaseCButton] InitButton started - waiting for proxy...");
        yield return new WaitUntil(() => DuelManagerProxy.Instance != null);
        Debug.Log("[PhaseCButton] Proxy acquired.");

        yield return new WaitUntil(() => DuelManagerProxy.Instance.CurrentDuelState != null);
        Debug.Log("[PhaseCButton] DuelState ready. Building button...");

        _duelManager = DuelManagerProxy.Instance;

        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnClick);

        if (_tutorialSystem == null)
        {
            _tutorialSystem = FindObjectOfType<TutorialSystem>();
        }
    }

    void Update()
    {
        if (_duelManager == null) return;
        if (_button == null) return;

        var phase = _duelManager.CurrentDuelState?.CurrentPhase;
        if (phase == null)
        {
            _button.gameObject.SetActive(false);
            return;
        }

        bool show = phase.Tags.Contains("BuildingPhase") ||
                    phase.Tags.Contains("PlanningPhase");
        _button.gameObject.SetActive(show);
        _button.interactable = !show || _tutorialSystem == null || !_tutorialSystem.IsTutorialActive || _tutorialSystem.AllowsPhaseConfirmation();
    }

    void OnClick()
    {
        if (_tutorialSystem != null && _tutorialSystem.IsTutorialActive && !_tutorialSystem.AllowsPhaseConfirmation())
        {
            Debug.Log("[PhaseCButton] Tutorial blocked phase confirmation until the current tutorial step is completed.");
            return;
        }

        DuelManagerProxy.Instance?.ConfirmCurrentPhase();

        if (_tutorialSystem != null && _tutorialSystem.IsTutorialActive)
        {
            _tutorialSystem.OnPhaseConfirmed();
        }
    }
}