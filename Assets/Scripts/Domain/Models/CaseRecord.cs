using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    [Serializable]
    public sealed class CaseRecord
    {
        public int turn, knowledge = -1, speech = -1, vote = -1, majority = -1, influence;
        public bool completed;
        public int cityDelta, personalDelta;
        public string caseId, questionId, argumentId, institutionalLean, leanReason, sourceRef;
        public int debateIndex, questionCorrect = -1, selectedAnswer = -1;
        public int debateChunk;
        public string contentVersion;
        public CharacterMetrics metricDelta;
        public string authoredCaseSnapshot, authoredDialogueSnapshot;
        public string politicalVersion, speechApproachId;
        public int pendingSpeechSide = -1, speechApproach = -1;
        public int speakerComponent, writerComponent, officialComponent, supportSide = 2;
        public int baseComponent, foodComponent, trustComponent, fundsComponent, supportTotal, supportThreshold;
        public int foodBefore, foodDelta, trustBefore, trustDelta;
        public bool policyFullyFunded;
        public CharacterMetrics speechMetricsBefore;
    }
}
