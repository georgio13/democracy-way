using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace DemocracyWay.Editor
{
    // Applies only a complete matching publication. Failed validation leaves current game assets intact.
    [InitializeOnLoad] public static class ArticyPublication
    {
        [Serializable] sealed class Publication { public int schemaVersion; public string exportedUtc,bundleSha256,unitySha256; }
        static string attempted;
        static double next;
        static ArticyPublication(){EditorApplication.update+=Poll;}
        static string Hash(string path)
        {
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();
        }
        [MenuItem("Tools/Democracy Way/Articy/Apply Published Narrative")]
        public static void Apply(){attempted=null;Process(true);}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            next=EditorApplication.timeSinceStartup+2;
            Process(false);
        }
        static void Process(bool force)
        {
            const string publication="Narrative/Exports/Publication.json",applied="Narrative/Exports/AppliedPublication.json",bundle="Narrative/Exports/UnityContentBundle.json",unity="Assets/ArticyContent/Game.articyu3d";
            if(!File.Exists(publication)||!File.Exists(bundle)||!File.Exists(unity))return;
            string json=File.ReadAllText(publication);
            if(!force&&(attempted==json||File.Exists(applied)&&File.ReadAllText(applied)==json))return;
            attempted=json;
            try
            {
                var p=JsonUtility.FromJson<Publication>(json);
                if(p==null||p.schemaVersion!=1||p.bundleSha256!=Hash(bundle)||p.unitySha256!=Hash(unity))throw new InvalidDataException("Incomplete articy publication; export again from Publish narrative to Unity.");
                ArticyContentImport.Process(true);
                File.WriteAllText(applied,json);
                Debug.Log("Native narrative publication applied: "+p.exportedUtc);
            }
            catch(Exception e){Debug.LogError("Native narrative publication was not applied: "+e.Message);if(force)throw;}
        }
    }
}
