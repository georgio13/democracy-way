#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

namespace DemocracyWay.EditorTools
{
    /// <summary>
    /// Play always enters through Bootstrap, whatever scene is open — the
    /// persistent singletons live only there, so starting anywhere else gives
    /// a half-dead game with no error to point at it.
    ///
    /// Delete this file (or the assignment) to get plain Play back; that also
    /// costs you the Bootstrap→MainMenu route, so Play lands in the open scene.
    /// </summary>
    [InitializeOnLoad]
    public static class DemocracyWayPlayFromBootstrap
    {
        static DemocracyWayPlayFromBootstrap()
        {
            // playModeStartScene isn't serialized — it resets on every domain
            // reload, which is exactly when [InitializeOnLoad] fires. delayCall
            // waits for the AssetDatabase; loading during the static ctor can
            // return null on a fresh import. A null assignment is harmless: it
            // just means "start from the open scene".
            EditorApplication.delayCall += () =>
                EditorSceneManager.playModeStartScene =
                    AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Bootstrap.unity");
        }
    }
}
#endif
