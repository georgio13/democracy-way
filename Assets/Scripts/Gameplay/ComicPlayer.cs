using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DemocracyWay.Data;
using DemocracyWay.Services;
using DemocracyWay.UI;

namespace DemocracyWay.Gameplay
{
    /// <summary>
    /// Plays the intro comic from <c>ServicesRoot.Config.introComic</c>:
    /// panels are spawned into a grid up front (alpha 0, so the layout never
    /// jumps) and revealed one by one with each panel's own delay, fade and
    /// sound. Everything runs on unscaled time per the project convention.
    ///
    /// Skipping (button or Space/Escape) funnels into <see cref="Finish"/>,
    /// which is idempotent — the natural end and a skip can never double-fire
    /// the scene transition.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Comic Player")]
    [DisallowMultipleComponent]
    public class ComicPlayer : MonoBehaviour
    {
        [Tooltip("Το container με GridLayoutGroup όπου μπαίνουν τα καρέ.")]
        [SerializeField] private RectTransform panelContainer;

        [Tooltip("Prefab ενός καρέ: Image + CanvasGroup.")]
        [SerializeField] private GameObject panelPrefab;

        [Tooltip("Κουμπί παράλειψης — εμφανίζεται μόνο όταν το ComicSequence επιτρέπει skip.")]
        [SerializeField] private UiButton skipButton;

        [Tooltip("Ετικέτα του κουμπιού παράλειψης.")]
        [SerializeField] private string skipLabel = "Παράλειψη";

        private ComicSequence sequence;
        private Coroutine playRoutine;
        private bool finished;

        void Start()
        {
            sequence = ServicesRoot.Config != null ? ServicesRoot.Config.introComic : null;

            if (skipButton != null)
            {
                skipButton.Text = skipLabel;
                skipButton.onClick.AddListener(Finish);
                skipButton.gameObject.SetActive(sequence != null && sequence.allowSkip);
            }

            if (sequence == null)
                Debug.LogWarning("[ComicPlayer] Δεν υπάρχει introComic στο GameConfig — μετάβαση κατευθείαν στο πρώτο κεφάλαιο.", this);

            playRoutine = StartCoroutine(PlayRoutine());
        }

        void Update()
        {
            // Space/Escape skip, polled directly per the no-.inputactions rule.
            // Gated on allowSkip (same rule as the button) and on Flow.IsBusy,
            // because a GoToScene issued mid-transition would be dropped.
            if (finished || sequence == null || !sequence.allowSkip) return;
            var flow = ServicesRoot.Flow;
            if (flow != null && flow.IsBusy) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.spaceKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
                Finish();
        }

        private IEnumerator PlayRoutine()
        {
            if (sequence != null && sequence.panels.Count > 0 &&
                panelContainer != null && panelPrefab != null)
            {
                var groups = BuildPanels();
                for (int i = 0; i < groups.Count; i++)
                {
                    var panel = sequence.panels[i];
                    if (panel.delayBeforeShow > 0f)
                        yield return new WaitForSecondsRealtime(panel.delayBeforeShow);

                    ServicesRoot.Audio?.PlaySfx(panel.sound);
                    yield return FadeIn(groups[i], panel.fadeInDuration);
                }

                if (sequence.holdAfterLastPanel > 0f)
                    yield return new WaitForSecondsRealtime(sequence.holdAfterLastPanel);
            }

            // Never finish while the reveal transition still runs — the
            // GoToScene inside Finish would be ignored and the comic would
            // hang forever with 'finished' already set.
            var flow = ServicesRoot.Flow;
            while (flow != null && flow.IsBusy) yield return null;
            Finish();
        }

        /// <summary>Spawns every panel invisible so the grid layout is final
        /// from frame one — cells must not shift while panels appear.</summary>
        private List<CanvasGroup> BuildPanels()
        {
            var groups = new List<CanvasGroup>();
            for (int i = 0; i < sequence.panels.Count; i++)
            {
                var go = Instantiate(panelPrefab, panelContainer);
                go.name = $"ComicPanel_{i + 1}";

                var image = go.GetComponent<Image>();
                if (image != null)
                {
                    image.sprite = sequence.panels[i].image;
                    image.enabled = sequence.panels[i].image != null;
                }

                var group = go.GetComponent<CanvasGroup>();
                if (group == null) group = go.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                groups.Add(group);
            }
            return groups;
        }

        private IEnumerator FadeIn(CanvasGroup group, float duration)
        {
            if (group == null) yield break;
            if (duration <= 0f) { group.alpha = 1f; yield break; }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime; // unscaled: project-wide UI rule
                group.alpha = Mathf.Clamp01(t / duration);
                yield return null;
            }
            group.alpha = 1f;
        }

        /// <summary>
        /// Ends the comic exactly once, from whichever path got here first
        /// (natural end, skip button, Space/Escape), and hands off to the
        /// first chapter.
        /// </summary>
        public void Finish()
        {
            if (finished) return;
            var flow = ServicesRoot.Flow;
            // Don't latch 'finished' while a transition runs — GoToScene
            // would be dropped and no path would ever retry.
            if (flow == null || flow.IsBusy) return;

            finished = true;
            if (playRoutine != null) StopCoroutine(playRoutine);

            var chapter = ServicesRoot.Config != null ? ServicesRoot.Config.firstChapter : null;
            if (chapter == null)
            {
                Debug.LogError("[ComicPlayer] Δεν υπάρχει firstChapter στο GameConfig — επιστροφή στο μενού.", this);
                flow.GoToScene("MainMenu");
                return;
            }

            flow.GoToScene(chapter.sceneName, chapter.title, showLoading: true);
        }
    }
}
