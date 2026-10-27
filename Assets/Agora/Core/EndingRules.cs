namespace Agora
{
    /// <summary>Τα τέλη. Προσωπικό τέλος ΠΟΤΕ από ψήφους — μόνο από λόγους, φήμη, ήθος, συνέπεια.</summary>
    public static class EndingRules
    {
        public static CityEnding City(CityState c, GameConfig cfg)
        {
            if (c.food < cfg.famineFood || (c.war && c.readiness < cfg.famineReadiness)) return CityEnding.Famine;
            if (c.demosPower < cfg.coupDemosPower) return CityEnding.PropertyLimit;
            if (c.trust < cfg.stasisTrust && c.demosPower < 50) return CityEnding.Stasis;
            if (c.demosPower >= 50 && c.MinAll() >= cfg.enduresMinAll) return CityEnding.Counterfactual;
            return CityEnding.GarrisonOnly;
        }

        public static PersonalEnding Personal(PlayerState p, CityEnding city)
        {
            if (city == CityEnding.PropertyLimit && p.spokeForA >= 10 && p.anagnorisimotita >= 50) return PersonalEnding.Hypereides;
            if (city == CityEnding.PropertyLimit && p.spokeForB >= 10) return PersonalEnding.Demades;
            if (p.anagnorisimotita >= 70 && p.ethos < 40) return PersonalEnding.HarpalusMan;
            if (p.anagnorisimotita >= 60 && p.ethos >= 50 && p.synepeia >= 50) return PersonalEnding.Lycurgus;
            if (p.anagnorisimotita < 30) return PersonalEnding.Anonymous;
            if (p.ethos < 50) return PersonalEnding.Stratocles;
            return PersonalEnding.Anonymous;
        }

        /// <summary>Ο ανώνυμος θήτης στο τέλος Π2 είναι ανάμεσα στους 12.000 αποκλεισμένους.</summary>
        public static bool ExcludedByPropertyLimit(PlayerState p, CityEnding city)
            => city == CityEnding.PropertyLimit && p.socialClass == SocialClass.Thes;
    }
}
