using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Exploration.Inventory
{
    /// <summary>
    /// RE2-Remake style 3D examine view.
    ///
    /// Setup: in the inventory canvas, add a RawImage that will display the model.
    /// Drop this component on the same UI object. Assign:
    ///   - _rawImage : the RawImage to draw into
    ///   - _stageRoot: a Transform somewhere far from gameplay (e.g. (-1000, -1000, 0))
    ///                 where prefabs will be instanced
    ///   - _stageCamera: a Camera positioned at _stageRoot, with TargetTexture left empty
    ///                   (this script creates a RenderTexture and assigns it).
    ///
    /// On <see cref="Show(ItemDef)"/>: instantiates ExaminePrefab under _stageRoot, points the
    /// camera, and starts listening for mouse drag. <see cref="Hide"/> tears it down.
    /// </summary>
    public class ExamineView3D : MonoBehaviour, IDragHandler, IPointerDownHandler
    {
        [SerializeField] private RawImage _rawImage;
        [SerializeField] private Transform _stageRoot;
        [SerializeField] private Camera _stageCamera;
        [SerializeField] private Text _captionLabel;
        [SerializeField] private Vector2 _renderTextureSize = new Vector2(512, 512);
        [SerializeField] private float _dragSensitivity = 0.4f;

        private RenderTexture _rt;
        private GameObject _spawned;

        private void Awake()
        {
            EnsureRenderTexture();
            gameObject.SetActive(false);
        }

        private void EnsureRenderTexture()
        {
            if (_rt != null) return;
            _rt = new RenderTexture((int)_renderTextureSize.x, (int)_renderTextureSize.y, 16, RenderTextureFormat.ARGB32)
            {
                name = "ExamineRT",
                antiAliasing = 4
            };
            _rt.Create();
            if (_rawImage != null) _rawImage.texture = _rt;
            if (_stageCamera != null) _stageCamera.targetTexture = _rt;
        }

        public void Show(ItemDef item, string caption)
        {
            if (item == null || item.ExaminePrefab == null)
            {
                Hide();
                return;
            }
            EnsureRenderTexture();
            ClearStage();

            _spawned = Instantiate(item.ExaminePrefab, _stageRoot);
            _spawned.transform.localPosition = Vector3.zero;
            _spawned.transform.localRotation = Quaternion.Euler(item.ExamineStartEuler);

            if (_stageCamera != null && _stageRoot != null)
            {
                _stageCamera.transform.position = _stageRoot.position + (-_stageCamera.transform.forward * item.ExamineCameraDistance);
                _stageCamera.transform.LookAt(_stageRoot);
            }

            if (_captionLabel != null) _captionLabel.text = caption;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            ClearStage();
            gameObject.SetActive(false);
        }

        private void ClearStage()
        {
            if (_spawned != null) { Destroy(_spawned); _spawned = null; }
        }

        public void OnPointerDown(PointerEventData eventData) { /* used so OnDrag fires reliably */ }

        public void OnDrag(PointerEventData eventData)
        {
            if (_spawned == null) return;
            // Rotate around world-up for yaw and camera-right for pitch so the user feels they're
            // turning the object in hand, regardless of how the stage camera is oriented.
            float yaw = -eventData.delta.x * _dragSensitivity;
            float pitch = eventData.delta.y * _dragSensitivity;
            _spawned.transform.Rotate(Vector3.up, yaw, Space.World);
            _spawned.transform.Rotate(_stageCamera != null ? _stageCamera.transform.right : Vector3.right, pitch, Space.World);
        }

        private void OnDestroy()
        {
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
            }
        }
    }
}
