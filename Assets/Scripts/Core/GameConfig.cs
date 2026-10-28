using UnityEngine;

namespace Agora
{
    /// <summary>
    /// Όλες οι ρυθμιζόμενες σταθερές του game loop σε ένα asset.
    /// Οι προεπιλογές είναι οι τιμές της δεύτερης προσομοίωσης (27–28/10).
    /// </summary>
    [CreateAssetMenu(menuName = "Agora/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Πόλη")]
        public int cityStart = 50;
        public int warStartsAtPrytany = 5;
        public int emergencyBonus = 10;             // Β έκτακτης ανάγκης σε πόλεμο
        public float reaction = 4f;                  // −react·(ID−50)/10
        public int trustSensitivityCap = 2;          // s(Εμ) ≤ 2
        public float noise = 4f;                     // ε ∈ U(−noise, +noise)
        public int passThreshold = 50;
        public float contestedBand = 12f;            // |S0−passThreshold| ≤ band → αμφίρροπη υπόθεση
        public int s0Min = 5, s0Max = 95;            // όρια του S0
        public int lowTrustThreshold = 40;           // ενεργοποιεί το lowTrustBaseOverride (υπόθεση 18)
        public int lowAnyMetricThreshold = 30;       // ενεργοποιεί το lowAnyMetricBaseOverride (υπόθεση 20)

        [Header("Βάση ανά πρυτανεία (ειρήνη / πόλεμος)")]
        public int peaceTreasury = 3, peaceFood = 2, peaceReadiness = -3;
        public int warTreasury = -3, warFood = -4, warReadiness = -1;

        [Header("Κλιμάκωση αποτελεσμάτων (αν τα effects δίνονται ΑΚΛΙΜΑΚΩΤΑ· 1 = ήδη κλιμακωμένα)")]
        public float scaleDemosPowerA = 1f, scaleDemosPowerB = 1f, scaleTrustA = 1f, scaleTrustB = 1f;
        public float garrisonDemosPowerLossMultiplier = 1.5f;   // μετά την 15Β
        public int areopagusBonusB = 5;                          // μετά την 18Β, πρυτανεία 10
        public int auditFineTreasury = 6;                        // μετά την 17Α

        [Header("Παίκτης — αρχικές τιμές")]
        public int startPeitho = 30, startEthos = 60, startSynepeia = 50, startParrhesia = 40, startAnagnorisimotita = 10;
        public int wallet_Thes = 60, income_Thes = 30;
        public int wallet_Zeug = 150, income_Zeug = 60;
        public int wallet_Hipp = 300, income_Hipp = 120;
        public int wallet_Pent = 600, income_Pent = 250;
        public int assemblyPayPerPrytany = 10;      // μηδενίζεται μετά την 12Β

        [Header("Παίκτης — ομιλία")]
        public float speechCap = 12f;
        public int peithoPerSpeech = 2, peithoCap = 80;
        public int anagnPerSpeech = 2, anagnPerWin = 2, anagnLossAfterTwoSilences = 2;
        public int synepeiaVoteMatch = 2, synepeiaVoteMismatch = -8, synepeiaSameDirection = 1;
        public int parrhesiaAgainstMajority = 4;
        public int silencesBeforeLoss = 2;          // σιωπές στη σειρά πριν πέσει η Αναγνωρισιμότητα

        [Header("Παίκτης — δωροδοκία")]
        public int bribeCostPerWeight = 6;          // δραχμές ανά μονάδα βάρους
        public int bribeEthosLoss = 10;
        public int exposureAnagnGain = 10, exposureTrustLoss = 5;

        [Header("Παίκτης — καταγγελία")]
        public int complaintWinAnagn = 4, complaintWinParrhesia = 4, complaintWinEthos = 3;
        public int complaintWinTrust = 2, complaintWinTreasury = 2;
        public int complaintLoseAnagn = -3, complaintParrhesiaAlways = 2;
        public int complaintAcquittedWeightBonus = 5;
        public int maxOfficialWeight = 15;          // ανώτατο βάρος σώματος
        public int sycophantFine = 60;              // = 1.000 δραχμές στην κλίμακα του παιχνιδιού

        [Header("Τέλη πόλης")]
        public int famineFood = 15, famineReadiness = 15;
        public int coupDemosPower = 25;
        public int stasisTrust = 35;
        public int enduresMinAll = 25;
        public int demosPowerMajority = 50;         // ο δήμος «κρατά»: ΙΔ ≥ αυτού

        [Header("Προσωπικά τέλη")]
        public int personalFactionSpeeches = 10;    // Υπερείδης/Δημάδης: λόγοι υπέρ της πλευράς
        public int personalAnagnHypereides = 50;
        public int personalAnagnHarpalus = 70, personalEthosHarpalusMax = 40;
        public int personalAnagnLycurgus = 60, personalEthosLycurgusMin = 50, personalSynepeiaLycurgusMin = 50;
        public int personalAnagnAnonymousMax = 30;
        public int personalEthosStratoclesMax = 50;

        public (int wallet, int income) ClassMoney(SocialClass c) => c switch
        {
            SocialClass.Thes => (wallet_Thes, income_Thes),
            SocialClass.Zeugites => (wallet_Zeug, income_Zeug),
            SocialClass.Hippeus => (wallet_Hipp, income_Hipp),
            _ => (wallet_Pent, income_Pent),
        };
    }
}
