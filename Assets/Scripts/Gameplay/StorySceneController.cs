using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DemocracyWay.Data;
using DemocracyWay.Services;

namespace DemocracyWay.Gameplay
{
    /// <summary>
    /// The one scene-level script of every story scene: applies the chapter's
    /// look and sound, marks progress on the session, and starts the dialogue
    /// after the reveal. It is also the ONLY place that opts the game into
    /// pausing — a scene that can pause says so itself instead of the pause
    /// service keeping scene-name lists.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Story Scene Controller")]
    [DisallowMultipleComponent]
    public class StorySceneController : MonoBehaviour
    {
        [Tooltip("Το ChapterDefinition αυτής της σκηνής — ορίζει background, μουσική και διάλογο.")]
        [SerializeField] private ChapterDefinition chapter;

        [Tooltip("Το full-screen Image όπου μπαίνει το background του κεφαλαίου.")]
        [SerializeField] private Image backgroundImage;

        [Tooltip("Ο DialogueRunner της σκηνής — ξεκινά μετά το fade-in και την καθυστέρηση του κεφαλαίου.")]
        [SerializeField] private DialogueRunner dialogueRunner;

        void OnEnable()
        {
            var pause = ServicesRoot.Pause;
            if (pause != null) pause.CanPause = true;
        }

        void OnDisable()
        {
            var pause = ServicesRoot.Pause;
            if (pause == null) return;
            pause.CanPause = false;
            // Safety: leaving the scene while paused must never carry
            // timeScale 0 / muted voice into the next scene.
            pause.ForceResume();
        }

        void Start()
        {
            if (chapter == null)
            {
                Debug.LogError("[StoryScene] Δεν έχει συνδεθεί ChapterDefinition — η σκηνή δεν μπορεί να ξεκινήσει.", this);
                return;
            }

            if (backgroundImage != null && chapter.background != null)
                backgroundImage.sprite = chapter.background;

            ServicesRoot.Audio?.PlayMusic(chapter.ambientMusic);

            // The single place SetChapter is called: entering the scene IS the
            // checkpoint (analytics event + save), nothing else may claim it.
            ServicesRoot.Session?.SetChapter(chapter);

            StartCoroutine(IntroRoutine());
        }

        /// <summary>
        /// Holds the dialogue until the black overlay has fully revealed the
        /// scene, then waits the authored beat. The delay is SCALED on
        /// purpose: pausing during it freezes the countdown too.
        /// </summary>
        private IEnumerator IntroRoutine()
        {
            var flow = ServicesRoot.Flow;
            while (flow != null && flow.IsBusy) yield return null;

            if (chapter.dialogueStartDelay > 0f)
                yield return new WaitForSeconds(chapter.dialogueStartDelay);

            if (dialogueRunner != null)
                dialogueRunner.Begin(chapter);
            else
                Debug.LogError("[StoryScene] Δεν έχει συνδεθεί DialogueRunner.", this);
        }
    }
}
