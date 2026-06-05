using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using System;
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
        [SerializeField] private TextMeshProUGUI _warningText;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _displayDuration = 2f;
        [SerializeField] private float _fadeDuration = 0.3f;

        private bool _isShowing = false;

        private void Awake()
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
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
