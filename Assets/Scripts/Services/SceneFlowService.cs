using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DemocracyWay.Services
{
    /// <summary>
    /// Owns the persistent full-screen overlay (black fade + chapter title +
    /// loading indicator) and every scene change. One public entry point:
    ///
    ///   ServicesRoot.Flow.GoToScene("MainMenu");
    ///   ServicesRoot.Flow.GoToScene(chapter.sceneName, chapter.title, showLoading: true);
    ///
    /// Timeline: fade to black → (loading text while LoadSceneAsync runs) →
    /// (chapter title holds on black) → fade out revealing the new scene.
    /// The overlay is authored to start fully black, so the very first
    /// GoToScene (Boot → MainMenu) skips the fade-in and simply reveals.
    /// Scene scripts that must wait for the reveal poll <see cref="IsBusy"/>.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Scene Flow Service")]
    [DisallowMultipleComponent]
    public class SceneFlowService : MonoBehaviour
    {
        [Header("Overlay (children of the Systems prefab, wired once)")]
        [SerializeField] private Canvas overlayCanvas;
        [SerializeField] private CanvasGroup fadeGroup;
        [SerializeField] private TMP_Text chapterTitleText;
        [SerializeField] private GameObject loadingGroup;

        /// <summary>True from the moment a transition starts until the reveal
        /// completes. UI ignores clicks and scenes hold their intros while busy.</summary>
        public bool IsBusy { get; private set; }

        void Awake()
        {
            // The overlay is serialized fully black (alpha 1) so the first
            // frame of the game is black, never a naked half-loaded scene.
            if (overlayCanvas != null) overlayCanvas.sortingOrder = 9999;
            if (chapterTitleText != null) chapterTitleText.text = string.Empty;
            if (loadingGroup != null) loadingGroup.SetActive(false);
            if (fadeGroup != null)
            {
                fadeGroup.blocksRaycasts = fadeGroup.alpha > 0f;
                fadeGroup.interactable = false;
            }
        }

        void Start()
        {
            // Boot lands here with the overlay still black and no transition
            // running (BootLoader calls GoToScene in ITS Start — but if a dev
            // presses Play on a normal scene with PlayFromBoot disabled, the
            // black overlay would stick forever). Reveal defensively.
            if (!IsBusy && fadeGroup != null && fadeGroup.alpha >= 1f &&
                SceneManager.GetActiveScene().name != "Boot")
            {
                StartCoroutine(Fade(1f, 0f, FadeDuration));
            }
        }

        private float FadeDuration => ServicesRoot.Config != null ? ServicesRoot.Config.fadeDuration : 0.6f;
        private float TitleHold => ServicesRoot.Config != null ? ServicesRoot.Config.chapterTitleHold : 1.8f;
        private float MinLoadingTime => ServicesRoot.Config != null ? ServicesRoot.Config.minLoadingTime : 0.4f;

        /// <summary>
        /// Fades to black, loads <paramref name="sceneName"/>, optionally shows
        /// the loading indicator and/or holds a chapter title, then reveals.
        /// </summary>
        public void GoToScene(string sceneName, string chapterTitle = "", bool showLoading = false)
        {
            if (IsBusy)
            {
                Debug.LogWarning($"[SceneFlow] Αγνοήθηκε GoToScene('{sceneName}') — τρέχει ήδη μετάβαση.");
                return;
            }
            StartCoroutine(TransitionRoutine(sceneName, chapterTitle ?? "", showLoading));
        }

        private IEnumerator TransitionRoutine(string sceneName, string chapterTitle, bool showLoading)
        {
            IsBusy = true;
            fadeGroup.blocksRaycasts = true;

            // 1. To black (skipped when already black — e.g. the Boot reveal).
            if (fadeGroup.alpha < 1f)
                yield return Fade(fadeGroup.alpha, 1f, FadeDuration);

            // 2. Load behind the black. Activation is held back so the switch
            //    happens in one controlled moment.
            if (showLoading && loadingGroup != null) loadingGroup.SetActive(true);
            float loadStarted = Time.unscaledTime;

            var op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;
            while (op.progress < 0.9f) yield return null;

            // Loading screens that flash for two frames read as a glitch —
            // keep the indicator up for a minimum beat.
            if (showLoading)
                while (Time.unscaledTime - loadStarted < MinLoadingTime) yield return null;

            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            if (loadingGroup != null) loadingGroup.SetActive(false);

            // 3. Chapter title on black.
            if (!string.IsNullOrEmpty(chapterTitle) && chapterTitleText != null)
            {
                chapterTitleText.text = chapterTitle;
                yield return new WaitForSecondsRealtime(TitleHold);
                chapterTitleText.text = string.Empty;
            }

            // 4. Reveal.
            yield return Fade(1f, 0f, FadeDuration);

            fadeGroup.blocksRaycasts = false;
            IsBusy = false;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (fadeGroup == null) yield break;
            if (duration <= 0f) { fadeGroup.alpha = to; yield break; }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime; // unscaled: works during pause (timeScale 0)
                fadeGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
            fadeGroup.alpha = to;
        }
    }
}
