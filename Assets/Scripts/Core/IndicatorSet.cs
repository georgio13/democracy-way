using System;
using UnityEngine;

namespace DemocracyWay.Core
{
    /// <summary>The five game indicators. Order matters only for display.</summary>
    public enum IndicatorId
    {
        Eunomia,     // Ευνομία — τάξη και νομιμότητα της πόλεως
        Demofilia,   // Δημοφιλία — στήριξη του πλήθους
        Ethos,       // Ήθος — φήμη για δικαιοσύνη
        Kachypopsia, // Καχυποψία — υποψία των άλλων (μόνο όταν profile.suspicionEnabled)
        Oikos        // Οίκος — ευημερία της περιουσίας
    }

    /// <summary>
    /// Current values of the five indicators, clamped to [0,100].
    /// Kachypopsia is always stored but only shown/used when the profile enables it
    /// (see <see cref="CitizenProfile.suspicionEnabled"/>).
    /// Plain serializable fields so JsonUtility round-trips the save unchanged.
    /// </summary>
    [Serializable]
    public class IndicatorSet
    {
        public const int Min = 0;
        public const int Max = 100;

        public int eunomia = 50;
        public int demofilia = 50;
        public int ethos = 50;
        public int kachypopsia = 50;
        public int oikos = 50;

        public int Get(IndicatorId id) => id switch
        {
            IndicatorId.Eunomia => eunomia,
            IndicatorId.Demofilia => demofilia,
            IndicatorId.Ethos => ethos,
            IndicatorId.Kachypopsia => kachypopsia,
            IndicatorId.Oikos => oikos,
            _ => 0
        };

        public void Set(IndicatorId id, int value)
        {
            value = Mathf.Clamp(value, Min, Max);
            switch (id)
            {
                case IndicatorId.Eunomia: eunomia = value; break;
                case IndicatorId.Demofilia: demofilia = value; break;
                case IndicatorId.Ethos: ethos = value; break;
                case IndicatorId.Kachypopsia: kachypopsia = value; break;
                case IndicatorId.Oikos: oikos = value; break;
            }
        }

        /// <summary>Adds <paramref name="delta"/> (positive or negative), clamped.</summary>
        public void Apply(IndicatorId id, int delta) => Set(id, Get(id) + delta);

        /// <summary>Deep copy, used when starting a run from the config's defaults.</summary>
        public IndicatorSet Clone() => (IndicatorSet)MemberwiseClone();
    }
}
