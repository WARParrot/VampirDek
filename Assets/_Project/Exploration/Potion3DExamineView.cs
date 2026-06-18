using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Exploration
{
    /// <summary>
    /// Procedural 3D examine viewport for the antidote potion. Builds, on first call:
    ///   - a hidden stage GameObject (potion mesh + lights) far from the gameplay world,
    ///   - a dedicated Camera rendering that stage into a RenderTexture,
    ///   - a RawImage overlay parented to the inventory examine modal that draws the RT,
    ///   - drag-to-rotate handler on the RawImage so the player can spin the bottle.
    /// Compared to <see cref="Exploration.Inventory.ExamineView3D"/> this is self-contained:
    /// no scene wiring, no ExaminePrefab asset — just call Show / Hide.
    /// </summary>
    public static class Potion3DExamineView
    {
        private static RawImage _rawImage;
        private static Camera _camera;
        private static GameObject _stage;
        private static RenderTexture _rt;
        private static Text _captionLabel;

        public static void Show(Transform examineRoot, string caption)
        {
            EnsureBuilt(examineRoot);
            if (_rawImage != null) _rawImage.gameObject.SetActive(true);
            if (_stage != null) { _stage.SetActive(true); _stage.transform.localRotation = Quaternion.identity; }
            if (_camera != null) _camera.enabled = true;
            if (_captionLabel != null) _captionLabel.text = caption;
        }

        public static void Hide()
        {
            if (_rawImage != null) _rawImage.gameObject.SetActive(false);
            if (_stage != null) _stage.SetActive(false);
            if (_camera != null) _camera.enabled = false;
        }

        private static void EnsureBuilt(Transform examineRoot)
        {
            if (_rawImage != null && _stage != null && _camera != null) return;

            // RenderTexture.
            if (_rt == null)
            {
                _rt = new RenderTexture(640, 640, 16, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                _rt.Create();
            }

            // RawImage overlay — lives inside the ExamineFrame card. Anchored at top-left,
            // big enough to feel like a real "in-hand" rotation, but tucked so it doesn't
            // cover the close button or the text column on the right.
            var rawGo = new GameObject("PotionExamine3D", typeof(RectTransform), typeof(RawImage), typeof(PotionDragHandler));
            rawGo.transform.SetParent(examineRoot, false);
            rawGo.transform.SetAsFirstSibling(); // render under text/button, but still receives drag because they're transparent above
            var rt = (RectTransform)rawGo.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -24f);
            rt.sizeDelta = new Vector2(380f, 540f);
            _rawImage = rawGo.GetComponent<RawImage>();
            _rawImage.texture = _rt;
            _rawImage.raycastTarget = true;

            // Stage far away from the rest of the scene so nothing else gets rendered.
            _stage = new GameObject("~PotionExamineStage");
            Object.DontDestroyOnLoad(_stage);
            _stage.transform.position = new Vector3(-1000f, -1000f, 0f);

            // Camera.
            var camGo = new GameObject("~PotionExamineCam", typeof(Camera));
            camGo.transform.SetParent(_stage.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0.05f, 0.45f);
            camGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            _camera = camGo.GetComponent<Camera>();
            _camera.targetTexture = _rt;
            _camera.backgroundColor = new Color(0.04f, 0.03f, 0.06f, 1f);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.fieldOfView = 35f;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 4f;
            _camera.cullingMask = ~0;

            // Lighting.
            var keyGo = new GameObject("KeyLight", typeof(Light));
            keyGo.transform.SetParent(_stage.transform, false);
            keyGo.transform.localPosition = new Vector3(0.4f, 0.5f, 0.5f);
            keyGo.transform.localRotation = Quaternion.Euler(35f, 200f, 0f);
            var kl = keyGo.GetComponent<Light>();
            kl.type = LightType.Directional;
            kl.intensity = 1.1f;
            kl.color = new Color(1f, 0.92f, 0.82f);

            var fillGo = new GameObject("FillLight", typeof(Light));
            fillGo.transform.SetParent(_stage.transform, false);
            fillGo.transform.localPosition = new Vector3(-0.4f, 0.2f, 0.4f);
            fillGo.transform.localRotation = Quaternion.Euler(20f, 160f, 0f);
            var fl = fillGo.GetComponent<Light>();
            fl.type = LightType.Directional;
            fl.intensity = 0.4f;
            fl.color = new Color(0.6f, 0.4f, 0.9f);

            // Potion mesh — re-use the same procedural look as the in-world reveal.
            var bottle = BuildPotion();
            bottle.transform.SetParent(_stage.transform, false);
            bottle.transform.localPosition = Vector3.zero;
            bottle.transform.localRotation = Quaternion.identity;

            // Wire drag handler.
            rawGo.GetComponent<PotionDragHandler>().Target = bottle.transform;
        }

        private static GameObject BuildPotion()
        {
            var root = new GameObject("Potion");

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            body.transform.localScale = new Vector3(0.12f, 0.14f, 0.12f);
            Object.Destroy(body.GetComponent<Collider>());
            var bRen = body.GetComponent<Renderer>();
            if (bRen != null) bRen.material.color = new Color(0.35f, 0.10f, 0.55f, 1f);

            var neck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            neck.name = "Neck";
            neck.transform.SetParent(root.transform, false);
            neck.transform.localPosition = new Vector3(0f, 0.20f, 0f);
            neck.transform.localScale = new Vector3(0.05f, 0.035f, 0.05f);
            Object.Destroy(neck.GetComponent<Collider>());
            var nRen = neck.GetComponent<Renderer>();
            if (nRen != null) nRen.material.color = new Color(0.20f, 0.15f, 0.10f, 1f);

            var cork = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cork.name = "Cork";
            cork.transform.SetParent(root.transform, false);
            cork.transform.localPosition = new Vector3(0f, 0.255f, 0f);
            cork.transform.localScale = new Vector3(0.065f, 0.03f, 0.065f);
            Object.Destroy(cork.GetComponent<Collider>());
            var cRen = cork.GetComponent<Renderer>();
            if (cRen != null) cRen.material.color = new Color(0.5f, 0.32f, 0.18f, 1f);

            return root;
        }

        private class PotionDragHandler : MonoBehaviour, IDragHandler, IPointerDownHandler
        {
            public Transform Target;
            private const float Sens = 0.5f;
            public void OnPointerDown(PointerEventData e) { }
            public void OnDrag(PointerEventData e)
            {
                if (Target == null) return;
                Target.Rotate(Vector3.up, -e.delta.x * Sens, Space.World);
                Target.Rotate(Vector3.right, e.delta.y * Sens, Space.World);
            }
        }
    }
}
