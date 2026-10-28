using System;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Οι επιλογές της βιογραφίας ως ΔΕΛΤΑ πάνω στις αρχικές τιμές του πολίτη (GameConfig).
    /// Ένα σύστημα δεικτών: τα ονόματα και η κλίμακα είναι του Agora.PlayerState.
    /// Η Αυθεντία δεν υπάρχει εδώ — είναι υπολογιζόμενη. Η πόλη δεν αγγίζεται από τη βιογραφία.
    /// </summary>
    [Serializable]
    public sealed class BiographyDeltas
    {
        public int peitho, ethos, synepeia, parrhesia, anagnorisimotita;
        public int walletDrachmas;   // σε δραχμές (οι παλιοί οβολοί των επιλογών ÷ 6)

        public void Add(BiographyDeltas other)
        {
            if (other == null) return;
            peitho += other.peitho;
            ethos += other.ethos;
            synepeia += other.synepeia;
            parrhesia += other.parrhesia;
            anagnorisimotita += other.anagnorisimotita;
            walletDrachmas += other.walletDrachmas;
        }

        public BiographyDeltas Copy() => new BiographyDeltas
        {
            peitho = peitho, ethos = ethos, synepeia = synepeia,
            parrhesia = parrhesia, anagnorisimotita = anagnorisimotita, walletDrachmas = walletDrachmas
        };
    }

    /// <summary>
    /// Ο πολίτης όπως θα μπει στην εκστρατεία: αρχικές τιμές GameConfig + δέλτα βιογραφίας + τάξη.
    /// Καθαρή συνάρτηση — ίδια λογική στη βιογραφία, στη σύνοψη και στο InitialiseAgoraCitizen.
    /// </summary>
    public static class CitizenPreviewRules
    {
        public static Agora.PlayerState Preview(Agora.GameConfig cfg, Agora.SocialClass cls, string name, BiographyDeltas deltas)
        {
            var citizen = new Agora.PlayerState(cfg, cls, name);
            if (deltas != null)
            {
                citizen.peitho = Agora.PlayerState.Clamp(citizen.peitho + deltas.peitho);
                // Τελικός σχεδιασμός: ήθος + συνέπεια → Ηθική · αναγνωρισιμότητα + παρρησία → Φήμη.
                citizen.ethos = Agora.PlayerState.Clamp(citizen.ethos + deltas.ethos + deltas.synepeia);
                citizen.fame = Agora.PlayerState.Clamp(citizen.fame + deltas.anagnorisimotita + deltas.parrhesia);
                citizen.synepeia = Agora.PlayerState.Clamp(citizen.synepeia + deltas.synepeia);
                citizen.parrhesia = Agora.PlayerState.Clamp(citizen.parrhesia + deltas.parrhesia);
                citizen.anagnorisimotita = Agora.PlayerState.Clamp(citizen.anagnorisimotita + deltas.anagnorisimotita);
                citizen.wallet = Math.Max(0, citizen.wallet + deltas.walletDrachmas);
            }
            return citizen;
        }
    }
}
