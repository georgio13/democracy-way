using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    public sealed partial class GameFlowController
    {
        public void NextPrologue()
        {
            if (modalOpen || narrativeFading || currentScreen != "prologue")
                return;
            EnsureState();
            StartCoroutine(ChangeProloguePage());
        }

        private IEnumerator ChangeProloguePage()
        {
            narrativeFading = true;
            CanvasGroup group = roots.TryGetValue("prologue", out GameObject root) ? root.GetComponent<CanvasGroup>() : null;
            if (group != null)
            {
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            float elapsed = 0f;
            while (elapsed < 0.3f)
            {
                elapsed += Time.unscaledDeltaTime;
                if (group != null)
                    group.alpha = 1f - Mathf.Clamp01(elapsed / 0.3f);
                yield return null;
            }

            // The prologue is an ordered collection of pages, independent of biography flow.
            int nextPage = State.prologueIndex + 1;
            if (nextPage < config.prologue.Count)
            {
                State.prologueIndex = nextPage;
                SaveCheckpoint();
                RenderPrologue();
            }
            else
            {
                State.phase = "demographics";
                SaveCheckpoint();
                Show(State.phase);
            }

            elapsed = 0f;
            while (elapsed < 0.5f)
            {
                elapsed += Time.unscaledDeltaTime;
                if (group != null)
                    group.alpha = Mathf.Clamp01(elapsed / 0.5f);
                yield return null;
            }

            if (group != null)
            {
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
            }

            narrativeFading = false;
        }

        public void ChooseBio(int choiceIndex)
        {
            if (modalOpen || State == null || State.phase != "biography")
                return;
            BiographyStep step = CurrentBioStep();
            var visible = step == null ? null : VisibleBioChoices(step);
            if (step == null || visible == null || choiceIndex < 0 || choiceIndex >= visible.Count)
                return;
            NarrativeChoice choice = visible[choiceIndex];
            if (choice == null || string.IsNullOrWhiteSpace(choice.id))
                return;
            GameRules.ApplyChoice(State, step, choice);
            if (step.kind == BiographyStepKind.Identity)
                State.identityComplete = true;
            SaveCheckpoint();
            RenderBiography();

        }

        public void AdvanceBio()
        {
            if (modalOpen || State == null || State.phase != "biography")
                return;
            BiographyStep step = CurrentBioStep();
            if (step == null)
                return;
            if (!State.choices.Exists(x => x.stepId == step.id))
            {
                SetStatus(JsonContent.Copy("Copy_63be43133176a9a4"));
                return;
            }

            AdvanceBiographyIndex();
        }

        private void AdvanceBiographyIndex()
        {
            int nextIndex = State.biographyIndex + 1;
            if (nextIndex < config.biography.Count)
            {
                State.biographyIndex = nextIndex;
                SaveCheckpoint(); ShowBiography();
            }
            else { State.phase = "summary"; SaveCheckpoint(); Show("bio-summary"); }
        }

        public void FinishBio()
        {
            if (modalOpen || State == null || State.phase != "summary")
                return;
            for (int i = 0; i < config.biography.Count; i++)
            {
                BiographyStep step = config.biography[i];
                bool complete = step.kind == BiographyStepKind.Identity ? State.identityComplete : State.choices.Exists(x => x.stepId == step.id);
                if (!complete)
                {
                    State.biographyIndex = i;
                    State.phase = "biography";
                    ShowBiography();
                    SetStatus(JsonContent.Copy("Copy_7ec44327191f5131"));
                    return;
                }
            }

            State.biographyComplete = true;
            State.phase = "prytany-title";
            RecalculateMetrics();
            InitialiseAgoraCitizen();   // τάξη + bonuses από τη βιογραφία, πόλη πάντα από cityStart (ΣΤ.1)
            SaveCheckpoint();
            Show(State.phase);
        }

        private BiographyStep CurrentBioStep()
        {
            return State != null && State.biographyIndex >= 0 && State.biographyIndex < config.biography.Count ? config.biography[State.biographyIndex] : null;
        }

        private void ShowBiography()
        {
            EnsureState();
            BiographyStep step = CurrentBioStep();
            if (step == null)
            {
                SetStatus(JsonContent.Copy("Copy_1bdbf5eab98ae2e2"));
                return;
            }

            State.phase = "biography";
            string id = step.kind == BiographyStepKind.Family ? "bio-family" : step.kind == BiographyStepKind.Oath ? "bio-oath" : "bio-choice";
            Show(id);
        }
    }
}
