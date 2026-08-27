#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using DemocracyWay.Data;
using DemocracyWay.Gameplay;
using DemocracyWay.Services;
using DemocracyWay.UI;

namespace DemocracyWay.Setup
{
    /// <summary>
    /// Phase 3 of the one-shot Setup: the five scenes, Boot first. Every
    /// builder creates an empty scene and only THEN loads the prefabs/assets
    /// it wires — NewScene unloads unreferenced assets, so anything fetched
    /// earlier would already be a destroyed object by assignment time (a
    /// silent-null bug the old project actually hit).
    /// </summary>
    internal static class SetupScenes
    {
        public static void CreateAll()
        {
            BuildBoot();
            BuildMainMenu();
            BuildCharacterCreation();
            BuildComicIntro();
            BuildChapter01();
        }

        /// <summary>
        /// Registers the five scenes with Boot at index 0 — the entry scene
        /// that owns the Systems prefab. Runs even when every scene already
        /// existed, so a half-configured project self-heals its build list.
        /// </summary>
        public static void RegisterBuildScenes()
        {
            string[] paths =
            {
                SetupPaths.BootScene,   // MUST stay first: the services live here
                SetupPaths.MainMenuScene,
                SetupPaths.CharacterCreationScene,
                SetupPaths.ComicIntroScene,
                SetupPaths.Chapter01Scene
            };

            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (var path in paths)
            {
                if (System.IO.File.Exists(path))
                    list.Add(new EditorBuildSettingsScene(path, true));
                else
                    Debug.LogError($"[Setup] Η σκηνή λείπει από τον δίσκο και δεν μπήκε στα Build Settings: {path}");
            }
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log($"[Setup] Build Settings: {list.Count} σκηνές (Boot πρώτη).");
        }

        // ═════════════════════════════════════════════════════════════════════
        // BOOT
        // ═════════════════════════════════════════════════════════════════════

        private static void BuildBoot()
        {
            if (SetupCommon.SkipIfExists(SetupPaths.BootScene)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var systemsPrefab = SetupCommon.Load<GameObject>(SetupPaths.SystemsPrefab);

            SetupCommon.CreateCamera();
            SetupCommon.CreateEventSystem();   // nothing clickable here, but never ship a canvas-less scene without one

            // InstantiatePrefab (NOT Instantiate): the scene must keep the
            // prefab CONNECTION, so later prefab edits flow into Boot.
            if (systemsPrefab != null)
                PrefabUtility.InstantiatePrefab(systemsPrefab);

            new GameObject("BootLoader", typeof(BootLoader));   // firstSceneName default: "MainMenu"

            EditorSceneManager.SaveScene(scene, SetupPaths.BootScene);
            SetupCommon.MarkCreated(SetupPaths.BootScene);
        }

        // ═════════════════════════════════════════════════════════════════════
        // MAIN MENU
        // ═════════════════════════════════════════════════════════════════════

        private static void BuildMainMenu()
        {
            if (SetupCommon.SkipIfExists(SetupPaths.MainMenuScene)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var uiButton = SetupCommon.Load<GameObject>(SetupPaths.UiButtonPrefab);
            var settingsPrefab = SetupCommon.Load<GameObject>(SetupPaths.SettingsPanelPrefab);
            var confirmPrefab = SetupCommon.Load<GameObject>(SetupPaths.ConfirmDialogPrefab);
            var slotPanelPrefab = SetupCommon.Load<GameObject>(SetupPaths.SaveSlotPanelPrefab);
            var smokeTexture = SetupCommon.Load<Texture2D>(SetupPaths.MenuSmokePng);
            var statueSprite = SetupCommon.Load<Sprite>(SetupPaths.AthenaStatuePng);
            var fireflyMaterial = SetupCommon.Load<Material>(SetupPaths.FireflyMat);

            var cam = SetupCommon.CreateCamera();
            SetupCommon.CreateEventSystem();

            // ── Canvas_BG: Screen Space - Camera, so world-space particles can
            // float BETWEEN the camera and the background plane. ──
            var bgCanvasGO = new GameObject("Canvas_BG", typeof(Canvas), typeof(CanvasScaler));
            var bgCanvas = bgCanvasGO.GetComponent<Canvas>();
            bgCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            bgCanvas.worldCamera = cam;
            bgCanvas.planeDistance = 10f;
            bgCanvas.sortingOrder = 0;
            SetupCommon.ConfigureScaler(bgCanvasGO.GetComponent<CanvasScaler>());

            var smokeGO = new GameObject("Smoke", typeof(RectTransform), typeof(RawImage), typeof(ScrollingBackground));
            smokeGO.transform.SetParent(bgCanvas.transform, false);
            SetupCommon.StretchFull((RectTransform)smokeGO.transform);
            var smoke = smokeGO.GetComponent<RawImage>();
            smoke.texture = smokeTexture;
            // ~1.5 horizontal tiles: the seamless-X texture repeats through the
            // importer's Wrap Repeat while ScrollingBackground slides uvRect.x.
            smoke.uvRect = new Rect(0f, 0f, 1.5f, 1f);
            smoke.raycastTarget = false;
            var scrollSo = new SerializedObject(smokeGO.GetComponent<ScrollingBackground>());
            SetupCommon.SetRef(scrollSo, "target", smoke, "MainMenu (Smoke)");
            SetupCommon.SetFloat(scrollSo, "speed", 0.008f, "MainMenu (Smoke)");
            scrollSo.ApplyModifiedPropertiesWithoutUndo();

            var statueGO = new GameObject("AthenaStatue", typeof(RectTransform), typeof(Image));
            statueGO.transform.SetParent(bgCanvas.transform, false);
            var statueRect = (RectTransform)statueGO.transform;
            statueRect.anchorMin = statueRect.anchorMax = new Vector2(0f, 0.5f);
            statueRect.pivot = new Vector2(0f, 0.5f);
            statueRect.anchoredPosition = new Vector2(60f, 0f);
            statueRect.sizeDelta = new Vector2(500f, 1000f);
            var statue = statueGO.GetComponent<Image>();
            statue.sprite = statueSprite;
            statue.preserveAspect = true;
            statue.raycastTarget = false;

            CreateFireflies(fireflyMaterial);

            // ── Canvas_UI: the interactive layer, always above BG + particles. ──
            var uiCanvas = SetupCommon.CreateOverlayCanvas("Canvas_UI", 10);

            SetupCommon.CreateLabel(uiCanvas.transform, "Title", "Οδός Δημοκρατίας", 64,
                new Vector2(0.55f, 0.78f), new Vector2(0.99f, 0.92f),
                TextAlignmentOptions.Center, SetupCommon.Cream, bold: true);

            var mainColumn = SetupCommon.CreateVerticalColumn(uiCanvas.transform, "MainColumn",
                new Vector2(0.62f, 0.30f), new Vector2(0.92f, 0.72f), 20f, TextAnchor.UpperCenter);

            var newGame = SetupCommon.InstantiateUiButton(uiButton, mainColumn, "NewGameButton", "Νέο Παιχνίδι");
            var load = SetupCommon.InstantiateUiButton(uiButton, mainColumn, "LoadButton", "Φόρτωση Παιχνιδιού");
            var settingsBtn = SetupCommon.InstantiateUiButton(uiButton, mainColumn, "SettingsButton", "Ρυθμίσεις");
            var quit = SetupCommon.InstantiateUiButton(uiButton, mainColumn, "QuitButton", "Έξοδος");

            // Text arrives from the controller; only place + style live here.
            var hint = SetupCommon.CreateLabel(mainColumn, "SlotsFullHint", string.Empty, 20,
                Vector2.zero, Vector2.one, TextAlignmentOptions.Center, SetupCommon.Muted, wrap: true);
            hint.gameObject.AddComponent<LayoutElement>().preferredHeight = 56f;

            // Sub-panels last in sibling order = rendered on top of the column;
            // the confirm dialog last of all (it opens OVER the slot panel).
            var slotPanel = (GameObject)PrefabUtility.InstantiatePrefab(slotPanelPrefab, uiCanvas.transform);
            slotPanel.name = "SaveSlotPanel";
            var settingsPanel = (GameObject)PrefabUtility.InstantiatePrefab(settingsPrefab, uiCanvas.transform);
            settingsPanel.name = "SettingsPanel";
            var confirm = (GameObject)PrefabUtility.InstantiatePrefab(confirmPrefab, uiCanvas.transform);
            confirm.name = "ConfirmDialog";

            // The slot panel's Διαγραφή uses the scene-shared confirm dialog —
            // wired here as an instance override (the prefab leaves it empty).
            var slotPanelSo = new SerializedObject(slotPanel.GetComponent<SaveSlotPanel>());
            SetupCommon.SetRef(slotPanelSo, "confirmDialog", confirm.GetComponent<ConfirmDialog>(), "MainMenu (SaveSlotPanel)");
            slotPanelSo.ApplyModifiedPropertiesWithoutUndo();

            var controller = new GameObject("MainMenuController", typeof(MainMenuController))
                .GetComponent<MainMenuController>();
            var so = new SerializedObject(controller);
            SetupCommon.SetRef(so, "mainColumn", mainColumn.gameObject, "MainMenu (Controller)");
            SetupCommon.SetRef(so, "newGameButton", newGame.GetComponent<UiButton>(), "MainMenu (Controller)");
            SetupCommon.SetRef(so, "loadButton", load.GetComponent<UiButton>(), "MainMenu (Controller)");
            SetupCommon.SetRef(so, "settingsButton", settingsBtn.GetComponent<UiButton>(), "MainMenu (Controller)");
            SetupCommon.SetRef(so, "quitButton", quit.GetComponent<UiButton>(), "MainMenu (Controller)");
            SetupCommon.SetRef(so, "slotsFullHint", hint, "MainMenu (Controller)");
            SetupCommon.SetRef(so, "saveSlotPanel", slotPanel.GetComponent<SaveSlotPanel>(), "MainMenu (Controller)");
            SetupCommon.SetRef(so, "settingsPanel", settingsPanel.GetComponent<SettingsPanel>(), "MainMenu (Controller)");
            SetupCommon.SetRef(so, "confirmDialog", confirm.GetComponent<ConfirmDialog>(), "MainMenu (Controller)");
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, SetupPaths.MainMenuScene);
            SetupCommon.MarkCreated(SetupPaths.MainMenuScene);
        }

        /// <summary>
        /// Golden fireflies drifting at z=5 — between the camera (z=0) and the
        /// BG canvas plane (z=10). ParticleSystemRenderer sorting order 1 puts
        /// them above the BG canvas (0) but the overlay UI canvas still covers
        /// them, so buttons never fight glowing dots.
        /// </summary>
        private static void CreateFireflies(Material fireflyMaterial)
        {
            var go = new GameObject("Fireflies", typeof(ParticleSystem));
            go.transform.position = new Vector3(0f, 0f, 5f);

            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.maxParticles = 25;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startSpeed = 0f;   // drift comes from velocityOverLifetime, not emission
            main.startColor = new Color(0.909f, 0.773f, 0.416f, 1f);   // ~#E8C56A

            var emission = ps.emission;
            emission.rateOverTime = 3f;

            // Box the size of the orthographic view (size 5 → 10 world units tall).
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(18f, 10f, 1f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);   // slow upward drift
            velocity.z = new ParticleSystem.MinMaxCurve(0f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 0.1f;
            noise.scrollSpeed = 0.05f;

            // Fade in/out over each particle's life so fireflies never pop.
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f),
                    new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = fireflyMaterial;
            renderer.sortingOrder = 1;
        }

        // ═════════════════════════════════════════════════════════════════════
        // CHARACTER CREATION
        // ═════════════════════════════════════════════════════════════════════

        private static void BuildCharacterCreation()
        {
            if (SetupCommon.SkipIfExists(SetupPaths.CharacterCreationScene)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var uiButton = SetupCommon.Load<GameObject>(SetupPaths.UiButtonPrefab);
            var optionPrefab = SetupCommon.Load<GameObject>(SetupPaths.CreationOptionButtonPrefab);

            SetupCommon.CreateCamera();
            SetupCommon.CreateEventSystem();
            var canvas = SetupCommon.CreateOverlayCanvas("Canvas", 0);

            var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGO.transform.SetParent(canvas.transform, false);
            SetupCommon.StretchFull((RectTransform)bgGO.transform);
            var bg = bgGO.GetComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.08f, 1f);
            bg.raycastTarget = false;

            // ── LEFT: preview (image + title + description) ──
            var previewPanelGO = new GameObject("PreviewPanel", typeof(RectTransform), typeof(Image));
            previewPanelGO.transform.SetParent(canvas.transform, false);
            SetupCommon.Anchor((RectTransform)previewPanelGO.transform,
                new Vector2(0.03f, 0.10f), new Vector2(0.45f, 0.90f));
            var previewPanelBg = previewPanelGO.GetComponent<Image>();
            previewPanelBg.color = SetupCommon.PanelDim;
            previewPanelBg.raycastTarget = false;

            var previewImageGO = new GameObject("PreviewImage", typeof(RectTransform), typeof(Image));
            previewImageGO.transform.SetParent(previewPanelGO.transform, false);
            SetupCommon.Anchor((RectTransform)previewImageGO.transform,
                new Vector2(0.07f, 0.42f), new Vector2(0.93f, 0.96f));
            var previewImage = previewImageGO.GetComponent<Image>();
            previewImage.preserveAspect = true;
            previewImage.raycastTarget = false;
            previewImage.enabled = false;   // the controller enables it per hovered option

            var previewTitle = SetupCommon.CreateLabel(previewPanelGO.transform, "PreviewTitle", string.Empty, 34,
                new Vector2(0.07f, 0.32f), new Vector2(0.93f, 0.40f),
                TextAlignmentOptions.MidlineLeft, SetupCommon.Cream, bold: true);

            var previewDescription = SetupCommon.CreateLabel(previewPanelGO.transform, "PreviewDescription", string.Empty, 22,
                new Vector2(0.07f, 0.05f), new Vector2(0.93f, 0.31f),
                TextAlignmentOptions.TopLeft, SetupCommon.White, wrap: true);

            // ── RIGHT: step header + option list + navigation ──
            var header = SetupCommon.CreateLabel(canvas.transform, "StepHeader", string.Empty, 40,
                new Vector2(0.50f, 0.86f), new Vector2(0.97f, 0.94f),
                TextAlignmentOptions.MidlineLeft, SetupCommon.Cream, bold: true);

            var optionsContainer = SetupCommon.CreateVerticalColumn(canvas.transform, "OptionsContainer",
                new Vector2(0.50f, 0.20f), new Vector2(0.97f, 0.84f), 10f, TextAnchor.UpperCenter);

            var back = SetupCommon.InstantiateUiButton(uiButton, canvas.transform, "BackButton", "Πίσω");
            SetupCommon.Anchor((RectTransform)back.transform,
                new Vector2(0.50f, 0.06f), new Vector2(0.66f, 0.135f));

            var start = SetupCommon.InstantiateUiButton(uiButton, canvas.transform, "StartButton", "Έναρξη");
            SetupCommon.Anchor((RectTransform)start.transform,
                new Vector2(0.80f, 0.06f), new Vector2(0.97f, 0.135f));
            start.SetActive(false);   // appears only after all six picks

            var controller = new GameObject("CharacterCreationController", typeof(CharacterCreationController))
                .GetComponent<CharacterCreationController>();
            var so = new SerializedObject(controller);
            SetupCommon.SetRef(so, "optionsContainer", optionsContainer, "CharacterCreation (Controller)");
            SetupCommon.SetRef(so, "optionButtonPrefab", optionPrefab != null ? optionPrefab.GetComponent<CreationOptionButton>() : null, "CharacterCreation (Controller)");
            SetupCommon.SetRef(so, "previewImage", previewImage, "CharacterCreation (Controller)");
            SetupCommon.SetRef(so, "previewTitleText", previewTitle, "CharacterCreation (Controller)");
            SetupCommon.SetRef(so, "previewDescriptionText", previewDescription, "CharacterCreation (Controller)");
            SetupCommon.SetRef(so, "headerText", header, "CharacterCreation (Controller)");
            SetupCommon.SetRef(so, "backButton", back.GetComponent<UiButton>(), "CharacterCreation (Controller)");
            SetupCommon.SetRef(so, "startButton", start.GetComponent<UiButton>(), "CharacterCreation (Controller)");
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, SetupPaths.CharacterCreationScene);
            SetupCommon.MarkCreated(SetupPaths.CharacterCreationScene);
        }

        // ═════════════════════════════════════════════════════════════════════
        // COMIC INTRO
        // ═════════════════════════════════════════════════════════════════════

        private static void BuildComicIntro()
        {
            if (SetupCommon.SkipIfExists(SetupPaths.ComicIntroScene)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var uiButton = SetupCommon.Load<GameObject>(SetupPaths.UiButtonPrefab);
            var comicPanelPrefab = SetupCommon.Load<GameObject>(SetupPaths.ComicPanelPrefab);

            SetupCommon.CreateCamera();
            SetupCommon.CreateEventSystem();
            var canvas = SetupCommon.CreateOverlayCanvas("Canvas", 0);

            var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGO.transform.SetParent(canvas.transform, false);
            SetupCommon.StretchFull((RectTransform)bgGO.transform);
            var bg = bgGO.GetComponent<Image>();
            bg.color = new Color(0.02f, 0.03f, 0.05f, 1f);
            bg.raycastTarget = false;

            var gridGO = new GameObject("PanelGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGO.transform.SetParent(canvas.transform, false);
            SetupCommon.Anchor((RectTransform)gridGO.transform,
                new Vector2(0.04f, 0.16f), new Vector2(0.96f, 0.94f));
            var grid = gridGO.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(560f, 410f);
            grid.spacing = new Vector2(24f, 24f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.MiddleCenter;

            var skip = SetupCommon.InstantiateUiButton(uiButton, canvas.transform, "SkipButton", "Παράλειψη");
            SetupCommon.Anchor((RectTransform)skip.transform,
                new Vector2(0.80f, 0.045f), new Vector2(0.96f, 0.115f));

            var player = new GameObject("ComicPlayer", typeof(ComicPlayer)).GetComponent<ComicPlayer>();
            var so = new SerializedObject(player);
            SetupCommon.SetRef(so, "panelContainer", gridGO.GetComponent<RectTransform>(), "ComicIntro (ComicPlayer)");
            SetupCommon.SetRef(so, "panelPrefab", comicPanelPrefab, "ComicIntro (ComicPlayer)");
            SetupCommon.SetRef(so, "skipButton", skip.GetComponent<UiButton>(), "ComicIntro (ComicPlayer)");
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, SetupPaths.ComicIntroScene);
            SetupCommon.MarkCreated(SetupPaths.ComicIntroScene);
        }

        // ═════════════════════════════════════════════════════════════════════
        // CHAPTER 01
        // ═════════════════════════════════════════════════════════════════════

        private static void BuildChapter01()
        {
            if (SetupCommon.SkipIfExists(SetupPaths.Chapter01Scene)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var chapterAsset = SetupCommon.Load<ChapterDefinition>(SetupPaths.Chapter01Asset);
            var indicatorRowPrefab = SetupCommon.Load<GameObject>(SetupPaths.IndicatorRowPrefab);
            var tooltipPrefab = SetupCommon.Load<GameObject>(SetupPaths.TooltipViewPrefab);
            var dialoguePanelPrefab = SetupCommon.Load<GameObject>(SetupPaths.DialoguePanelPrefab);
            var bgSprite = SetupCommon.Load<Sprite>(SetupPaths.BgChapter01Png);

            SetupCommon.CreateCamera();
            SetupCommon.CreateEventSystem();
            var canvas = SetupCommon.CreateOverlayCanvas("Canvas", 0);

            // The controller re-applies the chapter background at runtime; the
            // sprite is ALSO set here so the scene looks right in the editor.
            var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGO.transform.SetParent(canvas.transform, false);
            SetupCommon.StretchFull((RectTransform)bgGO.transform);
            var bg = bgGO.GetComponent<Image>();
            bg.sprite = bgSprite;
            bg.color = Color.white;
            bg.raycastTarget = false;

            // ── Indicator HUD, top-left ──
            var hudGO = new GameObject("IndicatorHud", typeof(RectTransform), typeof(IndicatorHudView));
            hudGO.transform.SetParent(canvas.transform, false);
            var hudRect = (RectTransform)hudGO.transform;
            hudRect.anchorMin = hudRect.anchorMax = new Vector2(0f, 1f);
            hudRect.pivot = new Vector2(0f, 1f);
            hudRect.anchoredPosition = new Vector2(24f, -24f);
            hudRect.sizeDelta = new Vector2(360f, 320f);

            var hudBackdropGO = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            hudBackdropGO.transform.SetParent(hudGO.transform, false);
            SetupCommon.StretchFull((RectTransform)hudBackdropGO.transform);
            var hudBackdrop = hudBackdropGO.GetComponent<Image>();
            hudBackdrop.color = new Color(0f, 0f, 0f, 0.35f);
            hudBackdrop.raycastTarget = false;

            var rows = SetupCommon.CreateVerticalColumn(hudGO.transform, "Rows",
                Vector2.zero, Vector2.one, 8f, TextAnchor.UpperCenter);
            rows.offsetMin = new Vector2(10f, 10f);
            rows.offsetMax = new Vector2(-10f, -10f);

            // ── Profile HUD, top-right ──
            var profileHudGO = new GameObject("ProfileHud", typeof(RectTransform), typeof(ProfileHudView));
            profileHudGO.transform.SetParent(canvas.transform, false);
            var profileRect = (RectTransform)profileHudGO.transform;
            profileRect.anchorMin = profileRect.anchorMax = new Vector2(1f, 1f);
            profileRect.pivot = new Vector2(1f, 1f);
            profileRect.anchoredPosition = new Vector2(-24f, -24f);
            profileRect.sizeDelta = new Vector2(480f, 320f);

            var profileText = SetupCommon.CreateLabel(profileHudGO.transform, "ProfileText", string.Empty, 22,
                Vector2.zero, Vector2.one, TextAlignmentOptions.TopRight, SetupCommon.White, wrap: true);

            // ── Dialogue panel (prefab root stays ACTIVE: its runner polls
            // input; the visible PanelRoot child hides itself in Awake) ──
            var dialoguePanel = (GameObject)PrefabUtility.InstantiatePrefab(dialoguePanelPrefab, canvas.transform);
            dialoguePanel.name = "DialoguePanel";

            // Tooltip LAST so it renders above HUD and dialogue alike.
            var tooltip = (GameObject)PrefabUtility.InstantiatePrefab(tooltipPrefab, canvas.transform);
            tooltip.name = "TooltipView";

            var hudSo = new SerializedObject(hudGO.GetComponent<IndicatorHudView>());
            SetupCommon.SetRef(hudSo, "rowPrefab", indicatorRowPrefab != null ? indicatorRowPrefab.GetComponent<IndicatorRowView>() : null, "Chapter01 (IndicatorHud)");
            SetupCommon.SetRef(hudSo, "rowsContainer", rows, "Chapter01 (IndicatorHud)");
            SetupCommon.SetRef(hudSo, "tooltipView", tooltip.GetComponent<TooltipView>(), "Chapter01 (IndicatorHud)");
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            var profileSo = new SerializedObject(profileHudGO.GetComponent<ProfileHudView>());
            SetupCommon.SetRef(profileSo, "profileText", profileText, "Chapter01 (ProfileHud)");
            profileSo.ApplyModifiedPropertiesWithoutUndo();

            var controller = new GameObject("StorySceneController", typeof(StorySceneController))
                .GetComponent<StorySceneController>();
            var so = new SerializedObject(controller);
            SetupCommon.SetRef(so, "chapter", chapterAsset, "Chapter01 (StorySceneController)");
            SetupCommon.SetRef(so, "backgroundImage", bg, "Chapter01 (StorySceneController)");
            SetupCommon.SetRef(so, "dialogueRunner", dialoguePanel.GetComponent<DialogueRunner>(), "Chapter01 (StorySceneController)");
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, SetupPaths.Chapter01Scene);
            SetupCommon.MarkCreated(SetupPaths.Chapter01Scene);
        }
    }
}
#endif
