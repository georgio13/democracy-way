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
        public CharacterMetrics metrics;
        public int personalFunds;
        public int cityFunds;
        public string institutionalLean;
        public int economicClassIndex = -1;
        public int tribeIndex = -1;
        public string profession;
        public string playerName, demeId, trittysId;
        public CityMetricDelta cityMetrics = new CityMetricDelta();
        public string deme;
        public string trittys;
    }
}
