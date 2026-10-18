using System;
using System.IO;
using System.Linq;
using DemocracyWay.NewUI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DemocracyWay.Editor
{
    // Editor-only adapter: the authored project is the source; runtime uses validated baked assets.
    public static class ArticyContentImport
    {
        [Serializable] public sealed class AssetReference { public string propertyPath, guid; public long fileId; }
        [Serializable] public sealed class ContentBundle
        {
            public int schemaVersion;
            public string projectGuid, configJson, campaignJson, cityJson, lexiconJson;
            public AssetReference[] references;
        }
        const string ConfigPath = "Assets/Data/Introduction/GameConfiguration.asset";
        static readonly string[] DataPaths = { "Assets/Data/Gameplay/CampaignContent.json", "Assets/Data/City/CityContent.json", "Assets/Data/Lexicon/Lexicon.json" };
        static string BundlePath => Path.GetFullPath("Narrative/Exports/UnityContentBundle.json");

        [MenuItem("Tools/Democracy Way/Articy/Validate Full Content Export")]
        public static void ValidateExport() { Process(false); }
        [MenuItem("Tools/Democracy Way/Articy/Apply Full Content Export")]
        public static void ApplyExport() { Process(true); }
        public static void Process(bool apply)
        {
            if (!File.Exists(BundlePath)) throw new FileNotFoundException("Export all native content from articy first.", BundlePath);
            var bundle = JsonUtility.FromJson<ContentBundle>(File.ReadAllText(BundlePath));
            var target = AssetDatabase.LoadAssetAtPath<GameConfiguration>(ConfigPath);
            if (target == null) throw new InvalidOperationException("Missing authored configuration.");
            var clone = Prepare(bundle, target);
            try
            {
                if (!apply) { Debug.Log("Full articy content export validated; no game assets changed."); return; }
                var paths = new[] { ConfigPath }.Concat(DataPaths).ToArray();
                var backup = paths.ToDictionary(p => p, File.ReadAllBytes);
                string archive = Path.GetFullPath("Narrative/Migration/Backups/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-ffff"));
                Directory.CreateDirectory(archive);
                foreach (var p in paths) File.WriteAllBytes(Path.Combine(archive, Path.GetFileName(p)), backup[p]);
                try
                {
                    Undo.RecordObject(target, "Apply articy content");
                    string name = target.name;
                    EditorUtility.CopySerialized(clone, target); target.name = name;
                    EditorUtility.SetDirty(target); AssetDatabase.SaveAssets();
                    var content = new[] { bundle.campaignJson, bundle.cityJson, bundle.lexiconJson };
                    for (int i = 0; i < DataPaths.Length; i++) File.WriteAllText(DataPaths[i], content[i], new System.Text.UTF8Encoding(false));
                    AssetDatabase.Refresh();
                    Debug.Log("Applied full native articy export. Previous assets archived at " + archive);
                }
                catch
                {
                    foreach (var p in paths) File.WriteAllBytes(p, backup[p]);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    throw;
                }
            }
            finally { Object.DestroyImmediate(clone); }
        }
        public static GameConfiguration Prepare(ContentBundle bundle, GameConfiguration baseline)
        {
            if (bundle == null || bundle.schemaVersion != 1 || !Guid.TryParse(bundle.projectGuid, out _) || bundle.references == null || string.IsNullOrWhiteSpace(bundle.configJson))
                throw new InvalidOperationException("Invalid native export envelope.");
            var clone = Object.Instantiate(baseline);
            try
            {
                JsonUtility.FromJsonOverwrite(bundle.configJson, clone);
                var so = new SerializedObject(clone);
                foreach (var link in bundle.references)
                {
                    var property = so.FindProperty(link.propertyPath);
                    if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                        throw new InvalidOperationException("Invalid asset reference path: " + link.propertyPath);
                    Object value = null;
                    if (!string.IsNullOrEmpty(link.guid) && link.fileId != 0)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(link.guid);
                        value = AssetDatabase.LoadAllAssetsAtPath(path).FirstOrDefault(asset => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long id) && guid == link.guid && id == link.fileId);
                        if (value == null) throw new InvalidOperationException("Missing referenced asset: " + link.guid + ":" + link.fileId);
                    }
                    property.objectReferenceValue = value;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                if (!clone.Validate(out string error)) throw new InvalidOperationException(error);
                // Existing saves must retain every previously shipped stable node and choice ID.
                foreach (var step in baseline.biography)
                {
                    var next = clone.biography.Find(s => s.id == step.id);
                    if (next == null || step.choices.Any(c => !next.choices.Any(n => n.id == c.id)))
                        throw new InvalidOperationException("Export removes a shipped biography/choice ID: " + step.id);
                }
                if (baseline.prologue.Any(p => !clone.prologue.Any(n => n.id == p.id))) throw new InvalidOperationException("Export removes a shipped prologue ID.");
                var campaign = JsonUtility.FromJson<CampaignContent>(bundle.campaignJson);
                var city = JsonUtility.FromJson<CityContent>(bundle.cityJson);
                if (campaign?.cases == null || campaign.cases.Length == 0 || campaign.cases.Any(c => c == null || string.IsNullOrEmpty(c.id) || c.answers == null || c.answers.Length != 3 || c.correct < 0 || c.correct >= 3)) throw new InvalidOperationException("Invalid campaign export.");
                if (campaign.cases.Select(c => c.id).Distinct().Count() != campaign.cases.Length) throw new InvalidOperationException("Duplicate campaign IDs.");
                if (city?.news == null || city.writers == null || city.complaints == null || city.officials == null) throw new InvalidOperationException("Incomplete city export.");
                if (string.IsNullOrWhiteSpace(bundle.lexiconJson) || !bundle.lexiconJson.Contains("\"gameEntries\"")) throw new InvalidOperationException("Missing reference content export.");
                return clone;
            }
            catch { Object.DestroyImmediate(clone); throw; }
        }
    }
}
