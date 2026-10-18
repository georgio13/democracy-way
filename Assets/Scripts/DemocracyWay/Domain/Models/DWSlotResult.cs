using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    public sealed class DWSlotResult
    {
        public DWSlotStatus status;
        public DWGameState state;
        public string title;
        public string savedAtUtc;
        public bool recoveredBackup;
        public string error;
    }
}
