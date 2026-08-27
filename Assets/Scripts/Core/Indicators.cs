using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.Core
{
    /// <summary>
    /// The five values the player's decisions move. Enum names stay ASCII so
    /// they survive JSON round-trips cleanly; the Greek display names live in
    /// <see cref="IndicatorInfo"/>.
    /// </summary>
    public enum IndicatorType
    {
        Eunomia,       // Ευνομία
        Demos,         // Δήμος / Δημοφιλία
        Ethos,         // Ήθος & Ακεραιότητα
        Kachypopsia,   // Καχυποψία
        Oikos          // Οίκος & Συντήρηση
    }

    /// <summary>Display metadata for one indicator. Static — never serialised.</summary>
    public static class IndicatorInfo
    {
        public const int Min = 0;
        public const int Max = 100;

        public static readonly IndicatorType[] All =
            (IndicatorType[])Enum.GetValues(typeof(IndicatorType));

        public static string Name(IndicatorType t) => t switch
        {
            IndicatorType.Eunomia     => "Ευνομία",
            IndicatorType.Demos       => "Δήμος / Δημοφιλία",
            IndicatorType.Ethos       => "Ήθος & Ακεραιότητα",
            IndicatorType.Kachypopsia => "Καχυποψία",
            IndicatorType.Oikos       => "Οίκος & Συντήρηση",
            _ => t.ToString()
        };

        public static string Description(IndicatorType t) => t switch
        {
            IndicatorType.Eunomia     => "Η τάξη και η νομιμότητα της πόλεως.",
            IndicatorType.Demos       => "Πόσο σε στηρίζει το πλήθος των πολιτών.",
            IndicatorType.Ethos       => "Η φήμη σου για δικαιοσύνη και αδιαφθορία.",
            IndicatorType.Kachypopsia => "Η υποψία των άλλων απέναντί σου.",
            IndicatorType.Oikos       => "Η ευημερία του οίκου και της περιουσίας σου.",
            _ => string.Empty
        };

        /// <summary>Bar colour. Καχυποψία reads as a threat, so it is the one
        /// indicator drawn in red — high is bad, unlike the other four.</summary>
        public static Color BarColor(IndicatorType t) => t switch
        {
            IndicatorType.Eunomia     => new Color(0.45f, 0.62f, 0.85f),
            IndicatorType.Demos       => new Color(0.85f, 0.72f, 0.35f),
            IndicatorType.Ethos       => new Color(0.55f, 0.80f, 0.60f),
            IndicatorType.Kachypopsia => new Color(0.82f, 0.40f, 0.36f),
            IndicatorType.Oikos       => new Color(0.72f, 0.58f, 0.82f),
            _ => Color.white
        };
    }

    /// <summary>
    /// The five indicator values. Backed by a plain int[] indexed by
    /// <see cref="IndicatorType"/> so JsonUtility can serialise it directly —
    /// a Dictionary would not survive the round-trip.
    /// </summary>
    [Serializable]
    public class IndicatorSet
    {
        [SerializeField] private int[] values = new int[5];

        public IndicatorSet()
        {
            values = new int[IndicatorInfo.All.Length];
            for (int i = 0; i < values.Length; i++) values[i] = 50;
        }

        public int this[IndicatorType t]
        {
            get
            {
                EnsureSize();
                return values[(int)t];
            }
            set
            {
                EnsureSize();
                values[(int)t] = Mathf.Clamp(value, IndicatorInfo.Min, IndicatorInfo.Max);
            }
        }

        public void Apply(IndicatorType t, int delta) => this[t] = this[t] + delta;

        /// <summary>Seeds every indicator with a random starting value. Used for
        /// the current placeholder build — real starting values will later be
        /// derived from the citizen profile.</summary>
        public void Randomise(int min = 25, int max = 75)
        {
            EnsureSize();
            for (int i = 0; i < values.Length; i++)
                values[i] = UnityEngine.Random.Range(min, max + 1);
        }

        public IndicatorSet Clone()
        {
            var copy = new IndicatorSet();
            EnsureSize();
            Array.Copy(values, copy.values, values.Length);
            return copy;
        }

        /// <summary>Guards against a save written before an indicator was added
        /// to the enum — grows the array and fills new slots with the midpoint
        /// rather than throwing on index-out-of-range.</summary>
        private void EnsureSize()
        {
            int need = IndicatorInfo.All.Length;
            if (values == null)
            {
                values = new int[need];
                for (int i = 0; i < need; i++) values[i] = 50;
                return;
            }
            if (values.Length >= need) return;

            int oldLength = values.Length;
            Array.Resize(ref values, need);
            for (int i = oldLength; i < need; i++) values[i] = 50;
        }
    }

    /// <summary>
    /// A change to one indicator, attached to dialogue outcomes.
    /// </summary>
    [Serializable]
    public struct IndicatorDelta
    {
        public IndicatorType indicator;
        public int delta;

        /// <summary>"Ευνομία +5" / "Καχυποψία −3" for feedback text.</summary>
        public override string ToString()
        {
            string sign = delta >= 0 ? "+" : "−";
            return $"{IndicatorInfo.Name(indicator)} {sign}{Mathf.Abs(delta)}";
        }
    }

    /// <summary>Convenience list wrapper so a List&lt;IndicatorDelta&gt; can be
    /// applied in one call.</summary>
    public static class IndicatorDeltaExtensions
    {
        public static void ApplyAll(this IndicatorSet set, IList<IndicatorDelta> deltas)
        {
            if (set == null || deltas == null) return;
            for (int i = 0; i < deltas.Count; i++)
                set.Apply(deltas[i].indicator, deltas[i].delta);
        }
    }
}
