using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DemocracyWay.Menu
{
    using DemocracyWay.Framework;

    /// <summary>
    /// Persistent singleton. Listens for the Pause input action and toggles the pause
    /// overlay on every scene except Bootstrap and MainMenu. The overlay prefab is
    /// instantiated lazily under a DontDestroyOnLoad parent and reused.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Pause Menu Controller")]
    [DisallowMultipleComponent]
    public class PauseMenuController : MonoBehaviour
    {
        public static PauseMenuController Instance { get; private set; }

        [Header("Pause Overlay")]
        [Tooltip("Prefab containing PauseMenuPanel. Instantiated on first pause.")]
        [SerializeField] private GameObject pauseMenuPrefab;

        [Header("Scenes Where Pause Is Disabled")]
        [SerializeField] private string[] disabledScenes = { "Bootstrap", "MainMenu" };

        [Header("Input")]
        [Tooltip("Input action asset. The 'UI/Pause' action is bound to ESC.")]
        [SerializeField] private InputActionAsset inputActions;

        [SerializeField] private string actionMap = "UI";
        [SerializeField] private string actionName = "Pause";

        // Runtime
        private PauseMenuPanel panelInstance;
        private InputAction pauseAction;
        public bool IsPaused { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent != null) transform.SetParent(null, false);
            DontDestroyOnLoad(gameObject);
        }

        void OnEnable()
        {
            if (inputActions != null)
            {
                var map = inputActions.FindActionMap(actionMap, throwIfNotFound: false);
                if (map != null)
                {
                    pauseAction = map.FindAction(actionName, throwIfNotFound: false);
                    if (pauseAction != null)
                    {
                        pauseAction.performed += OnPausePerformed;
                        pauseAction.Enable();
                    }
                }
            }
        }

        void OnDisable()
        {
            if (pauseAction != null)
            {
                pauseAction.performed -= OnPausePerformed;
                pauseAction.Disable();
            }
        }

        private void OnPausePerformed(InputAction.CallbackContext ctx)
        {
            if (IsPauseDisabledInCurrentScene()) return;
            Toggle();
        }

        private bool IsPauseDisabledInCurrentScene()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            foreach (var s in disabledScenes)
                if (s == currentScene) return true;
            return false;
        }

        public void Toggle()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        public void Pause()
        {
            if (IsPaused) return;
            EnsurePanel();
            if (panelInstance == null) return;

            IsPaused = true;
            Time.timeScale = 0f;
            AudioListener.pause = true; // pauses everything except ignoreListenerPause sources
            AudioManager.Instance?.SetPauseDucking(true);
            panelInstance.Show();
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            AudioManager.Instance?.SetPauseDucking(false);
            if (panelInstance != null) panelInstance.Hide();
        }

        public void ForceResumeBeforeSceneLoad()
        {
            // Called when user picks "Main Menu" from pause — restore timescale before load.
            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            AudioManager.Instance?.SetPauseDucking(false);
            if (panelInstance != null) panelInstance.Hide();
        }

        private void EnsurePanel()
        {
            if (panelInstance != null) return;
            if (pauseMenuPrefab == null)
            {
                Debug.LogError("[PauseMenuController] pauseMenuPrefab not assigned.");
                return;
            }
            var go = Instantiate(pauseMenuPrefab, transform);
            panelInstance = go.GetComponent<PauseMenuPanel>();
            if (panelInstance == null)
                Debug.LogError("[PauseMenuController] Prefab is missing PauseMenuPanel component.");
        }
    }
}
