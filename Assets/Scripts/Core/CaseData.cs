using System;
using System.Collections.Generic;
using UnityEngine;

namespace Agora
{
    [Serializable]
    public struct MetricDelta
    {
        public Metric metric;
        public int amount;      // ήδη ΚΛΙΜΑΚΩΜΕΝΟ (οι τιμές της καρτέλας «Υποθέσεις»)
    }

    /// <summary>Η θέση ενός αξιωματούχου στην υπόθεση. Όσοι είναι υπέρ Α ή Β είναι ήδη μέσα στη Βάση.</summary>
    [Serializable]
    public class OfficialStance
    {
        public OfficialData official;
        public Side side;                       // A, B ή None (= ο αναποφάσιστος)
    }

    [Serializable]
    public class Speech
    {
        public OfficialData speaker;
        [TextArea(4, 12)] public string text;
    }

    /// <summary>Η δίκη της εβδομάδας (υποθέσεις 3, 9, 10, 11, 13). Γίνεται πάντα· ο παίκτης διαλέγει αν θα κατηγορήσει.</summary>
    [Serializable]
    public class TrialData
    {
        public bool exists;
        public TrialVenue venue;
        public ComplaintProcedure procedure;
        public OfficialData defendant;
        [Range(0, 100)] public int evidence;
        public Side convictionFavors;               // η πλευρά που κερδίζει τους πόντους της δίκης αν καταδικαστεί
        public OfficialData replacementSpeaker;     // μιλά αντί για τον κατηγορούμενο αν καταδικαστεί
        public string defaultAccuser;               // ποιος κατηγορεί αν δεν κατηγορήσει ο παίκτης
        [TextArea(2, 4)] public string marketClue;  // «Αγορά: ένα στοιχείο για τη δίκη» (προαιρετικό)
        [TextArea(2, 5)] public string charge;
        [TextArea(2, 5)] public string defence;
        [TextArea(2, 5)] public string verdictGuilty;
        [TextArea(2, 5)] public string verdictAcquitted;
    }

    /// <summary>
    /// Μία από τις 20 υποθέσεις. Όλο το περιεχόμενο είναι εδώ — τίποτα στον κώδικα.
    /// Η πρόταση Α είναι πάντα η δημοκρατική, η Β η ολιγαρχική ή «της ανάγκης».
    /// </summary>
    [CreateAssetMenu(menuName = "Agora/Case", fileName = "Case_")]
    public class CaseData : ScriptableObject
    {
        [Header("Ταυτότητα")]
        [Range(1, 20)] public int id;
        [Range(1, 10)] public int prytany;
        public string title;
        public string athPolRef;
        public CaseVenue venue;

        [Header("Κείμενα")]
        [TextArea(3, 8)] public string newsPeace;
        [TextArea(3, 8)] public string newsWar;     // κενό = ίδιο με newsPeace
        [TextArea(3, 10)] public string ek1;        // πριν τους λόγους
        [TextArea(2, 4)] public string optionA;     // σύντομη περίληψη της πρότασης Α (για την Αγορά)
        [TextArea(2, 4)] public string optionB;     // σύντομη περίληψη της πρότασης Β (για την Αγορά)
        [TextArea(2, 5)] public string argumentA;   // το κεντρικό επιχείρημα του λόγου του παίκτη υπέρ της Α
        [TextArea(2, 5)] public string argumentB;   // το κεντρικό επιχείρημα του λόγου του παίκτη υπέρ της Β
        public Speech speechA;
        public Speech speechB;
        [TextArea(3, 10)] public string ek3;        // μετά το αποτέλεσμα
        public List<Metric> visibleMetrics = new List<Metric>();   // ποιοι δείκτες δείχνονται στην Είδηση

        [Header("Ψηφοφορία")]
        [Range(0, 100)] public int baseSupport = 50;
        public Metric pressureMetric;
        public Side pressureFavors;                         // None = η υπόθεση δεν έχει πίεση
        public bool pressureAnyMetric;                      // υπ. 20: ο χαμηλότερος από τους 4 ορατούς δείκτες
        public bool emergencyB;                             // υπόθεση «έκτακτης ανάγκης»: −10 όσο διαρκεί ο πόλεμος
        public List<OfficialStance> officials = new List<OfficialStance>();
        public OfficialData undecided;                      // ο ένας αναποφάσιστος που δέχεται κίνηση
        public Difficulty difficulty;

        [Header("Επιπτώσεις")]
        public List<MetricDelta> effectsA = new List<MetricDelta>();
        public List<MetricDelta> effectsB = new List<MetricDelta>();

        [Header("Δίκη της εβδομάδας")]
        public TrialData trial = new TrialData();

        [Header("Ειδικοί κανόνες (υποθέσεις 12, 15, 17, 18, 19, 20)")]
        public bool abolishesPayIfB;            // 12Β: σταματά ο μισθός Εκκλησίας
        public bool decidesWarEnd;              // 15: Α = συνέχεια, Β = ειρήνη με φρουρά
        public bool auditFineIfA;               // 17Α: Ταμείο +6 την επόμενη πρυτανεία
        public bool areopagusIfB;               // 18Β: −5 στις 19 και 20
        public bool affectedByAreopagus;        // 19, 20

        public string NewsFor(bool war) => war && !string.IsNullOrEmpty(newsWar) ? newsWar : newsPeace;
    }
}
