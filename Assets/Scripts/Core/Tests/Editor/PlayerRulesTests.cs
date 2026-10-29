using System;
using NUnit.Framework;
using UnityEngine;

namespace Agora.Tests
{
    /// <summary>Agora_FINAL_DESIGN ενότητα 2: οι τρεις κινήσεις του παίκτη και οι δείκτες του.</summary>
    public sealed class PlayerRulesTests
    {
        static PlayerState Player(int wallet = 150) { var p = TestWorld.Player(TestWorld.Cfg()); p.wallet = wallet; return p; }

        // ---------- Πειθώ ----------

        [TestCase(29, Difficulty.Easy, false)]
        [TestCase(30, Difficulty.Easy, true)]
        [TestCase(44, Difficulty.Medium, false)]
        [TestCase(45, Difficulty.Medium, true)]
        [TestCase(59, Difficulty.Hard, false)]
        [TestCase(60, Difficulty.Hard, true)]
        public void Persuade_NeedsPeithoAtLeastDifficulty(int peitho, Difficulty d, bool expected)
            => Assert.AreEqual(expected, AssemblyRules.CanPersuade(peitho, d, TestWorld.Cfg()));

        // ---------- Δωροδοκία ----------

        /// <summary>Γεννήτρια που δίνει πάντα την ίδια τιμή στο Next(n): 0 = αποκάλυψη, 1 = κανείς δεν το έμαθε.</summary>
        sealed class FixedRandom : System.Random
        {
            readonly int value;
            public FixedRandom(int value) { this.value = value; }
            public override int Next(int maxValue) => Math.Min(value, maxValue - 1);
        }

        [Test]
        public void Bribe_CostsMoneyAndEthos()
        {
            var p = Player(); var city = TestWorld.City(50, 50, 50, 50, 50);
            Assert.IsTrue(AssemblyRules.Bribe(p, city, TestWorld.Cfg(), new FixedRandom(1), out bool exposed));
            Assert.IsFalse(exposed);
            Assert.AreEqual(120, p.wallet);
            Assert.AreEqual(50, p.ethos);
            Assert.AreEqual(1, p.bribes);
            Assert.AreEqual(10, p.fame);
            Assert.AreEqual(50, city.trust);
        }

        [Test] // 1 στις 3: Φήμη −5, Εμπιστοσύνη −3.
        public void Bribe_ExposedCostsFameAndTrust()
        {
            var p = Player(); var city = TestWorld.City(50, 50, 50, 50, 50);
            Assert.IsTrue(AssemblyRules.Bribe(p, city, TestWorld.Cfg(), new FixedRandom(0), out bool exposed));
            Assert.IsTrue(exposed);
            Assert.AreEqual(5, p.fame);
            Assert.AreEqual(47, city.trust);
        }

        [Test]
        public void Bribe_ExposureIsOneInThree()
        {
            var rng = new System.Random(3); var cfg = TestWorld.Cfg(); int exposed = 0;
            for (int i = 0; i < 3000; i++)
            {
                var p = Player(1000);
                AssemblyRules.Bribe(p, TestWorld.City(50, 50, 50, 50, 50), cfg, rng, out bool e);
                if (e) exposed++;
            }
            Assert.That(exposed, Is.InRange(900, 1100));
        }

        [Test]
        public void Bribe_NotEnoughMoney()
        {
            var p = Player(29);
            Assert.IsFalse(AssemblyRules.Bribe(p, TestWorld.City(50, 50, 50, 50, 50), TestWorld.Cfg(), new FixedRandom(1), out _));
            Assert.AreEqual(29, p.wallet);
            Assert.AreEqual(60, p.ethos);
        }

        // ---------- Λογογράφος ----------

        [Test]
        public void Logographer_Costs40()
        {
            var p = Player(40);
            Assert.IsTrue(AssemblyRules.BuyLogographer(p, TestWorld.Cfg()));
            Assert.AreEqual(0, p.wallet);
            Assert.IsFalse(AssemblyRules.BuyLogographer(p, TestWorld.Cfg()));
        }

        // ---------- Δίκη ----------

        [TestCase(Side.A, true, Side.A)]
        [TestCase(Side.A, false, Side.B)]
        [TestCase(Side.B, true, Side.B)]
        [TestCase(Side.B, false, Side.A)]
        public void Trial_ConvictionFavorsOneSideAcquittalTheOther(Side convictionFavors, bool guilty, Side expected)
            => Assert.AreEqual(expected, AssemblyRules.TrialFavors(new TrialData { exists = true, convictionFavors = convictionFavors }, guilty));

        [Test]
        public void Trial_AccuserWinsConviction()
        {
            var p = Player();
            AssemblyRules.ApplyTrialToAccuser(p, TestWorld.Cfg(), new TrialBreakdown { guilty = true, total = 60 });
            Assert.AreEqual(15, p.fame);
            Assert.AreEqual(63, p.ethos);
            Assert.AreEqual(150, p.wallet);
        }

        [Test]
        public void Trial_AccuserLosesAcquittal()
        {
            var p = Player();
            AssemblyRules.ApplyTrialToAccuser(p, TestWorld.Cfg(), new TrialBreakdown { guilty = false, total = 30 });
            Assert.AreEqual(7, p.fame);
            Assert.AreEqual(60, p.ethos);
            Assert.IsFalse(p.sycophantFlag);
        }

        [Test] // Κάτω από το ένα πέμπτο: πρόστιμο 50 δρ. και συκοφάντης· το πορτοφόλι δεν γίνεται αρνητικό.
        public void Trial_SycophantPaysFine()
        {
            var p = Player(30);
            AssemblyRules.ApplyTrialToAccuser(p, TestWorld.Cfg(), new TrialBreakdown { guilty = false, sycophant = true, total = 19 });
            Assert.AreEqual(0, p.wallet);
            Assert.IsTrue(p.sycophantFlag);
        }

        [Test] // Ο καταδικασμένος ομιλητής δίνει τη θέση του στον αναπληρωτή.
        public void Trial_ConvictedSpeakerIsReplaced()
        {
            var c = TestWorld.Case(9);
            Assert.AreEqual(c.speechB.speaker, AssemblyRules.SpeakerOf(c, Side.B, false));
            Assert.AreEqual(c.trial.replacementSpeaker, AssemblyRules.SpeakerOf(c, Side.B, true));
            Assert.AreEqual(c.speechA.speaker, AssemblyRules.SpeakerOf(c, Side.A, true));
        }

        // ---------- Μετά την ψηφοφορία ----------

        [Test] // Μίλησε για την Α και πέρασε η Α: Πειθώ +2, Φήμη +2 +2.
        public void AfterVote_SpokeAndWon()
        {
            var p = Player();
            AssemblyRules.ApplyPlayerAfterVote(p, TestWorld.Cfg(), Side.A, Side.A, true);
            Assert.AreEqual(32, p.peitho);
            Assert.AreEqual(14, p.fame);
            Assert.AreEqual(60, p.ethos);
            Assert.AreEqual(1, p.spokeForA);
        }

        [Test] // Μίλησε για τη Β, πέρασε η Α, ψήφισε Α: Φήμη +2 μόνο, Ηθική −5.
        public void AfterVote_SpokeLostAndVotedAgainstSpeech()
        {
            var p = Player();
            AssemblyRules.ApplyPlayerAfterVote(p, TestWorld.Cfg(), Side.B, Side.A, true);
            Assert.AreEqual(12, p.fame);
            Assert.AreEqual(55, p.ethos);
            Assert.AreEqual(1, p.spokeForB);
        }

        [Test] // Πειθώ έως 70.
        public void AfterVote_PeithoCappedAt70()
        {
            var p = Player(); p.peitho = 69;
            AssemblyRules.ApplyPlayerAfterVote(p, TestWorld.Cfg(), Side.A, Side.A, true);
            Assert.AreEqual(70, p.peitho);
            AssemblyRules.ApplyPlayerAfterVote(p, TestWorld.Cfg(), Side.A, Side.A, true);
            Assert.AreEqual(70, p.peitho);
        }

        [Test] // Φήμη −2 σε κάθε σιωπή από τη δεύτερη συνεχόμενη και μετά· ο λόγος μηδενίζει τη σειρά.
        public void AfterVote_SilenceCostsFameFromTheSecond()
        {
            var p = Player(); var cfg = TestWorld.Cfg();
            AssemblyRules.ApplyPlayerAfterVote(p, cfg, Side.None, Side.A, true);
            Assert.AreEqual(10, p.fame, "πρώτη σιωπή");
            AssemblyRules.ApplyPlayerAfterVote(p, cfg, Side.None, Side.A, true);
            Assert.AreEqual(8, p.fame, "δεύτερη");
            AssemblyRules.ApplyPlayerAfterVote(p, cfg, Side.None, Side.B, false);
            Assert.AreEqual(6, p.fame, "τρίτη");
            AssemblyRules.ApplyPlayerAfterVote(p, cfg, Side.A, Side.A, false);
            Assert.AreEqual(0, p.silentStreak);
            AssemblyRules.ApplyPlayerAfterVote(p, cfg, Side.None, Side.A, true);
            Assert.AreEqual(8, p.fame, "νέα πρώτη σιωπή: χωρίς απώλεια");
        }

        [Test] // Η ψήφος του παίκτη δεν αλλάζει τίποτα όταν σωπαίνει.
        public void AfterVote_SilentVoteChangesNothingButStreak()
        {
            var a = Player(); var b = Player();
            AssemblyRules.ApplyPlayerAfterVote(a, TestWorld.Cfg(), Side.None, Side.A, true);
            AssemblyRules.ApplyPlayerAfterVote(b, TestWorld.Cfg(), Side.None, Side.B, true);
            Assert.AreEqual(JsonUtility.ToJson(a), JsonUtility.ToJson(b));
        }

        [Test] // Η είσοδος της ψηφοφορίας από την υπόθεση και την πόλη: πίεση, πόλεμος, Άρειος Πάγος.
        public void BuildVoteInput_FromCaseAndCity()
        {
            var c = TestWorld.Case(19);
            var city = TestWorld.City(50, 50, 30, 50, 50, war: true);
            city.areopagusRestored = true;
            var v = AssemblyRules.BuildVoteInput(c, city, Player(), Side.None, Side.A, Side.B, Side.B);
            Assert.AreEqual(49, v.baseSupport);
            Assert.AreEqual(30, v.pressureValue);
            Assert.AreEqual(Side.B, v.pressureFavors);
            Assert.IsTrue(v.war && v.emergency && v.areopagus);
            var r = AssemblyRules.Vote(v, TestWorld.Cfg(), 0);
            Assert.AreEqual(49 - 5 - 10 - 5 + 4 - 6, r.total);
        }
    }
}
