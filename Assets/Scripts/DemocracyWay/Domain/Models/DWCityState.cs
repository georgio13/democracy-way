using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    [Serializable]
    public sealed class DWCityState
    {
        public List<int> readNews = new List<int>();
        public int speechWriter = -1;
        public int speechBonus;
        public int speechUses;
        public string[] complaintDecisions = new string[3];
        public int[] officialApproaches = new int[6];
        public int[] officialSupport = new int[6];
        public int corruptionRisk;
    }
}
