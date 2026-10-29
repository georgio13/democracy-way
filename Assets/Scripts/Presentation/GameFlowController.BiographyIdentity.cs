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
        string TrittysLabel()
        {
            var step = config.biography.FirstOrDefault(s => s.selectionKind == "trittys");
            return step?.choices.FirstOrDefault(c => c.trittysId == State.trittysId)?.trittys ?? State.trittys;
        }

        List<NarrativeChoice> VisibleBioChoices(BiographyStep step) => step.choices.Where(c =>
            (c.requireTribe < 0 || c.requireTribe == State.tribeIndex) &&
            (c.requireClass < 0 || c.requireClass == State.economicClassIndex) &&
            (string.IsNullOrEmpty(c.requireTrittys) || c.requireTrittys == State.trittysId)).ToList();

        /// <summary>«600 δραχμές · +250 σε κάθε πρυτανεία» — από το GameConfig, όχι γραμμένο στο κείμενο.</summary>
        string ClassMoneyLine(int economicClassIndex)
        {
            var (wallet, income) = AgoraCfg.ClassMoney(SocialClassOf(economicClassIndex));
            return W(BioText.ClassMoney, wallet, income);
        }

        void RenderAuthoredBiography()
        {
            var step = CurrentBioStep();
            if (step == null) return;
            // Οι θέσεις επιλογών υπάρχουν στη σκηνή (το μεγαλύτερο βήμα, η φυλή, θέλει 10 — έλεγχος στα tests).
            var choices = VisibleBioChoices(step);
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
                bool chosen = selected != null && selected.choiceId == choice.id;
                SetActive(key + ".selected", chosen);
                var shape = button.GetComponent<PanelGraphic>();
                if (shape != null) shape.color = chosen ? new Color32(203, 184, 135, 255) : new Color32(34, 61, 66, 245);
                var label = Get<TMP_Text>(key + ".label");
                if (label != null)
                {
                    // Οι επιλογές τάξης λένε τι χρήματα φέρνουν· οι υπόλοιπες διαβάζονται ως διάλογος.
                    label.richText = choice.economicClassIndex >= 0;
                    label.text = choice.economicClassIndex >= 0
                        ? choice.text + "\n<size=80%>" + ClassMoneyLine(choice.economicClassIndex) + "</size>"
                        : choice.text;
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
            string[] years = { BioText.Year0, BioText.Year1, BioText.Year2, BioText.Year3, BioText.Year4 };
            RestoreAuthoredBiographyTimeline();
            for (int i = 0; i < 5; i++) { SetActive(currentScreen + ".milestone." + i, i == milestone); SetText(currentScreen + ".milestone." + i + ".label", W(years[i])); }
            SetInteractable(currentScreen + ".primary", selected != null);
            RenderBiographyIdentity();
            RenderBiographyCitizen(step);
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

        /// <summary>Το πάνελ «Ο πολίτης σου» (αντικείμενο σκηνής): οι 4 δείκτες του παίκτη όπως θα μπει στην
        /// εκστρατεία (αρχικές τιμές + δέλτα βιογραφίας) και τι άλλαξε η επιλογή αυτού του βήματος.</summary>
        void RenderBiographyCitizen(BiographyStep step)
        {
            var text = Get<TMP_Text>(currentScreen + ".citizen-metrics");
            if (text == null) return;
            var citizen = CitizenPreview();
            var record = State.choices.LastOrDefault(c => c.stepId == step.id);
            string delta = record == null ? ""
                : record.economicClassIndex >= 0 ? ClassMoneyLine(record.economicClassIndex)
                : MetricDeltas(record.metrics);
            text.text = "<b>" + W(BioText.CitizenHeading) + "</b>\n"
                + W(BioText.CitizenMetrics, citizen.peitho, citizen.fame, citizen.ethos, citizen.wallet)
                + (string.IsNullOrEmpty(delta) ? "" : "\n<color=#D6BA7F>" + W(BioText.YourChoice, delta) + "</color>");
        }

        void RestoreAuthoredBiographyTimeline()
        {
            if (!roots.TryGetValue(currentScreen, out var root)) return;
            var timeline = root.transform.Find("Life timeline") as RectTransform;
            if (timeline == null) return;
            timeline.gameObject.SetActive(true);
            CivicUIFactory.Box(timeline, 108, 54, 1704, 134.4f);
            foreach (Transform child in timeline) child.gameObject.SetActive(true);
            string[] captions = { BioText.Chapter0, BioText.Chapter1, BioText.Chapter2, BioText.Chapter3, BioText.Chapter4 };
            foreach (var caption in timeline.GetComponentsInChildren<TMP_Text>(true))
                for (int i = 0; i < captions.Length; i++)
                    if (caption.name == "Chapter " + i) caption.text = W(captions[i]);
        }

        // Η πόλη δεν εμφανίζεται πουθενά στη βιογραφία — η δημιουργία χαρακτήρα δεν την αγγίζει (METRICS_UNIFY).

        /// <summary>Το πάνελ ταυτότητας (αντικείμενο σκηνής): ό,τι έχει αποφασιστεί ως τώρα, «—» για τα υπόλοιπα.</summary>
        void RenderBiographyIdentity()
        {
            var text = Get<TMP_Text>(currentScreen + ".citizen-identity");
            if (text == null) return;
            bool Has(string id) => State.choices.Exists(c => c.stepId == id);
            string[] ages = { BioText.AgeBeforeBirth, BioText.AgeBirth, BioText.Age14, BioText.Age18, BioText.Age18to20, BioText.Age20to29, BioText.Age30 };
            string age = W(ages[Mathf.Clamp(State.biographyIndex - 3, 0, ages.Length - 1)]);
            text.text = "<b>" + (Has("birth-name") ? State.playerName : W(BioText.IdentityFamily)) + "</b> · " + age
                + "\n" + W(BioText.IdentityTribe, Has("heritage-tribe") ? TribeName(State.tribeIndex) : "—")
                + "\n" + W(BioText.IdentityTrittys, Has("heritage-trittys") ? TrittysLabel() : "—")
                + "\n" + W(BioText.IdentityDeme, Has("heritage-deme") ? State.deme : "—")
                + "\n" + W(BioText.IdentityClass, Has("heritage-class") ? config.economicClasses[State.economicClassIndex] : "—")
                + "\n" + W(BioText.IdentityProfession, Has("adult-profession") ? State.profession : "—")
                + "\n" + W(BioText.IdentityOffices, Has("council-lottery") ? State.offices : "—");
        }
    }
}
