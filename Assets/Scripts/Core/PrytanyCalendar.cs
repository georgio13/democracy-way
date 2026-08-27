using System;
using System.Collections.Generic;

namespace DemocracyWay.Core
{
    /// <summary>What a call to <see cref="PrytanyCalendar.AdvanceWeek"/> caused.</summary>
    public struct WeekAdvanceResult
    {
        /// <summary>The week rolled over into a new prytany.</summary>
        public bool prytanyChanged;

        /// <summary>The last week of the last prytany has been passed — the year is over.</summary>
        public bool yearFinished;
    }

    /// <summary>
    /// The Athenian year: one prytany per tribe, drawn by lot at the start of a
    /// run (Fisher–Yates, no tribe presides twice — as historically). Numbers
    /// are 1-based because they are shown to the player as-is.
    /// </summary>
    [Serializable]
    public class PrytanyCalendar
    {
        /// <summary>Tribe ids in the order they preside. Drawn once per run.</summary>
        public List<string> tribeOrder = new List<string>();

        public int prytanyNumber = 1;   // 1..tribeOrder.Count
        public int weekNumber = 1;      // 1..weeksPerPrytany
        public int weeksPerPrytany = 5;

        /// <summary>Id of the tribe currently presiding, or "" after the year ends.</summary>
        public string PresidingTribeId =>
            (prytanyNumber >= 1 && prytanyNumber <= tribeOrder.Count)
                ? tribeOrder[prytanyNumber - 1]
                : "";

        public bool IsYearFinished => prytanyNumber > tribeOrder.Count;

        /// <summary>
        /// Advances one week; rolls into the next prytany when the current one
        /// ends. Callers (SessionService) autosave and log after this returns.
        /// </summary>
        public WeekAdvanceResult AdvanceWeek()
        {
            var result = new WeekAdvanceResult();
            if (IsYearFinished) { result.yearFinished = true; return result; }

            weekNumber++;
            if (weekNumber > weeksPerPrytany)
            {
                weekNumber = 1;
                prytanyNumber++;
                result.prytanyChanged = true;
                result.yearFinished = IsYearFinished;
            }
            return result;
        }

        /// <summary>
        /// Draws a fresh calendar: shuffles the tribe ids with Fisher–Yates so
        /// the presiding order is random but every tribe presides exactly once.
        /// </summary>
        public static PrytanyCalendar CreateNew(IEnumerable<string> tribeIds, int weeksPerPrytany, int seed)
        {
            var order = new List<string>(tribeIds);
            var rng = new Random(seed);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            return new PrytanyCalendar
            {
                tribeOrder = order,
                prytanyNumber = 1,
                weekNumber = 1,
                weeksPerPrytany = Math.Max(1, weeksPerPrytany)
            };
        }
    }
}
