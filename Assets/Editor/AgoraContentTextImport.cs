using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Agora.EditorTools
{
    /// <summary>
    /// Μίας χρήσης: διαβάζει το Content_texts.json (warTransition + 20 υποθέσεις) και γεμίζει
    /// τα αντίστοιχα πεδία ΚΕΙΜΕΝΟΥ στα CaseData assets και στο GameContent.
    /// Αριθμούς, θέσεις αξιωματούχων και αναφορές δεν τα αγγίζει. Οι αλλαγές γίνονται με Undo.
    /// </summary>
    public static class AgoraContentTextImport
    {
        [Serializable]
        sealed class CaseTexts
        {
            public int id;
            public string title, athPolRef, newsPeace, newsWar, ek1, speakerA, speechA, speakerB, speechB, ek3;
            public string optionA, optionB, argumentA, argumentB;
            public string complaintCharge, complaintDefence, complaintVerdict;
        }

        [Serializable]
        sealed class Document
        {
            public string warTransition;
            public List<CaseTexts> cases;
        }

        [MenuItem("Agora/Import Case Texts…")]
        public static void Import()
        {
            string path = EditorUtility.OpenFilePanel("Διάλεξε το Content_texts.json", "", "json");
            if (string.IsNullOrEmpty(path)) return;
            Run(path);
        }

        /// <summary>Για batch mode: Unity.exe -batchmode -quit -executeMethod ... ImportHeadless -agoraTexts "path".</summary>
        public static void ImportHeadless()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-agoraTexts");
            if (index < 0 || index + 1 >= args.Length)
            {
                Debug.LogError("Agora import: λείπει το όρισμα -agoraTexts <path>.");
                EditorApplication.Exit(1);
                return;
            }
            bool ok = Run(args[index + 1]);
            if (ok) AgoraContentValidation.Validate();   // το log δείχνει και τι απομένει
            EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool Run(string path)
        {
            var document = JsonUtility.FromJson<Document>(File.ReadAllText(path));
            if (document == null || document.cases == null || document.cases.Count == 0)
            {
                Debug.LogError("Agora import: δεν διαβάστηκαν υποθέσεις από το " + path);
                return false;
            }

            var content = AssetDatabase.FindAssets("t:GameContent")
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameContent>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(x => x != null);
            if (content == null)
            {
                Debug.LogError("Agora import: δεν βρέθηκε GameContent. Τρέξε πρώτα Agora → Generate Content (once).");
                return false;
            }

            int updated = 0, warnings = 0;
            if (!string.IsNullOrWhiteSpace(document.warTransition))
            {
                Undo.RecordObject(content, "Import Agora texts");
                content.warTransition = document.warTransition.Trim();
                EditorUtility.SetDirty(content);
            }

            foreach (var texts in document.cases)
            {
                var c = content.cases.FirstOrDefault(x => x != null && x.id == texts.id);
                if (c == null)
                {
                    Debug.LogWarning("Agora import: δεν υπάρχει υπόθεση με id " + texts.id + " στο GameContent.", content);
                    warnings++;
                    continue;
                }
                Undo.RecordObject(c, "Import Agora texts");
                c.title = Keep(texts.title, c.title);
                c.athPolRef = Keep(texts.athPolRef, c.athPolRef);
                c.newsPeace = Keep(texts.newsPeace, c.newsPeace);
                if (texts.newsWar != null) c.newsWar = texts.newsWar.Trim();   // κενό = «ίδιο με ειρήνη», επιτρεπτό
                c.ek1 = Keep(texts.ek1, c.ek1);
                c.ek3 = Keep(texts.ek3, c.ek3);
                c.optionA = Keep(texts.optionA, c.optionA);
                c.optionB = Keep(texts.optionB, c.optionB);
                c.argumentA = Keep(texts.argumentA, c.argumentA);
                c.argumentB = Keep(texts.argumentB, c.argumentB);
                if (c.speechA != null) c.speechA.text = Keep(texts.speechA, c.speechA.text);
                if (c.speechB != null) c.speechB.text = Keep(texts.speechB, c.speechB.text);
                warnings += CheckSpeaker(c, "Α", c.speechA, texts.speakerA);
                warnings += CheckSpeaker(c, "Β", c.speechB, texts.speakerB);
                if (c.complaint != null && c.complaint.available)
                {
                    c.complaint.charge = Keep(texts.complaintCharge, c.complaint.charge);
                    c.complaint.defence = Keep(texts.complaintDefence, c.complaint.defence);
                    c.complaint.verdictText = Keep(texts.complaintVerdict, c.complaint.verdictText);
                }
                else if (!string.IsNullOrWhiteSpace(texts.complaintCharge))
                {
                    Debug.LogWarning("Agora import: " + c.name + ": το έγγραφο έχει κείμενα καταγγελίας αλλά το asset δεν την έχει διαθέσιμη (complaint.available).", c);
                    warnings++;
                }
                EditorUtility.SetDirty(c);
                updated++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Agora import: ενημερώθηκαν " + updated + " υποθέσεις"
                + (warnings > 0 ? ", " + warnings + " προειδοποιήσεις — δες παραπάνω." : ".")
                + " Τρέξε Agora → Validate Content για επανέλεγχο.");
            return true;
        }

        // Κενό πεδίο στο έγγραφο δεν σβήνει ποτέ υπάρχον κείμενο στο asset.
        static string Keep(string incoming, string current) => string.IsNullOrWhiteSpace(incoming) ? current : incoming.Trim();

        static int CheckSpeaker(CaseData c, string side, Speech speech, string documented)
        {
            if (speech == null || speech.speaker == null || string.IsNullOrWhiteSpace(documented)
                || string.IsNullOrEmpty(speech.speaker.displayName)) return 0;
            string name = documented.Split(',')[0].Trim();
            if (name.StartsWith(speech.speaker.displayName, StringComparison.Ordinal)) return 0;
            Debug.LogWarning("Agora import: " + c.name + ": ο ομιλητής " + side + " στο έγγραφο είναι «" + name
                + "», στο asset «" + speech.speaker.FullName + "». Δεν άλλαξα την αναφορά — έλεγξέ το.", c);
            return 1;
        }
    }
}
