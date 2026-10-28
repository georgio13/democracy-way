using System.Linq;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Πρόοδος εκστρατείας και έλεγχοι εγκυρότητας saves. Οι κανόνες του παιχνιδιού
    /// ζουν αποκλειστικά στο Agora.AssemblyRules / Agora.EndingRules.
    /// </summary>
    public static class CampaignRules
    {
        public static int CasesPerPrytany => JsonContent.Settings.casesPerPrytany;
        public static int TotalCases => JsonContent.Settings.prytanyCount * CasesPerPrytany;
        public static int Turn(GameState state) => (state.prytany - 1) * CasesPerPrytany + state.week - 1;

        /// <summary>Ταυτίζεται με το CaseData.id του περιεχομένου (1..20).</summary>
        public static string CaseIdForTurn(int turn) => (turn + 1).ToString();

        public static CaseRecord Current(GameState state)
        {
            if (state.campaign == null)
                state.campaign = new CampaignState();
            var found = state.campaign.cases.Find(x => x.turn == Turn(state));
            if (found == null)
            {
                found = new CaseRecord { turn = Turn(state) };
                state.campaign.cases.Add(found);
            }
            return found;
        }

        public static bool ValidCampaign(CampaignState campaign)
        {
            if (campaign == null)
                return true;
            if (campaign.cases == null || campaign.cases.Count > TotalCases || campaign.survey == null || campaign.survey.Length != 2)
                return false;
            if (campaign.survey.Any(a => a != -1 && (a < 1 || a > 7)))
                return false;
            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (var record in campaign.cases)
                if (record == null || record.turn < 0 || record.turn >= TotalCases || !seen.Add(record.turn) || record.vote < -1 || record.vote > 1 || record.majority < -1 || record.majority > 1 || (record.completed && (record.vote < 0 || record.majority < 0)))
                    return false;
            return true;
        }

        public static bool ValidPhase(string id) => !string.IsNullOrEmpty(id) && (id == "demographics"
            || id == "procedure-briefing" || id == "case-prep" || id == "case-trial" || id == "deliberation"
            || id == "speech" || id == "vote" || id == "vote-result" || id == "week-report"
            || id == "week-transition" || id == "prytany-transition"
            || id == "initial-knowledge" || id == "initial-tendencies" || id == "final-tendencies"
            || id == "final-knowledge" || id == "final-survey"
            || id == "ending-city" || id == "ending-personal" || id == "complete");
    }
}
