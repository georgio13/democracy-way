using NUnit.Framework;

namespace Agora.Tests
{
    /// <summary>Agora_FINAL_DESIGN ενότητα 3: τέλη πόλης και τέλη παίκτη, με τη σειρά και τα όριά τους.</summary>
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

        [Test] // «Η πόλη που άντεξε» μόνο χωρίς φρουρά (στην υπ. 15 δεν πέρασε η Β).
        public void City_GarrisonBlocksCounterfactual()
        {
            var city = TestWorld.City(50, 50, 50, 50, 55);
            city.garrison = true;
            Assert.AreEqual(CityEnding.GarrisonOnly, EndingRules.City(city, TestWorld.Cfg()));
        }

        static PlayerState Citizen(int a, int b, int bribes, int fame, int ethos)
        {
            var p = TestWorld.Player(TestWorld.Cfg());
            p.spokeForA = a; p.spokeForB = b; p.bribes = bribes; p.fame = fame; p.ethos = ethos;
            return p;
        }

        // λόγοι Α, λόγοι Β, δωροδοκίες, Φήμη, Ηθική, τέλος πόλης → τέλος παίκτη
        [TestCase(8, 3, 0, 10, 60, CityEnding.PropertyLimit, PersonalEnding.Hypereides)]
        [TestCase(8, 3, 0, 10, 60, CityEnding.GarrisonOnly, PersonalEnding.Hypereides)]
        [TestCase(7, 3, 0, 10, 60, CityEnding.PropertyLimit, PersonalEnding.Anonymous, TestName = "Personal_SevenSpeechesAreNotEnough")]
        [TestCase(8, 3, 0, 10, 60, CityEnding.Counterfactual, PersonalEnding.Anonymous, TestName = "Personal_HypereidesOnlyWhenCityLost")]
        [TestCase(8, 3, 0, 10, 60, CityEnding.Famine, PersonalEnding.Anonymous, TestName = "Personal_FamineIsNotALoss")]
        [TestCase(9, 9, 0, 10, 60, CityEnding.PropertyLimit, PersonalEnding.Anonymous, TestName = "Personal_TieIsNeitherSide")]
        [TestCase(3, 8, 0, 10, 60, CityEnding.GarrisonOnly, PersonalEnding.Demades)]
        [TestCase(8, 3, 3, 10, 60, CityEnding.PropertyLimit, PersonalEnding.Hypereides, TestName = "Personal_HypereidesBeforeHarpalus")]
        [TestCase(0, 0, 3, 50, 70, CityEnding.Counterfactual, PersonalEnding.HarpalusMan)]
        [TestCase(0, 0, 2, 50, 70, CityEnding.Counterfactual, PersonalEnding.Lycurgus)]
        [TestCase(0, 0, 0, 40, 50, CityEnding.Counterfactual, PersonalEnding.Lycurgus)]
        [TestCase(0, 0, 0, 40, 49, CityEnding.Counterfactual, PersonalEnding.Stratocles)]
        [TestCase(0, 0, 0, 39, 90, CityEnding.Counterfactual, PersonalEnding.Anonymous)]
        public void Personal(int a, int b, int bribes, int fame, int ethos, CityEnding city, PersonalEnding expected)
            => Assert.AreEqual(expected, EndingRules.Personal(Citizen(a, b, bribes, fame, ethos), city, TestWorld.Cfg()));

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
