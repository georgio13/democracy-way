using System;
using System.Globalization;
using System.Linq;

namespace DemocracyWay.NewUI
{
    // Questionnaire state is distinct from the campaign's narrative outcomes.
    public static class EvaluationRules
    {
        public static EvaluationState Ensure(GameState state, EvaluationContent content)
        {
            if (state == null || content == null) throw new ArgumentNullException(state == null ? nameof(state) : nameof(content));
            if (state.evaluation == null) state.evaluation = new EvaluationState();
            var e = state.evaluation;
            if (string.IsNullOrEmpty(e.instrumentVersion)) e.instrumentVersion = content.instrumentVersion;
            if (string.IsNullOrEmpty(e.researchParticipantId)) e.researchParticipantId = Guid.NewGuid().ToString("N");
            e.baselineAnswers = Normalise(e.baselineAnswers, 8, 0, 3);
            e.postAnswers = Normalise(e.postAnswers, 8, 0, 3);
            e.experienceAnswers = Normalise(e.experienceAnswers, content.experience.Length, 1, 5);
            e.intentionAnswers = Normalise(e.intentionAnswers, content.participationIntention.Length, 1, 5);
            // Unity's field JSON can reconstruct a null nested serializable class
            // as an all-default object. It is still an unstarted assessment.
            e.baselinePoint = NormalisePoint(e.baselinePoint);
            e.offerPoint = NormalisePoint(e.offerPoint);
            e.startPoint = NormalisePoint(e.startPoint);
            e.submitPoint = NormalisePoint(e.submitPoint);
            return e;
        }

        static bool EmptyPoint(EvaluationPoint p) => p == null ||
            string.IsNullOrEmpty(p.utc) && string.IsNullOrEmpty(p.caseId) && string.IsNullOrEmpty(p.lastCompletedCaseId) &&
            string.IsNullOrEmpty(p.contentVersion) && string.IsNullOrEmpty(p.phase) && p.prytany == 0 && p.week == 0 &&
            p.turn == 0 && p.completedCases == 0 && p.activeSeconds == 0 && p.progress == 0;
        static EvaluationPoint NormalisePoint(EvaluationPoint point) => EmptyPoint(point) ? null : point;
        public static bool HasPoint(EvaluationPoint p) => p != null &&
            !string.IsNullOrWhiteSpace(p.utc) && DateTimeOffset.TryParse(p.utc, CultureInfo.InvariantCulture, DateTimeStyles.None, out var utc) && utc.Offset == TimeSpan.Zero &&
            !string.IsNullOrWhiteSpace(p.caseId) && !string.IsNullOrWhiteSpace(p.contentVersion) &&
            p.prytany >= 1 && p.prytany <= 10 && p.week >= 1 && p.week <= 5 &&
            p.turn == (p.prytany - 1) * 5 + p.week - 1 && p.completedCases >= 0 && p.completedCases <= p.turn + 1 &&
            !float.IsNaN(p.activeSeconds) && !float.IsInfinity(p.activeSeconds) && p.activeSeconds >= 0 &&
            !float.IsNaN(p.progress) && !float.IsInfinity(p.progress) && Math.Abs(p.progress - p.completedCases / 50f) < .0001f;

        static int[] Normalise(int[] answers, int size, int min, int max)
        {
            if (answers != null && answers.Length == size) return answers;
            var result = Enumerable.Repeat(-1, size).ToArray();
            if (answers != null) for (int i = 0; i < Math.Min(size, answers.Length); i++) if (answers[i] >= min && answers[i] <= max) result[i] = answers[i];
            return result;
        }

        public static EvaluationPoint Point(GameState state, string caseId, string contentVersion)
        {
            var completed = state.campaign?.cases?.Where(c => c.completed).OrderBy(c => c.turn).ToArray();
            return new EvaluationPoint { utc = DateTime.UtcNow.ToString("O"), caseId = caseId, contentVersion = contentVersion, phase = state.phase,
                lastCompletedCaseId = completed?.LastOrDefault()?.caseId, prytany = state.prytany, week = state.week,
                turn = CampaignRules.Turn(state), completedCases = completed?.Length ?? 0, activeSeconds = state.activeSeconds,
                progress = (completed?.Length ?? 0) / 50f };
        }

        public static bool AnswerBaseline(GameState state, EvaluationContent content, int answer, EvaluationPoint point)
        {
            var e = Ensure(state, content);
            if (e.baselineComplete || e.baselineFeedbackPending || e.baselineIndex < 0 || e.baselineIndex >= 8 || answer < 0 || answer > 3 || !HasPoint(point) || e.baselinePoint != null && !HasPoint(e.baselinePoint)) return false;
            if (e.baselinePoint == null) e.baselinePoint = point;
            e.baselineAnswers[e.baselineIndex] = answer;
            e.baselineFeedbackPending = true;
            MirrorCampaign(state, e);
            return true;
        }

        public static bool ContinueBaseline(GameState state, EvaluationContent content)
        {
            var e = Ensure(state, content);
            if (!e.baselineFeedbackPending || e.baselineComplete) return false;
            e.baselineFeedbackPending = false;
            e.baselineIndex++;
            e.baselineComplete = e.baselineIndex >= 8;
            MirrorCampaign(state, e);
            return true;
        }

        public static bool Available(GameState state, EvaluationContent content) => Ensure(state, content).baselineComplete && (state.campaign?.cases?.Count(c => c.completed) ?? 0) >= content.minimumCompletedCases;
        public static bool ShouldOffer(GameState state, EvaluationContent content) => Available(state, content) && state.prytany >= content.offerAtPrytany && !state.evaluation.offered && !state.evaluation.completed;
        public static bool Offer(GameState state, EvaluationContent content, EvaluationPoint point)
        {
            if (!ShouldOffer(state, content) || !HasPoint(point)) return false;
            state.evaluation.offered = true; state.evaluation.offerPoint = point;
            return true;
        }
        public static bool Defer(GameState state, EvaluationContent content)
        {
            if (!Available(state, content) || state.evaluation.completed) return false;
            state.evaluation.deferred = true;
            return true;
        }
        public static bool Begin(GameState state, EvaluationContent content, EvaluationPoint point, string returnPhase)
        {
            if (!Available(state, content) || state.evaluation.completed || !HasPoint(point)) return false;
            var e = state.evaluation;
            if (e.startPoint != null && !HasPoint(e.startPoint)) return false;
            if (e.startPoint == null) e.startPoint = point;
            e.returnPhase = string.IsNullOrEmpty(returnPhase) ? "map-stela" : returnPhase;
            e.deferred = false;
            return true;
        }
        public static bool AnswerPost(GameState state, EvaluationContent content, int answer)
        {
            var e = Ensure(state, content);
            if (!HasPoint(e.startPoint) || e.postComplete || e.postIndex < 0 || e.postIndex >= 8 || answer < 0 || answer > 3) return false;
            e.postAnswers[e.postIndex++] = answer;
            e.postComplete = e.postIndex >= 8;
            MirrorCampaign(state, e);
            return true;
        }
        public static bool Rate(GameState state, EvaluationContent content, bool intention, int index, int value)
        {
            var e = Ensure(state, content);
            var answers = intention ? e.intentionAnswers : e.experienceAnswers;
            if (!HasPoint(e.startPoint) || !e.postComplete || e.completed || index < 0 || index >= answers.Length || value < 1 || value > 5) return false;
            answers[index] = value;
            return true;
        }
        public static bool Submit(GameState state, EvaluationContent content, EvaluationPoint point)
        {
            var e = Ensure(state, content);
            if (e.completed || !e.postComplete || !HasPoint(e.startPoint) || !HasPoint(point) || e.experienceAnswers.Any(x => x < 1) || e.intentionAnswers.Any(x => x < 1)) return false;
            e.completed = e.surveyComplete = true;
            e.submitPoint = point;
            MirrorCampaign(state, e);
            return true;
        }
        static void MirrorCampaign(GameState state, EvaluationState e)
        {
            if (state.campaign == null) state.campaign = new CampaignState();
            state.campaign.initialAnswers = (int[])e.baselineAnswers.Clone();
            state.campaign.initialIndex = Math.Min(8, e.baselineIndex);
            state.campaign.initialComplete = e.baselineComplete;
            state.campaign.finalAnswers = (int[])e.postAnswers.Clone();
            state.campaign.finalIndex = Math.Min(8, e.postIndex);
            state.campaign.finalComplete = e.postComplete;
            state.campaign.surveyComplete = e.surveyComplete;
        }
        public static bool Valid(EvaluationState e)
        {
            if (e == null) return true;
            if (e.baselineAnswers == null || e.baselineAnswers.Length != 8 || e.postAnswers == null || e.postAnswers.Length != 8 || e.experienceAnswers == null || e.experienceAnswers.Length != 12 || e.intentionAnswers == null || e.intentionAnswers.Length != 2) return false;
            if (e.baselineIndex < 0 || e.baselineIndex > 8 || e.postIndex < 0 || e.postIndex > 8) return false;
            if (e.baselineAnswers.Concat(e.postAnswers).Any(x => x < -1 || x > 3) || e.experienceAnswers.Concat(e.intentionAnswers).Any(x => x != -1 && (x < 1 || x > 5))) return false;
            if (new[] { e.baselinePoint, e.offerPoint, e.startPoint, e.submitPoint }.Any(p => !EmptyPoint(p) && !HasPoint(p))) return false;
            return !e.completed || e.postComplete && e.surveyComplete && HasPoint(e.startPoint) && HasPoint(e.submitPoint) && !e.experienceAnswers.Concat(e.intentionAnswers).Any(x => x < 1);
        }
    }
}
