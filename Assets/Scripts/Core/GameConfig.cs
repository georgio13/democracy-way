using UnityEngine;

namespace Agora
{
    /// <summary>
    /// Όλες οι ρυθμιζόμενες σταθερές του παιχνιδιού σε ένα asset.
    /// Οι προεπιλογές είναι οι τιμές του Agora_FINAL_DESIGN (ενότητες 2 και 3).
    /// </summary>
    [CreateAssetMenu(menuName = "Agora/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Πόλη")]
        public int cityStart = 50;
        public int warStartsAtPrytany = 5;

        [Header("Μεταβολή ανά πρυτανεία (ειρήνη / πόλεμος)")]
        public int peaceTreasury = 3, peaceFood = 2, peaceReadiness = -3;
        public int warTreasury = -3, warFood = -4, warReadiness = -1;

        [Header("Ειδικοί κανόνες")]
        public float garrisonDemosPowerLossMultiplier = 1.5f;   // μετά την 15Β
        public int auditFineTreasury = 6;                        // μετά την 17Α

        [Header("Κανόνας ψηφοφορίας συνέλευσης (S ≥ passThreshold → περνά η Α)")]
        public int passThreshold = 50;
        public int pressureBelow = 35;              // ο δείκτης πίεσης κάτω από αυτό → Πίεση
        public int pressurePoints = 5;
        public int warEmergencyPoints = 10;         // −, υποθέσεις έκτακτης ανάγκης σε πόλεμο
        public int areopagusPoints = 5;             // −, υπ. 19–20 αν στην 18 πέρασε η Β
        public int trialPoints = 4;                 // ± από την ετυμηγορία της δίκης της εβδομάδας
        public int officialPoints = 4;              // ± ο αναποφάσιστος που κέρδισε ο παίκτης
        public int speechPeithoDivisor = 10;        // λόγος = Πειθώ ÷ 10 (ακέραιο)
        public int speechPeithoMax = 70;            // ανώτατη Πειθώ
        public int logographerPoints = 3;           // + στον λόγο για την πλευρά που παραγγέλθηκε
        public int moodRange = 10;                  // διάθεση του πλήθους ∈ [−moodRange, +moodRange]
        public int leanTowardAFrom = 56;            // «Πού γέρνει η πόλη»: γνωστό άθροισμα από εδώ και πάνω → προς Α
        public int leanTowardBUpTo = 44;            // από εδώ και κάτω → προς Β· ενδιάμεσα αμφίρροπο

        [Header("Κανόνας δίκης (D ≥ guiltyThreshold → ένοχος)")]
        public int guiltyThreshold = 50;
        public int sycophantBelow = 20;             // κατήγορος με D κάτω από αυτό → συκοφάντης
        public int evidenceStrongFrom = 55, evidenceDoubtfulFrom = 35;

        [Header("Παίκτης — αρχικές τιμές")]
        public int startPeitho = 30, startFame = 10, startEthos = 60;
        public int wallet_Thes = 60, income_Thes = 30;
        public int wallet_Zeug = 150, income_Zeug = 60;
        public int wallet_Hipp = 300, income_Hipp = 120;
        public int wallet_Pent = 600, income_Pent = 250;
        public int assemblyPayPerPrytany = 10;      // μηδενίζεται μετά την 12Β

        [Header("Παίκτης — μεταβολές")]
        public int peithoPerSpeech = 2;             // έως speechPeithoMax
        public int fameSpeech = 2, fameWin = 2, fameConviction = 5, fameAcquittal = -3, fameSecondSilence = -2;
        public int silencesBeforeLoss = 2;          // συνεχόμενες σιωπές πριν πέσει η Φήμη
        public int ethosConviction = 3, ethosVoteAgainstSpeech = -5;

        [Header("Κινήσεις")]
        public int difficultyEasy = 30, difficultyMedium = 45, difficultyHard = 60;   // Πειθώ που χρειάζεται ο αναποφάσιστος
        public int logographerCost = 40;
        public int bribeCost = 30;
        public int bribeEthosLoss = 10;
        public int bribeExposureOneIn = 3, bribeExposureFameLoss = 5, bribeExposureTrustLoss = 3;
        public int accuserFine = 50;                // ο συκοφάντης

        [Header("Τέλη πόλης")]
        public int famineFood = 15, famineReadiness = 15;
        public int coupDemosPower = 25;
        public int stasisTrust = 35;
        public int enduresMinAll = 25;
        public int demosPowerMajority = 50;         // ο δήμος «κρατά»: ΙΔ ≥ αυτού

        [Header("Τέλη παίκτη")]
        public int sideSpeeches = 8;                // Υπερείδης/Δημάδης: λόγοι για μία πλευρά
        public int harpalusBribes = 3;              // ο άνθρωπος του Αρπάλου
        public int knownFame = 40;                  // Λυκούργος/Στρατοκλής: η πόλη σε ξέρει
        public int trustedEthos = 50;               // Λυκούργος: και σε εμπιστεύεται

        public int PersuadeThreshold(Difficulty d) => d switch
        {
            Difficulty.Easy => difficultyEasy,
            Difficulty.Medium => difficultyMedium,
            _ => difficultyHard,
        };

        public (int wallet, int income) ClassMoney(SocialClass c) => c switch
        {
            SocialClass.Thes => (wallet_Thes, income_Thes),
            SocialClass.Zeugites => (wallet_Zeug, income_Zeug),
            SocialClass.Hippeus => (wallet_Hipp, income_Hipp),
            _ => (wallet_Pent, income_Pent),
        };
    }
}
