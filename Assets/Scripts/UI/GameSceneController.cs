using TMPro;
using UnityEngine;

namespace DemocracyWay.UI
{
    using DemocracyWay.Core;
    using DemocracyWay.Framework;

    /// <summary>
    /// The playable screen for this milestone: the five indicators, the current
    /// prytany, and two buttons — draw a random dialogue, or end the round and
    /// let the lot pick the next presiding tribe.
    ///
    /// Deliberately thin. It owns no game rules; it wires the HUD to
    /// <see cref="GameStateService"/> and hands dialogues to
    /// <see cref="DialoguePanel"/>.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Game Scene Controller")]
    public class GameSceneController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DialoguePanel dialoguePanel;
        [SerializeField] private MenuButton randomDialogueButton;
        [SerializeField] private MenuButton nextRoundButton;
        [SerializeField] private TMP_Text statusLabel;

        [Header("Flow")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("Strings")]
        [SerializeField] private string randomDialogueLabel = "Τυχαίος διάλογος";
        [SerializeField] private string nextRoundLabel = "Επόμενη πρυτανεία";
        [SerializeField] private string noDialoguesText = "Δεν υπάρχουν διάλογοι στη βάση.";
        [SerializeField] private string yearOverText = "Όλες οι πρυτανείες ολοκληρώθηκαν.";

        private GameStateService state;

        void Awake()
        {
            if (randomDialogueButton != null)
            {
                randomDialogueButton.Text = randomDialogueLabel;
                randomDialogueButton.onClick.AddListener(PlayRandomDialogue);
            }
            if (nextRoundButton != null)
            {
                nextRoundButton.Text = nextRoundLabel;
                nextRoundButton.onClick.AddListener(NextRound);
            }
        }

        void Start()
        {
            state = GameStateService.Instance;

            if (state == null || state.Session == null || state.Session.profile == null ||
                !state.Session.profile.IsComplete)
            {
                // Someone opened Game.unity directly without a run in progress.
                // Bounce back rather than showing an empty HUD.
                Debug.LogWarning("[GameScene] No run in progress — returning to the main menu.");
                if (SceneTransitionController.Instance != null)
                    SceneTransitionController.Instance.LoadSceneWithTransition(mainMenuSceneName, string.Empty);
                return;
            }

            if (statusLabel != null) statusLabel.text = string.Empty;
            UpdateButtons();
        }

        private void PlayRandomDialogue()
        {
            if (state == null || dialoguePanel == null) return;

            var entry = state.PickRandomDialogue();
            if (entry == null)
            {
                if (statusLabel != null) statusLabel.text = noDialoguesText;
                return;
            }

            if (statusLabel != null) statusLabel.text = string.Empty;
            SetButtonsInteractable(false);
            dialoguePanel.Play(entry, closedCallback: () =>
            {
                SetButtonsInteractable(true);
                UpdateButtons();
            });
        }

        private void NextRound()
        {
            if (state == null) return;
            state.AdvanceRound();
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            var prytany = state != null ? state.Prytany : null;
            bool yearOver = prytany == null || prytany.IsFinished;

            if (nextRoundButton != null) nextRoundButton.Interactable = !yearOver;
            if (yearOver && statusLabel != null) statusLabel.text = yearOverText;
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (randomDialogueButton != null) randomDialogueButton.Interactable = interactable;
            if (nextRoundButton != null) nextRoundButton.Interactable = interactable;
        }
    }
}
