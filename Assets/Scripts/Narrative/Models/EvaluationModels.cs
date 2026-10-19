using System;

namespace DemocracyWay.NewUI
{
    [Serializable]
    public sealed class EvaluationContent
    {
        public int schemaVersion, minimumCompletedCases = 5, offerAtPrytany = 2;
        public string instrumentVersion, language, offerTitle, offerBody, offerNow, offerLater;
        public string questionnaireStatus, knowledgeDesignNote;
        public string[] ratingLabels;
        public EvaluationQuestion[] onboarding, postTest;
        public EvaluationRating[] experience, participationIntention;
        public EvaluationSource[] sources;
    }

    [Serializable]
    public sealed class EvaluationQuestion
    {
        public string id, construct, prompt, feedback, identityTemplate, sourceRef, sourceUrl;
        public string[] answers;
        public int correctIndex;
    }

    [Serializable]
    public sealed class EvaluationRating
    {
        public string id, construct, prompt, sourceRef;
        public bool reverse;
    }

    [Serializable]
    public sealed class EvaluationSource { public string id, citation, url; }

    [Serializable]
    public sealed class EvaluationPoint
    {
        public string utc, caseId, lastCompletedCaseId, contentVersion, phase;
        public int prytany, week, turn, completedCases;
        public float activeSeconds, progress;
    }

    [Serializable]
    public sealed class EvaluationState
    {
        public string instrumentVersion, researchParticipantId;
        public string exposure = "biography+baseline-feedback";
        public string returnPhase = "map-stela";
        public int baselineIndex, postIndex;
        public int[] baselineAnswers = Empty(8), postAnswers = Empty(8);
        public int[] experienceAnswers = Empty(12), intentionAnswers = Empty(2);
        public bool baselineFeedbackPending, baselineComplete, postComplete, surveyComplete;
        public bool offered, deferred, completed;
        public EvaluationPoint baselinePoint, offerPoint, startPoint, submitPoint;
        static int[] Empty(int length) { var a = new int[length]; for (int i = 0; i < length; i++) a[i] = -1; return a; }
    }
}
