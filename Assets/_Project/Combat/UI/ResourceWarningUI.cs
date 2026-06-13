using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using System;
using Combat;
using Core;
#if DOTWEEN
using DG.Tweening;
#endif

namespace Combat.UI
{
    /// <summary>
    /// Отображает предупреждения о недостатке ресурсов в центре экрана
    /// </summary>
    public class ResourceWarningUI : MonoBehaviour
    {
        public static ResourceWarningUI Current { get; private set; }

        [SerializeField] private TextMeshProUGUI _warningText;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _displayDuration = 2f;
        [SerializeField] private float _fadeDuration = 0.3f;

        private TextMeshProUGUI _headerText;
        private bool _isShowing = false;

        private bool _subscribed;

        private void Awake()
        {
            Current = this;

            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            ReparentToOverlayCanvas();

            _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_subscribed) TrySubscribeHandFull();
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            if (!_subscribed) return;
            EventBus bus;
            try { bus = GlobalServices.EventBus; } catch { return; }
            if (bus == null) return;
            bus.Unsubscribe<HandFullEvent>(OnHandFull);
            _subscribed = false;
        }

        private void TrySubscribeHandFull()
        {
            EventBus bus;
            try { bus = GlobalServices.EventBus; } catch { return; }
            if (bus == null) return;
            bus.Subscribe<HandFullEvent>(OnHandFull);
            _subscribed = true;
        }

        private void OnHandFull(HandFullEvent e)
        {
            // Only warn for the player.
            var state = DuelManagerProxy.Instance?.CurrentDuelState;
            if (state == null || e.Side != state.PlayerSide) return;
            string msg = $"Рука переполнена (максимум <color=#ffd864>{SideState.MaxHandSize}</color> карт). Добор карты пропущен.";
            ShowWarningAsync(msg).Forget();
        }

        private void ReparentToOverlayCanvas()
        {
            var overlay = FindScreenOverlayCanvas();
            if (overlay == null) return;
            var rt = transform as RectTransform;
            if (rt == null) return;

            rt.SetParent(overlay.transform, false);
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -28f);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            rt.sizeDelta = new Vector2(900f, 120f);

            // Bind the whole panel into a single dark visual block.
            var bg = GetComponent<UnityEngine.UI.Image>();
            if (bg != null)
            {
                bg.enabled = true;
                bg.color = new Color(0.05f, 0.03f, 0.08f, 0.92f);
                bg.raycastTarget = false;
            }
            var ol = GetComponent<UnityEngine.UI.Outline>();
            if (ol == null) ol = gameObject.AddComponent<UnityEngine.UI.Outline>();
            ol.effectColor = new Color(0.92f, 0.32f, 0.28f, 0.95f);
            ol.effectDistance = new Vector2(2f, -2f);

            // No separate "ПРЕДУПРЕЖДЕНИЕ" label — the dark panel + golden outline already
            // tell the player this is a warning. We HIDE leftover header children (don't
            // Destroy in Awake — that triggers an IndexedSet OOB inside CanvasUpdateRegistry).
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child != null && child.name == "WarningHeader")
                    child.gameObject.SetActive(false);
            }
            if (_headerText != null)
                _headerText.gameObject.SetActive(false);

            if (_warningText != null)
            {
                var trt = _warningText.rectTransform;
                trt.anchorMin = new Vector2(0f, 0f);
                trt.anchorMax = new Vector2(1f, 1f);
                trt.pivot = new Vector2(0.5f, 0.5f);
                trt.offsetMin = new Vector2(24f, 14f);
                trt.offsetMax = new Vector2(-24f, -14f);
                _warningText.alignment = TMPro.TextAlignmentOptions.Center;
                _warningText.textWrappingMode = TMPro.TextWrappingModes.Normal;
                _warningText.overflowMode = TMPro.TextOverflowModes.Overflow;
                _warningText.textWrappingMode = TMPro.TextWrappingModes.Normal;
                _warningText.enableAutoSizing = true;
                _warningText.fontSizeMin = 18f;
                _warningText.fontSizeMax = 28f;
                _warningText.color = new Color(1f, 0.95f, 0.9f, 1f);
                _warningText.richText = true;
            }
        }

        private static Canvas FindScreenOverlayCanvas()
        {
            var all = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            foreach (var c in all)
            {
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
                    return c;
            }
            return null;
        }

        /// <summary>
        /// Показывает предупреждение о недостатке ресурсов
        /// </summary>
        public async UniTask ShowWarningAsync(string message)
        {
            if (_isShowing) return;
            if (_warningText == null)
            {
                Debug.LogWarning("[ResourceWarningUI] _warningText не назначен в инспекторе");
                return;
            }
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                Debug.LogWarning("[ResourceWarningUI] CanvasGroup отсутствует");
                return;
            }

            _isShowing = true;
            _warningText.text = message;
            _canvasGroup.alpha = 0f;
            gameObject.SetActive(true);

#if DOTWEEN
            transform.DOComplete();
            transform.DOShakePosition(0.4f, new Vector3(20f, 0f, 0f), 14, 90f, false, true);
#endif

            await FadeAsync(1f, _fadeDuration);
            await UniTask.Delay(TimeSpan.FromSeconds(_displayDuration));
            await FadeAsync(0f, _fadeDuration);

            if (this != null && gameObject != null)
                gameObject.SetActive(false);
            _isShowing = false;
        }

        private async UniTask FadeAsync(float targetAlpha, float duration)
        {
            float startAlpha = _canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                elapsed += Time.deltaTime;
                await UniTask.Yield();
            }

            _canvasGroup.alpha = targetAlpha;
        }
    }
}
