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
    public sealed partial class DWApp
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

            if (State.prologueIndex + 1 < config.prologue.Count)
            {
                State.prologueIndex++;
                SaveCheckpoint();
                RenderPrologue();
            }
            else
            {
                State.phase = "biography";
                State.biographyIndex = 0;
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
            DWBioStep step = CurrentBioStep();
            if (step == null || step.kind == DWBioKind.Identity || step.choices == null || choiceIndex < 0 || choiceIndex >= step.choices.Count)
                return;
            DWChoice choice = step.choices[choiceIndex];
            if (choice == null || string.IsNullOrWhiteSpace(choice.id))
                return;
            DWGameRules.ApplyChoice(State, step, choice);
            SaveCheckpoint();
            RenderBiography();
            SetText("bio.feedback", choice.consequence);
            SetText(currentScreen + ".feedback", choice.consequence);
        }

        public void AdvanceBio()
        {
            if (modalOpen || State == null || State.phase != "biography")
                return;
            DWBioStep step = CurrentBioStep();
            if (step == null)
                return;
            if (step.kind == DWBioKind.Identity)
            {
                FinishIdentity();
                return;
            }

            if (!State.choices.Exists(x => x.stepId == step.id))
            {
                SetStatus("Επίλεξε πρώτα τη στάση του χαρακτήρα σου.");
                return;
            }

            AdvanceBiographyIndex();
        }

        private void AdvanceBiographyIndex()
        {
            if (State.biographyIndex + 1 < config.biography.Count)
            {
                State.biographyIndex++;
                SaveCheckpoint();
                ShowBiography();
            }
            else
            {
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
            DWBioStep step = CurrentBioStep();
            if (step == null || step.kind != DWBioKind.Identity)
                return;
            TMP_InputField name = Get<TMP_InputField>("identity.name");
            string value = name != null ? name.text : State.playerName;
            value = (value ?? "").Trim().Replace("<", "").Replace(">", "").Replace("\n", " ").Replace("\r", " ");
            if (string.IsNullOrWhiteSpace(value))
            {
                SetStatus("Συμπλήρωσε το όνομα του χαρακτήρα σου.");
                return;
            }

            State.playerName = value.Length > 40 ? value.Substring(0, 40) : value;
            TMP_Dropdown tribe = Get<TMP_Dropdown>("identity.tribe");
            TMP_Dropdown economicClass = Get<TMP_Dropdown>("identity.class");
            State.tribeIndex = tribe != null ? Mathf.Clamp(tribe.value, 0, config.tribes.Count - 1) : State.tribeIndex;
            State.economicClassIndex = step.editEconomicClass && economicClass != null ? Mathf.Clamp(economicClass.value, 0, config.economicClasses.Count - 1) : State.economicClassIndex;
            DWTribe selectedTribe = config.tribes[State.tribeIndex];
            State.deme = string.IsNullOrEmpty(selectedTribe.deme) ? State.deme : selectedTribe.deme;
            State.trittys = string.IsNullOrEmpty(selectedTribe.trittys) ? State.trittys : selectedTribe.trittys;
            if (step.choices != null && State.tribeIndex < step.choices.Count)
                DWGameRules.ApplyChoice(State, step, step.choices[State.tribeIndex]);
            State.identityComplete = true;
            AdvanceBiographyIndex();
        }

        public void FinishBio()
        {
            if (modalOpen || State == null || State.phase != "summary")
                return;
            for (int i = 0; i < config.biography.Count; i++)
            {
                DWBioStep step = config.biography[i];
                bool complete = step.kind == DWBioKind.Identity ? State.identityComplete : State.choices.Exists(x => x.stepId == step.id);
                if (!complete)
                {
                    State.biographyIndex = i;
                    State.phase = "biography";
                    ShowBiography();
                    SetStatus("Ολοκλήρωσε αυτό το βήμα πριν ξεκινήσει η πρυτανεία.");
                    return;
                }
            }

            State.biographyComplete = true;
            State.phase = "prytany-title";
            RecalculateMetrics();
            SaveCheckpoint();
            Show("prytany-title");
        }

        private DWBioStep CurrentBioStep()
        {
            return State != null && State.biographyIndex >= 0 && State.biographyIndex < config.biography.Count ? config.biography[State.biographyIndex] : null;
        }

        private void ShowBiography()
        {
            EnsureState();
            DWBioStep step = CurrentBioStep();
            if (step == null)
            {
                SetStatus("Δεν βρέθηκε το βιογραφικό βήμα.");
                return;
            }

            State.phase = "biography";
            string id = step.kind == DWBioKind.Family ? "bio-family" : step.kind == DWBioKind.Identity ? "bio-identity" : step.kind == DWBioKind.Oath ? "bio-oath" : "bio-choice";
            Show(id);
        }
    }
}
