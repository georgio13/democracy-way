using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Agora
{
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
    /// Οι κανόνες του παιχνιδιού σε καθαρές (stateless) συναρτήσεις, κατά το Agora_FINAL_DESIGN.
    /// Κανένα MonoBehaviour εδώ.
    /// </summary>
    public static class AssemblyRules
    {
        // ---------- Ψηφοφορία συνέλευσης και δίκης ----------

        /// <summary>Η διάθεση του πλήθους: ο μόνος τυχαίος όρος. Η γεννήτρια περνιέται απ' έξω (seed για tests).</summary>
        public static int Mood(System.Random rng, GameConfig cfg) => rng.Next(-cfg.moodRange, cfg.moodRange + 1);

        /// <summary>Ο λόγος: Πειθώ ÷ 10 (ακέραιο), συν τον λόγο του λογογράφου αν παραγγέλθηκε για την ίδια πλευρά.</summary>
        public static int SpeechPoints(int peitho, bool logographer, GameConfig cfg)
            => Mathf.Clamp(peitho, 0, cfg.speechPeithoMax) / cfg.speechPeithoDivisor + (logographer ? cfg.logographerPoints : 0);

        /// <summary>Η τιμή που κρίνει την Πίεση της υπόθεσης· στην 20 ο χαμηλότερος από τους 4 ορατούς δείκτες.</summary>
        public static int PressureValue(CaseData c, CityState city)
            => c.pressureAnyMetric ? city.MinVisible() : city.Get(c.pressureMetric);

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

        // ---------- Συνέπειες της απόφασης ----------

        /// <summary>Επιπτώσεις Α ή Β και οι 4 ειδικοί κανόνες (12Β, 15, 17Α, 18Β).</summary>
        public static void ApplyOutcome(CaseData c, bool passedA, CityState city, GameConfig cfg)
        {
            foreach (var e in passedA ? c.effectsA : c.effectsB)
            {
                float f = e.metric == Metric.DemosPower && e.amount < 0 && city.garrison ? cfg.garrisonDemosPowerLossMultiplier : 1f;
                city.Add(e.metric, Mathf.RoundToInt(e.amount * f));
            }

            if (c.abolishesPayIfB && !passedA) city.payAbolished = true;
            if (c.decidesWarEnd) { city.war = passedA; city.garrison = !passedA; }
            if (c.auditFineIfA && passedA) city.auditFinePending = true;
            if (c.areopagusIfB && !passedA) city.areopagusRestored = true;
        }

        /// <summary>Αντίγραφα των θέσεων της υπόθεσης, για να μην αλλάζει το asset κατά το παιχνίδι.</summary>
        public static List<OfficialStance> CloneStances(CaseData c)
            => c.officials.Select(o => new OfficialStance { official = o.official, side = o.side }).ToList();
    }
}
