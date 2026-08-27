#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.EditorTools
{
    using DemocracyWay.Framework;
    using DemocracyWay.Menu;

    // Targeted aliases into DemocracyWay.UI — a blanket `using DemocracyWay.UI;`
    // would collide with DemocracyWay.Menu.SettingsPanel.
    using CharacterCreationController = global::DemocracyWay.UI.CharacterCreationController;
    using ComicPlayer                 = global::DemocracyWay.UI.ComicPlayer;
    using DialoguePanel               = global::DemocracyWay.UI.DialoguePanel;
    using GameSceneController         = global::DemocracyWay.UI.GameSceneController;
    using IndicatorHud                = global::DemocracyWay.UI.IndicatorHud;
    using PrytanyHud                  = global::DemocracyWay.UI.PrytanyHud;
    using SaveSlotPanel               = global::DemocracyWay.UI.SaveSlotPanel;
    using SaveSlotRow                 = global::DemocracyWay.UI.SaveSlotRow;
    using CreationOptionButton        = global::DemocracyWay.UI.CreationOptionButton;
    using ComicSequence               = global::DemocracyWay.UI.ComicSequence;
    using CreationDatabase            = global::DemocracyWay.Core.CreationDatabase;
    using DialogueDatabase            = global::DemocracyWay.Dialogue.DialogueDatabase;

    /// <summary>
    /// Regenerates the whole playable project from code.
    ///
    ///   Prefabs   MenuButton, ChoiceButton, ConfirmDialog, SettingsPanel,
    ///             PauseMenuPanel, SaveSlotRow, CreationOption, IndicatorBar,
    ///             ComicPanel
    ///   Content   CreationDatabase, DialogueDatabase, IntroComic
    ///   Scenes    Bootstrap → MainMenu → CharacterCreation → ComicIntro → Game
    ///
    /// Everything it produces is a normal asset: re-run it to reset to a known
    /// good state, or edit the results by hand and never run it again. Missing
    /// art is filled in by the procedural generators in
    /// <c>DemocracyWayInit.AssetFallback.cs</c>, so the project is playable with
    /// zero hand-drawn assets.
    ///
    /// Run via menu: <b>Tools ▸ DemocracyWay ▸ Init</b>.
    /// </summary>
    public static partial class DemocracyWayInit
    {
        // ── Asset paths (single source of truth) ──
        private const string PrefabFolder     = "Assets/Prefabs/UI";
        private const string SceneFolder      = "Assets/Scenes";
        private const string BgMainMenuId     = "BG_MainMenu";
        private const string CursorPath       = "Assets/Art/UI/UI_Cursor.png";
        private const string AmbientMusicPath = "Assets/Audio/Music/A_Ambient_MainMenu.wav";
        private const string ButtonHoverPath  = "Assets/Audio/SFX/A_ButtonHover.wav";
        private const string ButtonClickPath  = "Assets/Audio/SFX/A_ButtonClick.wav";
        private const string InputActionsPath = "Assets/Input/DemocracyWayInput.inputactions";
        private const string KargoFontsFolder = "Assets/Fonts";

        // Prefab paths, so the scene builders can re-load each prefab from disk
        // instead of holding an instance across AssetDatabase imports.
        private const string MenuButtonPrefabPath     = PrefabFolder + "/MenuButton.prefab";
        private const string ChoiceButtonPrefabPath   = PrefabFolder + "/ChoiceButton.prefab";
        private const string ConfirmDialogPrefabPath  = PrefabFolder + "/ConfirmDialog.prefab";
        private const string SettingsPanelPrefabPath  = PrefabFolder + "/SettingsPanel.prefab";
        private const string PauseMenuPrefabPath      = PrefabFolder + "/PauseMenuPanel.prefab";
        private const string SaveSlotRowPrefabPath    = PrefabFolder + "/SaveSlotRow.prefab";
        private const string CreationOptionPrefabPath = PrefabFolder + "/CreationOption.prefab";
        private const string IndicatorBarPrefabPath   = PrefabFolder + "/IndicatorBar.prefab";
        private const string ComicPanelPrefabPath     = PrefabFolder + "/ComicPanel.prefab";

        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string MainMenuScenePath  = "Assets/Scenes/MainMenu.unity";
        private const string CreationScenePath  = "Assets/Scenes/CharacterCreation.unity";
        private const string ComicScenePath     = "Assets/Scenes/ComicIntro.unity";
        private const string GameScenePath      = "Assets/Scenes/Game.unity";

        // ── Shared visual language ──
        private static readonly Color PanelBlack   = new Color(0.00f, 0.00f, 0.00f, 0.90f);
        private static readonly Color PanelDim     = new Color(0.02f, 0.03f, 0.05f, 0.82f);
        private static readonly Color BorderGold   = new Color(0.85f, 0.75f, 0.40f, 1.00f);
        private static readonly Color AccentCream  = new Color(0.95f, 0.88f, 0.68f, 1.00f);
        private static readonly Color LabelWhite   = Color.white;
        private static readonly Color LabelMuted   = new Color(0.72f, 0.70f, 0.64f, 1.00f);
        private static readonly Vector2 ReferenceRes = new Vector2(1920, 1080);

        // Cached fonts, resolved once per Init run.
        private static TMP_FontAsset fontRegular;
        private static TMP_FontAsset fontBold;

        [MenuItem("Tools/DemocracyWay/Init")]
        public static void Run()
        {
            // In batch mode (CI, -executeMethod) DisplayDialog returns false,
            // which would silently skip the entire run — so only ask a human.
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog(
                "Οδός Δημοκρατίας — Init",
                "Θα δημιουργηθούν / αντικατασταθούν:\n\n" +
                "PREFABS\n" +
                "• MenuButton, ChoiceButton, ConfirmDialog\n" +
                "• SettingsPanel, PauseMenuPanel\n" +
                "• SaveSlotRow, CreationOption, IndicatorBar, ComicPanel\n\n" +
                "CONTENT\n" +
                "• CreationDatabase (10 φυλές × 3 τριττύες)\n" +
                "• DialogueDatabase (8 διάλογοι)\n" +
                "• IntroComic (6 καρέ)\n\n" +
                "ΣΚΗΝΕΣ\n" +
                "• Bootstrap, MainMenu, CharacterCreation, ComicIntro, Game\n\n" +
                "Τα Build Settings θα ανανεωθούν (5 σκηνές).\n\nΣυνέχεια;",
                "Ναι, δημιούργησε", "Ακύρωση"))
                return;

            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

            EnsureFolders();
            ResolveFonts();
            ApplyDefaultTmpFont();

            // ── 1. Prefabs ──
            var menuButton = CreateMenuButtonPrefab("MenuButton");
            CreateMenuButtonPrefab("ChoiceButton");
            var confirm  = CreateConfirmDialogPrefab(menuButton);
            var settings = CreateSettingsPanelPrefab(menuButton);
            CreatePauseMenuPrefab(menuButton, settings, confirm);
            CreateSaveSlotRowPrefab(menuButton);
            CreateCreationOptionPrefab();
            CreateIndicatorBarPrefab();
            CreateComicPanelPrefab();

            // ── 2. Placeholder artwork ──
            // The PNG generators call AssetDatabase.ImportAsset /
            // SaveAndReimport, and an import part-way through a run tears down
            // and reloads assets — which silently invalidates any object
            // reference already in hand. A throwaway pass creates every PNG
            // first; after the refresh below, the content builders take their
            // cached-sprite branch and import nothing at all.
            PregenerateArtwork();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ── 3. Content databases ──
            BuildCreationDatabase();
            BuildDialogueDatabase();
            BuildComicSequence();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ── 4. Scenes ──
            // Each builder loads the prefabs and content assets it needs by
            // itself, after it has created its scene. Passing them in from here
            // does not work: EditorSceneManager.NewScene unloads unreferenced
            // assets, so anything fetched before that point is already a
            // destroyed object by the time the builder assigns it.
            //
            // This pass is only a check that everything actually exists.
            var prefabs = LoadGeneratedPrefabs();
            bool contentPresent =
                Load<CreationDatabase>(CreationDbPath) != null &&
                Load<DialogueDatabase>(DialogueDbPath) != null &&
                Load<ComicSequence>(ComicSequencePath) != null;

            if (!prefabs.AllPresent || !contentPresent)
            {
                Debug.LogError("[Init] Aborted: not every asset could be loaded from disk. " +
                               "Check the warnings above for the missing paths.");
                if (!Application.isBatchMode) EditorUtility.DisplayDialog(
                    "Init — Σφάλμα",
                    "Δεν φορτώθηκαν όλα τα assets από τον δίσκο.\n" +
                    "Δες την Κονσόλα για το ποιο λείπει και ξανατρέξε το Init.",
                    "OK");
                return;
            }

            BuildBootstrapScene();
            BuildMainMenuScene();
            BuildCharacterCreationScene();
            BuildComicScene();
            BuildGameScene();

            RegisterScenesInBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorSceneManager.OpenScene(BootstrapScenePath);

            if (!Application.isBatchMode) EditorUtility.DisplayDialog(
                "Init — Ολοκληρώθηκε",
                "Όλα έτοιμα.\n\n" +
                "Άνοιξε τη σκηνή Bootstrap και πάτα Play.\n\n" +
                "Ροή: MainMenu → Νέο Παιχνίδι → επιλογή θέσης →\n" +
                "δημιουργία χαρακτήρα (6 βήματα) → comic → παιχνίδι.",
                "OK");
        }

        // ═════════════════════════════════════════════════════════════════════
        // FOLDERS / FONTS
        // ═════════════════════════════════════════════════════════════════════

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/UI");
            EnsureFolder("Assets/Content");
            EnsureFolder("Assets/Audio");
            EnsureFolder("Assets/Audio/Music");
            EnsureFolder("Assets/Audio/SFX");
            EnsureFolder("Assets/Scenes");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void ResolveFonts()
        {
            fontRegular = FindKargoFont("Regular");
            fontBold    = FindKargoFont("Bold");
            if (fontRegular == null)
                Debug.LogWarning($"[Init] Δεν βρέθηκε Kargo *Regular SDF font asset στο {KargoFontsFolder}. " +
                                 "Θα χρησιμοποιηθεί το προεπιλεγμένο TMP font.");
        }

        /// <summary>
        /// Finds the Kargo TMP font asset for a weight. Matches on the
        /// <c>-{weight}</c> suffix rather than a bare substring, so asking for
        /// "Bold" cannot return "IFKargoSans-Extrabold SDF". Globs the folder
        /// rather than hard-coding a filename, so a font rename doesn't silently
        /// drop every label back to the TMP default.
        /// </summary>
        private static TMP_FontAsset FindKargoFont(string weight)
        {
            var guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { KargoFontsFolder });
            TMP_FontAsset fallback = null;

            foreach (var guid in guids)
            {
                var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;

                if (asset.name.IndexOf("Kargo", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (asset.name.IndexOf($"-{weight}", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return asset;

                if (fallback == null) fallback = asset;
            }
            return fallback;
        }

        /// <summary>Points TMP's default font at the resolved regular weight so
        /// any TextMeshProUGUI created without an explicit font still renders
        /// Greek polytonic correctly.</summary>
        private static void ApplyDefaultTmpFont()
        {
            if (fontRegular == null) return;
            var settings = TMP_Settings.instance;
            if (settings == null) return;

            var so = new SerializedObject(settings);
            var prop = so.FindProperty("m_defaultFontAsset");
            if (prop == null) return;
            prop.objectReferenceValue = fontRegular;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        // ═════════════════════════════════════════════════════════════════════
        // PREFAB BUILDERS
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Transparent button with a gold Border child (alpha 0 at rest)
        /// and a TMP Label. Used for both MenuButton and ChoiceButton.</summary>
        private static GameObject CreateMenuButtonPrefab(string name)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(480, 64);

            var hit = root.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            var borderGO = new GameObject("Border", typeof(RectTransform), typeof(Image));
            borderGO.transform.SetParent(root.transform, false);
            StretchFull(borderGO.GetComponent<RectTransform>());
            var borderImg = borderGO.GetComponent<Image>();
            borderImg.color = new Color(BorderGold.r, BorderGold.g, BorderGold.b, 0f);
            borderImg.type = Image.Type.Sliced;
            borderImg.raycastTarget = false;

            // Inner cut-out turns the solid border image into an outline.
            var inner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            inner.transform.SetParent(borderGO.transform, false);
            var innerRect = inner.GetComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(2f, 2f);
            innerRect.offsetMax = new Vector2(-2f, -2f);
            var innerImg = inner.GetComponent<Image>();
            innerImg.color = new Color(0f, 0f, 0f, 0f);
            innerImg.raycastTarget = false;

            var labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGO.transform.SetParent(root.transform, false);
            StretchFull(labelGO.GetComponent<RectTransform>());
            var tmp = labelGO.GetComponent<TextMeshProUGUI>();
            tmp.text = name;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 32;
            tmp.color = LabelWhite;
            tmp.raycastTarget = false;
            if (fontRegular != null) tmp.font = fontRegular;

            // Every vertical layout in the project controls child height, so a
            // button dropped straight into one (dialogue choices, for instance)
            // needs a preferred height of its own or it collapses to zero.
            root.AddComponent<LayoutElement>().preferredHeight = 64;

            var menuBtn = root.AddComponent<MenuButton>();
            var so = new SerializedObject(menuBtn);
            SetRef(so, "border", borderImg, "MenuButtonPrefab");
            SetRef(so, "label", tmp, "MenuButtonPrefab");
            SetRef(so, "hitTarget", hit, "MenuButtonPrefab");
            so.FindProperty("labelDisabledColor").colorValue = new Color(0.32f, 0.30f, 0.26f, 1f);
            so.ApplyModifiedPropertiesWithoutUndo();

            return SaveAndCleanup(root, $"{PrefabFolder}/{name}.prefab");
        }

        private static GameObject CreateConfirmDialogPrefab(GameObject menuButtonPrefab)
        {
            var root = new GameObject("ConfirmDialog", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            StretchFull(root.GetComponent<RectTransform>());
            var bg = root.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.75f);
            bg.raycastTarget = true;
            var group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(root.transform, false);
            var boxRect = box.GetComponent<RectTransform>();
            boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(820, 320);
            box.GetComponent<Image>().color = PanelBlack;

            var promptGO = new GameObject("Prompt", typeof(RectTransform), typeof(TextMeshProUGUI));
            promptGO.transform.SetParent(box.transform, false);
            var promptRect = promptGO.GetComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.06f, 0.42f);
            promptRect.anchorMax = new Vector2(0.94f, 0.90f);
            promptRect.offsetMin = promptRect.offsetMax = Vector2.zero;
            var promptTmp = promptGO.GetComponent<TextMeshProUGUI>();
            promptTmp.text = "…";
            promptTmp.alignment = TextAlignmentOptions.Center;
            promptTmp.fontSize = 30;
            promptTmp.color = LabelWhite;
            if (fontRegular != null) promptTmp.font = fontRegular;

            var yes = (GameObject)PrefabUtility.InstantiatePrefab(menuButtonPrefab, box.transform);
            yes.name = "YesButton";
            AnchorAt(yes.GetComponent<RectTransform>(), new Vector2(0.30f, 0.20f), new Vector2(260, 64));

            var no = (GameObject)PrefabUtility.InstantiatePrefab(menuButtonPrefab, box.transform);
            no.name = "NoButton";
            AnchorAt(no.GetComponent<RectTransform>(), new Vector2(0.70f, 0.20f), new Vector2(260, 64));

            var dialog = root.AddComponent<ConfirmDialog>();
            var so = new SerializedObject(dialog);
            SetRef(so, "rootGroup", group, "ConfirmDialogPrefab");
            SetRef(so, "promptText", promptTmp, "ConfirmDialogPrefab");
            SetRef(so, "yesButton", yes.GetComponent<MenuButton>(), "ConfirmDialogPrefab");
            SetRef(so, "noButton", no.GetComponent<MenuButton>(), "ConfirmDialogPrefab");
            so.ApplyModifiedPropertiesWithoutUndo();

            return SaveAndCleanup(root, $"{PrefabFolder}/ConfirmDialog.prefab");
        }

        private static GameObject CreateSettingsPanelPrefab(GameObject menuButtonPrefab)
        {
            var root = new GameObject("SettingsPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            StretchFull(root.GetComponent<RectTransform>());
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);
            var group = root.GetComponent<CanvasGroup>();

            var col = new GameObject("Column", typeof(RectTransform), typeof(VerticalLayoutGroup));
            col.transform.SetParent(root.transform, false);
            var colRect = col.GetComponent<RectTransform>();
            colRect.anchorMin = new Vector2(0.30f, 0.15f);
            colRect.anchorMax = new Vector2(0.70f, 0.88f);
            colRect.offsetMin = colRect.offsetMax = Vector2.zero;
            var layout = col.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 18;
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;

            AddSectionTitle(col.transform, "ΡΥΘΜΙΣΕΙΣ", 44, fontBold ?? fontRegular);

            var fullscreen = AddToggleRow(col.transform, "Πλήρης οθόνη");
            var resolution = AddDropdownRow(col.transform, "Ανάλυσις");
            var music      = AddSliderRow(col.transform, "Μουσική");
            var sfx        = AddSliderRow(col.transform, "Εφέ");
            var dialogue   = AddSliderRow(col.transform, "Διάλογοι");

            var back = (GameObject)PrefabUtility.InstantiatePrefab(menuButtonPrefab, col.transform);
            back.name = "BackButton";
            back.GetComponent<MenuButton>().Text = "Πίσω";
            back.AddComponent<LayoutElement>().preferredHeight = 72;

            var panel = root.AddComponent<SettingsPanel>();
            var so = new SerializedObject(panel);
            SetRef(so, "rootGroup", group, "SettingsPanelPrefab");
            SetRef(so, "fullscreenToggle", fullscreen.toggle, "SettingsPanelPrefab");
            SetRef(so, "resolutionDropdown", resolution.dropdown, "SettingsPanelPrefab");
            SetRef(so, "musicVolumeSlider", music.slider, "SettingsPanelPrefab");
            SetRef(so, "sfxVolumeSlider", sfx.slider, "SettingsPanelPrefab");
            SetRef(so, "dialogueVolumeSlider", dialogue.slider, "SettingsPanelPrefab");
            SetRef(so, "backButton", back.GetComponent<MenuButton>(), "SettingsPanelPrefab");
            SetRef(so, "musicVolumeLabel", music.valueLabel, "SettingsPanelPrefab");
            SetRef(so, "sfxVolumeLabel", sfx.valueLabel, "SettingsPanelPrefab");
            SetRef(so, "dialogueVolumeLabel", dialogue.valueLabel, "SettingsPanelPrefab");
            so.ApplyModifiedPropertiesWithoutUndo();

            return SaveAndCleanup(root, $"{PrefabFolder}/SettingsPanel.prefab");
        }

        private static GameObject CreatePauseMenuPrefab(GameObject menuButtonPrefab, GameObject settingsPanelPrefab, GameObject confirmDialogPrefab)
        {
            var root = new GameObject("PauseMenuPanel",
                typeof(RectTransform), typeof(CanvasGroup),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            StretchFull(root.GetComponent<RectTransform>());

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500; // above scene UI, below the transition overlay (9999)
            ConfigureScaler(root.GetComponent<CanvasScaler>());

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(root.transform, false);
            StretchFull(bg.GetComponent<RectTransform>());
            bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.94f);

            CreateLabel(root.transform, "Title", "Μενού Παύσεως", 72,
                new Vector2(0f, 0.78f), new Vector2(1f, 0.90f),
                TextAlignmentOptions.Center, LabelWhite, fontBold ?? fontRegular);

            var column = new GameObject("ButtonColumn", typeof(RectTransform), typeof(VerticalLayoutGroup));
            column.transform.SetParent(root.transform, false);
            var colRect = column.GetComponent<RectTransform>();
            colRect.anchorMin = new Vector2(0.35f, 0.20f);
            colRect.anchorMax = new Vector2(0.65f, 0.72f);
            colRect.offsetMin = colRect.offsetMax = Vector2.zero;
            var layout = column.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 20;
            layout.padding = new RectOffset(0, 0, 10, 10);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;

            var resume   = InstantiateLabeledButton(menuButtonPrefab, column.transform, "ResumeButton",   "Συνέχεια Παιχνιδιού");
            var settings = InstantiateLabeledButton(menuButtonPrefab, column.transform, "SettingsButton", "Ρυθμίσεις");
            var mainMenu = InstantiateLabeledButton(menuButtonPrefab, column.transform, "MainMenuButton", "Έξοδος στο Κύριο Μενού");
            var quit     = InstantiateLabeledButton(menuButtonPrefab, column.transform, "QuitButton",     "Έξοδος Παιχνιδιού");

            var settingsPanel = (GameObject)PrefabUtility.InstantiatePrefab(settingsPanelPrefab, root.transform);
            settingsPanel.name = "SettingsPanel";
            settingsPanel.SetActive(false);

            var confirm = (GameObject)PrefabUtility.InstantiatePrefab(confirmDialogPrefab, root.transform);
            confirm.name = "ConfirmDialog";
            confirm.SetActive(false);

            var pauseMenu = root.AddComponent<PauseMenuPanel>();
            var so = new SerializedObject(pauseMenu);
            SetRef(so, "rootGroup", root.GetComponent<CanvasGroup>(), "PauseMenuPrefab");
            SetRef(so, "resumeButton", resume.GetComponent<MenuButton>(), "PauseMenuPrefab");
            SetRef(so, "settingsButton", settings.GetComponent<MenuButton>(), "PauseMenuPrefab");
            SetRef(so, "mainMenuButton", mainMenu.GetComponent<MenuButton>(), "PauseMenuPrefab");
            SetRef(so, "quitButton", quit.GetComponent<MenuButton>(), "PauseMenuPrefab");
            SetRef(so, "settingsPanel", settingsPanel.GetComponent<SettingsPanel>(), "PauseMenuPrefab");
            SetRef(so, "confirmDialog", confirm.GetComponent<ConfirmDialog>(), "PauseMenuPrefab");
            so.ApplyModifiedPropertiesWithoutUndo();

            return SaveAndCleanup(root, $"{PrefabFolder}/PauseMenuPanel.prefab");
        }

        /// <summary>One row of the save browser: slot number, citizen summary,
        /// metadata line, and Load / Delete buttons.</summary>
        private static GameObject CreateSaveSlotRowPrefab(GameObject menuButtonPrefab)
        {
            var root = new GameObject("SaveSlotRow", typeof(RectTransform), typeof(Image));
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1100, 130);
            root.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);
            root.AddComponent<LayoutElement>().preferredHeight = 130;

            var slotLabel = CreateLabel(root.transform, "SlotLabel", "Θέση 1", 30,
                new Vector2(0.02f, 0.50f), new Vector2(0.16f, 0.92f),
                TextAlignmentOptions.MidlineLeft, AccentCream, fontBold ?? fontRegular);

            var summary = CreateLabel(root.transform, "Summary", "Κενή θέση", 28,
                new Vector2(0.16f, 0.50f), new Vector2(0.72f, 0.92f),
                TextAlignmentOptions.MidlineLeft, LabelWhite, fontRegular);

            var meta = CreateLabel(root.transform, "Meta", string.Empty, 20,
                new Vector2(0.16f, 0.10f), new Vector2(0.72f, 0.48f),
                TextAlignmentOptions.MidlineLeft, LabelMuted, fontRegular);

            var select = (GameObject)PrefabUtility.InstantiatePrefab(menuButtonPrefab, root.transform);
            select.name = "SelectButton";
            var selectRect = select.GetComponent<RectTransform>();
            selectRect.anchorMin = new Vector2(0.73f, 0.18f);
            selectRect.anchorMax = new Vector2(0.87f, 0.82f);
            selectRect.offsetMin = selectRect.offsetMax = Vector2.zero;
            select.GetComponent<MenuButton>().Text = "Φόρτωση";

            var delete = (GameObject)PrefabUtility.InstantiatePrefab(menuButtonPrefab, root.transform);
            delete.name = "DeleteButton";
            var deleteRect = delete.GetComponent<RectTransform>();
            deleteRect.anchorMin = new Vector2(0.875f, 0.18f);
            deleteRect.anchorMax = new Vector2(0.985f, 0.82f);
            deleteRect.offsetMin = deleteRect.offsetMax = Vector2.zero;
            delete.GetComponent<MenuButton>().Text = "Διαγραφή";

            var row = root.AddComponent<SaveSlotRow>();
            var so = new SerializedObject(row);
            SetRef(so, "slotLabel", slotLabel, "SaveSlotRowPrefab");
            SetRef(so, "summaryLabel", summary, "SaveSlotRowPrefab");
            SetRef(so, "metaLabel", meta, "SaveSlotRowPrefab");
            SetRef(so, "selectButton", select.GetComponent<MenuButton>(), "SaveSlotRowPrefab");
            SetRef(so, "deleteButton", delete.GetComponent<MenuButton>(), "SaveSlotRowPrefab");
            so.ApplyModifiedPropertiesWithoutUndo();

            return SaveAndCleanup(root, $"{PrefabFolder}/SaveSlotRow.prefab");
        }

        /// <summary>One entry in the character-creation option column.</summary>
        private static GameObject CreateCreationOptionPrefab()
        {
            var root = new GameObject("CreationOption", typeof(RectTransform), typeof(Image));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(620, 96);
            var bg = root.GetComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.04f);
            bg.raycastTarget = true;
            root.AddComponent<LayoutElement>().preferredHeight = 96;

            // Gold bar on the left edge, shown only while selected.
            var bar = new GameObject("SelectionBar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(root.transform, false);
            var barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0.12f);
            barRect.anchorMax = new Vector2(0.008f, 0.88f);
            barRect.offsetMin = barRect.offsetMax = Vector2.zero;
            var barImg = bar.GetComponent<Image>();
            barImg.color = BorderGold;
            barImg.raycastTarget = false;
            bar.SetActive(false);

            var title = CreateLabel(root.transform, "Title", "Επιλογή", 30,
                new Vector2(0.04f, 0.46f), new Vector2(0.98f, 0.94f),
                TextAlignmentOptions.MidlineLeft, new Color(0.88f, 0.86f, 0.80f), fontBold ?? fontRegular);
            title.raycastTarget = false;

            var subtitle = CreateLabel(root.transform, "Subtitle", string.Empty, 20,
                new Vector2(0.04f, 0.08f), new Vector2(0.98f, 0.44f),
                TextAlignmentOptions.MidlineLeft, LabelMuted, fontRegular);
            subtitle.raycastTarget = false;

            var button = root.AddComponent<CreationOptionButton>();
            var so = new SerializedObject(button);
            SetRef(so, "titleLabel", title, "CreationOptionPrefab");
            SetRef(so, "subtitleLabel", subtitle, "CreationOptionPrefab");
            SetRef(so, "background", bg, "CreationOptionPrefab");
            SetRef(so, "selectionBar", barImg, "CreationOptionPrefab");
            so.ApplyModifiedPropertiesWithoutUndo();

            return SaveAndCleanup(root, $"{PrefabFolder}/CreationOption.prefab");
        }

        /// <summary>One indicator bar: name, filled track, numeric value.
        /// Child names are load-bearing — IndicatorHud looks up "Name",
        /// "Value" and "Track/Fill" by path.</summary>
        private static GameObject CreateIndicatorBarPrefab()
        {
            var root = new GameObject("IndicatorBar", typeof(RectTransform));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(420, 74);
            root.AddComponent<LayoutElement>().preferredHeight = 74;

            var name = CreateLabel(root.transform, "Name", "Δείκτης", 24,
                new Vector2(0f, 0.48f), new Vector2(0.78f, 1f),
                TextAlignmentOptions.MidlineLeft, LabelWhite, fontRegular);
            name.raycastTarget = false;

            var value = CreateLabel(root.transform, "Value", "50", 24,
                new Vector2(0.78f, 0.48f), new Vector2(1f, 1f),
                TextAlignmentOptions.MidlineRight, AccentCream, fontBold ?? fontRegular);
            value.raycastTarget = false;

            var track = new GameObject("Track", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(root.transform, false);
            var trackRect = track.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0f, 0.12f);
            trackRect.anchorMax = new Vector2(1f, 0.42f);
            trackRect.offsetMin = trackRect.offsetMax = Vector2.zero;
            var trackImg = track.GetComponent<Image>();
            trackImg.color = new Color(1f, 1f, 1f, 0.10f);
            trackImg.raycastTarget = false;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(track.transform, false);
            StretchFull(fill.GetComponent<RectTransform>());
            var fillImg = fill.GetComponent<Image>();
            fillImg.color = BorderGold;
            fillImg.raycastTarget = false;
            // Horizontal fill driven by IndicatorHud via fillAmount.
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 0.5f;

            return SaveAndCleanup(root, $"{PrefabFolder}/IndicatorBar.prefab");
        }

        /// <summary>One comic frame. Child names "Art" and "Caption" are
        /// load-bearing — ComicPlayer looks them up by name.</summary>
        private static GameObject CreateComicPanelPrefab()
        {
            var root = new GameObject("ComicPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(560, 380);
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            root.GetComponent<CanvasGroup>().alpha = 0f;

            var art = new GameObject("Art", typeof(RectTransform), typeof(Image));
            art.transform.SetParent(root.transform, false);
            var artRect = art.GetComponent<RectTransform>();
            artRect.anchorMin = new Vector2(0.02f, 0.28f);
            artRect.anchorMax = new Vector2(0.98f, 0.97f);
            artRect.offsetMin = artRect.offsetMax = Vector2.zero;
            var artImg = art.GetComponent<Image>();
            artImg.preserveAspect = true;
            artImg.raycastTarget = false;

            var caption = CreateLabel(root.transform, "Caption", string.Empty, 21,
                new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.26f),
                TextAlignmentOptions.Top, new Color(0.88f, 0.85f, 0.78f), fontRegular);
            caption.raycastTarget = false;
            caption.textWrappingMode = TextWrappingModes.Normal;

            return SaveAndCleanup(root, $"{PrefabFolder}/ComicPanel.prefab");
        }

        // ═════════════════════════════════════════════════════════════════════
        // ASSET RE-LOADING
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The prefabs Init just wrote, re-loaded from disk.
        ///
        /// Holding on to the objects returned by the Create*Prefab calls is not
        /// safe across the artwork and content phases: those import assets, and
        /// an import invalidates live instances. Re-loading by path right before
        /// the scenes are built guarantees each reference actually serialises.
        /// </summary>
        private readonly struct GeneratedPrefabs
        {
            public readonly GameObject MenuButton;
            public readonly GameObject ChoiceButton;
            public readonly GameObject ConfirmDialog;
            public readonly GameObject SettingsPanel;
            public readonly GameObject PauseMenu;
            public readonly GameObject SaveSlotRow;
            public readonly GameObject CreationOption;
            public readonly GameObject IndicatorBar;
            public readonly GameObject ComicPanel;

            public GeneratedPrefabs(
                GameObject menuButton, GameObject choiceButton, GameObject confirmDialog,
                GameObject settingsPanel, GameObject pauseMenu, GameObject saveSlotRow,
                GameObject creationOption, GameObject indicatorBar, GameObject comicPanel)
            {
                MenuButton = menuButton;
                ChoiceButton = choiceButton;
                ConfirmDialog = confirmDialog;
                SettingsPanel = settingsPanel;
                PauseMenu = pauseMenu;
                SaveSlotRow = saveSlotRow;
                CreationOption = creationOption;
                IndicatorBar = indicatorBar;
                ComicPanel = comicPanel;
            }

            /// <summary>True when every prefab loaded.</summary>
            public bool AllPresent =>
                MenuButton != null && ChoiceButton != null && ConfirmDialog != null &&
                SettingsPanel != null && PauseMenu != null && SaveSlotRow != null &&
                CreationOption != null && IndicatorBar != null && ComicPanel != null;
        }

        private static GeneratedPrefabs LoadGeneratedPrefabs() => new GeneratedPrefabs(
            Load<GameObject>(MenuButtonPrefabPath),
            Load<GameObject>(ChoiceButtonPrefabPath),
            Load<GameObject>(ConfirmDialogPrefabPath),
            Load<GameObject>(SettingsPanelPrefabPath),
            Load<GameObject>(PauseMenuPrefabPath),
            Load<GameObject>(SaveSlotRowPrefabPath),
            Load<GameObject>(CreationOptionPrefabPath),
            Load<GameObject>(IndicatorBarPrefabPath),
            Load<GameObject>(ComicPanelPrefabPath));

        /// <summary>
        /// Runs every content builder once and throws the result away.
        ///
        /// The only thing that matters is the side effect: each builder asks
        /// LoadBgOrFallback / LoadCharOrFallback for its artwork, which writes
        /// and imports a placeholder PNG when none exists yet. Doing that here,
        /// as one batch followed by a single refresh, means the real content
        /// pass finds every PNG already on disk and imports nothing — so
        /// nothing it builds gets invalidated part-way through.
        /// </summary>
        private static void PregenerateArtwork()
        {
            BuildGenders();
            BuildTribes();
            BuildTrittyes();
            BuildWealthClasses();
            BuildPeriods();
            BuildOccupations();
            BuildComicPanels();
            Debug.Log("[Init] Placeholder artwork generated.");
        }


        // ═════════════════════════════════════════════════════════════════════
        // BUILD SETTINGS
        // ═════════════════════════════════════════════════════════════════════

        private static void RegisterScenesInBuildSettings()
        {
            // Bootstrap must be index 0 — it is the entry point that spins up
            // every persistent singleton before chaining into MainMenu.
            string[] paths =
            {
                BootstrapScenePath,
                MainMenuScenePath,
                CreationScenePath,
                ComicScenePath,
                GameScenePath
            };

            var list = new EditorBuildSettingsScene[paths.Length];
            for (int i = 0; i < paths.Length; i++)
                list[i] = new EditorBuildSettingsScene(paths[i], true);

            EditorBuildSettings.scenes = list;
            Debug.Log($"[Init] Build Settings: {paths.Length} σκηνές.");
        }

        /// <summary>
        /// Assigns an object reference onto a serialized field, and shouts if it
        /// does not take.
        ///
        /// Goes through objectReferenceEntityIdValue rather than the more
        /// obvious objectReferenceValue: from an editor script the latter
        /// silently refuses assets whose type lives in Assembly-CSharp (our
        /// ScriptableObjects), leaving the field null with no error at all —
        /// the scene then serialises {fileID: 0} and the game comes up with
        /// empty databases. Assignment by instance id is accepted, and the
        /// read-back below turns any future failure into a visible error
        /// instead of a blank screen.
        /// </summary>
        private static void SetRef(SerializedObject so, string propertyName, Object value, string context)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogError($"[Init] {context}: no serialized field named '{propertyName}'.");
                return;
            }

            if (value == null)
            {
                prop.objectReferenceValue = null;
                Debug.LogWarning($"[Init] {context}: '{propertyName}' left empty (source asset missing).");
                return;
            }

            prop.objectReferenceEntityIdValue = value.GetEntityId();

            if (prop.objectReferenceValue == null)
                Debug.LogError(
                    $"[Init] {context}: could not assign '{value.name}' " +
                    $"({value.GetType().Name}) to '{propertyName}'.");
        }


        // ═════════════════════════════════════════════════════════════════════
        // UI HELPERS
        // ═════════════════════════════════════════════════════════════════════

        private static GameObject SaveAndCleanup(GameObject root, string prefabPath)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[Init] Prefab: {prefabPath}");
            return prefab;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void AnchorAt(RectTransform rect, Vector2 anchor, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        private static void ConfigureScaler(CanvasScaler scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceRes;
            scaler.matchWidthOrHeight = 0.5f;
        }

        /// <summary>Creates a TMP label anchored to a normalised rect of its parent.</summary>
        private static TextMeshProUGUI CreateLabel(
            Transform parent, string name, string text, float fontSize,
            Vector2 anchorMin, Vector2 anchorMax,
            TextAlignmentOptions alignment, Color color, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = alignment;
            tmp.fontSize = fontSize;
            tmp.color = color;
            if (font != null) tmp.font = font;
            return tmp;
        }

        private static GameObject InstantiateLabeledButton(GameObject prefab, Transform parent, string name, string label)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            var mb = go.GetComponent<MenuButton>();
            if (mb != null) mb.Text = label;
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 80;
            return go;
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogWarning($"[Init] Λείπει: {path}");
            return asset;
        }

        private static TextMeshProUGUI AddSectionTitle(Transform parent, string text, float fontSize, TMP_FontAsset font)
        {
            var go = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = fontSize;
            tmp.color = AccentCream;
            if (font != null) tmp.font = font;
            go.AddComponent<LayoutElement>().preferredHeight = fontSize + 14;
            return tmp;
        }

        private struct ToggleRow { public Toggle toggle; public TextMeshProUGUI label; }
        private struct DropdownRow { public TMP_Dropdown dropdown; public TextMeshProUGUI label; }
        private struct SliderRow { public Slider slider; public TextMeshProUGUI label; public TextMeshProUGUI valueLabel; }

        private static ToggleRow AddToggleRow(Transform parent, string label)
        {
            var row = CreateRow(parent, label, 56, 20);

            var lbl = CreateText(row.transform, label, 28);
            var toggleGO = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle), typeof(Image));
            toggleGO.transform.SetParent(row.transform, false);
            var toggle = toggleGO.GetComponent<Toggle>();
            var bg = toggleGO.GetComponent<Image>();
            bg.color = new Color(1, 1, 1, 0.15f);
            toggle.targetGraphic = bg;

            var check = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            check.transform.SetParent(toggleGO.transform, false);
            var checkRect = check.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.1f, 0.1f);
            checkRect.anchorMax = new Vector2(0.9f, 0.9f);
            checkRect.offsetMin = checkRect.offsetMax = Vector2.zero;
            check.GetComponent<Image>().color = BorderGold;
            toggle.graphic = check.GetComponent<Image>();

            return new ToggleRow { toggle = toggle, label = lbl };
        }

        private static DropdownRow AddDropdownRow(Transform parent, string label)
        {
            var row = CreateRow(parent, label, 64, 20);
            var lbl = CreateText(row.transform, label, 28);

            var ddGO = new GameObject("Dropdown", typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown));
            ddGO.transform.SetParent(row.transform, false);
            ddGO.GetComponent<Image>().color = new Color(1, 1, 1, 0.15f);
            var dropdown = ddGO.GetComponent<TMP_Dropdown>();

            var capTmp = CreateLabel(ddGO.transform, "Label", string.Empty, 26,
                new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.9f),
                TextAlignmentOptions.MidlineLeft, LabelWhite, fontRegular);
            dropdown.captionText = capTmp;

            return new DropdownRow { dropdown = dropdown, label = lbl };
        }

        private static SliderRow AddSliderRow(Transform parent, string label)
        {
            var row = CreateRow(parent, label, 56, 16);
            var lbl = CreateText(row.transform, label, 28);

            var sliderGO = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGO.transform.SetParent(row.transform, false);
            var slider = sliderGO.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.7f;

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(sliderGO.transform, false);
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.4f);
            bgRect.anchorMax = new Vector2(1, 0.6f);
            bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(1, 1, 1, 0.15f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGO.transform, false);
            var faRect = fillArea.GetComponent<RectTransform>();
            faRect.anchorMin = new Vector2(0, 0.4f);
            faRect.anchorMax = new Vector2(1, 0.6f);
            faRect.offsetMin = faRect.offsetMax = Vector2.zero;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            StretchFull(fill.GetComponent<RectTransform>());
            fill.GetComponent<Image>().color = BorderGold;
            slider.fillRect = fill.GetComponent<RectTransform>();

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGO.transform, false);
            StretchFull(handleArea.GetComponent<RectTransform>());

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20, 40);
            handle.GetComponent<Image>().color = LabelWhite;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<Image>();

            var valueLbl = CreateText(row.transform, "70%", 24);
            return new SliderRow { slider = slider, label = lbl, valueLabel = valueLbl };
        }

        private static GameObject CreateRow(Transform parent, string name, float height, float spacing)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            row.AddComponent<LayoutElement>().preferredHeight = height;
            var lay = row.GetComponent<HorizontalLayoutGroup>();
            lay.spacing = spacing;
            lay.childForceExpandWidth = true;
            lay.childForceExpandHeight = true;
            return row;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string text, float fontSize)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.fontSize = fontSize;
            tmp.color = LabelWhite;
            if (fontRegular != null) tmp.font = fontRegular;
            return tmp;
        }
    }
}
#endif
