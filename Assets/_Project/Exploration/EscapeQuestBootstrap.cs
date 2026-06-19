using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Exploration.Inventory;

namespace Exploration
{
    /// <summary>
    /// Runtime bootstrapper: wires the new escape-the-room flow without needing to run
    /// the editor menu. On scene load, finds the lockbox + door in the scene, attaches:
    ///   - GivePotionOnUnlocked on the box (drops Item_Potion when puzzle solves)
    ///   - RoomExitEndingTrigger on the door (shows the dark-screen ending on unlock)
    ///   - BlockedDoor._requiredKeyItem = Item_KeyRoom1 (if not yet assigned)
    /// Also resets EscapeQuestState so each scene entry starts fresh.
    /// </summary>
    public static class EscapeQuestBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EscapeQuestState.Reset();
            // Defer one frame: Inventory.Awake() and other scene MonoBehaviours may not have
            // run yet at sceneLoaded time, so Inventory.Current could still be null. Hand
            // the work to a hidden coroutine runner.
            CoroutineHost.Run(WireSceneNextFrame());
        }

        private static System.Collections.IEnumerator WireSceneNextFrame()
        {
            yield return null;
            WireScene();
        }

        /// <summary>Hidden MonoBehaviour used to run coroutines from a static context.</summary>
        private class CoroutineHost : MonoBehaviour
        {
            private static CoroutineHost _instance;
            public static void Run(System.Collections.IEnumerator co)
            {
                if (_instance == null)
                {
                    var go = new GameObject("~EscapeQuestBootstrapHost");
                    DontDestroyOnLoad(go);
                    go.hideFlags = HideFlags.HideAndDontSave;
                    _instance = go.AddComponent<CoroutineHost>();
                }
                _instance.StartCoroutine(co);
            }
        }

        /// <summary>
        /// Debug flag: when true, every lockbox in the scene has its dials reconfigured to
        /// the 6 house letters (А В Л П Х М) and auto-solved one frame after scene load,
        /// so you can verify the rest of the flow (lid opens, potion drops in inventory)
        /// without entering the password yourself. Flip back to false once you want the
        /// real puzzle.
        /// </summary>
        private const bool DebugPreSolveLockbox = true;

        /// <summary>Target combination — chronological founding order of vampire houses.</summary>
        private static readonly string[] HouseCode = { "А", "В", "Л", "П", "Х", "М" };

        private static void WireScene()
        {
            var potion = FindItemById(EscapeQuestState.PotionItemId);
            var key = FindItemById("key_room1");
            var note = FindItemById("note_clue");

            // Note belongs to the player from the start — it's the lore the vampire dropped
            // along with the key. Hand it over directly so the player doesn't need to find it.
            if (note != null)
            {
                var inv = Inventory.Inventory.Current;
                if (inv != null && !inv.Has(note)) inv.Add(note);
            }

            // STEP 1: subscribe a direct listener on OnUnlocked BEFORE auto-solving. We can't
            // rely on GivePotionOnUnlocked.Awake() — it runs at AddComponent time before our
            // SetPrivate sets the refs, and disables itself when it sees nulls. Direct
            // delegate avoids that race.
            foreach (var puzzle in Object.FindObjectsByType<RotaryLockPuzzle>(FindObjectsSortMode.None))
            {
                if (potion == null) break;
                var p = puzzle; // capture for closure
                puzzle.OnUnlocked.AddListener(() =>
                {
                    var inv = Inventory.Inventory.Current;
                    if (inv == null) { Debug.LogWarning("[EscapeQuestBootstrap] OnUnlocked fired but Inventory.Current is null."); return; }
                    if (inv.Has(potion)) return;
                    if (!inv.Add(potion)) Debug.LogWarning("[EscapeQuestBootstrap] Could not add potion to inventory.");
                    else Debug.Log($"[EscapeQuestBootstrap] Potion '{potion.Id}' added to inventory from puzzle '{p.name}'.");
                });

                // Attach the bottle-rises-and-chest-fades reveal. Awake runs at AddComponent
                // time with a null _puzzle, so we re-bind+subscribe manually after.
                var reveal = puzzle.GetComponent<LockboxPotionReveal>();
                if (reveal == null) reveal = puzzle.gameObject.AddComponent<LockboxPotionReveal>();
                reveal.BindAndSubscribe(puzzle);
                // Spawn the bottle at the BOX'S VISUAL CENTRE, not its root transform — the
                // root often sits at world origin while the visible mesh is parented under a
                // pivot child. Combined renderer bounds give us the real position.
                var spawn = ComputeVisualCentre(puzzle.gameObject);
                if (spawn != null) reveal.OverrideSpawn(spawn);

                // Auto-wire the lid swing. The editor tool used to do this; we re-do it at
                // runtime so even hand-authored boxes get the animation.
                EnsureLidWired(puzzle);
            }

            // STEP 2: now safe to auto-solve.
            if (DebugPreSolveLockbox)
            {
                foreach (var puzzle in Object.FindObjectsByType<RotaryLockPuzzle>(FindObjectsSortMode.None))
                    PreSolveLockbox(puzzle);
            }

            foreach (var door in Object.FindObjectsByType<BlockedDoor>(FindObjectsSortMode.None))
            {
                if (door.GetComponent<RoomExitEndingTrigger>() == null)
                    door.gameObject.AddComponent<RoomExitEndingTrigger>();
                if (key != null && GetPrivate<ItemDef>(door, "_requiredKeyItem") == null)
                    SetPrivate(door, "_requiredKeyItem", key);
            }

            if (key != null && !PickupExistsFor(key))
                SpawnKeyPickup(key);

            PatchInventoryUiLayout();
            ShiftInventoryPanelsDown();
        }

        /// <summary>
        /// Push MainPocket / KeyItems / Detail down toward the bottom of the screen — the
        /// authored canvas anchors them near the top. We rebind them to the BOTTOM-LEFT
        /// anchor so their distance is measured from the bottom edge, with a small footer
        /// gap so they don't run off-screen.
        /// </summary>
        private static void ShiftInventoryPanelsDown()
        {
            var ui = Object.FindAnyObjectByType<InventoryUI>(FindObjectsInactive.Include);
            if (ui == null) return;

            // Reference canvas: 1920×1080.
            const float FooterGap = 40f;
            const float SideMargin = 60f;
            const float Gap = 16f;
            const float RightColW = 640f;
            const float KeyItemsH = 360f;
            const float DetailH = 560f;
            const float MainW = 1920f - SideMargin * 2f - RightColW - Gap;
            const float ColumnH = KeyItemsH + Gap + DetailH;

            var main = FindByName(ui.transform, "MainPocket");
            if (main != null) SetBottomLeft(main, SideMargin, FooterGap, MainW, ColumnH);

            var keyItems = FindByName(ui.transform, "KeyItems");
            if (keyItems != null) SetBottomLeft(keyItems, SideMargin + MainW + Gap, FooterGap + DetailH + Gap, RightColW, KeyItemsH);

            var detail = FindByName(ui.transform, "Detail");
            if (detail != null) SetBottomLeft(detail, SideMargin + MainW + Gap, FooterGap, RightColW, DetailH);

            Debug.Log("[EscapeQuestBootstrap] Inventory panels shifted to bottom of screen.");
        }

        private static void SetBottomLeft(RectTransform rt, float x, float yFromBottom, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(x, yFromBottom);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>
        /// The InventoryCanvas was authored once via the editor menu and saved into the scene.
        /// Re-running the editor menu would be the "clean" fix but we want hot-reload of the
        /// layout without manual steps, so here we walk the canvas hierarchy by name and
        /// adjust the RectTransform offsets at scene-load time. Safe to call repeatedly —
        /// just overwrites anchored values.
        /// </summary>
        private static void PatchInventoryUiLayout()
        {
            var ui = Object.FindAnyObjectByType<InventoryUI>(FindObjectsInactive.Include);
            if (ui == null) return;

            var mainPocket = FindByName(ui.transform, "MainPocket");
            if (mainPocket != null)
            {
                // КАРМАН title — flush to the very top of the panel.
                var mainTitle = FindByName(mainPocket, "MainTitle");
                if (mainTitle != null) SetTopLeft(mainTitle, 20f, -10f, 400f, 32f);

                // Push the grid down so the КАРМАН / СЛОТЫ band has empty space above the cells.
                var mainGrid = FindByName(mainPocket, "MainGrid");
                if (mainGrid != null)
                {
                    mainGrid.anchorMin = new Vector2(0, 0);
                    mainGrid.anchorMax = new Vector2(1, 1);
                    mainGrid.pivot = new Vector2(0.5f, 0.5f);
                    mainGrid.offsetMin = new Vector2(20, 20);
                    mainGrid.offsetMax = new Vector2(-20, -180);
                }
            }

            // СЛОТЫ counter: reparent under MainPocket, right-aligned next to КАРМАН.
            var slots = FindByName(ui.transform, "SlotsCounter");
            if (slots != null && mainPocket != null)
            {
                slots.SetParent(mainPocket, false);
                var txt = slots.GetComponent<Text>();
                if (txt != null) { txt.alignment = TextAnchor.UpperRight; txt.enabled = true; }
                // sizeDelta read directly — rect.width is 0 before the first canvas layout pass.
                float mainW = mainPocket.sizeDelta.x > 0f ? mainPocket.sizeDelta.x : 1184f;
                SetTopLeft(slots, 0f, -10f, mainW - 20f, 32f);
            }

            // Push key items grid down too.
            var keyItems = FindByName(ui.transform, "KeyItems");
            if (keyItems != null)
            {
                var keyGrid = FindByName(keyItems, "KeyGrid");
                if (keyGrid != null)
                {
                    keyGrid.anchorMin = new Vector2(0, 0);
                    keyGrid.anchorMax = new Vector2(1, 1);
                    keyGrid.pivot = new Vector2(0.5f, 0.5f);
                    keyGrid.offsetMin = new Vector2(20, 20);
                    keyGrid.offsetMax = new Vector2(-20, -90);
                }
            }

            Debug.Log("[EscapeQuestBootstrap] InventoryCanvas layout patched at runtime.");
        }

        private static RectTransform FindByName(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<RectTransform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static void SetTopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static bool PickupExistsFor(ItemDef item)
        {
            foreach (var p in Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None))
                if (GetPrivate<ItemDef>(p, "_item") == item) return true;
            return false;
        }

        private static void SpawnKeyPickup(ItemDef key)
        {
            // Place the key in front of the player's spawn — represents the body of the
            // defeated vampire dropping it. Falls back to world origin if no player found.
            var player = Object.FindAnyObjectByType<ExplorationController>();
            Vector3 pos = player != null
                ? player.transform.position + player.transform.forward * 1.8f + Vector3.up * 0.05f
                : new Vector3(0f, 0.05f, 0f);

            var go = new GameObject("VampireKeyDrop");
            go.transform.position = pos;
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(0.3f, 0.1f, 0.3f);

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "KeyVisual";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(0.18f, 0.04f, 0.08f);
            var rend = visual.GetComponent<Renderer>();
            if (rend != null) rend.material.color = new Color(0.6f, 0.4f, 0.15f, 1f);
            Object.Destroy(visual.GetComponent<BoxCollider>());

            var pickup = go.AddComponent<PickupItem>();
            SetPrivate(pickup, "_item", key);
        }

        /// <summary>
        /// Build a small Transform anchor that sits at the geometric centre of the box's
        /// visible renderers, slightly above so the bottle clears the lid. Returns null if
        /// the box has no renderers.
        /// </summary>
        private static Transform ComputeVisualCentre(GameObject root)
        {
            var rends = root.GetComponentsInChildren<Renderer>(false);
            if (rends == null || rends.Length == 0) return null;
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            var anchor = new GameObject("~PotionSpawnAnchor");
            anchor.transform.SetParent(root.transform, false);
            anchor.transform.position = b.center + Vector3.up * (b.extents.y * 0.5f);
            return anchor.transform;
        }

        /// <summary>
        /// Find a child whose name hints "lid" / "крыш" / "top" / "cover", give it an
        /// OpenableObject if missing, then attach a LidLinkOnUnlocked that subscribes to
        /// RotaryLockPuzzle.OnUnlocked → opens the lid. No-op if everything is already wired.
        /// </summary>
        private static void EnsureLidWired(RotaryLockPuzzle puzzle)
        {
            Debug.Log($"[EscapeQuestBootstrap] EnsureLidWired entered for '{puzzle.name}'.");
            var lid = FindLid(puzzle.transform);
            if (lid == null)
            {
                // Dump the immediate child names so the user can tell us what the lid is
                // actually called — we'll just add it to the hint list.
                var sb = new System.Text.StringBuilder();
                foreach (Transform c in puzzle.transform) sb.Append(c.name).Append(", ");
                Debug.LogWarning($"[EscapeQuestBootstrap] No lid found on '{puzzle.name}'. " +
                    $"Looked for child name containing: lid / top / cover / крыш. " +
                    $"Direct children: [{sb.ToString().TrimEnd(',', ' ')}]. " +
                    $"Rename the lid object or tell which hint to add.");
                return;
            }
            Debug.Log($"[EscapeQuestBootstrap] Lid detected on '{puzzle.name}': '{lid.name}' (full path: {GetPath(lid)}).");

            var openable = lid.GetComponent<OpenableObject>();
            bool freshlyAdded = openable == null;
            if (freshlyAdded) openable = lid.gameObject.AddComponent<OpenableObject>();
            // The editor tool defaulted to swing on X = -110 degrees. Match it.
            SetPrivate(openable, "_door", lid);
            var openEuler = new Vector3(-110f, 0f, 0f);
            SetPrivateValue(openable, "_openLocalEuler", openEuler);
            SetPrivateValue(openable, "_isLocked", true);

            // Re-run the bookkeeping Awake() does: if we just added the component, Awake ran
            // BEFORE we set _door, so _closedRotation / _openRotation are still identity →
            // AnimateSwing slerps identity→identity → lid never moves. Fix it by writing
            // those private fields ourselves now that _door is set.
            if (freshlyAdded)
            {
                var closedRot = lid.localRotation;
                var openRot = Quaternion.Euler(closedRot.eulerAngles + openEuler);
                SetPrivateValue(openable, "_closedRotation", closedRot);
                SetPrivateValue(openable, "_openRotation", openRot);
            }

            // Direct OnUnlocked listener so the lid swings even if LidLinkOnUnlocked isn't
            // present. Idempotent — we only add this once per puzzle.
            const string sentinelKey = "__escape_quest_lid_wired";
            if (puzzle.gameObject.GetComponent<LidWiredSentinel>() != null) return;
            puzzle.gameObject.AddComponent<LidWiredSentinel>();
            var captured = openable;
            var capturedLid = lid;
            puzzle.OnUnlocked.AddListener(() =>
            {
                if (captured == null) { Debug.LogWarning("[EscapeQuestBootstrap] Lid listener fired but openable is null."); return; }
                Debug.Log($"[EscapeQuestBootstrap] OnUnlocked → opening lid '{capturedLid.name}'. " +
                          $"locked={captured.IsLocked}, open={captured.IsOpen}");
                captured.SetLocked(false);
                captured.Open();
            });
        }

        /// <summary>Empty marker component so EnsureLidWired stays idempotent.</summary>
        private class LidWiredSentinel : MonoBehaviour { }

        private static string GetPath(Transform t)
        {
            if (t == null) return "<null>";
            var sb = new System.Text.StringBuilder(t.name);
            var cur = t.parent;
            while (cur != null) { sb.Insert(0, cur.name + "/"); cur = cur.parent; }
            return sb.ToString();
        }

        private static Transform FindLid(Transform root)
        {
            string[] hints = { "lid", "top", "cover", "крыш" };
            Transform found = null;
            void Walk(Transform t)
            {
                if (found != null) return;
                foreach (Transform c in t)
                {
                    string n = c.name.ToLowerInvariant();
                    foreach (var h in hints)
                    {
                        if (n.Contains(h)) { found = c; return; }
                    }
                    Walk(c);
                    if (found != null) return;
                }
            }
            Walk(root);
            return found;
        }

        /// <summary>
        /// Pre-configures the puzzle so it's ONE input away from being solved. Steps:
        ///   1. Rewrite each dial's Symbols with the house letters and sync the target code.
        ///   2. Snap dials 0..n-2 to the correct symbol.
        ///   3. Snap the LAST dial one step forward of the correct symbol — the player has
        ///      to pull it back (S key, scroll down, or step -1) to land on the right letter.
        /// Does NOT invoke OnUnlocked — that fires naturally inside RotaryLockPuzzle.StepDial
        /// → CheckSolution → Solve when the player dials the last ring back.
        /// </summary>
        private static void PreSolveLockbox(RotaryLockPuzzle puzzle)
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var dialsField = typeof(RotaryLockPuzzle).GetField("_dials", flags);
            var codeField = typeof(RotaryLockPuzzle).GetField("_targetCode", flags);
            if (dialsField == null || codeField == null) return;
            var dials = dialsField.GetValue(puzzle) as System.Collections.Generic.List<RotaryDial>;
            if (dials == null) return;

            int n = Mathf.Min(dials.Count, HouseCode.Length);

            for (int i = 0; i < n; i++)
            {
                var dial = dials[i];
                if (dial == null) continue;
                dial.Symbols = (string[])HouseCode.Clone();
                dial.DegreesPerStep = 60f;
                // i == n - 1 → offset by +1 so the player must step backward to solve.
                int idx = i == n - 1 ? (i + 1) % HouseCode.Length : i;
                dial.SetIndexInstant(idx);
            }

            var newCode = new string[n];
            for (int i = 0; i < n; i++) newCode[i] = HouseCode[i];
            codeField.SetValue(puzzle, newCode);

            Debug.Log($"[EscapeQuestBootstrap] Lockbox '{puzzle.name}' pre-set — target '{string.Join("", newCode)}'. " +
                      "Last dial is +1 off; pull it back (S key / scroll down) to open.");
        }

        private static ItemDef FindItemById(string id)
        {
            var inv = Inventory.Inventory.Current;
            var reg = inv != null ? inv.Registry : Resources.Load<ItemRegistry>("ItemRegistry");
            if (reg == null) return null;
            foreach (var def in reg.Items)
                if (def != null && def.Id == id) return def;
            return null;
        }

        private static void SetPrivate(Object target, string field, Object value)
        {
            var f = target.GetType().GetField(field,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            if (f != null) f.SetValue(target, value);
        }

        /// <summary>Like SetPrivate, but for value-type / non-Object fields (bool, Vector3, ...).</summary>
        private static void SetPrivateValue(object target, string field, object value)
        {
            var f = target.GetType().GetField(field,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            if (f != null) f.SetValue(target, value);
        }

        private static T GetPrivate<T>(Object target, string field) where T : class
        {
            var f = target.GetType().GetField(field,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            return f?.GetValue(target) as T;
        }
    }
}
