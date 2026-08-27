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
        [Tooltip("Ο τίτλος της θέσης, π.χ. «Θέση 1».")]
        [SerializeField] private TMP_Text slotTitleText;

        [Tooltip("Κείμενο «Κενή Θέση» — ορατό μόνο όταν η θέση είναι άδεια.")]
        [SerializeField] private TMP_Text emptyLabelText;

        [Tooltip("Γονέας των στοιχείων γεμάτης θέσης (profile, κεφάλαιο, ημερολόγιο, χρόνος, ημερομηνία).")]
        [SerializeField] private GameObject occupiedGroup;

        [Tooltip("Γραμμή προφίλ, π.χ. «Γυνή · Ερεχθηίς · Κεραμεικός».")]
        [SerializeField] private TMP_Text profileText;

        [Tooltip("Τίτλος του τρέχοντος κεφαλαίου του save.")]
        [SerializeField] private TMP_Text chapterText;

        [Tooltip("Θέση στο ημερολόγιο, π.χ. «Πρυτανεία 3 · Βδομάδα 2».")]
        [SerializeField] private TMP_Text calendarText;

        [Tooltip("Συνολικός χρόνος παιχνιδιού, π.χ. «02ω 15λ».")]
        [SerializeField] private TMP_Text playtimeText;

        [Tooltip("Τοπική ημερομηνία/ώρα αποθήκευσης, π.χ. «27/08/2026 16:30».")]
        [SerializeField] private TMP_Text savedAtText;

        [Tooltip("Διάφανο UiButton που καλύπτει όλη τη γραμμή — η επιλογή της θέσης.")]
        [SerializeField] private UiButton pickButton;

        [Tooltip("UiButton «Διαγραφή» — ορατό μόνο σε γεμάτες θέσεις σε mode φόρτωσης.")]
        [SerializeField] private UiButton deleteButton;

        [Header("Κείμενα")]
        [Tooltip("Μορφή τίτλου θέσης· {0} = αριθμός θέσης (1-βάσης).")]
        [SerializeField] private string slotTitleFormat = "Θέση {0}";

        [Tooltip("Κείμενο άδειας θέσης.")]
        [SerializeField] private string emptyLabel = "Κενή Θέση";

        [Tooltip("Μορφή ημερολογίου· {0} = πρυτανεία, {1} = βδομάδα.")]
        [SerializeField] private string calendarFormat = "Πρυτανεία {0} · Βδομάδα {1}";

        [Tooltip("Μορφή χρόνου παιχνιδιού· {0} = ώρες, {1} = λεπτά.")]
        [SerializeField] private string playtimeFormat = "{0:00}ω {1:00}λ";

        /// <summary>
        /// Fills the row from a summary. <paramref name="clickable"/> is
        /// decided by the panel's mode (load = occupied only, new game =
        /// empty only). A null <paramref name="onDelete"/> hides Διαγραφή.
        /// </summary>
        public void Bind(SaveSummary summary, bool clickable, Action onPick, Action onDelete)
        {
            bool occupied = summary != null && summary.exists;

            if (slotTitleText != null)
                slotTitleText.text = string.Format(slotTitleFormat, (summary != null ? summary.slot : 0) + 1);

            if (emptyLabelText != null)
            {
                emptyLabelText.text = emptyLabel;
                emptyLabelText.gameObject.SetActive(!occupied);
            }

            if (occupiedGroup != null)
                occupiedGroup.SetActive(occupied);

            if (occupied)
            {
                if (profileText != null) profileText.text = summary.profileLine;
                if (chapterText != null) chapterText.text = summary.chapterTitle;
                if (calendarText != null)
                    calendarText.text = string.Format(calendarFormat, summary.prytany, summary.week);
                if (playtimeText != null) playtimeText.text = FormatPlaytime(summary.playtimeSeconds);
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

        private string FormatPlaytime(double totalSeconds)
        {
            if (totalSeconds < 0) totalSeconds = 0;
            int hours = (int)(totalSeconds / 3600.0);
            int minutes = (int)(totalSeconds % 3600.0 / 60.0);
            return string.Format(playtimeFormat, hours, minutes);
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
