using System.Collections.Generic;
using Agora;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Οι πίνακες-στόχοι της ενότητας 4 του Agora_FINAL_DESIGN (από το final_sim.py, ζευγίτης) και η σύγκριση
    /// με ανοχή ±8 μονάδες τοις εκατό. Κοινά για το μενού του Editor και το test ισορροπίας.
    /// </summary>
    public static class BalanceTargets
    {
        public const double Tolerance = 8;

        /// <summary>Τέλη πόλης (%) ανά στρατηγική: άντεξε, φρουρά, όριο, στάσις, λιμός· και ψηφοφορίες που κρίνει.</summary>
        public static readonly Dictionary<BalanceSimulator.Strategy, (double endures, double garrison, double limit, double stasis, double famine, double decided)> City =
            new Dictionary<BalanceSimulator.Strategy, (double, double, double, double, double, double)>
            {
                { BalanceSimulator.Strategy.Silent,          (3, 71, 22, 5, 0, 0) },
                { BalanceSimulator.Strategy.DemocratSpeaker, (58, 42, 0, 0, 0, 8.3) },
                { BalanceSimulator.Strategy.OligarchSpeaker, (0, 0, 100, 0, 0, 7.4) },
                { BalanceSimulator.Strategy.DemocratBriber,  (76, 24, 0, 0, 0, 8.5) },
                { BalanceSimulator.Strategy.OligarchBriber,  (0, 0, 100, 0, 0, 7.5) },
                { BalanceSimulator.Strategy.Random,          (4, 68, 20, 8, 0, 3.2) },
                { BalanceSimulator.Strategy.HalfSpeakerA,    (17, 81, 1, 1, 0, 3.1) },
            };

        /// <summary>% Α ανά υπόθεση (1–20): σιωπηλός και δημοκράτης ρήτορας.</summary>
        public static readonly int[] SilentA = { 85, 52, 37, 51, 76, 62, 52, 71, 61, 43, 47, 43, 33, 61, 20, 48, 56, 45, 28, 19 };
        public static readonly int[] DemocratA = { 100, 81, 65, 100, 100, 95, 100, 100, 100, 95, 98, 100, 91, 100, 58, 100, 79, 83, 74, 62 };

        public struct Check
        {
            public string group, label;
            public double target, actual;
            public bool Ok => System.Math.Abs(actual - target) <= Tolerance;
        }

        /// <summary>Όλοι οι έλεγχοι ±8 της ενότητας 4. Οι «ψηφοφορίες που κρίνει» δείχνονται, αλλά δεν είναι ποσοστό.</summary>
        public static List<Check> Compare(IEnumerable<BalanceSimulator.Summary> summaries)
        {
            var checks = new List<Check>();
            foreach (var s in summaries)
            {
                if (City.TryGetValue(s.strategy, out var t))
                {
                    void Add(string label, double target, CityEnding e) =>
                        checks.Add(new Check { group = "city:" + s.strategy, label = label, target = target, actual = s.CityPercent(e) });
                    Add("Η πόλη άντεξε", t.endures, CityEnding.Counterfactual);
                    Add("Φρουρά", t.garrison, CityEnding.GarrisonOnly);
                    Add("Όριο περιουσίας", t.limit, CityEnding.PropertyLimit);
                    Add("Στάσις", t.stasis, CityEnding.Stasis);
                    Add("Λιμός", t.famine, CityEnding.Famine);
                }
                int[] perCase = s.strategy == BalanceSimulator.Strategy.Silent ? SilentA
                    : s.strategy == BalanceSimulator.Strategy.DemocratSpeaker ? DemocratA : null;
                if (perCase != null)
                    for (int i = 0; i < perCase.Length; i++)
                        checks.Add(new Check { group = "case:" + s.strategy, label = "Υπ. " + (i + 1), target = perCase[i], actual = s.CasePercentA(i) });
            }
            return checks;
        }
    }
}
