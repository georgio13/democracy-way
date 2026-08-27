using System;
using TMPro;
using UnityEngine;

namespace DemocracyWay.UI
{
    using DemocracyWay.Core;
    using DemocracyWay.Framework;

    /// <summary>
    /// One row in the save-slot list. Renders a <see cref="SaveSystem.SlotInfo"/>
    /// and reports clicks upward; it owns no save logic itself.
    ///
    /// An empty slot shows "Κενή θέση" and — depending on
    /// <see cref="SetEmptySlotSelectable"/> — is either inert (load menu) or
    /// clickable (choosing where a new game will be saved).
    /// </summary>
    [AddComponentMenu("DemocracyWay/Save Slot Row")]
    public class SaveSlotRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text slotLabel;
        [SerializeField] private TMP_Text summaryLabel;
        [SerializeField] private TMP_Text metaLabel;
        [SerializeField] private MenuButton selectButton;
        [SerializeField] private MenuButton deleteButton;

        private int slotIndex = -1;
        private Action<int> onSelect;
        private Action<int> onDelete;

        /// <summary>When true, an empty slot stays clickable (new-game flow).</summary>
        private bool emptySlotSelectable;

        void Awake()
        {
            if (selectButton != null) selectButton.onClick.AddListener(HandleSelect);
            if (deleteButton != null) deleteButton.onClick.AddListener(HandleDelete);
        }

        public void SetEmptySlotSelectable(bool selectable) => emptySlotSelectable = selectable;

        public void Bind(SaveSystem.SlotInfo info, Action<int> selectCallback, Action<int> deleteCallback)
        {
            slotIndex = info.Slot;
            onSelect  = selectCallback;
            onDelete  = deleteCallback;

            if (slotLabel != null) slotLabel.text = $"Θέση {info.Slot + 1}";

            if (info.IsEmpty)
            {
                if (summaryLabel != null) summaryLabel.text = "Κενή θέση";
                if (metaLabel != null)    metaLabel.text    = string.Empty;
                if (selectButton != null)
                {
                    selectButton.Text = emptySlotSelectable ? "Νέο" : "—";
                    selectButton.Interactable = emptySlotSelectable;
                }
                if (deleteButton != null) deleteButton.gameObject.SetActive(false);
            }
            else
            {
                if (summaryLabel != null) summaryLabel.text = info.CitizenSummary;
                if (metaLabel != null)
                    metaLabel.text = info.TotalRounds > 0
                        ? $"Γύρος {info.RoundNumber}/{info.TotalRounds}  ·  {info.Playtime}  ·  {info.SavedAt}"
                        : $"{info.Playtime}  ·  {info.SavedAt}";

                if (selectButton != null)
                {
                    selectButton.Text = emptySlotSelectable ? "Αντικατάσταση" : "Φόρτωση";
                    selectButton.Interactable = true;
                }
                if (deleteButton != null)
                {
                    deleteButton.gameObject.SetActive(true);
                    deleteButton.Text = "Διαγραφή";
                }
            }
        }

        private void HandleSelect()
        {
            if (slotIndex < 0) return;
            onSelect?.Invoke(slotIndex);
        }

        private void HandleDelete()
        {
            if (slotIndex < 0) return;
            onDelete?.Invoke(slotIndex);
        }
    }
}
