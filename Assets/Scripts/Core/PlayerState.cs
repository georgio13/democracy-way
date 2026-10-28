using System;

namespace Agora
{
    /// <summary>Οι δείκτες του παίκτη: Πειθώ, Φήμη, Ηθική και δραχμές.</summary>
    [Serializable]
    public class PlayerState
    {
        public string playerName;
        public SocialClass socialClass;
        public int wallet, income;
        public int peitho, fame, ethos;

        // μετρητές για τα τέλη του παίκτη
        public int spokeForA, spokeForB, bribes;
        public int silentStreak;
        public bool sycophantFlag;     // δεν μπορεί να κατηγορήσει ξανά

        public PlayerState() { }   // για JsonUtility (saves)

        public PlayerState(GameConfig cfg, SocialClass cls, string name)
        {
            playerName = name;
            socialClass = cls;
            (wallet, income) = cfg.ClassMoney(cls);
            peitho = cfg.startPeitho; fame = cfg.startFame; ethos = cfg.startEthos;
        }

        public static int Clamp(int v, int max = 100) => v < 0 ? 0 : (v > max ? max : v);
    }
}
