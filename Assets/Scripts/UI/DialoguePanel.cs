using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DemocracyWay.UI
{
    using DemocracyWay.Core;
    using DemocracyWay.Dialogue;
    using DemocracyWay.Framework;

    /// <summary>
    /// Plays one <see cref="DialogueEntry"/>: lines first (click to advance),
    /// then the choices, then the chosen option's outcome and the indicator
    /// changes it caused.
    ///
    /// The panel applies the indicator deltas itself through
    /// <see cref="GameStateService.ApplyDeltas"/> — the caller only needs to
    /// hand it an entry and get told when it closes.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Dialogue Panel")]
    public class DialoguePanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text speakerLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private TMP_Text effectsLabel;
        [SerializeField] private MenuButton continueButton;
        [SerializeField] private RectTransform choiceContainer;
        [SerializeField] private GameObject choicePrefab;

        [Header("Strings")]
        [SerializeField] private string continueLabel = "Συνέχεια";
        [SerializeField] private string closeLabel = "Κλείσιμο";
        [SerializeField] private string narratorLabel = "";

        private DialogueEntry entry;
        private Action onClosed;
        private int lineIndex;

        /// <summary>Which phase of the dialogue we are in. Kept explicit so the
        /// continue button knows whether it advances a line or closes.</summary>
        private enum Phase { Lines, Choices, Outcome }
        private Phase phase;

        void Awake()
        {
            if (continueButton != null) continueButton.onClick.AddListener(HandleContinue);
            SetVisible(false);
        }

        public void Play(DialogueEntry dialogue, Action closedCallback)
        {
            if (dialogue == null || !dialogue.IsValid)
            {
                closedCallback?.Invoke();
                return;
            }

            entry = dialogue;
            onClosed = closedCallback;
            lineIndex = 0;
            phase = Phase.Lines;

            gameObject.SetActive(true);
            SetVisible(true);

            if (titleLabel != null) titleLabel.text = entry.title;
            if (effectsLabel != null) effectsLabel.text = string.Empty;
            ClearChoices();
            ShowCurrentLine();
        }

        // ════════════ Lines ════════════

        private void ShowCurrentLine()
        {
            var line = entry.lines[lineIndex];

            if (speakerLabel != null)
                speakerLabel.text = line.IsNarration ? narratorLabel : line.speaker;
            if (bodyLabel != null)
                bodyLabel.text = line.text;

            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(true);
                continueButton.Text = continueLabel;
            }
        }

        private void HandleContinue()
        {
            switch (phase)
            {
                case Phase.Lines:
                    lineIndex++;
                    if (lineIndex < entry.lines.Count)
                    {
                        ShowCurrentLine();
                        return;
                    }
                    // Lines exhausted — either offer choices or end here.
                    if (entry.choices != null && entry.choices.Count > 0) ShowChoices();
                    else Close();
                    return;

                case Phase.Outcome:
                    Close();
                    return;
            }
        }

        // ════════════ Choices ════════════

        private void ShowChoices()
        {
            phase = Phase.Choices;

            if (speakerLabel != null) speakerLabel.text = string.Empty;
            if (bodyLabel != null) bodyLabel.text = string.Empty;
            if (continueButton != null) continueButton.gameObject.SetActive(false);

            if (choiceContainer == null || choicePrefab == null)
            {
                Debug.LogError("[DialoguePanel] choiceContainer/choicePrefab not wired — re-run Tools > DemocracyWay > Init.");
                Close();
                return;
            }

            ClearChoices();
            for (int i = 0; i < entry.choices.Count; i++)
            {
                var choice = entry.choices[i];
                var go = Instantiate(choicePrefab, choiceContainer);
                go.name = $"Choice_{i}";

                var button = go.GetComponent<MenuButton>();
                if (button == null) continue;
                button.Text = choice.text;
                // Local copy — otherwise every listener would capture the loop
                // variable and every button would apply the last choice.
                var captured = choice;
                button.onClick.AddListener(() => ChooseOption(captured));
            }
        }

        private void ClearChoices() => UiUtil.ClearChildren(choiceContainer);

        private void ChooseOption(DialogueChoice choice)
        {
            if (choice == null) { Close(); return; }

            if (GameStateService.Instance != null && choice.effects != null)
                GameStateService.Instance.ApplyDeltas(choice.effects);

            ShowOutcome(choice);
        }

        // ════════════ Outcome ════════════

        private void ShowOutcome(DialogueChoice choice)
        {
            phase = Phase.Outcome;
            ClearChoices();

            if (speakerLabel != null) speakerLabel.text = string.Empty;
            if (bodyLabel != null) bodyLabel.text = choice.outcome ?? string.Empty;
            if (effectsLabel != null) effectsLabel.text = choice.EffectsSummary();

            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(true);
                continueButton.Text = closeLabel;
            }
        }

        private void Close()
        {
            if (entry != null && GameStateService.Instance != null)
                GameStateService.Instance.MarkDialogueSeen(entry.id);

            ClearChoices();
            SetVisible(false);
            gameObject.SetActive(false);

            var cb = onClosed;
            entry = null;
            onClosed = null;
            cb?.Invoke();
        }

        private void SetVisible(bool visible)
        {
            if (rootGroup == null) return;
            rootGroup.alpha = visible ? 1f : 0f;
            rootGroup.blocksRaycasts = visible;
            rootGroup.interactable = visible;
        }
    }
}
