using System.Linq;
using DemocracyWay.NewUI;
using NUnit.Framework;

namespace Agora.Tests
{
    /// <summary>Βήμα 8: ο προσομοιωτής ισορροπίας παίζει με τις συναρτήσεις του παιχνιδιού, και τα αποτελέσματα
    /// μένουν μέσα στο ±8 των πινάκων της ενότητας 4.</summary>
    public sealed class BalanceSimulatorTests
    {
        [Test]
        public void SameSeed_SameGame()
        {
            var a = BalanceSimulator.Play(TestWorld.Content, BalanceSimulator.Strategy.Random, new System.Random(11));
            var b = BalanceSimulator.Play(TestWorld.Content, BalanceSimulator.Strategy.Random, new System.Random(11));
            CollectionAssert.AreEqual(a.passedA, b.passedA);
            Assert.AreEqual(a.city, b.city);
            Assert.AreEqual(a.personal, b.personal);
        }

        [Test] // Ο σιωπηλός δεν μιλά, δεν κρίνει τίποτα, δεν δωροδοκεί.
        public void Silent_NeverActs()
        {
            var g = BalanceSimulator.Play(TestWorld.Content, BalanceSimulator.Strategy.Silent, new System.Random(3));
            Assert.AreEqual(0, g.finalPlayer.spokeForA + g.finalPlayer.spokeForB);
            Assert.AreEqual(0, g.finalPlayer.bribes);
            Assert.AreEqual(0, g.decided);
        }

        [Test] // Ο ρήτορας μιλά και στις 20 υποθέσεις, πάντα για την πλευρά του, και η Πειθώ σταματά στο ανώτατο.
        public void Speaker_SpeaksEveryWeek()
        {
            var cfg = TestWorld.Content.config;
            var g = BalanceSimulator.Play(TestWorld.Content, BalanceSimulator.Strategy.DemocratSpeaker, new System.Random(5));
            Assert.AreEqual(20, g.finalPlayer.spokeForA);
            Assert.AreEqual(0, g.finalPlayer.spokeForB);
            Assert.AreEqual(cfg.speechPeithoMax, g.finalPlayer.peitho);
            Assert.AreEqual(0, g.finalPlayer.bribes, "ο τίμιος ρήτορας δεν δωροδοκεί");
        }

        [Test]
        public void Summary_CountsEveryGame()
        {
            var s = BalanceSimulator.Run(TestWorld.Content, BalanceSimulator.Strategy.HalfSpeakerA, 50, 1);
            Assert.AreEqual(50, s.city.Values.Sum());
            Assert.AreEqual(50, s.personal.Values.Sum());
            Assert.AreEqual(100, System.Enum.GetValues(typeof(CityEnding)).Cast<CityEnding>().Sum(e => s.CityPercent(e)), 1e-9);
        }

        [Test] // Ενότητα 4: 7 × 5.000 παρτίδες (λίγα δευτερόλεπτα)· πιάνει κάθε αλλαγή κανόνα που χαλά την ισορροπία.
        public void Balance_WithinTolerance()
        {
            var summaries = BalanceSimulator.All.Select((s, i) => BalanceSimulator.Run(TestWorld.Content, s, 5000, 1000 + i)).ToList();
            var failed = BalanceTargets.Compare(summaries).Where(c => !c.Ok).ToList();
            Assert.IsEmpty(failed, string.Join("\n", failed.Select(c => $"{c.group} {c.label}: στόχος {c.target}, βγήκε {c.actual:0.#}")));
        }
    }
}
