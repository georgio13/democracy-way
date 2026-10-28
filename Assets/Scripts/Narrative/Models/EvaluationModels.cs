using System;

namespace DemocracyWay.NewUI
{
    [Serializable]
    public sealed class EvaluationContent
    {
        public int schemaVersion, minimumCompletedCases = 20, offerAtPrytany = 10;
        public string instrumentVersion, language, offerTitle, offerBody, offerNow, offerLater;
        public string questionnaireStatus, knowledgeDesignNote;
        public string questionnaireInstructions, openFeedbackPrompt;
        public int openFeedbackMaxLength = 1000;
        public bool openFeedbackOptional = true;
        public string[] ratingLabels;
        public EvaluationQuestion[] onboarding, postTest;
        public string knowledgeInstructions;
        public EvaluationRating[] experience, participationIntention;
        public DemographicsContent demographics;
        public TendencyItem[] tendencies;
        public string tendencyInstructions;
        public string[] tendencyRatingLabels;
        public TendencyItem[] participation;
        public string participationInstructions;
        public string[] participationRatingLabels;
        public EvaluationSource[] sources;
    }

    [Serializable]
    public sealed class EvaluationQuestion
    {
        public string id, prompt, correctId;
        public KnowledgeAnswer[] answers;
    }

    [Serializable]
    public sealed class KnowledgeAnswer { public string id, text; }

    [Serializable]
    public sealed class EvaluationRating
    {
        public string id;        // αποθήκευση απαντήσεων
        public string prompt;    // το κείμενο που βλέπει ο παίκτης
        public string section;   // η ομάδα (τίτλος ενότητας) στο ερωτηματολόγιο
    }

    [Serializable]
    public sealed class TendencyItem { public string id, prompt; public bool reverse; }

    [Serializable]
    public sealed class EvaluationSource { public string id, citation, url; }

    [Serializable]
    public sealed class EvaluationPoint
    {
        public string utc, caseId, lastCompletedCaseId, contentVersion, phase;
        public int prytany, week, turn, completedCases, casesPerPrytany, totalCases;
        public float activeSeconds, progress;
    }

    [Serializable]
    public sealed class EvaluationState
    {
        public DemographicsState demographics = new DemographicsState();
        public string instrumentVersion, researchParticipantId;
        public string[] tendencyItemIds;
        public int[] baselineTendencyAnswers = Array.Empty<int>(), finalTendencyAnswers = Array.Empty<int>();
        public int baselineTendencyIndex, finalTendencyIndex;
        public bool baselineTendencyComplete, finalTendencyComplete;
        public EvaluationPoint baselineTendencyPoint, finalTendencyPoint;
        public string[] participationItemIds;
        public int[] baselineParticipationAnswers = Array.Empty<int>(), finalParticipationAnswers = Array.Empty<int>();
        public int baselineParticipationIndex, finalParticipationIndex;
        public bool baselineParticipationComplete, finalParticipationComplete;
        public EvaluationPoint baselineParticipationPoint, finalParticipationPoint;
        public string submissionId, openFeedback;
        public string[] experienceItemIds, intentionItemIds;
        public string[] instructionsSeen = Array.Empty<string>();   // π.χ. "knowledge-pre": οι οδηγίες δείχνονται μία φορά ανά ερωτηματολόγιο
        public bool synthetic;
        public string syntheticScenario;
        public EvaluationSurveyArchive[] archivedSurveys;
        public string exposure = "biography+baseline-feedback";
        public string administrationVersion = "experience-only-midcampaign-v2";
        public string postTestStatus = "not_started";
        public int baselineIndex, postIndex;
        public int[] baselineAnswers = Empty(8), postAnswers = Empty(8);
        public int[] experienceAnswers = Empty(12), intentionAnswers = Empty(2);
        public bool baselineComplete, postComplete, surveyComplete, onboardingHelpSeen;
        public bool offered, deferred, completed;
        public EvaluationPoint baselinePoint, offerPoint, startPoint, submitPoint, postPoint;
        static int[] Empty(int length) { var a = new int[length]; for (int i = 0; i < length; i++) a[i] = -1; return a; }
    }

    // A changed instrument never reinterprets old positional answers as new items.
    [Serializable]
    public sealed class EvaluationSurveyArchive
    {
        public string instrumentVersion, administrationVersion, submissionId, openFeedback;
        public string[] experienceItemIds, intentionItemIds;
        public int[] experienceAnswers, intentionAnswers;
        public EvaluationPoint offerPoint, startPoint, submitPoint;
        public bool completed;
    }
}
