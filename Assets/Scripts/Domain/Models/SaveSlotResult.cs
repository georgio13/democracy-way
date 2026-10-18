using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    public sealed class SaveSlotResult
    {
        public SaveSlotStatus status;
        public GameState state;
        public string title;
        public string savedAtUtc;
        public bool recoveredBackup;
        public string error;
    }
}
