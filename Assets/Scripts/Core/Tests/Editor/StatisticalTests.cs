using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Agora.Tests
{
    /// <summary>
    /// VERIFICATION.md ενότητα Ε: 1000 παρτίδες ανά στρατηγική, μόνο με AssemblyRules/EndingRules
    /// (όχι GameSession), ζευγίτης, σταθερό seed. Αντιστοιχία με τον προσομοιωτή Python.
    /// </summary>
    public sealed class StatisticalTests
    {
        const int Games = 1000;
        const int Seed = 20321;

        enum Strategy { Silent, DemocratOrator, OligarchOrator, DemocratBriber }

        sealed class Stats
        {
            public int totalCases, totalPassedA, totalFlips;
            public readonly int[] casePassedA = new int[21];
            public readonly Dictionary<CityEnding, int> city = new Dictionary<CityEnding, int>();
            public readonly Dictionary<PersonalEnding, int> personal = new Dictionary<PersonalEnding, int>();
            public double sumId, sumEm, sumAnagn, sumSynepeia, sumEthos;

            public double ARate => totalPassedA / (double)totalCases;
            public double CityPct(CityEnding e) => 100.0 * (city.TryGetValue(e, out int n) ? n : 0) / Games;
            public double PersonalPct(PersonalEnding e) => 100.0 * (personal.TryGetValue(e, out int n) ? n : 0) / Games;
            public double MeanFlips => totalFlips / (double)Games;
        }

        static Stats Run(Strategy strategy)
        {
            var content = TestWorld.Content;
            var cfg = TestWorld.Cfg();
            var rng = new Random(Seed);
            var stats = new Stats();
            Side towards = strategy == Strategy.OligarchOrator ? Side.B : Side.A;

            for (int game = 0; game < Games; game++)
            {
                var city = new CityState(cfg.cityStart);
                var player = new PlayerState(cfg, SocialClass.Zeugites, "sim");

                for (int prytany = 1; prytany <= 10; prytany++)
                {
                    AssemblyRules.StartPrytany(city, player, cfg, prytany);
                    for (int index = 0; index < 2; index++)
                    {
                        var c = content.CaseAt(prytany, index);
                        var stances = AssemblyRules.CloneStances(c);
                        bool spoke = strategy != Strategy.Silent;
                        float power = 0f;

                        if (strategy == Strategy.DemocratOrator || strategy == Strategy.OligarchOrator)
                        {
                            var neutral = stances.FirstOrDefault(s => s.side == Side.None && s.weight > 0);
                            if (neutral != null) AssemblyRules.Persuade(neutral, player, towards);
                            var logo = content.logographers.Where(l => l.costDrachmas <= player.wallet)
                                .OrderByDescending(l => l.costDrachmas).FirstOrDefault();
                            if (logo != null) player.wallet -= logo.costDrachmas;
                            power = AssemblyRules.SpeechPower(player, logo, cfg);
                        }
                        else if (strategy == Strategy.DemocratBriber)
                        {
                            var target = stances.Where(s => s.official != null && s.official.bribable && s.weight > 0 && s.side != towards)
                                .OrderBy(s => s.weight).FirstOrDefault();
                            if (target != null) AssemblyRules.Bribe(target, player, city, cfg, towards, rng, out _);
                            power = AssemblyRules.SpeechPower(player, null, cfg);
                        }

                        float s0 = AssemblyRules.ComputeS0(c, city, cfg, stances);
                        var result = AssemblyRules.Resolve(s0, spoke ? towards : Side.None, power, rng, cfg);
                        AssemblyRules.ApplyOutcome(c, result.passedA, city, cfg);
                        Side vote = strategy == Strategy.Silent ? (rng.Next(2) == 0 ? Side.A : Side.B) : towards;
                        AssemblyRules.ApplyPlayerAfterCase(player, cfg, spoke, spoke ? towards : Side.None, vote, result);

                        stats.totalCases++;
                        if (result.passedA) { stats.totalPassedA++; stats.casePassedA[c.id]++; }
                        if (result.playerFlipped) stats.totalFlips++;
                    }
                }

                var cityEnding = EndingRules.City(city, cfg);
                stats.city[cityEnding] = stats.city.TryGetValue(cityEnding, out int n) ? n + 1 : 1;
                var personalEnding = EndingRules.Personal(player, cityEnding, cfg);
                stats.personal[personalEnding] = stats.personal.TryGetValue(personalEnding, out int p) ? p + 1 : 1;
                stats.sumId += city.demosPower;
                stats.sumEm += city.trust;
                stats.sumAnagn += player.anagnorisimotita;
                stats.sumSynepeia += player.synepeia;
                stats.sumEthos += player.ethos;
            }
            return stats;
        }

        static void InRange(double value, double min, double max, string label) =>
            Assert.IsTrue(value >= min && value <= max, label + " = " + value.ToString("0.##") + " εκτός [" + min + ", " + max + "]");

        // Στόχοι: REBALANCE.md (σταθερά πρόσωπα, noise 6, νέες βάσεις).
        // Ανοχές: ±8 μονάδες στα ποσοστά τελών, ±0,05 στην Α-έγκριση, ±1,5 στα flips, ±5 στους μέσους δείκτες.

        [Test]
        public void E_Silent()
        {
            var s = Run(Strategy.Silent);
            InRange(s.ARate, 0.51, 0.61, "Α-έγκριση");
            InRange(s.CityPct(CityEnding.Counterfactual), 37, 53, "Π4 Counterfactual %");
            InRange(s.CityPct(CityEnding.GarrisonOnly), 47, 63, "Π5 GarrisonOnly %");
            Assert.LessOrEqual(s.CityPct(CityEnding.PropertyLimit) + s.CityPct(CityEnding.Stasis), 8.0, "Π2+Π3 %");
            InRange(s.sumId / Games, 45, 55, "τελικό ΙΔ");
            InRange(s.sumEm / Games, 47, 57, "τελικό Εμ");
            Assert.AreEqual(0, s.totalFlips, "flips σιωπηλού (δεν μιλά ποτέ)");
            Assert.LessOrEqual(s.sumAnagn / Games, 0.5, "τελική Αναγνωρισιμότητα (αναμένεται 0)");
        }

        [Test]
        public void E_Silent_PerCaseApprovalRates()
        {
            var s = Run(Strategy.Silent);
            int[] expected = { 0, 100, 60, 28, 52, 100, 52, 55, 61, 88, 29, 51, 52, 33, 42, 35, 61, 92, 75, 22, 41 };
            var failures = new List<string>();
            for (int id = 1; id <= 20; id++)
            {
                double actual = 100.0 * s.casePassedA[id] / Games;
                if (Math.Abs(actual - expected[id]) > 8)
                    failures.Add("υπόθεση " + id + ": " + actual.ToString("0.#") + "% (αναμενόταν " + expected[id] + "±8)");
            }
            if (failures.Count > 0)
                Assert.Fail("Απόκλιση από τον πίνακα Ε του REBALANCE.md: " + string.Join(" · ", failures));
        }

        [Test]
        public void E_DemocratOrator()
        {
            var s = Run(Strategy.DemocratOrator);
            InRange(s.ARate, 0.90, 1.00, "Α-έγκριση");
            Assert.GreaterOrEqual(s.CityPct(CityEnding.Counterfactual), 92.0, "Π4 %");
            InRange(s.MeanFlips, 10.6, 13.6, "μέσα flips");
            InRange(s.sumId / Games, 88, 98, "τελικό ΙΔ");
            InRange(s.sumEm / Games, 85, 95, "τελικό Εμ");
            Assert.GreaterOrEqual(s.sumSynepeia / Games, 99.9, "Συνέπεια (100)");
        }

        [Test]
        public void E_OligarchOrator()
        {
            var s = Run(Strategy.OligarchOrator);
            InRange(s.ARate, 0.08, 0.18, "Α-έγκριση");
            Assert.GreaterOrEqual(s.CityPct(CityEnding.PropertyLimit), 92.0, "Π2 %");
            InRange(s.MeanFlips, 10.5, 13.5, "μέσα flips");
            Assert.LessOrEqual(s.sumId / Games, 9.0, "τελικό ΙΔ");
            InRange(s.sumEm / Games, 6, 16, "τελικό Εμ");
        }

        [Test]
        public void E_DemocratBriber()
        {
            var s = Run(Strategy.DemocratBriber);
            Assert.LessOrEqual(s.sumEthos / Games, 0.5, "τελική Ηθική (0)");
            Assert.GreaterOrEqual(s.CityPct(CityEnding.Counterfactual), 91.0, "Π4 %");
            InRange(s.MeanFlips, 6.5, 9.5, "μέσα flips");
            InRange(s.sumId / Games, 63, 79, "τελικό ΙΔ");
            InRange(s.sumEm / Games, 45, 61, "τελικό Εμ");
        }
    }
}
