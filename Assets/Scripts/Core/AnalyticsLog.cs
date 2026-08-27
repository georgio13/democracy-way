using System;
using System.IO;
using UnityEngine;

namespace DemocracyWay.Core
{
    /// <summary>
    /// One analytics event as a flat record: every field exists on every event
    /// (unused ones stay empty/zero), so the whole log opens directly in
    /// pandas or Excel without any nested-JSON handling.
    /// </summary>
    [Serializable]
    public class AnalyticsEvent
    {
        public string t = "";          // UTC ISO-8601 timestamp
        public string session = "";    // GameSession.analyticsSessionId
        public string type = "";       // game_started | game_loaded | chapter_started | choice_made | week_advanced
        public int slot = -1;
        public string chapterId = "";
        public string nodeId = "";
        public string choiceId = "";
        public string choiceText = "";
        public int prytany;
        public int week;
        public string profileJson = ""; // full CitizenProfile, only on game_started
    }

    /// <summary>
    /// Append-only JSONL log of every player decision, for later analysis:
    /// persistentDataPath/analytics/events.jsonl — one JSON object per line.
    ///
    /// Analytics must never break the game, so every failure is swallowed
    /// with a warning. Remote collection can later ship this same file; the
    /// format is the contract.
    /// </summary>
    public static class AnalyticsLog
    {
        /// <summary>Tests point this at a temp directory. Null in the game itself.</summary>
        public static string RootOverride;

        private static string Dir =>
            Path.Combine(RootOverride ?? Application.persistentDataPath, "analytics");
        public static string FilePath => Path.Combine(Dir, "events.jsonl");

        public static void Log(AnalyticsEvent evt)
        {
            if (evt == null) return;
            try
            {
                evt.t = DateTime.UtcNow.ToString("o");
                Directory.CreateDirectory(Dir);
                File.AppendAllText(FilePath, JsonUtility.ToJson(evt) + "\n");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AnalyticsLog] Δεν γράφτηκε το event '{evt.type}': {e.Message}");
            }
        }
    }
}
