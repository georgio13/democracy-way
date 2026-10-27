using UnityEngine;

namespace Agora
{
    /// <summary>
    /// Ένα πρόσωπο της καρτέλας «Πρόσωπα». Εκπροσωπεί ένα σώμα (OfficeData) όταν αυτό μιλά.
    /// </summary>
    [CreateAssetMenu(menuName = "Agora/Official", fileName = "Official_")]
    public class OfficialData : ScriptableObject
    {
        public string displayName;      // «Φωκίων»
        public string demotic;          // «Ποταμεύς»
        public OfficeData office;       // το σώμα που εκπροσωπεί
        public bool isHistorical = true;
        public bool bribable;           // εξαγοράσιμος
        [Range(0, 100)] public int tendency = 50;   // 0 = σταθερά ολιγαρχικός, 100 = σταθερά δημοκράτης (για το προφίλ, όχι για τον υπολογισμό)
        [TextArea(2, 6)] public string bio;         // 2–3 προτάσεις, εμφανίζεται με κλικ στο όνομα
        public Sprite portrait;         // προαιρετικό, τοποθετείται από τον Inspector

        public string FullName => string.IsNullOrEmpty(demotic) ? displayName : $"{displayName} {demotic}";
    }
}
