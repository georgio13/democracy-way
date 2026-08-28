using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using DemocracyWay.Core;

namespace DemocracyWay.UI
{
    /// <summary>
    /// The four-slot list, shared by Φόρτωση and Νέο Παιχνίδι because the two
    /// screens differ only in which rows are pickable: load wants occupied
    /// slots (plus Διαγραφή), new game wants empty ones. Rows are rebuilt from
    /// the prefab on every open/delete instead of updated in place — slots are
    /// only four, and a full rebuild can never show stale state. The GameObject
    /// is saved inactive; the caller opens it via OpenForLoad/OpenForNewGame.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Save Slot Panel")]
    [DisallowMultipleComponent]
    public class SaveSlotPanel : MonoBehaviour
    {
        private enum Mode { Load, NewGame }

        [Tooltip("Prefab μίας γραμμής θέσης (με component SaveSlotRow).")]
        [SerializeField] private SaveSlotRow rowPrefab;

        [Tooltip("Ο γονέας μέσα στον οποίο χτίζονται οι γραμμές (π.χ. Vertical Layout Group).")]
        [SerializeField] private Transform rowContainer;

        [Tooltip("Κουμπί «Πίσω» — κλείνει το panel και ειδοποιεί όποιον το άνοιξε.")]
        [SerializeField] private UiButton backButton;

        [Tooltip("Ο τίτλος του panel — παίρνει διαφορετικό κείμενο ανά λειτουργία (φόρτωση/νέο παιχνίδι).")]
        [SerializeField] private TMP_Text titleText;

        [Tooltip("Ο κοινός διάλογος επιβεβαίωσης για τη Διαγραφή.")]
        [SerializeField] private ConfirmDialog confirmDialog;

        [Tooltip("Μήνυμα επιβεβαίωσης πριν τη διαγραφή μιας αποθήκευσης.")]
        [SerializeField] private string deleteConfirmMessage = "Να διαγραφεί οριστικά αυτή η αποθήκευση;";

        [Tooltip("Τίτλος του panel σε λειτουργία φόρτωσης.")]
        [SerializeField] private string loadTitle = "Φόρτωση Παιχνιδιού";

        [Tooltip("Τίτλος του panel σε λειτουργία νέου παιχνιδιού.")]
        [SerializeField] private string newGameTitle = "Νέο Παιχνίδι";

        private Mode mode;
        private Action<int> onPicked;
        private Action onBack;

        /// <summary>Only rows WE spawned get destroyed on rebuild — the
        /// container may also hold authored decoration (headers, dividers).</summary>
        private readonly List<SaveSlotRow> spawnedRows = new List<SaveSlotRow>();

        void Awake()
        {
            if (backButton != null) backButton.onClick.AddListener(Back);
        }

        // ════════ Modes ════════

        /// <summary>Load mode: occupied rows pickable, each with Διαγραφή.
        /// Picking does NOT close the panel — the scene transition (or a
        /// failed load leaving it open for another try) handles that.</summary>
        public void OpenForLoad(Action<int> onPicked, Action onBack)
        {
            Open(Mode.Load, onPicked, onBack);
        }

        /// <summary>New-game mode: only EMPTY rows pickable, no Διαγραφή.</summary>
        public void OpenForNewGame(Action<int> onPicked, Action onBack)
        {
            Open(Mode.NewGame, onPicked, onBack);
        }

        private void Open(Mode mode, Action<int> onPicked, Action onBack)
        {
            this.mode = mode;
            this.onPicked = onPicked;
            this.onBack = onBack;
            if (titleText != null)
                titleText.text = mode == Mode.Load ? loadTitle : newGameTitle;
            gameObject.SetActive(true);
            RebuildRows();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void Back()
        {
            Hide();
            var callback = onBack;
            onBack = null;
            callback?.Invoke();
        }

        // ════════ Rows ════════

        private void RebuildRows()
        {
            foreach (var row in spawnedRows)
                if (row != null) Destroy(row.gameObject);
            spawnedRows.Clear();

            if (rowPrefab == null || rowContainer == null)
            {
                Debug.LogError("[SaveSlotPanel] Λείπει rowPrefab ή rowContainer.", this);
                return;
            }

            for (int slot = 0; slot < SaveSystem.SlotCount; slot++)
            {
                var summary = SaveSystem.Summarize(slot);
                var row = Instantiate(rowPrefab, rowContainer);
                spawnedRows.Add(row);

                // A file that exists but can't be summarized is corrupt. Show
                // it as occupied-but-unloadable and let the player delete it —
                // otherwise it silently counts toward AllFull() (locking Νέο
                // Παιχνίδι) with no way to ever clear it from the UI.
                bool fileExists = SaveSystem.Exists(slot);
                bool corrupt = fileExists && !summary.exists;
                if (corrupt)
                {
                    summary.exists = true;
                    summary.chapterTitle = "Κατεστραμμένη αποθήκευση";
                }

                bool clickable = mode == Mode.Load ? summary.exists && !corrupt : !fileExists;
                int pickedSlot = slot;   // capture per iteration, not the loop variable

                row.Bind(
                    summary,
                    clickable,
                    onPick: () => onPicked?.Invoke(pickedSlot),
                    onDelete: mode == Mode.Load && fileExists
                        ? () => ConfirmDelete(pickedSlot)
                        : (Action)null);
            }
        }

        private void ConfirmDelete(int slot)
        {
            if (confirmDialog == null)
            {
                Debug.LogError("[SaveSlotPanel] Δεν έχει συνδεθεί ConfirmDialog για τη Διαγραφή.", this);
                return;
            }
            confirmDialog.Show(deleteConfirmMessage, onYes: () =>
            {
                SaveSystem.Delete(slot);
                RebuildRows();
                // An empty load list is a dead end — return the player to the
                // menu, which also re-locks its Φόρτωση button.
                if (mode == Mode.Load && !SaveSystem.AnyExists())
                    Back();
            });
        }
    }
}
