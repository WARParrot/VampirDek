using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core
{
    public class SceneTransitionManager : MonoBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }

        void Awake() => Instance = this;

        public async UniTask TransitionCameraAsync(Camera targetCamera, Transform destination, float duration)
        {
            Vector3 startPos = targetCamera.transform.position;
            Quaternion startRot = targetCamera.transform.rotation;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                targetCamera.transform.position = Vector3.Lerp(startPos, destination.position, elapsed / duration);
                targetCamera.transform.rotation = Quaternion.Slerp(startRot, destination.rotation, elapsed / duration);
                elapsed += Time.deltaTime;
                await UniTask.Yield();
            }
            targetCamera.transform.SetPositionAndRotation(destination.position, destination.rotation);
        }
    }
}