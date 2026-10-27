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
        GameObject demographicsPanel;
        Button demographicsContinue;
        readonly Dictionary<string, Button> demographicOptions = new Dictionary<string, Button>();
        readonly Dictionary<string, TMP_InputField> demographicInputs = new Dictionary<string, TMP_InputField>();
        DemographicsContent DemographicContent => EvaluationData.demographics;

        void RenderDemographics()
        {
            if (demographicsPanel == null) CreateDemographics();
            var data = DemographicsRules.Ensure(State, DemographicContent);
            if (data.completed) { GoCampaign("initial-knowledge"); return; }
            demographicsPanel.SetActive(true);
            foreach (var pair in demographicOptions)
            {
                string[] key = pair.Key.Split('/');
                bool selected = data.answers.Any(a => a.questionId == key[0] && a.optionId == key[1]);
                var shape = pair.Value.GetComponent<PanelGraphic>();
                shape.color = selected ? CivicUIFactory.Teal : new Color32(222, 212, 184, 255);
                pair.Value.GetComponentInChildren<TMP_Text>(true).color = selected ? CivicUIFactory.Paper : CivicUIFactory.Ink;
            }
            foreach (var pair in demographicInputs)
                if (!pair.Value.isFocused) pair.Value.SetTextWithoutNotify(data.answers.FirstOrDefault(a => a.questionId == pair.Key)?.value ?? "");
            demographicsContinue.interactable = DemographicsRules.Ready(State, DemographicContent);
            ClearSelection();
        }

        void ChooseDemographic(string id, string option)
        {
            if (!DemographicsRules.Answer(State, DemographicContent, id, option, null, EvaluationPointNow())) return;
            SaveCheckpoint(); RenderDemographics();
        }

        void FinishDemographics()
        {
            foreach (var pair in demographicInputs)
                DemographicsRules.Answer(State, DemographicContent, pair.Key, null, pair.Value.text, EvaluationPointNow());
            if (!DemographicsRules.Complete(State, DemographicContent)) return;
            GoCampaign("initial-knowledge");
        }

        void CreateDemographics()
        {
            var root = roots["demographics"];
            foreach (Transform child in root.transform)
                if (child.GetComponent<RawImage>() == null && !child.name.ToLowerInvariant().Contains("background")) child.gameObject.SetActive(false);
            var panel = CivicUIFactory.Panel(root.transform, "Demographic information", CivicUIFactory.Paper);
            demographicsPanel = panel.gameObject;
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(1320, 950);
            var template = Get<TMP_Text>("initial-knowledge.heading"); var font = template.font;
            var originalBanner = Get<TMP_Text>("initial-knowledge.banner");
            if (originalBanner != null)
            {
                var banner = Instantiate(originalBanner.transform.parent.gameObject, rect);
                banner.name = "Banner Demographics"; banner.SetActive(true);
                CivicUIFactory.Box((RectTransform)banner.transform, 40, 30, 620, 64);
                var title = banner.GetComponentInChildren<TMP_Text>(true); title.text = "ΔΗΜΟΓΡΑΦΙΚΑ ΣΤΟΙΧΕΙΑ";
                title.gameObject.SetActive(true); title.maxVisibleCharacters = int.MaxValue;
            }
            var description = CivicUIFactory.Text(rect, "Demographic instructions", DemographicContent.instructions, font, 23, CivicUIFactory.Ink);
            CivicUIFactory.Box(description.rectTransform, 44, 104, 1232, 60);
            description.enableAutoSizing = true; description.fontSizeMin = 20; description.fontSizeMax = 23;
            int choiceRow = 0, inputRow = 0;
            foreach (var question in DemographicContent.questions)
            {
                var q = question;
                if (q.kind == "choice")
                {
                    float y = 182 + choiceRow++ * 116;
                    var label = CivicUIFactory.Text(rect, q.id + " label", q.prompt, font, 24, CivicUIFactory.Ink);
                    CivicUIFactory.Box(label.rectTransform, 44, y, 1232, 36);
                    float width = (1232f - 10 * (q.options.Length - 1)) / q.options.Length;
                    for (int i = 0; i < q.options.Length; i++)
                    {
                        var option = q.options[i];
                        var button = CivicUIFactory.Button(rect, q.id + " " + option.id, option.label, font, () => ChooseDemographic(q.id, option.id), this);
                        CivicUIFactory.Box((RectTransform)button.transform, 44 + i * (width + 10), y + 40, width, 58);
                        var text = button.GetComponentInChildren<TMP_Text>(); text.fontSizeMax = 22; text.fontSizeMin = 17;
                        demographicOptions[q.id + "/" + option.id] = button;
                        targets["demographics." + q.id + "." + option.id] = button;
                    }
                }
                else
                {
                    float y = 656 + inputRow++ * 62;
                    var label = CivicUIFactory.Text(rect, q.id + " label", q.prompt, font, 21, CivicUIFactory.Ink);
                    CivicUIFactory.Box(label.rectTransform, 44, y + 8, 500, 42); label.enableAutoSizing = true; label.fontSizeMin = 18; label.fontSizeMax = 21;
                    var shape = CivicUIFactory.Panel(rect, q.id + " input", new Color32(221, 210, 181, 255));
                    CivicUIFactory.Box(shape.rectTransform, 555, y, 720, 48);
                    var viewport = CivicUIFactory.Rect(shape.transform, "Viewport"); CivicUIFactory.Fill(viewport, new Vector2(12, 6), new Vector2(12, 6)); viewport.gameObject.AddComponent<RectMask2D>();
                    var value = CivicUIFactory.Text(viewport, "Value", "", font, 22, CivicUIFactory.Ink); CivicUIFactory.Fill(value.rectTransform, Vector2.zero, Vector2.zero);
                    var hint = CivicUIFactory.Text(viewport, "Placeholder", "Προαιρετικό", font, 20, new Color32(114, 113, 89, 255)); CivicUIFactory.Fill(hint.rectTransform, Vector2.zero, Vector2.zero);
                    var input = shape.gameObject.AddComponent<TMP_InputField>(); input.textViewport = viewport; input.textComponent = value; input.placeholder = hint;
                    input.targetGraphic = shape; input.characterLimit = Math.Min(100, q.maxLength); input.navigation = new Navigation { mode = Navigation.Mode.None };
                    input.onEndEdit.AddListener(text => { if (DemographicsRules.Answer(State, DemographicContent, q.id, null, text, EvaluationPointNow())) SaveCheckpoint(); });
                    demographicInputs[q.id] = input;
                }
            }
            demographicsContinue = CivicUIFactory.Button(rect, "Continue to questions", "Συνέχεια", font, FinishDemographics, this);
            CivicUIFactory.Box((RectTransform)demographicsContinue.transform, 956, 860, 320, 66);
            targets["demographics.continue"] = demographicsContinue;
        }
    }
}
