using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    [Serializable]
    public sealed class CaseRecord
    {
        public int turn, knowledge = -1, speech = -1, vote = -1, majority = -1, influence;
        public bool completed;
        public int cityDelta, personalDelta;
        public string caseId;
    }
}
