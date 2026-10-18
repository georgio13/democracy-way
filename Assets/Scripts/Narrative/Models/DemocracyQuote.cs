using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    [Serializable]
    public sealed class DemocracyQuote
    {
        [TextArea(2, 5)]
        public string text;
        public string attribution;
    }
}
