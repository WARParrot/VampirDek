using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Core;

namespace Exploration
{
    /// <summary>
    /// Fullscreen dark-screen ending shown when the player leaves the starting room.
    /// Two branches based on <see cref="EscapeQuestState.PotionConsumed"/>:
    ///   drank   → defeated the vampire and survived as a human.
    ///   didn't  → defeated the vampire but turned into a ghoul.
    /// Self-contained: builds its own canvas on demand, no scene wiring needed.
    /// </summary>
    public static class EscapeRoomEndingUI
    {
        private const string SurvivedText =
            "Вы вышли из комнаты.\n\n" +
            "Ужасы прошлой ночи позади. Зелье из шкатулки сделало своё —\n" +
            "вы победили вампира и остались человеком.\n\n" +
            "Вы выжили.";

        private const string GhoulText =
            "Вы вышли из комнаты.\n\n" +
            "Вы победили вампира — но яд его укуса уже расходился по венам.\n" +
            "Противоядие так и осталось в шкатулке.\n\n" +
            "Вы выжили, но обратились в гуля.";

        private static GameObject _root;

        public static void ShowEnding()
        {
            if (_root != null) return; // idempotent
            bool survived = EscapeQuestState.PotionConsumed;
            _root = Build(survived ? SurvivedText : GhoulText, survived);
            GlobalServices.IsMenuOpen = true;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static void Dismiss()
        {
            if (_root != null) { Object.Destroy(_root); _root = null; }
            Time.timeScale = 1f;
            GlobalServices.IsMenuOpen = false;
        }

        private static GameObject Build(string body, bool survived)
        {
            var canvasGo = new GameObject("EscapeRoomEndingCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
                typeof(EndingInputForwarder));
            Object.DontDestroyOnLoad(canvasGo);
            var c = canvasGo.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 1000;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgRt = (RectTransform)bgGo.transform;
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            bgGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 1f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(canvasGo.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = new Vector2(0.5f, 0.5f);
            trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(1400, 700);
            trt.anchoredPosition = new Vector2(0, 40);
            var txt = textGo.GetComponent<Text>();
            txt.text = body;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontSize = 36;
            txt.color = survived
                ? new Color(0.92f, 0.90f, 0.84f, 1f)
                : new Color(0.85f, 0.55f, 0.55f, 1f);
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;

            var hintGo = new GameObject("Hint", typeof(RectTransform), typeof(Text));
            hintGo.transform.SetParent(canvasGo.transform, false);
            var hrt = (RectTransform)hintGo.transform;
            hrt.anchorMin = new Vector2(0.5f, 0f);
            hrt.anchorMax = new Vector2(0.5f, 0f);
            hrt.pivot = new Vector2(0.5f, 0f);
            hrt.sizeDelta = new Vector2(800, 60);
            hrt.anchoredPosition = new Vector2(0, 60);
            var hint = hintGo.GetComponent<Text>();
            hint.text = "Esc / Enter — закрыть";
            hint.alignment = TextAnchor.MiddleCenter;
            hint.fontSize = 20;
            hint.color = new Color(0.55f, 0.52f, 0.45f, 1f);
            hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hint.raycastTarget = false;

            return canvasGo;
        }

        /// <summary>
        /// Tiny MonoBehaviour attached to the ending canvas so Esc/Enter can dismiss it.
        /// Uses unscaled-time-safe Input System polling (timeScale = 0 while ending is up).
        /// </summary>
        private class EndingInputForwarder : MonoBehaviour
        {
            private void Update()
            {
                var kb = Keyboard.current;
                if (kb == null) return;
                if (kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                    Dismiss();
            }
        }
    }
}
