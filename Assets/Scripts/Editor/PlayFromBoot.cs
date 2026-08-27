#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

namespace DemocracyWay.EditorTools
{
    /// <summary>
    /// ▶ Play always enters through the Boot scene, whatever scene is open —
    /// the persistent services live only there, so starting anywhere else
    /// gives a half-dead game with no error pointing at the cause.
    /// Delete this file to get plain Play back.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromBoot
    {
        static PlayFromBoot()
        {
            // playModeStartScene is not serialized — it resets on every domain
            // reload, which is exactly when [InitializeOnLoad] fires. delayCall
            // waits for the AssetDatabase; a null assignment (scene missing,
            // Setup not run yet) harmlessly means "start from the open scene".
            EditorApplication.delayCall += () =>
                EditorSceneManager.playModeStartScene =
                    AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Boot.unity");
        }
    }
}
#endif
