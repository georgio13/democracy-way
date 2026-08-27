#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DemocracyWay.Setup
{
    /// <summary>
    /// Every asset path the one-shot scaffolder reads or writes, in one place —
    /// so a rename can never leave half the builders pointing at a stale path.
    /// The Art/Audio paths must match the placeholder files already on disk.
    /// </summary>
    internal static class SetupPaths
    {
        // ── Art (placeholders already on disk) ──
        public const string CursorPng       = "Assets/Art/UI/Cursor.png";
        public const string MenuSmokePng    = "Assets/Art/UI/MenuSmoke.png";
        public const string AthenaStatuePng = "Assets/Art/UI/AthenaStatue.png";
        public const string FireflyPng      = "Assets/Art/UI/Firefly.png";
        public const string FireflyMat      = "Assets/Art/UI/Firefly.mat";
        public const string BgChapter01Png  = "Assets/Art/Backgrounds/BG_Chapter01.png";
        public const string HeroPortraitPng = "Assets/Art/Portraits/P_Hero.png";
        public const string Comic1Png       = "Assets/Art/Comic/Comic_1.png";
        public const string Comic2Png       = "Assets/Art/Comic/Comic_2.png";
        public const string Comic3Png       = "Assets/Art/Comic/Comic_3.png";

        // ── Audio (placeholders already on disk) ──
        public const string MainMenuMusicWav = "Assets/Audio/Music/MainMenuAmbient.wav";
        public const string ChapterMusicWav  = "Assets/Audio/Music/ChapterAmbient.wav";
        public const string UiHoverWav       = "Assets/Audio/SFX/UIHover.wav";
        public const string UiClickWav       = "Assets/Audio/SFX/UIClick.wav";
        public const string ComicBeatWav     = "Assets/Audio/SFX/ComicBeat.wav";

        // ── Fonts (already imported as TMP font assets) ──
        public const string FontRegularPath = "Assets/Fonts/IFKargoSans-Regular SDF.asset";
        public const string FontBoldPath    = "Assets/Fonts/IFKargoSans-Bold SDF.asset";

        // ── Data assets (created once, then user-owned) ──
        public const string DataFolder            = "Assets/Data";
        public const string CreationDbAsset       = "Assets/Data/CreationDatabase.asset";
        public const string IndicatorCatalogAsset = "Assets/Data/IndicatorCatalog.asset";
        public const string IntroComicAsset       = "Assets/Data/IntroComic.asset";
        public const string Chapter01DialogueAsset = "Assets/Data/Chapter01Dialogue.asset";
        public const string Chapter01Asset        = "Assets/Data/Chapter01.asset";
        public const string GameConfigAsset       = "Assets/Data/GameConfig.asset";

        // ── Prefabs ──
        public const string PrefabFolder   = "Assets/Prefabs";
        public const string UiPrefabFolder = "Assets/Prefabs/UI";
        public const string UiButtonPrefab             = UiPrefabFolder + "/UiButton.prefab";
        public const string ConfirmDialogPrefab        = UiPrefabFolder + "/ConfirmDialog.prefab";
        public const string SettingsPanelPrefab        = UiPrefabFolder + "/SettingsPanel.prefab";
        public const string SaveSlotRowPrefab          = UiPrefabFolder + "/SaveSlotRow.prefab";
        public const string SaveSlotPanelPrefab        = UiPrefabFolder + "/SaveSlotPanel.prefab";
        public const string PauseMenuPanelPrefab       = UiPrefabFolder + "/PauseMenuPanel.prefab";
        public const string CreationOptionButtonPrefab = UiPrefabFolder + "/CreationOptionButton.prefab";
        public const string DialogueChoiceButtonPrefab = UiPrefabFolder + "/DialogueChoiceButton.prefab";
        public const string DialoguePanelPrefab        = UiPrefabFolder + "/DialoguePanel.prefab";
        public const string IndicatorRowPrefab         = UiPrefabFolder + "/IndicatorRow.prefab";
        public const string TooltipViewPrefab          = UiPrefabFolder + "/TooltipView.prefab";
        public const string ComicPanelPrefab           = UiPrefabFolder + "/ComicPanel.prefab";
        public const string SystemsPrefab              = PrefabFolder + "/Systems.prefab";

        // ── Scenes (Boot must stay first in the build list) ──
        public const string SceneFolder = "Assets/Scenes";
        public const string BootScene              = SceneFolder + "/Boot.unity";
        public const string MainMenuScene          = SceneFolder + "/MainMenu.unity";
        public const string CharacterCreationScene = SceneFolder + "/CharacterCreation.unity";
        public const string ComicIntroScene        = SceneFolder + "/ComicIntro.unity";
        public const string Chapter01Scene         = SceneFolder + "/Chapter01.unity";
    }

    /// <summary>
    /// Shared plumbing for the one-shot scaffolder: skip-if-exists bookkeeping,
    /// SerializedObject setters that SHOUT when a field name does not exist
    /// (silent wiring misses were the old project's plague), and the factories
    /// (labels, cameras, canvases, widgets) every builder leans on.
    /// Everything here is editor-only and dies with Assets/Setup/ — no runtime
    /// code may reference it.
    /// </summary>
    internal static class SetupCommon
    {
        // ── Shared visual language (matches the muted marble/gold placeholders) ──
        public static readonly Color PanelBlack = new Color(0.00f, 0.00f, 0.00f, 0.90f);
        public static readonly Color PanelDim   = new Color(0.02f, 0.03f, 0.05f, 0.82f);
        public static readonly Color Gold       = new Color(0.85f, 0.75f, 0.40f, 1.00f);
        public static readonly Color Cream      = new Color(0.95f, 0.88f, 0.68f, 1.00f);
        public static readonly Color White      = Color.white;
        public static readonly Color Muted      = new Color(0.72f, 0.70f, 0.64f, 1.00f);
        public static readonly Vector2 ReferenceRes = new Vector2(1920, 1080);

        // ── Run bookkeeping ──

        private static readonly List<string> created = new List<string>();
        private static readonly List<string> skipped = new List<string>();

        private static TMP_FontAsset fontRegular;
        private static TMP_FontAsset fontBold;

        /// <summary>Fresh counters + font cache per run, so a re-run after a
        /// domain reload never reports stale numbers or holds dead objects.</summary>
        public static void ResetRun()
        {
            created.Clear();
            skipped.Clear();
            fontRegular = null;
            fontBold = null;
        }

        /// <summary>
        /// The one overwrite guard: an existing path means the user owns that
        /// asset now — log, count, and let the builder bail out. This is what
        /// makes the whole Setup safe to re-run after a partial first run.
        /// </summary>
        public static bool SkipIfExists(string path)
        {
            if (!System.IO.File.Exists(path)) return false;
            skipped.Add(path);
            Debug.Log($"[Setup] Υπάρχει ήδη — παραλείπεται: {path}");
            return true;
        }

        public static void MarkCreated(string path)
        {
            created.Add(path);
            Debug.Log($"[Setup] Δημιουργήθηκε: {path}");
        }

        /// <summary>Greek end-of-run summary, so the console tells the story
        /// even when Setup ran headless.</summary>
        public static void ReportSummary()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[Setup] Ολοκληρώθηκε — δημιουργήθηκαν {created.Count}, παραλείφθηκαν (υπήρχαν ήδη) {skipped.Count}.");
            if (created.Count > 0)
            {
                sb.AppendLine("Δημιουργήθηκαν:");
                foreach (var path in created) sb.AppendLine("  • " + path);
            }
            if (skipped.Count > 0)
            {
                sb.AppendLine("Παραλείφθηκαν:");
                foreach (var path in skipped) sb.AppendLine("  • " + path);
            }
            Debug.Log(sb.ToString());
        }

        // ── Folders / loading ──

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        public static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                Debug.LogError($"[Setup] Δεν βρέθηκε asset: {path} ({typeof(T).Name}) — κάποιο βήμα θα μείνει ασύνδετο.");
            return asset;
        }

        // ── Fonts ──

        public static TMP_FontAsset FontRegular
        {
            get
            {
                if (fontRegular == null) fontRegular = Load<TMP_FontAsset>(SetupPaths.FontRegularPath);
                return fontRegular;
            }
        }

        public static TMP_FontAsset FontBold
        {
            get
            {
                if (fontBold == null) fontBold = Load<TMP_FontAsset>(SetupPaths.FontBoldPath);
                return fontBold;
            }
        }

        // ── SerializedObject setters (ALL of them shout on a missing field) ──

        private static SerializedProperty Find(SerializedObject so, string propertyName, string context)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
                Debug.LogError($"[Setup] {context}: ΔΕΝ υπάρχει serialized πεδίο '{propertyName}' " +
                               $"στο {so.targetObject.GetType().Name} — το wiring χάθηκε, διόρθωσε το όνομα.");
            return prop;
        }

        /// <summary>
        /// Assigns an object reference and verifies it actually took. Goes
        /// through objectReferenceEntityIdValue because plain
        /// objectReferenceValue can silently refuse cross-assembly assets from
        /// editor scripts, serialising {fileID: 0} with no error — the exact
        /// class of silent miss this project must never repeat.
        /// </summary>
        public static void SetRef(SerializedObject so, string propertyName, Object value, string context)
        {
            var prop = Find(so, propertyName, context);
            if (prop == null) return;

            if (value == null)
            {
                prop.objectReferenceValue = null;
                Debug.LogWarning($"[Setup] {context}: το '{propertyName}' έμεινε κενό (λείπει το asset-πηγή).");
                return;
            }

            prop.objectReferenceEntityIdValue = value.GetEntityId();

            if (prop.objectReferenceValue == null)
                Debug.LogError($"[Setup] {context}: απέτυχε η ανάθεση '{value.name}' " +
                               $"({value.GetType().Name}) στο '{propertyName}'.");
        }

        public static void SetEnum(SerializedObject so, string propertyName, int enumValueIndex, string context)
        {
            var prop = Find(so, propertyName, context);
            if (prop != null) prop.enumValueIndex = enumValueIndex;
        }

        public static void SetStringList(SerializedObject so, string propertyName, IReadOnlyList<string> values, string context)
        {
            var prop = Find(so, propertyName, context);
            if (prop == null) return;
            prop.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                prop.GetArrayElementAtIndex(i).stringValue = values[i];
        }

        public static void SetFloat(SerializedObject so, string propertyName, float value, string context)
        {
            var prop = Find(so, propertyName, context);
            if (prop != null) prop.floatValue = value;
        }

        public static void SetBool(SerializedObject so, string propertyName, bool value, string context)
        {
            var prop = Find(so, propertyName, context);
            if (prop != null) prop.boolValue = value;
        }

        public static void SetString(SerializedObject so, string propertyName, string value, string context)
        {
            var prop = Find(so, propertyName, context);
            if (prop != null) prop.stringValue = value;
        }

        // ── Rect helpers ──

        public static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void AnchorPoint(RectTransform rect, Vector2 anchor, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        // ── Scene-level factories ──

        /// <summary>Solid-black orthographic camera, tagged MainCamera. Every
        /// scene gets its own; the AudioListener lives on the persistent
        /// AudioService instead, so cameras never carry one.</summary>
        public static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera));
            go.tag = "MainCamera";
            var cam = go.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            return cam;
        }

        /// <summary>EventSystem with the Input System module — the project
        /// polls Keyboard/Mouse directly but uGUI raycasting still needs this.</summary>
        public static GameObject CreateEventSystem()
        {
            return new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        public static void ConfigureScaler(CanvasScaler scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceRes;
            scaler.matchWidthOrHeight = 0.5f;
        }

        /// <summary>Screen Space - Overlay canvas with the project-wide
        /// 1920×1080 scaler and a GraphicRaycaster.</summary>
        public static Canvas CreateOverlayCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            ConfigureScaler(go.GetComponent<CanvasScaler>());
            return canvas;
        }

        // ── UI factories ──

        /// <summary>TMP label anchored to a normalised rect of its parent.
        /// Wrapping is opt-in because single-line labels clip more predictably.</summary>
        public static TextMeshProUGUI CreateLabel(
            Transform parent, string name, string text, float fontSize,
            Vector2 anchorMin, Vector2 anchorMax,
            TextAlignmentOptions alignment, Color color, bool bold = false, bool wrap = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            Anchor((RectTransform)go.transform, anchorMin, anchorMax);

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = alignment;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            var font = bold ? (FontBold != null ? FontBold : FontRegular) : FontRegular;
            if (font != null) tmp.font = font;
            tmp.raycastTarget = false;   // labels must never steal hover/clicks
            return tmp;
        }

        /// <summary>
        /// Instance of the shared UiButton prefab with its Greek label set.
        /// The label is authored here (edit time) because UiButton only exposes
        /// Text through its serialized label reference, which the prefab wires.
        /// </summary>
        public static GameObject InstantiateUiButton(
            GameObject uiButtonPrefab, Transform parent, string name, string labelText,
            float preferredHeight = 64f, float labelFontSize = -1f)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(uiButtonPrefab, parent);
            go.name = name;

            var button = go.GetComponent<DemocracyWay.UI.UiButton>();
            if (button != null) button.Text = labelText;
            else Debug.LogError($"[Setup] Το prefab UiButton δεν έχει component UiButton ({name}).");

            if (labelFontSize > 0f)
            {
                var label = go.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.fontSize = labelFontSize;
            }

            var layout = go.GetComponent<LayoutElement>();
            if (layout != null) layout.preferredHeight = preferredHeight;
            return go;
        }

        /// <summary>Vertical layout container the spawn-heavy views (slots,
        /// options, choices, indicators) parent their rows into.</summary>
        public static RectTransform CreateVerticalColumn(
            Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            float spacing, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            Anchor((RectTransform)go.transform, anchorMin, anchorMax);

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            // childControl* on: the group honours each child's LayoutElement
            // preferred size instead of leaving authored sizes to fight it.
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = alignment;
            return (RectTransform)go.transform;
        }

        /// <summary>Square toggle (box + gold checkmark) for the settings rows.</summary>
        public static Toggle CreateToggle(Transform parent)
        {
            var go = new GameObject("Toggle", typeof(RectTransform), typeof(Image), typeof(Toggle), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var layout = go.GetComponent<LayoutElement>();
            layout.preferredWidth = 44f;
            layout.preferredHeight = 36f;
            layout.flexibleWidth = 0f;

            var bg = go.GetComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.12f);

            var checkGO = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkGO.transform.SetParent(go.transform, false);
            Anchor((RectTransform)checkGO.transform, new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f));
            var check = checkGO.GetComponent<Image>();
            check.color = Gold;
            check.raycastTarget = false;

            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = bg;
            toggle.graphic = check;
            toggle.isOn = true;
            return toggle;
        }

        /// <summary>Horizontal 0–1 slider (track, gold fill, handle) — the
        /// range is part of the SettingsPanel contract, so it is authored here.</summary>
        public static Slider CreateSlider(Transform parent)
        {
            var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var layout = go.GetComponent<LayoutElement>();
            layout.preferredWidth = 360f;
            layout.preferredHeight = 36f;
            layout.flexibleWidth = 0f;

            var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGO.transform.SetParent(go.transform, false);
            Anchor((RectTransform)bgGO.transform, new Vector2(0f, 0.4f), new Vector2(1f, 0.6f));
            var bg = bgGO.GetComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.12f);
            bg.raycastTarget = false;

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            Anchor((RectTransform)fillArea.transform, new Vector2(0f, 0.4f), new Vector2(1f, 0.6f));

            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(fillArea.transform, false);
            StretchFull((RectTransform)fillGO.transform);
            var fill = fillGO.GetComponent<Image>();
            fill.color = Gold;
            fill.raycastTarget = false;

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            StretchFull((RectTransform)handleArea.transform);

            var handleGO = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGO.transform.SetParent(handleArea.transform, false);
            ((RectTransform)handleGO.transform).sizeDelta = new Vector2(18f, 34f);
            var handle = handleGO.GetComponent<Image>();
            handle.color = White;

            var slider = go.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;   // the SettingsPanel contract: 0–1, no scaling
            slider.value = 0.7f;
            slider.fillRect = (RectTransform)fillGO.transform;
            slider.handleRect = (RectTransform)handleGO.transform;
            slider.targetGraphic = handle;
            return slider;
        }

        /// <summary>
        /// TMP_Dropdown with a full, working template (viewport, content,
        /// item toggle) — a dropdown without one throws the moment it opens,
        /// and the old scaffolder shipped exactly that bug.
        /// </summary>
        public static TMP_Dropdown CreateTmpDropdown(Transform parent)
        {
            var go = new GameObject("Dropdown", typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var layout = go.GetComponent<LayoutElement>();
            layout.preferredWidth = 360f;
            layout.preferredHeight = 44f;
            layout.flexibleWidth = 0f;

            var bg = go.GetComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.10f);

            var dropdown = go.GetComponent<TMP_Dropdown>();
            dropdown.targetGraphic = bg;

            var caption = CreateLabel(go.transform, "Label", string.Empty, 24,
                new Vector2(0.04f, 0f), new Vector2(0.86f, 1f),
                TextAlignmentOptions.MidlineLeft, White);

            // "»" is the one glyph the project already assumes the font has
            // (DialogueRunner's advance hint) — reused so nothing new can be missing.
            CreateLabel(go.transform, "Arrow", "»", 22,
                new Vector2(0.88f, 0f), new Vector2(0.98f, 1f),
                TextAlignmentOptions.Midline, Muted);

            // ── Template (inactive until the dropdown opens it) ──
            var template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            template.transform.SetParent(go.transform, false);
            var templateRect = (RectTransform)template.transform;
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, 2f);
            templateRect.sizeDelta = new Vector2(0f, 220f);
            template.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.10f, 0.98f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(template.transform, false);
            StretchFull((RectTransform)viewport.transform);
            viewport.GetComponent<Image>().color = Color.white;
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 44f);

            var item = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            item.transform.SetParent(content.transform, false);
            var itemRect = (RectTransform)item.transform;
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.sizeDelta = new Vector2(0f, 44f);

            var itemBgGO = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
            itemBgGO.transform.SetParent(item.transform, false);
            StretchFull((RectTransform)itemBgGO.transform);
            var itemBg = itemBgGO.GetComponent<Image>();
            itemBg.color = new Color(1f, 1f, 1f, 0.04f);

            var itemCheckGO = new GameObject("Item Checkmark", typeof(RectTransform), typeof(Image));
            itemCheckGO.transform.SetParent(item.transform, false);
            var itemCheckRect = (RectTransform)itemCheckGO.transform;
            itemCheckRect.anchorMin = itemCheckRect.anchorMax = new Vector2(0f, 0.5f);
            itemCheckRect.sizeDelta = new Vector2(18f, 18f);
            itemCheckRect.anchoredPosition = new Vector2(18f, 0f);
            var itemCheck = itemCheckGO.GetComponent<Image>();
            itemCheck.color = Gold;
            itemCheck.raycastTarget = false;

            var itemLabel = CreateLabel(item.transform, "Item Label", string.Empty, 24,
                Vector2.zero, Vector2.one, TextAlignmentOptions.MidlineLeft, White);
            var itemLabelRect = (RectTransform)itemLabel.transform;
            itemLabelRect.offsetMin = new Vector2(36f, 2f);
            itemLabelRect.offsetMax = new Vector2(-10f, -2f);

            var itemToggle = item.GetComponent<Toggle>();
            itemToggle.targetGraphic = itemBg;
            itemToggle.graphic = itemCheck;
            itemToggle.isOn = true;

            var scroll = template.GetComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;

            template.SetActive(false);

            dropdown.template = templateRect;
            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            return dropdown;
        }

        /// <summary>Saves a freshly built hierarchy as a prefab and destroys
        /// the scene copy — the disk asset is the only thing later steps use.</summary>
        public static GameObject SaveNewPrefab(GameObject root, string prefabPath)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            MarkCreated(prefabPath);
            return prefab;
        }
    }
}
#endif
