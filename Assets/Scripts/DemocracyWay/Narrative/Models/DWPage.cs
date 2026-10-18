using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    [Serializable]
    public sealed class DWPage
    {
        public string id;
        public string title;
        [TextArea(4, 15)]
        public string body;
        [TextArea(2, 8)]
        public string sourceNote;
        public Sprite background;
    }
}
