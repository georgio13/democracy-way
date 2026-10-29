using Agora;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Η λογιστική της εβδομάδας πάνω στο AgoraCaseState: ποιες κινήσεις έγιναν και με ποια σειρά.
    /// Τρεις ανεξάρτητες κινήσεις (αξιωματούχος, λογογράφος, κατηγορία)· οι αριθμοί και οι εκβάσεις
    /// έρχονται αποκλειστικά από το Agora.AssemblyRules.
    /// </summary>
    public static class WeekFlowRules
    {
        public static bool HasTrial(CaseData c) => c.trial != null && c.trial.exists;

        /// <summary>Η δίκη προηγείται της συνέλευσης: όσο δεν έχει γίνει, ανάβει το δικαστήριο.</summary>
        public static bool TrialPending(AgoraCaseState s, CaseData c) => HasTrial(c) && !s.trialDone;

        // ---------- Αξιωματούχος: μία κίνηση, μόνο στον αναποφάσιστο ----------

        public static bool CanMoveOfficial(AgoraCaseState s) => !s.resolved && !s.officialMoveDone;

        public static bool Persuade(AgoraCaseState s, CaseData c, PlayerState pl, GameConfig cfg, Side towards)
        {
            if (!CanMoveOfficial(s) || towards == Side.None || c.undecided == null || !AssemblyRules.CanPersuade(pl.peitho, c.difficulty, cfg)) return false;
            s.officialMoveDone = true;
            s.officialFavors = (int)towards;
            return true;
        }

        public static bool Bribe(AgoraCaseState s, CaseData c, PlayerState pl, CityState city, GameConfig cfg, System.Random rng, Side towards)
        {
            if (!CanMoveOfficial(s) || towards == Side.None || c.undecided == null || !c.undecided.bribable) return false;
            if (!AssemblyRules.Bribe(pl, city, cfg, rng, out bool exposed)) return false;
            s.officialMoveDone = true;
            s.officialFavors = (int)towards;
            s.officialBribed = true;
            s.bribeExposed = exposed;
            return true;
        }

        // ---------- Λογογράφος: ένας την εβδομάδα, για Α, Β ή την κατηγορία ----------

        /// <summary>Λόγος για την κατηγορία μόνο στην Ηλιαία, μόνο για τον κατήγορο, πριν τη δίκη.</summary>
        public static bool CanOrderForTrial(AgoraCaseState s, CaseData c) =>
            HasTrial(c) && c.trial.venue == TrialVenue.Heliaia && s.accuser && !s.trialDone;

        public static bool CanBuyLogographer(AgoraCaseState s, PlayerState pl, GameConfig cfg) =>
            !s.resolved && !s.spoke && !s.logographerBought && pl.wallet >= cfg.logographerCost;

        public static bool BuyLogographer(AgoraCaseState s, CaseData c, PlayerState pl, GameConfig cfg, Side side, bool forTrial)
        {
            if (!CanBuyLogographer(s, pl, cfg)) return false;
            if (forTrial ? !CanOrderForTrial(s, c) : side == Side.None) return false;
            if (!AssemblyRules.BuyLogographer(pl, cfg)) return false;
            s.logographerBought = true;
            s.logographerForTrial = forTrial;
            s.logographerFor = forTrial ? (int)Side.None : (int)side;
            return true;
        }

        // ---------- Κατηγορία: ανεξάρτητη από τις άλλες κινήσεις ----------

        public static bool CanAccuse(AgoraCaseState s, CaseData c, PlayerState pl) =>
            HasTrial(c) && !s.trialDone && !s.accuser && !pl.sycophantFlag;

        public static bool Accuse(AgoraCaseState s, CaseData c, PlayerState pl)
        {
            if (!CanAccuse(s, c, pl)) return false;
            s.accuser = true;
            return true;
        }

        /// <summary>Ο παίκτης που δεν κατηγορεί ψηφίζει ως δικαστής — μόνο στην Ηλιαία (ο Άρειος Πάγος είναι πρώην άρχοντες).
        /// Η ψήφος καταγράφεται για την έρευνα· δεν είναι όρος του κανόνα της δίκης.</summary>
        public static bool CanJudge(AgoraCaseState s, CaseData c) =>
            HasTrial(c) && c.trial.venue == TrialVenue.Heliaia && !s.trialDone && !s.accuser;

        public static bool JudgeVote(AgoraCaseState s, CaseData c, bool guilty)
        {
            if (!CanJudge(s, c)) return false;
            s.judgeVote = guilty ? 1 : 2;
            return true;
        }

        /// <summary>Η είσοδος της ψηφοφορίας από ό,τι είναι ήδη γνωστό: η δίκη αν έγινε, ο αξιωματούχος που κέρδισε ο
        /// παίκτης, και ο λόγος του λογογράφου αν τον παρήγγειλε (θεωρείται ότι θα μιλήσει γι' αυτή την πλευρά).</summary>
        public static VoteInput KnownVoteInput(AgoraCaseState s, CaseData c, CityState city, PlayerState pl)
        {
            var logographer = LogographerSide(s);
            return AssemblyRules.BuildVoteInput(c, city, pl, TrialFavors(s, c), (Side)s.officialFavors, logographer, logographer);
        }

        /// <summary>Η δίκη γίνεται πάντα, είτε κατηγορεί ο παίκτης είτε άλλος πολίτης.</summary>
        public static TrialBreakdown RunTrial(AgoraCaseState s, CaseData c, PlayerState pl, GameConfig cfg, System.Random rng)
        {
            if (!TrialPending(s, c)) return null;
            var input = new TrialInput
            {
                evidence = c.trial.evidence, venue = c.trial.venue, playerAccuses = s.accuser,
                peitho = pl.peitho, logographer = s.logographerBought && s.logographerForTrial
            };
            var r = AssemblyRules.Trial(input, cfg, rng);
            if (s.accuser) AssemblyRules.ApplyTrialToAccuser(pl, cfg, r);
            s.trialDone = true;
            s.trialGuilty = r.guilty;
            s.trialResult = r;
            return r;
        }

        public static Side TrialFavors(AgoraCaseState s, CaseData c) =>
            HasTrial(c) && s.trialDone ? AssemblyRules.TrialFavors(c.trial, s.trialGuilty) : Side.None;

        public static bool Convicted(AgoraCaseState s, CaseData c) => HasTrial(c) && s.trialDone && s.trialGuilty;

        // ---------- Συνέλευση ----------

        public static bool Speak(AgoraCaseState s, Side side)
        {
            if (s.spoke || s.resolved) return false;
            if (side != Side.None) { s.spoke = true; s.spokeFor = (int)side; }
            return true;
        }

        /// <summary>Η λογογραφία μετρά μόνο αν ο παίκτης μίλησε για την πλευρά που παρήγγειλε.</summary>
        public static Side LogographerSide(AgoraCaseState s) =>
            s.logographerBought && !s.logographerForTrial ? (Side)s.logographerFor : Side.None;

        static Metric LowestVisible(CityState city)
        {
            var lowest = Metric.Treasury;
            foreach (var m in new[] { Metric.Food, Metric.Readiness, Metric.Trust })
                if (city.Get(m) < city.Get(lowest)) lowest = m;
            return lowest;
        }

        /// <summary>Η ψηφοφορία: η ανάλυση αποθηκεύεται τη στιγμή της ψήφου. Η ψήφος του παίκτη δεν είναι όρος.</summary>
        public static VoteBreakdown ResolveVote(AgoraCaseState s, CaseData c, CityState city, PlayerState pl, GameConfig cfg, System.Random rng, Side vote)
        {
            if (s.resolved || TrialPending(s, c)) return null;
            var spokeFor = s.spoke ? (Side)s.spokeFor : Side.None;
            s.pressureMetric = (int)(c.pressureAnyMetric ? LowestVisible(city) : c.pressureMetric);
            var input = AssemblyRules.BuildVoteInput(c, city, pl, TrialFavors(s, c), (Side)s.officialFavors, spokeFor, LogographerSide(s));
            var r = AssemblyRules.Vote(input, cfg, rng);
            AssemblyRules.ApplyOutcome(c, r.passedA, city, cfg);
            AssemblyRules.ApplyPlayerAfterVote(pl, cfg, spokeFor, vote, r.passedA);
            s.resolved = true;
            s.passedA = r.passedA;
            s.vote = (int)vote;
            s.voteResult = r;
            return r;
        }
    }
}
