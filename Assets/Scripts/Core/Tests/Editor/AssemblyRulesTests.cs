using System;
using System.Linq;
using NUnit.Framework;

namespace Agora.Tests
{
    /// <summary>VERIFICATION.md ενότητα Δ (Δ1–Δ7): test vectors των καθαρών συναρτήσεων.</summary>
    public sealed class AssemblyRulesTests
    {
        const float Tolerance = 0.01f;

        // ---------- Δ1 ComputeS0 ----------
        // πόλη: T, Ep, Et, Em, ID, war — αναμενόμενο S0 από το spec
        static readonly object[][] D1 =
        {
            new object[] { 1, 1, new[] { 50, 50, 50, 50, 50 }, false, false, 70f },
            new object[] { 2, 1, new[] { 50, 50, 50, 50, 70 }, false, false, 62f },
            new object[] { 3, 1, new[] { 50, 50, 50, 80, 50 }, false, false, 76f },
            new object[] { 4, 12, new[] { 45, 50, 50, 50, 50 }, true, false, 50f },
            new object[] { 5, 12, new[] { 45, 50, 50, 50, 50 }, false, false, 60f },
            new object[] { 6, 18, new[] { 50, 50, 50, 35, 40 }, true, false, 43f },
            new object[] { 7, 18, new[] { 50, 50, 50, 45, 50 }, true, false, 55f },
            new object[] { 8, 20, new[] { 25, 50, 50, 50, 50 }, false, false, 40f },
            new object[] { 9, 20, new[] { 50, 50, 50, 50, 50 }, true, true, 33f },
            new object[] { 10, 19, new[] { 50, 50, 30, 50, 50 }, true, false, 53f },
            new object[] { 12, 7, new[] { 50, 50, 50, 50, 50 }, false, false, 48f },
            new object[] { 13, 3, new[] { 50, 50, 10, 50, 50 }, false, false, 29f },
            new object[] { 14, 5, new[] { 50, 50, 50, 50, 0 }, false, false, 88f },
        };

        [TestCaseSource(nameof(D1))]
        public void D1_ComputeS0(object[] row)
        {
            int caseId = (int)row[1];
            var m = (int[])row[2];
            var city = TestWorld.City(m[0], m[1], m[2], m[3], m[4], war: (bool)row[3]);
            city.areopagusRestored = (bool)row[4];
            var c = TestWorld.Case(caseId);
            float s0 = AssemblyRules.ComputeS0(c, city, TestWorld.Cfg(), AssemblyRules.CloneStances(c));
            Assert.AreEqual((float)row[5], s0, Tolerance, "Δ1." + row[0] + " υπόθεση " + caseId);
        }

        [Test] // Δ1.11: υπόθεση 11 με τον Αγνωνίδη (στόχο της καταγγελίας) σε weight 0
        public void D1_11_ComputeS0_AfterConviction()
        {
            var c = TestWorld.Case(11);
            var stances = AssemblyRules.CloneStances(c);
            var target = stances.First(s => s.official == c.complaint.target);
            target.weight = 0;
            float s0 = AssemblyRules.ComputeS0(c, TestWorld.City(50, 50, 50, 50, 50, war: true), TestWorld.Cfg(), stances);
            Assert.AreEqual(60f, s0, Tolerance);
        }

        // ---------- Δ2 SpeechPower ----------
        [TestCase(30, 0, 10, 0.90f)]
        [TestCase(50, 6, 40, 4.95f)]
        [TestCase(80, 9, 100, 12.00f)]
        [TestCase(0, 0, 0, 0.00f)]
        public void D2_SpeechPower(int peitho, int quality, int anagn, float expected)
        {
            var cfg = TestWorld.Cfg();
            var player = TestWorld.Player(cfg);
            player.peitho = peitho;
            player.anagnorisimotita = anagn;
            LogographerData logo = null;
            if (quality > 0)
            {
                logo = UnityEngine.ScriptableObject.CreateInstance<LogographerData>();
                logo.quality = quality;
            }
            Assert.AreEqual(expected, AssemblyRules.SpeechPower(player, logo, cfg), Tolerance);
        }

        // ---------- Δ3 Persuade / Bribe / Complain / SpeakerOf ----------
        static OfficialStance Neutral(int threshold, bool bribable = false, int weight = 5)
        {
            var official = UnityEngine.ScriptableObject.CreateInstance<OfficialData>();
            official.displayName = "Δοκιμαστικός";
            official.bribable = bribable;
            return new OfficialStance { official = official, side = Side.None, weight = weight, threshold = threshold };
        }

        [Test]
        public void D3_1_PersuadeSucceedsAtThreshold()
        {
            var player = TestWorld.Player(TestWorld.Cfg()); // Πειθώ 30, Συνέπεια 50 → 55
            var stance = Neutral(50);
            Assert.IsTrue(AssemblyRules.Persuade(stance, player, Side.A));
            Assert.AreEqual(Side.A, stance.side);
        }

        [Test]
        public void D3_2_PersuadeFailsAboveThreshold()
        {
            var player = TestWorld.Player(TestWorld.Cfg());
            var stance = Neutral(60);
            Assert.IsFalse(AssemblyRules.Persuade(stance, player, Side.A));
            Assert.AreEqual(Side.None, stance.side);
        }

        [Test]
        public void D3_3_PersuadeFailsOnCommitted()
        {
            var stance = Neutral(10);
            stance.side = Side.B;
            Assert.IsFalse(AssemblyRules.Persuade(stance, TestWorld.Player(TestWorld.Cfg()), Side.A));
        }

        [Test]
        public void D3_4_BribeSucceeds()
        {
            var cfg = TestWorld.Cfg();
            var player = TestWorld.Player(cfg);
            player.wallet = 100;
            var stance = Neutral(50, bribable: true, weight: 15);
            bool ok = AssemblyRules.Bribe(stance, player, TestWorld.City(50, 50, 50, 50, 50), cfg, Side.A, new Random(7), out _);
            Assert.IsTrue(ok);
            Assert.AreEqual(10, player.wallet);
            Assert.AreEqual(50, player.ethos);
            Assert.AreEqual(1, player.bribes);
            Assert.AreEqual(Side.A, stance.side);
        }

        [Test]
        public void D3_5_BribeTooExpensive()
        {
            var cfg = TestWorld.Cfg();
            var player = TestWorld.Player(cfg);
            player.wallet = 80;
            var stance = Neutral(50, bribable: true, weight: 15);
            Assert.IsFalse(AssemblyRules.Bribe(stance, player, TestWorld.City(50, 50, 50, 50, 50), cfg, Side.A, new Random(7), out _));
            Assert.AreEqual(80, player.wallet);
            Assert.AreEqual(60, player.ethos);
            Assert.AreEqual(Side.None, stance.side);
        }

        [Test]
        public void D3_6_BribeNotBribable()
        {
            var player = TestWorld.Player(TestWorld.Cfg());
            player.wallet = 600;
            Assert.IsFalse(AssemblyRules.Bribe(Neutral(50, bribable: false), player, TestWorld.City(50, 50, 50, 50, 50), TestWorld.Cfg(), Side.A, new Random(7), out _));
        }

        [Test] // Δ3.7 — spec: με Ηθική 100 η πιθανότητα αποκάλυψης είναι (100−100)/200 = 0
        public void D3_7_BribeNeverExposedAtFullEthos()
        {
            var cfg = TestWorld.Cfg();
            var rng = new Random(42);
            for (int i = 0; i < 200; i++)
            {
                var player = TestWorld.Player(cfg);
                player.ethos = 100;
                player.wallet = 1000;
                AssemblyRules.Bribe(Neutral(50, bribable: true, weight: 5), player, TestWorld.City(50, 50, 50, 50, 50), cfg, Side.A, rng, out bool exposed);
                Assert.IsFalse(exposed, "Αποκάλυψη με Ηθική 100 στην επανάληψη " + i);
            }
        }

        [Test] // Δ3.8
        public void D3_8_ComplainConviction()
        {
            var cfg = TestWorld.Cfg();
            var c = TestWorld.Case(11);
            var stances = AssemblyRules.CloneStances(c);
            var player = TestWorld.Player(cfg);
            var city = TestWorld.City(50, 50, 50, 50, 50);
            var result = AssemblyRules.Complain(c, stances, player, city, cfg);
            Assert.IsTrue(result.filed);
            Assert.IsTrue(result.convicted);
            Assert.AreEqual(0, stances.First(s => s.official == c.complaint.target).weight, "βάρος στόχου");
            Assert.AreEqual(14, player.anagnorisimotita);
            Assert.AreEqual(46, player.parrhesia);
            Assert.AreEqual(63, player.ethos);
            Assert.AreEqual(52, city.trust);
            Assert.AreEqual(52, city.treasury);
        }

        [Test] // Δ3.9
        public void D3_9_ComplainAcquittalSycophant()
        {
            var cfg = TestWorld.Cfg();
            var c = TestWorld.Case(9);
            var stances = AssemblyRules.CloneStances(c);
            var player = TestWorld.Player(cfg);
            player.wallet = 150;
            var result = AssemblyRules.Complain(c, stances, player, TestWorld.City(50, 50, 50, 50, 50), cfg);
            Assert.IsTrue(result.filed);
            Assert.IsFalse(result.convicted);
            Assert.IsTrue(result.sycophant);
            Assert.AreEqual(60, result.fine);
            Assert.AreEqual(90, player.wallet);
            Assert.AreEqual(15, stances.First(s => s.official == c.complaint.target).weight, "cap βάρους 15");
            Assert.AreEqual(7, player.anagnorisimotita);
            Assert.AreEqual(42, player.parrhesia);
            Assert.IsTrue(player.sycophantFlag);
        }

        [Test] // Δ3.10
        public void D3_10_ComplainFineCappedByWallet()
        {
            var cfg = TestWorld.Cfg();
            var c = TestWorld.Case(13);
            var player = TestWorld.Player(cfg);
            player.wallet = 30;
            var result = AssemblyRules.Complain(c, AssemblyRules.CloneStances(c), player, TestWorld.City(50, 50, 50, 50, 50), cfg);
            Assert.AreEqual(30, result.fine);
            Assert.AreEqual(0, player.wallet);
        }

        [Test] // Δ3.11
        public void D3_11_ComplainBlockedForSycophant()
        {
            var c = TestWorld.Case(11);
            var player = TestWorld.Player(TestWorld.Cfg());
            player.sycophantFlag = true;
            var result = AssemblyRules.Complain(c, AssemblyRules.CloneStances(c), player, TestWorld.City(50, 50, 50, 50, 50), TestWorld.Cfg());
            Assert.IsFalse(result.filed);
        }

        [Test] // Δ3.12
        public void D3_12_ComplainUnavailable()
        {
            var c = TestWorld.Case(1);
            var result = AssemblyRules.Complain(c, AssemblyRules.CloneStances(c), TestWorld.Player(TestWorld.Cfg()), TestWorld.City(50, 50, 50, 50, 50), TestWorld.Cfg());
            Assert.IsFalse(result.filed);
        }

        [Test] // Δ3.13
        public void D3_13_SpeakerFallback()
        {
            var c = TestWorld.Case(11);
            var expected = c.speechB.fallbackSpeaker != null ? c.speechB.fallbackSpeaker : c.speechB.speaker;
            Assert.AreEqual(expected, AssemblyRules.SpeakerOf(c.speechB, c, convicted: true));
        }

        // ---------- Δ4 Resolve (noise 0) ----------
        [TestCase(48f, Side.A, 3f, 51f, true, true)]
        [TestCase(48f, Side.B, 3f, 45f, false, false)]
        [TestCase(60f, Side.B, 12f, 48f, false, true)]
        [TestCase(50f, Side.None, 0f, 50f, true, false)]
        public void D4_Resolve(float s0, Side side, float power, float s, bool passedA, bool flipped)
        {
            var result = AssemblyRules.Resolve(s0, side, power, new Random(1), TestWorld.Cfg(noise: 0f));
            Assert.AreEqual(s, result.s, Tolerance, "S");
            Assert.AreEqual(passedA, result.passedA, "passedA");
            Assert.AreEqual(flipped, result.playerFlipped, "playerFlipped");
        }

        [Test]
        public void D4_ContestedBand()
        {
            Assert.IsTrue(AssemblyRules.Resolve(62f, Side.A, 5f, new Random(1), TestWorld.Cfg(noise: 0f)).contested, "|62−50| = 12 → contested");
            Assert.IsFalse(AssemblyRules.Resolve(63f, Side.A, 5f, new Random(1), TestWorld.Cfg(noise: 0f)).contested, "|63−50| = 13 → όχι");
        }

        // ---------- Δ5 ApplyOutcome ----------
        [Test]
        public void D5_1_Case1PassedA()
        {
            var city = TestWorld.City(50, 50, 50, 50, 50);
            AssemblyRules.ApplyOutcome(TestWorld.Case(1), true, city, TestWorld.Cfg());
            Assert.AreEqual(52, city.trust);
            Assert.AreEqual(52, city.demosPower);
            Assert.AreEqual(50, city.treasury);
            Assert.AreEqual(50, city.food);
            Assert.AreEqual(50, city.readiness);
        }

        [Test]
        public void D5_2_Case12FailedAbolishesPay()
        {
            var city = TestWorld.City(50, 50, 50, 50, 50);
            AssemblyRules.ApplyOutcome(TestWorld.Case(12), false, city, TestWorld.Cfg());
            Assert.AreEqual(62, city.treasury);
            Assert.AreEqual(47, city.trust);
            Assert.AreEqual(46, city.demosPower);
            Assert.IsTrue(city.payAbolished);
        }

        [Test]
        public void D5_3_Case15PassedAContinuesWar()
        {
            var city = TestWorld.City(50, 50, 50, 50, 50, war: true);
            AssemblyRules.ApplyOutcome(TestWorld.Case(15), true, city, TestWorld.Cfg());
            Assert.IsTrue(city.war);
            Assert.IsFalse(city.garrison);
        }

        [Test]
        public void D5_4_5_Case15PeaceWithGarrisonThenMultiplier()
        {
            var cfg = TestWorld.Cfg();
            var city = TestWorld.City(50, 50, 50, 50, 50, war: true);
            AssemblyRules.ApplyOutcome(TestWorld.Case(15), false, city, cfg);
            Assert.IsFalse(city.war);
            Assert.IsTrue(city.garrison);
            Assert.AreEqual(55, city.readiness, "Et χωρίς πολλαπλασιαστή στην ίδια υπόθεση");
            Assert.AreEqual(47, city.trust);
            Assert.AreEqual(48, city.demosPower, "ID −2 χωρίς ×1.5 στην ίδια υπόθεση");

            AssemblyRules.ApplyOutcome(TestWorld.Case(17), false, city, cfg);
            Assert.AreEqual(45, city.demosPower, "ID −2 × 1.5 = −3 με φρουρά");
        }

        [Test]
        public void D5_6_Case17AuditFine()
        {
            var cfg = TestWorld.Cfg();
            var city = TestWorld.City(50, 50, 50, 50, 50);
            AssemblyRules.ApplyOutcome(TestWorld.Case(17), true, city, cfg);
            Assert.IsTrue(city.auditFinePending);
            int treasuryBefore = city.treasury;
            AssemblyRules.StartPrytany(city, TestWorld.Player(cfg), cfg, 2);
            Assert.AreEqual(treasuryBefore + 3 + 6, city.treasury, "T: +3 ειρήνης +6 πρόστιμο");
            Assert.IsFalse(city.auditFinePending);
        }

        [Test]
        public void D5_7_Case18RestoresAreopagus()
        {
            var city = TestWorld.City(50, 50, 50, 50, 50);
            AssemblyRules.ApplyOutcome(TestWorld.Case(18), false, city, TestWorld.Cfg());
            Assert.IsTrue(city.areopagusRestored);
        }

        [Test]
        public void D5_8_Clamp()
        {
            var city = TestWorld.City(98, 50, 50, 50, 50);
            city.Add(Metric.Treasury, 5);
            Assert.AreEqual(100, city.treasury);
            city.treasury = 2;
            city.Add(Metric.Treasury, -5);
            Assert.AreEqual(0, city.treasury);
        }

        // ---------- Δ6 ApplyPlayerAfterCase ----------
        static VoteResult Result(float s0, bool passedA) => new VoteResult { s0 = s0, s = s0, passedA = passedA };

        [Test]
        public void D6_1_SpokeWithMajorityAndWon()
        {
            var cfg = TestWorld.Cfg();
            var player = TestWorld.Player(cfg);
            AssemblyRules.ApplyPlayerAfterCase(player, cfg, spoke: true, Side.A, Side.A, Result(60f, true));
            Assert.AreEqual(1, player.speeches);
            Assert.AreEqual(1, player.speechesVindicated);
            Assert.AreEqual(14, player.anagnorisimotita);
            Assert.AreEqual(32, player.peitho);
            Assert.AreEqual(52, player.synepeia);
            Assert.AreEqual(40, player.parrhesia, "δεν μίλησε κόντρα στην πλειοψηφία");
        }

        [Test]
        public void D6_2_SpokeAgainstMajority()
        {
            var cfg = TestWorld.Cfg();
            var player = TestWorld.Player(cfg);
            AssemblyRules.ApplyPlayerAfterCase(player, cfg, spoke: true, Side.A, Side.A, Result(40f, true));
            Assert.AreEqual(44, player.parrhesia);
        }

        [Test]
        public void D6_3_VoteAgainstOwnSpeech()
        {
            var cfg = TestWorld.Cfg();
            var player = TestWorld.Player(cfg);
            AssemblyRules.ApplyPlayerAfterCase(player, cfg, spoke: true, Side.A, Side.B, Result(60f, true));
            Assert.AreEqual(42, player.synepeia);
        }

        [Test]
        public void D6_4_TwoSilences()
        {
            var cfg = TestWorld.Cfg();
            var player = TestWorld.Player(cfg);
            AssemblyRules.ApplyPlayerAfterCase(player, cfg, spoke: false, Side.None, Side.A, Result(60f, true));
            AssemblyRules.ApplyPlayerAfterCase(player, cfg, spoke: false, Side.None, Side.B, Result(60f, true));
            Assert.AreEqual(2, player.silentStreak);
            Assert.AreEqual(8, player.anagnorisimotita);
        }

        [Test]
        public void D6_5_SilenceAfterSpeech()
        {
            var cfg = TestWorld.Cfg();
            var player = TestWorld.Player(cfg);
            AssemblyRules.ApplyPlayerAfterCase(player, cfg, spoke: true, Side.A, Side.A, Result(60f, true));
            int anagnAfterSpeech = player.anagnorisimotita;
            AssemblyRules.ApplyPlayerAfterCase(player, cfg, spoke: false, Side.None, Side.A, Result(60f, true));
            Assert.AreEqual(1, player.silentStreak);
            Assert.AreEqual(anagnAfterSpeech, player.anagnorisimotita);
        }

        [Test]
        public void D6_6_SameDirectionVote()
        {
            var cfg = TestWorld.Cfg();
            var player = TestWorld.Player(cfg);
            AssemblyRules.ApplyPlayerAfterCase(player, cfg, spoke: false, Side.None, Side.A, Result(60f, true));
            int before = player.synepeia;
            AssemblyRules.ApplyPlayerAfterCase(player, cfg, spoke: false, Side.None, Side.A, Result(60f, true));
            Assert.AreEqual(before + 1, player.synepeia);
        }

        [Test]
        public void D6_7_PeithoCap()
        {
            var cfg = TestWorld.Cfg();
            var player = TestWorld.Player(cfg);
            player.peitho = 79;
            AssemblyRules.ApplyPlayerAfterCase(player, cfg, spoke: true, Side.A, Side.A, Result(60f, true));
            Assert.AreEqual(80, player.peitho);
        }

        [Test]
        public void D6_8_9_Authority()
        {
            var player = TestWorld.Player(TestWorld.Cfg());
            player.speeches = 4;
            player.speechesVindicated = 3;
            player.synepeia = 60;
            Assert.AreEqual(45, player.Authority);
            player.speeches = 0;
            Assert.AreEqual(0, player.Authority);
        }

        // ---------- Δ7 StartPrytany ----------
        [Test]
        public void D7_1_PeacePrytany()
        {
            var cfg = TestWorld.Cfg();
            var city = TestWorld.City(50, 50, 50, 50, 50);
            var player = TestWorld.Player(cfg);
            AssemblyRules.StartPrytany(city, player, cfg, 1);
            Assert.AreEqual(53, city.treasury);
            Assert.AreEqual(52, city.food);
            Assert.AreEqual(47, city.readiness);
            Assert.AreEqual(220, player.wallet);
            Assert.IsFalse(city.war);
        }

        [Test]
        public void D7_2_WarStarts()
        {
            var cfg = TestWorld.Cfg();
            var city = TestWorld.City(50, 50, 50, 50, 50);
            AssemblyRules.StartPrytany(city, TestWorld.Player(cfg), cfg, 5);
            Assert.IsTrue(city.war);
            Assert.AreEqual(47, city.treasury);
            Assert.AreEqual(46, city.food);
            Assert.AreEqual(49, city.readiness);
        }

        [Test]
        public void D7_3_PayAbolished()
        {
            var cfg = TestWorld.Cfg();
            var city = TestWorld.City(50, 50, 50, 50, 50);
            city.payAbolished = true;
            var player = TestWorld.Player(cfg);
            AssemblyRules.StartPrytany(city, player, cfg, 6);
            Assert.AreEqual(210, player.wallet, "εισόδημα μόνο, χωρίς μισθό");
        }

        [Test]
        public void D7_4_AuditFine()
        {
            var cfg = TestWorld.Cfg();
            var city = TestWorld.City(50, 50, 50, 50, 50);
            city.auditFinePending = true;
            AssemblyRules.StartPrytany(city, TestWorld.Player(cfg), cfg, 10);
            Assert.AreEqual(59, city.treasury, "+3 ειρήνης +6 πρόστιμο");
            Assert.IsFalse(city.auditFinePending);
        }
    }
}
