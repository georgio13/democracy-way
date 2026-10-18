using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    [Serializable]
    public sealed class DWCampaignState
    {
        public List<DWCaseRecord> cases = new List<DWCaseRecord>();
        public int[] initialAnswers = new int[8]
        {
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1
        };
        public int[] finalAnswers = new int[8]
        {
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1
        };
        public int initialIndex, finalIndex;
        public int[] survey = new int[2]
        {
            -1,
            -1
        };
        public bool initialComplete, finalComplete, surveyComplete;
        public int war, readiness, legitimacy;
        public string cityEnding, personalEnding;
    }
}
