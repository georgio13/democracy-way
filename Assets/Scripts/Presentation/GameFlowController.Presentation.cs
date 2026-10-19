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
        private void Render()
        {
            if (currentScreen == "main-menu")
                RenderMainMenu();
            else if (currentScreen == "slots-new" || currentScreen == "slots-load")
                RenderSlots();
            else if (currentScreen == "settings")
                RenderSettings();
            else if (currentScreen == "profile")
                RenderProfile();
            else if (currentScreen == "prologue")
                RenderPrologue();
            else if (currentScreen == "bio-summary")
                RenderSummary();
            else if (currentScreen.StartsWith("bio-", StringComparison.Ordinal))
                RenderBiography();
            else if (currentScreen == "prytany-title")
                RenderPrytany();
            else if (currentScreen == "lottery")
                RenderLottery();
            else if (currentScreen == "map-stela")
            {
                RenderMap();
                RenderCityBadges();
                RenderCampaignMap();
                RenderCityMetricsHUD();
                RenderEvaluationAccess();
            }
            else if (IsCityScreen(currentScreen))
                RenderCity();
            else if (IsCampaignScreen(currentScreen))
                RenderCampaign();
        }

        private void RenderMainMenu()
        {
            bool any = false;
            for (int i = 0; i < SaveStore.SlotCount; i++)
                any |= SaveStore.Read(i).status != SaveSlotStatus.Empty;
            SetInteractable("main.load", any); // Invalid saves remain accessible for deletion.
            SetActive("main.exit", !IsWeb);
            TMP_Text footer = Get<TMP_Text>("main.footer");
            if (footer != null)
            {
                Vector2 position = footer.rectTransform.anchoredPosition;
                position.y = -(IsWeb ? 269f : 349f) * 1.2f;
                footer.rectTransform.anchoredPosition = position;
            }
        }

        private void RenderSlots()
        {
            bool loading = currentScreen == "slots-load";
            string prefix = loading ? "slots-load" : "slots-new";
            SetText(prefix + ".eyebrow", loading ? ArticyNarrative.Copy("Copy_88432a37c69e06d6") : ArticyNarrative.Copy("Copy_b193aad902079130"));
            SetText(prefix + ".title", loading ? ArticyNarrative.Copy("Copy_0f6b2ffdfe795cf8") : ArticyNarrative.Copy("Copy_370c113c787a399b"));
            SetText(prefix + ".primary.label", loading ? ArticyNarrative.Copy("Copy_027b0b558dd1e26a") : ArticyNarrative.Copy("Copy_d4cf2992479f2dad"));
            bool allowedSelection = false, anyAvailable = false;
            for (int i = 0; i < SaveStore.SlotCount; i++)
            {
                SaveSlotResult result = SaveStore.Read(i);
                string key = prefix + ".slot." + i;
                bool valid = result.status == SaveSlotStatus.Valid, empty = result.status == SaveSlotStatus.Empty;
                bool allowed = loading ? valid : empty;
                bool chosen = selectedSlot == i && allowed;
                anyAvailable |= allowed;
                string name = valid ? result.state.playerName : empty ? ArticyNarrative.Copy("Copy_ae8812cabfe6d28c") : ArticyNarrative.Copy("Copy_aa1d0930e0ebc648");
                SetText(key + ".title", name);
                string detail = empty ? (loading ? ArticyNarrative.Copy("Copy_202c21be0bed7103") : ArticyNarrative.Copy("Copy_e46a7e190a1a355d")) : valid ? SlotStage(result.state) : ArticyNarrative.Copy("Copy_b576f5addc0d5a4b");
                if (valid && DateTime.TryParse(result.savedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime date))
                {
                    date = date.ToLocalTime();
                    string day = date.Date == DateTime.Today ? ArticyNarrative.Copy("Copy_cf2ea4c9b5150068") : date.Date == DateTime.Today.AddDays(-1) ? ArticyNarrative.Copy("Copy_f57c54b41ba935aa") : date.ToString("dd/MM/yyyy");
                    detail += "\n" + day + ", " + date.ToString("HH:mm");
                }

                SetText(key + ".detail", detail);
                SetInteractable(key, allowed);
                SetActive(key + ".selected", chosen);
                SetActive(key + ".portrait", valid);
                SetActive(key + ".filled", valid);
                SetActive(key + ".empty", !valid);
                SetActive(key + ".stamp", valid);
                SetActive(key + ".delete", !empty);
                SetInteractable(key + ".delete", loading && result.status != SaveSlotStatus.Unavailable);
                CanvasGroup fade = Get<CanvasGroup>(key + ".fade");
                if (fade != null)
                    fade.alpha = allowed ? 1f : .48f;
                Color ink = chosen ? new Color32(241, 230, 202, 255) : new Color32(36, 60, 61, 255);
                TMP_Text title = Get<TMP_Text>(key + ".title"), info = Get<TMP_Text>(key + ".detail"), cross = Get<TMP_Text>(key + ".delete.label");
                if (title != null)
                    title.color = ink;
                if (info != null)
                    info.color = chosen ? ink : new Color32(105, 112, 91, 255);
                if (cross != null)
                    cross.color = ink;
                Button delete = Get<Button>(key + ".delete");
                PanelGraphic deleteFrame = delete != null ? delete.GetComponent<PanelGraphic>() : null;
                if (deleteFrame != null)
                {
                    deleteFrame.borderColor = ink;
                    deleteFrame.border = 1.2f;
                    deleteFrame.cut = 0;
                    delete.transition = UnityEngine.UI.Selectable.Transition.None;
                    deleteFrame.canvasRenderer.SetColor(Color.white);
                    deleteFrame.SetVerticesDirty();
                }

                Button row = Get<Button>(key);
                PanelGraphic shape = row != null ? row.GetComponent<PanelGraphic>() : null;
                if (shape != null)
                    shape.color = chosen ? new Color32(35, 61, 64, 255) : new Color32(225, 213, 184, 255);
                if (selectedSlot == i)
                    allowedSelection = allowed;
            }

            SetInteractable(prefix + ".primary", allowedSelection);
            SetText(prefix + ".hint", loading ? ArticyNarrative.Copy("Copy_eaae7ae5917b8623") : anyAvailable ? ArticyNarrative.Copy("Copy_74e5162662e35f3f") : ArticyNarrative.Copy("Copy_f66c2696f5c74aff"));
        }

        private static string SlotStage(GameState state)
        {
            if (state.phase == "prologue")
                return ArticyNarrative.Copy("Copy_25b0ef0e5008174d");
            if (state.phase == "biography" || state.phase == "summary")
                return ArticyNarrative.Copy("Copy_1ca3a318398adcc3");
            string[] greek =
            {
                ArticyNarrative.Copy("Copy_89f9d2fb8432db42"),
                ArticyNarrative.Copy("Copy_98b57fb1137b35bd"),
                ArticyNarrative.Copy("Copy_f235b2d4e165b68e"),
                ArticyNarrative.Copy("Copy_a8dd9dae37132b84"),
                ArticyNarrative.Copy("Copy_5f826232605f221e"),
                ArticyNarrative.Copy("Copy_3c102fabd92cfdea"),
                ArticyNarrative.Copy("Copy_c7c43d61ccac1cb9"),
                ArticyNarrative.Copy("Copy_7dd80f6afc4ed10b"),
                ArticyNarrative.Copy("Copy_2dd5885ba989d7ee"),
                ArticyNarrative.Copy("Copy_07b1f0f313750d22")
            };
            return greek[Mathf.Clamp(state.prytany - 1, 0, 9)] + ArticyNarrative.Copy("Copy_9573ff89eebfbd15") + state.week + " / 5";
        }

        private void RenderPrologue()
        {
            EnsureState();
            State.prologueIndex = Mathf.Clamp(State.prologueIndex, 0, config.prologue.Count - 1);
            ProloguePage page = config.prologue[State.prologueIndex];
            SetText("prologue.title", page.title);
            SetText("prologue.body", page.body);
            SetText("prologue.step", (State.prologueIndex + 1) + " / " + config.prologue.Count);
            for (int i = 0; i < config.prologue.Count; i++)
                SetActive("prologue.page." + i, i == State.prologueIndex);
            TMP_Text narrative = Get<TMP_Text>("prologue.body");
            if (narrative != null)
            {
                // Each page fits inside the fixed reading area; no scrolling or clipping.
                narrative.enableAutoSizing = true;
                narrative.fontSizeMin = 26;
                narrative.fontSizeMax = 36;
                narrative.lineSpacing = 5;
                narrative.paragraphSpacing = 12;
                Canvas.ForceUpdateCanvases();
                narrative.ForceMeshUpdate();
            }

        }

        private void RenderBiography()
        {
            if (!string.IsNullOrEmpty(CurrentBioStep()?.selectionKind)) { RenderAuthoredBiography(); return; }
            BiographyStep step = CurrentBioStep();
            if (step == null)
                return;
            string progress = step.year;
            int milestone = step.kind == BiographyStepKind.Family ? 0 : step.id == "register" || step.kind == BiographyStepKind.Oath ? 2 : step.id == "training" || step.id == "guard" ? 3 : step.id == "adult" || step.id == "council" ? 4 : 1;
            for (int m = 0; m < 5; m++)
            {
                SetActive(currentScreen + ".milestone." + m, m == milestone);
                TMP_Text year = Get<TMP_Text>(currentScreen + ".milestone." + m + ".label");
                if (year != null)
                    year.color = m == milestone ? new Color32(255, 240, 203, 255) : new Color32(216, 222, 199, 255);
            }

            TMP_Text heading = Get<TMP_Text>(currentScreen + ".title");
            if (heading != null)
            {
                heading.enableAutoSizing = true;
                heading.fontSizeMax = 42;
                heading.fontSizeMin = 29;
            }

            RectTransform choices = Get<RectTransform>(currentScreen + ".choices");
            bool oath = step.kind == BiographyStepKind.Oath;
            int count = step.choices != null ? step.choices.Count : 0;
            if (choices != null && !oath)
            {
                choices.anchoredPosition = new Vector2(886 * 1.2f, -(count >= 4 ? 526 : 550) * 1.2f);
                choices.sizeDelta = new Vector2(636 * 1.2f, (count >= 4 ? 280 : 247) * 1.2f);
                var scroll = choices.GetComponent<ScrollRect>();
                if (scroll != null && scroll.content != null)
                {
                    const float rowHeight = 72f;
                    const float spacing = 10f;
                    for (int i = 0; i < 10; i++)
                    {
                        var option = Get<Button>(currentScreen + ".option." + i);
                        if (option == null) continue;
                        var row = option.GetComponent<RectTransform>();
                        row.anchorMin = new Vector2(0, 1);
                        row.anchorMax = Vector2.one;
                        row.pivot = new Vector2(.5f, 1);
                        row.anchoredPosition = new Vector2(0, -i * (rowHeight + spacing));
                        row.sizeDelta = new Vector2(0, rowHeight);
                    }
                    scroll.content.sizeDelta = new Vector2(0, Mathf.Max(0, count * (rowHeight + spacing) - spacing));
                }
            }

            foreach (string key in new[]
            {
                "bio.title",
                currentScreen + ".title"
            }

            )
                SetText(key, step.title);
            foreach (string key in new[]
            {
                "bio.body",
                currentScreen + ".body"
            }

            )
                SetText(key, step.body);
            foreach (string key in new[]
            {
                "bio.step",
                currentScreen + ".step"
            }

            )
                SetText(key, progress);
            for (int i = 0; i < config.biography.Count; i++)
            {
                SetText("bio.timeline." + i + ".year", config.biography[i].year);
                SetActive("bio.timeline." + i + ".current", i == State.biographyIndex);
                SetText(currentScreen + ".timeline." + i + ".year", config.biography[i].year);
                SetActive(currentScreen + ".timeline." + i + ".current", i == State.biographyIndex);
            }

            SetImage("bio.background", step.background);
            SetImage(currentScreen + ".background", step.background);
            ChoiceRecord selected = State.choices.Find(x => x.stepId == step.id);
            for (int i = 0; i < 10; i++)
            {
                bool exists = step.choices != null && i < step.choices.Count;
                foreach (string key in new[]
                {
                    "bio.option." + i,
                    currentScreen + ".option." + i
                }

                )
                {
                    SetActive(key, exists);
                    if (exists)
                    {
                        SetText(key + ".label", step.choices[i].text);
                        SetActive(key + ".metrics", false);
                        bool isSelected = selected != null && selected.choiceId == step.choices[i].id;
                        SetActive(key + ".selected", isSelected);
                        TMP_Text optionLabel = Get<TMP_Text>(key + ".label");
                        Button optionButton = Get<Button>(key);
                        Transform badge = optionButton != null ? optionButton.transform.Find("Choice index") : null;
                        if (badge != null)
                        {
                            PanelGraphic badgeShape = badge.GetComponent<PanelGraphic>();
                            if (badgeShape != null)
                                badgeShape.color = isSelected ? new Color32(104, 120, 87, 255) : Color.clear;
                            TMP_Text letter = badge.GetComponentInChildren<TMP_Text>();
                            if (letter != null)
                            {
                                letter.text = count > 4 ? (i + 1).ToString() : "ΑΒΓΔ"[i].ToString();
                                letter.color = isSelected ? new Color32(232, 224, 199, 255) : new Color32(198, 175, 118, 255);
                            }
                        }

                        if (optionLabel != null)
                            optionLabel.color = isSelected ? new Color32(36, 60, 61, 255) : new Color32(241, 230, 202, 255);
                    }
                }
            }

            SetInteractable(currentScreen + ".primary", selected != null);
            int[] metricValues =
            {
                State.metrics.consistency,
                State.metrics.ethics,
                State.metrics.authority,
                State.metrics.persuasion,
                State.metrics.parrhesia,
                State.metrics.recognition
            };
            for (int i = 0; i < 6; i++)
                SetText(currentScreen + ".metric." + i, metricValues[i].ToString());
            if (lastRenderedBioStep != step.id)
            {
                Canvas.ForceUpdateCanvases();
                if (roots.TryGetValue(currentScreen, out GameObject activeRoot))
                    foreach (var scroll in activeRoot.GetComponentsInChildren<ScrollRect>(true))
                        if (scroll.content != null)
                            scroll.verticalNormalizedPosition = 1;
                lastRenderedBioStep = step.id;
            }
        }

        private void RenderSummary()
        {
            EnsureState();
            SetText("summary.body", State.playerName + "\n" + State.playerAge + ArticyNarrative.Copy("Copy_b0ff9eb7a58bf3d6") + TribeName(State.tribeIndex) + "\n" + config.economicClasses[Mathf.Clamp(State.economicClassIndex, 0, config.economicClasses.Count - 1)] + ArticyNarrative.Copy("Copy_72b5e49b1563ea57") + State.deme + ArticyNarrative.Copy("Copy_a90744770635bb27") + TrittysLabel() + "\n" + State.profession + "\n" + State.offices);
            SetText("summary.metrics", State.metrics.ToGreekLines());
            SetText("summary.name", State.playerName);
            SetText("summary.age", ArticyNarrative.Copy("Copy_d27fcffa71ab967e") + State.playerAge + ArticyNarrative.Copy("Copy_750d677c988d9482"));
            SetText("summary.identity.0", "Φυλή: " + TribeName(State.tribeIndex) + "\nΔήμος: " + State.deme + "\nΤριττύα: " + TrittysLabel());
            SetText("summary.identity.1", State.profession + "\n" + config.economicClasses[Mathf.Clamp(State.economicClassIndex, 0, config.economicClasses.Count - 1)]);
            SetText("summary.identity.2", (State.offices ?? "").Replace(" · ", "\n"));
            for (int i = 0; i < 3; i++)
            {
                var identity = Get<TMP_Text>("summary.identity." + i);
                if (identity == null) continue;
                identity.enableAutoSizing = true;
                identity.fontSizeMin = 19;
                identity.fontSizeMax = 21.6f;
                identity.lineSpacing = 8;
                identity.verticalAlignment = VerticalAlignmentOptions.Top;
            }
            SetImage("summary.emblem", config.tribes[State.tribeIndex].emblem);
            int[] values =
            {
                State.metrics.consistency,
                State.metrics.ethics,
                State.metrics.authority,
                State.metrics.persuasion,
                State.metrics.parrhesia,
                State.metrics.recognition
            };
            for (int i = 0; i < values.Length; i++)
            {
                SetText("summary.metric." + i, values[i].ToString());
                Slider bar = Get<Slider>("summary.metric." + i + ".bar");
                if (bar != null)
                    bar.SetValueWithoutNotify(Mathf.Lerp(bar.minValue, bar.maxValue, values[i] / 100f));
            }
        }

        private void RenderPrytany()
        {
            audioSettings?.ChangeMusic(config.MusicForPrytany(State?.prytany ?? 1));
            EnsureState();
            string[] ordinals =
            {
                ArticyNarrative.Copy("Copy_5c46fb5ce3c0a458"),
                ArticyNarrative.Copy("Copy_8509d0fc5943c94c"),
                ArticyNarrative.Copy("Copy_d4f1082945860c05"),
                ArticyNarrative.Copy("Copy_5f489244eac49453"),
                ArticyNarrative.Copy("Copy_0b6a7606784364a7"),
                ArticyNarrative.Copy("Copy_1adbd881e87894f4"),
                ArticyNarrative.Copy("Copy_e05547ea8d704479"),
                ArticyNarrative.Copy("Copy_d336b8d752196427"),
                ArticyNarrative.Copy("Copy_84ed3f352e51672f"),
                ArticyNarrative.Copy("Copy_ba8676935e1bf943")
            };
            string[] roman =
            {
                "I",
                "II",
                "III",
                "IV",
                "V",
                "VI",
                "VII",
                "VIII",
                "IX",
                "X"
            };
            int chapter = Mathf.Clamp(State.prytany - 1, 0, 9);
            SetText("prytany.title", ArticyNarrative.Copy("Copy_3e3295844f841450").TrimEnd() + " " + ordinals[chapter].Trim() + " " + ArticyNarrative.Copy("Copy_8ba25509b337198d").TrimStart());
            SetText("prytany.roman", roman[chapter]);
            SetText("prytany.tribe", State.currentTribe >= 0 ? TribeName(State.currentTribe).ToUpperInvariant() + ArticyNarrative.Copy("Copy_0318871c2b7b5380") : ArticyNarrative.Copy("Copy_8b44729931e24eab"));
            SetText("prytany.footer", ArticyNarrative.Copy("Copy_228a792defd35fbf") + State.week + ArticyNarrative.Copy("Copy_8177807e48b3c5c3"));
            DemocracyQuote quote = config.prytanyQuotes.Count > 0 ? config.prytanyQuotes[(State.prytany - 1) % config.prytanyQuotes.Count] : null;
            SetText("prytany.quote", quote != null ? quote.text : "");
            SetText("prytany.attribution", quote != null ? (quote.attribution ?? "").Split('·')[0].Trim() : "");
            TMP_Text body = Get<TMP_Text>("prytany.quote"), source = Get<TMP_Text>("prytany.attribution");
            if (body != null && source != null)
            {
                source.fontSize = 27;
                source.enableAutoSizing = true; source.fontSizeMin = 24; source.fontSizeMax = 27;
                body.enableAutoSizing = true; body.fontSizeMin = 25; body.fontSizeMax = 36;
                CivicUIFactory.Box(body.rectTransform, 360, 658, 1200, 154);
                body.alignment = TextAlignmentOptions.Center;
                CivicUIFactory.Box(source.rectTransform, 360, 838, 1200, 42);
                source.alignment = TextAlignmentOptions.Center;
            }
        }

        private void RenderLottery()
        {
            EnsureState();
            SetText("lottery.body", State.currentTribe < 0 ? ArticyNarrative.Copy("Copy_f98d9d533ac8b805") : ArticyNarrative.Copy("Copy_c1f733670aebd7d7") + TribeName(State.currentTribe) + ".");
            for (int i = 0; i < 10; i++)
            {
                string status = State.currentTribe == i ? ArticyNarrative.Copy("Copy_a21b664817565e8c") : State.drawnTribes.Contains(i) ? ArticyNarrative.Copy("Copy_0008e0f7f5eda78f") + (State.drawnTribes.IndexOf(i) + 1).ToString("00") : ArticyNarrative.Copy("Copy_7961be8330cfef48");
                SetText("lottery.tribe." + i, status);
                CanvasGroup fade = Get<CanvasGroup>("lottery.tribe." + i + ".fade");
                if (fade != null)
                    fade.alpha = State.drawnTribes.Contains(i) && State.currentTribe != i ? .45f : 1f;
                SetActive("lottery.tribe." + i + ".selected", State.currentTribe == i);
                SetActive("lottery.tribe." + i + ".used", State.drawnTribes.Contains(i) && State.currentTribe != i);
            }

            SetText("lottery.result", State.currentTribe >= 0 ? TribeName(State.currentTribe) + ArticyNarrative.Copy("Copy_cebe6ca122c440f7") : ArticyNarrative.Copy("Copy_83229ddd6f5397d0"));
            SetText("lottery.primary.label", State.currentTribe >= 0 ? ArticyNarrative.Copy("Copy_f3fc4caef743a68e") : ArticyNarrative.Copy("Copy_7e810e5a6a10a819"));
            SetInteractable("lottery.primary", State.currentTribe >= 0 || State.drawnTribes.Count < 10);
            SetInteractable("lottery.begin", State.currentTribe >= 0);
        }

        private void RenderMap()
        {
            EnsureState();
            SetText("map.status", ArticyNarrative.Copy("Copy_0008e0f7f5eda78f") + State.prytany + ArticyNarrative.Copy("Copy_8ba8e7d0292e8747") + State.week + " / 5");
            SetText("map.week", State.week + " / 5");
            SetText("map.identity", State.playerName);
            SetText("map.office.label", State.offices);
            SetText("map.city", State.cityFunds.ToString("N0") + " οβ.");
            SetText("map.personal", State.personalFunds.ToString("N0") + " οβ.");
            string[] prytanyNumerals = { "Α΄", "Β΄", "Γ΄", "Δ΄", "Ε΄", "ΣΤ΄", "Ζ΄", "Η΄", "Θ΄", "Ι΄" };
            SetText("map.prytany", prytanyNumerals[Mathf.Clamp(State.prytany - 1, 0, 9)] + "\nπρυτανεία");
            SetText("map.week", State.week + " / 5");
            for (int i = 0; i < 10; i++)
            {
                var tick = Get<PanelGraphic>("map.tick." + i);
                if (tick != null)
                    tick.color = i == State.prytany - 1 ? new Color32(23, 43, 48, 255) : i < State.prytany - 1 ? new Color32(115, 128, 93, 255) : new Color32(181, 178, 139, 255);
            }

            for (int i = 0; i < 5; i++)
            {
                var tick = Get<PanelGraphic>("map.bead." + i);
                if (tick != null)
                    tick.color = i == State.week - 1 ? new Color32(232, 223, 203, 255) : i < State.week - 1 ? new Color32(180, 154, 98, 255) : new Color32(66, 93, 83, 255);
            }

            SetText("map.funds", ArticyNarrative.Copy("Copy_047651c755e3bfc4") + State.cityFunds.ToString("N0") + ArticyNarrative.Copy("Copy_a056bcffd38f7391") + State.personalFunds.ToString("N0") + " οβ.");
            SetText("map.tribe", TribeName(State.currentTribe));
            SetImage("map.tribe.emblem", State.currentTribe >= 0 ? config.tribes[State.currentTribe].emblem : null);
            SetImage("map.player.emblem", config.tribes[State.tribeIndex].emblem);
            SetText("map.notice", ArticyNarrative.Copy("Copy_2aede956f376db8b"));
        }

        private void RenderProfile()
        {
            EnsureState();
            SetText("profile.heading", State.playerName);
            SetText("profile.name", State.playerName);
            SetText("profile.age", State.playerAge + ArticyNarrative.Copy("Copy_b0ff9eb7a58bf3d6") + State.profession);
            SetText("profile.origin", State.deme + " · " + TribeName(State.tribeIndex));
            SetImage("profile.emblem", config.tribes[State.tribeIndex].emblem);
            SetText("profile.funds", ArticyNarrative.Copy("Copy_9fd25730f3bc1ab9") + State.cityFunds.ToString("N0") + ArticyNarrative.Copy("Copy_9bc90cc634c1c0ff") + State.personalFunds.ToString("N0") + " οβ.");
            string[] identity =
            {
                TribeName(State.tribeIndex),
                State.deme,
                TrittysLabel(),
                config.economicClasses[Mathf.Clamp(State.economicClassIndex, 0, config.economicClasses.Count - 1)]
            };
            for (int i = 0; i < 4; i++)
                SetText("profile.identity." + i, identity[i]);
            string[] offices = (State.offices ?? "").Split(new[] { " · " }, StringSplitOptions.None);
            for (int i = 0; i < 2; i++)
                SetText("profile.office." + i, i < offices.Length ? offices[i] : "—");
            int[] metrics =
            {
                State.metrics.consistency,
                State.metrics.ethics,
                State.metrics.authority,
                State.metrics.persuasion,
                State.metrics.parrhesia,
                State.metrics.recognition
            };
            for (int i = 0; i < 6; i++)
            {
                SetText("profile.metric." + i, metrics[i].ToString());
                Slider bar = Get<Slider>("profile.metric." + i + ".bar");
                if (bar != null)
                    bar.SetValueWithoutNotify(metrics[i]);
            }
        }

        private string TribeName(int index)
        {
            return index >= 0 && index < config.tribes.Count ? config.tribes[index].name : "—";
        }

    }
}




