using UnityEngine;

namespace Agora
{
    /// <summary>
    /// Ένα αξίωμα της Αθηναίων Πολιτείας ως ΣΩΜΑ: «οι δέκα πωληταί», «οι 35 σιτοφύλακες».
    /// Το πλήθος είναι το πραγματικό (κεφ. 42–62). Ένα asset ανά γραμμή του πίνακα «Αξιώματα».
    /// </summary>
    [CreateAssetMenu(menuName = "Agora/Office", fileName = "Office_")]
    public class OfficeData : ScriptableObject
    {
        [Header("Ταυτότητα")]
        public string displayName;          // «Πωληταί»
        [TextArea] public string displayNameWithCount; // «Οι δέκα πωληταί» (όπως εμφανίζεται στην οθόνη)
        public int count = 1;               // πραγματικό πλήθος
        public SelectionMode selection = SelectionMode.Lot;
        public int payObols;                // 62.2 — 0 αν δεν αναφέρεται
        public string athPolRef;            // «47.2–3»

        [Header("Εκπαιδευτικό κείμενο (ΕΚ)")]
        [TextArea(3, 8)] public string description;
    }
}
