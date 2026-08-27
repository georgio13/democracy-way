using System;
using TMPro;
using UnityEngine;

namespace DemocracyWay.Menu
{
    using DemocracyWay.Framework;

    /// <summary>
    /// Generic yes/no confirmation dialog. Reused for "Exit to main menu?" and
    /// "Quit game?" prompts from the pause menu and main menu.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Confirm Dialog")]
    public class ConfirmDialog : MonoBehaviour
    {
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private MenuButton yesButton;
        [SerializeField] private MenuButton noButton;

        [Header("Button Labels")]
        [SerializeField] private string yesLabel = "Ναι";
        [SerializeField] private string noLabel = "Όχι";

        private Action onYesCallback;
        private Action onNoCallback;

        void Awake()
        {
            if (yesButton != null)
            {
                yesButton.Text = yesLabel;
                yesButton.onClick.AddListener(HandleYes);
            }
            if (noButton != null)
            {
                noButton.Text = noLabel;
                noButton.onClick.AddListener(HandleNo);
            }
        }

        public void Open(string prompt, Action onYes, Action onNo)
        {
            onYesCallback = onYes;
            onNoCallback = onNo;

            if (promptText != null) promptText.text = prompt;
            if (rootGroup != null)
            {
                rootGroup.alpha = 1f;
                rootGroup.blocksRaycasts = true;
                rootGroup.interactable = true;
            }
            gameObject.SetActive(true);
        }

        public void Close()
        {
            if (rootGroup != null)
            {
                rootGroup.alpha = 0f;
                rootGroup.blocksRaycasts = false;
                rootGroup.interactable = false;
            }
            onYesCallback = null;
            onNoCallback = null;
        }

        private void HandleYes()
        {
            var cb = onYesCallback;
            Close();
            cb?.Invoke();
        }

        private void HandleNo()
        {
            var cb = onNoCallback;
            Close();
            cb?.Invoke();
        }
    }
}
