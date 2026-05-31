using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Threading;

namespace Core
{
    public class HintUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Button _dismissButton;
        [SerializeField] private float _displayDuration = 5f;
        [SerializeField] private float _fadeDuration = 1f;

        private CancellationTokenSource _cts;

        private void Awake()
        {
            _dismissButton.onClick.AddListener(Dismiss);
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        public async UniTask ShowAsync(string message)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                _messageText.text = message;
                _canvasGroup.alpha = 0f;
                gameObject.SetActive(true);

                await Fade(1f, _fadeDuration, token);
                await UniTask.Delay(TimeSpan.FromSeconds(_displayDuration), cancellationToken: token);
                await Fade(0f, _fadeDuration, token);

                if (this != null && gameObject != null)
                    gameObject.SetActive(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        public void Dismiss()
        {
            _cts?.Cancel();
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
            if (this != null && gameObject != null)
                gameObject.SetActive(false);
        }

        private async UniTask Fade(float targetAlpha, float duration, CancellationToken token)
        {
            if (_canvasGroup == null) return;

            float startAlpha = _canvasGroup.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (token.IsCancellationRequested) return;
                if (_canvasGroup == null) return;

                _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                elapsed += Time.deltaTime;
                await UniTask.Yield(token);
            }

            if (_canvasGroup != null)
                _canvasGroup.alpha = targetAlpha;
        }
    }
}