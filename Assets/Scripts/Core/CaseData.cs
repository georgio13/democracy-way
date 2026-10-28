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

    [Serializable]
    public struct MetricSensitivity
    {
        public Metric metric;
        public int perTen;      // s: μονάδες S0 ανά 10 μονάδες απόκλισης του δείκτη από το 50
    }

    [Serializable]
    public class OfficialStance
    {
        public OfficialData official;
        public Side side;                       // A, B ή None (= ουδέτερος, πείθεται)
        [Range(0, 15)] public int weight;       // w του σώματος
        [Range(0, 100)] public int threshold;   // T, μόνο για ουδέτερους
    }

    [Serializable]
    public class Speech
    {
        public OfficialData speaker;
        public OfficialData fallbackSpeaker;    // αν ο ομιλητής καταδικαστεί (καταγγελία)
        [TextArea(4, 12)] public string text;
    }

    [Serializable]
    public class ComplaintOption
    {
        public bool available;
        public ComplaintProcedure procedure;
        public OfficialData target;
        public bool guilty;                     // προκαθορισμένο — όχι τυχαίο
        [TextArea(2, 4)] public string charge;
        [TextArea(2, 4)] public string defence;
        public int votesFor;                    // π.χ. 318
        public int votesAgainst;                // π.χ. 183   (501 δικαστές)
        [TextArea(2, 5)] public string verdictText;

        public bool BelowOneFifth => votesFor * 5 < votesFor + votesAgainst;
    }

    /// <summary>
    /// Μία από τις 20 υποθέσεις. Όλο το περιεχόμενο είναι εδώ — τίποτα στον κώδικα.
    /// Η πρόταση Α είναι πάντα η δημοκρατική, η Β η ολιγαρχική/συγκεντρωτική.
    /// </summary>
    [CreateAssetMenu(menuName = "Agora/Case", fileName = "Case_")]
    public class CaseData : ScriptableObject
    {
        [Header("Ταυτότητα")]
        [Range(1, 20)] public int id;
        [Range(1, 10)] public int prytany;
        public string title;
        public string athPolRef;
        public bool inCouncil;              // true = η σκηνή είναι το Βουλευτήριο, αλλιώς η Πνύκα

        [Header("Κείμενα")]
        [TextArea(3, 8)] public string newsPeace;
        [TextArea(3, 8)] public string newsWar;     // κενό = ίδιο με newsPeace
        [TextArea(3, 10)] public string ek1;        // πριν τους λόγους
        [TextArea(2, 4)] public string optionA;     // σύντομη περίληψη της πρότασης Α (για την Αγορά)
        [TextArea(2, 4)] public string optionB;     // σύντομη περίληψη της πρότασης Β (για την Αγορά)
        public Speech speechA;
        public Speech speechB;
        [TextArea(3, 10)] public string ek3;        // μετά το αποτέλεσμα
        public List<Metric> visibleMetrics = new List<Metric>();   // ποιοι δείκτες δείχνονται στην Είδηση

        [Header("Αριθμοί")]
        [Range(0, 100)] public int baseSupport = 50;        // B
        public List<MetricSensitivity> sensitivities = new List<MetricSensitivity>();
        public bool emergencyB;                             // Β «έκτακτης ανάγκης»: +10 σε πόλεμο
        public List<MetricDelta> effectsA = new List<MetricDelta>();
        public List<MetricDelta> effectsB = new List<MetricDelta>();
        public List<OfficialStance> officials = new List<OfficialStance>();

        [Header("Καταγγελία (υποθέσεις 3, 9, 10, 11, 13)")]
        public ComplaintOption complaint = new ComplaintOption();

        [Header("Ειδικοί κανόνες (υποθέσεις 12, 15, 17, 18, 19, 20)")]
        public bool abolishesPayIfB;            // 12
        public bool decidesWarEnd;              // 15: Α = συνέχεια, Β = ειρήνη με φρουρά
        public bool auditFineIfA;               // 17: Τ +6 στην επόμενη πρυτανεία
        public bool areopagusIfB;               // 18: +5 στις Β της πρυτανείας 10
        public bool affectedByAreopagus;        // 19, 20
        public int lowTrustBaseOverride = -1;   // 18: αν Εμ < 40 → βάση 52
        public int lowAnyMetricBaseOverride = -1; // 20: αν κάποιος δείκτης < 30 → βάση 40

        public string NewsFor(bool war) => war && !string.IsNullOrEmpty(newsWar) ? newsWar : newsPeace;
    }
}
