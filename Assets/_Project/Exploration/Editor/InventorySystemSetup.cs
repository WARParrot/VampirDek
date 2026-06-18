#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Exploration.Inventory;

namespace Exploration.Editor
{
    /// <summary>
    /// One-shot scaffolder for the RE2-style inventory.
    /// Run "VampirDek/Inventory/Bootstrap All" to populate the project with sample items,
    /// recipes, an ItemRegistry in Resources, and to drop a functional Inventory runtime +
    /// Inventory UI canvas into the currently open scene.
    ///
    /// Other entries spawn individual pieces if you only need one (e.g. a sample rotary lockbox).
    /// </summary>
    public static class InventorySystemSetup
    {
        private const string DataFolder = "Assets/_Project/Data/Inventory";
        private const string ResourcesFolder = "Assets/Resources";
        private const string RegistryPath = ResourcesFolder + "/ItemRegistry.asset";

        // ---- top-level menu --------------------------------------------------

        [MenuItem("VampirDek/Inventory/Bootstrap All", false, 0)]
        public static void BootstrapAll()
        {
            EnsureFolders();
            var registry = CreateOrLoadRegistry();
            CreateSampleItems(registry);
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();

            SpawnInventoryRuntime(registry);
            SpawnInventoryUI();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog(
                "VampirDek Inventory",
                "Готово.\n\n" +
                "• Sample items + ItemRegistry: " + DataFolder + "\n" +
                "• Inventory runtime и InventoryCanvas — в активной сцене.\n\n" +
                "Не забудь в InventoryCanvas/InventoryUI назначить ссылку на ExplorationController сцены.",
                "OK");
        }

        [MenuItem("VampirDek/Inventory/Create Sample Items + Registry", false, 11)]
        public static void OnlyCreateAssets()
        {
            EnsureFolders();
            var reg = CreateOrLoadRegistry();
            CreateSampleItems(reg);
            EditorUtility.SetDirty(reg);
            AssetDatabase.SaveAssets();
            Selection.activeObject = reg;
            EditorGUIUtility.PingObject(reg);
        }

        [MenuItem("VampirDek/Inventory/Spawn Inventory Runtime", false, 12)]
        public static void OnlySpawnRuntime()
        {
            EnsureFolders();
            var registry = AssetDatabase.LoadAssetAtPath<ItemRegistry>(RegistryPath);
            if (registry == null) registry = CreateOrLoadRegistry();
            SpawnInventoryRuntime(registry);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        [MenuItem("VampirDek/Inventory/Spawn Inventory UI", false, 13)]
        public static void OnlySpawnUI()
        {
            SpawnInventoryUI();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        [MenuItem("VampirDek/Inventory/Diagnose Selected Interactable", false, 30)]
        public static void DiagnoseSelectedInteractable()
        {
            var go = Selection.activeGameObject;
            if (go == null) { Debug.LogWarning("[Diagnose] Выдели объект."); return; }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Diagnose: " + go.name + " ===");

            var interactable = go.GetComponentInParent<Exploration.IInteractable>();
            sb.AppendLine("IInteractable on root or parent: " + (interactable == null ? "MISSING" : interactable.GetType().Name));

            var cols = go.GetComponentsInChildren<Collider>(includeInactive: false);
            sb.AppendLine("Colliders in hierarchy: " + cols.Length);
            foreach (var c in cols)
                sb.AppendLine($"  - {c.GetType().Name} on '{c.gameObject.name}' (layer {LayerMask.LayerToName(c.gameObject.layer)}, enabled={c.enabled}, isTrigger={c.isTrigger})");

            var rends = go.GetComponentsInChildren<Renderer>();
            sb.AppendLine("Renderers in hierarchy: " + rends.Length);

            // Distance to player if any
            var player = Object.FindAnyObjectByType<ExplorationController>();
            if (player != null)
                sb.AppendLine("Distance to player: " + Vector3.Distance(player.transform.position, go.transform.position).ToString("F2") + " m (interaction range usually 4m)");

            sb.AppendLine("Root scale: " + go.transform.lossyScale);
            sb.AppendLine("Root rotation: " + go.transform.rotation.eulerAngles);

            Debug.Log(sb.ToString(), go);
        }

        [MenuItem("VampirDek/Inventory/Ensure Colliders On Selected (fix raycast)", false, 31)]
        public static void EnsureCollidersOnSelected()
        {
            var go = Selection.activeGameObject;
            if (go == null) { EditorUtility.DisplayDialog("VampirDek", "Выдели шкатулку.", "OK"); return; }

            // Strategy: remove the (possibly busted) auto BoxCollider on root and add a MeshCollider
            // to every child renderer that doesn't already have one. This way the raycast hits the
            // actual visible geometry — there's no chance of a mis-sized bounding volume.
            var rootCol = go.GetComponent<Collider>();
            if (rootCol is BoxCollider)
            {
                Undo.DestroyObjectImmediate(rootCol);
                Debug.Log("[Diagnose] Удалён старый BoxCollider с корня.");
            }

            int added = 0;
            foreach (var rend in go.GetComponentsInChildren<MeshFilter>(includeInactive: false))
            {
                if (rend.GetComponent<Collider>() != null) continue;
                var mc = rend.gameObject.AddComponent<MeshCollider>();
                mc.convex = false; // non-convex is fine for static raycast targets
                added++;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("VampirDek",
                $"Добавлено MeshCollider'ов: {added}.\n\nТеперь raycast будет попадать в любую часть шкатулки.",
                "OK");
            Debug.Log($"[Diagnose] Добавлено {added} MeshCollider'ов на '{go.name}'.");
        }

        [MenuItem("VampirDek/Inventory/Setup Selected As Hex Dials", false, 24)]
        public static void SetupSelectedAsHexDials()
        {
            var selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
            {
                EditorUtility.DisplayDialog("VampirDek",
                    "Выдели все шестигранные диски (Ctrl+ЛКМ для нескольких) и запусти снова.",
                    "OK");
                return;
            }

            // Find the puzzle on the box.
            RotaryLockPuzzle puzzle = null;
            foreach (var go in selection)
            {
                puzzle = go.GetComponentInParent<RotaryLockPuzzle>();
                if (puzzle != null) break;
            }
            if (puzzle == null)
            {
                var box = FindBoxInScene();
                if (box != null) puzzle = box.GetComponent<RotaryLockPuzzle>() ?? box.AddComponent<RotaryLockPuzzle>();
            }

            string[] hexSymbols = { "A", "B", "C", "D", "E", "F" };
            var dials = new System.Collections.Generic.List<RotaryDial>();

            foreach (var go in selection)
            {
                var rd = go.GetComponent<RotaryDial>();
                if (rd == null) rd = go.AddComponent<RotaryDial>();
                rd.Symbols = (string[])hexSymbols.Clone();
                rd.DegreesPerStep = 60f;

                // Ensure colliders so clicks land.
                if (go.GetComponentInChildren<Collider>() == null)
                {
                    var ren = go.GetComponentInChildren<Renderer>();
                    if (ren != null)
                    {
                        var mc = ren.gameObject.AddComponent<MeshCollider>();
                        mc.convex = true;
                    }
                    else
                    {
                        var bc = go.AddComponent<BoxCollider>();
                        bc.size = Vector3.one * 0.2f;
                    }
                }

                EditorUtility.SetDirty(rd);
                dials.Add(rd);
            }

            // Sort by x then z so reading order matches visual layout.
            dials.Sort((a, b) =>
            {
                int cx = a.transform.position.x.CompareTo(b.transform.position.x);
                if (cx != 0) return cx;
                return a.transform.position.z.CompareTo(b.transform.position.z);
            });

            // Random hex code (A..F) per dial.
            var rng = new System.Random();
            var code = new string[dials.Count];
            for (int i = 0; i < code.Length; i++) code[i] = hexSymbols[rng.Next(hexSymbols.Length)];

            if (puzzle != null)
            {
                var so = new SerializedObject(puzzle);
                var dialsProp = so.FindProperty("_dials");
                dialsProp.arraySize = dials.Count;
                for (int i = 0; i < dials.Count; i++)
                    dialsProp.GetArrayElementAtIndex(i).objectReferenceValue = dials[i];
                var codeProp = so.FindProperty("_targetCode");
                codeProp.arraySize = code.Length;
                for (int i = 0; i < code.Length; i++)
                    codeProp.GetArrayElementAtIndex(i).stringValue = code[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Note text intentionally left alone — Item_Note holds the canonical 6-houses lore
            // for the main quest. Random hex codes generated by this debug tool are only printed
            // to the console.

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("VampirDek",
                $"Hex-диски настроены: {dials.Count}\nКод (debug): {string.Join("", code)}\n\n" +
                "Каждый диск: 6 граней A-F, шаг 60°. Это debug-инструмент; основной квест ставит " +
                "русские буквы домов через 'Setup Scene Box As Lockbox'.",
                "OK");
            Debug.Log($"[InventorySetup] Configured {dials.Count} hex dials. Code: {string.Join("", code)}");
        }

        /// <summary>
        /// For a hexagonal prism the spin axis is the longest dimension of its local-space bounds.
        /// </summary>
        private static Vector3 GuessHexAxisLocal(Transform dial)
        {
            var rends = dial.GetComponentsInChildren<MeshFilter>();
            if (rends.Length == 0) return Vector3.up;
            // Build local-space bounds from mesh vertices via transform inverse on world bounds extents
            // is tricky; simpler: pick the axis where world-bounds extent is largest, then map back.
            var b = rends[0].GetComponent<Renderer>() != null ? rends[0].GetComponent<Renderer>().bounds : new Bounds(dial.position, Vector3.one * 0.1f);
            for (int i = 1; i < rends.Length; i++)
            {
                var r = rends[i].GetComponent<Renderer>();
                if (r != null) b.Encapsulate(r.bounds);
            }
            // For axis-aligned bounds, the longest world extent corresponds to a world axis.
            Vector3 e = b.extents;
            Vector3 worldAxis =
                (e.x >= e.y && e.x >= e.z) ? Vector3.right :
                (e.y >= e.x && e.y >= e.z) ? Vector3.up :
                                              Vector3.forward;
            // Convert that world direction into the dial's local space so it stays consistent
            // even if the dial transform is rotated.
            Vector3 local = dial.InverseTransformDirection(worldAxis);
            // Snap to nearest local axis for clean editor values.
            float ax = Mathf.Abs(local.x), ay = Mathf.Abs(local.y), az = Mathf.Abs(local.z);
            if (ax >= ay && ax >= az) return new Vector3(Mathf.Sign(local.x == 0 ? 1 : local.x), 0, 0);
            if (ay >= ax && ay >= az) return new Vector3(0, Mathf.Sign(local.y == 0 ? 1 : local.y), 0);
            return new Vector3(0, 0, Mathf.Sign(local.z == 0 ? 1 : local.z));
        }

        // Dial Axis menus removed — RotaryDial now always spins around its own local Y.

        [MenuItem("VampirDek/Inventory/Bind Selected As Dials", false, 23)]
        public static void BindSelectedAsDials()
        {
            var selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
            {
                EditorUtility.DisplayDialog("VampirDek",
                    "Выдели в Hierarchy ВСЕ дочерние объекты-диски шкатулки (Ctrl+ЛКМ для нескольких) и запусти снова.",
                    "OK");
                return;
            }

            // Find the puzzle: nearest RotaryLockPuzzle going up from any selected object.
            RotaryLockPuzzle puzzle = null;
            foreach (var go in selection)
            {
                puzzle = go.GetComponentInParent<RotaryLockPuzzle>();
                if (puzzle != null) break;
            }
            if (puzzle == null)
            {
                var box = FindBoxInScene();
                if (box != null) puzzle = box.GetComponent<RotaryLockPuzzle>() ?? box.AddComponent<RotaryLockPuzzle>();
            }
            if (puzzle == null)
            {
                EditorUtility.DisplayDialog("VampirDek",
                    "Не нашёл RotaryLockPuzzle. Сначала запусти 'Setup Scene Box As Lockbox' или повесь компонент вручную на корень шкатулки.",
                    "OK");
                return;
            }

            string[] alphabet = { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" };
            var dials = new System.Collections.Generic.List<RotaryDial>();
            foreach (var go in selection)
            {
                var rd = go.GetComponent<RotaryDial>();
                if (rd == null) rd = go.AddComponent<RotaryDial>();
                rd.Symbols = (string[])alphabet.Clone();
                rd.DegreesPerStep = 360f / rd.Symbols.Length;

                if (go.GetComponentInChildren<Collider>() == null)
                {
                    var ren = go.GetComponentInChildren<Renderer>();
                    if (ren != null)
                    {
                        var mc = ren.gameObject.AddComponent<MeshCollider>();
                        mc.convex = true;
                    }
                    else
                    {
                        var bc = go.AddComponent<BoxCollider>();
                        bc.size = Vector3.one * 0.2f;
                    }
                }

                EditorUtility.SetDirty(rd);
                dials.Add(rd);
            }

            // Sort by world position along x then z so visual order matches reading order.
            dials.Sort((a, b) =>
            {
                int cx = a.transform.position.x.CompareTo(b.transform.position.x);
                if (cx != 0) return cx;
                return a.transform.position.z.CompareTo(b.transform.position.z);
            });

            var rng = new System.Random();
            var code = new string[dials.Count];
            for (int i = 0; i < code.Length; i++) code[i] = alphabet[rng.Next(alphabet.Length)];

            var so = new SerializedObject(puzzle);
            var dialsProp = so.FindProperty("_dials");
            dialsProp.arraySize = dials.Count;
            for (int i = 0; i < dials.Count; i++)
                dialsProp.GetArrayElementAtIndex(i).objectReferenceValue = dials[i];
            var codeProp = so.FindProperty("_targetCode");
            codeProp.arraySize = code.Length;
            for (int i = 0; i < code.Length; i++)
                codeProp.GetArrayElementAtIndex(i).stringValue = code[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            // Note text intentionally left alone — Item_Note holds the canonical 6-houses lore.
            // Random codes from this debug tool are only printed to the console.

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("VampirDek",
                $"Привязано дисков: {dials.Count}\nКод: {string.Join("", code)}\n\nЗапиши код или посмотри Examine на Item_Note.",
                "OK");
            Debug.Log($"[InventorySetup] Bound {dials.Count} dials to {puzzle.name}. Code: {string.Join("", code)}");
        }

        [MenuItem("VampirDek/Inventory/Spawn Sample Rotary Lockbox", false, 21)]
        public static void SpawnSampleLockbox()
        {
            CreateSampleLockbox();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        [MenuItem("VampirDek/Inventory/Setup Scene Box As Lockbox", false, 20)]
        public static void ConfigureSceneBoxLockbox()
        {
            // Find the player's authored box in the open scene by name (case-insensitive).
            var go = FindBoxInScene();
            if (go == null)
            {
                EditorUtility.DisplayDialog("VampirDek",
                    "Не нашёл объект 'box' (или 'lockbox' / 'шкатул*' / 'сундук*') в активной сцене. " +
                    "Переименуй корневой GameObject шкатулки в 'box' и запусти ещё раз — или используй " +
                    "пункт 'Setup Selected As Rotary Lockbox' и выдели его вручную.",
                    "OK");
                return;
            }
            // Remove the leftover sample lockbox if it was spawned earlier.
            var sample = GameObject.Find("SampleRotaryLockbox");
            if (sample != null && sample != go)
            {
                Undo.DestroyObjectImmediate(sample);
                Debug.Log("[InventorySetup] Удалён предыдущий SampleRotaryLockbox — используется твой '" + go.name + "'.");
            }

            EnsureFolders();
            var registry = CreateOrLoadRegistry();
            CreateSampleItems(registry);
            SpawnInventoryRuntime(registry);
            SpawnInventoryUI();

            ConfigureLockboxRoot(go, registry);
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }

        private static GameObject FindBoxInScene()
        {
            string[] hints = { "lockbox", "шкатул", "сундук" };
            GameObject exactBox = null;
            GameObject partial = null;

            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                string n = t.name.ToLowerInvariant();
                if (n == "box") { exactBox = t.gameObject; break; }
                if (partial != null) continue;
                if (n.Contains("box") && n != "skybox") { partial = t.gameObject; continue; }
                foreach (var h in hints) if (n.Contains(h)) { partial = t.gameObject; break; }
            }
            return exactBox != null ? exactBox : partial;
        }

        [MenuItem("VampirDek/Inventory/Setup Selected As Rotary Lockbox", false, 22)]
        public static void ConfigureSelectedLockbox()
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                EditorUtility.DisplayDialog("VampirDek", "Выдели шкатулку (корневой GameObject) в Hierarchy и запусти ещё раз.", "OK");
                return;
            }

            EnsureFolders();
            var registry = CreateOrLoadRegistry();
            CreateSampleItems(registry);

            // Make sure runtime + UI exist
            SpawnInventoryRuntime(registry);
            SpawnInventoryUI();

            ConfigureLockboxRoot(go, registry);
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }

        private static void ConfigureLockboxRoot(GameObject root, ItemRegistry registry)
        {
            // 1. Collider for interaction raycast.
            if (root.GetComponent<Collider>() == null)
            {
                var box = root.AddComponent<BoxCollider>();
                // Try to size to combined renderer bounds.
                var rends = root.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    var b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    box.center = root.transform.InverseTransformPoint(b.center);
                    box.size = root.transform.InverseTransformVector(b.size);
                    box.size = new Vector3(Mathf.Abs(box.size.x), Mathf.Abs(box.size.y), Mathf.Abs(box.size.z));
                }
            }

            // 2. Find candidate dial children. Pattern: name contains "dial", "ring", "wheel",
            //    "циф", "колес", "кольц" — sorted by sibling order (assumed left-to-right authoring).
            var dials = FindDials(root.transform);
            if (dials.Count == 0)
            {
                // Fallback: every direct child with a Renderer that isn't named "lid"/"top"/"body".
                foreach (Transform t in root.transform)
                {
                    if (t.GetComponentInChildren<Renderer>() == null) continue;
                    string n = t.name.ToLowerInvariant();
                    if (n.Contains("lid") || n.Contains("top") || n.Contains("cover") || n.Contains("body") || n.Contains("крыш")) continue;
                    dials.Add(t.gameObject);
                }
            }

            if (dials.Count == 0)
            {
                EditorUtility.DisplayDialog("VampirDek",
                    "Не нашёл колец. Назови дочерние объекты 'Dial_1', 'Ring_1' и т.п. — или это просто крышка без рингов?",
                    "OK");
                return;
            }

            // 3. House-of-vampires symbols. Each dial carries the first letter of one of the
            //    six original houses. The puzzle solution is the houses in chronological order
            //    of their founding (clue is in Item_Note's lore text).
            //      А — Алой Короны        (origin)
            //      В — Вечного голода     (early rebellion suppression)
            //      Л — Лунного блика      (concealment magic for the castles)
            //      П — Полярных сов       (prison house)
            //      Х — Хранителей пепла   (post-great-rebellion castle restoration)
            //      М — Манящего Эха       (blood-graphophone communication)
            string[] houseLetters = { "А", "В", "Л", "П", "Х", "М" };

            // Clamp to 6 dials so we always match the 6-letter code length. If the scene has
            // extra "dial"-named children, the leftovers are ignored.
            if (dials.Count > houseLetters.Length)
                dials.RemoveRange(houseLetters.Length, dials.Count - houseLetters.Length);

            var dialComponents = new System.Collections.Generic.List<RotaryDial>();
            foreach (var d in dials)
            {
                var rd = d.GetComponent<RotaryDial>();
                if (rd == null) rd = d.AddComponent<RotaryDial>();
                rd.Symbols = (string[])houseLetters.Clone();
                rd.DegreesPerStep = 60f;
                EditorUtility.SetDirty(rd);
                dialComponents.Add(rd);
            }

            // 4. Target code = houses in founding order.
            string[] code = (string[])houseLetters.Clone();
            // If the scene has fewer than 6 dials, truncate so CheckSolution can succeed.
            if (dialComponents.Count < code.Length)
            {
                var truncated = new string[dialComponents.Count];
                System.Array.Copy(code, truncated, truncated.Length);
                code = truncated;
                Debug.LogWarning($"[InventorySetup] Шкатулка собрана только из {dialComponents.Count} дисков, ожидалось 6. " +
                                 "Целевой код усечён — игроку будет легче.");
            }
            string codeJoined = string.Join("", code);

            // 5. Ensure RotaryLockPuzzle on the root.
            var puzzle = root.GetComponent<RotaryLockPuzzle>();
            if (puzzle == null) puzzle = root.AddComponent<RotaryLockPuzzle>();

            // 6. Note is the canonical clue: keep whatever lore text the asset already holds
            //    (see Item_Note.asset — it lists the 6 houses; the puzzle answer is their
            //    chronological founding order). Just make sure the registry knows it.
            var note = AssetDatabase.LoadAssetAtPath<ItemDef>(DataFolder + "/Item_Note.asset");
            if (note != null) AddToRegistry(registry, note);

            // 7. Wire the puzzle via SerializedObject.
            var so = new SerializedObject(puzzle);
            var dialsProp = so.FindProperty("_dials");
            dialsProp.arraySize = dialComponents.Count;
            for (int i = 0; i < dialComponents.Count; i++)
                dialsProp.GetArrayElementAtIndex(i).objectReferenceValue = dialComponents[i];

            var codeProp = so.FindProperty("_targetCode");
            codeProp.arraySize = code.Length;
            for (int i = 0; i < code.Length; i++)
                codeProp.GetArrayElementAtIndex(i).stringValue = code[i];

            // Не делаем подсказку обязательной: иначе игрок не сможет даже открыть examine
            // и не поймёт, что от него хотят. Записка просто раскрывает код в examine, но не
            // блокирует доступ к шкатулке.
            // if (note != null) SetRef(so, "_requiredClueItem", note);

            // Spawn an in-world HUD canvas above the box so RotaryLockPuzzle can show the dial
            // readout when examining. Editor-generated, runtime-toggleable.
            var hudCanvasGo = new GameObject("LockboxHud",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            hudCanvasGo.transform.SetParent(root.transform, false);
            var hudCanvas = hudCanvasGo.GetComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.WorldSpace;
            var hudRect = (RectTransform)hudCanvasGo.transform;
            hudRect.sizeDelta = new Vector2(800, 200);
            hudRect.localScale = Vector3.one * 0.0025f;
            hudRect.localPosition = new Vector3(0f, 0.6f, 0f);

            var hudBgGo = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            hudBgGo.transform.SetParent(hudCanvasGo.transform, false);
            var hudBgRt = (RectTransform)hudBgGo.transform;
            hudBgRt.anchorMin = Vector2.zero; hudBgRt.anchorMax = Vector2.one;
            hudBgRt.offsetMin = Vector2.zero; hudBgRt.offsetMax = Vector2.zero;
            hudBgGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            var hudLabelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            hudLabelGo.transform.SetParent(hudCanvasGo.transform, false);
            var hudLabelRt = (RectTransform)hudLabelGo.transform;
            hudLabelRt.anchorMin = Vector2.zero; hudLabelRt.anchorMax = Vector2.one;
            hudLabelRt.offsetMin = new Vector2(20, 20); hudLabelRt.offsetMax = new Vector2(-20, -20);
            var hudText = hudLabelGo.GetComponent<Text>();
            hudText.alignment = TextAnchor.MiddleCenter;
            hudText.color = new Color(0.95f, 0.78f, 0.35f, 1f);
            hudText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hudText.fontSize = 24;
            hudText.supportRichText = true;
            hudText.raycastTarget = false;
            hudCanvasGo.SetActive(false);

            SetRef(so, "_hudRoot", hudCanvasGo);
            SetRef(so, "_hudLabel", hudText);

            // 8. Lid hookup — find a child whose name suggests a lid and add OpenableObject if missing.
            var lid = FindLid(root.transform);
            if (lid != null)
            {
                var openable = lid.GetComponent<OpenableObject>();
                if (openable == null) openable = lid.gameObject.AddComponent<OpenableObject>();
                // Make the lid swing on the X axis (most lids hinge that way). User can tweak.
                var openableSo = new SerializedObject(openable);
                var doorProp = openableSo.FindProperty("_door");
                if (doorProp != null && doorProp.objectReferenceValue == null)
                    doorProp.objectReferenceValue = lid;
                var eulerProp = openableSo.FindProperty("_openLocalEuler");
                if (eulerProp != null) eulerProp.vector3Value = new Vector3(-110f, 0f, 0f);
                var lockedProp = openableSo.FindProperty("_isLocked");
                if (lockedProp != null) lockedProp.boolValue = true;
                openableSo.ApplyModifiedPropertiesWithoutUndo();

                // Wire OnUnlocked → openable.SetLocked(false) + openable.Open()
                var onUnlockedProp = so.FindProperty("OnUnlocked");
                if (onUnlockedProp != null)
                {
                    // UnityEvent persistent calls are awkward via SerializedProperty; instead we
                    // attach an LidLink helper that wires itself at runtime.
                    var link = root.GetComponent<LidLinkOnUnlocked>();
                    if (link == null) link = root.AddComponent<LidLinkOnUnlocked>();
                    var linkSo = new SerializedObject(link);
                    SetRef(linkSo, "_puzzle", puzzle);
                    SetRef(linkSo, "_openable", openable);
                    linkSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // 8b. Drop the antidote potion into the player's inventory when the lid pops.
            var potion = AssetDatabase.LoadAssetAtPath<ItemDef>(DataFolder + "/Item_Potion.asset");
            if (potion != null)
            {
                var giver = root.GetComponent<GivePotionOnUnlocked>();
                if (giver == null) giver = root.AddComponent<GivePotionOnUnlocked>();
                var gso = new SerializedObject(giver);
                SetRef(gso, "_puzzle", puzzle);
                SetRef(gso, "_potion", potion);
                gso.ApplyModifiedPropertiesWithoutUndo();
                AddToRegistry(registry, potion);
            }
            else
            {
                Debug.LogWarning("[InventorySetup] Item_Potion.asset не найден — сначала запусти 'Create Sample Items + Registry'.");
            }

            // 9. Drop a PickupItem (the note) on the floor in front of the box so the player has a
            //    discoverable clue. Position: 2m in front of the box at floor height.
            SpawnNotePickupNear(root, note);

            Debug.Log("[InventorySetup] Шкатулка настроена. Код: " + codeJoined + ". Сгенерирована записка-подсказка.");
        }

        private static System.Collections.Generic.List<GameObject> FindDials(Transform root)
        {
            var list = new System.Collections.Generic.List<GameObject>();
            string[] hints = { "dial", "ring", "wheel", "циф", "колес", "кольц" };
            void Walk(Transform t)
            {
                foreach (Transform c in t)
                {
                    string n = c.name.ToLowerInvariant();
                    bool match = false;
                    foreach (var h in hints) if (n.Contains(h)) { match = true; break; }
                    if (match) list.Add(c.gameObject);
                    else Walk(c);
                }
            }
            Walk(root);
            // Sort by world position along x then z so order matches visual reading.
            list.Sort((a, b) =>
            {
                int cx = a.transform.position.x.CompareTo(b.transform.position.x);
                if (cx != 0) return cx;
                return a.transform.position.z.CompareTo(b.transform.position.z);
            });
            return list;
        }

        private static Transform FindLid(Transform root)
        {
            string[] hints = { "lid", "top", "cover", "крыш" };
            Transform best = null;
            void Walk(Transform t)
            {
                foreach (Transform c in t)
                {
                    string n = c.name.ToLowerInvariant();
                    foreach (var h in hints)
                    {
                        if (n.Contains(h)) { best = c; return; }
                    }
                    Walk(c);
                    if (best != null) return;
                }
            }
            Walk(root);
            return best;
        }

        private static Vector3 GuessLocalAxis(Transform dial)
        {
            // Pick the local axis whose world direction is closest to vertical — for typical
            // top-facing rings, that's the spin axis. For side-facing rings, switch to local Z.
            float dotY = Mathf.Abs(Vector3.Dot(dial.up, Vector3.up));
            float dotZ = Mathf.Abs(Vector3.Dot(dial.forward, Vector3.up));
            float dotX = Mathf.Abs(Vector3.Dot(dial.right, Vector3.up));
            if (dotY >= dotZ && dotY >= dotX) return Vector3.up;
            if (dotZ >= dotX) return Vector3.forward;
            return Vector3.right;
        }

        private static void SpawnNotePickupNear(GameObject root, ItemDef note)
        {
            if (note == null) return;
            // Avoid double-spawning.
            foreach (var existing in Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None))
            {
                var so = new SerializedObject(existing);
                var itemProp = so.FindProperty("_item");
                if (itemProp != null && itemProp.objectReferenceValue == note) return;
            }

            var pickup = new GameObject("NotePickup");
            pickup.transform.position = root.transform.position + root.transform.forward * 2f + Vector3.up * 0.1f;
            var col = pickup.AddComponent<BoxCollider>();
            col.size = new Vector3(0.3f, 0.05f, 0.3f);

            // Small paper visual (a flat quad-ish primitive).
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Paper";
            visual.transform.SetParent(pickup.transform, false);
            visual.transform.localScale = new Vector3(0.25f, 0.005f, 0.25f);
            Object.DestroyImmediate(visual.GetComponent<BoxCollider>());

            var p = pickup.AddComponent<PickupItem>();
            var pso = new SerializedObject(p);
            SetRef(pso, "_item", note);
            pso.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- folders / registry ---------------------------------------------

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project");
            EnsureFolder("Assets/_Project/Data");
            EnsureFolder(DataFolder);
            EnsureFolder(ResourcesFolder);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static ItemRegistry CreateOrLoadRegistry()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ItemRegistry>(RegistryPath);
            if (existing != null) return existing;
            var reg = ScriptableObject.CreateInstance<ItemRegistry>();
            AssetDatabase.CreateAsset(reg, RegistryPath);
            return reg;
        }

        private static T CreateOrLoadAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // ---- sample items + recipes -----------------------------------------

        private static void CreateSampleItems(ItemRegistry registry)
        {
            // Note text is authored in the asset file and not overwritten here on purpose:
            // ConfigureLockboxRoot no longer randomizes the code, so the lore-text in the
            // note stays the canonical clue (6 vampire houses → first letters → founding order).
            var note = MakeItem("Item_Note", "note_clue", "Старая записка",
                "Пожелтевший лист с записями о домах вампиров.",
                isKey: false, examine: "");
            var keyRoom = MakeItem("Item_KeyRoom1", "key_room1", "Старый ключ",
                "Тяжёлый бронзовый ключ. Выпал из руки поверженного вампира.",
                isKey: true, examine: "Кажется, он подходит к двери этой комнаты.");
            var potion = MakeItem("Item_Potion", EscapeQuestState.PotionItemId, "Зелье-противоядие",
                "Тёплая склянка из шкатулки. Внутри тёмная жидкость пахнет травами и серебром.",
                isKey: true, examine: "Противоядие. Если выпить — укус вампира не превратит вас в гуля.");
            potion.ConsumeOnUse = true;
            EditorUtility.SetDirty(potion);

            AddToRegistry(registry, note);
            AddToRegistry(registry, keyRoom);
            AddToRegistry(registry, potion);
        }

        private static ItemDef MakeItem(string fileName, string id, string display, string desc, bool isKey, string examine)
        {
            string path = DataFolder + "/" + fileName + ".asset";
            var item = CreateOrLoadAsset<ItemDef>(path);
            if (string.IsNullOrEmpty(item.Id)) item.Id = id;
            if (string.IsNullOrEmpty(item.DisplayNameFallback)) item.DisplayNameFallback = display;
            if (string.IsNullOrEmpty(item.DescriptionFallback)) item.DescriptionFallback = desc;
            if (string.IsNullOrEmpty(item.ExamineTextFallback)) item.ExamineTextFallback = examine;
            item.IsKeyItem = isKey;
            if (item.SlotSize <= 0) item.SlotSize = 1;
            if (item.MaxStack <= 0) item.MaxStack = 1;
            EditorUtility.SetDirty(item);
            return item;
        }

        private static void AddToRegistry(ItemRegistry registry, ItemDef def)
        {
            if (!registry.Items.Contains(def)) registry.Items.Add(def);
        }

        // ---- scene runtime ---------------------------------------------------

        private static void SpawnInventoryRuntime(ItemRegistry registry)
        {
            var existing = Object.FindAnyObjectByType<Inventory.Inventory>();
            if (existing != null)
            {
                Debug.Log("[InventorySetup] Inventory already in scene: " + existing.name);
                Selection.activeGameObject = existing.gameObject;
                return;
            }
            var go = new GameObject("Inventory");
            var inv = go.AddComponent<Inventory.Inventory>();
            // Assign registry via SerializedObject so the private field gets set.
            var so = new SerializedObject(inv);
            var prop = so.FindProperty("_registry");
            if (prop != null) prop.objectReferenceValue = registry;
            so.ApplyModifiedPropertiesWithoutUndo();
            Selection.activeGameObject = go;
        }

        // ---- inventory UI canvas --------------------------------------------

        // RE2-style palette
        private static readonly Color Bg          = new Color(0.04f, 0.04f, 0.05f, 0.94f);
        private static readonly Color Panel       = new Color(0.09f, 0.09f, 0.10f, 1f);
        private static readonly Color PanelEdge   = new Color(0.20f, 0.17f, 0.10f, 1f);
        private static readonly Color SlotBg      = new Color(0.12f, 0.12f, 0.13f, 1f);
        private static readonly Color SlotEdge    = new Color(0.30f, 0.26f, 0.16f, 1f);
        private static readonly Color KeyTint     = new Color(0.45f, 0.10f, 0.10f, 0.25f);
        private static readonly Color Accent     = new Color(0.95f, 0.78f, 0.35f, 1f);
        private static readonly Color TextMain   = new Color(0.92f, 0.90f, 0.84f, 1f);
        private static readonly Color TextDim    = new Color(0.65f, 0.62f, 0.55f, 1f);
        private static readonly Color BtnBg      = new Color(0.14f, 0.13f, 0.11f, 1f);

        private static void SpawnInventoryUI()
        {
            var existing = Object.FindAnyObjectByType<InventoryUI>();
            if (existing != null)
            {
                Debug.Log("[InventorySetup] InventoryUI уже есть, пересоздаю: " + existing.name);
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            // Canvas
            var canvasGo = new GameObject("InventoryCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(InventoryUI));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Dim background — fills the screen, swallows clicks behind UI.
            var panel = NewRect("Panel", canvasGo.transform);
            FullStretch(panel);
            var panelBg = panel.gameObject.AddComponent<Image>();
            panelBg.color = Bg;

            // ----- Layout, all panels pinned to TOP of the screen ----------------
            //
            //   ┌──────────── Header (top, full width) ────────────┐
            //   ├──────────────────────┬──────────────────────────┤
            //   │  MainPocket (left)   │  KeyItems (right top)    │
            //   │                      ├──────────────────────────┤
            //   │                      │  Detail   (right below)  │
            //   └──────────────────────┴──────────────────────────┘
            //
            // Reference resolution: 1920×1080.
            const float SideMargin = 60f;
            const float TopMargin = 30f;
            const float HeaderH = 60f;
            const float Gap = 16f;
            const float RightColW = 640f;
            const float KeyItemsH = 360f;
            const float DetailH = 560f;
            const float MainW = 1920f - SideMargin * 2f - RightColW - Gap;

            float headerY = -TopMargin;
            float rowY = -(TopMargin + HeaderH + Gap);
            float detailY = rowY - KeyItemsH - Gap;

            // Header
            var header = NewRect("Header", panel);
            AnchorTopLeft(header, SideMargin, headerY, 1920f - SideMargin * 2f, HeaderH);
            header.gameObject.AddComponent<Image>().color = Panel;
            AddBorder(header, PanelEdge, 2);

            var title = NewText("Title", header, "ИНВЕНТАРЬ", 32, TextAnchor.MiddleLeft);
            title.color = Accent;
            title.fontStyle = FontStyle.Bold;
            AnchorTopLeft(title.rectTransform, 24f, 0f, 600f, HeaderH);

            // SlotsCounter moved out of the header — sits ABOVE the inventory grid, next to
            // the КАРМАН title so the player reads "КАРМАН   СЛОТЫ 2/8" as a single header band.
            var slotsLabel = NewText("SlotsCounter", header, "", 20, TextAnchor.MiddleRight);
            slotsLabel.color = TextDim;
            slotsLabel.enabled = false; // header version no longer used; real one added inside MainPocket below.

            // Main pocket (left)
            var mainCol = NewRect("MainPocket", panel);
            AnchorTopLeft(mainCol, SideMargin, rowY, MainW, KeyItemsH + Gap + DetailH);
            mainCol.gameObject.AddComponent<Image>().color = Panel;
            AddBorder(mainCol, PanelEdge, 2);

            // КАРМАН label — sits flush at the top of the panel, well above the grid.
            var mainTitle = NewText("MainTitle", mainCol, "КАРМАН", 22, TextAnchor.UpperLeft);
            mainTitle.color = Accent;
            mainTitle.fontStyle = FontStyle.Bold;
            AnchorTopLeft(mainTitle.rectTransform, 20f, -10f, 400f, 32f);

            // СЛОТЫ counter, same band as КАРМАН but right-aligned. InventoryUI rebinds it
            // to keep "СЛОТЫ X / Y" live.
            slotsLabel.transform.SetParent(mainCol, false);
            slotsLabel.alignment = TextAnchor.UpperRight;
            slotsLabel.enabled = true;
            AnchorTopLeft(slotsLabel.rectTransform, 0f, -10f, MainW - 20f, 32f);

            // Grid pushed way down inside the panel so the КАРМАН/СЛОТЫ band sits in a clear
            // empty strip above the cells. offsetMax = -180 leaves ~170px of headroom.
            var mainGrid = NewRect("MainGrid", mainCol);
            mainGrid.anchorMin = new Vector2(0, 0);
            mainGrid.anchorMax = new Vector2(1, 1);
            mainGrid.pivot = new Vector2(0.5f, 0.5f);
            mainGrid.offsetMin = new Vector2(20, 20);
            mainGrid.offsetMax = new Vector2(-20, -180);
            var glm = mainGrid.gameObject.AddComponent<GridLayoutGroup>();
            glm.cellSize = new Vector2(150, 150);
            glm.spacing = new Vector2(8, 8);
            glm.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glm.constraintCount = 5;
            glm.padding = new RectOffset(4, 4, 4, 4);
            glm.childAlignment = TextAnchor.UpperLeft;

            // Key items (right, top)
            var keyCol = NewRect("KeyItems", panel);
            AnchorTopLeft(keyCol, SideMargin + MainW + Gap, rowY, RightColW, KeyItemsH);
            keyCol.gameObject.AddComponent<Image>().color = Panel;
            AddBorder(keyCol, PanelEdge, 2);

            var keyAccent = NewRect("KeyAccent", keyCol);
            keyAccent.anchorMin = new Vector2(0, 1);
            keyAccent.anchorMax = new Vector2(1, 1);
            keyAccent.pivot = new Vector2(0.5f, 1f);
            keyAccent.offsetMin = new Vector2(0, -4);
            keyAccent.offsetMax = new Vector2(0, 0);
            keyAccent.gameObject.AddComponent<Image>().color = Accent;

            var keyTitle = NewText("KeyTitle", keyCol, "КЛЮЧЕВЫЕ ПРЕДМЕТЫ", 18, TextAnchor.UpperLeft);
            keyTitle.color = TextMain;
            keyTitle.fontStyle = FontStyle.Bold;
            AnchorTopLeft(keyTitle.rectTransform, 20f, -20f, RightColW - 40f, 28f);

            var keyGrid = NewRect("KeyGrid", keyCol);
            keyGrid.anchorMin = new Vector2(0, 0);
            keyGrid.anchorMax = new Vector2(1, 1);
            keyGrid.pivot = new Vector2(0.5f, 0.5f);
            keyGrid.offsetMin = new Vector2(20, 20);
            keyGrid.offsetMax = new Vector2(-20, -90);
            var kgl = keyGrid.gameObject.AddComponent<GridLayoutGroup>();
            kgl.cellSize = new Vector2(96, 96);
            kgl.spacing = new Vector2(6, 6);
            kgl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            kgl.constraintCount = 5;
            kgl.padding = new RectOffset(4, 4, 4, 4);
            kgl.childAlignment = TextAnchor.UpperLeft;

            // Detail (right, below key items)
            var detail = NewRect("Detail", panel);
            AnchorTopLeft(detail, SideMargin + MainW + Gap, detailY, RightColW, DetailH);
            detail.gameObject.AddComponent<Image>().color = Panel;
            AddBorder(detail, PanelEdge, 2);

            var preview = NewRect("PreviewIcon", detail);
            AnchorTopLeft(preview, 24f, -24f, 120f, 120f);
            preview.gameObject.AddComponent<Image>().color = SlotBg;
            var previewIconRt = NewRect("Icon", preview);
            previewIconRt.anchorMin = Vector2.zero;
            previewIconRt.anchorMax = Vector2.one;
            previewIconRt.pivot = new Vector2(0.5f, 0.5f);
            previewIconRt.offsetMin = new Vector2(6, 6);
            previewIconRt.offsetMax = new Vector2(-6, -6);
            var previewImage = previewIconRt.gameObject.AddComponent<Image>();
            previewImage.preserveAspect = true;
            AddBorder(preview, SlotEdge, 1);

            var nameLabel = NewText("Name", detail, "", 24, TextAnchor.UpperLeft);
            nameLabel.color = Accent;
            nameLabel.fontStyle = FontStyle.Bold;
            AnchorTopLeft(nameLabel.rectTransform, 160f, -28f, RightColW - 180f, 36f);

            var divider = NewRect("Divider", detail);
            AnchorTopLeft(divider, 160f, -68f, RightColW - 180f, 1f);
            divider.gameObject.AddComponent<Image>().color = PanelEdge;

            var descLabel = NewText("Description", detail, "", 16, TextAnchor.UpperLeft);
            descLabel.color = TextMain;
            AnchorTopLeft(descLabel.rectTransform, 160f, -78f, RightColW - 180f, 90f);

            // Buttons row — fixed offset from the TOP of the detail panel.
            var btnRow = NewRect("Buttons", detail);
            AnchorTopLeft(btnRow, 24f, -180f, RightColW - 48f, 70f);
            var btnLayout = btnRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            btnLayout.spacing = 12;
            btnLayout.childForceExpandWidth = true;
            btnLayout.childAlignment = TextAnchor.MiddleCenter;

            var useBtn = NewButton("UseButton", btnRow, "ИСПОЛЬЗОВАТЬ");
            var combineBtn = NewButton("CombineButton", btnRow, "СОЕДИНИТЬ");
            var examineBtn = NewButton("ExamineButton", btnRow, "ОСМОТРЕТЬ");
            var discardBtn = NewButton("DiscardButton", btnRow, "ВЫБРОСИТЬ");

            var actionDivider = NewRect("ActionDivider", detail);
            AnchorTopLeft(actionDivider, 24f, -160f, RightColW - 48f, 1f);
            actionDivider.gameObject.AddComponent<Image>().color = PanelEdge;

            // Hint label sits inside the detail panel below the buttons. No separate footer
            // panel — fewer floating bands, keeps the layout glued to the top of the screen.
            var hint = NewText("Hint", detail, "TAB / ESC — закрыть   •   ЛКМ по предмету — выбрать", 14, TextAnchor.UpperCenter);
            hint.color = TextDim;
            AnchorTopLeft(hint.rectTransform, 24f, -270f, RightColW - 48f, 24f);

            // ----- Examine modal — fullscreen overlay (sits on top of everything) -----
            var examineRoot = NewRect("ExamineModal", panel);
            FullStretch(examineRoot);
            var exBg = examineRoot.gameObject.AddComponent<Image>();
            exBg.color = new Color(0f, 0f, 0f, 0.96f);

            // Bigger frame so multi-paragraph notes (vampire houses lore) fit fully.
            var examineFrame = NewRect("ExamineFrame", examineRoot);
            examineFrame.anchorMin = new Vector2(0.5f, 0.5f);
            examineFrame.anchorMax = new Vector2(0.5f, 0.5f);
            examineFrame.pivot = new Vector2(0.5f, 0.5f);
            examineFrame.anchoredPosition = Vector2.zero;
            examineFrame.sizeDelta = new Vector2(1200f, 880f);
            examineFrame.gameObject.AddComponent<Image>().color = Panel;
            AddBorder(examineFrame, Accent, 2);

            var examineIconRt = NewRect("ExamineIcon", examineFrame);
            // Small thumbnail in the corner — leaves the bulk of the frame for the text.
            examineIconRt.anchorMin = new Vector2(0f, 1f);
            examineIconRt.anchorMax = new Vector2(0f, 1f);
            examineIconRt.pivot = new Vector2(0f, 1f);
            examineIconRt.anchoredPosition = new Vector2(24f, -24f);
            examineIconRt.sizeDelta = new Vector2(180f, 180f);
            var examineImage = examineIconRt.gameObject.AddComponent<Image>();
            examineImage.preserveAspect = true;

            // Big readable text panel — left-aligned next to the thumbnail, fills the frame
            // down to the close button. Long lore notes (vampire houses) need this much room.
            var examineText = NewText("ExamineText", examineFrame, "", 20, TextAnchor.UpperLeft);
            examineText.color = TextMain;
            examineText.rectTransform.anchorMin = new Vector2(0f, 0f);
            examineText.rectTransform.anchorMax = new Vector2(1f, 1f);
            examineText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            examineText.rectTransform.offsetMin = new Vector2(224f, 100f);  // right of icon, above button
            examineText.rectTransform.offsetMax = new Vector2(-24f, -24f);
            examineText.horizontalOverflow = HorizontalWrapMode.Wrap;
            examineText.verticalOverflow = VerticalWrapMode.Overflow;

            var examineCloseBtn = NewButton("CloseButton", examineFrame, "ЗАКРЫТЬ");
            var closeRt = examineCloseBtn.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(0.5f, 0f);
            closeRt.anchorMax = new Vector2(0.5f, 0f);
            closeRt.pivot = new Vector2(0.5f, 0f);
            closeRt.anchoredPosition = new Vector2(0f, 24f);
            closeRt.sizeDelta = new Vector2(280f, 56f);
            examineRoot.gameObject.SetActive(false);

            // Slot prefab — we don't save to disk; we keep an instance as a template under canvas and reference it.
            // But Unity's GridLayoutGroup needs Instantiate() of a real prefab/asset. We create a hidden template
            // GameObject the InventoryUI can clone via Instantiate().
            var slotTemplate = BuildSlotTemplate(canvasGo.transform);

            // Wire InventoryUI fields via SerializedObject
            var ui = canvasGo.GetComponent<InventoryUI>();
            var so = new SerializedObject(ui);
            SetRef(so, "_slotPrefab", slotTemplate.GetComponent<InventorySlotUI>());
            SetRef(so, "_mainGrid", mainGrid);
            SetRef(so, "_keyItemGrid", keyGrid);
            SetRef(so, "_nameLabel", nameLabel);
            SetRef(so, "_descriptionLabel", descLabel);
            SetRef(so, "_previewIcon", previewImage);
            SetRef(so, "_useButton", useBtn);
            SetRef(so, "_combineButton", combineBtn);
            SetRef(so, "_examineButton", examineBtn);
            SetRef(so, "_discardButton", discardBtn);
            SetRef(so, "_examineRoot", examineRoot.gameObject);
            SetRef(so, "_examineText", examineText);
            SetRef(so, "_examineIcon", examineImage);
            SetRef(so, "_examineCloseButton", examineCloseBtn);
            SetRef(so, "_hintLabel", hint);
            SetRef(so, "_panelRoot", panel.gameObject);
            SetRef(so, "_slotsCounterLabel", slotsLabel);

            // Try to auto-bind ExplorationController if present.
            var player = Object.FindAnyObjectByType<ExplorationController>();
            if (player != null) SetRef(so, "_player", player);

            so.ApplyModifiedPropertiesWithoutUndo();

            // Hide the panel root by default (InventoryUI also does this in Awake at runtime).
            panel.gameObject.SetActive(false);

            Selection.activeGameObject = canvasGo;
            Debug.Log("[InventorySetup] InventoryCanvas создан. Жми Tab в Play Mode чтобы открыть.");
        }

        private static GameObject BuildSlotTemplate(Transform parent)
        {
            // Outer cell: solid background + thin edge border.
            var template = new GameObject("SlotTemplate", typeof(RectTransform), typeof(Image), typeof(InventorySlotUI));
            template.transform.SetParent(parent, false);
            template.SetActive(false);
            var rt = template.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(150, 150);
            var bg = template.GetComponent<Image>();
            bg.color = SlotBg;

            // Thin border (4 image strips) — gives that RE2 etched cell look.
            AddBorder((RectTransform)template.transform, SlotEdge, 1);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(template.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            Anchor(iconRt, new Vector2(0, 0), new Vector2(1, 1), new Vector2(10, 10), new Vector2(-20, -20));
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            var countGo = new GameObject("Count", typeof(RectTransform), typeof(Text));
            countGo.transform.SetParent(template.transform, false);
            var countRt = countGo.GetComponent<RectTransform>();
            Anchor(countRt, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-10, 8), new Vector2(50, 28));
            var countTxt = countGo.GetComponent<Text>();
            countTxt.alignment = TextAnchor.LowerRight;
            countTxt.fontSize = 20;
            countTxt.color = TextMain;
            countTxt.fontStyle = FontStyle.Bold;
            countTxt.raycastTarget = false;
            countTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Selection frame: a thicker accent outline that lights up when selected.
            var frame = new GameObject("SelectionFrame", typeof(RectTransform));
            frame.transform.SetParent(template.transform, false);
            var fRt = frame.GetComponent<RectTransform>();
            Anchor(fRt, new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            AddBorder(fRt, Accent, 3);
            frame.SetActive(false);

            var slot = template.GetComponent<InventorySlotUI>();
            var so = new SerializedObject(slot);
            SetRef(so, "_icon", iconImg);
            SetRef(so, "_countLabel", countTxt);
            SetRef(so, "_selectionFrame", frame);
            so.ApplyModifiedPropertiesWithoutUndo();

            return template;
        }

        /// <summary>
        /// Adds a 4-strip rectangular border inside <paramref name="rt"/>. Cheap, no texture.
        /// </summary>
        private static void AddBorder(RectTransform rt, Color color, float thickness)
        {
            void Strip(Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, string name)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(rt, false);
                var r = (RectTransform)go.transform;
                r.anchorMin = anchorMin;
                r.anchorMax = anchorMax;
                r.offsetMin = offsetMin;
                r.offsetMax = offsetMax;
                var img = go.GetComponent<Image>();
                img.color = color;
                img.raycastTarget = false;
            }
            Strip(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -thickness), new Vector2(0, 0), "Top");
            Strip(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, thickness), "Bottom");
            Strip(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(thickness, 0), "Left");
            Strip(new Vector2(1, 0), new Vector2(1, 1), new Vector2(-thickness, 0), new Vector2(0, 0), "Right");
        }

        // ---- sample rotary lockbox ------------------------------------------

        private static void CreateSampleLockbox()
        {
            var root = new GameObject("SampleRotaryLockbox");
            root.transform.position = new Vector3(0, 1, 0);

            // Body cube (visual only)
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(1.6f, 0.6f, 0.6f);
            Object.DestroyImmediate(body.GetComponent<BoxCollider>());

            // Collider on root so interaction works
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(1.6f, 0.6f, 0.6f);

            var puzzle = root.AddComponent<RotaryLockPuzzle>();

            // 4 dials in a row
            int dialCount = 4;
            string[] target = { "A", "B", "C", "D" };
            var dialsList = new System.Collections.Generic.List<RotaryDial>();
            for (int i = 0; i < dialCount; i++)
            {
                var dialGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                dialGo.name = "Dial_" + i;
                dialGo.transform.SetParent(root.transform, false);
                dialGo.transform.localPosition = new Vector3(-0.6f + i * 0.4f, 0.4f, 0);
                dialGo.transform.localRotation = Quaternion.Euler(0, 0, 90);
                dialGo.transform.localScale = new Vector3(0.25f, 0.05f, 0.25f);
                Object.DestroyImmediate(dialGo.GetComponent<CapsuleCollider>());

                var dial = dialGo.AddComponent<RotaryDial>();
                dial.Symbols = new[] { "A", "B", "C", "D", "E", "F", "G", "H" };
                dial.DegreesPerStep = 360f / dial.Symbols.Length;
                dialsList.Add(dial);
            }

            var so = new SerializedObject(puzzle);
            var dialsProp = so.FindProperty("_dials");
            dialsProp.arraySize = dialsList.Count;
            for (int i = 0; i < dialsList.Count; i++)
                dialsProp.GetArrayElementAtIndex(i).objectReferenceValue = dialsList[i];

            var codeProp = so.FindProperty("_targetCode");
            codeProp.arraySize = target.Length;
            for (int i = 0; i < target.Length; i++)
                codeProp.GetArrayElementAtIndex(i).stringValue = target[i];

            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log("[InventorySetup] Sample rotary lockbox создан. Target code: " + string.Join("", target));
        }

        // ---- ui helpers ------------------------------------------------------

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Text NewText(string name, Transform parent, string content, int fontSize, TextAnchor anchor, Vector2? anchored = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.text = content;
            t.fontSize = fontSize;
            t.alignment = anchor;
            t.color = Color.white;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            if (anchored.HasValue)
            {
                var rt = t.rectTransform;
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = anchored.Value;
                rt.sizeDelta = new Vector2(300, 30);
            }
            return t;
        }

        private static Button NewButton(string name, Transform parent, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.18f, 0.18f, 0.22f, 0.9f);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120, 44);

            var txtGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            txtGo.transform.SetParent(go.transform, false);
            var t = txtGo.GetComponent<Text>();
            t.text = label;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.fontSize = 16;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.raycastTarget = false;
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            return go.GetComponent<Button>();
        }

        /// <summary>
        /// Pin a RectTransform to the top-left of its parent. Offsets are absolute pixels in
        /// the reference resolution: x grows right, y grows DOWN from the top (so y is
        /// usually negative for items below the top edge).
        /// </summary>
        private static void AnchorTopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 anchored, Vector2 size)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            // Pivot: top-left for things anchored to the top, bottom-left otherwise.
            rt.pivot = new Vector2(0, 1);
            if (min.y == 0 && max.y == 0) rt.pivot = new Vector2(0, 0);
            if (min.x == 0.5f && max.x == 0.5f) rt.pivot = new Vector2(0.5f, rt.pivot.y);
            if (min.x == 1f && max.x == 1f) rt.pivot = new Vector2(1f, rt.pivot.y);
            if (min.y == 1f && max.y == 1f) rt.pivot = new Vector2(rt.pivot.x, 1f);
            if (min == Vector2.zero && max == Vector2.one)
            {
                rt.offsetMin = anchored;
                rt.offsetMax = anchored + size;
                return;
            }
            rt.anchoredPosition = anchored;
            rt.sizeDelta = size;
        }

        private static void FullStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void SetRef(SerializedObject so, string field, Object value)
        {
            var prop = so.FindProperty(field);
            if (prop != null) prop.objectReferenceValue = value;
        }
    }
}
#endif
