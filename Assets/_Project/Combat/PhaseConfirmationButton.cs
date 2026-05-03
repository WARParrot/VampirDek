using Definitions;
using UnityEngine;
using UnityEngine.UI;

public class PhaseConfirmationButton : MonoBehaviour
{
    private Button _button;

    void Start()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnClick);
    }

    void Update()
    {
        var dm = DuelManagerProxy.Instance;
        if (dm?.CurrentDuelState == null)
        {
            _button.gameObject.SetActive(false);
            return;
        }

        var phase = dm.CurrentDuelState.CurrentPhase;
        bool show = phase.Tags.Contains("BuildingPhase") ||
                    phase.Tags.Contains("PlanningPhase");
        _button.gameObject.SetActive(show);
    }

    void OnClick()
    {
        DuelManagerProxy.Instance?.ConfirmCurrentPhase();
    }
}