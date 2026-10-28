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
        TMP_InputField evaluationFeedback;
        GameObject evaluationSurveyPanel, evaluationDialog;
        Button evaluationSubmitButton, evaluationDialogConfirm, evaluationDialogCancel;
        TMP_Text evaluationSurveyProgress, evaluationDialogTitle, evaluationDialogBody;
        RectTransform evaluationSurveyContent;
        ScrollRect evaluationSurveyScroll;
        CanvasGroup evaluationBlockedGroup;
        bool evaluationPreviousInteractable, evaluationPreviousRaycasts;
        Action evaluationDialogEscape;
        int[] knowledgeAnswerOrder;      // θέση εμφάνισης → πραγματική θέση στο content (νέα σειρά ανά εμφάνιση ερώτησης)
        string knowledgeAnswerOrderKey;

        int KnowledgeAnswerAt(int display) =>
            knowledgeAnswerOrder != null && display >= 0 && display < knowledgeAnswerOrder.Length ? knowledgeAnswerOrder[display] : -1;

        EvaluationContent EvaluationData => evaluationData ?? (evaluationData = JsonContent.Evaluation);

        EvaluationState Evaluation => EvaluationRules.Ensure(State, EvaluationData);

        /// <summary>Μετά την τελική συμμετοχή: πρώτα τα δημογραφικά (αν λείπουν), μετά το MEEGA+.</summary>
        string AfterFinalQuestionnaires => DemographicsRules.Ensure(State, EvaluationData.demographics).completed ? "final-survey" : "demographics";
        EvaluationPoint EvaluationPointNow() => EvaluationRules.Point(State, CampaignRules.CaseIdForTurn(CampaignRules.Turn(State)), State.contentVersion.ToString());

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
            if (PendingQuestionnaireInstructions(screen, out string introKey, out _, out _))
            {
                // «Ξεκινάμε» στη σελίδα οδηγιών: σημειώνεται ότι διαβάστηκαν, μετά η πρώτη ερώτηση.
                var seen = Evaluation;
                seen.instructionsSeen = (seen.instructionsSeen ?? Array.Empty<string>()).Concat(new[] { introKey }).Distinct().ToArray();
                SaveCheckpoint(); RenderCampaign(); ClearSelection(); return true;
            }
            if (tendency)
            {
                bool post = screen == "final-tendencies";
                // Στην ίδια οθόνη: πρώτα οι τάσεις, μετά οι ερωτήσεις συμμετοχής.
                if (post ? Evaluation.finalTendencyComplete : Evaluation.baselineTendencyComplete)
                {
                    if (!EvaluationRules.AnswerParticipation(State, EvaluationData, post, answer + 1, EvaluationPointNow())) return true;
                    if (post && Evaluation.finalParticipationComplete) { GoCampaign(AfterFinalQuestionnaires); return true; }
                    if (!post && Evaluation.baselineParticipationComplete)
                    {
                        State.biographyIndex = 0; State.phase = "biography"; SaveCheckpoint(); ShowBiography(); return true;
                    }
                }
                else if (!EvaluationRules.AnswerTendency(State, EvaluationData, post, answer + 1, EvaluationPointNow())) return true;
            }
            else if (initial)
            {
                if (!EvaluationRules.AnswerBaseline(State, EvaluationData, KnowledgeAnswerAt(answer), EvaluationPointNow())) return true;
                if (Evaluation.baselineComplete) { GoCampaign("initial-tendencies"); return true; }
            }
            else
            {
                if (!EvaluationRules.BeginPost(State, EvaluationData, EvaluationPointNow()) || !EvaluationRules.AnswerPost(State, EvaluationData, KnowledgeAnswerAt(answer))) return true;
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
            if (baseline && e.baselineComplete) { GoCampaign("initial-tendencies"); return true; }
            if (post && e.postComplete) { GoCampaign("final-tendencies"); return true; }
            bool finalTendency = currentScreen == "final-tendencies";
            // Η οθόνη τάσεων φιλοξενεί δύο διαδοχικές ομάδες: τάσεις και έπειτα συμμετοχή.
            bool participation = tendency && (finalTendency ? e.finalTendencyComplete : e.baselineTendencyComplete);
            if (participation && (finalTendency ? e.finalParticipationComplete : e.baselineParticipationComplete))
            {
                if (finalTendency) GoCampaign(AfterFinalQuestionnaires);
                else { State.biographyIndex = 0; State.phase = "biography"; SaveCheckpoint(); ShowBiography(); }
                return true;
            }
            if (tendency && (finalTendency ? !e.postComplete : !e.baselineComplete)) { GoCampaign(finalTendency ? "final-knowledge" : "initial-knowledge"); return true; }
            var likertItems = !tendency ? null : participation ? EvaluationData.participation : EvaluationData.tendencies;
            int index = tendency ? participation ? finalTendency ? e.finalParticipationIndex : e.baselineParticipationIndex : finalTendency ? e.finalTendencyIndex : e.baselineTendencyIndex : baseline ? e.baselineIndex : e.postIndex;
            var questions = baseline ? EvaluationData.onboarding : EvaluationData.postTest;
            var question = tendency ? null : questions[index];
            if (!tendency)
            {
                // Νέο ανακάτεμα των απαντήσεων σε κάθε εμφάνιση ερώτησης· το κλικ μεταφράζεται
                // πίσω στην πραγματική θέση μέσω του KnowledgeAnswerAt.
                string orderKey = currentScreen + ":" + index;
                if (knowledgeAnswerOrderKey != orderKey)
                {
                    knowledgeAnswerOrder = Enumerable.Range(0, question.answers.Length).OrderBy(_ => UnityEngine.Random.value).ToArray();
                    knowledgeAnswerOrderKey = orderKey;
                }
            }
            if (PendingQuestionnaireInstructions(currentScreen, out _, out string introTitle, out string introText))
            {
                RenderQuestionnaireIntro(introTitle, introText);
                return true;
            }
            int count = tendency ? likertItems.Length : questions.Length;
            string prompt = tendency ? likertItems[index].prompt : question.prompt;
            QuestionnaireStep(currentScreen, out _, out string groupTitle, out _, out _, out _);
            CampaignText("heading", (index+1)+"/"+count+" · "+prompt);
            CampaignText("banner", groupTitle);
            CampaignText("body", "");   // οι οδηγίες δείχνονται μία φορά, στην αρχή του ερωτηματολογίου
            HideEvaluationChrome(currentScreen);
            var heading=Get<TMP_Text>(currentScreen+".heading");var body=Get<TMP_Text>(currentScreen+".body");
            if(heading!=null){CivicUIFactory.Box(heading.rectTransform,0,88,1058.4f,124);heading.enableAutoSizing=true;heading.fontSizeMin=25;heading.fontSizeMax=38;heading.color=CivicUIFactory.Ink;}
            if(body!=null){CivicUIFactory.Box(body.rectTransform,0,218,1058.4f,64);body.enableAutoSizing=true;body.fontSizeMin=21;body.fontSizeMax=26;}
            for(int i=0;i<5;i++) {
                var button=Get<Button>(currentScreen+".answer."+i);if(button==null)continue;
                button.gameObject.SetActive(i<(tendency?5:4));button.interactable=true;
                CivicUIFactory.Box((RectTransform)button.transform,0,296+i*68,1058.4f,62);
                var label=button.GetComponentInChildren<TMP_Text>(true);
                if(label!=null){CivicUIFactory.Fill(label.rectTransform,new Vector2(24,8),new Vector2(24,8));label.text=tendency?(i+1)+"   "+(participation?EvaluationData.participationRatingLabels:EvaluationData.tendencyRatingLabels)[i]:(char)('Α'+i)+"   "+(i<question.answers.Length?question.answers[knowledgeAnswerOrder[i]].text:"");label.enableAutoSizing=true;label.fontSizeMin=20;label.fontSizeMax=27;label.alignment=TextAlignmentOptions.Left;}
            }
            ClearSelection(); return true;
        }

        /// <summary>Ποιο ερωτηματολόγιο τρέχει τώρα σε αυτή την οθόνη (η οθόνη τάσεων φιλοξενεί
        /// διαδοχικά τάσεις και συμμετοχή), σε ποια ερώτηση βρίσκεται και ποιες είναι οι οδηγίες του.</summary>
        bool QuestionnaireStep(string screen, out string key, out string title, out string instructions, out int index, out bool done)
        {
            var e = Evaluation; var d = EvaluationData;
            key = title = instructions = null; index = 0; done = false;
            switch (screen)
            {
                case "initial-knowledge":
                    key = "knowledge-pre"; title = "Αρχικές γνώσεις"; instructions = d.knowledgeInstructions; index = e.baselineIndex; done = e.baselineComplete; return true;
                case "final-knowledge":
                    key = "knowledge-post"; title = "Τελικές γνώσεις"; instructions = d.knowledgeInstructions; index = e.postIndex; done = e.postComplete; return true;
                case "initial-tendencies":
                    if (!e.baselineTendencyComplete) { key = "tendencies-pre"; title = "Αρχικές τάσεις"; instructions = d.tendencyInstructions; index = e.baselineTendencyIndex; }
                    else { key = "participation-pre"; title = "Αρχική συμμετοχή"; instructions = d.participationInstructions; index = e.baselineParticipationIndex; done = e.baselineParticipationComplete; }
                    return true;
                case "final-tendencies":
                    if (!e.finalTendencyComplete) { key = "tendencies-post"; title = "Τελικές τάσεις"; instructions = d.tendencyInstructions; index = e.finalTendencyIndex; }
                    else { key = "participation-post"; title = "Τελική συμμετοχή"; instructions = d.participationInstructions; index = e.finalParticipationIndex; done = e.finalParticipationComplete; }
                    return true;
            }
            return false;
        }

        /// <summary>Οι οδηγίες (πεδίο instructions) δείχνονται μία φορά, πριν την πρώτη ερώτηση
        /// κάθε ερωτηματολογίου — ξεχωριστά για την αρχική και την τελική χορήγηση.</summary>
        bool PendingQuestionnaireInstructions(string screen, out string key, out string title, out string instructions)
        {
            if (!QuestionnaireStep(screen, out key, out title, out instructions, out int index, out bool done)) return false;
            return !done && index == 0 && !string.IsNullOrWhiteSpace(instructions)
                && !(Evaluation.instructionsSeen ?? Array.Empty<string>()).Contains(key);
        }

        /// <summary>Το ίδιο panel με τις ερωτήσεις, σε «σελίδα οδηγιών»: τίτλος «Οδηγίες», το κείμενο
        /// στη θέση των ερωτήσεων και το πρώτο κουμπί απάντησης ως «Ξεκινάμε».</summary>
        void RenderQuestionnaireIntro(string title, string instructions)
        {
            CampaignText("banner", title);
            CampaignText("heading", JsonContent.Copy("Copy_6d2a81f0c94e3b57"));
            CampaignText("body", instructions);
            HideEvaluationChrome(currentScreen);
            var heading = Get<TMP_Text>(currentScreen + ".heading"); var body = Get<TMP_Text>(currentScreen + ".body");
            if (heading != null) { CivicUIFactory.Box(heading.rectTransform, 0, 88, 1058.4f, 124); heading.enableAutoSizing = true; heading.fontSizeMin = 25; heading.fontSizeMax = 38; heading.color = CivicUIFactory.Ink; }
            if (body != null)
            {
                CivicUIFactory.Box(body.rectTransform, 0, 218, 1058.4f, 300);
                body.textWrappingMode = TextWrappingModes.Normal; body.overflowMode = TextOverflowModes.Truncate;
                body.enableAutoSizing = true; body.fontSizeMin = 22; body.fontSizeMax = 30;
            }
            for (int i = 0; i < 5; i++)
            {
                var button = Get<Button>(currentScreen + ".answer." + i); if (button == null) continue;
                button.gameObject.SetActive(i == 0); if (i != 0) continue;
                button.interactable = true;
                CivicUIFactory.Box((RectTransform)button.transform, 319.2f, 540, 420, 62);
                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    CivicUIFactory.Fill(label.rectTransform, new Vector2(24, 8), new Vector2(24, 8));
                    label.text = JsonContent.Copy("Copy_4b7e19c2d05a8f36");
                    label.enableAutoSizing = true; label.fontSizeMin = 20; label.fontSizeMax = 27; label.alignment = TextAlignmentOptions.Center;
                }
            }
            ClearSelection();
        }

        void HideEvaluationChrome(string screen)
        {
            foreach (string key in new[] { "funds", "eyebrow", "footer", "source" })
            {
                var item = Get<GameObject>(screen + "." + key); if (item == null) continue;
                item.SetActive(false);
            }
        }

        public void RecordEvaluationRating(bool intention, int index, int value)
        {
            if (!EvaluationRules.Rate(State, EvaluationData, intention, index, value)) return;
            SaveCheckpoint(); RenderEvaluationSurvey(); ClearSelection();
        }

        bool RenderEvaluationSurvey()
        {
            if (EvaluationData == null || State == null || !EvaluationRules.HasPoint(Evaluation.startPoint)) return false;
            if (!Evaluation.completed && !DemographicsRules.Ensure(State, EvaluationData.demographics).completed) { GoCampaign("demographics"); return true; }
            if (evaluationSurveyPanel == null) CreateEvaluationSurvey();
            evaluationSurveyPanel.SetActive(true); HideEvaluationChrome("final-survey");
            var evaluation = Evaluation;
            int[] values = evaluation.experienceAnswers.Concat(evaluation.intentionAnswers).ToArray();
            for (int row = 0; row < evaluationRatingRows.Count; row++)
                for (int value = 0; value < 5; value++)
                {
                    var button = evaluationRatingRows[row][value];
                    CivicUIFactory.PaintSelectable(button, values[row] == value + 1);
                    button.interactable = !evaluation.completed;
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
                float rowHeight = Mathf.Max(148, prompt.GetPreferredValues(prompt.text, questionWidth - 30, 0).y + 42);
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
            // Τα τέλη έχουν ήδη δειχτεί πριν τα ερωτηματολόγια· μετά το MEEGA+ μένει η ολοκλήρωση.
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
            targets["evaluation.complete.continue"] = evaluationDialogConfirm;
            evaluationDialog.SetActive(false);
        }
    }
}


