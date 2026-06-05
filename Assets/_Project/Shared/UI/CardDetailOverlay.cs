using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Shared.Localization;

namespace Shared.UI
{
    public class CardDetailOverlay : MonoBehaviour
    {
        private const string CanvasName = "CardDetailOverlayCanvas";
        private const string OverlayName = "CardDetailOverlay";
        private static CardDetailOverlay _instance;

        private TextMeshProUGUI _bodyText;
        private CanvasGroup _canvasGroup;
        private int _shownFrame = -1;

        public static void Show(string details, Transform anchor = null)
        {
            if (string.IsNullOrWhiteSpace(details)) return;
            var overlay = GetOrCreate(anchor);
            overlay.ShowInternal(details);
        }

        public static void Hide()
        {
            if (_instance != null) _instance.HideInternal();
        }

        private static CardDetailOverlay GetOrCreate(Transform anchor)
        {
            if (_instance != null) return _instance;

            var existing = GameObject.Find(CanvasName);
            Canvas canvas;
            if (existing != null && existing.TryGetComponent(out canvas))
            {
                // Reuse an authored overlay canvas if the scene/prefab already provides one.
            }
            else
            {
                var canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 5000;

                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }

            var overlayObject = new GameObject(OverlayName, typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(CardDetailOverlay));
            overlayObject.transform.SetParent(canvas.transform, false);
            _instance = overlayObject.GetComponent<CardDetailOverlay>();
            _instance.Build();
            _instance.HideInternal();
            return _instance;
        }

        private void Build()
        {
            var rootRect = GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var blocker = GetComponent<Image>();
            blocker.color = Color.clear;
            blocker.raycastTarget = false;

            _canvasGroup = GetComponent<CanvasGroup>();

            var panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panelObject.transform.SetParent(transform, false);
            var panel = panelObject.GetComponent<RectTransform>();
            panel.anchorMin = new Vector2(1f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 1f);
            panel.anchoredPosition = new Vector2(-24f, -24f);
            panel.sizeDelta = new Vector2(460f, 280f);

            var panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.06f, 0.05f, 0.07f, 0.96f);
            panelImage.raycastTarget = false;

            var layout = panelObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 14, 14);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = panelObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var titleObject = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObject.transform.SetParent(panelObject.transform, false);
            var titleText = titleObject.GetComponent<TextMeshProUGUI>();
            titleText.text = LocalizationService.T("ui.card_details.title", "Card details");
            titleText.fontSize = 24f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = new Color(1f, 0.88f, 0.62f, 1f);
            titleText.raycastTarget = false;

            var bodyObject = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
            bodyObject.transform.SetParent(panelObject.transform, false);
            _bodyText = bodyObject.GetComponent<TextMeshProUGUI>();
            _bodyText.fontSize = 20f;
            _bodyText.fontSizeMin = 13f;
            _bodyText.fontSizeMax = 22f;
            _bodyText.enableAutoSizing = true;
            _bodyText.enableWordWrapping = true;
            _bodyText.overflowMode = TextOverflowModes.Overflow;
            _bodyText.color = Color.white;
            _bodyText.raycastTarget = false;

            var closeObject = new GameObject("CloseHint", typeof(RectTransform), typeof(TextMeshProUGUI));
            closeObject.transform.SetParent(panelObject.transform, false);
            var closeText = closeObject.GetComponent<TextMeshProUGUI>();
            closeText.text = LocalizationService.T("ui.card_details.close_hint", "Click anywhere to close");
            closeText.fontSize = 13f;
            closeText.color = new Color(0.85f, 0.78f, 0.65f, 1f);
            closeText.alignment = TextAlignmentOptions.Right;
            closeText.raycastTarget = false;
        }

        private void ShowInternal(string details)
        {
            _bodyText.text = details;
            gameObject.SetActive(true);
            _shownFrame = Time.frameCount;
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
            transform.SetAsLastSibling();
        }

        private void HideInternal()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_canvasGroup == null || _canvasGroup.alpha <= 0f) return;
            if (Time.frameCount == _shownFrame) return;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                HideInternal();
            }
        }
    }
}
