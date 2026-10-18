using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    [Serializable]
    public sealed class GameState
    {
        public string runId;
        public int contentVersion;
        public string phase = "prologue";
        public int prologueIndex;
        public int biographyIndex;
        public string playerName = "Αθηναίος";
        public int tribeIndex;
        public int economicClassIndex = 2;
        public bool identityComplete;
        public bool biographyComplete;
        public int playerAge = 30;
        public string profession;
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
        public int currentTribe = -1;
        public List<int> drawnTribes = new List<int>();
        public CityState city = new CityState();
        public CampaignState campaign = new CampaignState();
    }
}
