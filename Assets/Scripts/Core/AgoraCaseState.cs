using System;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Η κατάσταση της εβδομάδας μέσα στο save. Η υπόθεση ξεκινά με την επίσκεψη στην Αγορά και
    /// μαζεύει επιλογές από τις οθόνες της πόλης (Λογογράφοι, Αξιωματούχοι, Καταγγελίες)· η συνέλευση
    /// τις καταναλώνει. Το αποτέλεσμα της ψηφοφορίας αποθηκεύεται ΤΗ στιγμή της ψήφου, ώστε φόρτωση
    /// στη μέση υπόθεσης να μην ξαναρίχνει το ζάρι. Τα enums του Agora αποθηκεύονται ως int.
    /// </summary>
    [Serializable]
    public sealed class AgoraCaseState
    {
        public int caseId = -1;               // -1 = καμία ενεργή υπόθεση
        public string step = "";              // city, trial, deliberation, speech, vote, result
        public int prep;                      // Agora.PrepAction — η ΜΙΑ ενέργεια της εβδομάδας
        public bool prepDone;                 // η ενέργεια προπαρασκευής καταναλώθηκε
        public bool complaintPending;         // «τη φέρνω στη συνέλευση» — η δίκη εκτελείται ΕΚΕΙ, όχι στις Καταγγελίες
        public bool speechBought;             // αγοράστηκε λόγος στους Λογογράφους
        public int speechSide;                // Agora.Side του αγορασμένου λόγου
        public int logoIndex = -1;            // ποιος λογογράφος γράφτηκε (1..3)· -1 = κανείς
        public bool speechDelivered;          // στο βήμα εκφωνήθηκε ο αγορασμένος λόγος (αλλιώς μόνος = L0)
        public bool convicted, spoke;
        public bool trialSycophant;           // αποτέλεσμα δίκης για την οθόνη case-trial
        public int trialFine;
        public int spokeFor;                  // Agora.Side
        public int[] stanceSides = Array.Empty<int>();    // παράλληλα με τα CaseData.officials
        public int[] stanceWeights = Array.Empty<int>();
        public bool resolved;
        public float s0, s;
        public bool passedA, playerFlipped, contested;
        public int vote;                      // Agora.Side
    }
}
