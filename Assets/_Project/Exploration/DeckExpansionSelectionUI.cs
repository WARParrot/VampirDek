using System.Collections.Generic;
using Core;
using Cysharp.Threading.Tasks;
using Definitions;
using UnityEngine;
using UnityEngine.UI;

namespace Exploration
{
    /// <summary>
    /// Self-contained card-pick overlay used by WorldPortal next-night transitions.
    /// It avoids prefab/scene dependencies so the replay loop can work from any portal.
    /// </summary>
    public static class DeckExpansionSelectionUI
    {
        private static GameObject _root;

        public static async UniTask<CardDef> ShowAsync(IReadOnlyList<CardDef> choices)
        {
            if (choices == null || choices.Count == 0) return null;

            var tcs = new UniTaskCompletionSource<CardDef>();
            Build(choices, tcs);

            var previousMenuOpen = GlobalServices.IsMenuOpen;
            var previousLockState = Cursor.lockState;
            var previousCursorVisible = Cursor.visible;

            GlobalServices.IsMenuOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var chosen = await tcs.Task;

            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }

            GlobalServices.IsMenuOpen = previousMenuOpen;
            Cursor.lockState = previousLockState;
            Cursor.visible = previousCursorVisible;
            return chosen;
        }

        private static void Build(IReadOnlyList<CardDef> choices, UniTaskCompletionSource<CardDef> tcs)
        {
            if (_root != null) Object.Destroy(_root);

            _root = new GameObject("DeckExpansionSelectionCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Object.DontDestroyOnLoad(_root);

            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1100;

            var scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var root = (RectTransform)_root.transform;
            var bg = CreateImage(root, "Bg", new Color(0.01f, 0.005f, 0.02f, 0.88f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.zero);
            bg.raycastTarget = true;

            CreateText(root, "Title", "Выберите карту для колоды", 52, TextAnchor.MiddleCenter,
                new Color(1f, 0.86f, 0.48f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(1200, 90));

            CreateText(root, "Subtitle", "Она останется с вами в следующей ночи", 24, TextAnchor.MiddleCenter,
                new Color(0.82f, 0.76f, 0.66f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -165), new Vector2(1200, 50));

            float spacing = 380f;
            float startX = -spacing * (choices.Count - 1) * 0.5f;
            for (int i = 0; i < choices.Count; i++)
            {
                var card = choices[i];
                var panel = CreateImage(root, $"Choice_{i}", new Color(0.08f, 0.045f, 0.10f, 0.96f),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(startX + spacing * i, -10f), new Vector2(300f, 430f));
                panel.gameObject.AddComponent<Outline>().effectColor = new Color(0.82f, 0.62f, 0.25f, 0.95f);

                var button = panel.gameObject.AddComponent<Button>();
                var captured = card;
                button.onClick.AddListener(() => tcs.TrySetResult(captured));

                CreateText((RectTransform)panel.transform, "Name", card?.CardName ?? "Unknown", 30, TextAnchor.MiddleCenter,
                    new Color(1f, 0.92f, 0.72f, 1f), new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(0.5f, 1), new Vector2(0, -42), new Vector2(-28, 74));

                var stats = card == null ? string.Empty : $"{card.Type}\nATK {card.Attack}   HP {card.Health}\nSPD {card.MinSpeed}-{card.MaxSpeed}";
                CreateText((RectTransform)panel.transform, "Stats", stats, 22, TextAnchor.MiddleCenter,
                    new Color(0.86f, 0.82f, 0.74f, 1f), Vector2.zero, Vector2.one,
                    new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(-32, -190));

                var desc = card != null && !string.IsNullOrWhiteSpace(card.Description) ? card.Description : "Новая возможность для следующей попытки.";
                CreateText((RectTransform)panel.transform, "Description", desc, 18, TextAnchor.UpperCenter,
                    new Color(0.74f, 0.70f, 0.62f, 1f), new Vector2(0, 0), new Vector2(1, 0),
                    new Vector2(0.5f, 0), new Vector2(0, 34), new Vector2(-34, 130));
            }
        }

        private static Image CreateImage(RectTransform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(RectTransform parent, string name, string text, int fontSize, TextAnchor alignment, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            var label = go.GetComponent<Text>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }
    }
}
