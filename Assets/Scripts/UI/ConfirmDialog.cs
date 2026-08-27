using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.UI
{
    /// <summary>
    /// One reusable Ναι/Όχι modal per menu, fed a message and callbacks at
    /// call time — so every destructive action (delete save, quit, leave to
    /// main menu) shares a single prefab instead of one popup prefab each.
    /// The full-screen dim Image is a raycast target: while the dialog is
    /// open, nothing behind it can be clicked. The GameObject is saved
    /// inactive; Show() activates it, either answer deactivates it.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Confirm Dialog")]
    [DisallowMultipleComponent]
    public class ConfirmDialog : MonoBehaviour
    {
        [Tooltip("Full-screen ημιδιαφανές Image που σκοτεινιάζει και μπλοκάρει ό,τι είναι από πίσω (raycast target ΟΝ).")]
        [SerializeField] private Image dimImage;

        [Tooltip("Το κείμενο της ερώτησης (π.χ. «Είστε σίγουροι…;»).")]
        [SerializeField] private TMP_Text messageText;

        [Tooltip("Κουμπί επιβεβαίωσης — label «Ναι».")]
        [SerializeField] private UiButton yesButton;

        [Tooltip("Κουμπί ακύρωσης — label «Όχι». Πάντα απλώς κλείνει τον διάλογο.")]
        [SerializeField] private UiButton noButton;

        private Action onYes;
        private Action onNo;

        void Awake()
        {
            // Listeners live here (not per Show) so repeated Show calls can
            // never stack duplicate invocations.
            if (yesButton != null) yesButton.onClick.AddListener(HandleYes);
            if (noButton != null) noButton.onClick.AddListener(HandleNo);
            if (dimImage != null) dimImage.raycastTarget = true;
        }

        /// <summary>
        /// Opens the dialog. Ναι closes then runs <paramref name="onYes"/>;
        /// Όχι closes then runs <paramref name="onNo"/> (optional — null means
        /// "just close"). Callbacks run AFTER closing so they may immediately
        /// open another panel or start a scene transition.
        /// </summary>
        public void Show(string message, Action onYes, Action onNo = null)
        {
            this.onYes = onYes;
            this.onNo = onNo;
            if (messageText != null) messageText.text = message;
            gameObject.SetActive(true);
        }

        public void HideImmediate()
        {
            onYes = null;
            onNo = null;
            gameObject.SetActive(false);
        }

        private void HandleYes()
        {
            var callback = onYes;
            HideImmediate();
            callback?.Invoke();
        }

        private void HandleNo()
        {
            var callback = onNo;
            HideImmediate();
            callback?.Invoke();
        }
    }
}
