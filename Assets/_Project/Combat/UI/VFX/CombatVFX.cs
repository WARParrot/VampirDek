#if DOTWEEN
using DG.Tweening;
#endif
using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
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
        private readonly Dictionary<IGameEntity, BoardSlotUI> _slotByEntity = new();
        private static readonly Vector3[] s_rectCorners = new Vector3[4];

        private IDisposable _subDamage;
        private IDisposable _subDied;
        private IDisposable _subPlaced;
        private IDisposable _subClash;

        private float _actionAnimationGateUntil;
        private const float MinActionGateSeconds = 0.65f;
        private const float DirectedAttackGateSeconds = 1.0f;
        private const float ClashGateSeconds = 1.28f;
        private const float PlacementGateSeconds = 0.32f;
        private const float DeathGateSeconds = 0.55f;

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

            if (_slotByEntity.TryGetValue(entity, out var cachedSlot) && cachedSlot != null && cachedSlot.Occupant == entity)
                return cachedSlot;

            RebuildSlotLookup();
            if (_slotByEntity.TryGetValue(entity, out cachedSlot) && cachedSlot != null && cachedSlot.Occupant == entity)
                return cachedSlot;

            return null;
        }

        private void RebuildSlotLookup()
        {
            _slotByEntity.Clear();
            if (_boardViewCache == null) return;

            foreach (var ui in _boardViewCache.GetSlotUIs())
            {
                var occupant = ui != null ? ui.Occupant : null;
                if (occupant != null)
                    _slotByEntity[occupant] = ui;
            }
        }

        // Converts the centre of a UI element into the screen-space coordinates that the VFX
        // canvas (Screen Space Overlay) uses. Source canvas might be World Space or Screen Space
        // Camera, so transform.position alone is unreliable.
        private static Vector3 GetScreenCenter(Transform t)
        {
            if (t == null) return Vector3.zero;
            var rect = t as RectTransform;
            if (rect == null) return t.position;
            var canvas = t.GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            Vector3 worldCenter = rect.TransformPoint(rect.rect.center);
            return cam != null ? (Vector3)RectTransformUtility.WorldToScreenPoint(cam, worldCenter) : worldCenter;
        }

        private static Vector3 GetScreenBottom(Transform t)
        {
            if (t == null) return Vector3.zero;
            var rect = t as RectTransform;
            if (rect == null) return t.position;
            var canvas = t.GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            rect.GetWorldCorners(s_rectCorners);
            Vector3 worldBottom = (s_rectCorners[0] + s_rectCorners[3]) * 0.5f;
            return cam != null ? (Vector3)RectTransformUtility.WorldToScreenPoint(cam, worldBottom) : worldBottom;
        }

        private void OnDamage(DamageDealtEvent e)
        {
            try
            {
                var sourceSlot = FindSlotFor(e.Source);
                var targetSlot = FindSlotFor(e.Target);

                // BloodWitch fires her own VFX via PlaySpellShot (red beam) — skip the default
                // source-lunge animation and pulse so she doesn't flash red herself, only the
                // target impact + damage number play through here.
                bool isWitchSpell = (e.Source as BoardCard)?.SourceCard?.CardName == "BloodWitch";

                if (sourceSlot != null && targetSlot != null && sourceSlot != targetSlot && !isWitchSpell)
                {
                    PlayDirectedAttack(sourceSlot, targetSlot, e.Amount);
                    GateActionAnimation(DirectedAttackGateSeconds);
                }
                else if (targetSlot != null)
                {
                    PlayHit(targetSlot, null, e.Amount);
                    GateActionAnimation(MinActionGateSeconds);
                }

                if (targetSlot != null) SpawnDamageNumber(targetSlot.transform.position, e.Amount);
            }
            catch (Exception ex) { Debug.LogWarning($"[CombatVFX] OnDamage error: {ex.Message}"); }
        }


        private void OnDied(EntityDiedEvent e)
        {
            try
            {
                var slot = FindSlotFor(e.Entity);
                if (slot != null)
                {
                    PlayDeath(slot.transform);
                    GateActionAnimation(DeathGateSeconds);
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[CombatVFX] OnDied error: {ex.Message}"); }
        }


        private void OnPlaced(PlacedCardEvent e)
        {
            try
            {
                var slot = FindSlotFor(e.Card);
                if (slot != null)
                {
                    PlayPlacement(slot);
                    GateActionAnimation(PlacementGateSeconds);
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[CombatVFX] OnPlaced error: {ex.Message}"); }
        }


        private void OnClash(ClashResolvedEvent e)
        {
            try
            {
                var winnerSlot = FindSlotFor(e.Winner);
                var loserSlot = FindSlotFor(e.Loser);
                if (winnerSlot != null && loserSlot != null)
                {
                    PlayClash(winnerSlot, loserSlot);
                    GateActionAnimation(ClashGateSeconds);
                }
                else if (winnerSlot != null)
                {
                    PlayAttackPulse(winnerSlot.transform, ResolveVfxStyle(e.Winner));
                    GateActionAnimation(MinActionGateSeconds);
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[CombatVFX] OnClash error: {ex.Message}"); }
        }



        public static void PlaySpellShot(IGameEntity source, IGameEntity target, Color color)
        {
#if DOTWEEN
            var instance = _instance;
            if (instance == null) return;
            try
            {
                var sourceSlot = instance.FindSlotFor(source);
                var targetSlot = instance.FindSlotFor(target);
                if (sourceSlot == null || targetSlot == null) return;

                var from = GetScreenCenter(sourceSlot.transform);
                var to = GetScreenCenter(targetSlot.transform);
                instance.SpawnLaserBeam(from, to, color);

                var style = new VfxStyle(color, true);
                instance.PlayHit(targetSlot, style, 0);
                instance.GateActionAnimation(MinActionGateSeconds);
            }
            catch (Exception ex) { Debug.LogWarning($"[CombatVFX] PlaySpellShot error: {ex.Message}"); }
#endif
        }

#if DOTWEEN
        private void SpawnLaserBeam(Vector3 fromScreen, Vector3 toScreen, Color color)
        {
            if (!EnsureCanvas()) return;

            var delta = toScreen - fromScreen;
            float length = delta.magnitude;
            if (length < 8f) return;
            var direction = delta / length;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            // Inset the endpoints a touch so the beam tucks under the card edges, like the planning arrows do.
            float sourceInset = Mathf.Min(28f, length * 0.12f);
            float targetInset = Mathf.Min(34f, length * 0.18f);
            var shaftFrom = fromScreen + (Vector3)(direction * sourceInset);
            var shaftTo = toScreen - (Vector3)(direction * targetInset);
            var shaftDelta = shaftTo - shaftFrom;
            float shaftLength = shaftDelta.magnitude;
            if (shaftLength < 4f) return;
            var mid = (shaftFrom + shaftTo) * 0.5f;
            var rotation = Quaternion.Euler(0f, 0f, angle);

            // Single clean shaft, same vibe as TargetPlanArrowsUI's planning arrows.
            var shaftGo = new GameObject("LaserShaft");
            shaftGo.transform.SetParent(_vfxCanvas.transform, false);
            var shaft = shaftGo.AddComponent<Image>();
            shaft.raycastTarget = false;
            shaft.color = new Color(color.r, color.g, color.b, 0.92f);
            var shaftRt = shaft.rectTransform;
            shaftRt.position = mid;
            shaftRt.sizeDelta = new Vector2(shaftLength, 5f);
            shaftRt.rotation = rotation;
            shaftRt.localScale = new Vector3(0f, 1f, 1f);

            // Small spark arrowhead at the target end (TMP unicode char, mirrors the planning arrows).
            var headGo = new GameObject("LaserHead");
            headGo.transform.SetParent(_vfxCanvas.transform, false);
            var head = headGo.AddComponent<TextMeshProUGUI>();
            head.text = "➤";
            head.fontSize = 26f;
            head.fontStyle = FontStyles.Bold;
            head.alignment = TextAlignmentOptions.Center;
            head.raycastTarget = false;
            head.color = new Color(color.r, color.g, color.b, 0.95f);
            var headRt = head.rectTransform;
            headRt.sizeDelta = new Vector2(44f, 44f);
            headRt.position = toScreen - (Vector3)(direction * Mathf.Min(18f, length * 0.08f));
            headRt.rotation = rotation;
            headRt.localScale = Vector3.zero;

            var seq = DOTween.Sequence();
            // Quick clean shoot — shaft snaps to length, head appears at the tip.
            seq.Append(shaftRt.DOScaleX(1f, 0.12f).SetEase(Ease.OutCubic));
            seq.Join(headRt.DOScale(Vector3.one, 0.14f).SetEase(Ease.OutBack));
            // Hold briefly, then graceful fade — no twitchy thinning.
            seq.AppendInterval(0.16f);
            seq.Append(DOTween.To(() => shaft.color.a, a => { var c = shaft.color; c.a = a; shaft.color = c; }, 0f, 0.45f).SetEase(Ease.InSine));
            seq.Join(DOTween.To(() => head.color.a, a => { var c = head.color; c.a = a; head.color = c; }, 0f, 0.45f).SetEase(Ease.InSine));
            seq.OnComplete(() =>
            {
                if (shaftGo != null) Destroy(shaftGo);
                if (headGo != null) Destroy(headGo);
            });
        }
#endif

        public static async UniTask AwaitCurrentActionAnimationsAsync(float fallbackSeconds = MinActionGateSeconds)
        {
            var instance = _instance;
            if (instance == null)
            {
                if (fallbackSeconds > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(fallbackSeconds));
                return;
            }

            await instance.WaitForActionAnimationsAsync(fallbackSeconds);
        }

        private async UniTask WaitForActionAnimationsAsync(float fallbackSeconds)
        {
            float targetUntil = Mathf.Max(_actionAnimationGateUntil, Time.time + Mathf.Max(0f, fallbackSeconds));
            while (Time.time < targetUntil)
                await UniTask.Yield();
        }

        private void GateActionAnimation(float seconds)
        {
            _actionAnimationGateUntil = Mathf.Max(_actionAnimationGateUntil, Time.time + Mathf.Max(0f, seconds));
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
            rt.position = screenPos + new Vector3(0f, 24f, 0f);

            Vector3 startPos = rt.position;
            Vector3 endPos = startPos + new Vector3(0f, 96f, 0f);
            DOTween.To(() => rt.position, p => rt.position = p, endPos, 1.05f).SetEase(Ease.OutCubic);
            DOTween.To(() => tmp.color.a, a => { var c = tmp.color; c.a = a; tmp.color = c; }, 0f, 1.05f)
                .OnComplete(() => { if (go != null) Destroy(go); });
#endif
        }
private void PlayPlacement(BoardSlotUI slotUI)
        {
#if DOTWEEN
            if (slotUI == null) return;
            var t = slotUI.transform;
            t.DOComplete();
            var baseScale = t.localScale;
            t.localScale = baseScale * 0.7f;
            t.DOScale(baseScale, 0.42f).SetEase(Ease.OutBack);

            // Always emit from the slot's actual bottom-centre (converted to overlay screen
            // coords), not from a card transform — placement dust must hug the slot rectangle.
            SpawnDustBurst(GetScreenBottom(t));
#endif
        }

#if DOTWEEN
        private void SpawnDustBurst(Vector3 origin)
        {
            if (!EnsureCanvas()) return;

            const int puffCount = 26;
            var dustColor = new Color(0.92f, 0.9f, 0.86f, 0.22f);
            var baseOrigin = origin; // already at the bottom-center of the slot

            for (int i = 0; i < puffCount; i++)
            {
                var go = new GameObject("DustPuff");
                go.transform.SetParent(_vfxCanvas.transform, false);
                var image = go.AddComponent<Image>();
                image.raycastTarget = false;
                image.color = dustColor;

                var rt = image.rectTransform;
                rt.sizeDelta = new Vector2(UnityEngine.Random.Range(2.5f, 5f), UnityEngine.Random.Range(2.5f, 5f));
                rt.position = baseOrigin + new Vector3(UnityEngine.Random.Range(-22f, 22f), UnityEngine.Random.Range(-2f, 3f), 0f);
                rt.localScale = Vector3.one * UnityEngine.Random.Range(0.5f, 0.85f);

                // Slow lazy drift outward with a gentle upward arc — settling dust, not a burst.
                float side = (i % 2 == 0) ? 1f : -1f;
                float horizontal = side * UnityEngine.Random.Range(10f, 26f);
                float vertical = UnityEngine.Random.Range(4f, 12f);
                var drift = new Vector3(horizontal, vertical, 0f);

                float duration = UnityEngine.Random.Range(0.85f, 1.25f);
                var seq = DOTween.Sequence();
                seq.Append(rt.DOMove(rt.position + drift, duration).SetEase(Ease.OutSine));
                seq.Join(rt.DOScale(Vector3.one * UnityEngine.Random.Range(0.3f, 0.55f), duration).SetEase(Ease.OutSine));
                seq.Join(DOTween.To(() => image.color.a, a => { var c = image.color; c.a = a; image.color = c; }, 0f, duration).SetEase(Ease.InSine));
                seq.OnComplete(() => { if (go != null) Destroy(go); });
            }
        }
#endif

        private void PlayDeath(Transform t)
        {
#if DOTWEEN
            t.DOComplete();
            var seq = DOTween.Sequence();
            var baseScale = t.localScale;
            seq.Append(t.DOScale(new Vector3(baseScale.x * 0.94f, baseScale.y * 1.06f, baseScale.z), 0.12f).SetEase(Ease.OutQuad));
            seq.Join(PulseGraphics(t, new Color(0.95f, 0.1f, 0.08f), 0.22f));
            seq.Append(t.DOScale(baseScale * 0.85f, 0.14f).SetEase(Ease.InQuad));
            seq.Append(t.DOScale(baseScale, 0.16f).SetEase(Ease.OutBack));
#endif
        }

        private void PlayDirectedAttack(BoardSlotUI sourceSlot, BoardSlotUI targetSlot, int amount)
        {
#if DOTWEEN
            if (sourceSlot == null || targetSlot == null) return;

            var style = ResolveVfxStyle(sourceSlot.Occupant);
            var source = sourceSlot.transform;
            var target = targetSlot.transform;
            source.DOComplete();

            var start = source.position;
            var destination = Vector3.Lerp(start, target.position, 0.38f);
            var baseScale = source.localScale;
            var duration = style.IsSpecial ? 0.34f : 0.28f;

            var seq = DOTween.Sequence();
            seq.Append(source.DOMove(destination, duration).SetEase(Ease.OutQuad));
            seq.Join(source.DOScale(baseScale * (style.IsSpecial ? 1.12f : 1.06f), duration).SetEase(Ease.OutQuad));
            seq.Join(PulseGraphics(source, style.Accent, duration + 0.05f));
            seq.Join(SpawnAttackTrail(source.position, target.position, style));
            seq.AppendCallback(() => PlayHit(targetSlot, style, amount));
            seq.Append(source.DOMove(start, 0.24f).SetEase(Ease.OutCubic));
            seq.Join(source.DOScale(baseScale, 0.24f).SetEase(Ease.OutCubic));
#endif
        }

        private void PlayHit(BoardSlotUI targetSlot, VfxStyle? style, int amount)
        {
#if DOTWEEN
            if (targetSlot == null) return;
            var accent = style?.Accent ?? new Color(1f, 0.2f, 0.15f);
            var targetTransform = targetSlot.transform;
            targetTransform.DOComplete();
            var baseScale = targetTransform.localScale;
            DOTween.Sequence()
                .Append(targetTransform.DOScale(new Vector3(baseScale.x * 0.9f, baseScale.y * 1.08f, baseScale.z), 0.1f).SetEase(Ease.OutQuad))
                .Append(targetTransform.DOScale(new Vector3(baseScale.x * 1.04f, baseScale.y * 0.96f, baseScale.z), 0.1f).SetEase(Ease.InOutQuad))
                .Append(targetTransform.DOScale(baseScale, 0.14f).SetEase(Ease.OutBack));
            PulseGraphics(targetTransform, accent, 0.34f);
            SpawnImpact(targetTransform.position, accent, style?.IsSpecial ?? false);
#endif
        }

        private void PlayClash(BoardSlotUI winnerSlot, BoardSlotUI loserSlot)
        {
#if DOTWEEN
            var winnerStyle = ResolveVfxStyle(winnerSlot.Occupant);
            var loserStyle = ResolveVfxStyle(loserSlot.Occupant);
            var winner = winnerSlot.transform;
            var loser = loserSlot.transform;
            winner.DOComplete();
            loser.DOComplete();

            var winnerStart = winner.position;
            var loserStart = loser.position;
            var mid = (winnerStart + loserStart) * 0.5f;
            var winnerMeet = Vector3.Lerp(winnerStart, mid, 0.42f);
            var loserMeet = Vector3.Lerp(loserStart, mid, 0.42f);
            var winnerScale = winner.localScale;
            var loserScale = loser.localScale;

            var seq = DOTween.Sequence();
            seq.Append(winner.DOMove(winnerMeet, 0.38f).SetEase(Ease.OutQuad));
            seq.Join(loser.DOMove(loserMeet, 0.38f).SetEase(Ease.OutQuad));
            seq.Join(winner.DOScale(winnerScale * 1.08f, 0.38f));
            seq.Join(loser.DOScale(loserScale * 1.08f, 0.38f));
            seq.Join(PulseGraphics(winner, winnerStyle.Accent, 0.35f));
            seq.Join(PulseGraphics(loser, loserStyle.Accent, 0.35f));
            seq.AppendCallback(() =>
            {
                SpawnClashBadge(mid, Blend(winnerStyle.Accent, loserStyle.Accent));
                SpawnImpact(mid, Blend(winnerStyle.Accent, loserStyle.Accent), winnerStyle.IsSpecial || loserStyle.IsSpecial);
            });
            seq.AppendInterval(0.32f);
            seq.Append(winner.DOMove(winnerStart, 0.34f).SetEase(Ease.OutCubic));
            seq.Join(loser.DOMove(loserStart, 0.34f).SetEase(Ease.OutCubic));
            seq.Join(winner.DOScale(winnerScale, 0.34f));
            seq.Join(loser.DOScale(loserScale, 0.34f));
            seq.AppendCallback(() => PlayAttackPulse(winner, winnerStyle));
#endif
        }

        private void PlayAttackPulse(Transform t, VfxStyle style)
        {
#if DOTWEEN
            if (t == null) return;
            t.DOComplete();
            var baseScale = t.localScale;
            var seq = DOTween.Sequence();
            seq.Append(t.DOScale(baseScale * 1.15f, 0.18f).SetEase(Ease.OutQuad));
            seq.Join(PulseGraphics(t, style.Accent, 0.28f));
            seq.Append(t.DOScale(baseScale, 0.24f).SetEase(Ease.InQuad));
#endif
        }

#if DOTWEEN
        // Per-graphic baseline so overlapping PulseGraphics calls (e.g. 3 BloodWitch shots on the
        // same target) always restore to the true original colour, not the in-flight flashed one.
        private readonly System.Collections.Generic.Dictionary<Graphic, Color> _graphicBaseline = new();

        private Tween PulseGraphics(Transform root, Color color, float duration)
        {
            var seq = DOTween.Sequence();
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == null) continue;
                if (!_graphicBaseline.TryGetValue(graphic, out var start))
                {
                    start = graphic.color;
                    _graphicBaseline[graphic] = start;
                }
                // Kill any in-flight color tween on this graphic before starting a new one.
                DOTween.Kill(graphic, complete: false);
                graphic.color = start;

                var flash = new Color(
                    Mathf.Clamp01(start.r * 0.45f + color.r * 0.85f),
                    Mathf.Clamp01(start.g * 0.45f + color.g * 0.85f),
                    Mathf.Clamp01(start.b * 0.45f + color.b * 0.85f),
                    start.a);

                var localGraphic = graphic;
                var localStart = start;
                seq.Join(DOTween.To(() => localGraphic.color, c => localGraphic.color = c, flash, duration * 0.45f)
                    .SetTarget(localGraphic)
                    .SetEase(Ease.OutQuad)
                    .SetLoops(2, LoopType.Yoyo)
                    .OnComplete(() => { if (localGraphic != null) localGraphic.color = localStart; })
                    .OnKill(() => { if (localGraphic != null) localGraphic.color = localStart; }));
            }
            return seq;
        }

        private Tween SpawnAttackTrail(Vector3 from, Vector3 to, VfxStyle style)
        {
            if (!EnsureCanvas()) return DOTween.Sequence();

            var go = new GameObject(style.IsSpecial ? "SpecialAttackTrail" : "AttackTrail");
            go.transform.SetParent(_vfxCanvas.transform, false);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = new Color(style.Accent.r, style.Accent.g, style.Accent.b, style.IsSpecial ? 0.75f : 0.5f);

            var rt = image.rectTransform;
            var delta = to - from;
            rt.position = (from + to) * 0.5f;
            rt.sizeDelta = new Vector2(Mathf.Max(40f, delta.magnitude), style.IsSpecial ? 10f : 6f);
            rt.rotation = Quaternion.FromToRotation(Vector3.right, delta.normalized);
            rt.localScale = new Vector3(0.05f, 1f, 1f);

            var seq = DOTween.Sequence();
            seq.Append(rt.DOScaleX(1f, style.IsSpecial ? 0.28f : 0.24f).SetEase(Ease.OutQuad));
            seq.AppendInterval(0.14f);
            seq.Append(DOTween.To(() => image.color.a, a => { var c = image.color; c.a = a; image.color = c; }, 0f, 0.36f));
            seq.OnComplete(() => { if (go != null) Destroy(go); });
            return seq;
        }

        private void SpawnImpact(Vector3 position, Color color, bool special)
        {
            if (!EnsureCanvas()) return;

            var go = new GameObject(special ? "SpecialImpact" : "Impact");
            go.transform.SetParent(_vfxCanvas.transform, false);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = new Color(color.r, color.g, color.b, special ? 0.75f : 0.55f);

            var rt = image.rectTransform;
            rt.position = position;
            rt.sizeDelta = Vector2.one * (special ? 42f : 30f);
            rt.localScale = Vector3.zero;

            var seq = DOTween.Sequence();
            seq.Append(rt.DOScale(Vector3.one * (special ? 2.2f : 1.5f), 0.36f).SetEase(Ease.OutCubic));
            seq.Join(DOTween.To(() => image.color.a, a => { var c = image.color; c.a = a; image.color = c; }, 0f, 0.36f));
            seq.OnComplete(() => { if (go != null) Destroy(go); });
        }

        private void SpawnClashBadge(Vector3 position, Color color)
        {
            if (!EnsureCanvas()) return;

            var go = new GameObject("ClashBadge");
            go.transform.SetParent(_vfxCanvas.transform, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = "CLASH";
            tmp.color = new Color(color.r, color.g, color.b, 0.95f);
            tmp.fontSize = 34;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            var rt = tmp.rectTransform;
            rt.sizeDelta = new Vector2(180f, 54f);
            rt.position = position + new Vector3(0f, 42f, 0f);
            rt.localScale = Vector3.one * 0.65f;

            var seq = DOTween.Sequence();
            seq.Append(rt.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack));
            seq.AppendInterval(0.32f);
            seq.Append(DOTween.To(() => tmp.color.a, a => { var c = tmp.color; c.a = a; tmp.color = c; }, 0f, 0.36f));
            seq.OnComplete(() => { if (go != null) Destroy(go); });
        }
#endif

        private VfxStyle ResolveVfxStyle(BoardCard card)
        {
            var def = card?.SourceCard;
            var profile = (def?.CombatVfxProfileId ?? string.Empty).Trim().ToLowerInvariant();
            var accent = def != null && def.CombatVfxTint.a > 0.01f ? def.CombatVfxTint : Color.clear;

            if (accent.a <= 0.01f)
            {
                accent = profile switch
                {
                    "blood" => new Color(0.85f, 0.02f, 0.05f, 1f),
                    "shadow" => new Color(0.45f, 0.1f, 0.95f, 1f),
                    "ritual" => new Color(0.95f, 0.25f, 0.85f, 1f),
                    "town" => new Color(1f, 0.82f, 0.25f, 1f),
                    _ => DefaultColorFor(def)
                };
            }

            return new VfxStyle(accent, !string.IsNullOrEmpty(profile));
        }

        private static Color DefaultColorFor(CardDef def)
        {
            if (def == null) return new Color(1f, 0.35f, 0.18f, 1f);
            return def.Type switch
            {
                CardType.Human => new Color(0.35f, 0.8f, 1f, 1f),
                CardType.Building => new Color(0.9f, 0.72f, 0.36f, 1f),
                CardType.Town => new Color(1f, 0.9f, 0.35f, 1f),
                _ => new Color(1f, 0.35f, 0.18f, 1f)
            };
        }

        private static Color Blend(Color a, Color b)
        {
            return new Color((a.r + b.r) * 0.5f, (a.g + b.g) * 0.5f, (a.b + b.b) * 0.5f, 1f);
        }

        private readonly struct VfxStyle
        {
            public readonly Color Accent;
            public readonly bool IsSpecial;

            public VfxStyle(Color accent, bool isSpecial)
            {
                Accent = accent;
                IsSpecial = isSpecial;
            }
        }
    }
}
