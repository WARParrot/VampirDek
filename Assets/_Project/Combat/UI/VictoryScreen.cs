using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.UI
{
    public class VictoryScreen : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private float _fadeInDuration = 0.6f;
        [SerializeField] private float _holdDuration = 1.5f;
        [SerializeField] private float _fadeOutDuration = 0.5f;

        private bool _isShowing;

        public async UniTask ShowVictoryAsync()
        {
            if (_isShowing) return;
            _isShowing = true;

            _canvasGroup.alpha = 0f;
            _titleText.text = "Победа!";
            _titleText.color = new Color(1f, 0.85f, 0f, 1f);
            gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < _fadeInDuration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / _fadeInDuration);
                await UniTask.Yield();
            }
            _canvasGroup.alpha = 1f;

            await UniTask.Delay(TimeSpan.FromSeconds(_holdDuration));

            elapsed = 0f;
            while (elapsed < _fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / _fadeOutDuration);
                await UniTask.Yield();
            }
            _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);

            _isShowing = false;
        }

        public async UniTask ShowDefeatAsync()
        {
            if (_isShowing) return;
            _isShowing = true;

            _canvasGroup.alpha = 0f;
            _titleText.text = "Поражение";
            _titleText.color = new Color(0.8f, 0.2f, 0.2f, 1f);
            gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < _fadeInDuration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / _fadeInDuration);
                await UniTask.Yield();
            }
            _canvasGroup.alpha = 1f;

            await UniTask.Delay(TimeSpan.FromSeconds(_holdDuration));

            elapsed = 0f;
            while (elapsed < _fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / _fadeOutDuration);
                await UniTask.Yield();
            }
            _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);

            _isShowing = false;
        }
    }
}