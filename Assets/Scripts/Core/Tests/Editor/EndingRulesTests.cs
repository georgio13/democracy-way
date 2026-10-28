using NUnit.Framework;

namespace Agora.Tests
{
    /// <summary>VERIFICATION.md Δ8: τέλη πόλης. Τα τέλη του παίκτη ελέγχονται στο βήμα 7.</summary>
    public sealed class EndingRulesTests
    {
        // T, Ep, Et, Em, ID, war → CityEnding
        [TestCase(50, 10, 50, 50, 50, false, CityEnding.Famine)]
        [TestCase(50, 50, 10, 50, 50, true, CityEnding.Famine)]
        [TestCase(50, 50, 10, 50, 50, false, CityEnding.GarrisonOnly, TestName = "D8_City_EtLowPeace_NoFamine_NoCounterfactual")]
        [TestCase(50, 50, 50, 50, 20, false, CityEnding.PropertyLimit)]
        [TestCase(50, 50, 50, 30, 45, false, CityEnding.Stasis)]
        [TestCase(50, 50, 50, 30, 55, false, CityEnding.Counterfactual)]
        [TestCase(50, 50, 50, 50, 55, false, CityEnding.Counterfactual)]
        [TestCase(50, 50, 50, 50, 45, false, CityEnding.GarrisonOnly)]
        [TestCase(20, 50, 50, 50, 55, false, CityEnding.GarrisonOnly)]
        public void D8_City(int t, int ep, int et, int em, int id, bool war, CityEnding expected)
        {
            var city = TestWorld.City(t, ep, et, em, id, war);
            Assert.AreEqual(expected, EndingRules.City(city, TestWorld.Cfg()));
        }

        [Test]
        public void D8_ExcludedByPropertyLimit()
        {
            var thes = TestWorld.Player(TestWorld.Cfg(), SocialClass.Thes);
            var zeugites = TestWorld.Player(TestWorld.Cfg(), SocialClass.Zeugites);
            Assert.IsTrue(EndingRules.ExcludedByPropertyLimit(thes, CityEnding.PropertyLimit));
            Assert.IsFalse(EndingRules.ExcludedByPropertyLimit(zeugites, CityEnding.PropertyLimit));
        }
    }
}
