using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.Core
{
    using DemocracyWay.Dialogue;

    /// <summary>
    /// The one persistent object that owns the current run. Lives in Bootstrap,
    /// survives every scene load, and is the only thing scenes need to find in
    /// order to read the profile, the indicators or the prytany.
    ///
    /// It also carries the two content databases, so scenes never have to load
    /// them from Resources — they ask <see cref="Instance"/> instead.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Game State Service")]
    [DisallowMultipleComponent]
    public class GameStateService : MonoBehaviour
    {
        public static GameStateService Instance { get; private set; }

        [Header("Content")]
        [SerializeField] private CreationDatabase creationDatabase;
        [SerializeField] private DialogueDatabase dialogueDatabase;

        public CreationDatabase Creation => creationDatabase;
        public DialogueDatabase Dialogues => dialogueDatabase;

        /// <summary>The run in progress. Never null after Awake — a fresh
        /// unstarted session stands in until New Game or Load replaces it.</summary>
        public GameSession Session { get; private set; } = new GameSession();

        /// <summary>Slot this run will autosave into. -1 until the player picks
        /// one (new game) or loads one.</summary>
        public int CurrentSlot { get; private set; } = -1;

        /// <summary>Raised whenever any indicator changes, so HUDs can refresh
        /// without polling.</summary>
        public event Action OnIndicatorsChanged;

        /// <summary>Raised when the prytany advances to a new round.</summary>
        public event Action OnRoundChanged;

        private readonly HashSet<string> seenDialogues = new HashSet<string>();

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

        void Update()
        {
            // Playtime accrues only once a run actually exists.
            if (CurrentSlot >= 0 && Session != null)
                Session.playtimeSeconds += Time.unscaledDeltaTime;
        }

        // ═══════════════════════════════════════
        // Run lifecycle
        // ═══════════════════════════════════════

        /// <summary>
        /// Starts a fresh run from a finished character-creation profile.
        /// Indicators get random starting values for now (placeholder — they
        /// will later be derived from the profile), and the prytany rota is
        /// drawn from the tribe list.
        /// </summary>
        public void StartNewRun(CitizenProfile profile, int slot)
        {
            Session = new GameSession
            {
                profile = profile ?? new CitizenProfile(),
                indicators = new IndicatorSet(),
                playtimeSeconds = 0f
            };
            Session.indicators.Randomise();
            Session.prytany = PrytanySchedule.Draw(
                creationDatabase != null ? creationDatabase.tribes : null);

            seenDialogues.Clear();
            Session.seenDialogueIds = Array.Empty<string>();

            CurrentSlot = slot;
            SaveNow();

            OnIndicatorsChanged?.Invoke();
            OnRoundChanged?.Invoke();
        }

        /// <summary>Loads a slot into the live session. Returns false when the
        /// slot is empty or unreadable.</summary>
        public bool LoadRun(int slot)
        {
            var loaded = SaveSystem.Load(slot);
            if (loaded == null) return false;

            Session = loaded;
            CurrentSlot = slot;

            seenDialogues.Clear();
            if (Session.seenDialogueIds != null)
                foreach (var id in Session.seenDialogueIds) seenDialogues.Add(id);

            OnIndicatorsChanged?.Invoke();
            OnRoundChanged?.Invoke();
            return true;
        }

        /// <summary>Writes the live session into its slot. No-op before a slot
        /// has been chosen.</summary>
        public bool SaveNow()
        {
            if (CurrentSlot < 0 || Session == null) return false;
            Session.seenDialogueIds = new List<string>(seenDialogues).ToArray();
            return SaveSystem.Save(CurrentSlot, Session);
        }

        // ═══════════════════════════════════════
        // Indicators
        // ═══════════════════════════════════════

        public IndicatorSet Indicators => Session != null ? Session.indicators : null;

        public void ApplyDeltas(IList<IndicatorDelta> deltas)
        {
            if (Session == null || Session.indicators == null || deltas == null) return;
            Session.indicators.ApplyAll(deltas);
            OnIndicatorsChanged?.Invoke();
            SaveNow();
        }

        // ═══════════════════════════════════════
        // Prytany / rounds
        // ═══════════════════════════════════════

        public PrytanySchedule Prytany => Session != null ? Session.prytany : null;

        /// <summary>Moves to the next prytany. Returns false when the year has
        /// ended — the caller decides what an ended year means.</summary>
        public bool AdvanceRound()
        {
            if (Session == null || Session.prytany == null) return false;
            bool stillRunning = Session.prytany.Advance();
            OnRoundChanged?.Invoke();
            SaveNow();
            return stillRunning;
        }

        // ═══════════════════════════════════════
        // Dialogue
        // ═══════════════════════════════════════

        /// <summary>Draws a dialogue the player has not seen yet where possible.
        /// Returns null when the database is empty.</summary>
        public DialogueEntry PickRandomDialogue()
        {
            if (dialogueDatabase == null) return null;
            return dialogueDatabase.PickRandom(seenDialogues);
        }

        public void MarkDialogueSeen(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (seenDialogues.Add(id)) SaveNow();
        }
    }
}
