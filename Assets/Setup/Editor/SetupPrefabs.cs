#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using DemocracyWay.Data;
using DemocracyWay.Gameplay;
using DemocracyWay.Services;
using DemocracyWay.UI;

namespace DemocracyWay.Setup
{
    /// <summary>
    /// Phase 2 of the one-shot Setup: every prefab, built against the REAL
    /// components in Scripts/UI and Scripts/Gameplay, with every serialized
    /// reference wired through the shouting SetRef helper. Each builder skips
    /// itself when its prefab already exists and loads its dependencies from
    /// disk by path, so a partially re-run Setup never holds stale objects.
    /// </summary>
    internal static class SetupPrefabs
    {
        public static void CreateAll()
        {
            // Ordered by dependency: UiButton feeds almost everything, the
            // pause menu nests Settings + Confirm, Systems needs the pause
            // menu prefab and the GameConfig asset (phase 1).
            CreateUiButton();
            CreateConfirmDialog();
            CreateSettingsPanel();
            CreateSaveSlotRow();
            CreateSaveSlotPanel();
            CreateTooltipView();
            CreatePauseMenuPanel();
            CreateCreationOptionButton();
            CreateDialogueChoiceButton();
            CreateDialoguePanel();
            CreateIndicatorRow();
            CreateComicPanel();
            CreateSystems();
        }

        // ═════════════════════════════════════════════════════════════════════
        // UiButton
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The one menu button: transparent hit area, TMP label, and a single
        /// gold underline Image as the hover border — a single Image because
        /// UiButton animates border alpha on ONE Image.color, so a multi-part
        /// frame would only half-fade.
        /// </summary>
        private static void CreateUiButton()
        {
            if (SetupCommon.SkipIfExists(SetupPaths.UiButtonPrefab)) return;

            var root = new GameObject("UiButton",
                typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(UiButton));
            ((RectTransform)root.transform).sizeDelta = new Vector2(480f, 64f);

            var hit = root.GetComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);   // invisible, but raycastable
            hit.raycastTarget = true;

            root.GetComponent<LayoutElement>().preferredHeight = 64f;

            var borderGO = new GameObject("Border", typeof(RectTransform), typeof(Image));
            borderGO.transform.SetParent(root.transform, false);
            var borderRect = (RectTransform)borderGO.transform;
            borderRect.anchorMin = new Vector2(0.02f, 0f);
            borderRect.anchorMax = new Vector2(0.98f, 0f);
            borderRect.pivot = new Vector2(0.5f, 0f);
            borderRect.anchoredPosition = new Vector2(0f, 2f);
            borderRect.sizeDelta = new Vector2(0f, 3f);
            var border = borderGO.GetComponent<Image>();
            border.color = new Color(SetupCommon.Gold.r, SetupCommon.Gold.g, SetupCommon.Gold.b, 0f);
            border.raycastTarget = false;

            var label = SetupCommon.CreateLabel(root.transform, "Label", string.Empty, 30,
                Vector2.zero, Vector2.one, TextAlignmentOptions.Center, SetupCommon.White);
            var labelRect = (RectTransform)label.transform;
            labelRect.offsetMin = new Vector2(10f, 6f);
            labelRect.offsetMax = new Vector2(-10f, -6f);

            var so = new SerializedObject(root.GetComponent<UiButton>());
            SetupCommon.SetRef(so, "border", border, "UiButton prefab");
            SetupCommon.SetRef(so, "label", label, "UiButton prefab");
            SetupCommon.SetRef(so, "hitTarget", hit, "UiButton prefab");
            so.ApplyModifiedPropertiesWithoutUndo();

            SetupCommon.SaveNewPrefab(root, SetupPaths.UiButtonPrefab);
        }

        // ═════════════════════════════════════════════════════════════════════
        // ConfirmDialog
        // ═════════════════════════════════════════════════════════════════════

        private static void CreateConfirmDialog()
        {
            if (SetupCommon.SkipIfExists(SetupPaths.ConfirmDialogPrefab)) return;
            var uiButton = SetupCommon.Load<GameObject>(SetupPaths.UiButtonPrefab);

            var root = new GameObject("ConfirmDialog",
                typeof(RectTransform), typeof(Image), typeof(ConfirmDialog));
            SetupCommon.StretchFull((RectTransform)root.transform);

            // The dim IS the root image: full-screen and raycastable so nothing
            // behind the dialog can be clicked while it is open.
            var dim = root.GetComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.75f);
            dim.raycastTarget = true;

            var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(root.transform, false);
            SetupCommon.AnchorPoint((RectTransform)box.transform, new Vector2(0.5f, 0.5f), new Vector2(780f, 300f));
            box.GetComponent<Image>().color = new Color(0.03f, 0.04f, 0.06f, 0.97f);

            var message = SetupCommon.CreateLabel(box.transform, "Message", string.Empty, 28,
                new Vector2(0.06f, 0.45f), new Vector2(0.94f, 0.90f),
                TextAlignmentOptions.Center, SetupCommon.White, wrap: true);

            var yes = SetupCommon.InstantiateUiButton(uiButton, box.transform, "YesButton", "Ναι");
            SetupCommon.AnchorPoint((RectTransform)yes.transform, new Vector2(0.30f, 0.22f), new Vector2(240f, 60f));

            var no = SetupCommon.InstantiateUiButton(uiButton, box.transform, "NoButton", "Όχι");
            SetupCommon.AnchorPoint((RectTransform)no.transform, new Vector2(0.70f, 0.22f), new Vector2(240f, 60f));

            var so = new SerializedObject(root.GetComponent<ConfirmDialog>());
            SetupCommon.SetRef(so, "dimImage", dim, "ConfirmDialog prefab");
            SetupCommon.SetRef(so, "messageText", message, "ConfirmDialog prefab");
            SetupCommon.SetRef(so, "yesButton", yes.GetComponent<UiButton>(), "ConfirmDialog prefab");
            SetupCommon.SetRef(so, "noButton", no.GetComponent<UiButton>(), "ConfirmDialog prefab");
            so.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);   // contract: saved inactive, Show() opens it
            SetupCommon.SaveNewPrefab(root, SetupPaths.ConfirmDialogPrefab);
        }

        // ═════════════════════════════════════════════════════════════════════
        // SettingsPanel
        // ═════════════════════════════════════════════════════════════════════

        private static void CreateSettingsPanel()
        {
            if (SetupCommon.SkipIfExists(SetupPaths.SettingsPanelPrefab)) return;
            var uiButton = SetupCommon.Load<GameObject>(SetupPaths.UiButtonPrefab);

            var root = new GameObject("SettingsPanel",
                typeof(RectTransform), typeof(Image), typeof(SettingsPanel));
            SetupCommon.StretchFull((RectTransform)root.transform);
            var bg = root.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.88f);
            bg.raycastTarget = true;   // swallow clicks meant for whatever is behind

            var column = SetupCommon.CreateVerticalColumn(root.transform, "Column",
                new Vector2(0.30f, 0.10f), new Vector2(0.70f, 0.90f), 14f, TextAnchor.UpperCenter);
            var columnLayout = column.GetComponent<VerticalLayoutGroup>();
            columnLayout.padding = new RectOffset(24, 24, 24, 24);

            AddColumnTitle(column, "Ρυθμίσεις");

            var fullscreenRow = CreateSettingsRow(column, "FullscreenRow", "Πλήρης Οθόνη", 48f);
            var fullscreenToggle = SetupCommon.CreateToggle(fullscreenRow);

            var resolutionRow = CreateSettingsRow(column, "ResolutionRow", "Ανάλυση", 52f);
            var resolutionDropdown = SetupCommon.CreateTmpDropdown(resolutionRow);

            var musicRow = CreateSettingsRow(column, "MusicRow", "Ένταση Μουσικής", 48f);
            var musicSlider = SetupCommon.CreateSlider(musicRow);

            var sfxRow = CreateSettingsRow(column, "SfxRow", "Ένταση Ηχητικών Εφέ", 48f);
            var sfxSlider = SetupCommon.CreateSlider(sfxRow);

            var voiceRow = CreateSettingsRow(column, "VoiceRow", "Ένταση Ομιλίας", 48f);
            var voiceSlider = SetupCommon.CreateSlider(voiceRow);

            var back = SetupCommon.InstantiateUiButton(uiButton, column, "BackButton", "Πίσω");

            var so = new SerializedObject(root.GetComponent<SettingsPanel>());
            SetupCommon.SetRef(so, "fullscreenToggle", fullscreenToggle, "SettingsPanel prefab");
            SetupCommon.SetRef(so, "resolutionDropdown", resolutionDropdown, "SettingsPanel prefab");
            SetupCommon.SetRef(so, "musicSlider", musicSlider, "SettingsPanel prefab");
            SetupCommon.SetRef(so, "sfxSlider", sfxSlider, "SettingsPanel prefab");
            SetupCommon.SetRef(so, "voiceSlider", voiceSlider, "SettingsPanel prefab");
            SetupCommon.SetRef(so, "backButton", back.GetComponent<UiButton>(), "SettingsPanel prefab");
            so.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);   // contract: saved inactive, Open() shows it
            SetupCommon.SaveNewPrefab(root, SetupPaths.SettingsPanelPrefab);
        }

        private static void AddColumnTitle(Transform column, string text)
        {
            var title = SetupCommon.CreateLabel(column, "Title", text, 44,
                Vector2.zero, Vector2.one, TextAlignmentOptions.Center, SetupCommon.Cream, bold: true);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
        }

        /// <summary>One settings row: Greek label on the left (flexible), the
        /// control on the right — the horizontal group keeps controls aligned
        /// without any hand-placed anchors.</summary>
        private static Transform CreateSettingsRow(Transform column, string name, string labelText, float height)
        {
            var row = new GameObject(name,
                typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(column, false);
            row.GetComponent<LayoutElement>().preferredHeight = height;

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            var label = SetupCommon.CreateLabel(row.transform, "RowLabel", labelText, 26,
                Vector2.zero, Vector2.one, TextAlignmentOptions.MidlineLeft, SetupCommon.White);
            var labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1f;
            labelLayout.preferredHeight = height;

            return row.transform;
        }

        // ═════════════════════════════════════════════════════════════════════
        // SaveSlotRow
        // ═════════════════════════════════════════════════════════════════════

        private static void CreateSaveSlotRow()
        {
            if (SetupCommon.SkipIfExists(SetupPaths.SaveSlotRowPrefab)) return;
            var uiButton = SetupCommon.Load<GameObject>(SetupPaths.UiButtonPrefab);

            var root = new GameObject("SaveSlotRow",
                typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(SaveSlotRow));
            ((RectTransform)root.transform).sizeDelta = new Vector2(1100f, 120f);
            var rowBg = root.GetComponent<Image>();
            rowBg.color = new Color(1f, 1f, 1f, 0.05f);
            rowBg.raycastTarget = false;   // clicks belong to the pick/delete buttons
            root.GetComponent<LayoutElement>().preferredHeight = 120f;

            // Pick button FIRST in sibling order: it must sit UNDER the delete
            // button in raycast priority, so Διαγραφή can win where they overlap.
            // Built from scratch (not the UiButton prefab): no label, no border —
            // a pure transparent hit surface over the whole row.
            var pickGO = new GameObject("PickButton", typeof(RectTransform), typeof(Image), typeof(UiButton));
            pickGO.transform.SetParent(root.transform, false);
            SetupCommon.StretchFull((RectTransform)pickGO.transform);
            var pickHit = pickGO.GetComponent<Image>();
            pickHit.color = new Color(0f, 0f, 0f, 0f);
            pickHit.raycastTarget = true;
            var pickSo = new SerializedObject(pickGO.GetComponent<UiButton>());
            SetupCommon.SetRef(pickSo, "hitTarget", pickHit, "SaveSlotRow prefab (PickButton)");
            pickSo.ApplyModifiedPropertiesWithoutUndo();

            var slotTitle = SetupCommon.CreateLabel(root.transform, "SlotTitle", "Θέση 1", 26,
                new Vector2(0.02f, 0.52f), new Vector2(0.17f, 0.94f),
                TextAlignmentOptions.MidlineLeft, SetupCommon.Cream, bold: true);

            var emptyLabel = SetupCommon.CreateLabel(root.transform, "EmptyLabel", "Κενή Θέση", 26,
                new Vector2(0.20f, 0.30f), new Vector2(0.80f, 0.72f),
                TextAlignmentOptions.MidlineLeft, SetupCommon.Muted);

            // Everything that only means something on an occupied slot lives
            // under one parent, so Bind can toggle it with a single SetActive.
            var occupied = new GameObject("OccupiedGroup", typeof(RectTransform));
            occupied.transform.SetParent(root.transform, false);
            SetupCommon.StretchFull((RectTransform)occupied.transform);

            var profile = SetupCommon.CreateLabel(occupied.transform, "ProfileText", string.Empty, 24,
                new Vector2(0.19f, 0.52f), new Vector2(0.58f, 0.94f),
                TextAlignmentOptions.MidlineLeft, SetupCommon.White);

            var chapter = SetupCommon.CreateLabel(occupied.transform, "ChapterText", string.Empty, 20,
                new Vector2(0.19f, 0.08f), new Vector2(0.58f, 0.48f),
                TextAlignmentOptions.MidlineLeft, SetupCommon.Muted);

            var calendar = SetupCommon.CreateLabel(occupied.transform, "CalendarText", string.Empty, 20,
                new Vector2(0.59f, 0.52f), new Vector2(0.83f, 0.94f),
                TextAlignmentOptions.MidlineLeft, SetupCommon.Cream);

            var playtime = SetupCommon.CreateLabel(occupied.transform, "PlaytimeText", string.Empty, 20,
                new Vector2(0.59f, 0.08f), new Vector2(0.83f, 0.48f),
                TextAlignmentOptions.MidlineLeft, SetupCommon.Muted);

            var savedAt = SetupCommon.CreateLabel(occupied.transform, "SavedAtText", string.Empty, 16,
                new Vector2(0.02f, 0.08f), new Vector2(0.17f, 0.48f),
                TextAlignmentOptions.MidlineLeft, SetupCommon.Muted);

            var delete = SetupCommon.InstantiateUiButton(uiButton, occupied.transform,
                "DeleteButton", "Διαγραφή", labelFontSize: 22f);
            SetupCommon.Anchor((RectTransform)delete.transform,
                new Vector2(0.845f, 0.28f), new Vector2(0.985f, 0.72f));

            var so = new SerializedObject(root.GetComponent<SaveSlotRow>());
            SetupCommon.SetRef(so, "slotTitleText", slotTitle, "SaveSlotRow prefab");
            SetupCommon.SetRef(so, "emptyLabelText", emptyLabel, "SaveSlotRow prefab");
            SetupCommon.SetRef(so, "occupiedGroup", occupied, "SaveSlotRow prefab");
            SetupCommon.SetRef(so, "profileText", profile, "SaveSlotRow prefab");
            SetupCommon.SetRef(so, "chapterText", chapter, "SaveSlotRow prefab");
            SetupCommon.SetRef(so, "calendarText", calendar, "SaveSlotRow prefab");
            SetupCommon.SetRef(so, "playtimeText", playtime, "SaveSlotRow prefab");
            SetupCommon.SetRef(so, "savedAtText", savedAt, "SaveSlotRow prefab");
            SetupCommon.SetRef(so, "pickButton", pickGO.GetComponent<UiButton>(), "SaveSlotRow prefab");
            SetupCommon.SetRef(so, "deleteButton", delete.GetComponent<UiButton>(), "SaveSlotRow prefab");
            so.ApplyModifiedPropertiesWithoutUndo();

            SetupCommon.SaveNewPrefab(root, SetupPaths.SaveSlotRowPrefab);
        }

        // ═════════════════════════════════════════════════════════════════════
        // SaveSlotPanel
        // ═════════════════════════════════════════════════════════════════════

        private static void CreateSaveSlotPanel()
        {
            if (SetupCommon.SkipIfExists(SetupPaths.SaveSlotPanelPrefab)) return;
            var uiButton = SetupCommon.Load<GameObject>(SetupPaths.UiButtonPrefab);
            var rowPrefab = SetupCommon.Load<GameObject>(SetupPaths.SaveSlotRowPrefab);

            var root = new GameObject("SaveSlotPanel",
                typeof(RectTransform), typeof(Image), typeof(SaveSlotPanel));
            SetupCommon.StretchFull((RectTransform)root.transform);
            var bg = root.GetComponent<Image>();
            bg.color = new Color(0.01f, 0.02f, 0.04f, 0.95f);
            bg.raycastTarget = true;

            SetupCommon.CreateLabel(root.transform, "Title", "Θέσεις Αποθήκευσης", 48,
                new Vector2(0.10f, 0.87f), new Vector2(0.90f, 0.96f),
                TextAlignmentOptions.Center, SetupCommon.Cream, bold: true);

            var container = SetupCommon.CreateVerticalColumn(root.transform, "RowContainer",
                new Vector2(0.14f, 0.20f), new Vector2(0.86f, 0.85f), 16f, TextAnchor.UpperCenter);

            var back = SetupCommon.InstantiateUiButton(uiButton, root.transform, "BackButton", "Πίσω");
            SetupCommon.Anchor((RectTransform)back.transform,
                new Vector2(0.42f, 0.07f), new Vector2(0.58f, 0.145f));

            var so = new SerializedObject(root.GetComponent<SaveSlotPanel>());
            SetupCommon.SetRef(so, "rowPrefab", rowPrefab != null ? rowPrefab.GetComponent<SaveSlotRow>() : null, "SaveSlotPanel prefab");
            SetupCommon.SetRef(so, "rowContainer", container, "SaveSlotPanel prefab");
            SetupCommon.SetRef(so, "backButton", back.GetComponent<UiButton>(), "SaveSlotPanel prefab");
            // confirmDialog is deliberately left null here: the panel uses the
            // scene-shared ConfirmDialog, wired per scene instance (MainMenu).
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[Setup] SaveSlotPanel: το confirmDialog συνδέεται ανά σκηνή (κοινός διάλογος) — σκόπιμα κενό στο prefab.");

            root.SetActive(false);   // contract: saved inactive
            SetupCommon.SaveNewPrefab(root, SetupPaths.SaveSlotPanelPrefab);
        }

        // ═════════════════════════════════════════════════════════════════════
        // TooltipView
        // ═════════════════════════════════════════════════════════════════════

        private static void CreateTooltipView()
        {
            if (SetupCommon.SkipIfExists(SetupPaths.TooltipViewPrefab)) return;

            var root = new GameObject("TooltipView",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter), typeof(TooltipView));
            var rect = (RectTransform)root.transform;
            rect.pivot = new Vector2(0f, 1f);   // hangs right-and-down from the pointer
            rect.sizeDelta = new Vector2(340f, 80f);

            var bg = root.GetComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.08f, 0.95f);
            // raycastTarget is forced off in TooltipView.Awake — author it off too.
            bg.raycastTarget = false;

            var layout = root.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // Size-to-content so Show()'s ForceRebuildLayoutImmediate measures
            // the REAL bubble before clamping it inside the canvas.
            var fitter = root.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var text = SetupCommon.CreateLabel(root.transform, "Text", string.Empty, 20,
                Vector2.zero, Vector2.one, TextAlignmentOptions.TopLeft, SetupCommon.White, wrap: true);
            text.gameObject.AddComponent<LayoutElement>().preferredWidth = 320f;

            var so = new SerializedObject(root.GetComponent<TooltipView>());
            SetupCommon.SetRef(so, "background", bg, "TooltipView prefab");
            SetupCommon.SetRef(so, "text", text, "TooltipView prefab");
            so.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);   // contract: saved inactive, callers Show/Hide
            SetupCommon.SaveNewPrefab(root, SetupPaths.TooltipViewPrefab);
        }

        // ═════════════════════════════════════════════════════════════════════
        // PauseMenuPanel
        // ═════════════════════════════════════════════════════════════════════

        private static void CreatePauseMenuPanel()
        {
            if (SetupCommon.SkipIfExists(SetupPaths.PauseMenuPanelPrefab)) return;
            var uiButton = SetupCommon.Load<GameObject>(SetupPaths.UiButtonPrefab);
            var settingsPrefab = SetupCommon.Load<GameObject>(SetupPaths.SettingsPanelPrefab);
            var confirmPrefab = SetupCommon.Load<GameObject>(SetupPaths.ConfirmDialogPrefab);

            // Own Canvas: the pause menu is instantiated under the persistent
            // Systems object, far from any scene canvas — it brings its own.
            var root = new GameObject("PauseMenuPanel",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PauseMenuPanel));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9000;   // code re-enforces this in Awake
            SetupCommon.ConfigureScaler(root.GetComponent<CanvasScaler>());

            // Dim + title + buttons under ONE parent so opening Ρυθμίσεις can
            // hide the whole top level with a single SetActive.
            var buttonColumn = new GameObject("ButtonColumn", typeof(RectTransform));
            buttonColumn.transform.SetParent(root.transform, false);
            SetupCommon.StretchFull((RectTransform)buttonColumn.transform);

            var dimGO = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dimGO.transform.SetParent(buttonColumn.transform, false);
            SetupCommon.StretchFull((RectTransform)dimGO.transform);
            var dim = dimGO.GetComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.72f);
            dim.raycastTarget = true;   // the frozen scene must not receive hovers

            SetupCommon.CreateLabel(buttonColumn.transform, "Title", "Παύση", 56,
                new Vector2(0.30f, 0.68f), new Vector2(0.70f, 0.78f),
                TextAlignmentOptions.Center, SetupCommon.Cream, bold: true);

            var column = SetupCommon.CreateVerticalColumn(buttonColumn.transform, "Column",
                new Vector2(0.38f, 0.26f), new Vector2(0.62f, 0.64f), 18f, TextAnchor.MiddleCenter);

            var resume = SetupCommon.InstantiateUiButton(uiButton, column, "ResumeButton", "Συνέχεια");
            var settingsBtn = SetupCommon.InstantiateUiButton(uiButton, column, "SettingsButton", "Ρυθμίσεις");
            var mainMenu = SetupCommon.InstantiateUiButton(uiButton, column, "MainMenuButton", "Επιστροφή στην Αρχική");
            var quit = SetupCommon.InstantiateUiButton(uiButton, column, "QuitButton", "Έξοδος");

            var settingsPanel = (GameObject)PrefabUtility.InstantiatePrefab(settingsPrefab, root.transform);
            settingsPanel.name = "SettingsPanel";   // saved inactive by its own prefab

            var confirm = (GameObject)PrefabUtility.InstantiatePrefab(confirmPrefab, root.transform);
            confirm.name = "ConfirmDialog";   // saved inactive by its own prefab

            var so = new SerializedObject(root.GetComponent<PauseMenuPanel>());
            SetupCommon.SetRef(so, "canvas", canvas, "PauseMenuPanel prefab");
            SetupCommon.SetRef(so, "buttonColumn", buttonColumn, "PauseMenuPanel prefab");
            SetupCommon.SetRef(so, "resumeButton", resume.GetComponent<UiButton>(), "PauseMenuPanel prefab");
            SetupCommon.SetRef(so, "settingsButton", settingsBtn.GetComponent<UiButton>(), "PauseMenuPanel prefab");
            SetupCommon.SetRef(so, "mainMenuButton", mainMenu.GetComponent<UiButton>(), "PauseMenuPanel prefab");
            SetupCommon.SetRef(so, "quitButton", quit.GetComponent<UiButton>(), "PauseMenuPanel prefab");
            SetupCommon.SetRef(so, "settingsPanel", settingsPanel.GetComponent<SettingsPanel>(), "PauseMenuPanel prefab");
            SetupCommon.SetRef(so, "confirmDialog", confirm.GetComponent<ConfirmDialog>(), "PauseMenuPanel prefab");
            so.ApplyModifiedPropertiesWithoutUndo();

            // Contract: the ROOT is saved inactive — PauseService instantiates
            // it and immediately calls Show().
            root.SetActive(false);
            SetupCommon.SaveNewPrefab(root, SetupPaths.PauseMenuPanelPrefab);
        }

        // ═════════════════════════════════════════════════════════════════════
        // CreationOptionButton / DialogueChoiceButton
        // ═════════════════════════════════════════════════════════════════════

        private static void CreateCreationOptionButton()
        {
            if (SetupCommon.SkipIfExists(SetupPaths.CreationOptionButtonPrefab)) return;

            var root = new GameObject("CreationOptionButton",
                typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(CreationOptionButton));
            ((RectTransform)root.transform).sizeDelta = new Vector2(620f, 64f);
            var bg = root.GetComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.05f);   // matches the component's idleColor default
            bg.raycastTarget = true;
            root.GetComponent<LayoutElement>().preferredHeight = 64f;

            var label = SetupCommon.CreateLabel(root.transform, "Label", string.Empty, 26,
                Vector2.zero, Vector2.one, TextAlignmentOptions.MidlineLeft, SetupCommon.White);
            var labelRect = (RectTransform)label.transform;
            labelRect.offsetMin = new Vector2(20f, 4f);
            labelRect.offsetMax = new Vector2(-20f, -4f);

            var so = new SerializedObject(root.GetComponent<CreationOptionButton>());
            SetupCommon.SetRef(so, "label", label, "CreationOptionButton prefab");
            SetupCommon.SetRef(so, "background", bg, "CreationOptionButton prefab");
            so.ApplyModifiedPropertiesWithoutUndo();

            SetupCommon.SaveNewPrefab(root, SetupPaths.CreationOptionButtonPrefab);
        }

        private static void CreateDialogueChoiceButton()
        {
            if (SetupCommon.SkipIfExists(SetupPaths.DialogueChoiceButtonPrefab)) return;

            var root = new GameObject("DialogueChoiceButton",
                typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(DialogueChoiceButton));
            ((RectTransform)root.transform).sizeDelta = new Vector2(900f, 38f);
            var bg = root.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);   // matches the component's idleColor default
            bg.raycastTarget = true;
            root.GetComponent<LayoutElement>().preferredHeight = 38f;

            var label = SetupCommon.CreateLabel(root.transform, "Label", string.Empty, 20,
                Vector2.zero, Vector2.one, TextAlignmentOptions.MidlineLeft, SetupCommon.White, wrap: true);
            var labelRect = (RectTransform)label.transform;
            labelRect.offsetMin = new Vector2(16f, 2f);
            labelRect.offsetMax = new Vector2(-16f, -2f);

            var so = new SerializedObject(root.GetComponent<DialogueChoiceButton>());
            SetupCommon.SetRef(so, "label", label, "DialogueChoiceButton prefab");
            SetupCommon.SetRef(so, "background", bg, "DialogueChoiceButton prefab");
            so.ApplyModifiedPropertiesWithoutUndo();

            SetupCommon.SaveNewPrefab(root, SetupPaths.DialogueChoiceButtonPrefab);
        }

        // ═════════════════════════════════════════════════════════════════════
        // DialoguePanel
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// DialogueRunner lives on the ACTIVE prefab root (its Update must poll
        /// for advance input); the visible panel is the "PanelRoot" child that
        /// the runner force-hides in Awake and shows per dialogue — so the
        /// scene instance never needs manual activation.
        /// </summary>
        private static void CreateDialoguePanel()
        {
            if (SetupCommon.SkipIfExists(SetupPaths.DialoguePanelPrefab)) return;
            var choicePrefab = SetupCommon.Load<GameObject>(SetupPaths.DialogueChoiceButtonPrefab);

            var root = new GameObject("DialoguePanel", typeof(RectTransform), typeof(DialogueRunner));
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0.16f, 0f);
            rect.anchorMax = new Vector2(0.84f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 28f);
            rect.sizeDelta = new Vector2(0f, 320f);

            var panelRoot = new GameObject("PanelRoot", typeof(RectTransform), typeof(Image));
            panelRoot.transform.SetParent(root.transform, false);
            SetupCommon.StretchFull((RectTransform)panelRoot.transform);
            var panelBg = panelRoot.GetComponent<Image>();
            panelBg.color = new Color(0f, 0f, 0f, 0.82f);
            panelBg.raycastTarget = true;

            var portraitGO = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGO.transform.SetParent(panelRoot.transform, false);
            var portraitRect = (RectTransform)portraitGO.transform;
            portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(0f, 1f);
            portraitRect.pivot = new Vector2(0f, 1f);
            portraitRect.anchoredPosition = new Vector2(18f, -18f);
            portraitRect.sizeDelta = new Vector2(96f, 96f);
            var portrait = portraitGO.GetComponent<Image>();
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;

            var speaker = SetupCommon.CreateLabel(panelRoot.transform, "SpeakerName", string.Empty, 26,
                new Vector2(0.14f, 0.82f), new Vector2(0.62f, 0.96f),
                TextAlignmentOptions.MidlineLeft, SetupCommon.Cream, bold: true);

            var body = SetupCommon.CreateLabel(panelRoot.transform, "Body", string.Empty, 22,
                new Vector2(0.14f, 0.40f), new Vector2(0.97f, 0.80f),
                TextAlignmentOptions.TopLeft, SetupCommon.White, wrap: true);

            var choices = SetupCommon.CreateVerticalColumn(panelRoot.transform, "ChoicesContainer",
                new Vector2(0.36f, 0.03f), new Vector2(0.97f, 0.40f), 6f, TextAnchor.LowerCenter);

            var hint = SetupCommon.CreateLabel(panelRoot.transform, "AdvanceHint", "συνέχεια ▸", 20,
                new Vector2(0.70f, 0.02f), new Vector2(0.97f, 0.13f),
                TextAlignmentOptions.MidlineRight, SetupCommon.Muted);

            var so = new SerializedObject(root.GetComponent<DialogueRunner>());
            SetupCommon.SetRef(so, "panelRoot", panelRoot, "DialoguePanel prefab");
            SetupCommon.SetRef(so, "portraitImage", portrait, "DialoguePanel prefab");
            SetupCommon.SetRef(so, "speakerNameText", speaker, "DialoguePanel prefab");
            SetupCommon.SetRef(so, "bodyText", body, "DialoguePanel prefab");
            SetupCommon.SetRef(so, "choicesContainer", choices, "DialoguePanel prefab");
            SetupCommon.SetRef(so, "choiceButtonPrefab", choicePrefab != null ? choicePrefab.GetComponent<DialogueChoiceButton>() : null, "DialoguePanel prefab");
            SetupCommon.SetRef(so, "advanceHintText", hint, "DialoguePanel prefab");
            so.ApplyModifiedPropertiesWithoutUndo();

            // Root stays ACTIVE (the runner's Update polls input); PanelRoot is
            // authored visible for editing comfort — Awake hides it at runtime.
            SetupCommon.SaveNewPrefab(root, SetupPaths.DialoguePanelPrefab);
        }

        // ═════════════════════════════════════════════════════════════════════
        // IndicatorRow
        // ═════════════════════════════════════════════════════════════════════

        private static void CreateIndicatorRow()
        {
            if (SetupCommon.SkipIfExists(SetupPaths.IndicatorRowPrefab)) return;

            var root = new GameObject("IndicatorRow",
                typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(IndicatorRowView));
            ((RectTransform)root.transform).sizeDelta = new Vector2(340f, 46f);
            var bg = root.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.30f);
            // The row itself receives the tooltip hover — it needs a raycastable graphic.
            bg.raycastTarget = true;
            root.GetComponent<LayoutElement>().preferredHeight = 46f;

            var nameText = SetupCommon.CreateLabel(root.transform, "Name", string.Empty, 20,
                new Vector2(0.05f, 0.42f), new Vector2(0.66f, 0.96f),
                TextAlignmentOptions.MidlineLeft, SetupCommon.White);

            var valueText = SetupCommon.CreateLabel(root.transform, "Value", "50", 20,
                new Vector2(0.66f, 0.42f), new Vector2(0.95f, 0.96f),
                TextAlignmentOptions.MidlineRight, SetupCommon.Cream, bold: true);

            var trackGO = new GameObject("Track", typeof(RectTransform), typeof(Image));
            trackGO.transform.SetParent(root.transform, false);
            SetupCommon.Anchor((RectTransform)trackGO.transform,
                new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.32f));
            var track = trackGO.GetComponent<Image>();
            track.color = new Color(1f, 1f, 1f, 0.12f);
            track.raycastTarget = false;

            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(trackGO.transform, false);
            SetupCommon.StretchFull((RectTransform)fillGO.transform);
            var fill = fillGO.GetComponent<Image>();
            fill.color = SetupCommon.Gold;
            fill.raycastTarget = false;
            // Filled type: IndicatorRowView drives fillAmount directly.
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0.5f;

            var so = new SerializedObject(root.GetComponent<IndicatorRowView>());
            SetupCommon.SetRef(so, "nameText", nameText, "IndicatorRow prefab");
            SetupCommon.SetRef(so, "valueText", valueText, "IndicatorRow prefab");
            SetupCommon.SetRef(so, "fillImage", fill, "IndicatorRow prefab");
            so.ApplyModifiedPropertiesWithoutUndo();

            SetupCommon.SaveNewPrefab(root, SetupPaths.IndicatorRowPrefab);
        }

        // ═════════════════════════════════════════════════════════════════════
        // ComicPanel
        // ═════════════════════════════════════════════════════════════════════

        private static void CreateComicPanel()
        {
            if (SetupCommon.SkipIfExists(SetupPaths.ComicPanelPrefab)) return;

            // ComicPlayer finds Image + CanvasGroup with GetComponent on the
            // root: keep the prefab exactly that shape, alpha 0 so the grid is
            // laid out before any panel is revealed.
            var root = new GameObject("ComicPanel",
                typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            ((RectTransform)root.transform).sizeDelta = new Vector2(560f, 410f);
            var image = root.GetComponent<Image>();
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            root.GetComponent<CanvasGroup>().alpha = 0f;

            SetupCommon.SaveNewPrefab(root, SetupPaths.ComicPanelPrefab);
        }

        // ═════════════════════════════════════════════════════════════════════
        // Systems
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The persistent Systems prefab: ServicesRoot on top, one child per
        /// service, and the SceneFlow overlay authored FULLY BLACK (alpha 1)
        /// so the very first frame of the game is black instead of a naked
        /// half-loaded scene.
        /// </summary>
        private static void CreateSystems()
        {
            if (SetupCommon.SkipIfExists(SetupPaths.SystemsPrefab)) return;

            var config = SetupCommon.Load<GameConfig>(SetupPaths.GameConfigAsset);
            var pauseMenuPrefab = SetupCommon.Load<GameObject>(SetupPaths.PauseMenuPanelPrefab);

            var root = new GameObject("Systems", typeof(ServicesRoot));

            // Audio — the AudioListener lives HERE because this object survives
            // scene loads while every scene camera dies with its scene.
            var audioGO = new GameObject("AudioService", typeof(AudioListener), typeof(AudioService));
            audioGO.transform.SetParent(root.transform, false);
            var audio = audioGO.GetComponent<AudioService>();

            var settingsGO = new GameObject("SettingsService", typeof(SettingsService));
            settingsGO.transform.SetParent(root.transform, false);
            var settings = settingsGO.GetComponent<SettingsService>();

            var sessionGO = new GameObject("SessionService", typeof(SessionService));
            sessionGO.transform.SetParent(root.transform, false);
            var session = sessionGO.GetComponent<SessionService>();

            var cursorGO = new GameObject("CursorService", typeof(CursorService));
            cursorGO.transform.SetParent(root.transform, false);
            var cursor = cursorGO.GetComponent<CursorService>();

            var pauseGO = new GameObject("PauseService", typeof(PauseService));
            pauseGO.transform.SetParent(root.transform, false);
            var pause = pauseGO.GetComponent<PauseService>();
            var pauseSo = new SerializedObject(pause);
            SetupCommon.SetRef(pauseSo, "pauseMenuPrefab", pauseMenuPrefab, "Systems prefab (PauseService)");
            pauseSo.ApplyModifiedPropertiesWithoutUndo();

            // SceneFlow + its persistent overlay canvas.
            var flowGO = new GameObject("SceneFlow", typeof(SceneFlowService));
            flowGO.transform.SetParent(root.transform, false);
            var flow = flowGO.GetComponent<SceneFlowService>();

            var overlayGO = new GameObject("OverlayCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            overlayGO.transform.SetParent(flowGO.transform, false);
            var overlayCanvas = overlayGO.GetComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 9999;   // code re-enforces this in Awake
            SetupCommon.ConfigureScaler(overlayGO.GetComponent<CanvasScaler>());

            var fadeGO = new GameObject("Fade", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            fadeGO.transform.SetParent(overlayGO.transform, false);
            SetupCommon.StretchFull((RectTransform)fadeGO.transform);
            var fadeImage = fadeGO.GetComponent<Image>();
            fadeImage.color = Color.black;
            fadeImage.raycastTarget = true;
            var fadeGroup = fadeGO.GetComponent<CanvasGroup>();
            fadeGroup.alpha = 1f;   // STARTS BLACK — the Boot reveal is the first fade

            var chapterTitle = SetupCommon.CreateLabel(fadeGO.transform, "ChapterTitle", string.Empty, 56,
                new Vector2(0.08f, 0.40f), new Vector2(0.92f, 0.60f),
                TextAlignmentOptions.Center, SetupCommon.Cream, bold: true);

            var loadingGO = new GameObject("Loading", typeof(RectTransform));
            loadingGO.transform.SetParent(fadeGO.transform, false);
            var loadingRect = (RectTransform)loadingGO.transform;
            loadingRect.anchorMin = loadingRect.anchorMax = new Vector2(1f, 0f);
            loadingRect.pivot = new Vector2(1f, 0f);
            loadingRect.anchoredPosition = new Vector2(-40f, 32f);
            loadingRect.sizeDelta = new Vector2(300f, 44f);
            SetupCommon.CreateLabel(loadingGO.transform, "LoadingText", "Φόρτωση...", 24,
                Vector2.zero, Vector2.one, TextAlignmentOptions.MidlineRight, SetupCommon.Muted);
            loadingGO.SetActive(false);

            var flowSo = new SerializedObject(flow);
            SetupCommon.SetRef(flowSo, "overlayCanvas", overlayCanvas, "Systems prefab (SceneFlow)");
            SetupCommon.SetRef(flowSo, "fadeGroup", fadeGroup, "Systems prefab (SceneFlow)");
            SetupCommon.SetRef(flowSo, "chapterTitleText", chapterTitle, "Systems prefab (SceneFlow)");
            SetupCommon.SetRef(flowSo, "loadingGroup", loadingGO, "Systems prefab (SceneFlow)");
            flowSo.ApplyModifiedPropertiesWithoutUndo();

            // ServicesRoot: the config + the six service references.
            var rootSo = new SerializedObject(root.GetComponent<ServicesRoot>());
            SetupCommon.SetRef(rootSo, "config", config, "Systems prefab (ServicesRoot)");
            SetupCommon.SetRef(rootSo, "audioService", audio, "Systems prefab (ServicesRoot)");
            SetupCommon.SetRef(rootSo, "settingsService", settings, "Systems prefab (ServicesRoot)");
            SetupCommon.SetRef(rootSo, "sessionService", session, "Systems prefab (ServicesRoot)");
            SetupCommon.SetRef(rootSo, "sceneFlowService", flow, "Systems prefab (ServicesRoot)");
            SetupCommon.SetRef(rootSo, "cursorService", cursor, "Systems prefab (ServicesRoot)");
            SetupCommon.SetRef(rootSo, "pauseService", pause, "Systems prefab (ServicesRoot)");
            rootSo.ApplyModifiedPropertiesWithoutUndo();

            SetupCommon.SaveNewPrefab(root, SetupPaths.SystemsPrefab);
        }
    }
}
#endif
