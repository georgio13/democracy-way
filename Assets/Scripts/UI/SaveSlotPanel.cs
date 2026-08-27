using System;
using TMPro;
using UnityEngine;

namespace DemocracyWay.UI
{
    using DemocracyWay.Core;
    using DemocracyWay.Framework;
    using DemocracyWay.Menu;

    /// <summary>
    /// The four-slot save browser. Serves both flows from the main menu:
    ///
    ///   • <see cref="OpenForLoad"/>  — pick an existing save to continue.
    ///   • <see cref="OpenForSave"/>  — pick where a new game will live;
    ///                                  empty slots are selectable and taken
    ///                                  ones ask for confirmation first.
    ///
    /// Rows are instantiated from <see cref="rowPrefab"/> on first open, so the
    /// slot count is driven by <see cref="SaveSystem.SlotCount"/> rather than by
    /// however many rows someone dragged into the scene.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Save Slot Panel")]
    public class SaveSlotPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private RectTransform rowContainer;
        [SerializeField] private GameObject rowPrefab;
        [SerializeField] private MenuButton backButton;
        [SerializeField] private ConfirmDialog confirmDialog;

        [Header("Strings")]
        [SerializeField] private string loadTitle = "ΦΟΡΤΩΣΗ ΠΑΙΧΝΙΔΙΟΥ";
        [SerializeField] private string saveTitle = "ΕΠΙΛΕΞΕ ΘΕΣΗ ΑΠΟΘΗΚΕΥΣΗΣ";
        [SerializeField] private string overwritePrompt = "Η θέση {0} θα αντικατασταθεί. Συνέχεια;";
        [SerializeField] private string deletePrompt = "Να διαγραφεί οριστικά η θέση {0};";

        private SaveSlotRow[] rows;
        private Action<int> onSlotChosen;
        private Action onBack;
        private bool selectingForNewGame;

        void Awake()
        {
            if (backButton != null) backButton.onClick.AddListener(HandleBack);
            SetVisible(false);
        }

        // ════════════ Entry points ════════════

        public void OpenForLoad(Action<int> slotChosen, Action back)
        {
            selectingForNewGame = false;
            Open(loadTitle, slotChosen, back);
        }

        public void OpenForSave(Action<int> slotChosen, Action back)
        {
            selectingForNewGame = true;
            Open(saveTitle, slotChosen, back);
        }

        private void Open(string title, Action<int> slotChosen, Action back)
        {
            onSlotChosen = slotChosen;
            onBack = back;

            if (titleLabel != null) titleLabel.text = title;

            gameObject.SetActive(true);
            SetVisible(true);
            EnsureRows();
            Refresh();
        }

        public void Close()
        {
            SetVisible(false);
            gameObject.SetActive(false);
            onSlotChosen = null;
            onBack = null;
        }

        // ════════════ Rows ════════════

        private void EnsureRows()
        {
            if (rows != null && rows.Length == SaveSystem.SlotCount) return;
            if (rowContainer == null || rowPrefab == null)
            {
                Debug.LogError("[SaveSlotPanel] rowContainer/rowPrefab not wired — re-run Tools > DemocracyWay > Init.");
                rows = Array.Empty<SaveSlotRow>();
                return;
            }

            // Clear anything already parented (e.g. a design-time sample row).
            UiUtil.ClearChildren(rowContainer);

            rows = new SaveSlotRow[SaveSystem.SlotCount];
            for (int i = 0; i < rows.Length; i++)
            {
                var go = Instantiate(rowPrefab, rowContainer);
                go.name = $"SaveSlotRow_{i}";
                rows[i] = go.GetComponent<SaveSlotRow>();
            }
        }

        private void Refresh()
        {
            if (rows == null) return;
            var infos = SaveSystem.ReadAllSlots();
            for (int i = 0; i < rows.Length && i < infos.Length; i++)
            {
                if (rows[i] == null) continue;
                rows[i].SetEmptySlotSelectable(selectingForNewGame);
                rows[i].Bind(infos[i], HandleSlotSelected, HandleSlotDelete);
            }
        }

        // ════════════ Callbacks ════════════

        private void HandleSlotSelected(int slot)
        {
            bool occupied = SaveSystem.Exists(slot);

            // Overwriting an existing save is the one destructive path here, so
            // it always goes through a confirmation.
            if (selectingForNewGame && occupied && confirmDialog != null)
            {
                confirmDialog.gameObject.SetActive(true);
                confirmDialog.Open(
                    string.Format(overwritePrompt, slot + 1),
                    onYes: () => { confirmDialog.gameObject.SetActive(false); Choose(slot); },
                    onNo:  () => confirmDialog.gameObject.SetActive(false));
                return;
            }

            if (!selectingForNewGame && !occupied) return; // nothing to load
            Choose(slot);
        }

        private void Choose(int slot)
        {
            var cb = onSlotChosen;
            Close();
            cb?.Invoke(slot);
        }

        private void HandleSlotDelete(int slot)
        {
            if (!SaveSystem.Exists(slot)) return;

            if (confirmDialog == null)
            {
                SaveSystem.Delete(slot);
                Refresh();
                return;
            }

            confirmDialog.gameObject.SetActive(true);
            confirmDialog.Open(
                string.Format(deletePrompt, slot + 1),
                onYes: () =>
                {
                    confirmDialog.gameObject.SetActive(false);
                    SaveSystem.Delete(slot);
                    Refresh();
                },
                onNo: () => confirmDialog.gameObject.SetActive(false));
        }

        private void HandleBack()
        {
            var cb = onBack;
            Close();
            cb?.Invoke();
        }

        private void SetVisible(bool visible)
        {
            if (rootGroup == null) return;
            rootGroup.alpha = visible ? 1f : 0f;
            rootGroup.blocksRaycasts = visible;
            rootGroup.interactable = visible;
        }
    }
}
