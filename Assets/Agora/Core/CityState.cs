using System;
using UnityEngine;

namespace Agora
{
    /// <summary>Η κατάσταση της πόλης. Απλή κλάση, serializable για τηλεμετρία/αποθήκευση.</summary>
    [Serializable]
    public class CityState
    {
        public int treasury, food, readiness, trust, demosPower;   // demosPower = ΙΔ, κρυφός
        public bool war;
        public bool garrison;        // μετά την 15Β
        public bool payAbolished;    // μετά την 12Β
        public bool areopagusRestored; // μετά την 18Β
        public bool auditFinePending;  // μετά την 17Α

        public CityState(int start)
        {
            treasury = food = readiness = trust = demosPower = start;
        }

        public int Get(Metric m) => m switch
        {
            Metric.Treasury => treasury,
            Metric.Food => food,
            Metric.Readiness => readiness,
            Metric.Trust => trust,
            _ => demosPower
        };

        public void Add(Metric m, int delta)
        {
            switch (m)
            {
                case Metric.Treasury: treasury = Clamp(treasury + delta); break;
                case Metric.Food: food = Clamp(food + delta); break;
                case Metric.Readiness: readiness = Clamp(readiness + delta); break;
                case Metric.Trust: trust = Clamp(trust + delta); break;
                default: demosPower = Clamp(demosPower + delta); break;
            }
        }

        public int MinVisible() => Mathf.Min(treasury, food, readiness, trust);
        public int MinAll() => Mathf.Min(MinVisible(), demosPower);

        public static int Clamp(int v) => Mathf.Clamp(v, 0, 100);
    }
}
