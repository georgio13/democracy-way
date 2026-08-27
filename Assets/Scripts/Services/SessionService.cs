using System;
using UnityEngine;
using DemocracyWay.Core;
using DemocracyWay.Data;

namespace DemocracyWay.Services
{
    /// <summary>
    /// Owns the run in progress: the <see cref="GameSession"/>, its save slot,
    /// autosaving, and the analytics trail. Dialogue and HUD talk to this and
    /// only this — nothing else touches SaveSystem or AnalyticsLog directly.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Session Service")]
    [DisallowMultipleComponent]
    public class SessionService : MonoBehaviour
    {
        /// <summary>The run in progress, or null while in the main menu.</summary>
        public GameSession Current { get; private set; }

        /// <summary>Slot this run saves into. -1 while no run is active.</summary>
        public int CurrentSlot { get; private set; } = -1;

        public bool HasActiveRun => Current != null && CurrentSlot >= 0;

        /// <summary>Raised after any indicator changes, so HUDs refresh without polling.</summary>
        public event Action IndicatorsChanged;

        /// <summary>Raised after the calendar advances (week or prytany).</summary>
        public event Action CalendarChanged;

        void Update()
        {
            if (!HasActiveRun) return;
            var pause = ServicesRoot.Pause;
            if (pause != null && pause.IsPaused) return;
            Current.playtimeSeconds += Time.unscaledDeltaTime;
        }

        // ════════ Run lifecycle ════════

        /// <summary>
        /// Starts a fresh run from a completed character creation into an
        /// empty slot: default indicators from config, prytany order drawn by
        /// lot, immediate first save, analytics trail opened.
        /// </summary>
        public void StartNewGame(CitizenProfile profile, int slot)
        {
            var config = ServicesRoot.Config;
            Current = new GameSession
            {
                profile = profile,
                indicators = config != null ? config.startingIndicators.Clone() : new IndicatorSet(),
                calendar = PrytanyCalendar.CreateNew(
                    config != null && config.creationDatabase != null
                        ? config.creationDatabase.TribeIds
                        : Array.Empty<string>(),
                    config != null ? config.weeksPerPrytany : 5,
                    seed: Environment.TickCount),
                createdAtIso = DateTime.UtcNow.ToString("o"),
                analyticsSessionId = Guid.NewGuid().ToString("N")
            };
            CurrentSlot = slot;

            AnalyticsLog.Log(new AnalyticsEvent
            {
                session = Current.analyticsSessionId,
                type = "game_started",
                slot = slot,
                profileJson = JsonUtility.ToJson(profile)
            });

            SaveNow();
        }

        /// <summary>Loads a slot into play. False when the slot is empty/corrupt.</summary>
        public bool LoadGame(int slot)
        {
            var session = SaveSystem.Load(slot);
            if (session == null) return false;

            Current = session;
            CurrentSlot = slot;

            AnalyticsLog.Log(new AnalyticsEvent
            {
                session = Current.analyticsSessionId,
                type = "game_loaded",
                slot = slot,
                chapterId = Current.currentChapterId,
                prytany = Current.calendar.prytanyNumber,
                week = Current.calendar.weekNumber
            });
            return true;
        }

        public void SaveNow()
        {
            if (!HasActiveRun) return;
            SaveSystem.Save(CurrentSlot, Current);
        }

        /// <summary>Leaves the run (back to main menu) WITHOUT saving — the
        /// last autosave stands, as the confirm dialog warned.</summary>
        public void EndToMainMenu()
        {
            // The voice source is persistent; without this, a dialogue line
            // cut off mid-sentence keeps talking over the main menu.
            ServicesRoot.Audio?.StopVoice();
            Current = null;
            CurrentSlot = -1;
        }

        // ════════ Story progress ════════

        /// <summary>
        /// Marks the chapter the player is entering; checkpoint-saves. A no-op
        /// when the session is already in this chapter — that's the load-game
        /// path re-entering the scene, and resetting the dialogue position (or
        /// double-logging chapter_started) would corrupt the resume.
        /// </summary>
        public void SetChapter(ChapterDefinition chapter)
        {
            if (!HasActiveRun || chapter == null) return;
            if (Current.currentChapterId == chapter.chapterId) return;

            Current.currentChapterId = chapter.chapterId;
            Current.currentChapterTitle = chapter.title;
            Current.currentDialogueNodeId = "";   // fresh chapter starts at its first node

            AnalyticsLog.Log(new AnalyticsEvent
            {
                session = Current.analyticsSessionId,
                type = "chapter_started",
                slot = CurrentSlot,
                chapterId = chapter.chapterId,
                prytany = Current.calendar.prytanyNumber,
                week = Current.calendar.weekNumber
            });

            SaveNow();
        }

        /// <summary>
        /// Called by the DialogueRunner every time a node is shown, so any
        /// save taken mid-chapter (the weekly autosave) knows where to resume.
        /// In-memory only — it hits disk with whichever save happens next.
        /// </summary>
        public void SetDialoguePosition(string nodeId)
        {
            if (HasActiveRun)
                Current.currentDialogueNodeId = nodeId ?? "";
        }

        /// <summary>
        /// The single entry point for a player decision: records it in the
        /// save, logs it for analysis, applies indicator effects and flags,
        /// and advances the week when the choice says so.
        /// </summary>
        public void RecordChoice(DialogueNode node, DialogueChoice choice)
        {
            if (!HasActiveRun || node == null || choice == null) return;

            Current.choices.Add(new ChoiceRecord
            {
                chapterId = Current.currentChapterId,
                nodeId = node.id,
                choiceId = choice.id,
                choiceText = choice.text,
                prytany = Current.calendar.prytanyNumber,
                week = Current.calendar.weekNumber,
                atIso = DateTime.UtcNow.ToString("o")
            });

            AnalyticsLog.Log(new AnalyticsEvent
            {
                session = Current.analyticsSessionId,
                type = "choice_made",
                slot = CurrentSlot,
                chapterId = Current.currentChapterId,
                nodeId = node.id,
                choiceId = choice.id,
                choiceText = choice.text,
                prytany = Current.calendar.prytanyNumber,
                week = Current.calendar.weekNumber
            });

            foreach (var flag in choice.setFlags)
                Current.SetFlag(flag);

            if (choice.effects.Count > 0)
            {
                foreach (var effect in choice.effects)
                    Current.indicators.Apply(effect.indicator, effect.delta);
                IndicatorsChanged?.Invoke();
            }

            if (choice.advanceWeek)
                AdvanceWeek();
        }

        /// <summary>Advances one prytany week. This is THE autosave moment.</summary>
        public void AdvanceWeek()
        {
            if (!HasActiveRun) return;

            Current.calendar.AdvanceWeek();
            CalendarChanged?.Invoke();

            AnalyticsLog.Log(new AnalyticsEvent
            {
                session = Current.analyticsSessionId,
                type = "week_advanced",
                slot = CurrentSlot,
                chapterId = Current.currentChapterId,
                prytany = Current.calendar.prytanyNumber,
                week = Current.calendar.weekNumber
            });

            SaveNow();
        }
    }
}
