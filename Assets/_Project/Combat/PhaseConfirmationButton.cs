using Definitions;
using UnityEngine;
using UnityEngine.UI;
using Combat;
using System.Collections;
using TMPro;
using Shared.Localization;

public class PhaseConfirmationButton : MonoBehaviour
{
    [SerializeField] private TutorialSystem _tutorialSystem;
    [SerializeField] private TextMeshProUGUI _label;

    private Button _button;
    private CanvasGroup _canvasGroup;
    private DuelManager _duelManager;

    void Awake()
    {
        _button = GetComponent<Button>();
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        SetVisible(false);
    }

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

        if (_button == null) _button = GetComponent<Button>();
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnClick);
            _button.onClick.AddListener(OnClick);
        }

        if (_label == null) _label = GetComponentInChildren<TextMeshProUGUI>(true);
        ApplyLocalization();

        if (_tutorialSystem == null)
        {
            _tutorialSystem = TutorialSystem.Current;
        }
    }

    private void ApplyLocalization()
    {
        if (_label != null) _label.text = LocalizationService.T("phase.confirm_button", "Confirm");
    }

    void Update()
    {
        if (_duelManager == null) return;
        if (_button == null) return;

        var phase = _duelManager.CurrentDuelState?.CurrentPhase;
        if (phase == null)
        {
            SetVisible(false);
            return;
        }

        bool phaseCanConfirm = phase.Tags.Contains("BuildingPhase") ||
                               phase.Tags.Contains("PlanningPhase");
        bool readyForConfirmation = _duelManager.CanConfirmCurrentPhase;
        bool allowedByTutorial = _tutorialSystem == null ||
                                 !_tutorialSystem.IsTutorialActive ||
                                 _tutorialSystem.AllowsPhaseConfirmation();
        bool show = phaseCanConfirm && readyForConfirmation && allowedByTutorial;
        SetVisible(show);
        _button.interactable = show;
    }

    private void SetVisible(bool visible)
    {
        // Do not SetActive(false) on this GameObject: that disables Update(), so the button
        // cannot bring itself back for the next Building/Planning confirmation wait.
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable = visible;
        }

        if (_button != null && !visible)
            _button.interactable = false;
    }

    void OnClick()
    {
        if (_button != null && !_button.interactable) return;

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
