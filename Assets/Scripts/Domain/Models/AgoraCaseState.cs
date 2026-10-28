using System;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Η κατάσταση της τρέχουσας υπόθεσης Agora μέσα στο save. Το αποτέλεσμα της ψηφοφορίας
    /// αποθηκεύεται ΤΗ στιγμή της ψήφου, ώστε φόρτωση στη μέση υπόθεσης να μην ξαναρίχνει το ζάρι.
    /// Τα enums του Agora αποθηκεύονται ως int για σταθερή σειριαλοποίηση.
    /// </summary>
    [Serializable]
    public sealed class AgoraCaseState
    {
        public int caseId = -1;               // -1 = καμία ενεργή υπόθεση
        public string step = "";              // case-news, case-prep, case-trial, case-speeches, case-speech, case-vote, case-result
        public int intendedSide;              // Agora.Side
        public int prep;                      // Agora.PrepAction
        public bool prepDone;                 // η μία ενέργεια προπαρασκευής καταναλώθηκε (ή παραλείφθηκε)
        public bool convicted, spoke;
        public bool trialSycophant;           // αποτέλεσμα δίκης για την οθόνη case-trial
        public int trialFine;
        public int spokeFor;                  // Agora.Side
        public int logoIndex = -1;            // δείκτης στο GameContent.logographers
        public int[] stanceSides = Array.Empty<int>();    // παράλληλα με τα CaseData.officials
        public int[] stanceWeights = Array.Empty<int>();
        public bool resolved;
        public float s0, s;
        public bool passedA, playerFlipped, contested;
        public int vote;                      // Agora.Side
    }
}
