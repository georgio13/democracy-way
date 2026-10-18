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

            string nextNode=ArticyNarrative.Next("Prologue_"+config.prologue[State.prologueIndex].id.Replace('-','_'));
            int nextPrologue=config.prologue.FindIndex(p=>"Prologue_"+p.id.Replace('-','_')==nextNode);
            if (nextPrologue >= 0)
            {
                State.prologueIndex=nextPrologue;
                SaveCheckpoint();
                RenderPrologue();
            }
            else
            {
                State.phase = "biography";
                State.biographyIndex = config.biography.FindIndex(b=>"Bio_"+b.id.Replace('-','_')==nextNode);
                if(State.biographyIndex<0)throw new InvalidOperationException("Native prologue has no biography continuation.");
                SaveCheckpoint();
                ShowBiography();
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
            if (step == null || step.kind == BiographyStepKind.Identity || step.choices == null || choiceIndex < 0 || choiceIndex >= step.choices.Count)
                return;
            NarrativeChoice choice = step.choices[choiceIndex];
            if (choice == null || string.IsNullOrWhiteSpace(choice.id))
                return;
            GameRules.ApplyChoice(State, step, choice);
            SaveCheckpoint();
            RenderBiography();
            SetText("bio.feedback", choice.consequence);
            SetText(currentScreen + ".feedback", choice.consequence);
        }

        public void AdvanceBio()
        {
            if (modalOpen || State == null || State.phase != "biography")
                return;
            BiographyStep step = CurrentBioStep();
            if (step == null)
                return;
            if (step.kind == BiographyStepKind.Identity)
            {
                FinishIdentity();
                return;
            }

            if (!State.choices.Exists(x => x.stepId == step.id))
            {
                SetStatus(ArticyNarrative.Copy("Copy_63be43133176a9a4"));
                return;
            }

            AdvanceBiographyIndex();
        }

        private void AdvanceBiographyIndex()
        {
            var step=CurrentBioStep();
            string node="Bio_"+step.id.Replace('-','_');
            var chosen=State.choices.Find(c=>c.stepId==step.id);
            if(chosen!=null)node+="_"+chosen.choiceId.Replace('-','_');
            string next=ArticyNarrative.Next(node);
            int nextIndex=config.biography.FindIndex(b=>"Bio_"+b.id.Replace('-','_')==next);
            if (nextIndex>=0)
            {
                State.biographyIndex=nextIndex;
                SaveCheckpoint();
                ShowBiography();
            }
            else
            {
                if(next!="BioSummary")throw new InvalidOperationException("Unknown native biography continuation: "+next);
                State.phase = "summary";
                SaveCheckpoint();
                Show("bio-summary");
            }
        }

        public void SelectTribe(int index)
        {
            if (State == null || index < 0 || index >= config.tribes.Count)
                return;
            State.tribeIndex = index;
            TMP_Dropdown tribe = Get<TMP_Dropdown>("identity.tribe");
            if (tribe != null)
                tribe.SetValueWithoutNotify(index);
            UpdateIdentityDetails();
        }

        public void FinishIdentity()
        {
            if (modalOpen || State == null || State.phase != "biography")
                return;
            BiographyStep step = CurrentBioStep();
            if (step == null || step.kind != BiographyStepKind.Identity)
                return;
            TMP_InputField name = Get<TMP_InputField>("identity.name");
            string value = name != null ? name.text : State.playerName;
            value = (value ?? "").Trim().Replace("<", "").Replace(">", "").Replace("\n", " ").Replace("\r", " ");
            if (string.IsNullOrWhiteSpace(value))
            {
                SetStatus(ArticyNarrative.Copy("Copy_d1473beeba186906"));
                return;
            }

            State.playerName = value.Length > 40 ? value.Substring(0, 40) : value;
            TMP_Dropdown tribe = Get<TMP_Dropdown>("identity.tribe");
            TMP_Dropdown economicClass = Get<TMP_Dropdown>("identity.class");
            State.tribeIndex = tribe != null ? Mathf.Clamp(tribe.value, 0, config.tribes.Count - 1) : State.tribeIndex;
            State.economicClassIndex = step.editEconomicClass && economicClass != null ? Mathf.Clamp(economicClass.value, 0, config.economicClasses.Count - 1) : State.economicClassIndex;
            TribeDefinition selectedTribe = config.tribes[State.tribeIndex];
            State.deme = string.IsNullOrEmpty(selectedTribe.deme) ? State.deme : selectedTribe.deme;
            State.trittys = string.IsNullOrEmpty(selectedTribe.trittys) ? State.trittys : selectedTribe.trittys;
            if (step.choices != null && State.tribeIndex < step.choices.Count)
                GameRules.ApplyChoice(State, step, step.choices[State.tribeIndex]);
            State.identityComplete = true;
            AdvanceBiographyIndex();
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
                    SetStatus(ArticyNarrative.Copy("Copy_7ec44327191f5131"));
                    return;
                }
            }

            State.biographyComplete = true;
            State.phase = ArticyNarrative.Route("BiographyComplete",State);
            RecalculateMetrics();
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
                SetStatus(ArticyNarrative.Copy("Copy_1bdbf5eab98ae2e2"));
                return;
            }

            State.phase = "biography";
            string id = step.kind == BiographyStepKind.Family ? "bio-family" : step.kind == BiographyStepKind.Identity ? "bio-identity" : step.kind == BiographyStepKind.Oath ? "bio-oath" : "bio-choice";
            Show(id);
        }
    }
}
