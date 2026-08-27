using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DemocracyWay.Data;
using DemocracyWay.Services;

namespace DemocracyWay.Gameplay
{
    /// <summary>
    /// Walks a chapter's <see cref="DialogueTree"/> node by node in the
    /// bottom-center panel. Linear nodes advance on click/Space (polled, per
    /// the no-.inputactions rule); branching nodes spawn one
    /// <see cref="DialogueChoiceButton"/> per choice and every consequence of
    /// a pick (history, analytics, indicators, flags, week) goes through
    /// <c>ServicesRoot.Session.RecordChoice</c> — the runner itself never
    /// touches game state.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Dialogue Runner")]
    [DisallowMultipleComponent]
    public class DialogueRunner : MonoBehaviour
    {
        [Header("Panel (κάτω-κέντρο)")]
        [Tooltip("Η ρίζα του panel διαλόγου — κρυφή όσο δεν τρέχει διάλογος.")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("Το πορτρέτο του ομιλητή στην πάνω-αριστερή γωνία του panel.")]
        [SerializeField] private Image portraitImage;

        [Tooltip("Το όνομα του ομιλητή, δίπλα στο πορτρέτο.")]
        [SerializeField] private TMP_Text speakerNameText;

        [Tooltip("Τα λόγια του ομιλητή.")]
        [SerializeField] private TMP_Text bodyText;

        [Header("Επιλογές")]
        [Tooltip("Το container όπου μπαίνουν τα κουμπιά επιλογών.")]
        [SerializeField] private RectTransform choicesContainer;

        [Tooltip("Prefab ενός κουμπιού επιλογής.")]
        [SerializeField] private DialogueChoiceButton choiceButtonPrefab;

        [Header("Προώθηση")]
        [Tooltip("Η υπόδειξη προώθησης — ορατή μόνο σε γραμμές χωρίς επιλογές.")]
        [SerializeField] private TMP_Text advanceHintText;

        [Tooltip("Το κείμενο της υπόδειξης προώθησης.")]
        [SerializeField] private string advanceHint = "συνέχεια ▸";

        private ChapterDefinition chapter;
        private DialogueTree tree;
        private DialogueNode currentNode;
        private bool running;
        private bool waitingForAdvance;

        /// <summary>Frame the current node appeared. The click that picked a
        /// choice (delivered by the EventSystem, possibly earlier in this same
        /// frame) must not also count as "advance" for the node it opened.</summary>
        private int shownFrame = -1;
        private readonly List<DialogueChoiceButton> choiceButtons = new List<DialogueChoiceButton>();

        void Awake()
        {
            // The panel is authored visible for editing comfort; at runtime it
            // only exists while a dialogue runs.
            if (panelRoot != null) panelRoot.SetActive(false);
            if (advanceHintText != null) advanceHintText.text = advanceHint;
        }

        /// <summary>Starts the chapter's dialogue at its StartNode. Called by
        /// the StorySceneController after the reveal + authored delay.</summary>
        public void Begin(ChapterDefinition chapter)
        {
            this.chapter = chapter;
            tree = chapter != null ? chapter.dialogue : null;
            running = true;

            var start = tree != null ? tree.StartNode : null;
            if (start == null)
            {
                // A chapter without dialogue is a data mistake, but the story
                // must keep flowing — treat it as an instantly finished tree.
                Debug.LogWarning("[DialogueRunner] Το κεφάλαιο δεν έχει DialogueTree/κόμβους — ο διάλογος παραλείπεται.", this);
                End();
                return;
            }

            // A loaded save that autosaved mid-chapter resumes at the node it
            // stood on. Replaying from the start would re-apply every already
            // recorded choice (double indicator effects, duplicate analytics).
            var session = ServicesRoot.Session;
            if (session != null && session.HasActiveRun &&
                !string.IsNullOrEmpty(session.Current.currentDialogueNodeId))
            {
                var saved = tree.GetNode(session.Current.currentDialogueNodeId);
                if (saved != null) start = saved;
            }

            if (panelRoot != null) panelRoot.SetActive(true);
            ShowNode(start);
        }

        void Update()
        {
            if (!running || !waitingForAdvance) return;

            // Advance input is dead while paused (the click belongs to the
            // pause menu) and mid-transition (GoToScene would be dropped).
            var pause = ServicesRoot.Pause;
            if (pause != null && pause.IsPaused) return;
            var flow = ServicesRoot.Flow;
            if (flow != null && flow.IsBusy) return;

            // Same-frame click guards: the click that spawned this node (via a
            // choice button) or closed the pause menu is not an advance.
            if (Time.frameCount == shownFrame) return;
            if (pause != null && Time.frameCount == pause.LastResumeFrame) return;

            bool clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            bool spacePressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            if (!clicked && !spacePressed) return;

            if (currentNode.IsEnd) End();
            else ShowNode(tree.GetNode(currentNode.nextNodeId));
        }

        // ════════ Node display ════════

        private void ShowNode(DialogueNode node)
        {
            if (node == null)
            {
                // Broken nextNodeId reference — OnValidate warned the author;
                // at runtime the only sane move is to end gracefully.
                End();
                return;
            }
            currentNode = node;
            shownFrame = Time.frameCount;
            ServicesRoot.Session?.SetDialoguePosition(node.id);

            if (portraitImage != null)
            {
                portraitImage.sprite = node.portrait;
                portraitImage.gameObject.SetActive(node.portrait != null);
            }
            if (speakerNameText != null) speakerNameText.text = node.speakerName;
            if (bodyText != null) bodyText.text = node.text;

            var audio = ServicesRoot.Audio;
            if (audio != null)
            {
                if (node.voiceClip != null) audio.PlayVoice(node.voiceClip);
                else audio.StopVoice(); // the previous line must not keep talking over this one
            }

            ClearChoiceButtons();
            if (node.HasChoices)
            {
                waitingForAdvance = false;
                if (advanceHintText != null) advanceHintText.gameObject.SetActive(false);
                foreach (var choice in node.choices)
                {
                    var button = Instantiate(choiceButtonPrefab, choicesContainer);
                    button.Init(choice, OnChoiceClicked);
                    choiceButtons.Add(button);
                }
            }
            else
            {
                waitingForAdvance = true;
                if (advanceHintText != null) advanceHintText.gameObject.SetActive(true);
            }
        }

        private void OnChoiceClicked(DialogueChoice choice)
        {
            if (!running || choice == null) return;

            // The session applies every consequence (record, analytics,
            // indicators, flags, week advance) in one place.
            ServicesRoot.Session?.RecordChoice(currentNode, choice);

            ClearChoiceButtons();
            if (string.IsNullOrEmpty(choice.nextNodeId)) End();
            else ShowNode(tree.GetNode(choice.nextNodeId));
        }

        // ════════ Teardown ════════

        private void End()
        {
            if (!running) return;
            running = false;
            waitingForAdvance = false;

            ClearChoiceButtons();
            ServicesRoot.Audio?.StopVoice();
            if (panelRoot != null) panelRoot.SetActive(false);

            // nextChapter empty = the chapter stays on its scene (per the
            // ChapterDefinition contract) — no transition then.
            var next = chapter != null ? chapter.nextChapter : null;
            if (next != null)
                ServicesRoot.Flow?.GoToScene(next.sceneName, next.title, showLoading: true);
        }

        private void ClearChoiceButtons()
        {
            foreach (var button in choiceButtons)
                if (button != null) Destroy(button.gameObject);
            choiceButtons.Clear();
        }

        void OnDestroy()
        {
            // Scene torn down mid-line (pause menu → main menu, next chapter):
            // the voice source is persistent and would keep talking.
            if (running) ServicesRoot.Audio?.StopVoice();
        }
    }
}
