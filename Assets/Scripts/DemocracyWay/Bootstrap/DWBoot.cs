using UnityEngine;
using UnityEngine.SceneManagement;

namespace DemocracyWay.NewUI
{
    [DisallowMultipleComponent]
    public sealed class DWBoot : MonoBehaviour
    {
        public string firstScene = "MainMenu";
        private void Start()
        {
            SceneManager.LoadScene(firstScene);
        }
    }
}
