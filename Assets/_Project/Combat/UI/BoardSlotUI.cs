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
    [SerializeField] private Sprite _emptySlotSprite;
    private static Sprite _runtimeEmptySlotSprite;
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
            SetHighlightSprite(GetEmptySlotSprite());
            return;
        }

        if (CardNameText != null) CardNameText.text = LocalizationService.CardName(occupant.SourceCard);

        if (CardStatsText != null)
        {
            CardStatsText.text = BoardCardRulesText.FormatBoardCardStats(occupant);
        }

        var cardSprite = LoadCardSprite(occupant.SourceCard);
        if (_cardImage != null)
        {
            if (cardSprite != null)
            {
                _cardImage.sprite = cardSprite;
                _cardImage.color = Color.white;
                _cardImage.enabled = true;
            }
            else
            {
                _cardImage.enabled = false;
            }
        }

        SetHighlightSprite(cardSprite != null ? cardSprite : (_fallbackSprite != null ? _fallbackSprite : GetEmptySlotSprite()));
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
        if (on && HighlightImage.sprite == null)
            SetHighlightSprite(GetEmptySlotSprite());

        var tint = GetHighlightTint(state);
        if (on && tint.a <= 0f) tint.a = 0.001f;

        HighlightImage.enabled = on;
        HighlightImage.color = tint;
        HighlightImage.SetAllDirty();
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
        EnsureSlotAffordanceImage();

        if (HighlightImage != null && _slotAffordance == null)
            _slotAffordance = HighlightImage.GetComponent<CardAffordanceHighlighter>() ?? HighlightImage.gameObject.AddComponent<CardAffordanceHighlighter>();

        if (_cardImage != null && _cardAffordance == null)
            _cardAffordance = _cardImage.GetComponent<CardAffordanceHighlighter>() ?? _cardImage.gameObject.AddComponent<CardAffordanceHighlighter>();
    }

    private void EnsureSlotAffordanceImage()
    {
        if (HighlightImage == null)
            HighlightImage = transform.Find("Highlight")?.GetComponent<Image>();

        if (HighlightImage == null)
        {
            var overlay = new GameObject("AffordanceOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlay.transform.SetParent(transform, false);
            overlay.transform.SetAsLastSibling();

            var rect = overlay.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            HighlightImage = overlay.GetComponent<Image>();
            HighlightImage.sprite = GetEmptySlotSprite();
            HighlightImage.type = Image.Type.Simple;
            HighlightImage.preserveAspect = false;
            HighlightImage.raycastTarget = false;
            HighlightImage.enabled = false;
        }
        else
        {
            if (HighlightImage.sprite == null)
                HighlightImage.sprite = GetEmptySlotSprite();
            HighlightImage.raycastTarget = false;
        }
    }

    private Sprite LoadCardSprite(CardDef cardDef)
    {
        if (cardDef == null || string.IsNullOrEmpty(cardDef.CardName)) return null;

        var tex = Resources.Load<Texture2D>("Textures/" + cardDef.CardName);
        return tex != null
            ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f)
            : null;
    }

    private void SetHighlightSprite(Sprite sprite)
    {
        EnsureAffordanceTargets();
        if (HighlightImage == null) return;

        HighlightImage.sprite = sprite != null ? sprite : GetEmptySlotSprite();
        HighlightImage.type = Image.Type.Simple;
        HighlightImage.preserveAspect = false;
    }

    private Sprite GetEmptySlotSprite()
    {
        if (_emptySlotSprite != null) return _emptySlotSprite;
        if (_fallbackSprite != null) return _fallbackSprite;
        return GetRuntimeEmptySlotSprite();
    }

    private static Sprite GetRuntimeEmptySlotSprite()
    {
        if (_runtimeEmptySlotSprite != null) return _runtimeEmptySlotSprite;

        const int width = 64;
        const int height = 96;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Runtime Empty Board Slot Texture",
            hideFlags = HideFlags.DontSave
        };

        var pixels = new Color32[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bool border = x < 2 || y < 2 || x >= width - 2 || y >= height - 2;
                pixels[y * width + x] = border
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(255, 255, 255, 96);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        _runtimeEmptySlotSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        _runtimeEmptySlotSprite.name = "Runtime Empty Board Slot Sprite";
        return _runtimeEmptySlotSprite;
    }

    private static Color GetHighlightTint(CardAffordanceState state)
    {
        return state switch
        {
            CardAffordanceState.Compatible => new Color(0.65f, 1f, 0.78f, 0.10f),
            CardAffordanceState.Incompatible => new Color(1f, 0.62f, 0.18f, 0.12f),
            CardAffordanceState.Blocked => new Color(1f, 0.18f, 0.16f, 0.14f),
            CardAffordanceState.Selected => new Color(0.55f, 0.92f, 1f, 0.10f),
            CardAffordanceState.Target => new Color(1f, 0.28f, 0.18f, 0.12f),
            CardAffordanceState.Planned => new Color(0.45f, 0.70f, 1f, 0.10f),
            CardAffordanceState.Warning => new Color(1f, 0.80f, 0.20f, 0.14f),
            _ => Color.clear
        };
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
            if (graphic == null || graphic == HighlightImage) continue;
            graphic.raycastTarget = true;
        }
    }

    private static string ShortRowName(Definitions.RowType rowType) => LocalizationService.ShortRowTypeName(rowType);
}
