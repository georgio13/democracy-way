using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    [Serializable]
    public sealed class BiographyStep
    {
        public string id;
        public string selectionKind;
        public BiographyStepKind kind;
        public string year;
        public string title;
        [TextArea(4, 15)]
        public string body;
        public string backgroundId;
        [NonSerialized] public Sprite background;
        public bool editEconomicClass = true;
        public List<NarrativeChoice> choices = new List<NarrativeChoice>();
    }
}
