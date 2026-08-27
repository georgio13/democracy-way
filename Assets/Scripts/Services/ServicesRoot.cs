using UnityEngine;
using DemocracyWay.Data;

namespace DemocracyWay.Services
{
    /// <summary>
    /// Root of the persistent "Systems" prefab that lives in the Boot scene.
    /// The ONE place with an instance guard and DontDestroyOnLoad; every
    /// service is a child component reached through the static accessors:
    ///
    ///   ServicesRoot.Audio / .Settings / .Session / .Flow / .Cursor / .Pause
    ///   ServicesRoot.Config  — the GameConfig asset with all design knobs
    ///
    /// All accessors are null before the Boot scene has run (e.g. pressing
    /// Play on another scene without the PlayFromBoot helper) — check
    /// <see cref="Ready"/> when in doubt.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Services Root")]
    [DisallowMultipleComponent]
    public class ServicesRoot : MonoBehaviour
    {
        public static ServicesRoot Instance { get; private set; }
        public static bool Ready => Instance != null;

        [SerializeField] private GameConfig config;

        [Header("Services (children of this prefab)")]
        [SerializeField] private AudioService audioService;
        [SerializeField] private SettingsService settingsService;
        [SerializeField] private SessionService sessionService;
        [SerializeField] private SceneFlowService sceneFlowService;
        [SerializeField] private CursorService cursorService;
        [SerializeField] private PauseService pauseService;

        public static GameConfig Config => Instance != null ? Instance.config : null;
        public static AudioService Audio => Instance != null ? Instance.audioService : null;
        public static SettingsService Settings => Instance != null ? Instance.settingsService : null;
        public static SessionService Session => Instance != null ? Instance.sessionService : null;
        public static SceneFlowService Flow => Instance != null ? Instance.sceneFlowService : null;
        public static CursorService Cursor => Instance != null ? Instance.cursorService : null;
        public static PauseService Pause => Instance != null ? Instance.pauseService : null;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // A second Systems prefab (someone opened Boot twice) — discard it.
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (config == null)
                Debug.LogError("[ServicesRoot] Το GameConfig δεν είναι συνδεδεμένο — τίποτα δεν θα δουλέψει σωστά.", this);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
