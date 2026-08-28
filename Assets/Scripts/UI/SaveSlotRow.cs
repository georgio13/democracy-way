using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using DemocracyWay.Core;

namespace DemocracyWay.UI
{
    /// <summary>
    /// One row of the save-slot list, instantiated from a prefab by
    /// <see cref="SaveSlotPanel"/>. Purely presentational: the panel decides
    /// per mode whether the row is pickable and whether Διαγραφή appears —
    /// the row just renders a <see cref="SaveSummary"/> and forwards clicks.
    /// Rows are destroyed and rebuilt on every change, so Bind never needs to
    /// undo a previous state.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Save Slot Row")]
    [DisallowMultipleComponent]
    public class SaveSlotRow : MonoBehaviour
    {
        [Header("Αναφορές")]
        [Tooltip("Κείμενο «Κενή Θέση» — ορατό μόνο όταν η θέση είναι άδεια.")]
        [SerializeField] private TMP_Text emptyLabelText;

        [Tooltip("Γονέας των στοιχείων γεμάτης θέσης (κεφάλαιο, ημερομηνία).")]
        [SerializeField] private GameObject occupiedGroup;

        [Tooltip("Τίτλος της γραμμής: το κεφάλαιο του save.")]
        [SerializeField] private TMP_Text chapterText;

        [Tooltip("Τοπική ημερομηνία/ώρα αποθήκευσης, π.χ. «27/08/2026 16:30».")]
        [SerializeField] private TMP_Text savedAtText;

        [Tooltip("Διάφανο UiButton που καλύπτει όλη τη γραμμή — η επιλογή της θέσης.")]
        [SerializeField] private UiButton pickButton;

        [Tooltip("UiButton «Διαγραφή» — ορατό μόνο σε γεμάτες θέσεις σε mode φόρτωσης.")]
        [SerializeField] private UiButton deleteButton;

        [Header("Κείμενα")]
        [Tooltip("Κείμενο άδειας θέσης.")]
        [SerializeField] private string emptyLabel = "Κενή Θέση";

        /// <summary>
        /// Fills the row from a summary. <paramref name="clickable"/> is
        /// decided by the panel's mode (load = occupied only, new game =
        /// empty only). A null <paramref name="onDelete"/> hides Διαγραφή.
        /// </summary>
        public void Bind(SaveSummary summary, bool clickable, Action onPick, Action onDelete)
        {
            bool occupied = summary != null && summary.exists;

            if (emptyLabelText != null)
            {
                emptyLabelText.text = emptyLabel;
                emptyLabelText.gameObject.SetActive(!occupied);
            }

            if (occupiedGroup != null)
                occupiedGroup.SetActive(occupied);

            if (occupied)
            {
                if (chapterText != null) chapterText.text = summary.chapterTitle;
                if (savedAtText != null) savedAtText.text = FormatSavedAt(summary.savedAtIso);
            }

            if (pickButton != null)
            {
                pickButton.Interactable = clickable;
                pickButton.onClick.RemoveAllListeners();
                if (onPick != null) pickButton.onClick.AddListener(() => onPick());
            }

            if (deleteButton != null)
            {
                deleteButton.gameObject.SetActive(occupied && onDelete != null);
                deleteButton.onClick.RemoveAllListeners();
                if (onDelete != null) deleteButton.onClick.AddListener(() => onDelete());
            }
        }

        private string FormatSavedAt(string savedAtIso)
        {
            // Saves store UTC ISO-8601; players read local wall-clock time. A
            // pre-summary save with a missing/garbled stamp just shows nothing —
            // it must never break the list.
            if (string.IsNullOrEmpty(savedAtIso)) return string.Empty;
            try
            {
                var utc = DateTime.Parse(savedAtIso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                return utc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                return string.Empty;
            }
        }
    }
}
