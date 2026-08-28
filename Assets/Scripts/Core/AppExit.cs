using UnityEngine;

namespace DemocracyWay.Core
{
    /// <summary>
    /// Application.Quit() is deliberately a no-op inside the Unity editor, so
    /// «Έξοδος» would look broken while testing. This quits the built game
    /// and stops Play mode in the editor — same button, honest behaviour.
    /// </summary>
    public static class AppExit
    {
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
