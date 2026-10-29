using System;
using System.Globalization;
using System.Linq;

namespace DemocracyWay.NewUI
{
    /// <summary>Separate pre/post knowledge, pre/post tendencies, and final experience responses.</summary>
    public static class EvaluationRules
    {
        // Σταθερό αναγνωριστικό των ερωτηματολογίων (δεν υπάρχει πια Protocol/έκδοση στο περιεχόμενο):
        // το χρειάζονται η τηλεμετρία και η εξαγωγή για να ταιριάζουν τις απαντήσεις με τις ερωτήσεις.
        public const string CurrentInstrumentVersion = "campaign-final-meega-el-6";   // βήμα 4: νέο τεστ γνώσεων (Κ9, Κ11) και εισαγωγή του τελικού τεστ
        static int[] Resize(int[] values, int length, int min, int max)
        {
            var result = Enumerable.Repeat(-1, length).ToArray();
            if (values != null) for (int i = 0; i < Math.Min(length, values.Length); i++) if (values[i] >= min && values[i] <= max) result[i] = values[i];
            return result;
        }
        public static EvaluationState Ensure(GameState state, EvaluationContent content)
        {
            if (state == null || content == null) throw new ArgumentNullException();
            var e = state.evaluation ?? (state.evaluation = new EvaluationState());
            if (!string.IsNullOrEmpty(e.instrumentVersion) && e.instrumentVersion != content.instrumentVersion)
            {
                var archived = new EvaluationSurveyArchive { instrumentVersion = e.instrumentVersion, administrationVersion = e.administrationVersion,
                    submissionId = e.submissionId, openFeedback = e.openFeedback, completed = e.completed,
                    experienceItemIds = e.experienceItemIds, intentionItemIds = e.intentionItemIds,
                    experienceAnswers = e.experienceAnswers, intentionAnswers = e.intentionAnswers,
                    offerPoint = e.offerPoint, startPoint = e.startPoint, submitPoint = e.submitPoint };
                e.archivedSurveys = (e.archivedSurveys ?? Array.Empty<EvaluationSurveyArchive>()).Concat(new[] { archived }).ToArray();
                e.experienceAnswers = null; e.intentionAnswers = null; e.completed = e.surveyComplete = false;
                e.startPoint = e.submitPoint = null; e.submissionId = null;
            }
            e.instrumentVersion = content.instrumentVersion;
            e.administrationVersion = "knowledge-tendencies-participation-prologue-biography-20cases-endings-post-demographics-meega-v5";
            e.exposure = "before-prologue";
            if (string.IsNullOrEmpty(e.researchParticipantId)) e.researchParticipantId = Guid.NewGuid().ToString("N");
            e.baselineAnswers = Resize(e.baselineAnswers, content.onboarding.Length, 0, 3);
            e.postAnswers = Resize(e.postAnswers, content.postTest.Length, 0, 3);
            e.baselineOptionOrders = ResizeOrders(e.baselineOptionOrders, content.onboarding.Length);
            e.postOptionOrders = ResizeOrders(e.postOptionOrders, content.postTest.Length);
            e.baselineTendencyAnswers = Resize(e.baselineTendencyAnswers, content.tendencies.Length, 1, 5);
            e.finalTendencyAnswers = Resize(e.finalTendencyAnswers, content.tendencies.Length, 1, 5);
            e.tendencyItemIds = content.tendencies.Select(x => x.id).ToArray();
            e.baselineParticipationAnswers = Resize(e.baselineParticipationAnswers, content.participation.Length, 1, 5);
            e.finalParticipationAnswers = Resize(e.finalParticipationAnswers, content.participation.Length, 1, 5);
            e.participationItemIds = content.participation.Select(x => x.id).ToArray();
            e.experienceAnswers = Resize(e.experienceAnswers, content.experience.Length, 1, 5);   // παλιά «0 = δεν μπορώ να κρίνω» → αναπάντητο
            e.intentionAnswers = Array.Empty<int>(); e.intentionItemIds = Array.Empty<string>();
            e.experienceItemIds = content.experience.Select(x => x.id).ToArray();
            e.baselinePoint = Normalise(e.baselinePoint); e.postPoint = Normalise(e.postPoint);
            e.startPoint = Normalise(e.startPoint); e.submitPoint = Normalise(e.submitPoint);
            e.baselineTendencyPoint = Normalise(e.baselineTendencyPoint); e.finalTendencyPoint = Normalise(e.finalTendencyPoint);
            e.baselineParticipationPoint = Normalise(e.baselineParticipationPoint); e.finalParticipationPoint = Normalise(e.finalParticipationPoint);
            e.postTestStatus = e.postComplete ? "complete" : e.postIndex > 0 ? "partial" : HasPoint(e.postPoint) ? "in_progress" : "not_started";
            return e;
        }
        static string[] ResizeOrders(string[] values, int length)
        {
            var result = Enumerable.Repeat("", length).ToArray();
            if (values != null) for (int i = 0; i < Math.Min(length, values.Length); i++) result[i] = values[i] ?? "";
            return result;
        }
        static EvaluationPoint Normalise(EvaluationPoint p) => p == null || string.IsNullOrEmpty(p.utc) ? null : p;
        public static bool HasPoint(EvaluationPoint p)
        {
            if (p == null) return false;
            int width = p.casesPerPrytany > 0 ? p.casesPerPrytany : 5;
            int total = p.totalCases > 0 ? p.totalCases : 50;
            return !string.IsNullOrWhiteSpace(p.utc) && DateTimeOffset.TryParse(p.utc, CultureInfo.InvariantCulture, DateTimeStyles.None, out var utc) && utc.Offset == TimeSpan.Zero &&
                !string.IsNullOrWhiteSpace(p.caseId) && !string.IsNullOrWhiteSpace(p.contentVersion) && p.prytany >= 1 && p.prytany <= 10 && p.week >= 1 && p.week <= width &&
                p.turn == (p.prytany - 1) * width + p.week - 1 && p.completedCases >= 0 && p.completedCases <= p.turn + 1 &&
                !float.IsNaN(p.activeSeconds) && !float.IsInfinity(p.activeSeconds) && p.activeSeconds >= 0 &&
                !float.IsNaN(p.progress) && !float.IsInfinity(p.progress) && Math.Abs(p.progress - p.completedCases / (float)total) < .0001f;
        }
        public static EvaluationPoint Point(GameState state, string caseId, string contentVersion)
        {
            var done = state.campaign.cases.Where(c => c.completed).OrderBy(c => c.turn).ToArray();
            return new EvaluationPoint { utc = DateTime.UtcNow.ToString("O"), caseId = caseId, contentVersion = contentVersion, phase = state.phase,
                lastCompletedCaseId = done.LastOrDefault()?.caseId, prytany = state.prytany, week = state.week,
                casesPerPrytany = CampaignRules.CasesPerPrytany, totalCases = CampaignRules.TotalCases,
                turn = CampaignRules.Turn(state), completedCases = done.Length, activeSeconds = state.activeSeconds, progress = done.Length / (float)CampaignRules.TotalCases };
        }
        /// <summary>shownOrder: τα ids των επιλογών με τη σειρά που εμφανίστηκαν (η σειρά είναι τυχαία).</summary>
        public static bool AnswerBaseline(GameState state, EvaluationContent content, int answer, EvaluationPoint point, string shownOrder = null)
        {
            var e = Ensure(state, content);
            if (e.baselineComplete || answer < 0 || answer > 3 || e.baselineIndex >= content.onboarding.Length || !HasPoint(point)) return false;
            e.baselinePoint = e.baselinePoint ?? point;
            e.baselineAnswers[e.baselineIndex] = answer;
            e.baselineOptionOrders[e.baselineIndex] = shownOrder ?? "";
            e.baselineIndex++; e.baselineComplete = e.baselineIndex == content.onboarding.Length;
            return true;
        }
        public static bool AnswerTendency(GameState state, EvaluationContent content, bool final, int answer, EvaluationPoint point)
        {
            var e = Ensure(state, content);
            if (answer < 1 || answer > 5 || !HasPoint(point)) return false;
            if (final)
            {
                if (!FullCampaignComplete(state) || !e.postComplete || e.finalTendencyComplete || e.finalTendencyIndex >= content.tendencies.Length) return false;
                e.finalTendencyPoint = e.finalTendencyPoint ?? point;
                e.finalTendencyAnswers[e.finalTendencyIndex++] = answer;
                e.finalTendencyComplete = e.finalTendencyIndex == content.tendencies.Length;
            }
            else
            {
                if (!e.baselineComplete || e.baselineTendencyComplete || e.baselineTendencyIndex >= content.tendencies.Length) return false;
                e.baselineTendencyPoint = e.baselineTendencyPoint ?? point;
                e.baselineTendencyAnswers[e.baselineTendencyIndex++] = answer;
                e.baselineTendencyComplete = e.baselineTendencyIndex == content.tendencies.Length;
            }
            return true;
        }
        public static bool AnswerParticipation(GameState state, EvaluationContent content, bool final, int answer, EvaluationPoint point)
        {
            var e = Ensure(state, content);
            if (answer < 1 || answer > 5 || !HasPoint(point)) return false;
            if (final)
            {
                if (!FullCampaignComplete(state) || !e.finalTendencyComplete || e.finalParticipationComplete || e.finalParticipationIndex >= content.participation.Length) return false;
                e.finalParticipationPoint = e.finalParticipationPoint ?? point;
                e.finalParticipationAnswers[e.finalParticipationIndex++] = answer;
                e.finalParticipationComplete = e.finalParticipationIndex == content.participation.Length;
            }
            else
            {
                if (!e.baselineTendencyComplete || e.baselineParticipationComplete || e.baselineParticipationIndex >= content.participation.Length) return false;
                e.baselineParticipationPoint = e.baselineParticipationPoint ?? point;
                e.baselineParticipationAnswers[e.baselineParticipationIndex++] = answer;
                e.baselineParticipationComplete = e.baselineParticipationIndex == content.participation.Length;
            }
            return true;
        }
        public static bool FullCampaignComplete(GameState state) => (state?.campaign?.cases?.Count(c => c.completed) ?? 0) == CampaignRules.TotalCases;
        public static bool Available(GameState state, EvaluationContent content) => Ensure(state, content).baselineComplete && state.evaluation.baselineTendencyComplete && FullCampaignComplete(state);
        public static bool Begin(GameState state, EvaluationContent content, EvaluationPoint point)
        {
            if (!Available(state, content) || !HasPoint(point)) return false;
            var e = state.evaluation;
            e.startPoint = e.startPoint ?? point;
            if (string.IsNullOrEmpty(e.submissionId)) e.submissionId = Guid.NewGuid().ToString("N");
            return true;
        }
        public static bool BeginPost(GameState state, EvaluationContent content, EvaluationPoint point)
        {
            if (!Begin(state, content, point)) return false;
            var e = state.evaluation; e.postPoint = e.postPoint ?? point;
            e.postTestStatus = e.postComplete ? "complete" : "in_progress"; return true;
        }
        public static bool AnswerPost(GameState state, EvaluationContent content, int answer, string shownOrder = null)
        {
            var e = Ensure(state, content);
            if (!FullCampaignComplete(state) || !HasPoint(e.postPoint) || e.postComplete || e.postIndex >= content.postTest.Length || answer < 0 || answer > 3) return false;
            e.postOptionOrders[e.postIndex] = shownOrder ?? "";
            e.postAnswers[e.postIndex++] = answer; e.postComplete = e.postIndex == content.postTest.Length;
            e.postTestStatus = e.postComplete ? "complete" : "partial"; return true;
        }
        public static bool ValidRating(EvaluationRating item, int value) => value >= 1 && value <= 5;
        public static bool QuestionnaireAnswered(EvaluationState e, EvaluationContent content) => e.experienceAnswers.Length == content.experience.Length && content.experience.Select((x, i) => ValidRating(x, e.experienceAnswers[i])).All(x => x);
        static bool Ready(EvaluationState e) => HasPoint(e.startPoint) && e.postComplete && e.finalTendencyComplete;
        public static bool Rate(GameState state, EvaluationContent content, bool intention, int index, int value)
        {
            var e = Ensure(state, content);
            if (intention || !Ready(e) || e.completed || index < 0 || index >= content.experience.Length || !ValidRating(content.experience[index], value)) return false;
            e.experienceAnswers[index] = value; return true;
        }
        public static bool SetFeedback(GameState state, EvaluationContent content, string feedback)
        {
            var e = Ensure(state, content); feedback = (feedback ?? "").Trim();
            if (!Ready(e) || e.completed || feedback.Length > Math.Min(1000, content.openFeedbackMaxLength)) return false;
            e.openFeedback = feedback; return true;
        }
        public static bool Submit(GameState state, EvaluationContent content, EvaluationPoint point)
        {
            var e = Ensure(state, content);
            if (e.completed || !Ready(e) || !HasPoint(point) || !FullCampaignComplete(state) || !QuestionnaireAnswered(e, content)) return false;
            e.completed = e.surveyComplete = true; e.submitPoint = point; return true;
        }
        public static bool Valid(EvaluationState e)
        {
            if (e == null) return true;
            if (!DemographicsRules.Valid(e.demographics)) return false;
            bool Answers(int[] a, int min, int max) => a != null && a.Length <= 200 && a.All(x => x == -1 || x >= min && x <= max);
            if (!Answers(e.baselineAnswers, 0, 3) || !Answers(e.postAnswers, 0, 3) || !Answers(e.experienceAnswers, 0, 5) || !Answers(e.intentionAnswers, 1, 5) ||
                !Answers(e.baselineTendencyAnswers, 1, 5) || !Answers(e.finalTendencyAnswers, 1, 5) ||
                !Answers(e.baselineParticipationAnswers, 1, 5) || !Answers(e.finalParticipationAnswers, 1, 5)) return false;
            if (e.baselineIndex < 0 || e.baselineIndex > e.baselineAnswers.Length || e.postIndex < 0 || e.postIndex > e.postAnswers.Length ||
                e.baselineTendencyIndex < 0 || e.baselineTendencyIndex > e.baselineTendencyAnswers.Length || e.finalTendencyIndex < 0 || e.finalTendencyIndex > e.finalTendencyAnswers.Length ||
                e.baselineParticipationIndex < 0 || e.baselineParticipationIndex > e.baselineParticipationAnswers.Length || e.finalParticipationIndex < 0 || e.finalParticipationIndex > e.finalParticipationAnswers.Length) return false;
            foreach (var p in new[] { e.baselinePoint, e.postPoint, e.startPoint, e.submitPoint, e.baselineTendencyPoint, e.finalTendencyPoint, e.baselineParticipationPoint, e.finalParticipationPoint }) if (Normalise(p) != null && !HasPoint(p)) return false;
            return !e.completed || e.surveyComplete && Ready(e) && HasPoint(e.submitPoint) && e.experienceAnswers.All(x => x >= 0);
        }
    }
}
