using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.UI
{
    /// <summary>One comic frame: a picture and the caption under it.</summary>
    [Serializable]
    public class ComicPanel
    {
        public Sprite artwork;

        [TextArea(2, 5)]
        public string caption;
    }

    /// <summary>
    /// An ordered set of comic frames, revealed one at a time. Seeded by
    /// <c>Tools ▸ DemocracyWay ▸ Init</c>; edit the asset to change the intro.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ComicSequence",
        menuName = "DemocracyWay/Comic Sequence")]
    public class ComicSequence : ScriptableObject
    {
        [Tooltip("Shown above the frames while they appear.")]
        public string title = "Η αρχή";

        public List<ComicPanel> panels = new List<ComicPanel>();

        [Header("Timing")]
        [Tooltip("Seconds each frame takes to fade in.")]
        [Min(0.05f)] public float fadeDuration = 0.7f;

        [Tooltip("Seconds to wait after one frame finishes before the next starts.")]
        [Min(0f)] public float gapBetweenPanels = 0.55f;
    }
}
