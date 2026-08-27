using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.Dialogue
{
    using DemocracyWay.Core;

    /// <summary>One spoken (or narrated) line inside a dialogue.</summary>
    [Serializable]
    public class DialogueLine
    {
        [Tooltip("Character name. Leave empty for narration.")]
        public string speaker;

        [TextArea(2, 6)]
        public string text;

        public bool IsNarration => string.IsNullOrEmpty(speaker);
    }

    /// <summary>
    /// One option at the end of a dialogue. Picking it applies
    /// <see cref="effects"/> to the run's indicators and shows
    /// <see cref="outcome"/> as a closing beat.
    /// </summary>
    [Serializable]
    public class DialogueChoice
    {
        [TextArea(1, 3)]
        public string text;

        [Tooltip("Indicator changes applied when this option is chosen.")]
        public List<IndicatorDelta> effects = new List<IndicatorDelta>();

        [Tooltip("Shown after the choice is made. Leave empty to close immediately.")]
        [TextArea(2, 5)]
        public string outcome;

        /// <summary>"Ευνομία +5 · Καχυποψία −3" — the summary line under the
        /// outcome text so the player can see what moved.</summary>
        public string EffectsSummary()
        {
            if (effects == null || effects.Count == 0) return string.Empty;
            var parts = new List<string>(effects.Count);
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].delta == 0) continue;
                parts.Add(effects[i].ToString());
            }
            return string.Join("  ·  ", parts);
        }
    }

    /// <summary>A complete dialogue: some lines, then some choices.</summary>
    [Serializable]
    public class DialogueEntry
    {
        [Tooltip("Stable key. Recorded in the save so the picker can prefer unseen dialogues.")]
        public string id;

        [Tooltip("Shown as the dialogue's heading.")]
        public string title;

        public List<DialogueLine> lines = new List<DialogueLine>();

        [Tooltip("Leave empty for a dialogue that just ends after its lines.")]
        public List<DialogueChoice> choices = new List<DialogueChoice>();

        public bool IsValid => lines != null && lines.Count > 0;
    }
}
