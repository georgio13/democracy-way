using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DemocracyWay.Data
{
    /// <summary>
    /// One selectable option in character creation. The id is the stable key
    /// used by saves, filters and analytics — never change an id after players
    /// have saves that reference it; change only titles, texts and images.
    /// </summary>
    [Serializable]
    public class CreationOption
    {
        [Tooltip("Σταθερό αναγνωριστικό (λατινικά, χωρίς κενά). Μπαίνει στα saves — μην το αλλάξεις εκ των υστέρων.")]
        public string id = "";

        [Tooltip("Το όνομα που βλέπει ο παίκτης στη λίστα.")]
        public string title = "";

        [Tooltip("Η περιγραφή που εμφανίζεται αριστερά όταν ο παίκτης κάνει hover/επιλογή.")]
        [TextArea(3, 8)]
        public string description = "";

        [Tooltip("Η εικόνα που εμφανίζεται αριστερά μαζί με την περιγραφή.")]
        public Sprite image;
    }

    [Serializable]
    public class GenderOption : CreationOption
    {
        [Tooltip("Αν ενεργό, ο δείκτης Καχυποψία υπάρχει σε αυτό το παιχνίδι (προορίζεται για τη Γυνή).")]
        public bool enablesSuspicion;
    }

    [Serializable]
    public class TrittysOption : CreationOption
    {
        [Tooltip("Το id της φυλής στην οποία ανήκει αυτή η τριττύα — φιλτράρει τη λίστα στο βήμα 3.")]
        public string tribeId = "";
    }

    [Serializable]
    public class ProfessionOption : CreationOption
    {
        [Tooltip("Το id της τριττύας στην οποία υπάρχει αυτό το επάγγελμα — φιλτράρει τη λίστα στο βήμα 6.")]
        public string trittysId = "";
    }

    /// <summary>
    /// All character-creation content, owned and edited by the author in the
    /// Inspector. The six steps read straight from these lists; the dependent
    /// steps (Τριττύα από Φυλή, Επάγγελμα από Τριττύα) use the filter methods.
    /// </summary>
    [CreateAssetMenu(fileName = "CreationDatabase", menuName = "DemocracyWay/Creation Database")]
    public class CreationDatabase : ScriptableObject
    {
        [Header("Βήμα 1 — Φύλο")]
        public List<GenderOption> genders = new List<GenderOption>();

        [Header("Βήμα 2 — Φυλή")]
        public List<CreationOption> tribes = new List<CreationOption>();

        [Header("Βήμα 3 — Τριττύα (φιλτράρεται από τη Φυλή)")]
        public List<TrittysOption> trittyes = new List<TrittysOption>();

        [Header("Βήμα 4 — Οικονομική Κατάστασις")]
        public List<CreationOption> wealthClasses = new List<CreationOption>();

        [Header("Βήμα 5 — Περίοδος")]
        public List<CreationOption> periods = new List<CreationOption>();

        [Header("Βήμα 6 — Επάγγελμα (φιλτράρεται από την Τριττύα)")]
        public List<ProfessionOption> professions = new List<ProfessionOption>();

        public List<TrittysOption> TrittyesFor(string tribeId) =>
            trittyes.Where(t => t.tribeId == tribeId).ToList();

        public List<ProfessionOption> ProfessionsFor(string trittysId) =>
            professions.Where(p => p.trittysId == trittysId).ToList();

        public IEnumerable<string> TribeIds => tribes.Select(t => t.id);
    }
}
