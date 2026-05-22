using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

namespace Core
{
    public class HintUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Button _dismissButton;
        [SerializeField] private float _displayDuration = 5f;
        [SerializeField] private float _fadeDuration = 1f;

        private void Awake()
        {
            _dismissButton.onClick.AddListener(Dismiss);
            gameObject.SetActive(false);
        }

        public async UniTask ShowAsync(string message)
        {
            _messageText.text = message;
            _canvasGroup.alpha = 0f;
            gameObject.SetActive(true);

            await Fade(1f, _fadeDuration);
            await UniTask.Delay(TimeSpan.FromSeconds(_displayDuration));
            await Fade(0f, _fadeDuration);
            gameObject.SetActive(false);
        }

        public void Dismiss()
        {
            _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        private async UniTask Fade(float targetAlpha, float duration)
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