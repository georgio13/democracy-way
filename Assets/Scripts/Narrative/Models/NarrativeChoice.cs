using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    [Serializable]
    public sealed class NarrativeChoice
    {
        public string id;
        [TextArea(2, 5)]
        public string text;
        [TextArea(2, 5)]
        public string consequence;
        public CharacterMetrics metrics = new CharacterMetrics();
        public int personalFunds;
        public int cityFunds;
        public int economicClassIndex = -1;
        public int tribeIndex = -1;
        public string profession;
        public string deme;
        public string trittys;
        [Tooltip("Contextual institutional interpretation: democratic / oligarchic / mixed.")]
        public string institutionalLean = "mixed";
        [TextArea(2, 6)]
        public string sourceNote;
    }
}
