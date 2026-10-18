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
        public void NewGame()
        {
            selectedSlot = -1;
            Show("slots-new");
        }

        public void LoadMenu()
        {
            selectedSlot = -1;
            Show("slots-load");
        }

        public void Settings()
        {
            Show("settings");
        }

        public void SelectSlot(int slot)
        {
            if (modalOpen || (currentScreen != "slots-new" && currentScreen != "slots-load"))
                return;
            DWSlotResult result = DWStore.Read(slot);
            bool allowed = currentScreen == "slots-new" ? result.status == DWSlotStatus.Empty : result.status == DWSlotStatus.Valid;
            if (!allowed)
            {
                SetStatus("Η θέση δεν είναι διαθέσιμη για αυτή την ενέργεια.");
                return;
            }

            selectedSlot = slot;
            RenderSlots();
            SetStatus("");
        }

        public void StartSelectedSlot()
        {
            if (modalOpen || selectedSlot < 0)
                return;
            DWSlotResult result = DWStore.Read(selectedSlot);
            if (currentScreen == "slots-new")
            {
                if (result.status != DWSlotStatus.Empty)
                {
                    RenderSlots();
                    return;
                }

                DWGameState fresh = NewState();
                if (!DWStore.Save(selectedSlot, fresh, out string error))
                {
                    SetStatus(error);
                    return;
                }

                DWSession.State = fresh;
                DWSession.Slot = selectedSlot;
                DWTelemetry.Capture(fresh, campaignContent);
                Show("prologue");
            }
            else if (currentScreen == "slots-load")
            {
                if (result.status != DWSlotStatus.Valid)
                {
                    RenderSlots();
                    return;
                }

                if (!Compatible(result.state))
                {
                    SetStatus("Η αποθήκευση δεν αντιστοιχεί σε αυτή την έκδοση του περιεχομένου.");
                    return;
                }

                DWSession.State = result.state;
                DWSession.Slot = selectedSlot;
                if (State.phase == "biography")
                    ShowBiography();
                else
                    Show(State.phase == "summary" ? "bio-summary" : State.phase);
            }
        }

        private bool Compatible(DWGameState state) => DWGameRules.Compatible(state, config);
        public void DeleteSlot(int slot)
        {
            if (modalOpen || currentScreen != "slots-load")
                return;
            DWSlotResult result = DWStore.Read(slot);
            if (result.status == DWSlotStatus.Empty || result.status == DWSlotStatus.Unavailable)
                return;
            pendingDelete = slot;
            exitConfirmation = false;
            OpenModal("Η αποθήκευση «" + (result.state != null ? result.state.playerName : "Θέση " + (slot + 1)) + "» στη θέση " + (slot + 1) + "\nθα διαγραφεί οριστικά.");
        }

        public void ConfirmDelete()
        {
            if (!modalOpen)
                return;
            if (returnToMenuConfirmation)
            {
                CancelModal();
                RestoreGameTime();
                settingsFromPause = false;
                DWStore.FlushWebStorage();
                Show("main-menu");
                return;
            }

            if (exitConfirmation)
            {
                CancelModal();
                if (!IsWeb)
                {
#if UNITY_EDITOR
                    Debug.Log("Έξοδος: στο κανονικό desktop build η εφαρμογή θα κλείσει.");
#else
                    Application.Quit();
#endif
                }

                return;
            }

            int slot = pendingDelete;
            if (slot < 0)
                return;
            bool deleted = DWStore.Delete(slot, out string error);
            CancelModal();
            if (deleted)
            {
                if (DWSession.Slot == slot)
                {
                    DWSession.State = null;
                    DWSession.Slot = -1;
                }

                selectedSlot = -1;
            }

            RenderSlots();
            SetStatus(deleted ? "Η Θέση " + (slot + 1) + " διαγράφηκε." : error);
        }

        private void OpenModal(string body)
        {
            GameObject modal = Get<GameObject>("modal.root");
            if (modal == null)
            {
                SetStatus("Δεν έχει συνδεθεί το παράθυρο επιβεβαίωσης.");
                return;
            }

            SetText("modal.body", body);
            SetText("modal.title", returnToMenuConfirmation ? "Αφήνεις προς το παρόν την πόλη;" : exitConfirmation ? "Έξοδος από το παιχνίδι;" : "Να σβήσει αυτή η διαδρομή;");
            SetText("modal.banner", returnToMenuConfirmation ? "ΕΠΙΣΤΡΟΦΗ ΣΤΟ ΜΕΝΟΥ" : exitConfirmation ? "ΕΞΟΔΟΣ" : "ΔΙΑΓΡΑΦΗ ΙΣΤΟΡΙΑΣ");
            SetText("modal.cancel.label", returnToMenuConfirmation ? "Παραμονή" : exitConfirmation ? "Ακύρωση" : "Διατήρηση");
            SetText("modal.confirm.label", returnToMenuConfirmation ? "Κύριο μενού" : exitConfirmation ? "Έξοδος" : "Διαγραφή");
            Button confirm = Get<Button>("modal.confirm");
            if (confirm != null && confirm.GetComponent<DWShape>() != null)
                confirm.GetComponent<DWShape>().color = (exitConfirmation || returnToMenuConfirmation) ? new Color32(185, 160, 102, 255) : new Color32(164, 86, 67, 255);
            TMP_Text label = Get<TMP_Text>("modal.confirm.label");
            if (label != null)
                label.color = (exitConfirmation || returnToMenuConfirmation) ? new Color32(36, 60, 61, 255) : new Color32(243, 232, 206, 255);
            if (roots.TryGetValue(currentScreen, out GameObject root))
            {
                modalBlockedGroup = root.GetComponent<CanvasGroup>();
                if (modalBlockedGroup != null)
                {
                    previousInteractable = modalBlockedGroup.interactable;
                    previousBlocksRaycasts = modalBlockedGroup.blocksRaycasts;
                    modalBlockedGroup.interactable = false;
                    modalBlockedGroup.blocksRaycasts = false;
                }
            }

            modal.SetActive(true);
            modalOpen = true;
            ClearSelection();
        }

        public void CancelModal()
        {
            SetActive("modal.root", false);
            if (modalBlockedGroup != null)
            {
                modalBlockedGroup.interactable = previousInteractable;
                modalBlockedGroup.blocksRaycasts = previousBlocksRaycasts;
                modalBlockedGroup = null;
            }

            ClearSelection();
            modalOpen = false;
            pendingDelete = -1;
            exitConfirmation = false;
            returnToMenuConfirmation = false;
        }

        private void AskExit()
        {
            if (IsWeb)
                return;
            exitConfirmation = true;
            pendingDelete = -1;
            OpenModal("Να κλείσει η εφαρμογή;");
        }

        public void OpenPause()
        {
            if (modalOpen || routing || drawingLottery || gamePaused || !roots.ContainsKey("pause"))
                return;
            if (currentScreen == "profile")
            {
                CloseProfile();
            }

            pauseReturnScreen = currentScreen;
            prePauseTimeScale = Time.timeScale;
            gamePaused = true;
            Time.timeScale = 0;
            Show("pause");
        }

        private void RestoreGameTime()
        {
            if (gamePaused)
                Time.timeScale = prePauseTimeScale;
            gamePaused = false;
        }

        public void ResumeGame()
        {
            if (modalOpen)
                return;
            RestoreGameTime();
            settingsFromPause = false;
            Show(pauseReturnScreen ?? "map-stela");
        }

        private void PauseSettings()
        {
            settingsFromPause = true;
            Show("settings");
        }

        private void RequestMainMenu()
        {
            returnToMenuConfirmation = true;
            exitConfirmation = false;
            OpenModal("Η πρόοδος έως το τελευταίο σημείο\nαυτόματης αποθήκευσης διατηρείται.");
        }

        public void OpenProfile()
        {
            if (modalOpen || routing || gamePaused || !roots.ContainsKey("profile"))
                return;
            profileReturnScreen = currentScreen;
            Show("profile");
        }

        private void CloseProfile()
        {
            if (modalOpen)
                return;
            Show(profileReturnScreen ?? "map-stela");
        }

        public void Back()
        {
            if (currentScreen == "settings" && settingsFromPause && !modalOpen)
            {
                DWStore.FlushWebStorage();
                Show("pause");
                return;
            }

            if (currentScreen == "pause" && !modalOpen)
            {
                ResumeGame();
                return;
            }

            if (currentScreen == "profile" && !modalOpen)
            {
                CloseProfile();
                return;
            }

            if (IsCampaignScreen(currentScreen) && !modalOpen)
            {
                GoCampaign("map-stela");
                return;
            }

            if (IsCityScreen(currentScreen) && !modalOpen)
            {
                Show(currentScreen == "complaint-detail" ? "complaints" : currentScreen == "official-detail" ? "offices" : "map-stela");
                return;
            }

            if (modalOpen)
            {
                CancelModal();
                return;
            }

            if (currentScreen == "settings")
                DWStore.FlushWebStorage();
            if (currentScreen == "prologue" && State != null && State.prologueIndex > 0)
            {
                State.prologueIndex--;
                SaveCheckpoint();
                RenderPrologue();
                return;
            }

            if (currentScreen != null && currentScreen.StartsWith("bio-", StringComparison.Ordinal) && currentScreen != "bio-summary" && State != null && State.biographyIndex > 0)
            {
                State.biographyIndex--;
                State.phase = "biography";
                SaveCheckpoint();
                ShowBiography();
                return;
            }

            Show("main-menu");
        }
    }
}
