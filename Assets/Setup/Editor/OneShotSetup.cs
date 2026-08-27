#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DemocracyWay.Setup
{
    /// <summary>
    /// The ONE-SHOT scaffolder entry point. Runs once, builds everything the
    /// project needs to be playable (importer settings, Data assets, prefabs,
    /// the five scenes, Build Settings), refuses to overwrite anything that
    /// already exists, and is then deleted together with the whole
    /// Assets/Setup/ folder — nothing at runtime may ever reference it.
    ///
    /// Interactive:  Tools » DemocracyWay » Setup (μία φορά)
    /// Headless:     Unity -batchmode -executeMethod DemocracyWay.Setup.OneShotSetup.Run
    /// </summary>
    public static class OneShotSetup
    {
        [MenuItem("Tools/DemocracyWay/Setup (μία φορά)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Setup] Δεν τρέχει σε Play mode — σταμάτησε το παιχνίδι και ξαναδοκίμασε.");
                return;
            }

            // Only ask a human: in batch mode this dialog would return false
            // and silently skip the entire run.
            if (!Application.isBatchMode)
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

            SetupCommon.ResetRun();

            // ── 0. Folders (the Art/Audio/Fonts folders already exist) ──
            SetupCommon.EnsureFolder(SetupPaths.DataFolder);
            SetupCommon.EnsureFolder(SetupPaths.PrefabFolder);
            SetupCommon.EnsureFolder(SetupPaths.UiPrefabFolder);
            SetupCommon.EnsureFolder(SetupPaths.SceneFolder);

            // ── 1. Importers first: sprites/cursor/wrap-repeat must be right
            //       BEFORE any Data asset or prefab references them. ──
            SetupAssets.ConfigureImporters();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ── 2. Data assets (the dialogue before its chapter, everything
            //       before the GameConfig that points at all of it). ──
            SetupAssets.CreateDataAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ── 3. Prefabs (UiButton first, Systems last — it needs the pause
            //       menu prefab and the GameConfig). ──
            SetupPrefabs.CreateAll();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ── 4. Scenes + Build Settings (Boot first — the entry scene). ──
            SetupScenes.CreateAll();
            SetupScenes.RegisterBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Land on Boot so the very next ▶ press starts the real flow
            // (PlayFromBoot pins play mode there anyway).
            if (System.IO.File.Exists(SetupPaths.BootScene))
                EditorSceneManager.OpenScene(SetupPaths.BootScene);

            SetupCommon.ReportSummary();
        }
    }
}
#endif
