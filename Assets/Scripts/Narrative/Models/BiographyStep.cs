using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    [Serializable]
    public sealed class BiographyStep
    {
        public string id;
        public BiographyStepKind kind;
        public string year;
        public string title;
        [TextArea(4, 15)]
        public string body;
        [TextArea(2, 8)]
        public string sourceNote;
        public Sprite background;
        public bool editEconomicClass = true;
        public List<NarrativeChoice> choices = new List<NarrativeChoice>();
    }
}
