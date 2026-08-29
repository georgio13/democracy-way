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
        [Tooltip("Η ζώνη τριττύος του επαγγέλματος (asty, paralia ή mesogeia) — μαζί με την τάξη φιλτράρει τη λίστα στο βήμα 6.")]
        public string zoneId = "";

        [Tooltip("Το id της οικονομικής τάξης στην οποία ανήκει το επάγγελμα — μαζί με τη ζώνη φιλτράρει τη λίστα στο βήμα 6.")]
        public string wealthId = "";
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

        [Header("Βήμα 3 — Τριττύα")]
        public List<TrittysOption> trittyes = new List<TrittysOption>();

        [Header("Βήμα 4 — Οικονομική Κατάσταση")]
        public List<CreationOption> wealthClasses = new List<CreationOption>();

        [Header("Βήμα 5 — Περίοδος")]
        public List<CreationOption> periods = new List<CreationOption>();

        [Header("Βήμα 6 — Επάγγελμα")]
        public List<ProfessionOption> professions = new List<ProfessionOption>();

        public List<TrittysOption> TrittyesFor(string tribeId) =>
            trittyes.Where(t => t.tribeId == tribeId).ToList();

        /// <summary>
        /// Professions are authored per trittys ZONE and wealth class, not per
        /// individual trittys. The zone is the last segment of the trittys id
        /// (e.g. "erechtheis_asty" → "asty") — a naming convention the trittys
        /// list follows for all ten tribes.
        /// </summary>
        public List<ProfessionOption> ProfessionsFor(string trittysId, string wealthId)
        {
            int cut = trittysId != null ? trittysId.LastIndexOf('_') : -1;
            string zone = cut >= 0 ? trittysId.Substring(cut + 1) : trittysId;
            return professions.Where(p => p.zoneId == zone && p.wealthId == wealthId).ToList();
        }

        public IEnumerable<string> TribeIds => tribes.Select(t => t.id);
    }
}
