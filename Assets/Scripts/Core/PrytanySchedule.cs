using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.Core
{
    /// <summary>
    /// The prytany rota for one game. Historically the Athenian year was split
    /// into ten prytanies, one per Kleisthenic tribe, and the order was drawn by
    /// lot — no tribe held the presidency twice in the same year. This class
    /// reproduces that: it shuffles the ten tribe ids once at the start of a run
    /// and then walks the shuffled order, one entry per round.
    ///
    /// So <c>TotalRounds == number of tribes</c>: the game lasts exactly as long
    /// as it takes every tribe to preside once.
    /// </summary>
    [Serializable]
    public class PrytanySchedule
    {
        [SerializeField] private string[] order = Array.Empty<string>();
        [SerializeField] private string[] displayNames = Array.Empty<string>();
        [SerializeField] private int currentRound; // 0-based index into order

        /// <summary>Number of rounds in the game — one per prytany.</summary>
        public int TotalRounds => order != null ? order.Length : 0;

        /// <summary>1-based round number for display ("Γύρος 3 / 10").</summary>
        public int RoundNumber => currentRound + 1;

        public bool IsFinished => order == null || currentRound >= order.Length;

        /// <summary>Tribe id currently holding the prytany, or null when the
        /// year is over.</summary>
        public string CurrentTribeId =>
            (order != null && currentRound >= 0 && currentRound < order.Length)
                ? order[currentRound]
                : null;

        public string CurrentTribeName =>
            (displayNames != null && currentRound >= 0 && currentRound < displayNames.Length)
                ? displayNames[currentRound]
                : "—";

        /// <summary>
        /// Draws a fresh rota from the given tribes. Order is randomised, so two
        /// runs with the same tribe list still get different prytany sequences.
        /// </summary>
        public static PrytanySchedule Draw(IReadOnlyList<CreationOption> tribes)
        {
            var schedule = new PrytanySchedule();
            if (tribes == null || tribes.Count == 0) return schedule;

            var ids   = new List<string>(tribes.Count);
            var names = new List<string>(tribes.Count);
            for (int i = 0; i < tribes.Count; i++)
            {
                ids.Add(tribes[i].id);
                names.Add(tribes[i].title);
            }

            // Fisher–Yates over both lists in lockstep so id[i] always pairs
            // with name[i].
            for (int i = ids.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (ids[i], ids[j])     = (ids[j], ids[i]);
                (names[i], names[j]) = (names[j], names[i]);
            }

            schedule.order        = ids.ToArray();
            schedule.displayNames = names.ToArray();
            schedule.currentRound = 0;
            return schedule;
        }

        /// <summary>Advances to the next prytany. Returns false when the year
        /// has ended (no more rounds).</summary>
        public bool Advance()
        {
            if (IsFinished) return false;
            currentRound++;
            return !IsFinished;
        }

        /// <summary>True when the player's own tribe is presiding this round —
        /// the hook for "your tribe is in charge" content later on.</summary>
        public bool IsPlayerTribePresiding(CitizenProfile profile) =>
            profile != null &&
            !string.IsNullOrEmpty(profile.tribeId) &&
            profile.tribeId == CurrentTribeId;
    }
}
