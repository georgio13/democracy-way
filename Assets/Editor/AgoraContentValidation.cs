using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Agora.EditorTools
{
    /// <summary>
    /// Έλεγχος πληρότητας του περιεχομένου Agora μετά το Generate Content και την επικόλληση
    /// των κειμένων (README βήματα 2–3). Αναφέρει ΟΛΑ τα προβλήματα μαζί, δεν σταματά στο πρώτο.
    /// Δεν αγγίζει κανένα asset — μόνο διαβάζει.
    /// </summary>
    public static class AgoraContentValidation
    {
        [MenuItem("Agora/Validate Content")]
        public static void Validate()
        {
            var errors = new List<string>();
            void Require(bool ok, string message, Object context = null)
            {
                if (ok) return;
                errors.Add(message);
                Debug.LogError("Agora content: " + message, context);
            }

            var content = LoadAll<GameContent>().FirstOrDefault();
            if (content == null)
            {
                Debug.LogError("Agora content: δεν βρέθηκε GameContent asset. Τρέξε πρώτα Agora → Generate Content (once).");
                return;
            }

            Require(content.config != null, "Το GameContent δεν έχει GameConfig.", content);
            // Μία πηγή αλήθειας για τα πλήθη: τα Settings πρέπει να συμφωνούν με το περιεχόμενο.
            var settings = DemocracyWay.NewUI.JsonContent.Settings;
            Require(content.prytanyTribes.Count == settings.prytanyCount
                && content.cases.Count == settings.prytanyCount * settings.casesPerPrytany,
                "Τα πλήθη του GameContent (" + content.cases.Count + " υποθέσεις, " + content.prytanyTribes.Count
                + " πρυτανείες) δεν συμφωνούν με τα Settings (" + settings.prytanyCount + "×" + settings.casesPerPrytany + ").", content);
            Require(content.prytanyTribes.Count == 10 && content.prytanyTribes.All(t => !string.IsNullOrWhiteSpace(t)),
                "Χρειάζονται 10 ονόματα φυλών στο prytanyTribes.", content);
            Require(content.prytanyEpistatai.Count == 10 && content.prytanyEpistatai.All(t => !string.IsNullOrWhiteSpace(t)),
                "Χρειάζονται 10 επιστάτες στο prytanyEpistatai.", content);
            Require(!string.IsNullOrWhiteSpace(content.prologue), "Λείπει ο πρόλογος στο GameContent.", content);
            Require(!string.IsNullOrWhiteSpace(content.warTransition), "Λείπει το warTransition (μετάβαση πρυτανείας 5).", content);
            Require(!string.IsNullOrWhiteSpace(content.whatReallyHappened), "Λείπει το «Τι έγινε πραγματικά».", content);

            Require(content.cases.Count == 20, "Χρειάζονται 20 υποθέσεις στο GameContent, βρέθηκαν " + content.cases.Count + ".", content);
            for (int i = 0; i < content.cases.Count; i++)
            {
                var c = content.cases[i];
                if (c == null) { Require(false, "Κενή θέση υπόθεσης στο index " + i + ".", content); continue; }
                string id = c.name;
                Require(c.id == i + 1, id + ": id " + c.id + ", αναμενόταν " + (i + 1) + " από τη σειρά της λίστας.", c);
                Require(c.prytany == i / 2 + 1, id + ": prytany " + c.prytany + ", αναμενόταν " + (i / 2 + 1) + ".", c);
                Require(!string.IsNullOrWhiteSpace(c.title), id + ": λείπει τίτλος.", c);
                Require(!string.IsNullOrWhiteSpace(c.newsPeace), id + ": λείπει newsPeace.", c);
                Require(!string.IsNullOrWhiteSpace(c.ek1), id + ": λείπει ek1.", c);
                Require(!string.IsNullOrWhiteSpace(c.ek3), id + ": λείπει ek3.", c);
                Require(c.speechA != null && c.speechA.speaker != null && !string.IsNullOrWhiteSpace(c.speechA.text),
                    id + ": λείπει λόγος ή ομιλητής Α.", c);
                Require(c.speechB != null && c.speechB.speaker != null && !string.IsNullOrWhiteSpace(c.speechB.text),
                    id + ": λείπει λόγος ή ομιλητής Β.", c);
                Require(c.effectsA.Count > 0 && c.effectsB.Count > 0, id + ": λείπουν αποτελέσματα Α ή Β.", c);
                Require(c.officials.Count > 0, id + ": καμία θέση αξιωματούχων.", c);
                foreach (var stance in c.officials)
                {
                    if (stance == null || stance.official == null) { Require(false, id + ": θέση αξιωματούχου χωρίς πρόσωπο.", c); continue; }
                    if (stance.side == Side.None && stance.weight > 0)
                        Require(stance.threshold > 0, id + ": ο ουδέτερος " + stance.official.displayName + " δεν έχει κατώφλι T.", c);
                }
                if (c.complaint != null && c.complaint.available)
                {
                    Require(c.complaint.target != null, id + ": καταγγελία χωρίς στόχο.", c);
                    Require(!string.IsNullOrWhiteSpace(c.complaint.charge) && !string.IsNullOrWhiteSpace(c.complaint.defence)
                        && !string.IsNullOrWhiteSpace(c.complaint.verdictText), id + ": ελλιπή κείμενα καταγγελίας.", c);
                    Require(c.complaint.votesFor > 0 && c.complaint.votesAgainst > 0, id + ": λείπουν οι ψήφοι της καταγγελίας.", c);
                    if (c.complaint.votesFor > 0 && c.complaint.votesFor + c.complaint.votesAgainst != 501)
                        Debug.LogWarning("Agora content: " + id + ": ψήφοι καταγγελίας "
                            + c.complaint.votesFor + "+" + c.complaint.votesAgainst + " ≠ 501 δικαστές.", c);
                }
            }

            var offices = LoadAll<OfficeData>();
            var officials = LoadAll<OfficialData>();
            var logographers = LoadAll<LogographerData>();
            if (offices.Count != 35) Debug.LogWarning("Agora content: βρέθηκαν " + offices.Count + " αξιώματα (το README περιγράφει 35).");
            if (officials.Count != 21) Debug.LogWarning("Agora content: βρέθηκαν " + officials.Count + " πρόσωπα (το README περιγράφει 21).");
            Require(content.offices.Count == offices.Count && content.offices.All(x => x != null),
                "Το GameContent.offices (" + content.offices.Count + ") δεν περιέχει όλα τα Office assets (" + offices.Count + ").", content);
            Require(content.officials.Count == officials.Count && content.officials.All(x => x != null),
                "Το GameContent.officials (" + content.officials.Count + ") δεν περιέχει όλα τα Official assets (" + officials.Count + ").", content);
            Require(content.logographers.Count == logographers.Count && content.logographers.All(x => x != null),
                "Το GameContent.logographers (" + content.logographers.Count + ") δεν περιέχει όλους τους λογογράφους (" + logographers.Count + ").", content);

            foreach (var office in offices)
            {
                Require(!string.IsNullOrWhiteSpace(office.displayName) && !string.IsNullOrWhiteSpace(office.displayNameWithCount),
                    office.name + ": λείπει όνομα αξιώματος.", office);
                Require(!string.IsNullOrWhiteSpace(office.description), office.name + ": λείπει η περιγραφή ΕΚ.", office);
                Require(office.count >= 1, office.name + ": μη έγκυρο πλήθος.", office);
            }
            foreach (var person in officials)
            {
                Require(!string.IsNullOrWhiteSpace(person.displayName), person.name + ": λείπει όνομα προσώπου.", person);
                Require(person.office != null, person.name + ": δεν εκπροσωπεί κανένα σώμα.", person);
                Require(!string.IsNullOrWhiteSpace(person.bio), person.name + ": λείπει το bio.", person);
            }
            Require(logographers.Count == 4, "Αναμένονταν 4 λογογράφοι (μόνος + 3), βρέθηκαν " + logographers.Count + ".");
            Require(logographers.Any(l => l.quality == 0 && l.costDrachmas == 0), "Λείπει η επιλογή «μόνος» (L=0, κόστος 0).");
            foreach (var logo in logographers)
                Require(!string.IsNullOrWhiteSpace(logo.displayName), logo.name + ": λείπει όνομα λογογράφου.", logo);

            if (errors.Count == 0)
                Debug.Log("Agora content: όλα εντάξει — " + content.cases.Count + " υποθέσεις, " + officials.Count
                    + " πρόσωπα, " + offices.Count + " αξιώματα, " + logographers.Count + " λογογράφοι.");
            else
                Debug.LogError("Agora content: " + errors.Count + " προβλήματα — δες τα μηνύματα παραπάνω (κλικ σε καθένα για το asset).");
        }

        static List<T> LoadAll<T>() where T : Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(x => x != null).ToList();
    }
}
