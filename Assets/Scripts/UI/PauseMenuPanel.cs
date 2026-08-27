using UnityEngine;
using DemocracyWay.Services;

namespace DemocracyWay.UI
{
    /// <summary>
    /// Root component of the pause menu prefab that <see cref="PauseService"/>
    /// instantiates on first pause and reuses afterwards. Lives in the UI
    /// assembly, so the service talks to it only through
    /// <see cref="IPauseMenuPanel"/>. Carries its OWN Canvas at sortingOrder
    /// 9000: above every scene canvas, below the SceneFlow overlay (9999) so
    /// fades always cover it. All child widgets animate on unscaled time —
    /// the menu exists precisely while timeScale is 0.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Pause Menu Panel")]
    [DisallowMultipleComponent]
    public class PauseMenuPanel : MonoBehaviour, IPauseMenuPanel
    {
        /// <summary>Below the SceneFlow overlay (9999), above everything else.</summary>
        private const int SortingOrder = 9000;

        [Header("Δομή")]
        [Tooltip("Το Canvas του panel — το sortingOrder του επιβάλλεται σε 9000 από τον κώδικα.")]
        [SerializeField] private Canvas canvas;

        [Tooltip("Η στήλη με τα τέσσερα κουμπιά (+dim) — κρύβεται όσο είναι ανοιχτές οι Ρυθμίσεις.")]
        [SerializeField] private GameObject buttonColumn;

        [Header("Κουμπιά")]
        [Tooltip("Κουμπί «Συνέχεια» — κλείνει την παύση.")]
        [SerializeField] private UiButton resumeButton;

        [Tooltip("Κουμπί «Ρυθμίσεις» — ανοίγει το παιδί SettingsPanel.")]
        [SerializeField] private UiButton settingsButton;

        [Tooltip("Κουμπί «Επιστροφή στην Αρχική» — με επιβεβαίωση.")]
        [SerializeField] private UiButton mainMenuButton;

        [Tooltip("Κουμπί «Έξοδος» — με επιβεβαίωση.")]
        [SerializeField] private UiButton quitButton;

        [Header("Υπο-panels (παιδιά αυτού του prefab)")]
        [Tooltip("Το SettingsPanel παιδί — το ίδιο component με του κεντρικού μενού.")]
        [SerializeField] private SettingsPanel settingsPanel;

        [Tooltip("Ο κοινός διάλογος επιβεβαίωσης για Επιστροφή/Έξοδο.")]
        [SerializeField] private ConfirmDialog confirmDialog;

        [Header("Μηνύματα")]
        [Tooltip("Μήνυμα επιβεβαίωσης για επιστροφή στο κεντρικό μενού.")]
        [SerializeField] private string mainMenuConfirmMessage =
            "Η πρόοδος μετά το τελευταίο autosave θα χαθεί. Επιστροφή στην αρχική οθόνη;";

        [Tooltip("Μήνυμα επιβεβαίωσης για έξοδο από το παιχνίδι.")]
        [SerializeField] private string quitConfirmMessage =
            "Είστε σίγουροι ότι θέλετε να πραγματοποιήσετε έξοδο;";

        void Awake()
        {
            // Enforced in code so a mis-authored prefab can't hide the pause
            // menu under a scene canvas or over the fade overlay.
            if (canvas != null) canvas.sortingOrder = SortingOrder;

            if (resumeButton != null) resumeButton.onClick.AddListener(HandleResume);
            if (settingsButton != null) settingsButton.onClick.AddListener(HandleSettings);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(HandleMainMenu);
            if (quitButton != null) quitButton.onClick.AddListener(HandleQuit);
        }

        // ════════ IPauseMenuPanel ════════

        public void Show()
        {
            // Always come back to the top level: a previous pause may have
            // been closed (ESC) while Ρυθμίσεις or a confirm was open.
            if (settingsPanel != null) settingsPanel.Hide();
            if (confirmDialog != null) confirmDialog.HideImmediate();
            if (buttonColumn != null) buttonColumn.SetActive(true);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // ════════ Buttons ════════

        private void HandleResume()
        {
            ServicesRoot.Pause?.Resume();
        }

        private void HandleSettings()
        {
            if (settingsPanel == null) return;
            if (buttonColumn != null) buttonColumn.SetActive(false);
            settingsPanel.Open(onBack: () =>
            {
                if (buttonColumn != null) buttonColumn.SetActive(true);
            });
        }

        private void HandleMainMenu()
        {
            confirmDialog?.Show(mainMenuConfirmMessage, onYes: () =>
            {
                // ForceResume BEFORE the transition: the main menu must not
                // load with timeScale 0 and paused audio.
                ServicesRoot.Pause?.ForceResume();
                ServicesRoot.Session?.EndToMainMenu();
                ServicesRoot.Flow?.GoToScene("MainMenu");
            });
        }

        private void HandleQuit()
        {
            confirmDialog?.Show(quitConfirmMessage, onYes: Application.Quit);
        }
    }
}
