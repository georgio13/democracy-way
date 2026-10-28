using System.Collections.Generic;
using UnityEngine;

namespace Agora
{
    /// <summary>
    /// Ο κατάλογος όλου του περιεχομένου. Ένα asset, συνδεδεμένο στον GameSession.
    /// Αν αλλάξει η σειρά των υποθέσεων, αλλάζει εδώ — όχι στον κώδικα.
    /// </summary>
    [CreateAssetMenu(menuName = "Agora/Game Content", fileName = "GameContent")]
    public class GameContent : ScriptableObject
    {
        public GameConfig config;

        [Tooltip("20 υποθέσεις, με τη σειρά που παίζονται (2 ανά πρυτανεία).")]
        public List<CaseData> cases = new List<CaseData>();

        [Tooltip("Οι 10 φυλές με τη σειρά των πρυτανειών.")]
        public List<string> prytanyTribes = new List<string>
        {
            "Ερεχθηίς", "Αιγηίς", "Πανδιονίς", "Λεοντίς", "Ακαμαντίς",
            "Οινηίς", "Κεκροπίς", "Ιπποθωντίς", "Αιαντίς", "Αντιοχίς"
        };

        [Tooltip("Ο επιστάτης των πρυτάνεων κάθε πρυτανείας (10 ονόματα).")]
        public List<string> prytanyEpistatai = new List<string>();

        public List<OfficeData> offices = new List<OfficeData>();
        public List<OfficialData> officials = new List<OfficialData>();
        public List<LogographerData> logographers = new List<LogographerData>();   // 4 επιλογές: μόνος + 3

        [TextArea(3, 10)] public string prologue;
        [TextArea(3, 10)] public string warTransition;   // μετάβαση πρυτανείας 5
        [TextArea(3, 10)] public string whatReallyHappened;   // «Τι έγινε πραγματικά, Σεπτέμβριος 322»

        [Header("Κείμενα τελών (επίλογος — συμπληρώνονται από τον συγγραφέα)")]
        [TextArea(2, 8)] public string endingFamine, endingPropertyLimit, endingStasis, endingCounterfactual, endingGarrisonOnly;
        [TextArea(2, 8)] public string endingHypereides, endingDemades, endingHarpalusMan, endingLycurgus, endingAnonymous, endingStratocles;

        public CaseData CaseAt(int prytany, int indexInPrytany) => cases[(prytany - 1) * 2 + indexInPrytany];
    }
}
