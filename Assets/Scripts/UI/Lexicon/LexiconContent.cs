using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    /// <summary>Τα λήμματα του λεξικού ως asset (πρώην Lexicon.json), επεξεργάσιμα από τον Inspector.</summary>
    [CreateAssetMenu(menuName = "Democracy Way/Lexicon Content", fileName = "LexiconContent")]
    public sealed class LexiconContent : ScriptableObject
    {
        public List<LexiconPanel.Entry> entries = new List<LexiconPanel.Entry>();
    }
}
