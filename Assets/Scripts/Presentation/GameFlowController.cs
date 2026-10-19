using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    [DisallowMultipleComponent]
    public sealed partial class GameFlowController : MonoBehaviour
    {
        public GameConfiguration config;
        public List<UIScreen> screens = new List<UIScreen>();
        public List<UIBinding> bindings = new List<UIBinding>();
        public string defaultScreen = "main-menu";
        public string CurrentScreen => currentScreen;
        public GameState State => GameSession.State;

        private readonly Dictionary<string, UnityEngine.Object> targets = new Dictionary<string, UnityEngine.Object>();
        private readonly Dictionary<string, GameObject> roots = new Dictionary<string, GameObject>();
        private readonly List<KeyValuePair<Button, UnityAction>> clickHandlers = new List<KeyValuePair<Button, UnityAction>>();
        private readonly HashSet<Button> wiredButtons = new HashSet<Button>();
        private string currentScreen;
        private int selectedSlot = -1;
        private int pendingDelete = -1;
        private bool modalOpen;
        private bool exitConfirmation;
        private CanvasGroup modalBlockedGroup;
        private bool previousInteractable;
        private bool previousBlocksRaycasts;
        private AudioSettingsService audioSettings;
        private bool routing;
        private bool drawingLottery;
        private bool gamePaused;
        private float prePauseTimeScale = 1f;
        private string pauseReturnScreen = "map-stela";
        private string profileReturnScreen = "map-stela";
        private bool settingsFromPause;
        private bool returnToMenuConfirmation;
        private bool narrativeFading;
        private string lastRenderedBioStep;
        private string saveWarning;
#if UNITY_EDITOR
        private bool editorFullscreenPreview;
#else
        private bool fullscreenRequested;
        private bool fullscreenRequestPending;
        private float fullscreenRequestStarted;
#endif
        public static bool IsWeb
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogError("GameFlowController requires an authored GameConfiguration asset.", this);
                enabled = false;
                return;
            }

            foreach (UIBinding binding in bindings)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.key) || binding.target == null)
                    continue;
                if (targets.ContainsKey(binding.key))
                    Debug.LogWarning("Duplicate DW binding: " + binding.key, this);
                targets[binding.key] = binding.target;
            }

            foreach (UIScreen screen in screens)
            {
                if (screen == null || string.IsNullOrWhiteSpace(screen.id) || screen.root == null)
                    continue;
                roots[screen.id] = screen.root;
            }

            if (!config.Validate(out string configError))
            {
                SetStatus(configError);
                Debug.LogError(configError, this);
                enabled = false;
                return;
            }

            SetupAudio();
            WireButtons();
            SetupSettings();
            SetupEvaluationUI();
            SetActive("modal.root", false);
        }

        private void Start()
        {
            if (!AccountBridge.IsReady && string.IsNullOrEmpty(EditorTestDirectory()))
            {
                UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Login");
                return;
            }
            if (config.cursorTexture != null)
                Cursor.SetCursor(config.cursorTexture, config.cursorHotspot, CursorMode.Auto);
            string requested = GameSession.PendingScreen;
            GameSession.PendingScreen = null;
            if (string.IsNullOrEmpty(requested))
                requested = defaultScreen;
            bool directPreview = State == null;
            if (requested != "main-menu" && requested != "slots-new" && requested != "slots-load" && requested != "settings")
                EnsureState();
            if (directPreview && State != null)
            {
                if (SceneFor(requested) == "Gameplay")
                {
                    State.biographyComplete = true;
                    State.phase = requested;
                    if (requested == "map-stela")
                        GameRules.DrawTribe(State, config.tribes.Count, 0);
                }
                else if (requested.StartsWith("bio-", StringComparison.Ordinal))
                {
                    State.phase = requested == "bio-summary" ? "summary" : "biography";
                    BiographyStepKind kind = requested == "bio-family" ? BiographyStepKind.Family : requested == "bio-identity" ? BiographyStepKind.Identity : requested == "bio-oath" ? BiographyStepKind.Oath : BiographyStepKind.Choice;
                    int first = config.biography.FindIndex(x => x.kind == kind);
                    if (first >= 0)
                        State.biographyIndex = first;
                }
            }

            if (requested == "biography")
                ShowBiography();
            else
                Show(requested);
        }

        private void Update()
        {
            if (AccountBridge.IsAccountScreenOpen || (!AccountBridge.IsReady && string.IsNullOrEmpty(EditorTestDirectory()))) return;
            if (State != null && Application.isFocused && !gamePaused && !modalOpen)
                State.activeSeconds += Mathf.Min(Time.unscaledDeltaTime, 1f);
#if ENABLE_INPUT_SYSTEM
            if(UnityEngine.InputSystem.Keyboard.current!=null&&UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if(HandleEvaluationEscape())return;
                if(modalOpen)CancelModal();
                else if(currentScreen=="profile")CloseProfile();
                else if(currentScreen=="pause")ResumeGame();
                else if(currentScreen=="settings"&&settingsFromPause)Back();
                else if(SceneManager.GetActiveScene().name=="Gameplay")OpenPause();
            }
#endif
            if (currentScreen == "settings")
            {
                Toggle fullscreen = Get<Toggle>("settings.fullscreen");
                if (fullscreen != null)
                    fullscreen.SetIsOnWithoutNotify(FullscreenDisplayState());
            }
        }

        private void OnDestroy()
        {
            if (gamePaused)
                Time.timeScale = prePauseTimeScale;
            foreach (var pair in clickHandlers)
                if (pair.Key != null)
                    pair.Key.onClick.RemoveListener(pair.Value);
        }

        private static string EditorTestDirectory()
        {
#if UNITY_EDITOR
            return SaveStore.TestDirectory;
#else
            return null;
#endif
        }

        public T Get<T>(string key)
            where T : UnityEngine.Object
        {
            if (!targets.TryGetValue(key, out UnityEngine.Object target) || target == null)
                return null;
            if (target is T typed)
                return typed;
            if (typeof(T) == typeof(GameObject) && target is Component component)
                return component.gameObject as T;
            GameObject gameObject = target as GameObject;
            if (gameObject == null && target is Component other)
                gameObject = other.gameObject;
            return gameObject != null && typeof(Component).IsAssignableFrom(typeof(T)) ? gameObject.GetComponent(typeof(T)) as T : null;
        }

        private void WireButtons()
        {
            WireCityButtons();
            WireCampaignButtons();
            Bind("map.profile", OpenProfile);
            Bind("map.pause", OpenPause);
            Bind("profile.back", CloseProfile);
            Bind("profile.close", CloseProfile);
            Bind("pause.primary", ResumeGame);
            Bind("pause.settings", PauseSettings);
            Bind("pause.menu", RequestMainMenu);
            Bind("main.new", NewGame);
            Bind("main.load", LoadMenu);
            Bind("main.settings", Settings);
            Bind("main.exit", AskExit);
            foreach (string id in new[]
            {
                "slots-new",
                "slots-load",
                "settings",
                "prologue",
                "prytany-title",
                "lottery",
                "map-stela"
            }

            )
                Bind(id + ".back", Back);
            Bind("slots-new.primary", StartSelectedSlot);
            Bind("slots-load.primary", StartSelectedSlot);
            Bind("settings.primary", Back);
            for (int i = 0; i < SaveStore.SlotCount; i++)
            {
                int index = i;
                Bind("slots-new.slot." + index, () => SelectSlot(index));
                Bind("slots-load.slot." + index, () => SelectSlot(index));
                Bind("slots-load.slot." + index + ".delete", () => DeleteSlot(index));
            }

            Bind("prologue.primary", NextPrologue);
            Bind("bio-family.primary", AdvanceBio);
            Bind("bio-choice.primary", AdvanceBio);
            Bind("bio-oath.primary", AdvanceBio);
            Bind("bio-summary.primary", FinishBio);
            Bind("prytany-title.primary", NextPrytany);
            Bind("lottery.primary", () =>
            {
                if (State != null && State.currentTribe >= 0)
                    BeginPrytany();
                else
                    DrawNext();
            });
            Bind("lottery.begin", BeginPrytany);
            Bind("modal.confirm", ConfirmDelete);
            Bind("modal.cancel", CancelModal);
            for (int i = 0; i < 10; i++)
            {
                int choice = i;
                Bind("bio.option." + choice, () => ChooseBio(choice));
                foreach (string id in new[]
                {
                    "bio-family",
                    "bio-choice",
                    "bio-oath"
                }

                )
                    Bind(id + ".option." + choice, () => ChooseBio(choice));
            }

            var seen = new HashSet<Button>();
            foreach (UIBinding binding in bindings)
            {
                if (binding == null || string.IsNullOrEmpty(binding.key))
                    continue;
                Button button = Get<Button>(binding.key);
                if (button == null || !seen.Add(button))
                    continue;
                ButtonAudio fx = button.GetComponent<ButtonAudio>();
                if (fx == null)
                    fx = button.gameObject.AddComponent<ButtonAudio>();
                fx.app = this;
            }
        }

        private void Bind(string key, UnityAction action)
        {
            Button button = Get<Button>(key);
            if (button == null || !wiredButtons.Add(button))
                return;
            button.onClick.AddListener(action);
            clickHandlers.Add(new KeyValuePair<Button, UnityAction>(button, action));
        }

        public void Show(string id)
        {
            if (routing || string.IsNullOrEmpty(id))
                return;
            if (id == "final-knowledge" && State != null && State.evaluation != null && State.evaluation.completed && State.evaluation.postComplete && State.campaign != null && !string.IsNullOrEmpty(State.campaign.personalEnding)) id = "complete";
            if (id == "biography")
            {
                ShowBiography();
                return;
            }

            if (!roots.TryGetValue(id, out GameObject root) || root == null)
            {
                string scene = SceneFor(id);
                if (scene != null && scene != SceneManager.GetActiveScene().name)
                {
                    CancelModal();
                    GameSession.PendingScreen = id;
                    routing = true;
                    StartCoroutine(LoadSceneAfterFade(scene));
                    return;
                }

                Debug.LogError("GameFlowController: missing screen binding " + id + " in " + SceneManager.GetActiveScene().name, this);
                SetStatus(ArticyNarrative.Copy("Copy_a2ba254fd31afa60") + id + "».");
                return;
            }

            CancelModal();
            var unique = new HashSet<GameObject>(roots.Values);
            foreach (GameObject item in unique)
                if (item != null)
                    item.SetActive(false);
            root.SetActive(true);
            currentScreen = id;
            SetText("app.status", "");
            SetText(id + ".status", "");
            Render();
            CleanPresentation();
            if (!string.IsNullOrEmpty(saveWarning))
                SetStatus(saveWarning);
            FocusPrimary();
        }

        private IEnumerator LoadSceneAfterFade(string scene)
        {
            CanvasGroup group = roots.TryGetValue(currentScreen ?? "", out GameObject root) ? root.GetComponent<CanvasGroup>() : null;
            if (group != null)
            {
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            float elapsed = 0f;
            while (elapsed < 0.18f)
            {
                elapsed += Time.unscaledDeltaTime;
                if (group != null)
                    group.alpha = 1f - Mathf.Clamp01(elapsed / 0.18f);
                yield return null;
            }

            SceneManager.LoadScene(scene);
        }

        private static string SceneFor(string id)
        {
            if (id == "main-menu" || id == "slots-new" || id == "slots-load" || id == "settings")
                return "MainMenu";
            if (id == "prologue" || id == "biography" || id.StartsWith("bio-", StringComparison.Ordinal))
                return "Introduction";
            if (id == "prytany-title" || id == "lottery" || id == "map-stela" || id == "pause" || id == "profile" || IsCityScreen(id) || IsCampaignScreen(id))
                return "Gameplay";
            return null;
        }

        private GameState NewState()
        {
            return new GameState
            {
                runId = Guid.NewGuid().ToString("N"),
                contentVersion = config.contentVersion,
                playerAge = config.playerAge,
                profession = config.defaultProfession,
                baseline = config.startingMetrics.Copy(),
                metrics = config.startingMetrics.Copy(),
                basePersonalFunds = config.startingPersonalFunds,
                personalFunds = config.startingPersonalFunds,
                baseCityFunds = config.startingCityFunds,
                cityFunds = config.startingCityFunds
            };
        }

        private void EnsureState()
        {
            if (GameSession.State == null)
            {
                GameSession.State = NewState();
                GameSession.Slot = -1;
            }
            // Upgrade the old generic placeholder, preserving any existing custom name.
            if (string.IsNullOrWhiteSpace(State.playerName) || State.playerName == "Αθηναίος")
                State.playerName = GameState.DefaultPlayerName;
        }

        private bool SaveCheckpoint()
        {
            if (State == null || GameSession.Slot < 0)
                return true; // Direct scene preview never writes to a real slot.
            bool ok = SaveStore.Save(GameSession.Slot, State, out string error);
            if (ok)
                ResearchTelemetry.Capture(State, campaignContent);
            saveWarning = ok ? null : error;
            if (!ok)
            {
                SetStatus(error);
                Debug.LogWarning(error, this);
            }

            return ok;
        }

        private void RecalculateMetrics()
        {
            GameRules.Recalculate(State);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                SaveStore.FlushWebStorage();
        }

        private static void ClearSelection()
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events == null)
                return;
            events.firstSelectedGameObject = null;
            events.SetSelectedGameObject(null);
        }

        private void FocusPrimary()
        {
            if (SceneManager.GetActiveScene().name == "MainMenu" || currentScreen == "prytany-title" || currentScreen == "pause" || currentScreen == "profile" || IsCityScreen(currentScreen) || IsCampaignScreen(currentScreen) || (currentScreen == "settings" && settingsFromPause))
            {
                ClearSelection();
                return;
            }

            if (currentScreen == "main-menu")
            {
                Focus(Get<Button>("main.new"));
                return;
            }

            Button primary = Get<Button>(currentScreen + ".primary");
            if (primary != null && primary.IsInteractable())
                Focus(primary);
            else
                Focus(Get<Button>(currentScreen + ".back"));
        }

        private static void Focus(Button target)
        {
            if (target == null || !target.isActiveAndEnabled || !target.IsInteractable() || UnityEngine.EventSystems.EventSystem.current == null)
                return;
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        private void SetStatus(string value)
        {
            string key = (currentScreen ?? "main-menu") + ".status";
            if (Get<TMP_Text>(key) == null)
                key = Get<TMP_Text>("app.status") != null ? "app.status" : "main.status";
            SetText(key, value);
        }

        private void SetText(string key, string value)
        {
            TMP_Text label = Get<TMP_Text>(key);
            if (label == null)
                return;
            label.richText = false;
            label.text = value ?? "";
        }

        private void SetImage(string key, Sprite sprite)
        {
            Image image = Get<Image>(key);
            if (image != null && sprite != null)
            {
                image.sprite = sprite;
                image.color = Color.white;
            }
        }

        private void SetActive(string key, bool active)
        {
            GameObject gameObject = Get<GameObject>(key);
            if (gameObject != null)
                gameObject.SetActive(active);
        }

        private void SetInteractable(string key, bool enabled)
        {
            Selectable selectable = Get<Selectable>(key);
            if (selectable != null)
                selectable.interactable = enabled;
        }

        private static string MetricDeltas(CharacterMetrics metrics)
        {
            if (metrics == null)
                return "";
            string[] names =
            {
                ArticyNarrative.Copy("Copy_5c3117cb8ed2675b"),
                ArticyNarrative.Copy("Copy_44cbf197ce871c19"),
                ArticyNarrative.Copy("Copy_2b3515f2ecd7824a"),
                ArticyNarrative.Copy("Copy_2c88568db2fddb62"),
                ArticyNarrative.Copy("Copy_cc950e76fab76b7a"),
                ArticyNarrative.Copy("Copy_c638883310aed0c3")
            };
            int[] values =
            {
                metrics.consistency,
                metrics.ethics,
                metrics.authority,
                metrics.persuasion,
                metrics.parrhesia,
                metrics.recognition
            };
            var parts = new List<string>();
            for (int i = 0; i < names.Length; i++)
                if (values[i] != 0)
                    parts.Add(names[i] + " " + (values[i] > 0 ? "+" : "") + values[i]);
            return string.Join(" · ", parts);
        }
    }
}
