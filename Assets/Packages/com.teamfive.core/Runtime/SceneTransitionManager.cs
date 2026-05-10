using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core
{
    public class SceneTransitionManager : MonoBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }

        private Vector3 _savedPosition;
        private Quaternion _savedRotation;
        private bool _hasSaved;

        private Camera _mainCamera;

        private void Awake()
        {
            Instance = this;
            _mainCamera = Camera.main;
            DontDestroyOnLoad(gameObject);
        }

        public void SaveCameraState()
        {
            if (_mainCamera != null)
            {
                _savedPosition = _mainCamera.transform.position;
                _savedRotation = _mainCamera.transform.rotation;
                _hasSaved = true;
            }
        }

        public async UniTask RestoreCameraAsync(float duration = 1.0f)
        {
            if (!_hasSaved || _mainCamera == null) return;
            await MoveCameraToPoint(_mainCamera, _savedPosition, _savedRotation, duration);
        }

        public async UniTask MoveCameraToTransform(Transform target, float duration)
        {
            if (_mainCamera != null)
                await MoveCameraToPoint(_mainCamera, target.position, target.rotation, duration);
        }

        private async UniTask MoveCameraToPoint(Camera cam, Vector3 targetPos, Quaternion targetRot, float duration)
        {
            Vector3 startPos = cam.transform.position;
            Quaternion startRot = cam.transform.rotation;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                cam.transform.position = Vector3.Lerp(startPos, targetPos, t);
                cam.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                elapsed += Time.deltaTime;
                await UniTask.Yield();
            }
            cam.transform.position = targetPos;
            cam.transform.rotation = targetRot;
        }
    }
}