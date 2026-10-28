using DemocracyWay.NewUI;
using NUnit.Framework;

namespace Agora.Tests
{
    /// <summary>Ο πολίτης της βιογραφίας = αρχικές τιμές GameConfig + δέλτα + τάξη.</summary>
    public sealed class CitizenPreviewTests
    {
        [Test] // (α) Χωρίς επιλογές: ακριβώς τα defaults του GameConfig και το πορτοφόλι της τάξης.
        public void NoChoices_MatchesGameConfigDefaults()
        {
            var cfg = TestWorld.Cfg();
            var citizen = CitizenPreviewRules.Preview(cfg, SocialClass.Zeugites, "t", new BiographyDeltas());
            Assert.AreEqual(cfg.startPeitho, citizen.peitho);
            Assert.AreEqual(cfg.startFame, citizen.fame);
            Assert.AreEqual(cfg.startEthos, citizen.ethos);
            Assert.AreEqual(cfg.ClassMoney(SocialClass.Zeugites).wallet, citizen.wallet);
        }

        [Test] // (β) Δέλτα εφαρμόζονται πάνω στα defaults.
        public void DeltasApplyOnTopOfDefaults()
        {
            var cfg = TestWorld.Cfg();
            var citizen = CitizenPreviewRules.Preview(cfg, SocialClass.Zeugites, "t",
                new BiographyDeltas { ethos = -10, peitho = 5, walletDrachmas = -20 });
            Assert.AreEqual(cfg.startEthos - 10, citizen.ethos);
            Assert.AreEqual(cfg.startPeitho + 5, citizen.peitho);
            Assert.AreEqual(cfg.startFame, citizen.fame);
            Assert.AreEqual(cfg.ClassMoney(SocialClass.Zeugites).wallet - 20, citizen.wallet);
        }

        [Test] // (γ) Clamp 0–100 και πορτοφόλι ποτέ αρνητικό.
        public void ValuesAreClamped()
        {
            var cfg = TestWorld.Cfg();
            var citizen = CitizenPreviewRules.Preview(cfg, SocialClass.Thes, "t",
                new BiographyDeltas { ethos = 500, peitho = -500, walletDrachmas = -100000 });
            Assert.AreEqual(100, citizen.ethos);
            Assert.AreEqual(0, citizen.peitho);
            Assert.AreEqual(0, citizen.wallet);
        }

        [Test] // Μηδενικά δέλτα και null δέλτα δίνουν το ίδιο αποτέλεσμα με καθαρό PlayerState.
        public void NullDeltasEqualFreshCitizen()
        {
            var cfg = TestWorld.Cfg();
            var preview = CitizenPreviewRules.Preview(cfg, SocialClass.Hippeus, "t", null);
            var fresh = new PlayerState(cfg, SocialClass.Hippeus, "t");
            Assert.AreEqual(fresh.peitho, preview.peitho);
            Assert.AreEqual(fresh.fame, preview.fame);
            Assert.AreEqual(fresh.ethos, preview.ethos);
            Assert.AreEqual(fresh.wallet, preview.wallet);
        }
    }
}
