using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using DemocracyWay.NewUI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DemocracyWay.Editor
{
    // Editor-only baking keeps the existing save/rule/UI contract and WebGL runtime independent
    // of vendor assemblies. No flow instructions or metric side effects run during this import.
    public static class DWArticyAuthoring
    {
        public const string ImporterUrl = "https://www.articy.com/en/downloads/unity/";
        [MenuItem("Tools/Democracy Way/Articy/Open Official Importer Download")]
        private static void OpenDownload()
        {
            Application.OpenURL(ImporterUrl);
        }

        [MenuItem("Tools/Democracy Way/Articy/Create Biography Import Map")]
        private static void CreateMap()
        {
            DWConfig config = Selection.activeObject as DWConfig;
            if (config == null)
                throw new InvalidOperationException("Select the authored DWConfig asset first.");
            string path = EditorUtility.SaveFilePanelInProject("Articy map", "ArticyImportMap", "asset", "Save the import map.");
            if (string.IsNullOrEmpty(path))
                return;
            var map = ScriptableObject.CreateInstance<DWArticyImportMap>();
            map.target = config;
            for (int i = 0; i < config.prologue.Count; i++)
            {
                string source = "Prologue_" + config.prologue[i].id.Replace('-', '_');
                Add(map, source, "DisplayName", "prologue.Array.data[" + i + "].title");
                Add(map, source, "Text", "prologue.Array.data[" + i + "].body");
            }

            string[] metrics =
            {
                "consistency",
                "ethics",
                "authority",
                "persuasion",
                "parrhesia",
                "recognition"
            };
            for (int i = 0; i < config.biography.Count; i++)
            {
                var step = config.biography[i];
                string prefix = "biography.Array.data[" + i + "]";
                string source = "Bio_" + step.id.Replace('-', '_');
                Add(map, source, "DisplayName", prefix + ".title");
                Add(map, source, "Text", prefix + ".body");
                for (int j = 0; j < step.choices.Count; j++)
                {
                    string choice = source + "_" + step.choices[j].id.Replace('-', '_');
                    string target = prefix + ".choices.Array.data[" + j + "]";
                    Add(map, choice, "Text", target + ".text");
                    foreach (string metric in metrics)
                        Add(map, choice, "Metrics." + char.ToUpperInvariant(metric[0]) + metric.Substring(1), target + ".metrics." + metric);
                }
            }

            AssetDatabase.CreateAsset(map, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = map;
        }

        private static void Add(DWArticyImportMap map, string name, string source, string target)
        {
            map.fields.Add(new DWArticyFieldBinding { technicalName = name, sourceProperty = source, targetProperty = target });
        }

        [MenuItem("Tools/Democracy Way/Articy/Validate Selected Import Map")]
        private static void ValidateSelected()
        {
            ProcessSelected(false);
        }

        [MenuItem("Tools/Democracy Way/Articy/Bake Selected Import Map")]
        private static void BakeSelected()
        {
            ProcessSelected(true);
        }

        private static void ProcessSelected(bool apply)
        {
            var map = Selection.activeObject as DWArticyImportMap;
            if (map == null)
                throw new InvalidOperationException("Select an ArticyImportMap asset.");
            Type database = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Articy.Unity.ArticyDatabase", false)).FirstOrDefault(t => t != null);
            MethodInfo get = database?.GetMethod("GetObject", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (get == null)
                throw new InvalidOperationException("Install the official articy:draft X Unity importer and export your articy project to Unity first.");
            Bake(map, name => get.Invoke(null, new object[] { name }), apply);
            Debug.Log(apply ? "Articy content baked. Existing choice IDs and save format retained." : "Articy map validated; no assets changed.");
        }

        public static void Bake(DWArticyImportMap map, Func<string, object> resolve, bool apply)
        {
            if (map == null || map.target == null || resolve == null || map.fields == null || map.fields.Count == 0)
                throw new ArgumentException("An authored target, resolver and at least one binding are required.");
            var clone = Object.Instantiate(map.target);
            try
            {
                var serialized = new SerializedObject(clone);
                var destinations = new HashSet<string>(StringComparer.Ordinal);
                foreach (var field in map.fields)
                {
                    if (field == null || string.IsNullOrWhiteSpace(field.technicalName) || !destinations.Add(field.targetProperty ?? ""))
                        throw new InvalidOperationException("Empty source or duplicate target binding.");
                    // IDs, topology, references and asset identity cannot change through a text/metric bake.
                    if (!AllowedTarget(field.targetProperty))
                        throw new InvalidOperationException("Unsupported authoring target: " + field.targetProperty);
                    SerializedProperty target = serialized.FindProperty(field.targetProperty);
                    if (target == null)
                        throw new InvalidOperationException("Missing target: " + field.targetProperty);
                    object source = resolve(field.technicalName);
                    if (source == null)
                        throw new InvalidOperationException("Missing Articy object: " + field.technicalName);
                    object value = ReadProperty(source, field.sourceProperty);
                    if (target.propertyType == SerializedPropertyType.String)
                        target.stringValue = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                    else if (target.propertyType == SerializedPropertyType.Integer)
                        target.intValue = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                    else
                        throw new InvalidOperationException("Only text and integer metrics are supported.");
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (!clone.Validate(out string error))
                    throw new InvalidOperationException(error);
                if (!apply)
                    return;
                Undo.RecordObject(map.target, "Bake Articy narrative content");
                string assetName = map.target.name;
                HideFlags flags = map.target.hideFlags;
                EditorUtility.CopySerialized(clone, map.target);
                map.target.name = assetName;
                map.target.hideFlags = flags;
                EditorUtility.SetDirty(map.target);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
        }

        private static bool AllowedTarget(string path)
        {
            if (string.IsNullOrEmpty(path) || (!path.StartsWith("prologue.Array.data[", StringComparison.Ordinal) && !path.StartsWith("biography.Array.data[", StringComparison.Ordinal)))
                return false;
            string[] text =
            {
                ".title",
                ".body",
                ".sourceNote",
                ".text",
                ".consequence",
                ".year"
            };
            if (text.Any(path.EndsWith))
                return true;
            return path.Contains(".choices.Array.data[") && path.Contains(".metrics.") && new[]
            {
                "consistency",
                "ethics",
                "authority",
                "persuasion",
                "parrhesia",
                "recognition"
            }.Any(m => path.EndsWith(".metrics." + m, StringComparison.Ordinal));
        }

        private static object ReadProperty(object value, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Missing source property path.");
            foreach (string part in path.Split('.'))
            {
                if (value == null)
                    throw new InvalidOperationException("Null Articy property: " + path);
                var property = value.GetType().GetProperty(part, BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                    throw new InvalidOperationException("Missing Articy property: " + path);
                value = property.GetValue(value, null);
            }

            if (value == null)
                throw new InvalidOperationException("Null Articy value: " + path);
            return value;
        }
    }
}
