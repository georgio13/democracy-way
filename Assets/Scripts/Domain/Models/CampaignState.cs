using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    [Serializable]
    public sealed class CampaignState
    {
        public List<CaseRecord> cases = new List<CaseRecord>();
        public int[] survey = new int[2]
        {
            -1,
            -1
        };
        public int war, readiness, legitimacy;
        public int foodSecurity = 50;
        public string cityEnding, personalEnding;
    }
}
