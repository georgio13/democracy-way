using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using DemocracyWay.Data;
using DemocracyWay.Services;

namespace DemocracyWay.Gameplay
{
    /// <summary>
    /// One entry in the character-creation option list on the right.
    /// Hovering only highlights the row (gold label + sound); clicking
    /// *selects* it (tick, same language as the save-slot rows) and fills
    /// the preview panel. Advancing is the controller's job via «Επόμενο».
    /// </summary>
    [AddComponentMenu("DemocracyWay/Creation Option Button")]
    [DisallowMultipleComponent]
    public class CreationOptionButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Tooltip("Ο τίτλος της επιλογής όπως εμφανίζεται στη λίστα.")]
        [SerializeField] private TMP_Text label;

        [Tooltip("Το τικ που εμφανίζεται όταν η επιλογή είναι η διαλεγμένη του βήματος.")]
        [SerializeField] private GameObject tick;

        [Tooltip("Χρώμα του τίτλου σε ηρεμία.")]
        [SerializeField] private Color labelIdleColor = Color.white;

        [Tooltip("Χρώμα του τίτλου στο hover και όσο η επιλογή είναι διαλεγμένη (#D2A656).")]
        [SerializeField] private Color labelHighlightColor = new Color(0.8235294f, 0.6509804f, 0.3372549f, 1f);

        private CreationOption option;
        private Action<CreationOption> onClick;
        private bool selected;

        /// <summary>Poia επιλογή δείχνει η γραμμή — ο controller συγκρίνει με
        /// αυτήν για να κρατά τικαρισμένη μόνο μία γραμμή του βήματος.</summary>
        public CreationOption Option => option;

        /// <summary>
        /// Fills the row with its option and callbacks. Plain C# callbacks
        /// instead of UnityEvents because the controller spawns these at
        /// runtime — there is nothing to wire in the Inspector.
        /// </summary>
        public void Init(CreationOption option, Action<CreationOption> onClick)
        {
            this.option = option;
            this.onClick = onClick;

            if (label != null) label.text = option != null ? option.title : string.Empty;
            SetSelected(false);
        }

        /// <summary>Δείχνει/κρύβει το τικ και κρατά τον τίτλο χρυσό όσο η
        /// γραμμή είναι η διαλεγμένη του βήματος.</summary>
        public void SetSelected(bool value)
        {
            selected = value;
            if (tick != null) tick.SetActive(value);
            ApplyLabelColor(highlighted: value);
        }

        private void ApplyLabelColor(bool highlighted)
        {
            if (label != null) label.color = highlighted ? labelHighlightColor : labelIdleColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ApplyLabelColor(true);
            ServicesRoot.Audio?.PlayUiHover();
        }

        public void OnPointerExit(PointerEventData eventData) => ApplyLabelColor(selected);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            ServicesRoot.Audio?.PlayUiClick();
            onClick?.Invoke(option);
        }
    }
}
