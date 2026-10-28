using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    public sealed partial class GameFlowController
    {
        readonly Dictionary<string, TMP_Text> biographyIdentityLabels = new Dictionary<string, TMP_Text>();
        readonly Dictionary<string, TMP_Text> biographyPlayerLabels = new Dictionary<string, TMP_Text>();

        string TrittysLabel()
        {
            var step = config.biography.FirstOrDefault(s => s.selectionKind == "trittys");
            return step?.choices.FirstOrDefault(c => c.trittysId == State.trittysId)?.trittys ?? State.trittys;
        }

        List<NarrativeChoice> VisibleBioChoices(BiographyStep step) => step.choices.Where(c =>
            (c.requireTribe < 0 || c.requireTribe == State.tribeIndex) &&
            (c.requireClass < 0 || c.requireClass == State.economicClassIndex) &&
            (string.IsNullOrEmpty(c.requireTrittys) || c.requireTrittys == State.trittysId)).ToList();

        void EnsureBiographyOptions(int count)
        {
            var original = Get<Button>(currentScreen + ".option.0");
            if (original == null) return;
            for (int i = 0; i < count; i++)
            {
                string key = currentScreen + ".option." + i;
                if (Get<Button>(key) != null) continue;
                var clone = Instantiate(original, original.transform.parent);
                clone.name = "Authored biography choice " + i;
                clone.onClick.RemoveAllListeners();
                int index = i;
                clone.onClick.AddListener(() => ChooseBio(index));
                targets[key] = clone;
                var label = clone.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == "Label") ?? clone.GetComponentInChildren<TMP_Text>(true);
                targets[key + ".label"] = label;
                Transform selected = clone.transform.Find("Selected");
                if (selected != null) targets[key + ".selected"] = selected.gameObject;
            }
        }

        void RenderAuthoredBiography()
        {
            var step = CurrentBioStep();
            if (step == null) return;
            var choices = VisibleBioChoices(step);
            EnsureBiographyOptions(choices.Count);
            SetText(currentScreen + ".title", step.title);
            SetText(currentScreen + ".body", step.body);
            SetText(currentScreen + ".step", step.year);
            SetImage(currentScreen + ".background", step.background);
            var heading = Get<TMP_Text>(currentScreen + ".title");
            if (heading != null) { heading.enableAutoSizing = true; heading.fontSizeMin = 29; heading.fontSizeMax = 38; }
            var body = Get<TMP_Text>(currentScreen + ".body");
            if (body != null) { body.enableAutoSizing = true; body.fontSizeMin = 20; body.fontSizeMax = 27; }
            LayoutAuthoredBiographyNarrative(heading, body);
            var choiceRoot = Get<RectTransform>(currentScreen + ".choices");
            var scroll = choiceRoot != null ? choiceRoot.GetComponent<ScrollRect>() : null;
            const float height = 70, gap = 8, choiceWidth = 822, viewportWidth = 798;
            if (choiceRoot != null)
            {
                CivicUIFactory.Box(choiceRoot, 1004, 666, choiceWidth, 310);
                if (scroll != null && scroll.content != null)
                {
                    foreach (var layout in scroll.content.GetComponents<LayoutGroup>()) layout.enabled = false;
                    foreach (var fitter in scroll.content.GetComponents<ContentSizeFitter>()) fitter.enabled = false;
                    if (scroll.viewport != null) CivicUIFactory.Fill(scroll.viewport, Vector2.zero, new Vector2(24, 0));
                    // The oath scene has a fixed-width content rect. Setting only sizeDelta.x=0
                    // collapses every stretched option there; make the actual width explicit.
                    CivicUIFactory.Box(scroll.content, 0, 0, viewportWidth, Math.Max(310, choices.Count * (height + gap) - gap));
                    scroll.horizontal = false;
                    scroll.vertical = choices.Count > 4;
                    scroll.movementType = ScrollRect.MovementType.Clamped;
                    if (scroll.verticalScrollbar != null) scroll.verticalScrollbar.gameObject.SetActive(choices.Count > 4);
                }
            }
            var selected = State.choices.Find(c => c.stepId == step.id);
            for (int i = 0; i < Math.Max(choices.Count, 32); i++)
            {
                string key = currentScreen + ".option." + i;
                var button = Get<Button>(key);
                if (button == null) continue;
                bool exists = i < choices.Count;
                button.gameObject.SetActive(exists);
                if (!exists) continue;
                var row = (RectTransform)button.transform;
                CivicUIFactory.Box(row, 0, i * (height + gap), viewportWidth, height);
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                var choice = choices[i];
                // Decisions are read as dialogue. Their effects belong in the indicators,
                // rather than numerical hints attached to an answer.
                SetText(key + ".label", choice.text);
                SetActive(key + ".metrics", false);
                bool chosen = selected != null && selected.choiceId == choice.id;
                SetActive(key + ".selected", chosen);
                var shape = button.GetComponent<PanelGraphic>();
                if (shape != null) shape.color = chosen ? new Color32(203, 184, 135, 255) : new Color32(34, 61, 66, 245);
                var label = Get<TMP_Text>(key + ".label");
                if (label != null)
                {
                    CivicUIFactory.Fill(label.rectTransform, new Vector2(86, 7), new Vector2(18, 7));
                    label.enableAutoSizing = true; label.fontSizeMin = 18; label.fontSizeMax = 24;
                    label.alignment = TextAlignmentOptions.MidlineLeft;
                    label.color = chosen ? CivicUIFactory.Ink : new Color32(241, 230, 202, 255);
                }
                var badge = button.transform.Find("Choice index");
                if (badge != null)
                {
                    var letter = badge.GetComponentInChildren<TMP_Text>();
                    if (letter != null) letter.text = choices.Count <= 4 ? "ΑΒΓΔ"[i].ToString() : (i + 1).ToString();
                }
            }
            if (scroll != null && scroll.content != null)
            {
                if (lastRenderedBioStep != step.id) scroll.verticalNormalizedPosition = 1;
            }
            var primary = Get<Button>(currentScreen + ".primary");
            if (primary != null)
            {
                primary.navigation = new Navigation { mode = Navigation.Mode.None };
                var primaryLabel = primary.GetComponentInChildren<TMP_Text>(true);
                if (primaryLabel != null)
                {
                    primaryLabel.enableAutoSizing = true;
                    primaryLabel.fontSizeMin = 21;
                    primaryLabel.fontSizeMax = 26;
                    primaryLabel.textWrappingMode = TextWrappingModes.NoWrap;
                }
            }
            int milestone = State.biographyIndex < 4 ? 0 : State.biographyIndex == 4 ? 1 : State.biographyIndex == 5 ? 2 : State.biographyIndex < 9 ? 3 : 4;
            string[] years = { "Πριν από το 352", "352 π.Χ.", "338 π.Χ.", "334–332 π.Χ.", "322 π.Χ." };
            RestoreAuthoredBiographyTimeline();
            for (int i = 0; i < 5; i++) { SetActive(currentScreen + ".milestone." + i, i == milestone); SetText(currentScreen + ".milestone." + i + ".label", years[i]); }
            SetInteractable(currentScreen + ".primary", selected != null);
            RenderBiographyIdentity(step);
            LayoutAuthoredBiographyMetrics();
            lastRenderedBioStep = step.id;
        }

        void LayoutAuthoredBiographyNarrative(TMP_Text heading, TMP_Text body)
        {
            if (heading == null || body == null) return;
            var inner = heading.transform.parent as RectTransform;
            var paper = inner != null ? inner.parent as RectTransform : null;
            if (inner == null || paper == null) return;
            CivicUIFactory.Box(paper, 1004, 220, 822, 430);
            CivicUIFactory.Box(inner, 38, 28, 746, 378);
            // Artwork is a separate child of the paper container in the approved prefab.
            // It must stretch with the resized container, rather than retain its original dimensions.
            foreach (Transform child in paper)
            {
                var artwork = child.GetComponent<Image>();
                if (artwork == null) continue;
                CivicUIFactory.Fill(artwork.rectTransform, Vector2.zero, Vector2.zero);
                artwork.preserveAspect = false;
            }
            var oldBodyScroll = body.GetComponentInParent<ScrollRect>();
            if (body.transform.parent != inner)
            {
                body.transform.SetParent(inner, false);
                if (oldBodyScroll != null) oldBodyScroll.gameObject.SetActive(false);
            }
            var fitter = body.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
            var step = Get<TMP_Text>(currentScreen + ".step");
            if (step != null) CivicUIFactory.Box(step.rectTransform, 0, 2, 746, 26);
            CivicUIFactory.Box(heading.rectTransform, 0, 30, 746, 72);
            CivicUIFactory.Box(body.rectTransform, 0, 112, 746, 264);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.raycastTarget = false;
            body.overflowMode = TextOverflowModes.Overflow;
        }

        void LayoutAuthoredBiographyMetrics()
        {
            if (!roots.TryGetValue(currentScreen, out var root)) return;
            var labels = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < 6; i++)
            {
                var label = labels.FirstOrDefault(x => x.name == "Metric label " + i);
                if (label != null)
                {
                    label.gameObject.SetActive(false);
                }
                var value = Get<TMP_Text>(currentScreen + ".metric." + i);
                if (value != null) value.gameObject.SetActive(false);
            }
            if (!biographyPlayerLabels.TryGetValue(currentScreen, out var playerText))
            {
                var panel = CivicUIFactory.Panel(root.transform, "Biography player indicators", new Color32(28, 52, 56, 242));
                CivicUIFactory.Box(panel.rectTransform, 85, 922, 850, 118);
                playerText = CivicUIFactory.Text(panel.transform, "Player values and changes", "", Get<TMP_Text>(currentScreen + ".body").font, 22, new Color32(239, 229, 203, 255));
                CivicUIFactory.Fill(playerText.rectTransform, new Vector2(22, 8), new Vector2(22, 8));
                playerText.enableAutoSizing = true; playerText.fontSizeMin = 17; playerText.fontSizeMax = 22;
                biographyPlayerLabels[currentScreen] = playerText;
            }
            // Ένα σύστημα δεικτών: ο πολίτης όπως θα μπει στην εκστρατεία (αρχικές τιμές + δέλτα βιογραφίας).
            var citizen = CitizenPreview();
            var selected = State.choices.LastOrDefault(c => c.stepId == CurrentBioStep()?.id);
            string delta = selected != null ? MetricDeltas(selected.metrics) : "";
            playerText.text = "<b>Ο πολίτης σου</b> · Πειθώ " + citizen.peitho + " · Φήμη " + citizen.fame + " · Ηθική " + citizen.ethos
                + "\nΠορτοφόλι " + citizen.wallet + " δρ."
                + (string.IsNullOrEmpty(delta) ? "" : "\n<color=#D6BA7F>Η επιλογή σου: " + delta + "</color>");
        }

        void RestoreAuthoredBiographyTimeline()
        {
            if (!roots.TryGetValue(currentScreen, out var root)) return;
            var timeline = root.transform.Find("Life timeline") as RectTransform;
            if (timeline == null) return;
            timeline.gameObject.SetActive(true);
            CivicUIFactory.Box(timeline, 108, 54, 1704, 134.4f);
            foreach (Transform child in timeline) child.gameObject.SetActive(true);
            string[] captions = { "Οικογένεια", "Γέννηση", "Η πόλη", "Όρκος και θητεία", "Αξιώματα" };
            foreach (var caption in timeline.GetComponentsInChildren<TMP_Text>(true))
                for (int i = 0; i < captions.Length; i++)
                    if (caption.name == "Chapter " + i) caption.text = captions[i];
        }

        // Η πόλη δεν εμφανίζεται πουθενά στη βιογραφία — η δημιουργία χαρακτήρα δεν την αγγίζει (METRICS_UNIFY).

        void RenderBiographyIdentity(BiographyStep step)
        {
            if (!biographyIdentityLabels.TryGetValue(currentScreen, out var text))
            {
                var panel = CivicUIFactory.Panel(roots[currentScreen].transform, "Citizen identity", new Color32(28, 52, 56, 242));
                CivicUIFactory.Box(panel.rectTransform, 85, 578, 850, 220);
                var font = Get<TMP_Text>(currentScreen + ".body").font;
                text = CivicUIFactory.Text(panel.transform, "Identity details", "", font, 24, new Color32(239, 229, 203, 255));
                CivicUIFactory.Fill(text.rectTransform, new Vector2(26, 18), new Vector2(26, 18));
                text.enableAutoSizing = true; text.fontSizeMin = 18; text.fontSizeMax = 22;
                biographyIdentityLabels[currentScreen] = text;
            }
            bool Has(string id) => State.choices.Exists(c => c.stepId == id);
            string age = State.biographyIndex < 4 ? "Πριν από τη γέννηση" : State.biographyIndex == 4 ? "Γέννηση" : State.biographyIndex == 5 ? "14 ετών" : State.biographyIndex == 6 ? "18 ετών" : State.biographyIndex == 7 ? "18–20 ετών" : State.biographyIndex == 8 ? "20–29 ετών" : "30 ετών";
            text.text = "<b>" + (Has("birth-name") ? State.playerName : "Η οικογένειά σου") + "</b> · " + age
                + "\nΦυλή: " + (Has("heritage-tribe") ? TribeName(State.tribeIndex) : "—")
                + "\nΤριττύς: " + (Has("heritage-trittys") ? TrittysLabel() : "—")
                + "\nΔήμος: " + (Has("heritage-deme") ? State.deme : "—")
                + "\nΟικονομική τάξη: " + (Has("heritage-class") ? config.economicClasses[State.economicClassIndex] : "—")
                + "\nΕπάγγελμα: " + (Has("adult-profession") ? State.profession : "—")
                + "\nΑξιώματα: " + (Has("council-lottery") ? State.offices : "—");
        }
    }
}
