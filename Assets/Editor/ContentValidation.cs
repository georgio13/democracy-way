using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace DemocracyWay.NewUI.Editor {
    public sealed class ContentValidation : IPreprocessBuildWithReport {
        [Serializable] sealed class StudyManifest {public string campaignFile,instrumentFile,instrumentVersion,contentVersion;}
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) { Validate(); }
        static void Require(bool ok, string error) { if (!ok) throw new BuildFailedException("Content JSON: " + error); }
        [MenuItem("Tools/Content/Validate JSON")]
        public static void Validate() {
            JsonContent.Reset();
            var s=JsonContent.Settings; var plan=JsonContent.Plan; var campaign=JsonContent.Campaign; var e=JsonContent.Evaluation;
            Require(s.casesPerPrytany==2 && s.prytanyCount==10, "Expected 10 prytanies with 2 cases each.");
            Require(plan.prytanies.Length==s.prytanyCount, "Campaign.prytanies count.");
            Require(plan.availableCaseIds.Distinct().Count()==plan.availableCaseIds.Length, "Duplicate availableCaseIds.");
            foreach(var chapter in plan.prytanies) Require(chapter.caseIds.Length==s.casesPerPrytany && chapter.caseIds.All(id=>plan.availableCaseIds.Contains(id)), "Invalid case schedule: "+chapter.id);
            foreach(var c in campaign.cases) {
                Require(!string.IsNullOrWhiteSpace(c.id) && c.arguments!=null, "Missing case identity/arguments.");
                foreach(int side in new[]{0,1}) Require(c.arguments.Any(a=>a.side==side) && c.arguments.Where(a=>a.side==side).All(a=>a.fragments!=null&&a.fragments.Length>0&&a.fragments.All(f=>!string.IsNullOrWhiteSpace(f))), "Missing dialogue side in "+c.id);
                if(c.politics!=null) Require(c.politics.approaches!=null && c.politics.approaches.Length>0, "Missing speech approaches in "+c.id);
            }
            var tribes=JsonContent.Load<TribesFile>("Tribes").tribes;
            Require(tribes.Length==10 && tribes.All(t=>t.trittyes.Length==3), "Expected 10 tribes and 30 trittyes.");
            var demes=tribes.SelectMany(t=>t.trittyes).SelectMany(t=>t.demes).ToArray();
            Require(demes.Length>=130 && demes.Select(d=>d.id).Distinct().Count()==demes.Length, "Invalid or duplicate deme IDs.");
            foreach(var bank in new[]{e.onboarding,e.postTest}) {
                Require(bank.Length>0 && bank.Select(q=>q.id).Distinct().Count()==bank.Length,"Missing/duplicate knowledge IDs.");
                foreach(var q in bank) Require(q.answers!=null && q.answers.Length==4 && q.correctIndex>=0 && q.correctIndex<4,"Each knowledge question needs 4 answers and correctIndex 0..3: "+q.id);
            }
            Require(e.tendencies.Length>0 && e.tendencyRatingLabels.Length==5 && e.tendencies.Select(q=>q.id).Distinct().Count()==e.tendencies.Length,"Invalid tendency questionnaire.");
            Require(e.demographics!=null && e.demographics.questions!=null && e.demographics.questions.Length>0,"Missing demographics.");
            foreach(var q in e.demographics.questions) Require(!string.IsNullOrWhiteSpace(q.id) && (q.kind=="text" || q.options!=null&&q.options.Length>0),"Invalid demographic question: "+q.id);
            Require(e.experience.Length>0 && e.ratingLabels.Length==5,"Invalid MEEGA+ questionnaire.");
            var media=AssetDatabase.LoadAssetAtPath<GameConfiguration>("Assets/Data/Introduction/GameConfiguration.asset");
            Require(media!=null,"Missing media configuration.");
            var config=JsonContent.Configuration(media); UnityEngine.Object.DestroyImmediate(config);
            // Freeze the exact runtime instrument text: browser and exporter hash the same bytes.
            var instrument=JsonUtility.ToJson(e);
            using(var sha=System.Security.Cryptography.SHA256.Create()) {
                string hash=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(instrument))).Replace("-", "").ToLowerInvariant().Substring(0,32);
                string directory=System.IO.Path.GetFullPath("Tools/Research/Instruments");System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllText(System.IO.Path.Combine(directory,e.instrumentVersion+"-"+hash+".json"),instrument,new System.Text.UTF8Encoding(false));
                string bank=JsonUtility.ToJson(campaign);
                string bankHash=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(bank))).Replace("-", "").ToLowerInvariant().Substring(0,32);
                string bankDirectory=System.IO.Path.GetFullPath("Tools/Research/CaseBanks");System.IO.Directory.CreateDirectory(bankDirectory);
                string bankFile=campaign.contentVersion+"-"+bankHash+".json";
                string instrumentFile=e.instrumentVersion+"-"+hash+".json";
                System.IO.File.WriteAllText(System.IO.Path.Combine(bankDirectory,bankFile),bank,new System.Text.UTF8Encoding(false));
                var manifest=new StudyManifest {campaignFile="CaseBanks/"+bankFile,instrumentFile="Instruments/"+instrumentFile,instrumentVersion=e.instrumentVersion,contentVersion=campaign.contentVersion};
                System.IO.File.WriteAllText("Tools/Research/StudyManifest.json",JsonUtility.ToJson(manifest,true),new System.Text.UTF8Encoding(false));
            }
            Debug.Log("JSON validation passed: 10 prytanies / 20 cases, "+demes.Length+" demes, separate knowledge/tendencies/MEEGA+.");
        }
    }
}
