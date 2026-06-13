using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shared.UI
{
    // Center-screen pulsing flash used for special effect callouts ("Безвкусица!", etc.).
    // Plays a sequence of (text, color) frames — the first is meant to be a red pulsing hit-style
    // shout, the rest are usually a quieter white follow-up.
    public class EffectFlashOverlay : MonoBehaviour
    {
        private const string CanvasName = "EffectFlashOverlayCanvas";
        private static EffectFlashOverlay _instance;

        private TextMeshProUGUI _text;
        private CanvasGroup _group;
        private RectTransform _rect;
        private float _frameDuration;
        private float _elapsed;
        private int _index;
        private List<Frame> _frames;
        private bool _pulsePhase1;

        public struct Frame
        {
            public string Text;
            public Color Color;
            public float Duration;
            public bool Pulse;
        }

        public static void ShowGourmetRefusal()
        {
            Show(new List<Frame>
            {
                new Frame { Text = "Безвкусица!", Color = new Color(1f, 0.15f, 0.15f, 1f), Duration = 0.9f, Pulse = true },
                new Frame { Text = "Я уже это ел", Color = Color.white, Duration = 1.2f, Pulse = false }
            });
        }

        public static void ShowLonerFound()
        {
            Show(new List<Frame>
            {
                new Frame { Text = "О нет, они всё-таки нашли меня…", Color = Color.white, Duration = 1.8f, Pulse = false }
            });
        }

        public static void ShowBloodWitchSpawn()
        {
            Show(new List<Frame>
            {
                new Frame { Text = "Никто не сбежит!", Color = new Color(1f, 0.1f, 0.12f, 1f), Duration = 1.4f, Pulse = true }
            });
        }

        public static void ShowProvokerBlock(string cardName)
        {
            string message = cardName switch
            {
                "FreshSpawn" => "Молодняк ведёт себя слишком вызывающе — пора поставить его на место",
                "Decoy"      => "Не могу упустить ту сладость",
                _            => "Сначала разберись с провокатором"
            };
            Show(new List<Frame>
            {
                new Frame { Text = message, Color = new Color(1f, 0.15f, 0.15f, 1f), Duration = 1.6f, Pulse = false }
            });
        }

        public static void ShowElusiveBlock()
        {
            Show(new List<Frame>
            {
                new Frame { Text = "Я не могу его поймать…", Color = new Color(0.85f, 0.85f, 1f, 1f), Duration = 1.6f, Pulse = false }
            });
        }

        public static void ShowBuildingShield()
        {
            Show(new List<Frame>
            {
                new Frame { Text = "Здание закрывает цель — сначала разрушь его", Color = new Color(0.95f, 0.75f, 0.35f, 1f), Duration = 1.6f, Pulse = false }
            });
        }

        public static void Show(List<Frame> frames)
        {
            if (frames == null || frames.Count == 0) return;
            var overlay = GetOrCreate();
            overlay.Play(frames);
        }

        private static EffectFlashOverlay GetOrCreate()
        {
            if (_instance != null) return _instance;

            var canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6000;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var overlayObject = new GameObject("EffectFlashOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(EffectFlashOverlay));
            overlayObject.transform.SetParent(canvas.transform, false);
            _instance = overlayObject.GetComponent<EffectFlashOverlay>();
            _instance.Build();
            return _instance;
        }

        private void Build()
        {
            _rect = GetComponent<RectTransform>();
            _rect.anchorMin = new Vector2(0.5f, 0.5f);
            _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.anchoredPosition = Vector2.zero;
            _rect.sizeDelta = new Vector2(1600f, 240f);

            _group = GetComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _group.alpha = 0f;

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            _text = textObject.GetComponent<TextMeshProUGUI>();
            _text.alignment = TextAlignmentOptions.Center;
            _text.fontSize = 96f;
            _text.fontStyle = FontStyles.Bold;
            _text.raycastTarget = false;
            _text.text = string.Empty;
            _text.color = Color.white;
        }

        private void Play(List<Frame> frames)
        {
            _frames = frames;
            _index = 0;
            _elapsed = 0f;
            ApplyCurrentFrame();
        }

        private void ApplyCurrentFrame()
        {
            if (_frames == null || _index >= _frames.Count)
            {
                _group.alpha = 0f;
                _frames = null;
                return;
            }
            var frame = _frames[_index];
            _text.text = frame.Text;
            _text.color = frame.Color;
            _frameDuration = Mathf.Max(0.1f, frame.Duration);
            _pulsePhase1 = frame.Pulse;
            _group.alpha = 1f;
        }

        private void Update()
        {
            if (_frames == null) return;
            _elapsed += Time.unscaledDeltaTime;
            var frame = _frames[_index];

            if (frame.Pulse)
            {
                // Two-beat pulse with a quick fade-out tail.
                float t = _elapsed / _frameDuration;
                float pulse = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 4f));
                _group.alpha = Mathf.Clamp01(pulse);
            }
            else
            {
                float t = _elapsed / _frameDuration;
                _group.alpha = Mathf.Clamp01(1.2f - t);
            }

            if (_elapsed >= _frameDuration)
            {
                _index++;
                _elapsed = 0f;
                ApplyCurrentFrame();
            }
        }
    }
}
