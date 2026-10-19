using System;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    // Authored in the case's articy feature. Unity only adapts the published data.
    [Serializable]
    public sealed class PoliticalCaseDefinition
    {
        public int version;
        public int baseSupportA = 42, requiredSupport = 50;
        public int maxSpeakerInfluence = 8, maxWriterInfluence = 4, maxOfficialInfluence = 4;
        public int foodBaseline = 50, foodContextDivisor = 10, trustContextDivisor = 10;
        public int lowFundsThreshold = 6500, lowFundsPenalty = -4;
        public int foodDeltaA, foodDeltaB, trustDeltaA, trustDeltaB, readinessDeltaA, readinessDeltaB;
        public string choiceA, choiceB, outcomeA, outcomeB, historicalStatus;
        public PoliticalDialogueLine[] dialogue;
        public PoliticalSpeechApproach[] approaches;

        public static PoliticalCaseDefinition Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var result = JsonUtility.FromJson<PoliticalCaseDefinition>(value);
            if (result == null || result.version != 1 || result.dialogue == null || result.dialogue.Length == 0 || result.approaches == null || result.approaches.Length != 3)
                throw new InvalidOperationException("Incomplete authored political case.");
            return result;
        }
    }

    [Serializable]
    public sealed class PoliticalDialogueLine
    {
        public string id, speaker, text, speakerRole, stageText, nextLabel;
        public int portraitIndex;
        public int side;
    }

    [Serializable]
    public sealed class PoliticalSpeechApproach
    {
        public string id, label, textForA, textForB, followupFlag;
        public CharacterMetrics weights = new CharacterMetrics();
        public CharacterMetrics metrics = new CharacterMetrics();
        public int argumentBonus = 1;
    }
}
