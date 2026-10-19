using UnityEngine;
using UnityEngine.SceneManagement;

namespace DemocracyWay.NewUI
{
    [DisallowMultipleComponent]
    public sealed class Bootstrap : MonoBehaviour
    {
        public string firstScene = "Login";
        private void Start()
        {
            SceneManager.LoadScene(firstScene);
        }
    }
}
