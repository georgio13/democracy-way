using System;
using UnityEngine;

namespace DemocracyWay.Core
{
    /// <summary>
    /// Everything about one playthrough that needs to survive a save/load:
    /// who the player is, where the five indicators stand, and which prytany
    /// the year has reached. Plain [Serializable] so JsonUtility handles it.
    /// </summary>
    [Serializable]
    public class GameSession
    {
        public const int SchemaVersion = 1;

        public int schemaVersion = SchemaVersion;

        /// <summary>UTC ISO-8601. Written on every save, shown in the slot list.</summary>
        public string savedAtUtc;

        public CitizenProfile profile = new CitizenProfile();
        public IndicatorSet indicators = new IndicatorSet();
        public PrytanySchedule prytany = new PrytanySchedule();

        /// <summary>Seconds of wall-clock play, accumulated across sessions.</summary>
        public float playtimeSeconds;

        /// <summary>Ids of dialogues already seen, so the random picker can
        /// prefer unseen ones. Serialised as a flat array for JsonUtility.</summary>
        public string[] seenDialogueIds = Array.Empty<string>();

        public string SavedAtDisplay()
        {
            if (string.IsNullOrEmpty(savedAtUtc)) return "—";
            return DateTime.TryParse(
                savedAtUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var parsed)
                ? parsed.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                : savedAtUtc;
        }

        public string PlaytimeDisplay()
        {
            var span = TimeSpan.FromSeconds(Mathf.Max(0f, playtimeSeconds));
            return span.TotalHours >= 1
                ? $"{(int)span.TotalHours}ω {span.Minutes:00}λ"
                : $"{span.Minutes}λ";
        }
    }
}
