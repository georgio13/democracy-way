using UnityEngine;
using UnityEngine.InputSystem;

namespace DemocracyWay.Services
{
    /// <summary>
    /// Implemented by the pause menu prefab's root component (lives in the UI
    /// assembly, which Services cannot reference directly).
    /// </summary>
    public interface IPauseMenuPanel
    {
        void Show();
        void Hide();
    }

    /// <summary>
    /// ESC → pause overlay, but ONLY where a scene opted in: the story scene
    /// controller sets <see cref="CanPause"/> true in OnEnable and false in
    /// OnDisable. No scene-name lists — a scene that can pause says so itself.
    /// Pausing freezes time (timeScale 0), pauses voice (AudioListener.pause)
    /// and ducks the music; menus keep working because all UI fades and the
    /// overlay run on unscaled time.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Pause Service")]
    [DisallowMultipleComponent]
    public class PauseService : MonoBehaviour
    {
        [Tooltip("Prefab με το pause menu. Φτιάχνεται στην πρώτη παύση και επαναχρησιμοποιείται.")]
        [SerializeField] private GameObject pauseMenuPrefab;

        /// <summary>Set by the current scene (StorySceneController). While
        /// false — menus, creation, comic — ESC does nothing.</summary>
        public bool CanPause { get; set; }

        public bool IsPaused { get; private set; }

        private IPauseMenuPanel panel;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;
            if (!CanPause) return;

            var flow = ServicesRoot.Flow;
            if (flow != null && flow.IsBusy) return; // never pause mid-transition

            Toggle();
        }

        public void Toggle()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        public void Pause()
        {
            if (IsPaused || !EnsurePanel()) return;

            IsPaused = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;                    // voice pauses; music/SFX ignore it
            ServicesRoot.Audio?.SetPauseDucking(true);
            panel.Show();
        }

        public void Resume()
        {
            if (!IsPaused) return;

            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            ServicesRoot.Audio?.SetPauseDucking(false);
            panel?.Hide();
        }

        /// <summary>Restores time/audio before leaving the scene from the pause
        /// menu (Επιστροφή στην Αρχική) — the scene must not load frozen.</summary>
        public void ForceResume()
        {
            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            ServicesRoot.Audio?.SetPauseDucking(false);
            panel?.Hide();
        }

        private bool EnsurePanel()
        {
            if (panel != null) return true;
            if (pauseMenuPrefab == null)
            {
                Debug.LogError("[PauseService] Δεν έχει συνδεθεί pauseMenuPrefab.", this);
                return false;
            }
            var go = Instantiate(pauseMenuPrefab, transform);
            panel = go.GetComponent<IPauseMenuPanel>();
            if (panel == null)
            {
                Debug.LogError("[PauseService] Το prefab δεν έχει component που υλοποιεί IPauseMenuPanel.", go);
                return false;
            }
            return true;
        }
    }
}
