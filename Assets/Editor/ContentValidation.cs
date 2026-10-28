using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace DemocracyWay.NewUI.Editor {
    /// <summary>Έλεγχος του περιεχομένου που χρησιμοποιεί το ενιαίο σύστημα (βιογραφία, όργανα έρευνας)
    /// και πάγωμα των ακριβών bytes των οργάνων για την αναπαραγωγιμότητα της μελέτης.</summary>
    public sealed class ContentValidation : IPreprocessBuildWithReport {
        [Serializable] sealed class StudyManifest {public string campaignFile,instrumentFile,instrumentVersion,contentVersion;}
        [Serializable] sealed class CaseBank {public string[] cases;}
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) { Validate(); }
        static void Require(bool ok, string error) { if (!ok) throw new BuildFailedException("Content: " + error); }
        [MenuItem("Tools/Content/Validate Content")]
        public static void Validate() {
            JsonContent.Reset();
            var s=JsonContent.Settings; var e=JsonContent.Evaluation;
            Require(s.casesPerPrytany==2 && s.prytanyCount==10, "Expected 10 prytanies with 2 cases each.");
            var tribes=JsonContent.Tribes;
            Require(tribes.Length==10 && tribes.All(t=>t.trittyes.Length==3), "Expected 10 tribes and 30 trittyes.");
            var demes=tribes.SelectMany(t=>t.trittyes).SelectMany(t=>t.demes).ToArray();
            Require(demes.Length>=130 && demes.Select(d=>d.id).Distinct().Count()==demes.Length, "Invalid or duplicate deme IDs.");
            foreach(var bank in new[]{e.onboarding,e.postTest}) {
                Require(bank.Length>0 && bank.Select(q=>q.id).Distinct().Count()==bank.Length,"Missing/duplicate knowledge IDs.");
                foreach(var q in bank) Require(q.answers!=null && q.answers.Length==4 && q.answers.All(a=>!string.IsNullOrWhiteSpace(a.id)) && q.answers.Select(a=>a.id).Distinct().Count()==q.answers.Length && q.answers.Count(a=>a.id==q.correctId)==1,"Each knowledge question needs 4 answers with unique ids and a correctId matching one of them: "+q.id);
            }
            Require(e.tendencies.Length>0 && e.tendencyRatingLabels.Length==5 && e.tendencies.Select(q=>q.id).Distinct().Count()==e.tendencies.Length,"Invalid tendency questionnaire.");
            Require(e.participation.Length>0 && e.participationRatingLabels.Length==5 && e.participation.Select(q=>q.id).Distinct().Count()==e.participation.Length,"Invalid participation questionnaire.");
            Require(e.demographics!=null && e.demographics.questions!=null && e.demographics.questions.Length>0,"Missing demographics.");
            foreach(var q in e.demographics.questions) Require(!string.IsNullOrWhiteSpace(q.id) && (q.kind=="text" || q.options!=null&&q.options.Length>0),"Invalid demographic question: "+q.id);
            Require(e.experience.Length>0 && e.ratingLabels.Length==5,"Invalid MEEGA+ questionnaire.");
            var media=AssetDatabase.LoadAssetAtPath<GameConfiguration>("Assets/Content/GameConfiguration.asset");
            Require(media!=null,"Missing media configuration.");
            Require(media.agoraContent!=null && media.agoraContent.cases.Count==s.prytanyCount*s.casesPerPrytany,"GameConfiguration must reference the GameContent with all cases.");
            var config=JsonContent.Configuration(media); UnityEngine.Object.DestroyImmediate(config);
            // Freeze the exact runtime instrument text: browser and exporter hash the same bytes.
            var instrument=JsonUtility.ToJson(e);
            using(var sha=System.Security.Cryptography.SHA256.Create()) {
                string hash=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(instrument))).Replace("-", "").ToLowerInvariant().Substring(0,32);
                string directory=System.IO.Path.GetFullPath("Tools/Research/Instruments");System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllText(System.IO.Path.Combine(directory,e.instrumentVersion+"-"+hash+".json"),instrument,new System.Text.UTF8Encoding(false));
                // Το «case bank» του ενιαίου συστήματος: τα 20 CaseData assets σειριαλοποιημένα, για το ίδιο hash-trail.
                string bank=JsonUtility.ToJson(new CaseBank{cases=media.agoraContent.cases.Select(JsonUtility.ToJson).ToArray()});
                string bankHash=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(bank))).Replace("-", "").ToLowerInvariant().Substring(0,32);
                string bankDirectory=System.IO.Path.GetFullPath("Tools/Research/CaseBanks");System.IO.Directory.CreateDirectory(bankDirectory);
                string bankFile="agora-"+s.contentVersion+"-"+bankHash+".json";
                string instrumentFile=e.instrumentVersion+"-"+hash+".json";
                System.IO.File.WriteAllText(System.IO.Path.Combine(bankDirectory,bankFile),bank,new System.Text.UTF8Encoding(false));
                var manifest=new StudyManifest {campaignFile="CaseBanks/"+bankFile,instrumentFile="Instruments/"+instrumentFile,instrumentVersion=e.instrumentVersion,contentVersion="agora-"+s.contentVersion};
                System.IO.File.WriteAllText("Tools/Research/StudyManifest.json",JsonUtility.ToJson(manifest,true),new System.Text.UTF8Encoding(false));
            }
            Debug.Log("Content validation passed: ενιαίο σύστημα, "+demes.Length+" demes, όργανα γνώσεων/τάσεων/MEEGA+ παγωμένα.");
        }
    }
}
