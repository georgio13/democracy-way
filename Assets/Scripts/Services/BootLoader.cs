using UnityEngine;
using UnityEngine.SceneManagement;

namespace DemocracyWay.Services
{
    /// <summary>
    /// The only scene object in Boot besides the Systems prefab. By the time
    /// its Start() runs, every service Awake() has finished — it then sends
    /// the game to the main menu through the (already black) overlay, so the
    /// first thing the player ever sees is the menu fading in.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Boot Loader")]
    [DisallowMultipleComponent]
    public class BootLoader : MonoBehaviour
    {
        [SerializeField] private string firstSceneName = "MainMenu";

        void Start()
        {
            if (ServicesRoot.Flow != null)
                ServicesRoot.Flow.GoToScene(firstSceneName);
            else
                SceneManager.LoadScene(firstSceneName); // never strand the player on black
        }
    }
}
