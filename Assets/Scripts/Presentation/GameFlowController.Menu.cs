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
            SaveSlotResult result = SaveStore.Read(slot);
            bool allowed = currentScreen == "slots-new" ? result.status == SaveSlotStatus.Empty : result.status == SaveSlotStatus.Valid;
            if (!allowed)
            {
                SetStatus(JsonContent.Copy("Copy_7d2123a2aa37e008"));
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
            SaveSlotResult result = SaveStore.Read(selectedSlot);
            if (currentScreen == "slots-new")
            {
                if (result.status != SaveSlotStatus.Empty)
                {
                    RenderSlots();
                    return;
                }

                GameState fresh = NewState();
                // Το αρχικό τεστ γνώσεων (και οι τάσεις) γίνεται πριν από τον πρόλογο και τη βιογραφία:
                // η βιογραφία διδάσκει απαντήσεις του τεστ και μετρά ως μέρος της παρέμβασης.
                fresh.phase = "initial-knowledge";
                if (!SaveStore.Save(selectedSlot, fresh, out string error))
                {
                    SetStatus(error);
                    return;
                }

                GameSession.State = fresh;
                GameSession.Slot = selectedSlot;
                ResearchTelemetry.Capture(fresh);
                Show(fresh.phase);
            }
            else if (currentScreen == "slots-load")
            {
                if (result.status != SaveSlotStatus.Valid)
                {
                    RenderSlots();
                    return;
                }

                if (!Compatible(result.state))
                {
                    SetStatus(JsonContent.Copy("Copy_5f96ebff952a90d9"));
                    return;
                }

                GameSession.State = result.state;
                GameSession.Slot = selectedSlot;
                if (State.phase == "biography")
                    ShowBiography();
                else
                    Show(State.phase == "summary" ? "bio-summary" : State.phase);
            }
        }

        private bool Compatible(GameState state) => GameRules.Compatible(state, config);
        public void DeleteSlot(int slot)
        {
            if (modalOpen || currentScreen != "slots-load")
                return;
            SaveSlotResult result = SaveStore.Read(slot);
            if (result.status == SaveSlotStatus.Empty || result.status == SaveSlotStatus.Unavailable)
                return;
            pendingDelete = slot;
            exitConfirmation = false;
            OpenModal(JsonContent.Copy("Copy_d89d468447c2ade2") + (result.state != null ? result.state.playerName : JsonContent.Copy("Copy_feb8809653ec7d20") + (slot + 1)) + JsonContent.Copy("Copy_b06541c79ca58aee") + (slot + 1) + JsonContent.Copy("Copy_c99911bb15cc693a"));
        }

        public void ConfirmDelete()
        {
            if (!modalOpen)
                return;
            if (mapGuideConfirmation)
            {
                State.mapGuideSeen = true;
                SaveCheckpoint();
                CancelModal();
                return;
            }
            if (marketGateConfirmation)
            {
                CancelModal();
                OpenMarket();
                return;
            }

            if (returnToMenuConfirmation)
            {
                CancelModal();
                RestoreGameTime();
                settingsFromPause = false;
                SaveStore.FlushWebStorage();
                Show("main-menu");
                return;
            }

            if (exitConfirmation)
            {
                CancelModal();
                if (!IsWeb)
                {
#if UNITY_EDITOR
                    Debug.Log(JsonContent.Copy("Copy_c546d6ae1f2ab985"));
#else
                    Application.Quit();
#endif
                }

                return;
            }

            int slot = pendingDelete;
            if (slot < 0)
                return;
            bool deleted = SaveStore.Delete(slot, out string error);
            CancelModal();
            if (deleted)
            {
                if (GameSession.Slot == slot)
                {
                    GameSession.State = null;
                    GameSession.Slot = -1;
                }

                selectedSlot = -1;
            }

            RenderSlots();
            SetStatus(deleted ? JsonContent.Copy("Copy_56fdc3548a43471e") + (slot + 1) + JsonContent.Copy("Copy_08437fd7bef254cd") : error);
        }

        private void OpenModal(string body)
        {
            GameObject modal = Get<GameObject>("modal.root");
            if (modal == null)
            {
                SetStatus(JsonContent.Copy("Copy_26d76bc8ac61da84"));
                return;
            }

            SetText("modal.body", body);
            SetText("modal.title", marketGateConfirmation ? JsonContent.Copy("Copy_3a91c4e07b25d8f6") : returnToMenuConfirmation ? JsonContent.Copy("Copy_3b7d2b4c89341818") : exitConfirmation ? JsonContent.Copy("Copy_3c425a1d54d29c48") : JsonContent.Copy("Copy_08e52a462c5e8087"));
            SetText("modal.banner", marketGateConfirmation ? JsonContent.Copy("Copy_84b5d2f6a1c93e07") : returnToMenuConfirmation ? JsonContent.Copy("Copy_0fbdf139f32fbe03") : exitConfirmation ? JsonContent.Copy("Copy_9e9d8f4e2e474fdd") : JsonContent.Copy("Copy_dfa7abed718a9679"));
            SetText("modal.cancel.label", marketGateConfirmation ? JsonContent.Copy("Copy_b07f3c58d94a21e6") : returnToMenuConfirmation ? JsonContent.Copy("Copy_98fa5c1c9102ad29") : exitConfirmation ? JsonContent.Copy("Copy_c094b97741b41a20") : JsonContent.Copy("Copy_24281287fc92ad20"));
            SetText("modal.confirm.label", marketGateConfirmation ? JsonContent.Copy("Copy_c6d8e94f07b3a215") : returnToMenuConfirmation ? JsonContent.Copy("Copy_8394cf88f71e8118") : exitConfirmation ? JsonContent.Copy("Copy_6a5920d4ffad7a42") : JsonContent.Copy("Copy_650a9a115575ccac"));
            bool gentle = exitConfirmation || returnToMenuConfirmation || marketGateConfirmation || mapGuideConfirmation;
            Button confirm = Get<Button>("modal.confirm");
            if (confirm != null && confirm.GetComponent<PanelGraphic>() != null)
                confirm.GetComponent<PanelGraphic>().color = gentle ? new Color32(185, 160, 102, 255) : new Color32(164, 86, 67, 255);
            TMP_Text label = Get<TMP_Text>("modal.confirm.label");
            if (label != null)
                label.color = gentle ? new Color32(36, 60, 61, 255) : new Color32(243, 232, 206, 255);
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
            if (mapGuideConfirmation)
                RestoreModalLayout();
            mapGuideConfirmation = false;
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
            marketGateConfirmation = false;
        }

        // ---------- Οδηγός χάρτη: μία φορά, μετά την κλήρωση της πρώτης πρυτανείας ----------

        readonly List<(RectTransform rect, Vector2 position, Vector2 size)> modalLayout = new List<(RectTransform, Vector2, Vector2)>();
        (TextAlignmentOptions alignment, bool autoSize, float size, float min, float max, TextWrappingModes wrapping) modalMessageStyle;

        /// <summary>Ο χάρτης της πρώτης πρυτανείας ανοίγει με τις οδηγίες του παιχνιδιού.</summary>
        void ShowMapGuideIfNeeded()
        {
            if (modalOpen || State == null || State.mapGuideSeen || State.prytany != 1 || currentScreen != "map-stela")
                return;
            mapGuideConfirmation = true;
            pendingDelete = -1;
            OpenModal(JsonContent.Copy("Copy_8f3c6a1e52d09b74"));
            SetText("modal.title", JsonContent.Copy("Copy_a52e7d90c3f1b486"));
            SetText("modal.banner", JsonContent.Copy("Copy_e1b94f27d60a3c85"));
            SetText("modal.confirm.label", JsonContent.Copy("Copy_7c05d3b8e9a24f61"));
            EnlargeModalForGuide();
        }

        /// <summary>Το ConfirmationModal της σκηνής είναι φτιαγμένο για μία πρόταση (κείμενο 718×106):
        /// για τον οδηγό μεγαλώνει προσωρινά, χωρίς «Ακύρωση», και επανέρχεται στο κλείσιμο.</summary>
        void EnlargeModalForGuide()
        {
            var title = Get<TMP_Text>("modal.title");
            var message = Get<TMP_Text>("modal.body");
            var confirm = Get<Button>("modal.confirm");
            var bannerLabel = Get<TMP_Text>("modal.banner");
            if (title == null || message == null || confirm == null) return;
            var content = title.rectTransform.parent as RectTransform;
            var paper = content != null ? content.parent as RectTransform : null;
            var artwork = paper != null ? paper.Find("Parchment artwork") as RectTransform : null;
            var banner = bannerLabel != null ? bannerLabel.rectTransform.parent as RectTransform : null;
            const float width = 1200f, height = 780f, inner = width - 86.4f;
            modalLayout.Clear();
            void Set(RectTransform rect, Vector2 position, Vector2 size)
            {
                if (rect == null) return;
                modalLayout.Add((rect, rect.anchoredPosition, rect.sizeDelta));
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
            }
            Set(paper, new Vector2((1920f - width) / 2f, -(1080f - height) / 2f), new Vector2(width, height));
            Set(artwork, artwork != null ? artwork.anchoredPosition : Vector2.zero, new Vector2(width, height));
            Set(content, content.anchoredPosition, new Vector2(inner, height - 86.4f));
            Set(banner, banner != null ? new Vector2((inner - banner.sizeDelta.x) / 2f, banner.anchoredPosition.y) : Vector2.zero, banner != null ? banner.sizeDelta : Vector2.zero);
            Set(title.rectTransform, title.rectTransform.anchoredPosition, new Vector2(inner, title.rectTransform.sizeDelta.y));
            Set(message.rectTransform, new Vector2(0f, -84f), new Vector2(inner, 500f));
            var confirmRect = (RectTransform)confirm.transform;
            Set(confirmRect, new Vector2((inner - confirmRect.sizeDelta.x) / 2f, -610f), confirmRect.sizeDelta);
            modalMessageStyle = (message.alignment, message.enableAutoSizing, message.fontSize, message.fontSizeMin, message.fontSizeMax, message.textWrappingMode);
            message.alignment = TextAlignmentOptions.TopLeft;
            message.textWrappingMode = TextWrappingModes.Normal;
            message.enableAutoSizing = true;
            message.fontSizeMin = 18f;
            message.fontSizeMax = 26f;
            SetActive("modal.cancel", false);
        }

        void RestoreModalLayout()
        {
            foreach (var (rect, position, size) in modalLayout)
            {
                if (rect == null) continue;
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
            }
            modalLayout.Clear();
            var message = Get<TMP_Text>("modal.body");
            if (message != null)
            {
                message.alignment = modalMessageStyle.alignment;
                message.textWrappingMode = modalMessageStyle.wrapping;
                message.enableAutoSizing = modalMessageStyle.autoSize;
                message.fontSize = modalMessageStyle.size;
                message.fontSizeMin = modalMessageStyle.min;
                message.fontSizeMax = modalMessageStyle.max;
            }
            SetActive("modal.cancel", true);
        }

        private void AskExit()
        {
            if (IsWeb)
                return;
            exitConfirmation = true;
            pendingDelete = -1;
            OpenModal(JsonContent.Copy("Copy_9b8a2f9e7df301f6"));
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
            OpenModal(JsonContent.Copy("Copy_2abd78f173e97ef8"));
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
                SaveStore.FlushWebStorage();
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

            if (IsCityScreen(currentScreen) && !modalOpen)
            {
                Show(currentScreen == "complaint-detail" ? "complaints" : currentScreen == "official-detail" ? "offices" : "map-stela");
                return;
            }

            if (IsCampaignScreen(currentScreen) && !modalOpen)
            {
                GoCampaign("map-stela");
                return;
            }

            if (modalOpen)
            {
                CancelModal();
                return;
            }

            if (currentScreen == "settings")
                SaveStore.FlushWebStorage();
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
