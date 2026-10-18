using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    public static class GameSession
    {
        public static GameState State;
        public static int Slot = -1;
        public static string PendingScreen;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            State = null;
            Slot = -1;
            PendingScreen = null;
        }
    }
}
