using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.Data
{
    /// <summary>
    /// The intro comic: panels appear one by one, each with its own delay and
    /// sound. The player can skip the whole thing when allowSkip is on.
    /// </summary>
    [CreateAssetMenu(fileName = "ComicSequence", menuName = "DemocracyWay/Comic Sequence")]
    public class ComicSequence : ScriptableObject
    {
        [Serializable]
        public class Panel
        {
            [Tooltip("Η εικόνα του καρέ.")]
            public Sprite image;

            [Tooltip("Κείμενο αφήγησης του καρέ — εμφανίζεται στο κάτω μέρος της οθόνης όταν αποκαλύπτεται το καρέ.")]
            [TextArea(2, 4)]
            public string caption = "";

            [Tooltip("Δευτερόλεπτα αναμονής ΠΡΙΝ εμφανιστεί αυτό το καρέ.")]
            [Range(0f, 10f)]
            public float delayBeforeShow = 1f;

            [Tooltip("Δευτερόλεπτα που διαρκεί το fade-in του καρέ.")]
            [Range(0f, 5f)]
            public float fadeInDuration = 0.6f;

            [Tooltip("Ήχος που παίζει τη στιγμή που εμφανίζεται το καρέ.")]
            public AudioClip sound;
        }

        public List<Panel> panels = new List<Panel>();

        [Tooltip("Αν ενεργό, ο παίκτης μπορεί να παραλείψει το comic.")]
        public bool allowSkip = true;

        [Tooltip("Δευτερόλεπτα αναμονής μετά το τελευταίο καρέ πριν συνεχίσει το παιχνίδι.")]
        [Range(0f, 10f)]
        public float holdAfterLastPanel = 2f;
    }
}
