using UnityEngine;
using UnityEngine.SceneManagement;

namespace DemocracyWay.NewUI
{
    [DisallowMultipleComponent]
    public sealed class Bootstrap : MonoBehaviour
    {
        public string firstScene = "MainMenu";
        private void Start()
        {
            SceneManager.LoadScene(firstScene);
        }
    }
}
