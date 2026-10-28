using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    [Serializable]
    public sealed class GameState
    {
        public const string DefaultPlayerName = "Νικόμαχος";
        public string runId;
        public int contentVersion;
        public float activeSeconds;
        public EvaluationState evaluation = new EvaluationState();
        public string phase = "prologue";
        public int prologueIndex;
        public int biographyIndex;
        public string playerName = DefaultPlayerName;
        public int tribeIndex;
        public int economicClassIndex = 2;
        public bool identityComplete;
        public bool biographyComplete;
        public int playerAge = 30;
        public string profession;
        public string demeId, trittysId;
        public CityMetricDelta biographyCityApplied = new CityMetricDelta();
        public string trittys = "Δεν έχει οριστεί";
        public string deme = "Δεν έχει οριστεί";
        public string offices = "Βουλευτής · Λογιστής της Βουλής";
        public CharacterMetrics baseline = new CharacterMetrics();
        public CharacterMetrics metrics = new CharacterMetrics();
        public int basePersonalFunds;
        public int baseCityFunds;
        public int personalFunds;
        public int cityFunds;
        public List<ChoiceRecord> choices = new List<ChoiceRecord>();
        public int prytany = 1;
        public int week = 1;
        public int marketVisitedTurn = -1;   // ο γύρος που ο παίκτης επισκέφθηκε την Αγορά — προϋπόθεση για την υπόθεση
        public int currentTribe = -1;
        public List<int> drawnTribes = new List<int>();
        public CampaignState campaign = new CampaignState();

        // Ενιαίο σύστημα: η πόλη και ο πολίτης του Agora ζουν μέσα στο ίδιο save.
        public Agora.CityState polis = new Agora.CityState();
        public Agora.PlayerState citizen = new Agora.PlayerState();
        public AgoraCaseState agoraCase = new AgoraCaseState();
    }
}
