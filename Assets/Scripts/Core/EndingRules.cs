namespace Agora
{
    /// <summary>Τα τέλη (Agora_FINAL_DESIGN ενότητα 3): ένας κανόνας μίας γραμμής το καθένα, ελεγμένοι
    /// με τη σειρά· ισχύει ο πρώτος που ταιριάζει.</summary>
    public static class EndingRules
    {
        public static CityEnding City(CityState c, GameConfig cfg)
        {
            if (c.food < cfg.famineFood || (c.war && c.readiness < cfg.famineReadiness)) return CityEnding.Famine;
            if (c.demosPower < cfg.coupDemosPower) return CityEnding.PropertyLimit;
            if (c.trust < cfg.stasisTrust && c.demosPower < cfg.demosPowerMajority) return CityEnding.Stasis;
            if (c.demosPower >= cfg.demosPowerMajority && c.MinAll() >= cfg.enduresMinAll && !c.garrison) return CityEnding.Counterfactual;
            return CityEnding.GarrisonOnly;
        }

        /// <summary>Η πόλη «έχασε» στα τέλη 2 (όριο των δύο χιλιάδων) και 5 (φρουρά).</summary>
        public static bool CityLost(CityEnding city) => city == CityEnding.PropertyLimit || city == CityEnding.GarrisonOnly;

        public static PersonalEnding Personal(PlayerState p, CityEnding city, GameConfig cfg)
        {
            bool lost = CityLost(city);
            if (lost && p.spokeForA >= cfg.sideSpeeches && p.spokeForA > p.spokeForB) return PersonalEnding.Hypereides;
            if (lost && p.spokeForB >= cfg.sideSpeeches && p.spokeForB > p.spokeForA) return PersonalEnding.Demades;
            if (p.bribes >= cfg.harpalusBribes) return PersonalEnding.HarpalusMan;
            if (p.fame >= cfg.knownFame && p.ethos >= cfg.trustedEthos) return PersonalEnding.Lycurgus;
            if (p.fame >= cfg.knownFame) return PersonalEnding.Stratocles;
            return PersonalEnding.Anonymous;
        }

        /// <summary>Ο ανώνυμος θήτης στο τέλος «όριο των δύο χιλιάδων» είναι ανάμεσα στους 12.000 αποκλεισμένους.</summary>
        public static bool ExcludedByPropertyLimit(PlayerState p, CityEnding city)
            => city == CityEnding.PropertyLimit && p.socialClass == SocialClass.Thes;
    }
}
