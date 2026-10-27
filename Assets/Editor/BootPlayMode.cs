using UnityEditor;
using UnityEditor.SceneManagement;
namespace DemocracyWay.NewUI.Editor {
    // Play always enters the same authentication gateway as a player build.
    [InitializeOnLoad] public static class BootPlayMode {
        static BootPlayMode() { EditorApplication.delayCall += () => {
            var boot = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Boot.unity");
            if (boot != null && !EditorApplication.isPlayingOrWillChangePlaymode) EditorSceneManager.playModeStartScene = boot;
        }; }
    }
}
