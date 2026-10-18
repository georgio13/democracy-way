using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    [Serializable]
    public sealed class DWBioStep
    {
        public string id;
        public DWBioKind kind;
        public string year;
        public string title;
        [TextArea(4, 15)]
        public string body;
        [TextArea(2, 8)]
        public string sourceNote;
        public Sprite background;
        public bool editEconomicClass = true;
        public List<DWChoice> choices = new List<DWChoice>();
    }
}
