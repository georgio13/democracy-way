using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DemocracyWay.Menu
{
    using DemocracyWay.Framework;

    /// <summary>
    /// Pause menu UI controller. The panel is a full-screen black overlay with a title
    /// "Μενού Παύσης" and four MenuButtons: Συνέχεια, Ρυθμίσεις, Κύριο Μενού, Έξοδος.
    /// The settings panel and confirmation dialogs are sibling prefab instances.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Pause Menu Panel")]
    public class PauseMenuPanel : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private CanvasGroup rootGroup;

        [Header("Buttons")]
        [SerializeField] private MenuButton resumeButton;
        [SerializeField] private MenuButton settingsButton;
        [SerializeField] private MenuButton mainMenuButton;
        [SerializeField] private MenuButton quitButton;

        [Header("Sub-panels")]
        [Tooltip("Settings prefab instance child (hidden by default).")]
        [SerializeField] private SettingsPanel settingsPanel;

        [Tooltip("Generic yes/no dialog child (hidden by default).")]
        [SerializeField] private ConfirmDialog confirmDialog;

        [Header("Strings (editable for localization)")]
        [SerializeField] private string confirmMainMenuPrompt = "Είσαι σίγουρος ότι θέλεις να επιστρέψεις στο κύριο μενού;";
        [SerializeField] private string confirmQuitPrompt = "Είσαι σίγουρος ότι θέλεις να βγεις από το παιχνίδι;";
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        void Awake()
        {
            if (resumeButton != null) resumeButton.onClick.AddListener(OnResume);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnOpenSettings);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenu);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuit);

            if (settingsPanel != null) settingsPanel.gameObject.SetActive(false);
            if (confirmDialog != null) confirmDialog.gameObject.SetActive(false);

            Hide();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            if (rootGroup != null)
            {
                rootGroup.alpha = 1f;
                rootGroup.blocksRaycasts = true;
                rootGroup.interactable = true;
            }
        }

        public void Hide()
        {
            if (rootGroup != null)
            {
                rootGroup.alpha = 0f;
                rootGroup.blocksRaycasts = false;
                rootGroup.interactable = false;
            }
            gameObject.SetActive(false);
        }

        // ════════════ Button handlers ════════════

        private void OnResume()
        {
            PauseMenuController.Instance?.Resume();
        }

        private void OnOpenSettings()
        {
            if (settingsPanel == null) return;
            settingsPanel.gameObject.SetActive(true);
            settingsPanel.Open(onClose: () => settingsPanel.gameObject.SetActive(false));
        }

        private void OnMainMenu()
        {
            if (confirmDialog == null)
            {
                DoMainMenu();
                return;
            }
            confirmDialog.gameObject.SetActive(true);
            confirmDialog.Open(
                confirmMainMenuPrompt,
                onYes: () =>
                {
                    confirmDialog.gameObject.SetActive(false);
                    DoMainMenu();
                },
                onNo: () => confirmDialog.gameObject.SetActive(false));
        }

        private void OnQuit()
        {
            if (confirmDialog == null)
            {
                DoQuit();
                return;
            }
            confirmDialog.gameObject.SetActive(true);
            confirmDialog.Open(
                confirmQuitPrompt,
                onYes: () =>
                {
                    confirmDialog.gameObject.SetActive(false);
                    DoQuit();
                },
                onNo: () => confirmDialog.gameObject.SetActive(false));
        }

        private void DoMainMenu()
        {
            PauseMenuController.Instance?.ForceResumeBeforeSceneLoad();
            if (SceneTransitionController.Instance != null)
                SceneTransitionController.Instance.LoadSceneWithTransition(mainMenuSceneName, string.Empty);
            else
                SceneManager.LoadScene(mainMenuSceneName);
        }

        private void DoQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
