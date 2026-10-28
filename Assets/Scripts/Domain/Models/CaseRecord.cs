using System;

namespace DemocracyWay.NewUI
{
    /// <summary>Ελάχιστη εγγραφή ολοκληρωμένης υπόθεσης: πρόοδος για αξιολόγηση/saves. Η πλήρης
    /// κατάσταση της τρέχουσας υπόθεσης ζει στο AgoraCaseState.</summary>
    [Serializable]
    public sealed class CaseRecord
    {
        public int turn;
        public string caseId;
        public int vote = -1, majority = -1;
        public bool completed;
    }
}
