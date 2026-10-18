using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Replaces the existing file at this same path; do not keep a second copy.
public sealed class CopyWebLoaderAssets : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    private static readonly string[] Sources =
    {
        "Art/UI/Logo.png",
        "Fonts/IFKargoSans/IFKargoSans-Regular.ttf",
        "Art/UI/Cursor.png",
        "WebGLTemplates/DemocracyWayLoader/research.js"
    };
    public int callbackOrder => 100;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL)
            return;
        foreach (string relative in Sources)
            Require(relative);
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL)
            return;
        string target = Path.Combine(Path.GetFullPath(report.summary.outputPath), "LoaderAssets");
        Directory.CreateDirectory(target);
        foreach (string relative in Sources)
            File.Copy(Require(relative), Path.Combine(target, Path.GetFileName(relative)), true);
        Debug.Log("[Democracy Way] Logo, IFKargoSans and cursor copied to Web build LoaderAssets.");
    }

    private static string Require(string relative)
    {
        string full = Path.Combine(Application.dataPath, relative);
        if (!File.Exists(full))
            throw new BuildFailedException("Missing Web loader source: Assets/" + relative);
        return full;
    }
}
