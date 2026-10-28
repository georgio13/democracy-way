namespace Agora
{
    /// <summary>Οι τέσσερις ορατοί δείκτες της πόλης και ο κρυφός (ID).</summary>
    public enum Metric { Treasury, Food, Readiness, Trust, DemosPower }

    /// <summary>Πλευρά πρότασης: A = δημοκρατική, B = ολιγαρχική/κεντρική.</summary>
    public enum Side { None, A, B }

    /// <summary>Τάξη του παίκτη (Σόλων). Καθορίζει μόνο πορτοφόλι/εισόδημα.</summary>
    public enum SocialClass { Thes, Zeugites, Hippeus, Pentakosiomedimnos }

    /// <summary>Τρόπος ανάδειξης αξιώματος κατά την Αθηναίων Πολιτεία.</summary>
    public enum SelectionMode { Lot, Election, FromCouncil, ByAge, Lifelong }

    /// <summary>Διαδικασίες καταγγελίας (ΑΠ 43.4, 45.2, 52.1, 59.2–3).</summary>
    public enum ComplaintProcedure { Eisangelia, Endeixis, Phasis, GrapheSykophantias, GrapheParanomon }

    /// <summary>Η μία ενέργεια προπαρασκευής ανά υπόθεση.</summary>
    public enum PrepAction { None, Persuade, Bribe, Complain }

    /// <summary>Φάσεις της ροής. Κάθε φάση = ένα προκατασκευασμένο panel στη σκηνή.</summary>
    public enum Phase
    {
        Prologue, PreTest, Attitude, CharacterCreation,
        PrytanyIntro, CaseNews, CasePrep, CaseSpeeches, CasePlayerSpeech, CaseVote, CaseResult,
        PostTest, AttitudePost, Meega, Epilogue
    }

    public enum CityEnding { Famine, PropertyLimit, Stasis, Counterfactual, GarrisonOnly }
    public enum PersonalEnding { Hypereides, Demades, HarpalusMan, Lycurgus, Anonymous, Stratocles }
}
