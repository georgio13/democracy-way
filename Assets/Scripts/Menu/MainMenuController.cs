using UnityEngine;
using UnityEngine.SceneManagement;

namespace DemocracyWay.Menu
{
    using DemocracyWay.Core;
    using DemocracyWay.Framework;
    using DemocracyWay.UI;

    /// <summary>
    /// Main menu. Four entries:
    ///
    ///   Νέο Παιχνίδι → pick a save slot → character creation
    ///   Φόρτωση      → pick a slot with a save → straight into the game
    ///   Ρυθμίσεις    → settings panel
    ///   Έξοδος       → confirm, then quit
    ///
    /// Both game entries route through the same <see cref="SaveSlotPanel"/>;
    /// only the mode it opens in differs.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Main Menu Controller")]
    public class MainMenuController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private MenuButton newGameButton;
        [SerializeField] private MenuButton loadGameButton;
        [SerializeField] private MenuButton settingsButton;
        [SerializeField] private MenuButton quitButton;

        [Header("Sub-panels")]
        [SerializeField] private SettingsPanel settingsPanel;
        [SerializeField] private ConfirmDialog confirmDialog;
        [SerializeField] private SaveSlotPanel saveSlotPanel;

        [Header("Audio")]
        [Tooltip("Main menu ambient music. Wired to A_Ambient_MainMenu by Init.")]
        [SerializeField] private AudioClip ambientMusic;

        [Header("Scene Flow")]
        [SerializeField] private string characterCreationSceneName = "CharacterCreation";
        [SerializeField] private string gameSceneName = "Game";

        [Header("Strings")]
        [SerializeField] private string confirmQuitPrompt = "Είσαι σίγουρος ότι θέλεις να βγεις από το παιχνίδι;";

        [Header("Button Labels")]
        [SerializeField] private string newGameLabel = "Νέο Παιχνίδι";
        [SerializeField] private string loadGameLabel = "Φόρτωση";
        [SerializeField] private string settingsLabel = "Ρυθμίσεις";
        [SerializeField] private string quitLabel = "Έξοδος";

        void Awake()
        {
            if (newGameButton != null)
            {
                newGameButton.Text = newGameLabel;
                newGameButton.onClick.AddListener(OnNewGame);
            }
            if (loadGameButton != null)
            {
                loadGameButton.Text = loadGameLabel;
                loadGameButton.onClick.AddListener(OnLoadGame);
                loadGameButton.Interactable = SaveSystem.AnySaveExists();
            }
            if (settingsButton != null)
            {
                settingsButton.Text = settingsLabel;
                settingsButton.onClick.AddListener(OnSettings);
            }
            if (quitButton != null)
            {
                quitButton.Text = quitLabel;
                quitButton.onClick.AddListener(OnQuit);
            }

            if (settingsPanel != null) settingsPanel.gameObject.SetActive(false);
            if (confirmDialog != null) confirmDialog.gameObject.SetActive(false);
            if (saveSlotPanel != null) saveSlotPanel.gameObject.SetActive(false);
        }

        void Start()
        {
            if (ambientMusic != null && AudioManager.Instance != null)
                AudioManager.Instance.PlayMusic(ambientMusic);
        }

        // ════════════ Button handlers ════════════

        private void OnNewGame()
        {
            if (saveSlotPanel == null)
            {
                // No slot picker available — fall back to the first free slot.
                CharacterCreationController.TargetSlot = SaveSystem.FirstEmptySlot();
                GoToCharacterCreation();
                return;
            }

            SetMenuInteractable(false);
            saveSlotPanel.OpenForSave(
                slotChosen: slot =>
                {
                    CharacterCreationController.TargetSlot = slot;
                    GoToCharacterCreation();
                },
                back: () => SetMenuInteractable(true));
        }

        private void OnLoadGame()
        {
            if (saveSlotPanel == null || !SaveSystem.AnySaveExists()) return;

            SetMenuInteractable(false);
            saveSlotPanel.OpenForLoad(
                slotChosen: slot =>
                {
                    if (GameStateService.Instance == null || !GameStateService.Instance.LoadRun(slot))
                    {
                        Debug.LogWarning($"[MainMenu] Slot {slot} could not be loaded.");
                        SetMenuInteractable(true);
                        if (loadGameButton != null)
                            loadGameButton.Interactable = SaveSystem.AnySaveExists();
                        return;
                    }
                    LoadScene(gameSceneName);
                },
                back: () =>
                {
                    SetMenuInteractable(true);
                    // A save may have been deleted while the panel was open.
                    if (loadGameButton != null)
                        loadGameButton.Interactable = SaveSystem.AnySaveExists();
                });
        }

        private void OnSettings()
        {
            if (settingsPanel == null) return;
            settingsPanel.gameObject.SetActive(true);
            settingsPanel.Open(onClose: () => settingsPanel.gameObject.SetActive(false));
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

        // ════════════ Helpers ════════════

        private void GoToCharacterCreation() => LoadScene(characterCreationSceneName);

        private void LoadScene(string sceneName)
        {
            if (SceneTransitionController.Instance != null)
                SceneTransitionController.Instance.LoadSceneWithTransition(sceneName, string.Empty);
            else
                SceneManager.LoadScene(sceneName);
        }

        /// <summary>Greys out the menu column while a modal panel is up, so the
        /// buttons underneath can't be clicked through it.</summary>
        private void SetMenuInteractable(bool interactable)
        {
            if (newGameButton != null)  newGameButton.Interactable  = interactable;
            if (loadGameButton != null) loadGameButton.Interactable = interactable && SaveSystem.AnySaveExists();
            if (settingsButton != null) settingsButton.Interactable = interactable;
            if (quitButton != null)     quitButton.Interactable     = interactable;
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
