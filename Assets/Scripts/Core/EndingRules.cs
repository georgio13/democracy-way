namespace Agora
{
    /// <summary>Τα τέλη. Προσωπικό τέλος ΠΟΤΕ από ψήφους — μόνο από λόγους, φήμη, ήθος, συνέπεια.</summary>
    public static class EndingRules
    {
        public static CityEnding City(CityState c, GameConfig cfg)
        {
            if (c.food < cfg.famineFood || (c.war && c.readiness < cfg.famineReadiness)) return CityEnding.Famine;
            if (c.demosPower < cfg.coupDemosPower) return CityEnding.PropertyLimit;
            if (c.trust < cfg.stasisTrust && c.demosPower < cfg.demosPowerMajority) return CityEnding.Stasis;
            if (c.demosPower >= cfg.demosPowerMajority && c.MinAll() >= cfg.enduresMinAll) return CityEnding.Counterfactual;
            return CityEnding.GarrisonOnly;
        }

        public static PersonalEnding Personal(PlayerState p, CityEnding city, GameConfig cfg)
        {
            if (city == CityEnding.PropertyLimit && p.spokeForA >= cfg.personalFactionSpeeches && p.anagnorisimotita >= cfg.personalAnagnHypereides) return PersonalEnding.Hypereides;
            if (city == CityEnding.PropertyLimit && p.spokeForB >= cfg.personalFactionSpeeches) return PersonalEnding.Demades;
            if (p.anagnorisimotita >= cfg.personalAnagnHarpalus && p.ethos < cfg.personalEthosHarpalusMax) return PersonalEnding.HarpalusMan;
            if (p.anagnorisimotita >= cfg.personalAnagnLycurgus && p.ethos >= cfg.personalEthosLycurgusMin && p.synepeia >= cfg.personalSynepeiaLycurgusMin) return PersonalEnding.Lycurgus;
            if (p.anagnorisimotita < cfg.personalAnagnAnonymousMax) return PersonalEnding.Anonymous;
            if (p.ethos < cfg.personalEthosStratoclesMax) return PersonalEnding.Stratocles;
            return PersonalEnding.Anonymous;
        }

        /// <summary>Ο ανώνυμος θήτης στο τέλος Π2 είναι ανάμεσα στους 12.000 αποκλεισμένους.</summary>
        public static bool ExcludedByPropertyLimit(PlayerState p, CityEnding city)
            => city == CityEnding.PropertyLimit && p.socialClass == SocialClass.Thes;
    }
}
