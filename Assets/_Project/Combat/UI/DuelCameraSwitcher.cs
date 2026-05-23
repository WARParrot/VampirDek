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

        private Transform _currentTarget;

        void OnEnable()
        {
            if (SeatView != null)
            {
                _currentTarget = SeatView;
                transform.position = SeatView.position;
                transform.rotation = SeatView.rotation;
            }
        }

        void Update()
        {
            if (SeatView == null || OverheadView == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            float mouseY = mousePos.y / Screen.height;

            if (mouseY >= _topThreshold)
                _currentTarget = OverheadView;
            else if (mouseY <= _bottomThreshold)
                _currentTarget = SeatView;

            if (_currentTarget != null)
            {
                transform.position = Vector3.Lerp(transform.position, _currentTarget.position,
                    Time.deltaTime * _transitionSpeed);
                transform.rotation = Quaternion.Slerp(transform.rotation, _currentTarget.rotation,
                    Time.deltaTime * _transitionSpeed);
            }
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;

            foreach (var canvas in FindObjectsOfType<Canvas>())
            {
                if (canvas.renderMode != RenderMode.WorldSpace) continue;
                canvas.worldCamera = cam;
            }
        }
    }
}