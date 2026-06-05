#if DOTWEEN
using DG.Tweening;
#endif
using System;
using System.Collections;
using Core;
using Definitions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.UI
{
    public class CombatVFX : MonoBehaviour
    {
        private static CombatVFX _instance;
        private Canvas _vfxCanvas;
        private BoardView _boardViewCache;

        private IDisposable _subDamage;
        private IDisposable _subDied;
        private IDisposable _subPlaced;
        private IDisposable _subClash;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            try
            {
                var go = new GameObject("[CombatVFX]");
                _instance = go.AddComponent<CombatVFX>();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CombatVFX] Bootstrap failed: {ex}");
            }
        }

        private void Awake()
        {
            StartCoroutine(DeferredInit());
        }

        private IEnumerator DeferredInit()
        {
            // Wait one frame so the GameObject is fully part of an active scene before we touch UI APIs.
            yield return null;

            try { DontDestroyOnLoad(gameObject); } catch (Exception ex) { Debug.LogWarning($"[CombatVFX] DontDestroyOnLoad failed: {ex.Message}"); }

            int waited = 0;
            while (GlobalServices.EventBus == null && waited < 600)
            {
                waited++;
                yield return null;
            }

            if (GlobalServices.EventBus == null)
            {
                Debug.LogWarning("[CombatVFX] EventBus never came up — VFX disabled.");
                yield break;
            }

            try
            {
                _subDamage = GlobalServices.EventBus.Subscribe<DamageDealtEvent>(OnDamage);
                _subDied   = GlobalServices.EventBus.Subscribe<EntityDiedEvent>(OnDied);
                _subPlaced = GlobalServices.EventBus.Subscribe<PlacedCardEvent>(OnPlaced);
                _subClash  = GlobalServices.EventBus.Subscribe<ClashResolvedEvent>(OnClash);
                Debug.Log("[CombatVFX] Subscribed to combat events.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CombatVFX] Subscribe failed: {ex}");
            }
        }

        private void OnDestroy()
        {
            _subDamage?.Dispose();
            _subDied?.Dispose();
            _subPlaced?.Dispose();
            _subClash?.Dispose();
            if (_instance == this) _instance = null;
        }

        private bool EnsureCanvas()
        {
            if (_vfxCanvas != null) return true;
            try
            {
                var go = new GameObject("VFX_Canvas");
                go.transform.SetParent(transform, false);
                _vfxCanvas = go.AddComponent<Canvas>();
                _vfxCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _vfxCanvas.sortingOrder = 9999;
                go.AddComponent<CanvasScaler>();
                var raycaster = go.AddComponent<GraphicRaycaster>();
                raycaster.enabled = false;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CombatVFX] EnsureCanvas failed: {ex.Message}");
                return false;
            }
        }

        private BoardSlotUI FindSlotFor(IGameEntity entity)
        {
            if (entity == null) return null;
            if (_boardViewCache == null) _boardViewCache = FindObjectOfType<BoardView>();
            if (_boardViewCache == null) return null;
            foreach (var ui in _boardViewCache.GetSlotUIs())
                if (ui != null && ui.Occupant == entity) return ui;
            return null;
        }

        private void OnDamage(DamageDealtEvent e)
        {
            try
            {
                var slot = FindSlotFor(e.Target);
                if (slot != null) SpawnDamageNumber(slot.transform.position, e.Amount);
                ShakeCamera(e.Amount);
            }
            catch (Exception ex) { Debug.LogWarning($"[CombatVFX] OnDamage error: {ex.Message}"); }
        }

        private void OnDied(EntityDiedEvent e)
        {
            try
            {
                var slot = FindSlotFor(e.Entity);
                if (slot != null) PlayDeath(slot.transform);
            }
            catch (Exception ex) { Debug.LogWarning($"[CombatVFX] OnDied error: {ex.Message}"); }
        }

        private void OnPlaced(PlacedCardEvent e)
        {
            try
            {
                var slot = FindSlotFor(e.Card);
                if (slot != null) PlayPlacement(slot.transform);
            }
            catch (Exception ex) { Debug.LogWarning($"[CombatVFX] OnPlaced error: {ex.Message}"); }
        }

        private void OnClash(ClashResolvedEvent e)
        {
            try
            {
                var winnerSlot = FindSlotFor(e.Winner);
                if (winnerSlot != null) PlayAttack(winnerSlot.transform);
            }
            catch (Exception ex) { Debug.LogWarning($"[CombatVFX] OnClash error: {ex.Message}"); }
        }

        private void SpawnDamageNumber(Vector3 screenPos, int amount)
        {
#if DOTWEEN
            if (!EnsureCanvas()) return;
            var go = new GameObject("DamageNumber");
            go.transform.SetParent(_vfxCanvas.transform, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = $"-{amount}";
            tmp.color = new Color(1f, 0.25f, 0.25f);
            tmp.fontSize = 42;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            var rt = tmp.rectTransform;
            rt.sizeDelta = new Vector2(160, 60);
            rt.position = screenPos;

            Vector3 startPos = rt.position;
            Vector3 endPos = startPos + new Vector3(0f, 80f, 0f);
            DOTween.To(() => rt.position, p => rt.position = p, endPos, 0.8f).SetEase(Ease.OutCubic);
            DOTween.To(() => tmp.color.a, a => { var c = tmp.color; c.a = a; tmp.color = c; }, 0f, 0.8f)
                .OnComplete(() => { if (go != null) Destroy(go); });
#endif
        }

        private void ShakeCamera(int amount)
        {
#if DOTWEEN
            var cam = Camera.main;
            if (cam == null) return;
            float mag = Mathf.Clamp(amount * 0.08f, 0.1f, 0.5f);
            cam.DOComplete();
            cam.transform.DOShakePosition(0.25f, new Vector3(mag, mag, 0f), 14, 90f, false, true);
#endif
        }

        private void PlayPlacement(Transform t)
        {
#if DOTWEEN
            t.DOComplete();
            var baseScale = t.localScale;
            t.localScale = baseScale * 0.7f;
            t.DOScale(baseScale, 0.3f).SetEase(Ease.OutBack);
#endif
        }

        private void PlayDeath(Transform t)
        {
#if DOTWEEN
            t.DOComplete();
            var seq = DOTween.Sequence();
            seq.Append(t.DOShakePosition(0.25f, 8f, 14, 90f, false, true));
            seq.Append(t.DOScale(t.localScale * 0.85f, 0.2f).SetEase(Ease.InQuad));
            seq.Append(t.DOScale(t.localScale, 0.2f).SetEase(Ease.OutQuad));
#endif
        }

        private void PlayAttack(Transform t)
        {
#if DOTWEEN
            t.DOComplete();
            var baseScale = t.localScale;
            var seq = DOTween.Sequence();
            seq.Append(t.DOScale(baseScale * 1.15f, 0.1f).SetEase(Ease.OutQuad));
            seq.Append(t.DOScale(baseScale, 0.15f).SetEase(Ease.InQuad));
#endif
        }
    }
}
