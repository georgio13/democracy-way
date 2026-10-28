using System;

namespace Agora
{
    /// <summary>Οι δείκτες του παίκτη. Η Αυθεντία ΥΠΟΛΟΓΙΖΕΤΑΙ, δεν αποθηκεύεται.</summary>
    [Serializable]
    public class PlayerState
    {
        public string playerName;
        public SocialClass socialClass;
        public int wallet, income;
        public int peitho, ethos, synepeia, parrhesia, anagnorisimotita;
        public int fame;               // Φήμη (τελικός σχεδιασμός)

        // ιστορικό για Αυθεντία και προφίλ
        public int speeches, speechesVindicated, spokeForA, spokeForB;
        public int bribes, exposures, complaintsFiled, complaintsWon;
        public int silentStreak;
        public Side lastVote = Side.None;
        public bool sycophantFlag;     // δεν μπορεί να καταγγείλει ξανά

        public PlayerState() { }   // για JsonUtility (saves)

        public PlayerState(GameConfig cfg, SocialClass cls, string name)
        {
            playerName = name;
            socialClass = cls;
            (wallet, income) = cfg.ClassMoney(cls);
            peitho = cfg.startPeitho; ethos = cfg.startEthos; synepeia = cfg.startSynepeia;
            parrhesia = cfg.startParrhesia; anagnorisimotita = cfg.startAnagnorisimotita;
            fame = cfg.startFame;
        }

        /// <summary>Αυθεντία = Συνέπεια × (ποσοστό λόγων που δικαιώθηκαν).</summary>
        public int Authority => speeches == 0 ? 0 : (int)Math.Round(synepeia * (double)speechesVindicated / speeches);

        public static int Clamp(int v, int max = 100) => v < 0 ? 0 : (v > max ? max : v);
    }
}
