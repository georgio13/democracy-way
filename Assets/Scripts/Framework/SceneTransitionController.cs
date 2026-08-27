using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DemocracyWay.Framework
{
    /// <summary>
    /// Persistent singleton that owns the full-screen black transition overlay and the
    /// chapter title text. Used for all story-scene loads and main-menu↔story transitions.
    ///
    /// Timeline:
    ///   fadeInDuration  → black fades in over current scene
    ///   holdDuration    → chapter name visible while SceneManager.LoadSceneAsync runs
    ///   fadeOutDuration → black fades out revealing the new scene
    /// </summary>
    [AddComponentMenu("DemocracyWay/Scene Transition Controller")]
    [DisallowMultipleComponent]
    public class SceneTransitionController : MonoBehaviour
    {
        public static SceneTransitionController Instance { get; private set; }

        [Header("Overlay References")]
        [SerializeField] private Canvas overlayCanvas;
        [SerializeField] private CanvasGroup overlayGroup;
        [SerializeField] private Image blackImage;
        [SerializeField] private TMP_Text chapterText;

        [Header("Timing")]
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float holdDuration = 1.5f;
        [SerializeField] private float fadeOutDuration = 1.5f;

        public bool IsTransitioning { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent != null) transform.SetParent(null, false);
            DontDestroyOnLoad(gameObject);

            // NOTE: Do NOT reset overlayGroup.alpha here. Init
            // serializes it to 1 (fully black) so the Bootstrap→MainMenu
            // fade-out looks correct. The transition coroutines manage alpha
            // from this point on. Resetting to 0 in Awake would flash the
            // raw scene for one frame before the first transition starts.
            if (overlayGroup != null)
            {
                overlayGroup.blocksRaycasts = overlayGroup.alpha > 0f;
                overlayGroup.interactable = overlayGroup.alpha > 0f;
            }
            if (overlayCanvas != null) overlayCanvas.sortingOrder = 9999; // above everything
            if (chapterText != null) chapterText.text = string.Empty;
        }

        /// <summary>
        /// Update the chapter title text on the overlay. Called by the
        /// destination <see cref="DemocracyWay.Story.SceneController"/> when it
        /// wakes up during a running transition, so the title shown to the
        /// player matches the scene being faded into (not the source scene).
        /// </summary>
        public void SetChapterText(string title)
        {
            if (chapterText != null)
                chapterText.text = title ?? string.Empty;
        }

        /// <summary>
        /// Fade to black, show chapter name, load the target scene, then fade out.
        /// </summary>
        public void LoadSceneWithTransition(string sceneName, string chapterTitle)
        {
            if (IsTransitioning) return;
            StartCoroutine(TransitionRoutine(sceneName, chapterTitle));
        }

        /// <summary>
        /// Fade to black with chapter title, without loading a new scene. Used on first
        /// entry to a story scene that was loaded directly (e.g. from main menu).
        /// </summary>
        public void FadeOutFromBlack(string chapterTitle, System.Action onFadeOutComplete = null)
        {
            StartCoroutine(FadeFromBlackRoutine(chapterTitle, onFadeOutComplete));
        }

        private IEnumerator TransitionRoutine(string sceneName, string chapterTitle)
        {
            IsTransitioning = true;

            // Show the source scene's chapter title (if provided) during fade-in.
            // For most scene-to-scene transitions this is empty — the destination
            // SceneController will set it after the scene loads.
            if (chapterText != null) chapterText.text = chapterTitle ?? string.Empty;
            if (overlayGroup != null)
            {
                overlayGroup.blocksRaycasts = true;
                overlayGroup.interactable = true;
            }

            // Fade to black
            yield return Fade(0f, 1f, fadeInDuration);

            // Load the next scene in the background
            var op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;
            while (op.progress < 0.9f) yield return null;

            // Activate the new scene (still hidden behind black overlay)
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            // Give the destination SceneController one frame to call
            // SetChapterText() with its own chapterTitle. This ensures the
            // title shown during the hold matches the scene being faded into
            // — identical to the first scene's behaviour.
            yield return null;

            // Hold with the (now-correct) chapter title visible
            yield return new WaitForSecondsRealtime(holdDuration);

            // Fade out of black revealing the new scene
            yield return Fade(1f, 0f, fadeOutDuration);

            if (chapterText != null) chapterText.text = string.Empty;
            if (overlayGroup != null)
            {
                overlayGroup.blocksRaycasts = false;
                overlayGroup.interactable = false;
            }

            IsTransitioning = false;
        }

        private IEnumerator FadeFromBlackRoutine(string chapterTitle, System.Action onComplete)
        {
            IsTransitioning = true;
            if (chapterText != null) chapterText.text = chapterTitle ?? string.Empty;
            if (overlayGroup != null)
            {
                overlayGroup.alpha = 1f;
                overlayGroup.blocksRaycasts = true;
                overlayGroup.interactable = true;
            }

            yield return new WaitForSecondsRealtime(holdDuration);
            yield return Fade(1f, 0f, fadeOutDuration);

            if (chapterText != null) chapterText.text = string.Empty;
            if (overlayGroup != null)
            {
                overlayGroup.blocksRaycasts = false;
                overlayGroup.interactable = false;
            }
            IsTransitioning = false;
            onComplete?.Invoke();
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (overlayGroup == null) yield break;
            if (duration <= 0f)
            {
                overlayGroup.alpha = to;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                overlayGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
            overlayGroup.alpha = to;
        }
    }
}
