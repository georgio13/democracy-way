using UnityEngine;
using UnityEngine.SceneManagement;

namespace DemocracyWay.Framework
{
    /// <summary>
    /// One-shot loader that lives on the Bootstrap scene. Persistent singletons
    /// (AudioManager, SettingsManager, CursorManager, SceneTransitionController,
    /// InkStoryRunner, PauseMenuController) all run their Awake() first because
    /// Bootstrap is the entry scene; this component then kicks off the transition
    /// into MainMenu so the first fade-from-black is visible and the transition
    /// overlay's starting alpha=1 smoothly clears.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Bootstrap Loader")]
    [DisallowMultipleComponent]
    public class DemocracyWayBootstrapLoader : MonoBehaviour
    {
        [SerializeField] private string firstSceneName = "MainMenu";

        void Start()
        {
            if (SceneTransitionController.Instance != null)
                SceneTransitionController.Instance.LoadSceneWithTransition(firstSceneName, string.Empty);
            else
                SceneManager.LoadScene(firstSceneName);
        }
    }
}
