using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.Core
{
    /// <summary>
    /// Every option offered in character creation, grouped by step. Generated
    /// and populated by <c>Tools ▸ DemocracyWay ▸ Init</c> (see
    /// <c>DemocracyWayInit.CreationContent.cs</c>), but it is a plain
    /// ScriptableObject — you can edit any text or swap any artwork in the
    /// Inspector afterwards and the game picks it up with no code change.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CreationDatabase",
        menuName = "DemocracyWay/Creation Database")]
    public class CreationDatabase : ScriptableObject
    {
        [Header("Βήμα 1 — Φύλο")]
        public List<CreationOption> genders = new List<CreationOption>();

        [Header("Βήμα 2 — Φυλή")]
        public List<CreationOption> tribes = new List<CreationOption>();

        [Header("Βήμα 3 — Τριττύα (φιλτράρεται από τη φυλή)")]
        [Tooltip("Each entry's parentId must be the id of its tribe.")]
        public List<CreationOption> trittyes = new List<CreationOption>();

        [Header("Βήμα 4 — Οικονομική κατάσταση")]
        public List<CreationOption> wealthClasses = new List<CreationOption>();

        [Header("Βήμα 5 — Περίοδος")]
        public List<CreationOption> periods = new List<CreationOption>();

        [Header("Βήμα 6 — Επάγγελμα")]
        public List<CreationOption> occupations = new List<CreationOption>();

        /// <summary>Greek heading shown above the option list for each step.</summary>
        public static string StepTitle(CreationStep step) => step switch
        {
            CreationStep.Gender     => "Φύλο",
            CreationStep.Tribe      => "Φυλή",
            CreationStep.Trittys    => "Τριττύα",
            CreationStep.Wealth     => "Οικονομική κατάστασις",
            CreationStep.Period     => "Περίοδος",
            CreationStep.Occupation => "Επάγγελμα",
            _ => step.ToString()
        };

        /// <summary>All options for a step, unfiltered.</summary>
        public List<CreationOption> AllFor(CreationStep step) => step switch
        {
            CreationStep.Gender     => genders,
            CreationStep.Tribe      => tribes,
            CreationStep.Trittys    => trittyes,
            CreationStep.Wealth     => wealthClasses,
            CreationStep.Period     => periods,
            CreationStep.Occupation => occupations,
            _ => new List<CreationOption>()
        };

        /// <summary>
        /// Options actually offered for a step given what the player has already
        /// chosen. This is where the tribe → trittys dependency is enforced:
        /// a trittys is shown only when its parentId matches the chosen tribe.
        /// </summary>
        public List<CreationOption> OptionsFor(CreationStep step, CitizenProfile profile)
        {
            var all = AllFor(step);
            if (step != CreationStep.Trittys) return all;

            string tribeId = profile != null ? profile.tribeId : null;
            var filtered = new List<CreationOption>();
            if (string.IsNullOrEmpty(tribeId)) return filtered;

            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].parentId == tribeId)
                    filtered.Add(all[i]);

            return filtered;
        }

        public CreationOption Find(CreationStep step, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var all = AllFor(step);
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].id == id) return all[i];
            return null;
        }
    }
}
