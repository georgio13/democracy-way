using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    [Serializable]
    internal sealed class DWEnvelope
    {
        public string format = "DemocracyWay.ApprovedUI";
        public int version = 1;
        public int slot;
        public string title;
        public string savedAtUtc;
        public string payloadJson;
        public string checksum;
    }
}
