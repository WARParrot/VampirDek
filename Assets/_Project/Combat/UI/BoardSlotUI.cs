using System.Collections.Generic;
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
    private static readonly Dictionary<string, Sprite> s_cardSpriteCache = new();
    private CardAffordanceHighlighter _slotAffordance;
    private CardAffordanceHighlighter _cardAffordance;
    private bool _uiInitialized;
    private BoardCard _lastDisplayedOccupant;
    private CardAffordanceState _lastSlotAffordanceState = CardAffordanceState.None;
    private CardAffordanceState _lastCardAffordanceState = CardAffordanceState.None;
    public int Index;

    public BoardCard Occupant => Board?.GetSlot(RowType, Index)?.Occupant;

    public void Configure(Board board, Definitions.RowType rowType, int rowLocalIndex)
    {
        Board = board;
        RowType = rowType;
        Index = rowLocalIndex;
        EnsureStaticBindings();
        ClearEmptySlotText();
        _lastDisplayedOccupant = null;
    }

    public void SetDisplay(BoardCard occupant)
    {
        EnsureStaticBindings();

        if (occupant == null)
        {
            if (_lastDisplayedOccupant != null)
            {
                ClearEmptySlotText();
                _lastDisplayedOccupant = null;
            }
            if (_cardImage != null && _cardImage.enabled) _cardImage.enabled = false;
            SetHighlightSprite(GetEmptySlotSprite());
            return;
        }

        string cardName = LocalizationService.CardName(occupant.SourceCard);
        if (CardNameText != null && CardNameText.text != cardName) CardNameText.text = cardName;

        if (CardStatsText != null)
        {
            string stats = BoardCardRulesText.FormatBoardCardStats(occupant);
            if (CardStatsText.text != stats) CardStatsText.text = stats;
        }
        if (SlotIndexText != null && SlotIndexText.text.Length != 0) SlotIndexText.text = string.Empty;

        var cardSprite = LoadCardSprite(occupant.SourceCard);
        if (_cardImage != null)
        {
            if (cardSprite != null)
            {
                if (_cardImage.sprite != cardSprite) _cardImage.sprite = cardSprite;
                if (_cardImage.color != Color.white) _cardImage.color = Color.white;
                if (!_cardImage.enabled) _cardImage.enabled = true;
            }
            else if (_cardImage.enabled)
            {
                _cardImage.enabled = false;
            }
        }

        SetHighlightSprite(cardSprite != null ? cardSprite : (_fallbackSprite != null ? _fallbackSprite : GetEmptySlotSprite()));
        _lastDisplayedOccupant = occupant;
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
        if (_lastSlotAffordanceState == state && HighlightImage.enabled == on) return;
        if (on && HighlightImage.sprite == null)
            SetHighlightSprite(GetEmptySlotSprite());

        var tint = GetHighlightTint(state);
        if (on && tint.a <= 0f) tint.a = 0.001f;

        if (HighlightImage.enabled != on) HighlightImage.enabled = on;
        if (HighlightImage.color != tint) HighlightImage.color = tint;
        HighlightImage.SetAllDirty();
        _slotAffordance.SetState(state, on ? 1f : 0f);
        _lastSlotAffordanceState = state;
    }

    public void SetCardAffordance(CardAffordanceState state)
    {
        EnsureAffordanceTargets();
        if (_cardAffordance == null) return;
        if (_lastCardAffordanceState == state) return;
        _cardAffordance.SetState(state, state == CardAffordanceState.None ? 0f : 1f);
        _lastCardAffordanceState = state;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Occupant != null)
        {
            CardDetailOverlay.Show(BoardCardRulesText.FormatBoardCardDetails(Occupant), transform);
        }

        PlanningPhaseController.Instance?.HandleSlotClick(this);
    }

    private void ClearEmptySlotText()
    {
        // Keep empty-slot/slot-index labels from being mistaken for row labels while preserving
        // occupied card name and stats text on the board card itself.
        if (CardNameText != null && CardNameText.text.Length != 0) CardNameText.text = string.Empty;
        if (CardStatsText != null && CardStatsText.text.Length != 0) CardStatsText.text = string.Empty;
        if (SlotIndexText != null && SlotIndexText.text.Length != 0) SlotIndexText.text = string.Empty;
    }

    private void EnsureStaticBindings()
    {
        if (_uiInitialized) return;
        AutoBindTextFields();
        EnsureRaycastTargets();
        _uiInitialized = true;
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
            HighlightImage = transform.Find("AffordanceOverlay")?.GetComponent<Image>()
                             ?? transform.Find("Highlight")?.GetComponent<Image>();

        // Some prefabs serialized HighlightImage to the slot root Image. That root image is the
        // stable empty-slot/raycast surface, so affordance clearing must not disable or rematerial it.
        // Use a dedicated non-raycast overlay for compatible/incompatible shader states instead.
        if (HighlightImage == null || HighlightImage.transform == transform)
        {
            HighlightImage = GetOrCreateAffordanceOverlay();
            _slotAffordance = null;
        }

        ConfigureAffordanceOverlay(HighlightImage);
    }

    private Image GetOrCreateAffordanceOverlay()
    {
        var existing = transform.Find("AffordanceOverlay")?.GetComponent<Image>()
                       ?? transform.Find("Highlight")?.GetComponent<Image>();
        if (existing != null && existing.transform != transform)
            return existing;

        var overlay = new GameObject("AffordanceOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(transform, false);
        overlay.transform.SetAsLastSibling();

        var rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return overlay.GetComponent<Image>();
    }

    private void ConfigureAffordanceOverlay(Image image)
    {
        if (image == null) return;

        if (image.sprite == null)
            image.sprite = GetEmptySlotSprite();

        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.raycastTarget = false;
        image.enabled = false;
        image.SetAllDirty();
    }

    private Sprite LoadCardSprite(CardDef cardDef)
    {
        var cardName = cardDef?.CardName;
        if (string.IsNullOrEmpty(cardName)) return null;
        if (s_cardSpriteCache.TryGetValue(cardName, out var cached)) return cached;

        var tex = Resources.Load<Texture2D>("Textures/" + cardName);
        var sprite = tex != null
            ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f)
            : null;
        s_cardSpriteCache[cardName] = sprite;
        return sprite;
    }

    private void SetHighlightSprite(Sprite sprite)
    {
        EnsureAffordanceTargets();
        if (HighlightImage == null) return;

        var wanted = sprite != null ? sprite : GetEmptySlotSprite();
        bool dirty = false;
        if (HighlightImage.sprite != wanted) { HighlightImage.sprite = wanted; dirty = true; }
        if (HighlightImage.type != Image.Type.Simple) { HighlightImage.type = Image.Type.Simple; dirty = true; }
        if (HighlightImage.preserveAspect) { HighlightImage.preserveAspect = false; dirty = true; }
        if (dirty) HighlightImage.SetAllDirty();
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
            rootGraphic = gameObject.AddComponent<Image>();
        }

        EnsureRenderableRaycastImage(rootGraphic as Image);
        rootGraphic.raycastTarget = true;

        foreach (var graphic in GetComponentsInChildren<Graphic>(true))
        {
            if (graphic == null || graphic.transform == transform) continue;
            // The slot root owns hit-testing. Child text/card/affordance graphics should not
            // compete in the raycast stack or mask neighboring empty slots.
            graphic.raycastTarget = false;
        }
    }

    private void EnsureRenderableRaycastImage(Image image)
    {
        if (image == null) return;

        bool dirty = false;
        if (image.sprite == null)
        {
            image.sprite = GetEmptySlotSprite();
            dirty = true;
        }

        if (image.type != Image.Type.Simple) { image.type = Image.Type.Simple; dirty = true; }
        if (image.preserveAspect) { image.preserveAspect = false; dirty = true; }

        if (image.color.a <= 0f)
        {
            var color = image.color;
            color.a = 0.001f;
            image.color = color;
            dirty = true;
        }

        if (dirty) image.SetAllDirty();
    }

    private static string ShortRowName(Definitions.RowType rowType) => LocalizationService.ShortRowTypeName(rowType);
}
