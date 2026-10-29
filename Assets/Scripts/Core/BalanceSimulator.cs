using System;
using System.Collections.Generic;
using Agora;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Agora_FINAL_DESIGN ενότητα 8, βήμα 8: παίζει ολόκληρες παρτίδες με ΤΙΣ ΙΔΙΕΣ συναρτήσεις του παιχνιδιού
    /// (StartPrytany, WeekFlowRules, EndingRules) — κανένας κανόνας δεν ξαναγράφεται εδώ. Οι 7 στρατηγικές
    /// ακολουθούν τη λογική του final_sim.py, ώστε τα αποτελέσματα να συγκρίνονται με τον πίνακα της ενότητας 4.
    /// </summary>
    public static class BalanceSimulator
    {
        public enum Strategy { Silent, DemocratSpeaker, OligarchSpeaker, DemocratBriber, OligarchBriber, Random, HalfSpeakerA }

        public static readonly Strategy[] All = (Strategy[])Enum.GetValues(typeof(Strategy));

        /// <summary>Τι κάνει ο παίκτης μία εβδομάδα (όπως το «plan» του final_sim.py).</summary>
        struct Plan
        {
            public Side side;          // None = καμία πλευρά (σιωπηλός)
            public bool speak, accuse, bribeIfNeeded, persuade, bribe, logographerForTrial;
            public Side logographer;   // None = κανένας λόγος για πλευρά
            public Side vote;
        }

        public sealed class Game
        {
            public CityEnding city;
            public PersonalEnding personal;
            public int decided;                        // ψηφοφορίες που έκρινε ο παίκτης (λόγος + αξιωματούχος)
            public readonly bool[] passedA = new bool[20];
            public CityState finalCity;
            public PlayerState finalPlayer;
        }

        public sealed class Summary
        {
            public Strategy strategy;
            public int games;
            public readonly Dictionary<CityEnding, int> city = new Dictionary<CityEnding, int>();
            public readonly Dictionary<PersonalEnding, int> personal = new Dictionary<PersonalEnding, int>();
            public readonly int[] passedA = new int[20];
            public double decided, peitho, fame, ethos;

            public double CityPercent(CityEnding e) => games == 0 ? 0 : 100.0 * (city.TryGetValue(e, out int n) ? n : 0) / games;
            public double PersonalPercent(PersonalEnding e) => games == 0 ? 0 : 100.0 * (personal.TryGetValue(e, out int n) ? n : 0) / games;
            public double CasePercentA(int caseIndex) => games == 0 ? 0 : 100.0 * passedA[caseIndex] / games;
        }

        public static Summary Run(GameContent content, Strategy strategy, int games, int seed, SocialClass cls = SocialClass.Zeugites)
        {
            var rng = new Random(seed);
            var summary = new Summary { strategy = strategy, games = games };
            for (int g = 0; g < games; g++)
            {
                var r = Play(content, strategy, rng, cls);
                summary.city[r.city] = (summary.city.TryGetValue(r.city, out int c) ? c : 0) + 1;
                summary.personal[r.personal] = (summary.personal.TryGetValue(r.personal, out int p) ? p : 0) + 1;
                for (int i = 0; i < 20; i++) if (r.passedA[i]) summary.passedA[i]++;
                summary.decided += r.decided;
                summary.peitho += r.finalPlayer.peitho; summary.fame += r.finalPlayer.fame; summary.ethos += r.finalPlayer.ethos;
            }
            if (games > 0) { summary.decided /= games; summary.peitho /= games; summary.fame /= games; summary.ethos /= games; }
            return summary;
        }

        /// <summary>Μία παρτίδα: 10 πρυτανείες × 2 υποθέσεις, με τη σειρά της ροής του παιχνιδιού.</summary>
        public static Game Play(GameContent content, Strategy strategy, Random rng, SocialClass cls = SocialClass.Zeugites)
        {
            var cfg = content.config;
            var city = new CityState(cfg.cityStart);
            var pl = new PlayerState(cfg, cls, "Sim");
            var game = new Game();
            int prytanies = content.cases.Count / 2;
            for (int prytany = 1; prytany <= prytanies; prytany++)
            {
                AssemblyRules.StartPrytany(city, pl, cfg, prytany);
                for (int week = 0; week < 2; week++)
                {
                    var c = content.CaseAt(prytany, week);
                    var s = new AgoraCaseState { caseId = c.id };
                    var plan = Decide(strategy, c, pl, cfg, rng);

                    // Αγορά: κατηγορία → λογογράφος → αξιωματούχος· μετά η δίκη, ο λόγος και η ψήφος.
                    if (plan.accuse) WeekFlowRules.Accuse(s, c, pl);
                    if (plan.logographerForTrial && WeekFlowRules.CanOrderForTrial(s, c))
                        WeekFlowRules.BuyLogographer(s, c, pl, cfg, Side.None, forTrial: true);
                    else if (plan.logographer != Side.None)
                        WeekFlowRules.BuyLogographer(s, c, pl, cfg, plan.logographer, forTrial: false);
                    if (plan.persuade && !WeekFlowRules.Persuade(s, c, pl, cfg, plan.side) && plan.bribeIfNeeded)
                        WeekFlowRules.Bribe(s, c, pl, city, cfg, rng, plan.side);
                    else if (plan.bribe)
                        WeekFlowRules.Bribe(s, c, pl, city, cfg, rng, plan.side);
                    WeekFlowRules.RunTrial(s, c, pl, cfg, rng);
                    WeekFlowRules.Speak(s, plan.speak ? plan.side : Side.None);
                    var vote = WeekFlowRules.ResolveVote(s, c, city, pl, cfg, rng, plan.vote);

                    int index = (prytany - 1) * 2 + week;
                    game.passedA[index] = vote.passedA;
                    if (plan.side != Side.None && Decided(vote, cfg)) game.decided++;
                }
            }
            game.city = EndingRules.City(city, cfg);
            game.personal = EndingRules.Personal(pl, game.city, cfg);
            game.finalCity = city;
            game.finalPlayer = pl;
            return game;
        }

        /// <summary>Ο παίκτης «έκρινε» την ψηφοφορία αν χωρίς τον λόγο του και τον αξιωματούχο του θα είχε βγει άλλο αποτέλεσμα.</summary>
        static bool Decided(VoteBreakdown r, GameConfig cfg)
        {
            int player = 0;
            foreach (var line in r.lines)
                if (line.term == VoteTerm.Speech || line.term == VoteTerm.Official) player += line.value;
            return (r.total - player >= cfg.passThreshold) != r.passedA;
        }

        static Plan Decide(Strategy strategy, CaseData c, PlayerState pl, GameConfig cfg, Random rng)
        {
            switch (strategy)
            {
                case Strategy.Silent:
                    return new Plan { side = Side.None, vote = Side.A };
                case Strategy.DemocratSpeaker: return Activist(Side.A, false, c, pl, cfg, rng);
                case Strategy.OligarchSpeaker: return Activist(Side.B, false, c, pl, cfg, rng);
                case Strategy.DemocratBriber: return Activist(Side.A, true, c, pl, cfg, rng);
                case Strategy.OligarchBriber: return Activist(Side.B, true, c, pl, cfg, rng);
                case Strategy.HalfSpeakerA:
                    // Ρεαλιστικός μαθητής: μιλά στο μισό, πάντα για την Α· πείθει όταν φτάνει η Πειθώ.
                    return new Plan { side = Side.A, speak = rng.NextDouble() < 0.5, persuade = true, vote = Side.A };
                default:
                {
                    var side = rng.Next(2) == 0 ? Side.A : Side.B;
                    var plan = new Plan { side = side, speak = rng.NextDouble() < 0.6 };
                    if (rng.NextDouble() < 0.5) { if (rng.Next(2) == 0) plan.persuade = true; else plan.bribe = true; }
                    if (rng.NextDouble() < 0.3) plan.logographer = side;
                    if (WeekFlowRules.HasTrial(c) && rng.NextDouble() < 0.4) plan.accuse = true;
                    plan.vote = rng.NextDouble() < 0.85 ? side : side == Side.A ? Side.B : Side.A;
                    return plan;
                }
            }
        }

        /// <summary>Ο ενεργός πολίτης του final_sim.py: μιλά πάντα για την πλευρά του, πείθει όταν φτάνει η Πειθώ
        /// (αλλιώς, αν δωροδοκεί, δωροδοκεί), κατηγορεί όταν η καταδίκη ευνοεί την πλευρά του και τα στοιχεία είναι
        /// τουλάχιστον αμφίβολα, και αγοράζει λογογράφο όσο περισσεύουν χρήματα.</summary>
        static Plan Activist(Side side, bool bribe, CaseData c, PlayerState pl, GameConfig cfg, Random rng)
        {
            var plan = new Plan { side = side, speak = true, persuade = true, bribeIfNeeded = bribe, vote = side };
            if (WeekFlowRules.HasTrial(c) && c.trial.convictionFavors == side
                && AssemblyRules.Evidence(c.trial.evidence, cfg) != EvidenceStrength.Weak)
                plan.accuse = true;
            int reserve = bribe ? cfg.bribeCost : 0;
            if (pl.wallet >= cfg.logographerCost + reserve)
            {
                if (plan.accuse && c.trial.venue == TrialVenue.Heliaia && rng.NextDouble() < 0.3) plan.logographerForTrial = true;
                else plan.logographer = side;
            }
            return plan;
        }
    }
}
