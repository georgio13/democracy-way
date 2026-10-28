namespace Agora
{
    /// <summary>Τα τέλη. Τα τέλη του παίκτη (ενότητα 3 του Agora_FINAL_DESIGN) έρχονται στο βήμα 7.</summary>
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

        /// <summary>Ο ανώνυμος θήτης στο τέλος «όριο των δύο χιλιάδων» είναι ανάμεσα στους 12.000 αποκλεισμένους.</summary>
        public static bool ExcludedByPropertyLimit(PlayerState p, CityEnding city)
            => city == CityEnding.PropertyLimit && p.socialClass == SocialClass.Thes;
    }
}
