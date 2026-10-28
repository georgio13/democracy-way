namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Λογιστική της εβδομάδας πάνω στο AgoraCaseState: ποια ενέργεια καταναλώθηκε και τι κουβαλάει
    /// ο παίκτης στη συνέλευση. ΚΑΝΕΝΑΣ κανόνας παιχνιδιού εδώ — οι αριθμοί και οι εκβάσεις ζουν
    /// αποκλειστικά στο Agora.AssemblyRules.
    /// </summary>
    public static class WeekFlowRules
    {
        /// <summary>Η μία προπαρασκευαστική ενέργεια της εβδομάδας είναι ακόμη διαθέσιμη.</summary>
        public static bool CanPrepare(AgoraCaseState s) => s != null && !s.prepDone && !s.resolved;

        /// <summary>Κλειδώνει την ενέργεια της εβδομάδας (Persuade/Bribe/Complain).</summary>
        public static bool MarkPrep(AgoraCaseState s, Agora.PrepAction action)
        {
            if (!CanPrepare(s)) return false;
            s.prep = (int)action;
            s.prepDone = true;
            return true;
        }

        /// <summary>«Τη φέρνω στη συνέλευση»: δεσμεύει την καταγγελία χωρίς να εκτελέσει τη δίκη.</summary>
        public static bool CommitComplaint(AgoraCaseState s)
        {
            if (!MarkPrep(s, Agora.PrepAction.Complain)) return false;
            s.complaintPending = true;
            return true;
        }

        /// <summary>Η συνέλευση παραλαμβάνει τη δεσμευμένη καταγγελία — ακριβώς μία φορά.</summary>
        public static bool TakePendingComplaint(AgoraCaseState s)
        {
            if (s == null || !s.complaintPending || s.resolved) return false;
            s.complaintPending = false;
            return true;
        }

        public static bool CanBuySpeech(AgoraCaseState s, int wallet, int cost) =>
            s != null && !s.resolved && !s.spoke && !s.speechBought && wallet >= cost;

        /// <summary>Αγορά λόγου στους Λογογράφους: μία την εβδομάδα, χωρίς επιστροφή.</summary>
        public static bool BuySpeech(AgoraCaseState s, Agora.PlayerState player, int logoIndex, Agora.Side side, int cost)
        {
            if (logoIndex < 1 || side == Agora.Side.None || !CanBuySpeech(s, player.wallet, cost)) return false;
            player.wallet -= cost;
            s.speechBought = true;
            s.speechSide = (int)side;
            s.logoIndex = logoIndex;
            return true;
        }

        /// <summary>Στο βήμα: εκφώνηση του αγορασμένου λόγου, ομιλία μόνος, ή σιωπή (side None).</summary>
        public static bool Speak(AgoraCaseState s, Agora.Side side, bool delivered)
        {
            if (s == null || s.spoke || s.resolved) return false;
            if (delivered && (!s.speechBought || s.logoIndex < 1)) return false;
            if (side == Agora.Side.None) return true;   // σιωπή: δεν γράφεται ομιλία
            s.spoke = true;
            s.spokeFor = (int)side;
            s.speechDelivered = delivered;
            return true;
        }

        /// <summary>Ποιον λογογράφο χρησιμοποιεί το SpeechPower: τον αγορασμένο αν εκφωνήθηκε, αλλιώς L0 (μόνος).</summary>
        public static int SpeechLogoIndex(AgoraCaseState s) =>
            s == null || !s.spoke ? -1 : s.speechDelivered && s.logoIndex >= 1 ? s.logoIndex : 0;
    }
}
