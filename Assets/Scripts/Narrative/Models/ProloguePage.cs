using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    [Serializable]
    public sealed class ProloguePage
    {
        public string id;
        public string title;
        [TextArea(4, 15)]
        public string body;
    }
}
