using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Combat.UI
{
    public class DuelCameraSwitcher : MonoBehaviour
    {
        [Header("Views (set dynamically at duel start)")]
        public Transform SeatView;
        public Transform OverheadView;

        [Header("Thresholds (0.0 = bottom, 1.0 = top of screen)")]
        [SerializeField] private float _topThreshold = 0.85f;
        [SerializeField] private float _bottomThreshold = 0.15f;

        [Header("Smoothing")]
        [SerializeField] private float _transitionSpeed = 5f;

        private const float CanvasRefreshInterval = 1f;

        private Transform _currentTarget;
        private bool _boardViewLocked;
        private bool _perspectiveSwitchingDisabled;
        private Camera _cachedMainCamera;
        private Canvas[] _worldSpaceCanvases;
        private float _nextCanvasRefreshAt;

        public bool PerspectiveSwitchingEnabled => !_perspectiveSwitchingDisabled;

        void OnEnable()
        {
            if (SeatView != null)
            {
                _currentTarget = SeatView;
                transform.position = SeatView.position;
                transform.rotation = SeatView.rotation;
            }

            RefreshWorldSpaceCanvasCache();
            AssignWorldSpaceCanvasCamera(Camera.main);
        }

        void Update()
        {
            if (SeatView == null || OverheadView == null) return;

            if (_boardViewLocked)
            {
                _currentTarget = OverheadView;
            }
            else if (!_perspectiveSwitchingDisabled && Mouse.current != null)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                float mouseY = Screen.height > 0 ? mousePos.y / Screen.height : 0f;

                if (mouseY >= _topThreshold)
                    _currentTarget = OverheadView;
                else if (mouseY <= _bottomThreshold)
                    _currentTarget = SeatView;
            }

            MoveTowardsCurrentTarget(Time.deltaTime * _transitionSpeed);
        }

        public UniTask FocusBoardViewAsync(float duration = 0.45f, bool lockBoardView = true)
        {
            return FocusViewAsync(OverheadView, duration, lockBoardView);
        }

        public UniTask FocusSeatViewAsync(float duration = 0.35f)
        {
            _boardViewLocked = false;
            return FocusViewAsync(SeatView, duration, false);
        }

        private async UniTask FocusViewAsync(Transform target, float duration, bool lockBoardView)
        {
            if (target == null) return;

            _boardViewLocked = lockBoardView;
            _currentTarget = target;

            if (duration <= 0f)
            {
                transform.position = target.position;
                transform.rotation = target.rotation;
                return;
            }

            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;
            float elapsed = 0f;

            while (elapsed < duration && target != null)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transform.position = Vector3.Lerp(startPosition, target.position, t);
                transform.rotation = Quaternion.Slerp(startRotation, target.rotation, t);
                elapsed += Time.deltaTime;
                await UniTask.Yield();
            }

            if (target != null)
            {
                transform.position = target.position;
                transform.rotation = target.rotation;
            }
        }

        public void SetBoardViewLocked(bool locked)
        {
            _boardViewLocked = locked;
            if (locked && OverheadView != null)
                _currentTarget = OverheadView;
        }

        public void SetPerspectiveSwitchingEnabled(bool enabled)
        {
            _perspectiveSwitchingDisabled = !enabled;
        }

        private void MoveTowardsCurrentTarget(float t)
        {
            if (_currentTarget == null) return;

            transform.position = Vector3.Lerp(transform.position, _currentTarget.position, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, _currentTarget.rotation, t);
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;

            bool refreshed = false;
            if (_worldSpaceCanvases == null || Time.unscaledTime >= _nextCanvasRefreshAt)
            {
                RefreshWorldSpaceCanvasCache();
                refreshed = true;
            }

            if (refreshed || cam != _cachedMainCamera)
                AssignWorldSpaceCanvasCamera(cam);
        }

        private void RefreshWorldSpaceCanvasCache()
        {
            _nextCanvasRefreshAt = Time.unscaledTime + CanvasRefreshInterval;
            _worldSpaceCanvases = FindObjectsOfType<Canvas>();
        }

        private void AssignWorldSpaceCanvasCamera(Camera cam)
        {
            _cachedMainCamera = cam;
            if (cam == null || _worldSpaceCanvases == null) return;

            foreach (var canvas in _worldSpaceCanvases)
            {
                if (canvas == null || canvas.renderMode != RenderMode.WorldSpace) continue;
                if (canvas.worldCamera != cam)
                    canvas.worldCamera = cam;
            }
        }
    }
}
