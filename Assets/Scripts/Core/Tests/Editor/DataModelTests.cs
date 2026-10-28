using DemocracyWay.NewUI;
using NUnit.Framework;
using UnityEngine;

namespace Agora.Tests
{
    /// <summary>Agora_FINAL_DESIGN ενότητα 2 και βήμα 8.2: το μοντέλο δεδομένων του παίκτη, της υπόθεσης και της βιογραφίας.</summary>
    public sealed class DataModelTests
    {
        [TestCase(SocialClass.Thes, 60, 30)]
        [TestCase(SocialClass.Zeugites, 150, 60)]
        [TestCase(SocialClass.Hippeus, 300, 120)]
        [TestCase(SocialClass.Pentakosiomedimnos, 600, 250)]
        public void EachClassStartsWithItsOwnMoney(SocialClass cls, int wallet, int income)
        {
            var citizen = CitizenPreviewRules.Preview(TestWorld.Cfg(), cls, "t", new BiographyDeltas());
            Assert.AreEqual(wallet, citizen.wallet);
            Assert.AreEqual(income, citizen.income);
        }

        [Test] // Αρχή: Πειθώ 30, Φήμη 10, Ηθική 60.
        public void NewCitizenStartsWithDesignValues()
        {
            var citizen = new PlayerState(TestWorld.Cfg(), SocialClass.Zeugites, "t");
            Assert.AreEqual(30, citizen.peitho);
            Assert.AreEqual(10, citizen.fame);
            Assert.AreEqual(60, citizen.ethos);
            Assert.IsFalse(citizen.sycophantFlag);
        }

        [Test] // peitho → Πειθώ · ethos + synepeia → Ηθική · anagnorisimotita + parrhesia → Φήμη · walletDrachmas → Δραχμές.
        public void BiographyWritesToTheNewMetrics()
        {
            var cfg = TestWorld.Cfg();
            var d = new BiographyDeltas { peitho = 4, ethos = 5, synepeia = -3, anagnorisimotita = 6, parrhesia = 2, walletDrachmas = 10 };
            var citizen = CitizenPreviewRules.Preview(cfg, SocialClass.Thes, "t", d);
            Assert.AreEqual(34, citizen.peitho);
            Assert.AreEqual(62, citizen.ethos);
            Assert.AreEqual(18, citizen.fame);
            Assert.AreEqual(70, citizen.wallet);
        }

        [Test]
        public void FameIsClamped()
        {
            var citizen = CitizenPreviewRules.Preview(TestWorld.Cfg(), SocialClass.Thes, "t", new BiographyDeltas { anagnorisimotita = -50 });
            Assert.AreEqual(0, citizen.fame);
        }

        [TestCase(Difficulty.Easy, 30)]
        [TestCase(Difficulty.Medium, 45)]
        [TestCase(Difficulty.Hard, 60)]
        public void DifficultyLevels(Difficulty d, int peitho) => Assert.AreEqual(peitho, TestWorld.Cfg().PersuadeThreshold(d));

        [Test] // Η πίεση κοιτάζει τον δείκτη της υπόθεσης· η υπόθεση 20 τον χαμηλότερο από τους 4 ορατούς (όχι την Ισχύ του Δήμου).
        public void PressureValue_OneMetricOrLowestOfFourVisible()
        {
            var c = ScriptableObject.CreateInstance<CaseData>();
            var city = TestWorld.City(40, 33, 60, 45, 10, war: false);
            c.pressureMetric = Metric.Trust;
            Assert.AreEqual(45, AssemblyRules.PressureValue(c, city));
            c.pressureAnyMetric = true;
            Assert.AreEqual(33, AssemblyRules.PressureValue(c, city));
            Object.DestroyImmediate(c);
        }

        [Test] // Οι αριθμοί των κινήσεων της ενότητας 2 είναι οι προεπιλογές του GameConfig.
        public void MoveCostsMatchSection2()
        {
            var cfg = TestWorld.Cfg();
            Assert.AreEqual(40, cfg.logographerCost);
            Assert.AreEqual(30, cfg.bribeCost);
            Assert.AreEqual(10, cfg.bribeEthosLoss);
            Assert.AreEqual(3, cfg.bribeExposureOneIn);
            Assert.AreEqual(5, cfg.bribeExposureFameLoss);
            Assert.AreEqual(3, cfg.bribeExposureTrustLoss);
            Assert.AreEqual(50, cfg.accuserFine);
            Assert.AreEqual(10, cfg.assemblyPayPerPrytany);
        }
    }
}
