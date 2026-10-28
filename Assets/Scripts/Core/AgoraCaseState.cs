using System;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Η κατάσταση της εβδομάδας μέσα στο save. Η υπόθεση ξεκινά με την επίσκεψη στην Αγορά.
    /// Το αποτέλεσμα της ψηφοφορίας αποθηκεύεται ΤΗ στιγμή της ψήφου, ώστε φόρτωση στη μέση
    /// υπόθεσης να μην ξαναρίχνει τη διάθεση. Τα enums του Agora αποθηκεύονται ως int.
    /// </summary>
    [Serializable]
    public sealed class AgoraCaseState
    {
        public int caseId = -1;               // -1 = καμία ενεργή υπόθεση
        public string step = "";              // city, deliberation, speech, vote, result
        public bool spoke;
        public int spokeFor;                  // Agora.Side
        public bool resolved;
        public bool passedA;
        public int vote;                      // Agora.Side
    }
}
