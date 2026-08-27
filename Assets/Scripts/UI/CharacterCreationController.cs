using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.UI
{
    using DemocracyWay.Core;
    using DemocracyWay.Framework;

    /// <summary>
    /// Drives the six-step character creation screen.
    ///
    /// Layout is fixed by design: the option list runs down the RIGHT side, one
    /// option under the next; the LEFT side shows the artwork for whatever
    /// option is currently highlighted, with its description underneath.
    /// Hovering an option previews it on the left; clicking commits it.
    ///
    /// Step order comes straight from <see cref="CreationStep"/>. Trittys is the
    /// only step whose option list depends on an earlier answer — that filtering
    /// lives in <see cref="CreationDatabase.OptionsFor"/>, not here.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Character Creation Controller")]
    public class CharacterCreationController : MonoBehaviour
    {
        [Header("Content")]
        [Tooltip("Falls back to GameStateService.Creation when left empty.")]
        [SerializeField] private CreationDatabase database;

        [Header("Left — preview")]
        [SerializeField] private Image previewImage;
        [SerializeField] private TMP_Text previewTitle;
        [SerializeField] private TMP_Text previewDescription;

        [Header("Right — options")]
        [SerializeField] private TMP_Text stepTitle;
        [SerializeField] private TMP_Text stepCounter;
        [SerializeField] private RectTransform optionContainer;
        [SerializeField] private GameObject optionPrefab;

        [Header("Navigation")]
        [SerializeField] private MenuButton backButton;
        [SerializeField] private MenuButton nextButton;

        [Header("Flow")]
        [Tooltip("Scene loaded once all six steps are answered.")]
        [SerializeField] private string comicSceneName = "ComicIntro";
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("Strings")]
        [SerializeField] private string nextLabel = "Επόμενο";
        [SerializeField] private string finishLabel = "Ξεκίνα";
        [SerializeField] private string backLabel = "Πίσω";
        [SerializeField] private string noOptionsHint = "Διάλεξε πρώτα φυλή.";

        private static readonly CreationStep[] Steps =
        {
            CreationStep.Gender,
            CreationStep.Tribe,
            CreationStep.Trittys,
            CreationStep.Wealth,
            CreationStep.Period,
            CreationStep.Occupation
        };

        private readonly List<CreationOptionButton> spawnedRows = new List<CreationOptionButton>();
        private CitizenProfile profile = new CitizenProfile();
        private int stepIndex;

        /// <summary>Slot the finished character will be saved into. Set by the
        /// main menu before this scene loads; -1 means "first free slot".</summary>
        public static int TargetSlot { get; set; } = -1;

        private CreationStep CurrentStep => Steps[stepIndex];

        void Awake()
        {
            if (database == null && GameStateService.Instance != null)
                database = GameStateService.Instance.Creation;

            if (backButton != null)
            {
                backButton.Text = backLabel;
                backButton.onClick.AddListener(GoBack);
            }
            if (nextButton != null) nextButton.onClick.AddListener(GoNext);
        }

        void Start()
        {
            // These three are what the whole screen is made of; without any one
            // of them the player just sees an empty column and no explanation,
            // so say exactly what is missing.
            if (database == null || optionPrefab == null || optionContainer == null)
            {
                Debug.LogError(
                    "[CharacterCreation] Missing wiring — re-run Tools > DemocracyWay > Init. " +
                    $"database={(database == null ? "NULL" : "ok")}, " +
                    $"optionPrefab={(optionPrefab == null ? "NULL" : "ok")}, " +
                    $"optionContainer={(optionContainer == null ? "NULL" : "ok")}");
                if (previewDescription != null)
                    previewDescription.text =
                        "Λείπουν δεδομένα. Τρέξε ξανά: Tools > DemocracyWay > Init";
                return;
            }

            stepIndex = 0;
            BuildStep();
        }

        // ════════════ Step rendering ════════════

        private void BuildStep()
        {
            ClearRows();

            var step = CurrentStep;
            var options = database.OptionsFor(step, profile);

            if (stepTitle != null)   stepTitle.text   = CreationDatabase.StepTitle(step);
            if (stepCounter != null) stepCounter.text = $"{stepIndex + 1} / {Steps.Length}";

            if (options.Count == 0)
            {
                // Expected only on Trittys before a tribe is picked. Anywhere
                // else it means the database came through empty, which is worth
                // a warning rather than a blank column.
                if (step != CreationStep.Trittys)
                    Debug.LogWarning($"[CharacterCreation] No options for step {step} — is the CreationDatabase populated?");

                ShowPreview(null);
                if (previewDescription != null) previewDescription.text = noOptionsHint;
                UpdateNavigation();
                return;
            }

            string chosenId = profile.GetId(step);
            CreationOption toPreview = null;

            for (int i = 0; i < options.Count; i++)
            {
                var go = Instantiate(optionPrefab, optionContainer);
                go.name = $"Option_{options[i].id}";
                var row = go.GetComponent<CreationOptionButton>();
                if (row == null) continue;

                row.Bind(options[i], HandleHover, HandleSelect);
                spawnedRows.Add(row);

                bool isChosen = options[i].id == chosenId;
                row.SetSelected(isChosen);
                if (isChosen) toPreview = options[i];
            }

            // Preview the already-chosen option when revisiting a step,
            // otherwise the first one so the left panel is never blank.
            ShowPreview(toPreview ?? options[0]);
            UpdateNavigation();
        }

        private void ClearRows()
        {
            spawnedRows.Clear();
            UiUtil.ClearChildren(optionContainer);
        }

        private void ShowPreview(CreationOption option)
        {
            if (previewImage != null)
            {
                previewImage.sprite = option != null ? option.artwork : null;
                previewImage.enabled = previewImage.sprite != null;
            }
            if (previewTitle != null)
                previewTitle.text = option != null ? option.title : string.Empty;
            if (previewDescription != null)
                previewDescription.text = option != null ? option.description : string.Empty;
        }

        // ════════════ Interaction ════════════

        private void HandleHover(CreationOption option) => ShowPreview(option);

        private void HandleSelect(CreationOption option)
        {
            if (option == null) return;

            profile.Set(CurrentStep, option);

            for (int i = 0; i < spawnedRows.Count; i++)
                spawnedRows[i].SetSelected(spawnedRows[i].Option == option);

            ShowPreview(option);
            UpdateNavigation();
        }

        private void UpdateNavigation()
        {
            bool hasChoice = !string.IsNullOrEmpty(profile.GetId(CurrentStep));
            bool isLast = stepIndex == Steps.Length - 1;

            if (nextButton != null)
            {
                nextButton.Text = isLast ? finishLabel : nextLabel;
                nextButton.Interactable = hasChoice;
            }
        }

        private void GoNext()
        {
            if (string.IsNullOrEmpty(profile.GetId(CurrentStep))) return;

            if (stepIndex < Steps.Length - 1)
            {
                stepIndex++;
                BuildStep();
                return;
            }

            Finish();
        }

        private void GoBack()
        {
            if (stepIndex > 0)
            {
                stepIndex--;
                BuildStep();
                return;
            }

            // Backing out of the first step abandons creation entirely.
            LoadScene(mainMenuSceneName, string.Empty);
        }

        private void Finish()
        {
            if (!profile.IsComplete)
            {
                Debug.LogWarning("[CharacterCreation] Finish pressed with an incomplete profile.");
                return;
            }

            int slot = TargetSlot >= 0 ? TargetSlot : SaveSystem.FirstEmptySlot();
            if (slot < 0) slot = 0; // all four full and none chosen — overwrite the first

            if (GameStateService.Instance != null)
                GameStateService.Instance.StartNewRun(profile, slot);

            LoadScene(comicSceneName, string.Empty);
        }

        private void LoadScene(string sceneName, string chapterTitle)
        {
            if (SceneTransitionController.Instance != null)
                SceneTransitionController.Instance.LoadSceneWithTransition(sceneName, chapterTitle);
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }
    }
}
