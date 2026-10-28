using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Agora
{
    /// <summary>Αποτέλεσμα ψηφοφορίας μιας υπόθεσης.</summary>
    public struct VoteResult
    {
        public float s0;            // πριν τον παίκτη
        public float s;             // τελικό
        public bool passedA;
        public bool playerFlipped;  // θα είχε βγει αλλιώς χωρίς τον παίκτη
        public bool contested;      // |s0−passThreshold| ≤ contestedBand
    }

    public struct ComplaintResult
    {
        public bool filed, convicted, sycophant;
        public int fine;
    }

    [Serializable]
    public struct VoteLine { public VoteTerm term; public int value; }

    [Serializable]
    public struct TrialLine { public TrialTerm term; public int value; }

    /// <summary>Ό,τι χρειάζεται η ψηφοφορία της συνέλευσης. Η ψήφος του παίκτη δεν είναι είσοδος.</summary>
    [Serializable]
    public sealed class VoteInput
    {
        public int baseSupport;
        public int pressureValue;           // η τιμή του δείκτη πίεσης της υπόθεσης
        public Side pressureFavors;         // None = η υπόθεση δεν έχει πίεση
        public bool emergency, war;
        public bool areopagus;              // υπ. 19–20 και στην 18 πέρασε η Β
        public Side trialFavors, officialFavors;
        public Side speechFor;              // None = σιωπή
        public int peitho;
        public Side logographerFor;
    }

    /// <summary>Η ανάλυση που βλέπει ο παίκτης: μόνο μη μηδενικοί όροι, η Βάση πάντα.</summary>
    [Serializable]
    public sealed class VoteBreakdown
    {
        public List<VoteLine> lines = new List<VoteLine>();
        public int total, mood;
        public bool passedA;
    }

    [Serializable]
    public sealed class TrialInput
    {
        public int evidence;
        public TrialVenue venue;
        public bool playerAccuses;
        public int peitho;
        public bool logographer;            // λόγος λογογράφου για την κατηγορία
    }

    [Serializable]
    public sealed class TrialBreakdown
    {
        public List<TrialLine> lines = new List<TrialLine>();
        public int total, mood;
        public bool guilty;
        public bool sycophant;              // ο παίκτης κατήγορος και D κάτω από το ένα πέμπτο
    }

    /// <summary>
    /// Όλοι οι κανόνες του game loop σε καθαρές (stateless) συναρτήσεις.
    /// Αντιστοιχούν 1:1 στον προσομοιωτή loop.py. Κανένα MonoBehaviour εδώ.
    /// </summary>
    public static class AssemblyRules
    {
        // ---------- Τελικός κανόνας: ψηφοφορία συνέλευσης και δίκης ----------

        /// <summary>Η διάθεση του πλήθους: ο μόνος τυχαίος όρος. Η γεννήτρια περνιέται απ' έξω (seed για tests).</summary>
        public static int Mood(System.Random rng, GameConfig cfg) => rng.Next(-cfg.moodRange, cfg.moodRange + 1);

        /// <summary>Ο λόγος: Πειθώ ÷ 10 (ακέραιο), συν τον λόγο του λογογράφου αν παραγγέλθηκε για την ίδια πλευρά.</summary>
        public static int SpeechPoints(int peitho, bool logographer, GameConfig cfg)
            => Mathf.Clamp(peitho, 0, cfg.speechPeithoMax) / cfg.speechPeithoDivisor + (logographer ? cfg.logographerPoints : 0);

        public static VoteBreakdown Vote(VoteInput v, GameConfig cfg, System.Random rng) => Vote(v, cfg, Mood(rng, cfg));

        public static VoteBreakdown Vote(VoteInput v, GameConfig cfg, int mood)
        {
            var r = new VoteBreakdown { mood = mood, total = v.baseSupport };
            r.lines.Add(new VoteLine { term = VoteTerm.Base, value = v.baseSupport });
            AddLine(r, VoteTerm.Pressure, v.pressureValue < cfg.pressureBelow ? Toward(v.pressureFavors, cfg.pressurePoints) : 0);
            AddLine(r, VoteTerm.War, v.war && v.emergency ? -cfg.warEmergencyPoints : 0);
            AddLine(r, VoteTerm.Areopagus, v.areopagus ? -cfg.areopagusPoints : 0);
            AddLine(r, VoteTerm.Trial, Toward(v.trialFavors, cfg.trialPoints));
            AddLine(r, VoteTerm.Official, Toward(v.officialFavors, cfg.officialPoints));
            AddLine(r, VoteTerm.Speech, Toward(v.speechFor, SpeechPoints(v.peitho, v.logographerFor != Side.None && v.logographerFor == v.speechFor, cfg)));
            AddLine(r, VoteTerm.Mood, mood);
            r.passedA = r.total >= cfg.passThreshold;
            return r;
        }

        public static TrialBreakdown Trial(TrialInput t, GameConfig cfg, System.Random rng) => Trial(t, cfg, Mood(rng, cfg));

        /// <summary>Στον Άρειο Πάγο ο λόγος δεν μετράει: ο παίκτης δίνει μόνο πληροφορίες (μήνυσις).</summary>
        public static TrialBreakdown Trial(TrialInput t, GameConfig cfg, int mood)
        {
            var r = new TrialBreakdown { mood = mood };
            r.lines.Add(new TrialLine { term = TrialTerm.Evidence, value = t.evidence });
            r.total = t.evidence;
            int accusation = t.playerAccuses && t.venue == TrialVenue.Heliaia ? SpeechPoints(t.peitho, t.logographer, cfg) : 0;
            foreach (var (term, value) in new[] { (TrialTerm.Accusation, accusation), (TrialTerm.Mood, mood) })
            {
                if (value == 0) continue;
                r.lines.Add(new TrialLine { term = term, value = value });
                r.total += value;
            }
            r.guilty = r.total >= cfg.guiltyThreshold;
            r.sycophant = t.playerAccuses && r.total < cfg.sycophantBelow;
            return r;
        }

        public static EvidenceStrength Evidence(int evidence, GameConfig cfg)
            => evidence >= cfg.evidenceStrongFrom ? EvidenceStrength.Strong
             : evidence >= cfg.evidenceDoubtfulFrom ? EvidenceStrength.Doubtful
             : EvidenceStrength.Weak;

        static int Toward(Side side, int amount) => side == Side.A ? amount : side == Side.B ? -amount : 0;

        static void AddLine(VoteBreakdown r, VoteTerm term, int value)
        {
            if (value == 0) return;
            r.lines.Add(new VoteLine { term = term, value = value });
            r.total += value;
        }

        // ---------- Πρυτανεία ----------

        public static void StartPrytany(CityState city, PlayerState pl, GameConfig cfg, int prytany)
        {
            if (prytany == cfg.warStartsAtPrytany) city.war = true;

            city.Add(Metric.Treasury, city.war ? cfg.warTreasury : cfg.peaceTreasury);
            city.Add(Metric.Food, city.war ? cfg.warFood : cfg.peaceFood);
            city.Add(Metric.Readiness, city.war ? cfg.warReadiness : cfg.peaceReadiness);

            if (city.auditFinePending) { city.Add(Metric.Treasury, cfg.auditFineTreasury); city.auditFinePending = false; }

            pl.wallet += pl.income + (city.payAbolished ? 0 : cfg.assemblyPayPerPrytany);
        }

        // ---------- S0: η στήριξη πριν τον παίκτη ----------

        public static float ComputeS0(CaseData c, CityState city, GameConfig cfg, IList<OfficialStance> officials)
        {
            float s0 = c.baseSupport;

            if (c.lowTrustBaseOverride >= 0 && city.trust < cfg.lowTrustThreshold) s0 = c.lowTrustBaseOverride;
            if (c.lowAnyMetricBaseOverride >= 0 && city.MinAll() < cfg.lowAnyMetricThreshold) s0 = c.lowAnyMetricBaseOverride;

            foreach (var sens in c.sensitivities)
            {
                int s = sens.perTen;
                if (sens.metric == Metric.Trust) s = Mathf.Min(s, cfg.trustSensitivityCap);
                s0 += s * (city.Get(sens.metric) - 50) / 10f;
            }

            s0 -= cfg.reaction * (city.demosPower - 50) / 10f;              // αντίδραση
            if (city.war && c.emergencyB) s0 -= cfg.emergencyBonus;         // «έκτακτη ανάγκη»
            if (c.affectedByAreopagus && city.areopagusRestored) s0 -= cfg.areopagusBonusB;

            foreach (var o in officials)
            {
                if (o.side == Side.A) s0 += o.weight;
                else if (o.side == Side.B) s0 -= o.weight;
            }
            return Mathf.Clamp(s0, cfg.s0Min, cfg.s0Max);
        }

        // ---------- Ενέργεια προπαρασκευής ----------

        /// <summary>Πείθω τον ουδέτερο: Πειθώ + Συνέπεια/2 ≥ T.</summary>
        public static bool Persuade(OfficialStance neutral, PlayerState pl, Side towards)
        {
            if (neutral.side != Side.None) return false;
            if (pl.peitho + pl.synepeia / 2f < neutral.threshold) return false;
            neutral.side = towards;
            return true;
        }

        /// <summary>Δωροδοκώ: κόστος 6·w, Ηθική −10, πιθανότητα αποκάλυψης (100−Ηθική)/200.</summary>
        public static bool Bribe(OfficialStance target, PlayerState pl, CityState city, GameConfig cfg, Side towards, System.Random rng, out bool exposed)
        {
            exposed = false;
            int cost = cfg.bribeCostPerWeight * target.weight;
            if (pl.wallet < cost || !target.official.bribable) return false;

            pl.wallet -= cost;
            int ethosBefore = pl.ethos;                      // η αποκάλυψη κρίνεται με την Ηθική ΠΡΙΝ την απώλεια (spec Δ3.7)
            pl.ethos = PlayerState.Clamp(pl.ethos - cfg.bribeEthosLoss);
            pl.bribes++;
            target.side = towards;

            if (rng.NextDouble() < (100 - ethosBefore) / 200.0)
            {
                exposed = true;
                pl.exposures++;
                pl.anagnorisimotita = PlayerState.Clamp(pl.anagnorisimotita + cfg.exposureAnagnGain);
                city.Add(Metric.Trust, -cfg.exposureTrustLoss);
            }
            return true;
        }

        /// <summary>Καταγγέλλω: έκβαση προκαθορισμένη από το asset της υπόθεσης.</summary>
        public static ComplaintResult Complain(CaseData c, IList<OfficialStance> officials, PlayerState pl, CityState city, GameConfig cfg)
        {
            var r = new ComplaintResult();
            var opt = c.complaint;
            if (!opt.available || pl.sycophantFlag) return r;

            r.filed = true;
            pl.complaintsFiled++;
            pl.parrhesia = PlayerState.Clamp(pl.parrhesia + cfg.complaintParrhesiaAlways);

            var stance = officials.FirstOrDefault(o => o.official == opt.target);

            if (opt.guilty)
            {
                r.convicted = true;
                pl.complaintsWon++;
                if (stance != null) stance.weight = 0;           // βγαίνει από την υπόθεση
                pl.anagnorisimotita = PlayerState.Clamp(pl.anagnorisimotita + cfg.complaintWinAnagn);
                pl.parrhesia = PlayerState.Clamp(pl.parrhesia + cfg.complaintWinParrhesia);
                pl.ethos = PlayerState.Clamp(pl.ethos + cfg.complaintWinEthos);
                city.Add(Metric.Trust, cfg.complaintWinTrust);
                city.Add(Metric.Treasury, cfg.complaintWinTreasury);
            }
            else
            {
                if (stance != null) stance.weight = Mathf.Min(cfg.maxOfficialWeight, stance.weight + cfg.complaintAcquittedWeightBonus);
                pl.anagnorisimotita = PlayerState.Clamp(pl.anagnorisimotita + cfg.complaintLoseAnagn);
                if (opt.BelowOneFifth)
                {
                    r.sycophant = true;
                    r.fine = Mathf.Min(cfg.sycophantFine, pl.wallet);
                    pl.wallet -= r.fine;
                    pl.sycophantFlag = true;
                }
            }
            return r;
        }

        /// <summary>Ο ομιλητής ενός λόγου, λαμβάνοντας υπόψη καταδίκη από καταγγελία.</summary>
        public static OfficialData SpeakerOf(Speech sp, CaseData c, bool convicted)
            => convicted && c.complaint.target == sp.speaker && sp.fallbackSpeaker != null ? sp.fallbackSpeaker : sp.speaker;

        // ---------- Λόγος και ψήφος ----------

        /// <summary>P = (Πειθώ/20 + L/2)·(0.5 + Αναγν/100), cap 12.</summary>
        public static float SpeechPower(PlayerState pl, LogographerData logo, GameConfig cfg)
        {
            int L = logo != null ? logo.quality : 0;
            return Mathf.Min(cfg.speechCap, (pl.peitho / 20f + L / 2f) * (0.5f + pl.anagnorisimotita / 100f));
        }

        public static VoteResult Resolve(float s0, Side playerSide, float playerContribution, System.Random rng, GameConfig cfg)
        {
            float signed = playerSide == Side.A ? playerContribution : playerSide == Side.B ? -playerContribution : 0f;
            float noise = (float)(rng.NextDouble() * 2 * cfg.noise - cfg.noise);
            float s = s0 + signed + noise;
            float counter = s - signed;
            return new VoteResult
            {
                s0 = s0, s = s,
                passedA = s >= cfg.passThreshold,
                playerFlipped = (counter >= cfg.passThreshold) != (s >= cfg.passThreshold),
                contested = Mathf.Abs(s0 - cfg.passThreshold) <= cfg.contestedBand
            };
        }

        // ---------- Συνέπειες ----------

        public static void ApplyOutcome(CaseData c, bool passedA, CityState city, GameConfig cfg)
        {
            var effects = passedA ? c.effectsA : c.effectsB;
            foreach (var e in effects)
            {
                float f = 1f;
                if (e.metric == Metric.DemosPower) f = passedA ? cfg.scaleDemosPowerA : cfg.scaleDemosPowerB;
                if (e.metric == Metric.Trust) f = passedA ? cfg.scaleTrustA : cfg.scaleTrustB;
                if (e.metric == Metric.DemosPower && e.amount < 0 && city.garrison) f *= cfg.garrisonDemosPowerLossMultiplier;
                city.Add(e.metric, Mathf.RoundToInt(e.amount * f));
            }

            if (c.abolishesPayIfB && !passedA) city.payAbolished = true;
            if (c.decidesWarEnd) { city.war = passedA; city.garrison = !passedA; }
            if (c.auditFineIfA && passedA) city.auditFinePending = true;
            if (c.areopagusIfB && !passedA) city.areopagusRestored = true;
        }

        /// <summary>Ενημέρωση παίκτη μετά την υπόθεση. Η ψήφος ΔΕΝ αλλάζει το αποτέλεσμα — μόνο Συνέπεια.</summary>
        public static void ApplyPlayerAfterCase(PlayerState pl, GameConfig cfg, bool spoke, Side spokeFor, Side vote, VoteResult v)
        {
            if (spoke)
            {
                pl.speeches++;
                pl.silentStreak = 0;
                pl.anagnorisimotita = PlayerState.Clamp(pl.anagnorisimotita + cfg.anagnPerSpeech);
                pl.peitho = PlayerState.Clamp(pl.peitho + cfg.peithoPerSpeech, cfg.peithoCap);
                if (spokeFor == Side.A) pl.spokeForA++; else pl.spokeForB++;

                bool vindicated = (spokeFor == Side.A) == v.passedA;
                if (vindicated) { pl.speechesVindicated++; pl.anagnorisimotita = PlayerState.Clamp(pl.anagnorisimotita + cfg.anagnPerWin); }
                if ((spokeFor == Side.A) != (v.s0 >= cfg.passThreshold)) pl.parrhesia = PlayerState.Clamp(pl.parrhesia + cfg.parrhesiaAgainstMajority);

                pl.synepeia = PlayerState.Clamp(pl.synepeia + (vote == spokeFor ? cfg.synepeiaVoteMatch : cfg.synepeiaVoteMismatch));
            }
            else
            {
                pl.silentStreak++;
                if (pl.silentStreak >= cfg.silencesBeforeLoss) pl.anagnorisimotita = PlayerState.Clamp(pl.anagnorisimotita - cfg.anagnLossAfterTwoSilences);
            }

            if (vote != Side.None && pl.lastVote == vote) pl.synepeia = PlayerState.Clamp(pl.synepeia + cfg.synepeiaSameDirection);
            pl.lastVote = vote;
        }

        /// <summary>Αντίγραφα των θέσεων της υπόθεσης, για να μην αλλάζει το asset κατά το παιχνίδι.</summary>
        public static List<OfficialStance> CloneStances(CaseData c)
            => c.officials.Select(o => new OfficialStance { official = o.official, side = o.side, weight = o.weight, threshold = o.threshold }).ToList();
    }
}
