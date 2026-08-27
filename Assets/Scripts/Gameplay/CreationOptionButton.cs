using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DemocracyWay.Data;
using DemocracyWay.Services;

namespace DemocracyWay.Gameplay
{
    /// <summary>
    /// One entry in the character-creation option list on the right. It is
    /// not a UiButton because it reports two distinct gestures: hovering
    /// *previews* the option on the left panel without committing to it,
    /// while clicking *selects* it and advances the step.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Creation Option Button")]
    [DisallowMultipleComponent]
    public class CreationOptionButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Tooltip("Ο τίτλος της επιλογής όπως εμφανίζεται στη λίστα.")]
        [SerializeField] private TMP_Text label;

        [Tooltip("Το background της επιλογής — αλλάζει χρώμα στο hover.")]
        [SerializeField] private Image background;

        [Tooltip("Χρώμα του background όταν ο κέρσορας είναι αλλού.")]
        [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.05f);

        [Tooltip("Χρώμα του background όταν ο κέρσορας είναι πάνω στην επιλογή.")]
        [SerializeField] private Color hoverColor = new Color(1f, 1f, 1f, 0.15f);

        private CreationOption option;
        private Action<CreationOption> onHover;
        private Action<CreationOption> onClick;

        /// <summary>
        /// Fills the row with its option and callbacks. Plain C# callbacks
        /// instead of UnityEvents because the controller spawns these at
        /// runtime — there is nothing to wire in the Inspector.
        /// </summary>
        public void Init(CreationOption option, Action<CreationOption> onHover, Action<CreationOption> onClick)
        {
            this.option = option;
            this.onHover = onHover;
            this.onClick = onClick;

            if (label != null) label.text = option != null ? option.title : string.Empty;
            if (background != null) background.color = idleColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (background != null) background.color = hoverColor;
            ServicesRoot.Audio?.PlayUiHover();
            onHover?.Invoke(option);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (background != null) background.color = idleColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            ServicesRoot.Audio?.PlayUiClick();
            onClick?.Invoke(option);
        }
    }
}
