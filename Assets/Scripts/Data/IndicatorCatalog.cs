using System;
using System.Collections.Generic;
using UnityEngine;
using DemocracyWay.Core;

namespace DemocracyWay.Data
{
    /// <summary>
    /// Display data for the five indicators: name shown in the HUD and the
    /// tooltip text shown on hover. The author edits texts here; the game
    /// logic (values, clamping) lives in <see cref="IndicatorSet"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "IndicatorCatalog", menuName = "DemocracyWay/Indicator Catalog")]
    public class IndicatorCatalog : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public IndicatorId id;

            [Tooltip("Το όνομα του δείκτη όπως εμφανίζεται στο HUD.")]
            public string displayName = "";

            [Tooltip("Το κείμενο του tooltip όταν ο παίκτης κάνει hover στον δείκτη.")]
            [TextArea(2, 5)]
            public string description = "";

            [Tooltip("Αν ενεργό, ο δείκτης εμφανίζεται μόνο όταν το προφίλ έχει suspicionEnabled (Καχυποψία).")]
            public bool onlyWhenSuspicionEnabled;

            [Tooltip("Αν ενεργό, η υψηλή τιμή είναι ΚΑΚΗ (π.χ. Καχυποψία) — το HUD μπορεί να το χρωματίσει ανάποδα.")]
            public bool highIsBad;
        }

        public List<Entry> entries = new List<Entry>();

        public Entry Get(IndicatorId id) => entries.Find(e => e.id == id);

        /// <summary>The entries this run actually shows, in catalog order.</summary>
        public IEnumerable<Entry> VisibleFor(bool suspicionEnabled)
        {
            foreach (var e in entries)
                if (!e.onlyWhenSuspicionEnabled || suspicionEnabled)
                    yield return e;
        }
    }
}
