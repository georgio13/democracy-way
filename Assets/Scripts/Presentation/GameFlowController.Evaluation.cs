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
        EvaluationContent evaluationData;
        readonly HashSet<string> evaluationQuizLayouts = new HashSet<string>();
        readonly List<Button[]> evaluationRatingRows = new List<Button[]>();
        readonly Dictionary<int, Button> evaluationNotApplicableButtons = new Dictionary<int, Button>();
        TMP_InputField evaluationFeedback;
        GameObject evaluationSurveyPanel, evaluationDialog;
        Button evaluationSubmitButton, evaluationDialogConfirm, evaluationDialogCancel;
        TMP_Text evaluationSurveyProgress, evaluationDialogTitle, evaluationDialogBody;
        RectTransform evaluationSurveyContent;
        ScrollRect evaluationSurveyScroll;
        CanvasGroup evaluationBlockedGroup;
        bool evaluationPreviousInteractable, evaluationPreviousRaycasts;
        Action evaluationDialogEscape;

        EvaluationContent EvaluationData => evaluationData ?? (evaluationData = JsonContent.Evaluation);

        EvaluationState Evaluation => EvaluationRules.Ensure(State, EvaluationData);
        EvaluationPoint EvaluationPointNow() => EvaluationRules.Point(State, Case.id, CampaignData.contentVersion);

        void SetupEvaluationUI()
        {
            foreach (string screen in new[] { "initial-knowledge", "final-knowledge", "initial-tendencies", "final-tendencies" })
            {
                if (!roots.ContainsKey(screen)) continue;
                var first = Get<Button>(screen + ".answer.0"); if (first == null) continue;
                for (int i = 0; i < 5; i++)
                {
                    string key = screen + ".answer." + i;
                    var button = Get<Button>(key);
                    if (button == null)
                    {
                        button = Instantiate(first.gameObject, first.transform.parent).GetComponent<Button>();
                        button.name = "Assessment answer " + i; button.onClick.RemoveAllListeners();
                        string target = screen; int answer = i;
                        button.onClick.AddListener(() => { PlayClick(); AnswerCampaign(target, answer); });
                        targets[key] = button; targets[key + ".label"] = button.GetComponentInChildren<TMP_Text>(true);
                    }
                    button.navigation = new Navigation { mode = Navigation.Mode.None };
                }
                evaluationQuizLayouts.Add(screen);
            }
        }

        bool TryAnswerEvaluation(string screen, int answer)
        {
            bool initial = screen == "initial-knowledge", final = screen == "final-knowledge";
            bool tendency = screen == "initial-tendencies" || screen == "final-tendencies";
            if (!initial && !final && !tendency) return false;
            if (tendency)
            {
                bool post = screen == "final-tendencies";
                if (!EvaluationRules.AnswerTendency(State, EvaluationData, post, answer + 1, EvaluationPointNow())) return true;
                if (post && Evaluation.finalTendencyComplete) { GoCampaign("final-survey"); return true; }
                if (!post && Evaluation.baselineTendencyComplete)
                {
                    State.biographyIndex = 0; State.phase = "biography"; SaveCheckpoint(); ShowBiography(); return true;
                }
            }
            else if (initial)
            {
                if (Evaluation.baselineFeedbackPending)
                {
                    if (answer != 0 || !EvaluationRules.ContinueBaseline(State, EvaluationData)) return true;
                }
                else if (!EvaluationRules.AnswerBaseline(State, EvaluationData, answer, EvaluationPointNow())) return true;
                if (Evaluation.baselineComplete) { GoCampaign("initial-tendencies"); return true; }
            }
            else
            {
                if (!EvaluationRules.BeginPost(State, EvaluationData, EvaluationPointNow()) || !EvaluationRules.AnswerPost(State, EvaluationData, answer)) return true;
                if (Evaluation.postComplete) { GoCampaign("final-tendencies"); return true; }
            }
            SaveCheckpoint(); RenderCampaign(); ClearSelection(); return true;
        }

        bool TryRenderEvaluationQuestion()
        {
            if(currentScreen=="demographics"){RenderDemographics();return true;}
            bool baseline = currentScreen == "initial-knowledge", post = currentScreen == "final-knowledge";
            bool tendency = currentScreen == "initial-tendencies" || currentScreen == "final-tendencies";
            if (!baseline && !post && !tendency) return false;
            if (!evaluationQuizLayouts.Contains(currentScreen)) SetupEvaluationUI();
            var e = Evaluation;
            if (post && !EvaluationRules.BeginPost(State, EvaluationData, EvaluationPointNow())) { GoCampaign("map-stela"); return true; }
            if(baseline && !e.baselineComplete && e.baselineIndex==0 && !DemographicsRules.Ensure(State,EvaluationData.demographics).completed){GoCampaign("demographics");return true;}
            if (baseline && e.baselineComplete) { GoCampaign("initial-tendencies"); return true; }
            if (post && e.postComplete) { GoCampaign("final-tendencies"); return true; }
            bool finalTendency = currentScreen == "final-tendencies";
            if (tendency && (finalTendency ? e.finalTendencyComplete : e.baselineTendencyComplete))
            {
                if (finalTendency) GoCampaign("final-survey");
                else { State.biographyIndex = 0; State.phase = "biography"; SaveCheckpoint(); ShowBiography(); }
                return true;
            }
            if (tendency && (finalTendency ? !e.postComplete : !e.baselineComplete)) { GoCampaign(finalTendency ? "final-knowledge" : "initial-knowledge"); return true; }
            int index = tendency ? finalTendency ? e.finalTendencyIndex : e.baselineTendencyIndex : baseline ? e.baselineIndex : e.postIndex;
            var questions = baseline ? EvaluationData.onboarding : EvaluationData.postTest;
            var question = tendency ? null : questions[index];
            int count = tendency ? EvaluationData.tendencies.Length : questions.Length;
            string prompt = tendency ? EvaluationData.tendencies[index].prompt : question.prompt;
            CampaignText("heading", (index+1)+"/"+count+" · "+prompt);
            CampaignText("banner", tendency ? finalTendency ? "Τελικές τάσεις" : "Αρχικές τάσεις" : baseline ? "Αρχικές γνώσεις" : "Τελικές γνώσεις");
            CampaignText("body", tendency ? EvaluationData.tendencyInstructions : "Επίλεξε μία από τις τέσσερις απαντήσεις.");
            HideEvaluationChrome(currentScreen);
            var heading=Get<TMP_Text>(currentScreen+".heading");var body=Get<TMP_Text>(currentScreen+".body");
            if(heading!=null){CivicUIFactory.Box(heading.rectTransform,0,88,1058.4f,124);heading.enableAutoSizing=true;heading.fontSizeMin=25;heading.fontSizeMax=38;heading.color=CivicUIFactory.Ink;}
            if(body!=null){CivicUIFactory.Box(body.rectTransform,0,218,1058.4f,64);body.enableAutoSizing=true;body.fontSizeMin=21;body.fontSizeMax=26;}
            for(int i=0;i<5;i++) {
                var button=Get<Button>(currentScreen+".answer."+i);if(button==null)continue;
                button.gameObject.SetActive(i<(tendency?5:4));button.interactable=true;
                CivicUIFactory.Box((RectTransform)button.transform,0,296+i*68,1058.4f,62);
                var label=button.GetComponentInChildren<TMP_Text>(true);
                if(label!=null){CivicUIFactory.Fill(label.rectTransform,new Vector2(24,8),new Vector2(24,8));label.text=tendency?(i+1)+"   "+EvaluationData.tendencyRatingLabels[i]:(char)('Α'+i)+"   "+(i<4?question.answers[i]:"");label.enableAutoSizing=true;label.fontSizeMin=20;label.fontSizeMax=27;label.alignment=TextAlignmentOptions.Left;}
            }
            ClearSelection(); return true;
        }

        void HideEvaluationChrome(string screen)
        {
            foreach (string key in new[] { "funds", "eyebrow", "footer", "source" })
            {
                var item = Get<GameObject>(screen + "." + key); if (item == null) continue;
                item.SetActive(false);
                if (key == "banner" && item.transform.parent != null)
                {
                    var parent = item.transform.parent;
                    if (parent.GetComponent<Graphic>() != null && parent.childCount <= 3) parent.gameObject.SetActive(false);
                }
            }
        }

        public void StartEvaluation()
        {
            HideEvaluationDialog();
            if (!EvaluationRules.Begin(State, EvaluationData, EvaluationPointNow(), "complete")) return;
            GoCampaign(!Evaluation.postComplete ? "final-knowledge" : !Evaluation.finalTendencyComplete ? "final-tendencies" : "final-survey");
        }

        public void RecordEvaluationRating(bool intention, int index, int value)
        {
            if (!EvaluationRules.Rate(State, EvaluationData, intention, index, value)) return;
            SaveCheckpoint(); RenderEvaluationSurvey(); ClearSelection();
        }

        bool RenderEvaluationSurvey()
        {
            if (EvaluationData == null || State == null || !EvaluationRules.HasPoint(Evaluation.startPoint)) return false;
            if (evaluationSurveyPanel == null) CreateEvaluationSurvey();
            evaluationSurveyPanel.SetActive(true); HideEvaluationChrome("final-survey");
            var evaluation = Evaluation;
            int[] values = evaluation.experienceAnswers.Concat(evaluation.intentionAnswers).ToArray();
            for (int row = 0; row < evaluationRatingRows.Count; row++)
                for (int value = 0; value < 5; value++)
                {
                    var button = evaluationRatingRows[row][value]; bool selected = values[row] == value + 1;
                    button.GetComponent<PanelGraphic>().color = selected ? CivicUIFactory.Teal : CivicUIFactory.Paper;
                    button.GetComponentInChildren<TMP_Text>().color = selected ? CivicUIFactory.Paper : CivicUIFactory.Ink;
                    button.interactable = !evaluation.completed;
                }
            foreach (var pair in evaluationNotApplicableButtons)
            {
                bool selected = values[pair.Key] == 0;
                pair.Value.GetComponent<PanelGraphic>().color = selected ? CivicUIFactory.Teal : CivicUIFactory.Paper;
                pair.Value.GetComponentInChildren<TMP_Text>().color = selected ? CivicUIFactory.Paper : CivicUIFactory.Ink;
                pair.Value.interactable = !evaluation.completed;
            }
            if (evaluationFeedback != null)
            {
                if (!evaluationFeedback.isFocused) evaluationFeedback.SetTextWithoutNotify(evaluation.openFeedback ?? "");
                evaluationFeedback.readOnly = evaluation.completed;
            }
            int answered = values.Count(x => x >= 0);
            evaluationSurveyProgress.text = evaluation.completed ? "Οι απαντήσεις σου αποθηκεύτηκαν." : answered + " από " + values.Length + " απαντήσεις";
            evaluationSubmitButton.interactable = evaluation.completed || EvaluationRules.QuestionnaireAnswered(evaluation, EvaluationData);
            evaluationSubmitButton.GetComponentInChildren<TMP_Text>().text = evaluation.completed ? "Ολοκλήρωση" : "Υποβολή αξιολόγησης";
            if (evaluation.completed && !modalOpen) ShowEvaluationCompletion();
            return true;
        }

        void CreateEvaluationSurvey()
        {
            var root = roots["final-survey"];
            var originalSubmit = Get<Button>("final-survey.primary");
            var font = originalSubmit?.GetComponentInChildren<TMP_Text>(true)?.font ?? Get<TMP_Text>("initial-knowledge.heading")?.font;
            foreach (Transform child in root.transform) if (child.GetComponent<RawImage>() == null) child.gameObject.SetActive(false);
            var panel = CivicUIFactory.Panel(root.transform, "Evaluation questionnaire", CivicUIFactory.Teal); evaluationSurveyPanel = panel.gameObject;
            var rect = panel.rectTransform; rect.anchorMin = new Vector2(.035f, .055f); rect.anchorMax = new Vector2(.965f, .95f); rect.offsetMin = rect.offsetMax = Vector2.zero;
            var title = CivicUIFactory.Text(rect, "Questionnaire title", "Η εμπειρία σου από το παιχνίδι", font, 36, CivicUIFactory.Paper);
            title.rectTransform.anchorMin = new Vector2(0, 1); title.rectTransform.anchorMax = Vector2.one;
            title.rectTransform.offsetMin = new Vector2(30, -76); title.rectTransform.offsetMax = new Vector2(-30, -20);
            var instruction = CivicUIFactory.Text(rect, "Questionnaire instruction", EvaluationData.questionnaireInstructions ?? "Απάντησε με βάση το μέρος του παιχνιδιού που έπαιξες. Δεν υπάρχουν σωστές ή λάθος απαντήσεις.", font, 24, CivicUIFactory.Paper);
            instruction.rectTransform.anchorMin = new Vector2(0, 1); instruction.rectTransform.anchorMax = Vector2.one;
            instruction.rectTransform.offsetMin = new Vector2(30, -182); instruction.rectTransform.offsetMax = new Vector2(-30, -82);
            instruction.textWrappingMode = TextWrappingModes.Normal; instruction.overflowMode = TextOverflowModes.Truncate;
            instruction.alignment = TextAlignmentOptions.TopLeft;
            instruction.enableAutoSizing = true; instruction.fontSizeMin = 20; instruction.fontSizeMax = 24;
            evaluationSurveyScroll = CivicUIFactory.Scroll(rect, "Evaluation statements", out evaluationSurveyContent);
            var scroll = (RectTransform)evaluationSurveyScroll.transform; CivicUIFactory.Fill(scroll, new Vector2(26, 190), new Vector2(26, 170));
            Canvas.ForceUpdateCanvases();
            float width = Mathf.Max(1200, evaluationSurveyScroll.viewport.rect.width - 20), y = 8;
            var items = EvaluationData.experience.Concat(EvaluationData.participationIntention).ToArray();
            string previousSection = null;
            for (int i = 0; i < items.Length; i++)
            {
                string sectionName = items[i].section;
                if (!string.IsNullOrEmpty(sectionName) && sectionName != previousSection)
                {
                    var section = CivicUIFactory.Text(evaluationSurveyContent, "Questionnaire section", sectionName, font, 29, CivicUIFactory.Ink); section.fontStyle = FontStyles.Bold;
                    CivicUIFactory.Box(section.rectTransform, 22, y + 10, width - 44, 54); y += 78;
                }
                previousSection = sectionName;
                var item = items[i]; var row = CivicUIFactory.Panel(evaluationSurveyContent, "Statement " + i, new Color32(236, 227, 204, 255));
                float questionWidth = width * .45f;
                var prompt = CivicUIFactory.Text(row.transform, "Statement prompt", (i + 1) + ". " + item.prompt, font, 26, CivicUIFactory.Ink);
                float rowHeight = Mathf.Max(item.allowNotApplicable ? 205 : 148, prompt.GetPreferredValues(prompt.text, questionWidth - 30, 0).y + 42);
                CivicUIFactory.Box(row.rectTransform, 0, y, width, rowHeight);
                CivicUIFactory.Box(prompt.rectTransform, 20, 20, questionWidth - 30, rowHeight - 36);
                float cellWidth = (width - questionWidth - 25) / 5;
                var buttons = new Button[5];
                for (int j = 0; j < 5; j++)
                {
                    bool intention = i >= EvaluationData.experience.Length;
                    int index = intention ? i - EvaluationData.experience.Length : i, value = j + 1;
                    var b = CivicUIFactory.Button(row.transform, "Rating " + value, value + "\n" + EvaluationData.ratingLabels[j], font,
                        () => RecordEvaluationRating(intention, index, value), this);
                    b.transition = Selectable.Transition.None;
                    CivicUIFactory.Box((RectTransform)b.transform, questionWidth + j * cellWidth, 23, cellWidth - 10, 98);
                    var label = b.GetComponentInChildren<TMP_Text>(); label.fontSizeMin = 16; label.fontSizeMax = 22;
                    buttons[j] = b; targets["evaluation.rating." + i + "." + j] = b;
                    targets["evaluation.ratingValue." + i + "." + value] = b;
                }
                if (item.allowNotApplicable)
                {
                    bool intention = i >= EvaluationData.experience.Length;
                    int index = intention ? i - EvaluationData.experience.Length : i;
                    var notApplicable = CivicUIFactory.Button(row.transform, "Cannot judge", EvaluationData.notApplicableLabel ?? "Δεν μπορώ να κρίνω", font,
                        () => RecordEvaluationRating(intention, index, 0), this);
                    notApplicable.transition = Selectable.Transition.None;
                    CivicUIFactory.Box((RectTransform)notApplicable.transform, questionWidth, 134, width - questionWidth - 25, 54);
                    evaluationNotApplicableButtons[i] = notApplicable;
                    targets["evaluation.notApplicable." + i] = notApplicable;
                }
                evaluationRatingRows.Add(buttons); y += rowHeight + 16;
            }
            var feedbackTitle = CivicUIFactory.Text(evaluationSurveyContent, "Optional feedback title", EvaluationData.openFeedbackPrompt ?? "Προαιρετικά: τι σε δυσκόλεψε ή τι θα άλλαζες;", font, 26, CivicUIFactory.Ink);
            CivicUIFactory.Box(feedbackTitle.rectTransform, 22, y + 16, width - 44, 80); y += 104;
            var feedbackPanel = CivicUIFactory.Panel(evaluationSurveyContent, "Optional feedback", CivicUIFactory.Paper);
            CivicUIFactory.Box(feedbackPanel.rectTransform, 12, y, width - 24, 190);
            var viewport = CivicUIFactory.Rect(feedbackPanel.transform, "Text viewport");
            CivicUIFactory.Fill(viewport, new Vector2(20, 14), new Vector2(20, 14)); viewport.gameObject.AddComponent<RectMask2D>();
            var feedbackText = CivicUIFactory.Text(viewport, "Feedback text", "", font, 25, CivicUIFactory.Ink); CivicUIFactory.Fill(feedbackText.rectTransform, Vector2.zero, Vector2.zero);
            feedbackText.alignment = TextAlignmentOptions.TopLeft; feedbackText.richText = false; feedbackText.raycastTarget = false;
            var placeholder = CivicUIFactory.Text(viewport, "Feedback placeholder", "Μην συμπεριλάβεις όνομα, email ή άλλα προσωπικά στοιχεία.", font, 24, new Color(.25f, .25f, .25f, .7f));
            CivicUIFactory.Fill(placeholder.rectTransform, Vector2.zero, Vector2.zero); placeholder.alignment = TextAlignmentOptions.TopLeft; placeholder.raycastTarget = false;
            evaluationFeedback = feedbackPanel.gameObject.AddComponent<TMP_InputField>();
            evaluationFeedback.textViewport = viewport; evaluationFeedback.textComponent = feedbackText; evaluationFeedback.placeholder = placeholder;
            evaluationFeedback.targetGraphic = feedbackPanel; evaluationFeedback.lineType = TMP_InputField.LineType.MultiLineNewline;
            evaluationFeedback.characterLimit = Math.Min(1000, EvaluationData.openFeedbackMaxLength > 0 ? EvaluationData.openFeedbackMaxLength : 1000);
            evaluationFeedback.navigation = new Navigation { mode = Navigation.Mode.None };
            evaluationFeedback.onEndEdit.AddListener(value => { if (EvaluationRules.SetFeedback(State, EvaluationData, value)) SaveCheckpoint(); });
            targets["evaluation.feedback"] = evaluationFeedback;
            y += 220;
            evaluationSurveyContent.sizeDelta = new Vector2(0, y + 8);
            evaluationSurveyProgress = CivicUIFactory.Text(rect, "Answered statement count", "", font, 24, CivicUIFactory.Paper);
            var progressRect = evaluationSurveyProgress.rectTransform; progressRect.anchorMin = progressRect.anchorMax = new Vector2(.04f, .065f); progressRect.pivot = new Vector2(0, .5f); progressRect.anchoredPosition = Vector2.zero; progressRect.sizeDelta = new Vector2(900, 54);
            evaluationSubmitButton = CivicUIFactory.Button(rect, "Submit questionnaire", "Υποβολή αξιολόγησης", font, () => { TryFinishEvaluationSurvey(); }, this);
            var submitRect = (RectTransform)evaluationSubmitButton.transform; submitRect.anchorMin = submitRect.anchorMax = new Vector2(.79f, .065f); submitRect.pivot = new Vector2(.5f, .5f); submitRect.anchoredPosition = Vector2.zero; submitRect.sizeDelta = new Vector2(370, 76);
            targets["evaluation.submit"] = evaluationSubmitButton; targets["final-survey.primary"] = evaluationSubmitButton;
            Canvas.ForceUpdateCanvases(); evaluationSurveyScroll.verticalNormalizedPosition = 1;
        }

        bool TryFinishEvaluationSurvey()
        {
            if (EvaluationData == null || !EvaluationRules.HasPoint(Evaluation.startPoint)) return false;
            if (!Evaluation.completed)
            {
                if (evaluationFeedback != null && !EvaluationRules.SetFeedback(State, EvaluationData, evaluationFeedback.text)) return true;
                if (!EvaluationRules.Submit(State, EvaluationData, EvaluationPointNow())) return true;
            }
            if (!SaveCheckpoint()) return true;
            ShowEvaluationCompletion(); return true;
        }

        void ShowEvaluationCompletion()
        {
            ShowEvaluationDialog("Η αξιολόγηση ολοκληρώθηκε", "Οι απαντήσεις γνώσεων, τάσεων και εμπειρίας αποθηκεύτηκαν. Ευχαριστούμε για τη συμμετοχή σου.",
                "Ολοκλήρωση", ReturnFromEvaluation, null, ReturnFromEvaluation);
        }

        void ReturnFromEvaluation()
        {
            HideEvaluationDialog();
            GoCampaign("complete");
        }

        bool HandleEvaluationEscape()
        {
            if (evaluationDialog == null || !evaluationDialog.activeSelf) return false;
            var action = evaluationDialogEscape;
            HideEvaluationDialog(); action?.Invoke(); return true;
        }

        void ShowEvaluationDialog(string title, string body, string confirm, Action onConfirm, string cancel, Action onCancel)
        {
            if (evaluationDialog == null) CreateEvaluationDialog();
            if (!evaluationDialog.activeSelf)
            {
                evaluationBlockedGroup = roots.TryGetValue(currentScreen, out var screen) ? screen.GetComponent<CanvasGroup>() : null;
                if (evaluationBlockedGroup != null)
                {
                    evaluationPreviousInteractable = evaluationBlockedGroup.interactable; evaluationPreviousRaycasts = evaluationBlockedGroup.blocksRaycasts;
                    evaluationBlockedGroup.interactable = false; evaluationBlockedGroup.blocksRaycasts = false;
                }
            }
            evaluationDialogTitle.text = title; evaluationDialogBody.text = body;
            evaluationDialogConfirm.GetComponentInChildren<TMP_Text>().text = confirm;
            evaluationDialogConfirm.onClick.RemoveAllListeners(); evaluationDialogConfirm.onClick.AddListener(() => { PlayClick(); HideEvaluationDialog(); onConfirm?.Invoke(); });
            evaluationDialogCancel.gameObject.SetActive(!string.IsNullOrEmpty(cancel));
            evaluationDialogCancel.GetComponentInChildren<TMP_Text>().text = cancel ?? "";
            evaluationDialogCancel.onClick.RemoveAllListeners(); evaluationDialogCancel.onClick.AddListener(() => { PlayClick(); HideEvaluationDialog(); onCancel?.Invoke(); });
            evaluationDialogEscape = onCancel; evaluationDialog.SetActive(true); evaluationDialog.transform.SetAsLastSibling(); modalOpen = true; ClearSelection();
        }

        void HideEvaluationDialog()
        {
            if (evaluationDialog == null || !evaluationDialog.activeSelf) return;
            evaluationDialog.SetActive(false);
            if (evaluationBlockedGroup != null)
            {
                evaluationBlockedGroup.interactable = evaluationPreviousInteractable; evaluationBlockedGroup.blocksRaycasts = evaluationPreviousRaycasts; evaluationBlockedGroup = null;
            }
            modalOpen = false; ClearSelection();
        }

        void CreateEvaluationDialog()
        {
            var parent = roots[currentScreen].transform.parent;
            var shade = CivicUIFactory.Panel(parent, "Evaluation dialog overlay", new Color32(12, 25, 28, 230));
            evaluationDialog = shade.gameObject; CivicUIFactory.Fill(shade.rectTransform, Vector2.zero, Vector2.zero);
            var panel = CivicUIFactory.Panel(shade.transform, "Evaluation dialog", CivicUIFactory.Paper);
            var rect = panel.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(1420, 560);
            var font = Get<TMP_Text>("initial-knowledge.heading")?.font;
            evaluationDialogTitle = CivicUIFactory.Text(panel.transform, "Dialog title", "", font, 38, CivicUIFactory.Ink); CivicUIFactory.Box(evaluationDialogTitle.rectTransform, 44, 36, 1332, 90);
            evaluationDialogTitle.enableAutoSizing = true; evaluationDialogTitle.fontSizeMin = 29; evaluationDialogTitle.fontSizeMax = 38;
            evaluationDialogBody = CivicUIFactory.Text(panel.transform, "Dialog message", "", font, 29, CivicUIFactory.Ink); CivicUIFactory.Box(evaluationDialogBody.rectTransform, 44, 146, 1332, 208);
            evaluationDialogBody.enableAutoSizing = true; evaluationDialogBody.fontSizeMin = 24; evaluationDialogBody.fontSizeMax = 29;
            evaluationDialogConfirm = CivicUIFactory.Button(panel.transform, "Evaluate now or continue", "", font, null, this); CivicUIFactory.Box((RectTransform)evaluationDialogConfirm.transform, 44, 408, 620, 92);
            evaluationDialogCancel = CivicUIFactory.Button(panel.transform, "Evaluate later", "", font, null, this); CivicUIFactory.Box((RectTransform)evaluationDialogCancel.transform, 704, 408, 672, 92);
            targets["evaluation.offer.now"] = evaluationDialogConfirm; targets["evaluation.offer.later"] = evaluationDialogCancel;
            targets["evaluation.complete.continue"] = evaluationDialogConfirm;
            evaluationDialog.SetActive(false);
        }
    }
}


