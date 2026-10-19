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
        public TextAsset evaluationContent;
        EvaluationContent evaluationData;
        readonly HashSet<string> evaluationQuizLayouts = new HashSet<string>();
        readonly List<Button[]> evaluationRatingRows = new List<Button[]>();
        GameObject evaluationSurveyPanel, evaluationDialog;
        Button evaluationAccessButton, evaluationSubmitButton, evaluationDialogConfirm, evaluationDialogCancel;
        TMP_Text evaluationSurveyProgress, evaluationDialogTitle, evaluationDialogBody;
        RectTransform evaluationSurveyContent;
        ScrollRect evaluationSurveyScroll;
        CanvasGroup evaluationBlockedGroup;
        bool evaluationPreviousInteractable, evaluationPreviousRaycasts;
        Action evaluationDialogEscape;

        EvaluationContent EvaluationData
        {
            get
            {
                if (evaluationData != null) return evaluationData;
                if (evaluationContent == null) evaluationContent = Resources.Load<TextAsset>("Narrative/EvaluationContent");
                if (evaluationContent != null) evaluationData = JsonUtility.FromJson<EvaluationContent>(evaluationContent.text);
                return evaluationData;
            }
        }
        EvaluationState Evaluation => EvaluationRules.Ensure(State, EvaluationData);
        EvaluationPoint EvaluationPointNow() => EvaluationRules.Point(State, Case.id, CampaignData.contentVersion);

        void SetupEvaluationUI()
        {
            if (EvaluationData == null) return;
            foreach (string screen in new[] { "initial-knowledge", "final-knowledge" })
            {
                if (!roots.ContainsKey(screen)) continue;
                var first = Get<Button>(screen + ".answer.0");
                if (first == null) continue;
                if (Get<Button>(screen + ".answer.3") == null)
                {
                    var fourth = Instantiate(first.gameObject, first.transform.parent).GetComponent<Button>();
                    fourth.name = "Fourth knowledge answer";
                    fourth.onClick.RemoveAllListeners();
                    string targetScreen = screen;
                    fourth.onClick.AddListener(() => { PlayClick(); AnswerCampaign(targetScreen, 3); });
                    targets[screen + ".answer.3"] = fourth;
                    targets[screen + ".answer.3.label"] = fourth.GetComponentInChildren<TMP_Text>(true);
                }
                for (int i = 0; i < 4; i++)
                {
                    var button = Get<Button>(screen + ".answer." + i);
                    if (button != null) button.navigation = new Navigation { mode = Navigation.Mode.None };
                }
                evaluationQuizLayouts.Add(screen);
            }
        }

        bool TryAnswerEvaluation(string screen, int answer)
        {
            if (EvaluationData == null || (screen != "initial-knowledge" && screen != "final-knowledge")) return false;
            if (screen == "initial-knowledge")
            {
                if (Evaluation.baselineFeedbackPending)
                {
                    if (answer != 0 || !EvaluationRules.ContinueBaseline(State, EvaluationData)) return true;
                    if (Evaluation.baselineComplete) { GoCampaign("prytany-title"); return true; }
                }
                else if (!EvaluationRules.AnswerBaseline(State, EvaluationData, answer, EvaluationPointNow())) return true;
            }
            else
            {
                if (!EvaluationRules.HasPoint(Evaluation.startPoint))
                {
                    if (!EvaluationRules.Available(State, EvaluationData)) return false;
                    EvaluationRules.Begin(State, EvaluationData, EvaluationPointNow(), "map-stela");
                }
                if (!EvaluationRules.AnswerPost(State, EvaluationData, answer)) return true;
                if (Evaluation.postComplete) { GoCampaign("final-survey"); return true; }
            }
            SaveCheckpoint(); RenderCampaign(); ClearSelection(); return true;
        }

        bool TryRenderEvaluationQuestion()
        {
            if (EvaluationData == null || (currentScreen != "initial-knowledge" && currentScreen != "final-knowledge")) return false;
            if (!evaluationQuizLayouts.Contains(currentScreen)) SetupEvaluationUI();
            bool baseline = currentScreen == "initial-knowledge";
            if (!baseline && !EvaluationRules.HasPoint(Evaluation.startPoint))
            {
                if (!EvaluationRules.Available(State, EvaluationData))
                {
                    var fourth = Get<Button>(currentScreen + ".answer.3"); if (fourth != null) fourth.gameObject.SetActive(false);
                    return false; // A legacy run without the new baseline keeps its legacy end-of-campaign instrument.
                }
                EvaluationRules.Begin(State, EvaluationData, EvaluationPointNow(), "map-stela"); SaveCheckpoint();
            }
            var state = Evaluation;
            if (baseline && state.baselineComplete) { GoCampaign("prytany-title"); return true; }
            if (!baseline && state.postComplete) { GoCampaign("final-survey"); return true; }
            int index = Mathf.Clamp(baseline ? state.baselineIndex : state.postIndex, 0, 7);
            var question = (baseline ? EvaluationData.onboarding : EvaluationData.postTest)[index];
            bool feedback = baseline && state.baselineFeedbackPending;
            bool correct = feedback && state.baselineAnswers[index] == question.correctIndex;
            HideEvaluationChrome(currentScreen);
            CampaignText("heading", feedback ? correct ? "Σωστά!" : "Δεν ήταν αυτή η σωστή απάντηση" : (index + 1) + "/8 · " + question.prompt);
            CampaignText("body", feedback ? "Σωστή απάντηση: " + question.answers[question.correctIndex] + "\n\n" + question.feedback +
                (string.IsNullOrWhiteSpace(question.identityTemplate) ? "" : "\n\n" + EvaluationIdentity(question.identityTemplate))
                : baseline ? "Επίλεξε μία απάντηση. Στη συνέχεια θα δεις μια σύντομη εξήγηση και τη σύνδεση με τον χαρακτήρα σου."
                : "Επίλεξε μία απάντηση για κάθε ερώτηση. Η απάντησή σου οριστικοποιείται με την επιλογή.");
            var heading = Get<TMP_Text>(currentScreen + ".heading");
            var body = Get<TMP_Text>(currentScreen + ".body");
            if (heading != null)
            {
                CivicUIFactory.Box(heading.rectTransform, 0, 24, 1058.4f, feedback ? 88 : 152);
                heading.enableAutoSizing = true; heading.fontSizeMin = 26; heading.fontSizeMax = 39;
                heading.color = feedback && !correct ? new Color32(135, 64, 51, 255) : CivicUIFactory.Ink;
            }
            if (body != null)
            {
                CivicUIFactory.Box(body.rectTransform, 0, feedback ? 130 : 190, 1058.4f, feedback ? 340 : 72);
                body.enableAutoSizing = true; body.fontSizeMin = 22; body.fontSizeMax = feedback ? 29 : 25;
            }
            for (int i = 0; i < 4; i++)
            {
                var button = Get<Button>(currentScreen + ".answer." + i);
                if (button == null) continue;
                button.gameObject.SetActive(!feedback || i == 0); button.interactable = true;
                var r = (RectTransform)button.transform;
                CivicUIFactory.Box(r, feedback ? 239 : 0, feedback ? 540 : 280 + i * 88, feedback ? 580 : 1058.4f, feedback ? 84 : 78);
                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    CivicUIFactory.Fill(label.rectTransform, new Vector2(24, 8), new Vector2(24, 8));
                    label.text = feedback ? "Συνέχεια" : (char)('Α' + i) + "   " + question.answers[i];
                    label.enableAutoSizing = true; label.fontSizeMin = 20; label.fontSizeMax = 28;
                    if (feedback) label.alignment = TextAlignmentOptions.Center;
                    else label.alignment = TextAlignmentOptions.Left;
                }
            }
            ClearSelection(); return true;
        }

        string EvaluationIdentity(string template)
        {
            string economic = State.economicClassIndex >= 0 && State.economicClassIndex < config.economicClasses.Count ? config.economicClasses[State.economicClassIndex] : "—";
            return template.Replace("{playerName}", State.playerName ?? "").Replace("{tribe}", TribeName(State.tribeIndex))
                .Replace("{trittys}", State.trittys ?? "").Replace("{deme}", State.deme ?? "")
                .Replace("{economicClass}", economic).Replace("{offices}", State.offices ?? "").Replace("{profession}", State.profession ?? "");
        }

        void HideEvaluationChrome(string screen)
        {
            foreach (string key in new[] { "funds", "banner", "eyebrow", "footer", "source" })
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

        void RenderEvaluationAccess()
        {
            if (State == null || EvaluationData == null || !roots.TryGetValue("map-stela", out var map)) return;
            if (evaluationAccessButton == null)
            {
                var font = Get<TMP_Text>("initial-knowledge.heading")?.font;
                evaluationAccessButton = CivicUIFactory.Button(map.transform, "Evaluation access", "Αξιολόγηση", font, StartEvaluation, this);
                var rect = (RectTransform)evaluationAccessButton.transform; rect.anchorMin = rect.anchorMax = new Vector2(.91f, .13f); rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(330, 76);
                targets["map.evaluation"] = evaluationAccessButton;
            }
            bool available = EvaluationRules.Available(State, EvaluationData);
            evaluationAccessButton.gameObject.SetActive(available || Evaluation.completed);
            evaluationAccessButton.interactable = available && !Evaluation.completed;
            evaluationAccessButton.GetComponentInChildren<TMP_Text>().text = Evaluation.completed ? "Αξιολόγηση ✓" : "Αξιολόγηση";
            if (!modalOpen && !gamePaused && EvaluationRules.Offer(State, EvaluationData, EvaluationPointNow()))
            {
                SaveCheckpoint();
                ShowEvaluationDialog(EvaluationData.offerTitle, EvaluationData.offerBody, EvaluationData.offerNow ?? "Αξιολόγηση τώρα", StartEvaluation,
                    EvaluationData.offerLater ?? "Συνεχίζω και θα την κάνω μετά", DeferEvaluation);
            }
        }

        public void StartEvaluation()
        {
            HideEvaluationDialog();
            if (EvaluationData == null || !EvaluationRules.Begin(State, EvaluationData, EvaluationPointNow(), State.phase)) return;
            GoCampaign(Evaluation.postComplete ? "final-survey" : "final-knowledge");
        }

        public void DeferEvaluation()
        {
            HideEvaluationDialog();
            if (EvaluationData != null && EvaluationRules.Defer(State, EvaluationData)) SaveCheckpoint();
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
            int[] values = Evaluation.experienceAnswers.Concat(Evaluation.intentionAnswers).ToArray();
            for (int row = 0; row < evaluationRatingRows.Count; row++)
                for (int value = 0; value < 5; value++)
                {
                    var button = evaluationRatingRows[row][value]; bool selected = values[row] == value + 1;
                    button.GetComponent<PanelGraphic>().color = selected ? CivicUIFactory.Teal : CivicUIFactory.Paper;
                    button.GetComponentInChildren<TMP_Text>().color = selected ? CivicUIFactory.Paper : CivicUIFactory.Ink;
                    button.interactable = !Evaluation.completed;
                }
            int answered = values.Count(x => x >= 1);
            evaluationSurveyProgress.text = Evaluation.completed ? "Οι απαντήσεις σου αποθηκεύτηκαν." : answered + " από " + values.Length + " απαντήσεις";
            evaluationSubmitButton.interactable = Evaluation.completed || answered == values.Length;
            evaluationSubmitButton.GetComponentInChildren<TMP_Text>().text = Evaluation.completed ? "Επιστροφή στο παιχνίδι" : "Υποβολή αξιολόγησης";
            if (Evaluation.completed && !modalOpen) ShowEvaluationCompletion();
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
            var title = CivicUIFactory.Text(rect, "Questionnaire title", "Η εμπειρία σου από το παιχνίδι", font, 36, CivicUIFactory.Paper); CivicUIFactory.Box(title.rectTransform, 30, 20, 1800, 56);
            var instruction = CivicUIFactory.Text(rect, "Questionnaire instruction", "Απάντησε με βάση το μέρος του παιχνιδιού που πραγματικά έπαιξες. Σε κάθε πρόταση επίλεξε από 1 έως 5.", font, 24, CivicUIFactory.Paper); CivicUIFactory.Box(instruction.rectTransform, 30, 82, 1900, 70);
            evaluationSurveyScroll = CivicUIFactory.Scroll(rect, "Fourteen evaluation statements", out evaluationSurveyContent);
            var scroll = (RectTransform)evaluationSurveyScroll.transform; CivicUIFactory.Fill(scroll, new Vector2(26, 128), new Vector2(26, 170));
            Canvas.ForceUpdateCanvases();
            float width = Mathf.Max(1200, evaluationSurveyScroll.viewport.rect.width - 20), y = 8;
            var items = EvaluationData.experience.Concat(EvaluationData.participationIntention).ToArray();
            for (int i = 0; i < items.Length; i++)
            {
                if (i == EvaluationData.experience.Length)
                {
                    var section = CivicUIFactory.Text(evaluationSurveyContent, "Participation intention section", "Πρόθεση πολιτικής συμμετοχής", font, 29, CivicUIFactory.Ink); section.fontStyle = FontStyles.Bold;
                    CivicUIFactory.Box(section.rectTransform, 22, y + 10, width - 44, 54); y += 78;
                }
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
            evaluationSurveyContent.sizeDelta = new Vector2(0, y + 8);
            evaluationSurveyProgress = CivicUIFactory.Text(rect, "Answered statement count", "", font, 24, CivicUIFactory.Paper);
            var progressRect = evaluationSurveyProgress.rectTransform; progressRect.anchorMin = progressRect.anchorMax = new Vector2(.04f, .065f); progressRect.pivot = new Vector2(0, .5f); progressRect.anchoredPosition = Vector2.zero; progressRect.sizeDelta = new Vector2(900, 54);
            evaluationSubmitButton = CivicUIFactory.Button(rect, "Submit questionnaire", "Υποβολή αξιολόγησης", font, () => { TryFinishEvaluationSurvey(); }, this);
            var submitRect = (RectTransform)evaluationSubmitButton.transform; submitRect.anchorMin = submitRect.anchorMax = new Vector2(.79f, .065f); submitRect.pivot = new Vector2(.5f, .5f); submitRect.anchoredPosition = Vector2.zero; submitRect.sizeDelta = new Vector2(650, 76);
            targets["evaluation.submit"] = evaluationSubmitButton; targets["final-survey.primary"] = evaluationSubmitButton;
            Canvas.ForceUpdateCanvases(); evaluationSurveyScroll.verticalNormalizedPosition = 1;
        }

        bool TryFinishEvaluationSurvey()
        {
            if (EvaluationData == null || !EvaluationRules.HasPoint(Evaluation.startPoint)) return false;
            if (!Evaluation.completed)
            {
                if (!EvaluationRules.Submit(State, EvaluationData, EvaluationPointNow())) return true;
            }
            if (!SaveCheckpoint()) return true;
            ShowEvaluationCompletion(); return true;
        }

        void ShowEvaluationCompletion()
        {
            ShowEvaluationDialog("Η αξιολόγηση ολοκληρώθηκε", "Οι απαντήσεις σου αποθηκεύτηκαν. Μπορείς να συνεχίσεις από το σημείο όπου σταμάτησες την πορεία σου.",
                "Συνέχεια στο παιχνίδι", ReturnFromEvaluation, null, ReturnFromEvaluation);
        }

        void ReturnFromEvaluation()
        {
            HideEvaluationDialog();
            string destination = Evaluation.returnPhase;
            if (string.IsNullOrEmpty(destination) || destination == "final-survey" || destination == "final-knowledge") destination = "map-stela";
            GoCampaign(destination);
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
            var parent = roots["map-stela"].transform.parent;
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
