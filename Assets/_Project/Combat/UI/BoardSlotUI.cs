using Combat;
using Combat.UI;
using Definitions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BoardSlotUI : MonoBehaviour, IPointerClickHandler
{
    public Image HighlightImage;
    public TextMeshProUGUI CardNameText;
    public TextMeshProUGUI CardStatsText;
    public TextMeshProUGUI SlotIndexText;
    public bool IsValidDropTarget { get; set; }

    public Board Board;
    public Definitions.RowType RowType;
    public int Index;

    public BoardCard Occupant => Board?.GetSlot(RowType, Index)?.Occupant;

    public void Configure(Board board, Definitions.RowType rowType, int rowLocalIndex)
    {
        Board = board;
        RowType = rowType;
        Index = rowLocalIndex;
        AutoBindTextFields();
        EnsureChildGraphicsDoNotStealRaycasts();
        if (SlotIndexText != null) SlotIndexText.text = $"{ShortRowName(rowType)} {rowLocalIndex + 1}";
    }

    public void SetDisplay(BoardCard occupant)
    {
        AutoBindTextFields();
        EnsureChildGraphicsDoNotStealRaycasts();

        if (occupant == null)
        {
            if (CardNameText != null) CardNameText.text = ShortRowName(RowType);
            if (CardStatsText != null) CardStatsText.text = "Empty";
            return;
        }

        if (CardNameText != null) CardNameText.text = occupant.SourceCard.CardName;

        if (CardStatsText != null)
        {
            var targetName = (occupant.PlannedTarget as BoardCard)?.SourceCard?.CardName;
            if (occupant.PlannedTarget != null && string.IsNullOrEmpty(targetName)) targetName = "Town";
            CardStatsText.text = $"HP {occupant.Health}/{occupant.MaxHealth}   ATK {occupant.Attack}" +
                                 (string.IsNullOrEmpty(targetName) ? string.Empty : $"\n→ {targetName}");
        }
    }

    public void SetHighlight(bool on)
    {
        if (HighlightImage == null) return;
        HighlightImage.enabled = on;
        HighlightImage.color = on ? new Color(1f, 0.8f, 0.1f, 0.45f) : Color.clear;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        PlanningPhaseController.Instance?.HandleSlotClick(this);
    }

    private void AutoBindTextFields()
    {
        CardNameText ??= FindText("CardName");
        CardStatsText ??= FindText("CardStats");
        SlotIndexText ??= FindText("SlotIndex");

        var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        if (CardNameText == null && texts.Length > 0) CardNameText = texts[0];
        if (CardStatsText == null && texts.Length > 1) CardStatsText = texts[1];
        if (SlotIndexText == null && texts.Length > 2) SlotIndexText = texts[2];
    }

    private TextMeshProUGUI FindText(string childName)
    {
        var child = transform.Find(childName);
        return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    private void EnsureChildGraphicsDoNotStealRaycasts()
    {
        var rootGraphic = GetComponent<Graphic>();
        if (rootGraphic == null)
        {
            var image = gameObject.AddComponent<Image>();
            image.color = Color.clear;
            rootGraphic = image;
        }
        rootGraphic.raycastTarget = true;

        foreach (var graphic in GetComponentsInChildren<Graphic>(true))
        {
            if (graphic == null || graphic == rootGraphic) continue;
            graphic.raycastTarget = false;
        }
    }

    private static string ShortRowName(Definitions.RowType rowType) => rowType switch
    {
        Definitions.RowType.Vanguard => "Vanguard",
        Definitions.RowType.Building => "Building",
        Definitions.RowType.Human => "Human",
        Definitions.RowType.Town => "Town",
        _ => rowType.ToString()
    };
}
