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
            }
            else if (IsCityScreen(currentScreen))
                RenderCity();
            else if (IsCampaignScreen(currentScreen))
                RenderCampaign();
        }

        private void RenderMainMenu()
        {
            bool any = false;
            for (int i = 0; i < DWStore.SlotCount; i++)
                any |= DWStore.Read(i).status != DWSlotStatus.Empty;
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
            SetText(prefix + ".eyebrow", loading ? "ΕΠΙΣΤΡΟΦΗ ΣΤΗΝ ΠΟΛΗ" : "ΜΙΑ ΝΕΑ ΑΡΧΗ");
            SetText(prefix + ".title", loading ? "Φόρτωση παιχνιδιού" : "Νέο παιχνίδι");
            SetText(prefix + ".primary.label", loading ? "Συνέχεια ›" : "Έναρξη ›");
            bool allowedSelection = false, anyAvailable = false;
            for (int i = 0; i < DWStore.SlotCount; i++)
            {
                DWSlotResult result = DWStore.Read(i);
                string key = prefix + ".slot." + i;
                bool valid = result.status == DWSlotStatus.Valid, empty = result.status == DWSlotStatus.Empty;
                bool allowed = loading ? valid : empty;
                bool chosen = selectedSlot == i && allowed;
                anyAvailable |= allowed;
                string name = valid ? result.state.playerName : empty ? "Κενή θέση" : "Μη διαθέσιμη θέση";
                SetText(key + ".title", name);
                string detail = empty ? (loading ? "Δεν υπάρχει αποθηκευμένο παιχνίδι." : "Μια νέα ιστορία περιμένει.") : valid ? SlotStage(result.state) : "Μη συμβατή ή κατεστραμμένη αποθήκευση";
                if (valid && DateTime.TryParse(result.savedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime date))
                {
                    date = date.ToLocalTime();
                    string day = date.Date == DateTime.Today ? "Σήμερα" : date.Date == DateTime.Today.AddDays(-1) ? "Χθες" : date.ToString("dd/MM/yyyy");
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
                SetInteractable(key + ".delete", loading && result.status != DWSlotStatus.Unavailable);
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
                DWShape deleteFrame = delete != null ? delete.GetComponent<DWShape>() : null;
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
                DWShape shape = row != null ? row.GetComponent<DWShape>() : null;
                if (shape != null)
                    shape.color = chosen ? new Color32(35, 61, 64, 255) : new Color32(225, 213, 184, 255);
                if (selectedSlot == i)
                    allowedSelection = allowed;
            }

            SetInteractable(prefix + ".primary", allowedSelection);
            SetText(prefix + ".hint", loading ? "Διάλεξε την ιστορία που θέλεις να συνεχίσεις." : anyAvailable ? "Επίλεξε μία κενή θέση για τη δική σου ιστορία." : "Όλες οι θέσεις είναι κατειλημμένες. Ελευθέρωσε μία από τη Φόρτωση.");
        }

        private static string SlotStage(DWGameState state)
        {
            if (state.phase == "prologue")
                return "Πρόλογος";
            if (state.phase == "biography" || state.phase == "summary")
                return "Η ζωή σου · Βιογραφία";
            string[] greek =
            {
                "Α΄",
                "Β΄",
                "Γ΄",
                "Δ΄",
                "Ε΄",
                "ΣΤ΄",
                "Ζ΄",
                "Η΄",
                "Θ΄",
                "Ι΄"
            };
            return greek[Mathf.Clamp(state.prytany - 1, 0, 9)] + " πρυτανεία · Εβδομάδα " + state.week + " / 5";
        }

        private void RenderPrologue()
        {
            EnsureState();
            State.prologueIndex = Mathf.Clamp(State.prologueIndex, 0, config.prologue.Count - 1);
            DWPage page = config.prologue[State.prologueIndex];
            SetText("prologue.title", page.title);
            SetText("prologue.body", page.body);
            SetText("prologue.source", page.sourceNote);
            SetText("prologue.step", (State.prologueIndex + 1) + " / " + config.prologue.Count);
            for (int i = 0; i < config.prologue.Count; i++)
                SetActive("prologue.page." + i, i == State.prologueIndex);
            TMP_Text narrative = Get<TMP_Text>("prologue.body");
            if (narrative != null)
            {
                Canvas.ForceUpdateCanvases();
                var scroll = narrative.GetComponentInParent<ScrollRect>();
                if (scroll != null)
                    scroll.verticalNormalizedPosition = 1;
            }

            SetImage("prologue.background", page.background);
        }

        private void RenderBiography()
        {
            DWBioStep step = CurrentBioStep();
            if (step == null)
                return;
            string progress = step.year;
            int milestone = step.kind == DWBioKind.Family ? 0 : step.id == "register" || step.kind == DWBioKind.Oath ? 2 : step.id == "training" || step.id == "guard" ? 3 : step.id == "adult" || step.id == "council" ? 4 : 1;
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
            bool oath = step.kind == DWBioKind.Oath;
            int count = step.choices != null ? step.choices.Count : 0;
            if (choices != null && !oath)
            {
                choices.anchoredPosition = new Vector2(886 * 1.2f, -(count >= 4 ? 526 : 550) * 1.2f);
                choices.sizeDelta = new Vector2(636 * 1.2f, (count >= 4 ? 280 : 247) * 1.2f);
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
            SetText("bio.source", step.sourceNote);
            SetText(currentScreen + ".source", step.sourceNote);
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
            DWChoiceRecord selected = State.choices.Find(x => x.stepId == step.id);
            for (int i = 0; i < 4; i++)
            {
                bool exists = step.kind != DWBioKind.Identity && step.choices != null && i < step.choices.Count;
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
                        SetText(key + ".metrics", MetricDeltas(step.choices[i].metrics));
                        bool isSelected = selected != null && selected.choiceId == step.choices[i].id;
                        SetActive(key + ".selected", isSelected);
                        TMP_Text optionLabel = Get<TMP_Text>(key + ".label");
                        Button optionButton = Get<Button>(key);
                        Transform badge = optionButton != null ? optionButton.transform.Find("Choice index") : null;
                        if (badge != null)
                        {
                            DWShape badgeShape = badge.GetComponent<DWShape>();
                            if (badgeShape != null)
                                badgeShape.color = isSelected ? new Color32(104, 120, 87, 255) : Color.clear;
                            TMP_Text letter = badge.GetComponentInChildren<TMP_Text>();
                            if (letter != null)
                                letter.color = isSelected ? new Color32(232, 224, 199, 255) : new Color32(198, 175, 118, 255);
                        }

                        if (optionLabel != null)
                            optionLabel.color = isSelected ? new Color32(36, 60, 61, 255) : new Color32(241, 230, 202, 255);
                    }
                }
            }

            SetInteractable(currentScreen + ".primary", step.kind == DWBioKind.Identity || selected != null);
            if (step.kind == DWBioKind.Identity)
            {
                TMP_InputField input = Get<TMP_InputField>("identity.name");
                if (input != null)
                    input.SetTextWithoutNotify(State.playerName);
                TMP_Dropdown tribe = Get<TMP_Dropdown>("identity.tribe");
                if (tribe != null)
                    tribe.SetValueWithoutNotify(State.tribeIndex);
                TMP_Dropdown economicClass = Get<TMP_Dropdown>("identity.class");
                if (economicClass != null)
                {
                    economicClass.SetValueWithoutNotify(State.economicClassIndex);
                    economicClass.interactable = step.editEconomicClass;
                }

                SetActive("identity.classRow", step.editEconomicClass);
                UpdateIdentityDetails();
            }

            DWChoice selectedChoice = selected != null ? step.choices.Find(x => x.id == selected.choiceId) : null;
            string caption = step.kind == DWBioKind.Family ? "Η καταγωγή σου ανοίγει διαφορετικές εμπειρίες.\nΗ στάση σου απέναντί τους είναι δική σου επιλογή." : oath ? "Ο όρκος είναι μια αρχή που θα δοκιμάζεται στις πράξεις σου." : "Κάποιες αποφάσεις ακούγονται μόνο από έναν άνθρωπο.\nΚι όμως, αφήνουν το αποτύπωμά τους.";
            SetText("bio.feedback", selectedChoice != null ? selectedChoice.consequence : caption);
            SetText(currentScreen + ".feedback", selectedChoice != null ? selectedChoice.consequence : caption);
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
                SetActive(currentScreen + ".sourcesPanel", false);
                Canvas.ForceUpdateCanvases();
                if (roots.TryGetValue(currentScreen, out GameObject activeRoot))
                    foreach (var scroll in activeRoot.GetComponentsInChildren<ScrollRect>(true))
                        scroll.verticalNormalizedPosition = 1;
                lastRenderedBioStep = step.id;
            }
        }

        private void RenderSummary()
        {
            EnsureState();
            SetText("summary.body", State.playerName + "\n" + State.playerAge + " ετών · " + TribeName(State.tribeIndex) + "\n" + config.economicClasses[Mathf.Clamp(State.economicClassIndex, 0, config.economicClasses.Count - 1)] + "\nΔήμος: " + State.deme + "\nΤριττύς: " + State.trittys + "\n" + State.profession + "\n" + State.offices);
            SetText("summary.metrics", State.metrics.ToGreekLines());
            SetText("summary.name", State.playerName);
            SetText("summary.age", "323/322 π.Χ. · " + State.playerAge + " ΕΤΩΝ");
            SetText("summary.identity.0", TribeName(State.tribeIndex) + "\nΔήμος " + State.deme);
            SetText("summary.identity.1", State.profession + "\n" + config.economicClasses[Mathf.Clamp(State.economicClassIndex, 0, config.economicClasses.Count - 1)]);
            SetText("summary.identity.2", (State.offices ?? "").Replace(" · ", "\n"));
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
                "πρώτη",
                "δεύτερη",
                "τρίτη",
                "τέταρτη",
                "πέμπτη",
                "έκτη",
                "έβδομη",
                "όγδοη",
                "ένατη",
                "δέκατη"
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
            SetText("prytany.title", "Η " + ordinals[chapter] + " πρυτανεία");
            SetText("prytany.roman", roman[chapter]);
            SetText("prytany.tribe", State.currentTribe >= 0 ? TribeName(State.currentTribe).ToUpperInvariant() + " · ΠΡΥΤΑΝΕΥΟΥΣΑ ΦΥΛΗ" : "Η ΣΕΙΡΑ ΤΩΝ ΦΥΛΩΝ ΘΑ ΚΡΙΘΕΙ ΜΕ ΚΛΗΡΩΣΗ");
            SetText("prytany.footer", "Εβδομάδα " + State.week + " από 5 · Η διάρκεια σε εβδομάδες αποτελεί εκπαιδευτική συμπύκνωση του πολιτειακού έτους.");
            DWQuote quote = config.prytanyQuotes.Count > 0 ? config.prytanyQuotes[(State.prytany - 1) % config.prytanyQuotes.Count] : null;
            SetText("prytany.quote", quote != null ? quote.text : "");
            SetText("prytany.attribution", quote != null ? quote.attribution : "");
            TMP_Text body = Get<TMP_Text>("prytany.quote"), source = Get<TMP_Text>("prytany.attribution");
            if (body != null && source != null)
            {
                body.ForceMeshUpdate(true);
                float height = Mathf.Min(body.preferredHeight / 1.2f, 130);
                source.rectTransform.anchoredPosition = new Vector2(300 * 1.2f, -(559 + height + 18) * 1.2f);
            }
        }

        private void RenderLottery()
        {
            EnsureState();
            SetText("lottery.body", State.currentTribe < 0 ? "Οι δέκα φυλές υπηρετούν διαδοχικά. Η φυλή που κληρώνεται αποσύρεται από τις επόμενες κληρώσεις του έτους." : "Την πρυτανεία αναλαμβάνει η " + TribeName(State.currentTribe) + ".");
            for (int i = 0; i < 10; i++)
            {
                string status = State.currentTribe == i ? "Κληρώθηκε τώρα" : State.drawnTribes.Contains(i) ? "Πρυτανεία " + (State.drawnTribes.IndexOf(i) + 1).ToString("00") : "Διαθέσιμη";
                SetText("lottery.tribe." + i, status);
                CanvasGroup fade = Get<CanvasGroup>("lottery.tribe." + i + ".fade");
                if (fade != null)
                    fade.alpha = State.drawnTribes.Contains(i) && State.currentTribe != i ? .45f : 1f;
                SetActive("lottery.tribe." + i + ".selected", State.currentTribe == i);
                SetActive("lottery.tribe." + i + ".used", State.drawnTribes.Contains(i) && State.currentTribe != i);
            }

            SetText("lottery.result", State.currentTribe >= 0 ? TribeName(State.currentTribe) + ". Οι πενήντα βουλευτές της αναλαμβάνουν την πρυτανεία." : "Οι διαθέσιμες φυλές συμμετέχουν στην κλήρωση.");
            SetText("lottery.primary.label", State.currentTribe >= 0 ? "Αρχίζει η πρυτανεία" : "Κλήρωση της φυλής");
            SetInteractable("lottery.primary", State.currentTribe >= 0 || State.drawnTribes.Count < 10);
            SetInteractable("lottery.begin", State.currentTribe >= 0);
        }

        private void RenderMap()
        {
            EnsureState();
            SetText("map.status", "Πρυτανεία " + State.prytany + " / 10\nΕβδομάδα " + State.week + " / 5");
            SetText("map.prytany", State.prytany + "η πρυτανεία");
            SetText("map.week", State.week + " / 5");
            SetText("map.identity", State.playerName);
            SetText("map.office.label", State.offices);
            SetText("map.city", State.cityFunds.ToString("N0"));
            SetText("map.personal", State.personalFunds.ToString("N0"));
            SetText("map.prytany", State.prytany + "η\nπρυτανεία");
            SetText("map.week", State.week + " / 5");
            for (int i = 0; i < 10; i++)
            {
                var tick = Get<DWShape>("map.tick." + i);
                if (tick != null)
                    tick.color = i == State.prytany - 1 ? new Color32(23, 43, 48, 255) : i < State.prytany - 1 ? new Color32(115, 128, 93, 255) : new Color32(181, 178, 139, 255);
            }

            for (int i = 0; i < 5; i++)
            {
                var tick = Get<DWShape>("map.bead." + i);
                if (tick != null)
                    tick.color = i == State.week - 1 ? new Color32(232, 223, 203, 255) : i < State.week - 1 ? new Color32(180, 154, 98, 255) : new Color32(66, 93, 83, 255);
            }

            SetText("map.funds", "ΠΟΛΗ  " + State.cityFunds.ToString("N0") + "     ΟΙΚΟΣ  " + State.personalFunds.ToString("N0"));
            SetText("map.tribe", TribeName(State.currentTribe));
            SetImage("map.tribe.emblem", State.currentTribe >= 0 ? config.tribes[State.currentTribe].emblem : null);
            SetImage("map.player.emblem", config.tribes[State.tribeIndex].emblem);
            SetText("map.notice", "Ολοκλήρωσε την ενεργή θεσμική δραστηριότητα πριν προχωρήσεις στην επόμενη εβδομάδα.");
        }

        private void RenderProfile()
        {
            EnsureState();
            SetText("profile.heading", State.playerName);
            SetText("profile.name", State.playerName);
            SetText("profile.age", State.playerAge + " ετών · " + State.profession);
            SetText("profile.origin", State.deme + " · " + TribeName(State.tribeIndex));
            SetImage("profile.emblem", config.tribes[State.tribeIndex].emblem);
            SetText("profile.funds", "Πόλη  " + State.cityFunds.ToString("N0") + "     Προσωπικό  " + State.personalFunds.ToString("N0"));
            string[] identity =
            {
                TribeName(State.tribeIndex),
                State.deme,
                State.trittys,
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

        private void SetupIdentity()
        {
            TMP_Dropdown tribe = Get<TMP_Dropdown>("identity.tribe");
            if (tribe != null)
            {
                tribe.ClearOptions();
                tribe.AddOptions(config.tribes.ConvertAll(x => x.name + (string.IsNullOrEmpty(x.deme) ? "" : " · " + x.deme)));
                tribe.onValueChanged.AddListener(SelectTribe);
            }

            TMP_Dropdown economicClass = Get<TMP_Dropdown>("identity.class");
            if (economicClass != null)
            {
                economicClass.ClearOptions();
                economicClass.AddOptions(config.economicClasses);
            }

            TMP_InputField input = Get<TMP_InputField>("identity.name");
            if (input != null)
                input.characterLimit = 40;
        }

        private void UpdateIdentityDetails()
        {
            if (State == null)
                return;
            DWTribe tribe = config.tribes[State.tribeIndex];
            SetText("identity.deme", "Δήμος: " + (tribe.deme ?? "—") + "\nΤριττύς: " + (tribe.trittys ?? "—"));
            SetText("identity.tribeName", tribe.name);
            SetText("identity.trittys", tribe.trittys ?? "—");
            SetText("identity.demeName", tribe.deme ?? "—");
            for (int i = 0; i < config.tribes.Count; i++)
            {
                SetActive("identity.tribe." + i + ".selected", i == State.tribeIndex);
                TMP_Text name = Get<TMP_Text>("identity.tribe." + i + ".label");
                if (name != null)
                    name.color = i == State.tribeIndex ? new Color32(236, 223, 188, 255) : new Color32(52, 73, 64, 255);
            }
        }
    }
}
