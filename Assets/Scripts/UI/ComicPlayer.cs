using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.UI
{
    using DemocracyWay.Framework;

    /// <summary>
    /// Plays a <see cref="ComicSequence"/>: frames fade in one after another
    /// into a grid, each with its caption. Clicking anywhere skips ahead —
    /// first click reveals everything instantly, second click moves on.
    ///
    /// Frames are instantiated from <see cref="panelPrefab"/> so the sequence
    /// length is data, not scene structure.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Comic Player")]
    public class ComicPlayer : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private ComicSequence sequence;

        [Header("References")]
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private RectTransform panelContainer;
        [SerializeField] private GameObject panelPrefab;
        [SerializeField] private MenuButton continueButton;
        [SerializeField] private TMP_Text hintLabel;

        [Header("Flow")]
        [SerializeField] private string nextSceneName = "Game";

        [Header("Strings")]
        [SerializeField] private string skipHint = "Κλικ για παράλειψη";
        [SerializeField] private string continueLabel = "Συνέχεια";

        private readonly List<CanvasGroup> panelGroups = new List<CanvasGroup>();
        private Coroutine playRoutine;
        private bool allRevealed;

        void Awake()
        {
            if (continueButton != null)
            {
                continueButton.Text = continueLabel;
                continueButton.onClick.AddListener(Continue);
                continueButton.gameObject.SetActive(false);
            }
            if (hintLabel != null) hintLabel.text = skipHint;
        }

        void Start()
        {
            if (sequence == null || sequence.panels == null || sequence.panels.Count == 0)
            {
                Debug.LogWarning("[ComicPlayer] No sequence assigned — skipping straight through.");
                RevealFinished();
                return;
            }

            if (titleLabel != null) titleLabel.text = sequence.title;
            BuildPanels();
            playRoutine = StartCoroutine(PlayRoutine());
        }

        void Update()
        {
            // Any click/tap while frames are still appearing reveals the rest.
            if (allRevealed) return;
            if (UnityEngine.InputSystem.Mouse.current != null &&
                UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
                RevealAll();
        }

        private void BuildPanels()
        {
            panelGroups.Clear();
            if (panelContainer == null || panelPrefab == null)
            {
                Debug.LogError("[ComicPlayer] panelContainer/panelPrefab not wired — re-run Tools > DemocracyWay > Init.");
                return;
            }

            UiUtil.ClearChildren(panelContainer);

            for (int i = 0; i < sequence.panels.Count; i++)
            {
                var data = sequence.panels[i];
                var go = Instantiate(panelPrefab, panelContainer);
                go.name = $"ComicPanel_{i + 1}";

                var group = go.GetComponent<CanvasGroup>();
                if (group == null) group = go.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                panelGroups.Add(group);

                var image = go.transform.Find("Art")?.GetComponent<Image>();
                if (image != null)
                {
                    image.sprite = data.artwork;
                    image.enabled = data.artwork != null;
                }

                var caption = go.transform.Find("Caption")?.GetComponent<TMP_Text>();
                if (caption != null) caption.text = data.caption;
            }
        }

        private IEnumerator PlayRoutine()
        {
            for (int i = 0; i < panelGroups.Count; i++)
            {
                yield return FadeIn(panelGroups[i], sequence.fadeDuration);
                if (sequence.gapBetweenPanels > 0f)
                    yield return new WaitForSeconds(sequence.gapBetweenPanels);
            }
            RevealFinished();
        }

        private IEnumerator FadeIn(CanvasGroup group, float duration)
        {
            if (group == null) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            group.alpha = 1f;
        }

        private void RevealAll()
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }
            for (int i = 0; i < panelGroups.Count; i++)
                if (panelGroups[i] != null) panelGroups[i].alpha = 1f;

            RevealFinished();
        }

        private void RevealFinished()
        {
            allRevealed = true;
            if (hintLabel != null) hintLabel.gameObject.SetActive(false);
            if (continueButton != null) continueButton.gameObject.SetActive(true);
        }

        private void Continue()
        {
            if (SceneTransitionController.Instance != null)
                SceneTransitionController.Instance.LoadSceneWithTransition(nextSceneName, string.Empty);
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
        }
    }
}
