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
        public BiographyDeltas metrics = new BiographyDeltas();   // δέλτα πολίτη — η πόλη δεν αγγίζεται από τη βιογραφία
        public int economicClassIndex = -1;
        public int tribeIndex = -1;
        public string profession;
        public string playerName, demeId, trittysId;
        public int requireTribe = -1, requireClass = -1;
        public string requireTrittys;
        public string deme;
        public string trittys;
        [Tooltip("Contextual institutional interpretation: democratic / oligarchic / mixed.")]
        public string institutionalLean = "mixed";
    }
}
