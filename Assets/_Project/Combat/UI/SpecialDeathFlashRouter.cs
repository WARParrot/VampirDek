using Combat;
using Core;
using UnityEngine;
using Shared.UI;

namespace Combat.UI
{
    // Listens for special on-death callouts and routes them to the center-screen flash overlay.
    // Currently:
    //   - VampireLoner death → red pulse "О нет, они всё-таки нашли меня".
    public class SpecialDeathFlashRouter : MonoBehaviour
    {
        private System.IDisposable _sub;
        private System.IDisposable _subPlaced;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (FindFirstObjectByType<SpecialDeathFlashRouter>() != null) return;
            var go = new GameObject(nameof(SpecialDeathFlashRouter));
            DontDestroyOnLoad(go);
            go.AddComponent<SpecialDeathFlashRouter>();
        }

        private void OnEnable()
        {
            StartCoroutine(SubscribeWhenReady());
        }

        private System.Collections.IEnumerator SubscribeWhenReady()
        {
            int waited = 0;
            while (waited < 600)
            {
                Core.EventBus bus = null;
                try { bus = GlobalServices.EventBus; }
                catch { /* Resolver not ready yet */ }

                if (bus != null)
                {
                    _sub = bus.Subscribe<EntityDiedEvent>(OnDied);
                    _subPlaced = bus.Subscribe<PlacedCardEvent>(OnPlaced);
                    yield break;
                }
                waited++;
                yield return null;
            }
        }

        private void OnDisable()
        {
            _sub?.Dispose();
            _sub = null;
            _subPlaced?.Dispose();
            _subPlaced = null;
        }

        private static void OnDied(EntityDiedEvent evt)
        {
            if (evt.Entity is BoardCard bc && bc.SourceCard != null
                && bc.SourceCard.CardName == CardBehaviorTags.VampireLoner)
            {
                EffectFlashOverlay.ShowLonerFound();
            }
        }

        private static void OnPlaced(PlacedCardEvent evt)
        {
            if (evt.Card?.SourceCard != null && evt.Card.SourceCard.CardName == "BloodWitch")
            {
                EffectFlashOverlay.ShowBloodWitchSpawn();
            }
        }
    }
}
