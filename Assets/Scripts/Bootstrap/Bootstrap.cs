using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DemocracyWay.NewUI
{
    [DisallowMultipleComponent]
    public sealed class Bootstrap : MonoBehaviour
    {
        private IEnumerator Start()
        {
            // Wait until Web session restoration has either finished or failed.
            // IsReady alone cannot distinguish a guest from an account still restoring.
            float deadline = Time.realtimeSinceStartup + 30f;
            while (!AccountBridge.AuthenticationResolved && Time.realtimeSinceStartup < deadline) yield return null;
            string scene = AccountBridge.IsAuthenticated && AccountBridge.IsReady ? "MainMenu" : "Login";
            yield return SceneManager.LoadSceneAsync(scene);
        }
    }
}
