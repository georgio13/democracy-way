using System;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Η κατάσταση της εβδομάδας μέσα στο save. Η υπόθεση ξεκινά με την επίσκεψη στην Αγορά.
    /// Οι αναλύσεις της δίκης και της ψηφοφορίας αποθηκεύονται ΤΗ στιγμή που γίνονται, ώστε φόρτωση
    /// στη μέση της εβδομάδας να μην ξαναρίχνει τη διάθεση. Τα enums του Agora αποθηκεύονται ως int.
    /// </summary>
    [Serializable]
    public sealed class AgoraCaseState
    {
        public int caseId = -1;               // -1 = καμία ενεργή υπόθεση
        public string step = "";              // city, deliberation, speech, vote, result

        // Οι τρεις ανεξάρτητες κινήσεις της εβδομάδας
        public bool officialMoveDone;         // μία κίνηση στον αναποφάσιστο (πειθώ ή δωροδοκία)
        public int officialFavors;            // Agora.Side
        public bool officialBribed, bribeExposed;
        public bool logographerBought;        // ένας λογογράφος
        public int logographerFor;            // Agora.Side· None με logographerForTrial = λόγος για την κατηγορία
        public bool logographerForTrial;
        public bool accuser;                  // ο παίκτης κατηγορεί στη δίκη της εβδομάδας

        // Η δίκη της εβδομάδας
        public int judgeVote;                 // 0 καμία, 1 ένοχος, 2 αθώος (μόνο για την έρευνα)
        public bool trialDone, trialGuilty;
        public Agora.TrialBreakdown trialResult;

        // Η συνέλευση
        public bool spoke;
        public int spokeFor;                  // Agora.Side
        public bool resolved;
        public bool passedA;
        public int vote;                      // Agora.Side
        public Agora.VoteBreakdown voteResult;
        public int pressureMetric;            // Agora.Metric που μέτρησε για την Πίεση (υπ. 20: ο χαμηλότερος τη στιγμή της ψήφου)
    }
}
