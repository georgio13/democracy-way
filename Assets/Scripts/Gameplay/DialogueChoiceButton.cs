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
    /// One selectable answer under the dialogue panel. Spawned per choice by
    /// the DialogueRunner, so it takes a plain callback instead of a
    /// UnityEvent — there is never anything to wire in the Inspector.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Dialogue Choice Button")]
    [DisallowMultipleComponent]
    public class DialogueChoiceButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Tooltip("Το κείμενο της επιλογής όπως το βλέπει ο παίκτης.")]
        [SerializeField] private TMP_Text label;

        [Tooltip("Το background της επιλογής — αλλάζει χρώμα στο hover.")]
        [SerializeField] private Image background;

        [Tooltip("Χρώμα του background όταν ο κέρσορας είναι αλλού.")]
        [SerializeField] private Color idleColor = new Color(0f, 0f, 0f, 0.55f);

        [Tooltip("Χρώμα του background όταν ο κέρσορας είναι πάνω στην επιλογή.")]
        [SerializeField] private Color hoverColor = new Color(0.25f, 0.22f, 0.12f, 0.75f);

        private DialogueChoice choice;
        private Action<DialogueChoice> onClick;

        public void Init(DialogueChoice choice, Action<DialogueChoice> onClick)
        {
            this.choice = choice;
            this.onClick = onClick;

            if (label != null) label.text = choice != null ? choice.text : string.Empty;
            if (background != null) background.color = idleColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (background != null) background.color = hoverColor;
            ServicesRoot.Audio?.PlayUiHover();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (background != null) background.color = idleColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            ServicesRoot.Audio?.PlayUiClick();
            onClick?.Invoke(choice);
        }
    }
}
