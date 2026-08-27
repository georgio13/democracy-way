using System;
using UnityEngine;

namespace DemocracyWay.Core
{
    /// <summary>
    /// The six choices the player makes in character creation, in the order
    /// they are presented. <see cref="Trittys"/> depends on <see cref="Tribe"/>:
    /// the options offered for it are filtered by the tribe already chosen.
    /// </summary>
    public enum CreationStep
    {
        Gender,
        Tribe,
        Trittys,
        Wealth,
        Period,
        Occupation
    }

    /// <summary>
    /// A single selectable option in one creation step. Purely data — the id is
    /// the stable key written into saves, everything else is presentation.
    ///
    /// <see cref="parentId"/> is what makes the tribe → trittys dependency work:
    /// trittys options carry the id of the tribe they belong to, and the
    /// creation UI shows only those whose parent matches the current selection.
    /// Options with an empty parentId are always shown.
    /// </summary>
    [Serializable]
    public class CreationOption
    {
        [Tooltip("Stable key persisted in save files. Never localise this.")]
        public string id;

        [Tooltip("Shown in the option list on the right.")]
        public string title;

        [Tooltip("Small line under the title in the list (e.g. a date range).")]
        public string subtitle;

        [Tooltip("Long text shown under the artwork on the left.")]
        [TextArea(3, 8)]
        public string description;

        [Tooltip("Artwork shown on the left when this option is highlighted.")]
        public Sprite artwork;

        [Tooltip("Empty = always available. Otherwise the id of the option in " +
                 "the previous step that this one belongs to (tribe → trittys).")]
        public string parentId;
    }

    /// <summary>
    /// The player's finished character. Six option ids plus the display names
    /// resolved at creation time, so a loaded save can show a readable summary
    /// without needing the database to still contain that exact option.
    /// </summary>
    [Serializable]
    public class CitizenProfile
    {
        public string genderId;
        public string tribeId;
        public string trittysId;
        public string wealthId;
        public string periodId;
        public string occupationId;

        // Denormalised display names — see class summary.
        public string genderName;
        public string tribeName;
        public string trittysName;
        public string wealthName;
        public string periodName;
        public string occupationName;

        /// <summary>True once all six steps have a chosen option.</summary>
        public bool IsComplete =>
            !string.IsNullOrEmpty(genderId) &&
            !string.IsNullOrEmpty(tribeId) &&
            !string.IsNullOrEmpty(trittysId) &&
            !string.IsNullOrEmpty(wealthId) &&
            !string.IsNullOrEmpty(periodId) &&
            !string.IsNullOrEmpty(occupationId);

        public string GetId(CreationStep step) => step switch
        {
            CreationStep.Gender     => genderId,
            CreationStep.Tribe      => tribeId,
            CreationStep.Trittys    => trittysId,
            CreationStep.Wealth     => wealthId,
            CreationStep.Period     => periodId,
            CreationStep.Occupation => occupationId,
            _ => null
        };

        public void Set(CreationStep step, CreationOption option)
        {
            string id   = option != null ? option.id    : null;
            string name = option != null ? option.title : null;

            switch (step)
            {
                case CreationStep.Gender:
                    genderId = id; genderName = name; break;
                case CreationStep.Tribe:
                    tribeId = id; tribeName = name;
                    // Changing tribe invalidates the trittys beneath it.
                    trittysId = null; trittysName = null;
                    break;
                case CreationStep.Trittys:
                    trittysId = id; trittysName = name; break;
                case CreationStep.Wealth:
                    wealthId = id; wealthName = name; break;
                case CreationStep.Period:
                    periodId = id; periodName = name; break;
                case CreationStep.Occupation:
                    occupationId = id; occupationName = name; break;
            }
        }

        /// <summary>One-line summary for save slots, e.g.
        /// "Ζευγίτης κεραμεύς — Ακαμαντίς / Κεραμείς".</summary>
        public string Summary()
        {
            if (string.IsNullOrEmpty(tribeName)) return "—";
            return $"{wealthName} {occupationName} — {tribeName} / {trittysName}";
        }
    }
}
