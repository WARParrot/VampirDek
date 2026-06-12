using Combat;
using Combat.UI;
using Definitions;
using TMPro;
using Shared.UI;
using Shared.Localization;
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
    [SerializeField] private Image _cardImage;
    [SerializeField] private Sprite _fallbackSprite;
    private CardAffordanceHighlighter _slotAffordance;
    private CardAffordanceHighlighter _cardAffordance;
    public int Index;

    public BoardCard Occupant => Board?.GetSlot(RowType, Index)?.Occupant;

    public void Configure(Board board, Definitions.RowType rowType, int rowLocalIndex)
    {
        Board = board;
        RowType = rowType;
        Index = rowLocalIndex;
        AutoBindTextFields();
        EnsureRaycastTargets();
        if (SlotIndexText != null) SlotIndexText.text = $"{ShortRowName(rowType)} {rowLocalIndex + 1}";
    }

    public void SetDisplay(BoardCard occupant)
    {
        AutoBindTextFields();
        EnsureRaycastTargets();

        if (occupant == null)
        {
            if (CardNameText != null) CardNameText.text = ShortRowName(RowType);
            if (CardStatsText != null) CardStatsText.text = "";
            if (SlotIndexText != null) SlotIndexText.text = "";
            if (_cardImage != null) _cardImage.enabled = false;
            return;
        }

        if (CardNameText != null) CardNameText.text = LocalizationService.CardName(occupant.SourceCard);

        if (CardStatsText != null)
        {
            CardStatsText.text = BoardCardRulesText.FormatBoardCardStats(occupant);
        }

        if (_cardImage != null)
        {
            var tex = Resources.Load<Texture2D>("Textures/" + occupant.SourceCard.CardName);
            if (tex != null)
            {
                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
                _cardImage.sprite = sprite;
                _cardImage.color = Color.white;
                _cardImage.enabled = true;
            }
            else
            {
                _cardImage.enabled = false;
            }
        }
    }

    public void SetHighlight(bool on)
    {
        SetSlotAffordance(on ? CardAffordanceState.Compatible : CardAffordanceState.None);
    }

    public void SetSlotAffordance(CardAffordanceState state)
    {
        EnsureAffordanceTargets();
        if (HighlightImage == null || _slotAffordance == null) return;

        bool on = state != CardAffordanceState.None;
        HighlightImage.enabled = on;
        HighlightImage.color = on ? Color.white : Color.clear;
        _slotAffordance.SetState(state, on ? 1f : 0f);
    }

    public void SetCardAffordance(CardAffordanceState state)
    {
        EnsureAffordanceTargets();
        if (_cardAffordance == null) return;
        _cardAffordance.SetState(state, state == CardAffordanceState.None ? 0f : 1f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Occupant != null)
        {
            CardDetailOverlay.Show(BoardCardRulesText.FormatBoardCardDetails(Occupant), transform);
        }

        PlanningPhaseController.Instance?.HandleSlotClick(this);
    }

    private void AutoBindTextFields()
    {
        CardNameText ??= FindText("CardName");
        CardStatsText ??= FindText("CardStats");
        SlotIndexText ??= FindText("SlotIndex");
        _cardImage ??= transform.Find("CardImage")?.GetComponent<Image>();
        EnsureAffordanceTargets();

        var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        if (CardNameText == null && texts.Length > 0) CardNameText = texts[0];
        if (CardStatsText == null && texts.Length > 1) CardStatsText = texts[1];
        if (SlotIndexText == null && texts.Length > 2) SlotIndexText = texts[2];
    }

    private void EnsureAffordanceTargets()
    {
        if (HighlightImage == null)
            HighlightImage = transform.Find("Highlight")?.GetComponent<Image>() ?? GetComponent<Image>();

        if (HighlightImage != null && _slotAffordance == null)
            _slotAffordance = HighlightImage.GetComponent<CardAffordanceHighlighter>() ?? HighlightImage.gameObject.AddComponent<CardAffordanceHighlighter>();

        if (_cardImage != null && _cardAffordance == null)
            _cardAffordance = _cardImage.GetComponent<CardAffordanceHighlighter>() ?? _cardImage.gameObject.AddComponent<CardAffordanceHighlighter>();
    }

    private TextMeshProUGUI FindText(string childName)
    {
        var child = transform.Find(childName);
        return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    private void EnsureRaycastTargets()
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
            if (graphic == null) continue;
            graphic.raycastTarget = true;
        }
    }

    private static string ShortRowName(Definitions.RowType rowType) => LocalizationService.ShortRowTypeName(rowType);
}
