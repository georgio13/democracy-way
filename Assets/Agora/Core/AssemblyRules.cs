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
        public bool contested;      // |s0−50| ≤ 12
    }

    public struct ComplaintResult
    {
        public bool filed, convicted, sycophant;
        public int fine;
    }

    /// <summary>
    /// Όλοι οι κανόνες του game loop σε καθαρές (stateless) συναρτήσεις.
    /// Αντιστοιχούν 1:1 στον προσομοιωτή loop.py. Κανένα MonoBehaviour εδώ.
    /// </summary>
    public static class AssemblyRules
    {
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

            if (c.lowTrustBaseOverride >= 0 && city.trust < 40) s0 = c.lowTrustBaseOverride;
            if (c.lowAnyMetricBaseOverride >= 0 && city.MinAll() < 30) s0 = c.lowAnyMetricBaseOverride;

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
            return Mathf.Clamp(s0, 5, 95);
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
            pl.ethos = PlayerState.Clamp(pl.ethos - cfg.bribeEthosLoss);
            pl.bribes++;
            target.side = towards;

            if (rng.NextDouble() < (100 - pl.ethos) / 200.0)
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
                if (stance != null) stance.weight = Mathf.Min(15, stance.weight + cfg.complaintAcquittedWeightBonus);
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
                contested = Mathf.Abs(s0 - 50) <= 12
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
                if ((spokeFor == Side.A) != (v.s0 >= 50)) pl.parrhesia = PlayerState.Clamp(pl.parrhesia + cfg.parrhesiaAgainstMajority);

                pl.synepeia = PlayerState.Clamp(pl.synepeia + (vote == spokeFor ? cfg.synepeiaVoteMatch : cfg.synepeiaVoteMismatch));
            }
            else
            {
                pl.silentStreak++;
                if (pl.silentStreak >= 2) pl.anagnorisimotita = PlayerState.Clamp(pl.anagnorisimotita - cfg.anagnLossAfterTwoSilences);
            }

            if (vote != Side.None && pl.lastVote == vote) pl.synepeia = PlayerState.Clamp(pl.synepeia + cfg.synepeiaSameDirection);
            pl.lastVote = vote;
        }

        /// <summary>Αντίγραφα των θέσεων της υπόθεσης, για να μην αλλάζει το asset κατά το παιχνίδι.</summary>
        public static List<OfficialStance> CloneStances(CaseData c)
            => c.officials.Select(o => new OfficialStance { official = o.official, side = o.side, weight = o.weight, threshold = o.threshold }).ToList();
    }
}
