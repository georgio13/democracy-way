using System;

namespace DemocracyWay.NewUI
{
    [Serializable]
    public sealed class CivicOfficesCatalogue
    {
        public string scope, period;
        public CivicOfficeEntry[] offices;
    }

    [Serializable]
    public sealed class CivicOfficeEntry
    {
        public string id, name, countStatus, selection, role, primaryReference, category, institution, parentOfficeId, notes;
        public int count;
    }
}
