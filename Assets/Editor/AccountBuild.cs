using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Static Web asset copying only; never generates or edits scenes.
public sealed class AccountBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    static readonly string[] Files = { "account.js" };
    public int callbackOrder => 120;
    static string Source(string file) => Path.Combine(Application.dataPath, "WebGLTemplates/DemocracyWayLoader", file);
    public void OnPreprocessBuild(BuildReport report) { if (report.summary.platform != BuildTarget.WebGL) return; foreach (var file in Files) if (!File.Exists(Source(file))) throw new BuildFailedException("Missing account UI asset: " + file); }
    public void OnPostprocessBuild(BuildReport report) { if (report.summary.platform != BuildTarget.WebGL) return; string target = Path.Combine(Path.GetFullPath(report.summary.outputPath), "LoaderAssets"); Directory.CreateDirectory(target); foreach (var file in Files) File.Copy(Source(file), Path.Combine(target, file), true); }
}
