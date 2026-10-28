using System;
using System.Linq;
using NUnit.Framework;

namespace Agora.Tests
{
    /// <summary>Agora_FINAL_DESIGN ενότητα 2 και 7.1: ο κανόνας της ψηφοφορίας και της δίκης.</summary>
    public sealed class VoteRulesTests
    {
        static int Term(VoteBreakdown r, VoteTerm t) => r.lines.Where(l => l.term == t).Sum(l => l.value);

        // ---------- Ψηφοφορία συνέλευσης ----------

        [Test] // Όλοι οι όροι μαζί, με τις τιμές του πίνακα της ενότητας 2.
        public void Vote_AllTermsOfSection2()
        {
            var v = new VoteInput
            {
                baseSupport = 52, pressureValue = 31, pressureFavors = Side.B,
                emergency = true, war = true, areopagus = true,
                trialFavors = Side.A, officialFavors = Side.A,
                speechFor = Side.A, peitho = 40, logographerFor = Side.A
            };
            var r = AssemblyRules.Vote(v, TestWorld.Cfg(), -3);
            CollectionAssert.AreEqual(
                new[] { VoteTerm.Base, VoteTerm.Pressure, VoteTerm.War, VoteTerm.Areopagus, VoteTerm.Trial, VoteTerm.Official, VoteTerm.Speech, VoteTerm.Mood },
                r.lines.Select(l => l.term).ToArray());
            CollectionAssert.AreEqual(new[] { 52, -5, -10, -5, 4, 4, 7, -3 }, r.lines.Select(l => l.value).ToArray());
            Assert.AreEqual(44, r.total);
            Assert.AreEqual(r.lines.Sum(l => l.value), r.total, "το άθροισμα της ανάλυσης = S");
            Assert.IsFalse(r.passedA);
        }

        [Test] // Το παράδειγμα της 7.1: 54 + 5 + 6 + 4 − 3 = 66, περνά η Α. Γραμμές με μηδέν δεν εμφανίζονται.
        public void Vote_ExampleOfSection7_1()
        {
            var v = new VoteInput { baseSupport = 54, pressureValue = 31, pressureFavors = Side.A, officialFavors = Side.A, speechFor = Side.A, peitho = 60 };
            var r = AssemblyRules.Vote(v, TestWorld.Cfg(), -3);
            CollectionAssert.AreEqual(new[] { 54, 5, 4, 6, -3 }, r.lines.Select(l => l.value).ToArray());
            Assert.AreEqual(66, r.total);
            Assert.IsTrue(r.passedA);
        }

        [TestCase(-1, 49, false)]
        [TestCase(0, 50, true)]
        public void Vote_PassesAtFifty(int mood, int expected, bool passedA)
        {
            var r = AssemblyRules.Vote(new VoteInput { baseSupport = 50, pressureValue = 50 }, TestWorld.Cfg(), mood);
            Assert.AreEqual(expected, r.total);
            Assert.AreEqual(passedA, r.passedA);
            Assert.AreEqual(mood == 0 ? 1 : 2, r.lines.Count, "η διάθεση 0 δεν εμφανίζεται, η Βάση πάντα");
        }

        [TestCase(34, -5)]
        [TestCase(35, 0)]
        public void Vote_PressureOnlyBelow35(int metric, int expected)
        {
            var r = AssemblyRules.Vote(new VoteInput { baseSupport = 50, pressureValue = metric, pressureFavors = Side.B }, TestWorld.Cfg(), 0);
            Assert.AreEqual(expected, Term(r, VoteTerm.Pressure));
        }

        [Test] // Υπόθεση χωρίς πίεση: ούτε δείκτης 0 δεν τη γέρνει.
        public void Vote_NoPressureSide_NoPressure()
        {
            var r = AssemblyRules.Vote(new VoteInput { baseSupport = 50, pressureValue = 0 }, TestWorld.Cfg(), 0);
            Assert.AreEqual(0, Term(r, VoteTerm.Pressure));
        }

        [TestCase(true, true, -10)]
        [TestCase(true, false, 0)]
        [TestCase(false, true, 0)]
        public void Vote_WarCountsOnlyForEmergencyCasesInWar(bool war, bool emergency, int expected)
        {
            var r = AssemblyRules.Vote(new VoteInput { baseSupport = 50, pressureValue = 50, war = war, emergency = emergency }, TestWorld.Cfg(), 0);
            Assert.AreEqual(expected, Term(r, VoteTerm.War));
        }

        [TestCase(Side.A, Side.None, 30, 3)]
        [TestCase(Side.A, Side.A, 30, 6)]
        [TestCase(Side.B, Side.B, 30, -6)]
        [TestCase(Side.A, Side.B, 30, 3)]      // λόγος για την άλλη πλευρά: ο λογογράφος χάνεται
        [TestCase(Side.None, Side.A, 30, 0)]   // σιωπή: ο λογογράφος χάνεται
        [TestCase(Side.A, Side.None, 39, 3)]   // ακέραια διαίρεση
        [TestCase(Side.A, Side.None, 70, 7)]
        [TestCase(Side.A, Side.A, 70, 10)]     // το ανώτατο: 3 έως 10
        [TestCase(Side.A, Side.A, 90, 10)]     // η Πειθώ δεν μετράει πάνω από 70
        public void Vote_Speech(Side speech, Side logographer, int peitho, int expected)
        {
            var v = new VoteInput { baseSupport = 50, pressureValue = 50, speechFor = speech, logographerFor = logographer, peitho = peitho };
            Assert.AreEqual(expected, Term(AssemblyRules.Vote(v, TestWorld.Cfg(), 0), VoteTerm.Speech));
        }

        [Test]
        public void Vote_TrialAndOfficialPushTowardsTheirSide()
        {
            var r = AssemblyRules.Vote(new VoteInput { baseSupport = 50, pressureValue = 50, trialFavors = Side.B, officialFavors = Side.B }, TestWorld.Cfg(), 0);
            Assert.AreEqual(-4, Term(r, VoteTerm.Trial));
            Assert.AreEqual(-4, Term(r, VoteTerm.Official));
            Assert.AreEqual(42, r.total);
        }

        // ---------- Διάθεση του πλήθους ----------

        [Test]
        public void Mood_SameSeedSameResult()
        {
            var v = new VoteInput { baseSupport = 50, pressureValue = 50 };
            var a = AssemblyRules.Vote(v, TestWorld.Cfg(), new Random(42));
            var b = AssemblyRules.Vote(v, TestWorld.Cfg(), new Random(42));
            Assert.AreEqual(a.mood, b.mood);
            Assert.AreEqual(a.total, b.total);
        }

        [Test]
        public void Mood_StaysWithinPlusMinusTen()
        {
            var rng = new Random(7);
            var cfg = TestWorld.Cfg();
            var seen = Enumerable.Range(0, 5000).Select(_ => AssemblyRules.Mood(rng, cfg)).ToList();
            Assert.AreEqual(-10, seen.Min());
            Assert.AreEqual(10, seen.Max());
        }

        // ---------- Δίκη ----------

        [TestCase(-9, 49, false)]
        [TestCase(-8, 50, true)]
        public void Trial_GuiltyAtFifty(int mood, int expected, bool guilty)
        {
            var r = AssemblyRules.Trial(new TrialInput { evidence = 58 }, TestWorld.Cfg(), mood);
            Assert.AreEqual(expected, r.total);
            Assert.AreEqual(guilty, r.guilty);
            Assert.IsFalse(r.sycophant, "ο παίκτης δεν κατηγόρησε");
        }

        [TestCase(-2, 19, true)]
        [TestCase(-1, 20, false)]
        public void Trial_AccuserBelowOneFifthIsSycophant(int mood, int expected, bool sycophant)
        {
            var t = new TrialInput { evidence = 18, venue = TrialVenue.Heliaia, playerAccuses = true, peitho = 30 };
            var r = AssemblyRules.Trial(t, TestWorld.Cfg(), mood);
            CollectionAssert.AreEqual(new[] { TrialTerm.Evidence, TrialTerm.Accusation, TrialTerm.Mood }, r.lines.Select(l => l.term).ToArray());
            Assert.AreEqual(expected, r.total);
            Assert.AreEqual(sycophant, r.sycophant);
            Assert.IsFalse(r.guilty);
        }

        [Test] // Ο κατήγορος με λογογράφο: Πειθώ ÷ 10 + 3.
        public void Trial_AccusationWithLogographer()
        {
            var t = new TrialInput { evidence = 52, venue = TrialVenue.Heliaia, playerAccuses = true, peitho = 44, logographer = true };
            var r = AssemblyRules.Trial(t, TestWorld.Cfg(), 0);
            Assert.AreEqual(7, r.lines.Single(l => l.term == TrialTerm.Accusation).value);
            Assert.AreEqual(59, r.total);
            Assert.IsTrue(r.guilty);
        }

        [Test] // Στον Άρειο Πάγο ο λόγος δεν μετράει, αλλά ο μηνυτής κρίνεται ως κατήγορος.
        public void Trial_AreopagusIgnoresSpeech()
        {
            var t = new TrialInput { evidence = 62, venue = TrialVenue.Areopagus, playerAccuses = true, peitho = 70, logographer = true };
            var r = AssemblyRules.Trial(t, TestWorld.Cfg(), 0);
            Assert.AreEqual(1, r.lines.Count);
            Assert.AreEqual(62, r.total);
        }

        [Test] // Ο παίκτης δικαστής: η δίκη δεν παίρνει όρο Κατηγορίας.
        public void Trial_NotAccuser_NoAccusationTerm()
        {
            var r = AssemblyRules.Trial(new TrialInput { evidence = 18, peitho = 70 }, TestWorld.Cfg(), -10);
            Assert.AreEqual(8, r.total);
            Assert.IsFalse(r.lines.Any(l => l.term == TrialTerm.Accusation));
            Assert.IsFalse(r.sycophant);
        }

        [TestCase(34, EvidenceStrength.Weak)]
        [TestCase(35, EvidenceStrength.Doubtful)]
        [TestCase(54, EvidenceStrength.Doubtful)]
        [TestCase(55, EvidenceStrength.Strong)]
        public void Evidence_Words(int evidence, EvidenceStrength expected)
            => Assert.AreEqual(expected, AssemblyRules.Evidence(evidence, TestWorld.Cfg()));
    }
}
