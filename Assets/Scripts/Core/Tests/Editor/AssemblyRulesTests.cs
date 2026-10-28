using NUnit.Framework;

namespace Agora.Tests
{
    /// <summary>Οι κανόνες που μένουν από το παλιό spec και ισχύουν και στο Agora_FINAL_DESIGN:
    /// οι επιπτώσεις Α/Β με τους 4 ειδικούς κανόνες (Δ5) και η αλλαγή πρυτανείας (Δ7).</summary>
    public sealed class AssemblyRulesTests
    {
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
