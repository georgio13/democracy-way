using TMPro;
using UnityEngine;
using DemocracyWay.Core;
using DemocracyWay.Services;

namespace DemocracyWay.UI
{
    /// <summary>
    /// Orchestrates the MainMenu scene: four UiButtons, the shared sub-panels
    /// (save slots, settings) and the confirm dialog. Button availability is
    /// recomputed every time a sub-panel closes — deleting a save inside
    /// Φόρτωση must immediately unlock Νέο Παιχνίδι and can lock Φόρτωση —
    /// so state is derived from SaveSystem on demand, never cached. The main
    /// column hides while a sub-panel is open so the two never fight for
    /// clicks or attention.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Main Menu Controller")]
    [DisallowMultipleComponent]
    public class MainMenuController : MonoBehaviour
    {
        [Header("Κύρια στήλη")]
        [Tooltip("Ο γονέας των τεσσάρων κουμπιών — κρύβεται όσο ένα υπο-panel είναι ανοιχτό.")]
        [SerializeField] private GameObject mainColumn;

        [Tooltip("Ο τίτλος του παιχνιδιού — κρύβεται μαζί με τη στήλη όσο ένα υπο-panel είναι ανοιχτό.")]
        [SerializeField] private GameObject titleObject;

        [Tooltip("Κουμπί «Νέο Παιχνίδι» — κλειδώνει όταν και οι 4 θέσεις είναι γεμάτες.")]
        [SerializeField] private UiButton newGameButton;

        [Tooltip("Κουμπί «Φόρτωση Παιχνιδιού» — κλειδώνει όταν δεν υπάρχει καμία αποθήκευση.")]
        [SerializeField] private UiButton loadButton;

        [Tooltip("Κουμπί «Ρυθμίσεις».")]
        [SerializeField] private UiButton settingsButton;

        [Tooltip("Κουμπί «Έξοδος» — με επιβεβαίωση.")]
        [SerializeField] private UiButton quitButton;

        [Tooltip("Μικρή υπόδειξη κάτω από το κλειδωμένο «Νέο Παιχνίδι» όταν όλες οι θέσεις είναι γεμάτες.")]
        [SerializeField] private TMP_Text slotsFullHint;

        [Header("Υπο-panels")]
        [Tooltip("Το κοινό panel θέσεων αποθήκευσης (νέο παιχνίδι + φόρτωση).")]
        [SerializeField] private SaveSlotPanel saveSlotPanel;

        [Tooltip("Το panel ρυθμίσεων.")]
        [SerializeField] private SettingsPanel settingsPanel;

        [Tooltip("Ο κοινός διάλογος επιβεβαίωσης (Έξοδος).")]
        [SerializeField] private ConfirmDialog confirmDialog;

        [Header("Μηνύματα")]
        [Tooltip("Μήνυμα επιβεβαίωσης για έξοδο από το παιχνίδι.")]
        [SerializeField] private string quitConfirmMessage =
            "Είστε σίγουροι ότι θέλετε να πραγματοποιήσετε έξοδο;";

        [Tooltip("Κείμενο της υπόδειξης όταν όλες οι θέσεις αποθήκευσης είναι γεμάτες.")]
        [SerializeField] private string slotsFullHintText =
            "Οι θέσεις αποθήκευσης είναι γεμάτες — διάγραψε μία από τη Φόρτωση.";

        void Awake()
        {
            if (newGameButton != null) newGameButton.onClick.AddListener(HandleNewGame);
            if (loadButton != null) loadButton.onClick.AddListener(HandleLoad);
            if (settingsButton != null) settingsButton.onClick.AddListener(HandleSettings);
            if (quitButton != null) quitButton.onClick.AddListener(HandleQuit);
        }

        void Start()
        {
            // Menu music starts here (not in Boot) so returning from a chapter
            // also brings it back. PlayMusic ignores a clip already playing.
            var config = ServicesRoot.Config;
            ServicesRoot.Audio?.PlayMusic(config != null ? config.mainMenuMusic : null);

            if (saveSlotPanel != null) saveSlotPanel.Hide();
            if (settingsPanel != null) settingsPanel.Hide();
            if (confirmDialog != null) confirmDialog.HideImmediate();
            ShowMainColumn();
        }

        /// <summary>Re-derives lock states from disk. Called on Start and every
        /// time a sub-panel closes, because Φόρτωση can delete saves.</summary>
        private void RefreshButtons()
        {
            bool allFull = SaveSystem.AllFull();
            if (newGameButton != null) newGameButton.Interactable = !allFull;
            if (slotsFullHint != null)
            {
                slotsFullHint.text = slotsFullHintText;
                slotsFullHint.gameObject.SetActive(allFull);
            }
            if (loadButton != null) loadButton.Interactable = SaveSystem.AnyExists();
        }

        private void ShowMainColumn()
        {
            if (mainColumn != null) mainColumn.SetActive(true);
            if (titleObject != null) titleObject.SetActive(true);
            RefreshButtons();
        }

        private void HideMainColumn()
        {
            if (mainColumn != null) mainColumn.SetActive(false);
            if (titleObject != null) titleObject.SetActive(false);
        }

        // ════════ Buttons ════════

        private void HandleNewGame()
        {
            if (saveSlotPanel == null) return;
            HideMainColumn();
            saveSlotPanel.OpenForNewGame(
                onPicked: slot =>
                {
                    // The chosen slot rides to CharacterCreation through
                    // PendingNewGame — the run only starts once creation completes.
                    PendingNewGame.TargetSlot = slot;
                    ServicesRoot.Flow?.GoToScene("CharacterCreation");
                },
                onBack: ShowMainColumn);
        }

        private void HandleLoad()
        {
            if (saveSlotPanel == null) return;
            HideMainColumn();
            saveSlotPanel.OpenForLoad(
                onPicked: slot =>
                {
                    var session = ServicesRoot.Session;
                    if (session == null || !session.LoadGame(slot)) return; // corrupt slot: list stays open

                    var config = ServicesRoot.Config;
                    var chapter = config != null
                        ? config.FindChapter(session.Current.currentChapterId) ?? config.firstChapter
                        : null;
                    if (chapter == null)
                    {
                        Debug.LogError("[MainMenu] Δεν βρέθηκε κεφάλαιο για το save — λείπει το firstChapter στο GameConfig;", this);
                        return;
                    }
                    ServicesRoot.Flow?.GoToScene(chapter.sceneName, chapter.title, showLoading: true);
                },
                onBack: ShowMainColumn);
        }

        private void HandleSettings()
        {
            if (settingsPanel == null) return;
            HideMainColumn();
            settingsPanel.Open(onBack: ShowMainColumn);
        }

        private void HandleQuit()
        {
            // AppExit: Application.Quit is a no-op in the editor — this also
            // stops Play mode, so «Έξοδος» is testable without a build.
            confirmDialog?.Show(quitConfirmMessage, onYes: AppExit.Quit);
        }
    }
}
