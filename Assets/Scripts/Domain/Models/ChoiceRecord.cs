using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    [Serializable]
    public sealed class ChoiceRecord
    {
        public string stepId;
        public string choiceId;
        public BiographyDeltas metrics;    // δέλτα πολίτη — ένα σύστημα δεικτών (METRICS_UNIFY)
        public string institutionalLean;
        public int economicClassIndex = -1;
        public int tribeIndex = -1;
        public string profession;
        public string playerName, demeId, trittysId;
        public string deme;
        public string trittys;
    }
}
