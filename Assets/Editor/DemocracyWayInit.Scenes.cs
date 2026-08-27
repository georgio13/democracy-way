#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DemocracyWay.EditorTools
{
    using DemocracyWay.Core;
    using DemocracyWay.Dialogue;
    using DemocracyWay.Framework;
    using DemocracyWay.Menu;

    using CharacterCreationController = global::DemocracyWay.UI.CharacterCreationController;
    using ComicPlayer                 = global::DemocracyWay.UI.ComicPlayer;
    using ComicSequence               = global::DemocracyWay.UI.ComicSequence;
    using DialoguePanel               = global::DemocracyWay.UI.DialoguePanel;
    using GameSceneController         = global::DemocracyWay.UI.GameSceneController;
    using IndicatorHud                = global::DemocracyWay.UI.IndicatorHud;
    using PrytanyHud                  = global::DemocracyWay.UI.PrytanyHud;
    using SaveSlotPanel               = global::DemocracyWay.UI.SaveSlotPanel;

    /// <summary>
    /// Builds the five scenes. Each one is created from an empty scene and
    /// fully wired here, so the scene files are disposable output — never edit
    /// them expecting the change to survive the next Init run.
    /// </summary>
    public static partial class DemocracyWayInit
    {
        // ═════════════════════════════════════════════════════════════════════
        // BOOTSTRAP
        // ═════════════════════════════════════════════════════════════════════

        private static void BuildBootstrapScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Loaded AFTER NewScene on purpose: creating a scene unloads
            // assets nothing references any more, which turns anything
            // fetched earlier into a destroyed object. Unity then refuses
            // the assignment and the field serialises as null.
            var pauseMenuPrefab = Load<GameObject>(PauseMenuPrefabPath);
            var creationDb      = Load<CreationDatabase>(CreationDbPath);
            var dialogueDb      = Load<DialogueDatabase>(DialogueDbPath);

            // This camera dies with the Bootstrap scene, so the AudioListener
            // deliberately lives on AudioManager (which is DontDestroyOnLoad)
            // rather than here — otherwise every later scene would have none.
            CreateCamera();
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var systems = new GameObject("[Systems]");

            var audioGO = new GameObject("AudioManager");
            audioGO.transform.SetParent(systems.transform, false);
            audioGO.AddComponent<AudioListener>();
            var audio = audioGO.AddComponent<AudioManager>();
            var audioSo = new SerializedObject(audio);
            SetRef(audioSo, "buttonHoverClip", Load<AudioClip>(ButtonHoverPath), "BootstrapScene");
            SetRef(audioSo, "buttonClickClip", Load<AudioClip>(ButtonClickPath), "BootstrapScene");
            audioSo.ApplyModifiedPropertiesWithoutUndo();

            var settingsGO = new GameObject("SettingsManager");
            settingsGO.transform.SetParent(systems.transform, false);
            settingsGO.AddComponent<SettingsManager>();

            var cursorGO = new GameObject("CursorManager");
            cursorGO.transform.SetParent(systems.transform, false);
            var cursor = cursorGO.AddComponent<CursorManager>();
            var cursorSo = new SerializedObject(cursor);
            SetRef(cursorSo, "cursorTexture", Load<Texture2D>(CursorPath), "BootstrapScene");
            cursorSo.ApplyModifiedPropertiesWithoutUndo();

            CreateSceneTransitionController(systems.transform);

            // GameStateService owns the run and both content databases, so
            // every scene can reach them through its singleton.
            var stateGO = new GameObject("GameStateService");
            stateGO.transform.SetParent(systems.transform, false);
            var state = stateGO.AddComponent<GameStateService>();
            var stateSo = new SerializedObject(state);
            SetRef(stateSo, "creationDatabase", creationDb, "BootstrapScene");
            SetRef(stateSo, "dialogueDatabase", dialogueDb, "BootstrapScene");
            stateSo.ApplyModifiedPropertiesWithoutUndo();

            var pauseGO = new GameObject("PauseMenuController");
            pauseGO.transform.SetParent(systems.transform, false);
            var pauseCtrl = pauseGO.AddComponent<PauseMenuController>();
            var pauseSo = new SerializedObject(pauseCtrl);
            SetRef(pauseSo, "pauseMenuPrefab", pauseMenuPrefab, "BootstrapScene");
            SetRef(pauseSo, "inputActions", Load<InputActionAsset>(InputActionsPath), "BootstrapScene");
            // Pause is meaningless outside the run itself — the menu, creation
            // and the intro comic all have their own way out.
            SetStringArray(pauseSo, "disabledScenes",
                new[] { "Bootstrap", "MainMenu", "CharacterCreation", "ComicIntro" });
            pauseSo.ApplyModifiedPropertiesWithoutUndo();

            var bootGO = new GameObject("BootstrapLoader");
            bootGO.AddComponent<DemocracyWayBootstrapLoader>();

            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
            Debug.Log($"[Init] Σκηνή: {BootstrapScenePath}");
        }

        private static void CreateSceneTransitionController(Transform parent)
        {
            var go = new GameObject("SceneTransitionController",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            ConfigureScaler(go.GetComponent<CanvasScaler>());

            var overlayGO = new GameObject("Overlay", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            overlayGO.transform.SetParent(go.transform, false);
            StretchFull(overlayGO.GetComponent<RectTransform>());
            var overlayImg = overlayGO.GetComponent<Image>();
            overlayImg.color = Color.black;
            overlayImg.raycastTarget = true;

            // Starts fully black so the first fade — Bootstrap into MainMenu —
            // is visible rather than a hard cut.
            var overlayGroup = overlayGO.GetComponent<CanvasGroup>();
            overlayGroup.alpha = 1f;

            var chapter = CreateLabel(overlayGO.transform, "ChapterText", string.Empty, 56,
                new Vector2(0.10f, 0.40f), new Vector2(0.90f, 0.60f),
                TextAlignmentOptions.Center, AccentCream, fontBold ?? fontRegular);

            var controller = go.AddComponent<SceneTransitionController>();
            var so = new SerializedObject(controller);
            SetRef(so, "overlayCanvas", canvas, "SceneTransitionController");
            SetRef(so, "overlayGroup", overlayGroup, "SceneTransitionController");
            SetRef(so, "blackImage", overlayImg, "SceneTransitionController");
            SetRef(so, "chapterText", chapter, "SceneTransitionController");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ═════════════════════════════════════════════════════════════════════
        // MAIN MENU
        // ═════════════════════════════════════════════════════════════════════

        private static void BuildMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Loaded AFTER NewScene on purpose: creating a scene unloads
            // assets nothing references any more, which turns anything
            // fetched earlier into a destroyed object. Unity then refuses
            // the assignment and the field serialises as null.
            var menuButtonPrefab    = Load<GameObject>(MenuButtonPrefabPath);
            var settingsPanelPrefab = Load<GameObject>(SettingsPanelPrefabPath);
            var confirmDialogPrefab = Load<GameObject>(ConfirmDialogPrefabPath);
            var slotRowPrefab       = Load<GameObject>(SaveSlotRowPrefabPath);
            CreateCamera();
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var canvas = CreateCanvas();

            AddBackground(canvas.transform, BgMainMenuId, vignetteAlpha: 0.45f);

            CreateLabel(canvas.transform, "Title", "Οδός Δημοκρατίας", 110,
                new Vector2(0.1f, 0.70f), new Vector2(0.9f, 0.90f),
                TextAlignmentOptions.Center, AccentCream, fontBold ?? fontRegular);

            var column = CreateVerticalColumn(canvas.transform, "ButtonColumn",
                new Vector2(0.38f, 0.13f), new Vector2(0.62f, 0.60f), spacing: 22,
                alignment: TextAnchor.UpperCenter);

            var newGame  = InstantiateLabeledButton(menuButtonPrefab, column, "NewGameButton",  "Νέο Παιχνίδι");
            var loadGame = InstantiateLabeledButton(menuButtonPrefab, column, "LoadGameButton", "Φόρτωση");
            var settings = InstantiateLabeledButton(menuButtonPrefab, column, "SettingsButton", "Ρυθμίσεις");
            var quit     = InstantiateLabeledButton(menuButtonPrefab, column, "QuitButton",     "Έξοδος");

            var settingsPanel = (GameObject)PrefabUtility.InstantiatePrefab(settingsPanelPrefab, canvas.transform);
            settingsPanel.name = "SettingsPanel";
            settingsPanel.SetActive(false);

            var confirmDialog = (GameObject)PrefabUtility.InstantiatePrefab(confirmDialogPrefab, canvas.transform);
            confirmDialog.name = "ConfirmDialog";
            confirmDialog.SetActive(false);

            var slotPanel = CreateSaveSlotPanel(canvas.transform, menuButtonPrefab, slotRowPrefab, confirmDialog);

            // Sibling order is render order on a Screen Space Overlay canvas.
            // The confirm dialog is opened *from* the slot panel (overwrite /
            // delete prompts), so it has to sit above it — and the settings
            // panel above the menu it covers.
            settingsPanel.transform.SetAsLastSibling();
            confirmDialog.transform.SetAsLastSibling();

            var ctrl = new GameObject("MainMenuController").AddComponent<MainMenuController>();
            var so = new SerializedObject(ctrl);
            SetRef(so, "newGameButton", newGame.GetComponent<MenuButton>(), "MainMenuScene");
            SetRef(so, "loadGameButton", loadGame.GetComponent<MenuButton>(), "MainMenuScene");
            SetRef(so, "settingsButton", settings.GetComponent<MenuButton>(), "MainMenuScene");
            SetRef(so, "quitButton", quit.GetComponent<MenuButton>(), "MainMenuScene");
            SetRef(so, "settingsPanel", settingsPanel.GetComponent<SettingsPanel>(), "MainMenuScene");
            SetRef(so, "confirmDialog", confirmDialog.GetComponent<ConfirmDialog>(), "MainMenuScene");
            SetRef(so, "saveSlotPanel", slotPanel, "MainMenuScene");
            SetRef(so, "ambientMusic", Load<AudioClip>(AmbientMusicPath), "MainMenuScene");
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
            Debug.Log($"[Init] Σκηνή: {MainMenuScenePath}");
        }

        /// <summary>Full-screen modal listing the four save slots. Shared by the
        /// New Game and Load flows; only the mode it opens in differs.</summary>
        private static SaveSlotPanel CreateSaveSlotPanel(
            Transform canvas, GameObject menuButtonPrefab, GameObject slotRowPrefab, GameObject confirmDialog)
        {
            var root = new GameObject("SaveSlotPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            root.transform.SetParent(canvas, false);
            StretchFull(root.GetComponent<RectTransform>());
            root.GetComponent<Image>().color = new Color(0.01f, 0.02f, 0.04f, 0.94f);

            var title = CreateLabel(root.transform, "Title", "ΦΟΡΤΩΣΗ ΠΑΙΧΝΙΔΙΟΥ", 52,
                new Vector2(0.05f, 0.85f), new Vector2(0.95f, 0.95f),
                TextAlignmentOptions.Center, AccentCream, fontBold ?? fontRegular);

            var container = CreateVerticalColumn(root.transform, "SlotContainer",
                new Vector2(0.12f, 0.22f), new Vector2(0.88f, 0.82f), spacing: 18,
                alignment: TextAnchor.UpperCenter);

            var back = (GameObject)PrefabUtility.InstantiatePrefab(menuButtonPrefab, root.transform);
            back.name = "BackButton";
            var backRect = back.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0.42f, 0.09f);
            backRect.anchorMax = new Vector2(0.58f, 0.16f);
            backRect.offsetMin = backRect.offsetMax = Vector2.zero;
            back.GetComponent<MenuButton>().Text = "Πίσω";

            var panel = root.AddComponent<SaveSlotPanel>();
            var so = new SerializedObject(panel);
            SetRef(so, "rootGroup", root.GetComponent<CanvasGroup>(), "SaveSlotPanel");
            SetRef(so, "titleLabel", title, "SaveSlotPanel");
            SetRef(so, "rowContainer", container, "SaveSlotPanel");
            SetRef(so, "rowPrefab", slotRowPrefab, "SaveSlotPanel");
            SetRef(so, "backButton", back.GetComponent<MenuButton>(), "SaveSlotPanel");
            SetRef(so, "confirmDialog", confirmDialog.GetComponent<ConfirmDialog>(), "SaveSlotPanel");
            so.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);
            return panel;
        }

        // ═════════════════════════════════════════════════════════════════════
        // CHARACTER CREATION
        // ═════════════════════════════════════════════════════════════════════

        private static void BuildCharacterCreationScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Loaded AFTER NewScene on purpose: creating a scene unloads
            // assets nothing references any more, which turns anything
            // fetched earlier into a destroyed object. Unity then refuses
            // the assignment and the field serialises as null.
            var menuButtonPrefab = Load<GameObject>(MenuButtonPrefabPath);
            var optionPrefab     = Load<GameObject>(CreationOptionPrefabPath);
            var creationDb       = Load<CreationDatabase>(CreationDbPath);
            CreateCamera();
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var canvas = CreateCanvas();

            // Flat dark ground rather than a photo — the artwork on the left is
            // the focus and a busy background would fight it.
            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(canvas.transform, false);
            StretchFull(bg.GetComponent<RectTransform>());
            var bgImg = bg.GetComponent<Image>();
            bgImg.color = new Color(0.04f, 0.05f, 0.08f, 1f);
            bgImg.raycastTarget = false;

            // ── LEFT: artwork with its description underneath ──
            var previewPanel = new GameObject("PreviewPanel", typeof(RectTransform), typeof(Image));
            previewPanel.transform.SetParent(canvas.transform, false);
            var previewRect = previewPanel.GetComponent<RectTransform>();
            previewRect.anchorMin = new Vector2(0.035f, 0.10f);
            previewRect.anchorMax = new Vector2(0.505f, 0.90f);
            previewRect.offsetMin = previewRect.offsetMax = Vector2.zero;
            var previewBg = previewPanel.GetComponent<Image>();
            previewBg.color = PanelDim;
            previewBg.raycastTarget = false;

            var previewImage = new GameObject("Artwork", typeof(RectTransform), typeof(Image));
            previewImage.transform.SetParent(previewPanel.transform, false);
            var artRect = previewImage.GetComponent<RectTransform>();
            artRect.anchorMin = new Vector2(0.04f, 0.44f);
            artRect.anchorMax = new Vector2(0.96f, 0.97f);
            artRect.offsetMin = artRect.offsetMax = Vector2.zero;
            var artImg = previewImage.GetComponent<Image>();
            artImg.preserveAspect = true;
            artImg.raycastTarget = false;

            var previewTitle = CreateLabel(previewPanel.transform, "PreviewTitle", string.Empty, 40,
                new Vector2(0.05f, 0.35f), new Vector2(0.95f, 0.43f),
                TextAlignmentOptions.Left, AccentCream, fontBold ?? fontRegular);

            var previewDesc = CreateLabel(previewPanel.transform, "PreviewDescription", string.Empty, 23,
                new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.34f),
                TextAlignmentOptions.TopLeft, new Color(0.84f, 0.82f, 0.76f), fontRegular);
            previewDesc.textWrappingMode = TextWrappingModes.Normal;

            // ── RIGHT: the option list, one under the next ──
            var stepTitle = CreateLabel(canvas.transform, "StepTitle", "Φύλο", 46,
                new Vector2(0.53f, 0.855f), new Vector2(0.90f, 0.925f),
                TextAlignmentOptions.MidlineLeft, AccentCream, fontBold ?? fontRegular);

            var stepCounter = CreateLabel(canvas.transform, "StepCounter", "1 / 6", 26,
                new Vector2(0.90f, 0.855f), new Vector2(0.965f, 0.925f),
                TextAlignmentOptions.MidlineRight, LabelMuted, fontRegular);

            var optionContainer = CreateScrollList(canvas.transform, "OptionList",
                new Vector2(0.53f, 0.16f), new Vector2(0.965f, 0.845f), spacing: 12);

            // ── Navigation ──
            var back = (GameObject)PrefabUtility.InstantiatePrefab(menuButtonPrefab, canvas.transform);
            back.name = "BackButton";
            var backRect = back.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0.53f, 0.045f);
            backRect.anchorMax = new Vector2(0.68f, 0.125f);
            backRect.offsetMin = backRect.offsetMax = Vector2.zero;

            var next = (GameObject)PrefabUtility.InstantiatePrefab(menuButtonPrefab, canvas.transform);
            next.name = "NextButton";
            var nextRect = next.GetComponent<RectTransform>();
            nextRect.anchorMin = new Vector2(0.81f, 0.045f);
            nextRect.anchorMax = new Vector2(0.965f, 0.125f);
            nextRect.offsetMin = nextRect.offsetMax = Vector2.zero;

            var ctrl = new GameObject("CharacterCreationController").AddComponent<CharacterCreationController>();
            var so = new SerializedObject(ctrl);
            SetRef(so, "database", creationDb, "CharacterCreationScene");
            SetRef(so, "previewImage", artImg, "CharacterCreationScene");
            SetRef(so, "previewTitle", previewTitle, "CharacterCreationScene");
            SetRef(so, "previewDescription", previewDesc, "CharacterCreationScene");
            SetRef(so, "stepTitle", stepTitle, "CharacterCreationScene");
            SetRef(so, "stepCounter", stepCounter, "CharacterCreationScene");
            SetRef(so, "optionContainer", optionContainer, "CharacterCreationScene");
            SetRef(so, "optionPrefab", optionPrefab, "CharacterCreationScene");
            SetRef(so, "backButton", back.GetComponent<MenuButton>(), "CharacterCreationScene");
            SetRef(so, "nextButton", next.GetComponent<MenuButton>(), "CharacterCreationScene");
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, CreationScenePath);
            Debug.Log($"[Init] Σκηνή: {CreationScenePath}");
        }

        // ═════════════════════════════════════════════════════════════════════
        // COMIC INTRO
        // ═════════════════════════════════════════════════════════════════════

        private static void BuildComicScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Loaded AFTER NewScene on purpose: creating a scene unloads
            // assets nothing references any more, which turns anything
            // fetched earlier into a destroyed object. Unity then refuses
            // the assignment and the field serialises as null.
            var menuButtonPrefab  = Load<GameObject>(MenuButtonPrefabPath);
            var comicPanelPrefab  = Load<GameObject>(ComicPanelPrefabPath);
            var sequence          = Load<ComicSequence>(ComicSequencePath);
            CreateCamera();
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var canvas = CreateCanvas();

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(canvas.transform, false);
            StretchFull(bg.GetComponent<RectTransform>());
            var bgImg = bg.GetComponent<Image>();
            bgImg.color = new Color(0.02f, 0.03f, 0.05f, 1f);
            bgImg.raycastTarget = false;

            var title = CreateLabel(canvas.transform, "Title", sequence != null ? sequence.title : string.Empty, 48,
                new Vector2(0.05f, 0.90f), new Vector2(0.95f, 0.975f),
                TextAlignmentOptions.Center, AccentCream, fontBold ?? fontRegular);

            // 3 × 2 grid of frames, revealed left-to-right, top-to-bottom.
            var gridGO = new GameObject("PanelGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGO.transform.SetParent(canvas.transform, false);
            var gridRect = gridGO.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.06f, 0.18f);
            gridRect.anchorMax = new Vector2(0.94f, 0.885f);
            gridRect.offsetMin = gridRect.offsetMax = Vector2.zero;
            var grid = gridGO.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(520, 340);
            grid.spacing = new Vector2(24, 24);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.MiddleCenter;

            var hint = CreateLabel(canvas.transform, "Hint", "Κλικ για παράλειψη", 22,
                new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.16f),
                TextAlignmentOptions.Center, LabelMuted, fontRegular);

            var cont = (GameObject)PrefabUtility.InstantiatePrefab(menuButtonPrefab, canvas.transform);
            cont.name = "ContinueButton";
            var contRect = cont.GetComponent<RectTransform>();
            contRect.anchorMin = new Vector2(0.42f, 0.035f);
            contRect.anchorMax = new Vector2(0.58f, 0.105f);
            contRect.offsetMin = contRect.offsetMax = Vector2.zero;
            cont.SetActive(false);

            var player = new GameObject("ComicPlayer").AddComponent<ComicPlayer>();
            var so = new SerializedObject(player);
            SetRef(so, "sequence", sequence, "ComicScene");
            SetRef(so, "titleLabel", title, "ComicScene");
            SetRef(so, "panelContainer", gridRect, "ComicScene");
            SetRef(so, "panelPrefab", comicPanelPrefab, "ComicScene");
            SetRef(so, "continueButton", cont.GetComponent<MenuButton>(), "ComicScene");
            SetRef(so, "hintLabel", hint, "ComicScene");
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ComicScenePath);
            Debug.Log($"[Init] Σκηνή: {ComicScenePath}");
        }

        // ═════════════════════════════════════════════════════════════════════
        // GAME
        // ═════════════════════════════════════════════════════════════════════

        private static void BuildGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Loaded AFTER NewScene on purpose: creating a scene unloads
            // assets nothing references any more, which turns anything
            // fetched earlier into a destroyed object. Unity then refuses
            // the assignment and the field serialises as null.
            var menuButtonPrefab    = Load<GameObject>(MenuButtonPrefabPath);
            var choiceButtonPrefab  = Load<GameObject>(ChoiceButtonPrefabPath);
            var indicatorBarPrefab  = Load<GameObject>(IndicatorBarPrefabPath);
            CreateCamera();
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var canvas = CreateCanvas();

            AddBackground(canvas.transform, "BG_PnyxMorning", vignetteAlpha: 0.62f);

            // ── Top bar: prytany + round + who you are ──
            var topBar = new GameObject("TopBar", typeof(RectTransform), typeof(Image));
            topBar.transform.SetParent(canvas.transform, false);
            var topRect = topBar.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0f, 0.90f);
            topRect.anchorMax = new Vector2(1f, 1f);
            topRect.offsetMin = topRect.offsetMax = Vector2.zero;
            var topImg = topBar.GetComponent<Image>();
            topImg.color = PanelDim;
            topImg.raycastTarget = false;

            var prytanyLabel = CreateLabel(topBar.transform, "PrytanyLabel", "—", 32,
                new Vector2(0.02f, 0.45f), new Vector2(0.62f, 0.95f),
                TextAlignmentOptions.MidlineLeft, AccentCream, fontBold ?? fontRegular);

            var citizenLabel = CreateLabel(topBar.transform, "CitizenLabel", string.Empty, 21,
                new Vector2(0.02f, 0.05f), new Vector2(0.62f, 0.44f),
                TextAlignmentOptions.MidlineLeft, LabelMuted, fontRegular);

            var roundLabel = CreateLabel(topBar.transform, "RoundLabel", "—", 32,
                new Vector2(0.64f, 0.20f), new Vector2(0.98f, 0.85f),
                TextAlignmentOptions.MidlineRight, LabelWhite, fontBold ?? fontRegular);

            var prytanyHud = new GameObject("PrytanyHud").AddComponent<PrytanyHud>();
            var prytanySo = new SerializedObject(prytanyHud);
            SetRef(prytanySo, "prytanyLabel", prytanyLabel, "GameScene");
            SetRef(prytanySo, "roundLabel", roundLabel, "GameScene");
            SetRef(prytanySo, "citizenLabel", citizenLabel, "GameScene");
            prytanySo.ApplyModifiedPropertiesWithoutUndo();

            // ── Right sidebar: the five indicators ──
            var sidebar = new GameObject("IndicatorSidebar", typeof(RectTransform), typeof(Image));
            sidebar.transform.SetParent(canvas.transform, false);
            var sideRect = sidebar.GetComponent<RectTransform>();
            sideRect.anchorMin = new Vector2(0.70f, 0.34f);
            sideRect.anchorMax = new Vector2(0.985f, 0.875f);
            sideRect.offsetMin = sideRect.offsetMax = Vector2.zero;
            var sideImg = sidebar.GetComponent<Image>();
            sideImg.color = PanelDim;
            sideImg.raycastTarget = false;

            CreateLabel(sidebar.transform, "SidebarTitle", "ΔΕΙΚΤΕΣ", 26,
                new Vector2(0.06f, 0.88f), new Vector2(0.94f, 0.98f),
                TextAlignmentOptions.MidlineLeft, AccentCream, fontBold ?? fontRegular);

            var barContainer = CreateVerticalColumn(sidebar.transform, "BarContainer",
                new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.86f), spacing: 14,
                alignment: TextAnchor.UpperCenter);

            var indicatorHud = new GameObject("IndicatorHud").AddComponent<IndicatorHud>();
            var indicatorSo = new SerializedObject(indicatorHud);
            SetRef(indicatorSo, "barContainer", barContainer, "GameScene");
            SetRef(indicatorSo, "barPrefab", indicatorBarPrefab, "GameScene");
            indicatorSo.ApplyModifiedPropertiesWithoutUndo();

            // ── Action buttons ──
            var actions = CreateVerticalColumn(canvas.transform, "ActionColumn",
                new Vector2(0.06f, 0.10f), new Vector2(0.36f, 0.34f), spacing: 18,
                alignment: TextAnchor.UpperCenter);

            var randomBtn = InstantiateLabeledButton(menuButtonPrefab, actions, "RandomDialogueButton", "Τυχαίος διάλογος");
            var nextBtn   = InstantiateLabeledButton(menuButtonPrefab, actions, "NextRoundButton",      "Επόμενη πρυτανεία");

            var status = CreateLabel(canvas.transform, "StatusLabel", string.Empty, 24,
                new Vector2(0.06f, 0.04f), new Vector2(0.66f, 0.095f),
                TextAlignmentOptions.MidlineLeft, LabelMuted, fontRegular);

            // ── Dialogue overlay ──
            var dialoguePanel = CreateDialoguePanel(canvas.transform, menuButtonPrefab, choiceButtonPrefab);

            var ctrl = new GameObject("GameSceneController").AddComponent<GameSceneController>();
            var so = new SerializedObject(ctrl);
            SetRef(so, "dialoguePanel", dialoguePanel, "GameScene");
            SetRef(so, "randomDialogueButton", randomBtn.GetComponent<MenuButton>(), "GameScene");
            SetRef(so, "nextRoundButton", nextBtn.GetComponent<MenuButton>(), "GameScene");
            SetRef(so, "statusLabel", status, "GameScene");
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, GameScenePath);
            Debug.Log($"[Init] Σκηνή: {GameScenePath}");
        }

        private static DialoguePanel CreateDialoguePanel(
            Transform canvas, GameObject menuButtonPrefab, GameObject choiceButtonPrefab)
        {
            var root = new GameObject("DialoguePanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            root.transform.SetParent(canvas, false);
            StretchFull(root.GetComponent<RectTransform>());
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.80f);

            var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(root.transform, false);
            var boxRect = box.GetComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.13f, 0.14f);
            boxRect.anchorMax = new Vector2(0.87f, 0.86f);
            boxRect.offsetMin = boxRect.offsetMax = Vector2.zero;
            box.GetComponent<Image>().color = PanelBlack;

            var title = CreateLabel(box.transform, "Title", string.Empty, 38,
                new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.97f),
                TextAlignmentOptions.MidlineLeft, AccentCream, fontBold ?? fontRegular);

            var speaker = CreateLabel(box.transform, "Speaker", string.Empty, 28,
                new Vector2(0.05f, 0.79f), new Vector2(0.95f, 0.87f),
                TextAlignmentOptions.MidlineLeft, new Color(0.78f, 0.86f, 0.92f), fontBold ?? fontRegular);

            var body = CreateLabel(box.transform, "Body", string.Empty, 27,
                new Vector2(0.05f, 0.46f), new Vector2(0.95f, 0.78f),
                TextAlignmentOptions.TopLeft, LabelWhite, fontRegular);
            body.textWrappingMode = TextWrappingModes.Normal;

            var effects = CreateLabel(box.transform, "Effects", string.Empty, 23,
                new Vector2(0.05f, 0.38f), new Vector2(0.95f, 0.45f),
                TextAlignmentOptions.MidlineLeft, AccentCream, fontRegular);

            var choices = CreateVerticalColumn(box.transform, "ChoiceContainer",
                new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.44f), spacing: 12,
                alignment: TextAnchor.UpperCenter);

            var cont = (GameObject)PrefabUtility.InstantiatePrefab(menuButtonPrefab, box.transform);
            cont.name = "ContinueButton";
            var contRect = cont.GetComponent<RectTransform>();
            contRect.anchorMin = new Vector2(0.72f, 0.05f);
            contRect.anchorMax = new Vector2(0.95f, 0.14f);
            contRect.offsetMin = contRect.offsetMax = Vector2.zero;

            var panel = root.AddComponent<DialoguePanel>();
            var so = new SerializedObject(panel);
            SetRef(so, "rootGroup", root.GetComponent<CanvasGroup>(), "DialoguePanel");
            SetRef(so, "titleLabel", title, "DialoguePanel");
            SetRef(so, "speakerLabel", speaker, "DialoguePanel");
            SetRef(so, "bodyLabel", body, "DialoguePanel");
            SetRef(so, "effectsLabel", effects, "DialoguePanel");
            SetRef(so, "continueButton", cont.GetComponent<MenuButton>(), "DialoguePanel");
            SetRef(so, "choiceContainer", choices, "DialoguePanel");
            SetRef(so, "choicePrefab", choiceButtonPrefab, "DialoguePanel");
            so.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);
            return panel;
        }

        // ═════════════════════════════════════════════════════════════════════
        // SCENE HELPERS
        // ═════════════════════════════════════════════════════════════════════

        private static void SetStringArray(SerializedObject so, string propertyName, string[] values)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null) return;
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).stringValue = values[i];
        }

        private static Camera CreateCamera()
        {
            var camGO = new GameObject("Main Camera", typeof(Camera));
            var cam = camGO.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            camGO.tag = "MainCamera";
            return cam;
        }

        private static Canvas CreateCanvas()
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            ConfigureScaler(go.GetComponent<CanvasScaler>());
            return canvas;
        }

        /// <summary>Full-bleed background image plus a dark vignette so text
        /// stays legible over it.</summary>
        private static void AddBackground(Transform canvas, string bgId, float vignetteAlpha)
        {
            var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGO.transform.SetParent(canvas, false);
            StretchFull(bgGO.GetComponent<RectTransform>());
            var bgImg = bgGO.GetComponent<Image>();
            bgImg.sprite = LoadBgOrFallback(bgId);
            bgImg.color = Color.white;
            bgImg.preserveAspect = false;
            bgImg.raycastTarget = false;

            var vignette = new GameObject("Vignette", typeof(RectTransform), typeof(Image));
            vignette.transform.SetParent(canvas, false);
            StretchFull(vignette.GetComponent<RectTransform>());
            var vImg = vignette.GetComponent<Image>();
            vImg.color = new Color(0f, 0f, 0f, vignetteAlpha);
            vImg.raycastTarget = false;
        }

        private static RectTransform CreateVerticalColumn(
            Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            float spacing, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            // childControlHeight makes the group honour each child's
            // LayoutElement.preferredHeight — which is also what lets a
            // ContentSizeFitter above it compute the right total height.
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childAlignment = alignment;
            return rect;
        }

        /// <summary>
        /// A scrollable vertical list. Returns the Content transform that rows
        /// should be parented to — the ScrollRect/Viewport/Content plumbing and
        /// the ContentSizeFitter that makes it scroll are set up here.
        /// </summary>
        private static RectTransform CreateScrollList(
            Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, float spacing)
        {
            var scrollGO = new GameObject(name, typeof(RectTransform), typeof(ScrollRect));
            scrollGO.transform.SetParent(parent, false);
            var scrollRect = scrollGO.GetComponent<RectTransform>();
            scrollRect.anchorMin = anchorMin;
            scrollRect.anchorMax = anchorMax;
            scrollRect.offsetMin = scrollRect.offsetMax = Vector2.zero;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollGO.transform, false);
            StretchFull(viewport.GetComponent<RectTransform>());
            var viewportImg = viewport.GetComponent<Image>();
            viewportImg.color = new Color(1f, 1f, 1f, 0.01f); // near-invisible, but Mask needs a graphic
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content",
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            // Top-anchored and full width so the list grows downward.
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = contentRect.offsetMax = Vector2.zero;

            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            // childControlHeight makes the group honour each child's
            // LayoutElement.preferredHeight — which is also what lets a
            // ContentSizeFitter above it compute the right total height.
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childAlignment = TextAnchor.UpperCenter;

            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var scroll = scrollGO.GetComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;

            return contentRect;
        }
    }
}
#endif
