using System;
using System.Linq;

namespace DemocracyWay.NewUI
{
    // State guards and save-ledger adapter. Narrative calculations execute native articy graphs.
    public static class CampaignRules
    {
        public static int Turn(GameState state) => (state.prytany - 1) * 5 + state.week - 1;
        public static CaseRecord Current(GameState state)
        {
            if (state.campaign == null)
                state.campaign = new CampaignState();
            var found = state.campaign.cases.Find(x => x.turn == Turn(state));
            if (found == null)
            {
                found = new CaseRecord
                {
                    turn = Turn(state)
                };
                state.campaign.cases.Add(found);
            }

            return found;
        }

        public static void Record(GameState state, string key, string choice, CharacterMetrics metrics, int city = 0, int personal = 0, string lean = "mixed")
        {
            var record = state.choices.Find(x => x.stepId == key);
            if (record == null)
            {
                record = new ChoiceRecord
                {
                    stepId = key
                };
                state.choices.Add(record);
            }

            record.choiceId = choice;
            record.metrics = metrics;
            record.cityFunds = city;
            record.personalFunds = personal;
            record.institutionalLean = lean;
            GameRules.Recalculate(state);
        }

        public static bool Answer(GameState state, int answer)
        {
            var record = Current(state);
            if (answer < 0 || answer > 2 || record.knowledge >= 0)
                return false;
            record.knowledge = answer;
            return true;
        }

        public static bool Speak(GameState state, int side, int bonus)
        {
            var record = Current(state);
            if (record.completed || record.speech >= 0 || side < 0 || side > 2) return false;
            using var result = ArticyNarrative.Run("Rule_Speech", state, ("Side", side), ("Bonus", Math.Max(0, bonus)));
            record.speech = side;
            record.influence = result.Number("Influence");
            Record(state, "case-" + record.turn + "-speech", side == 2 ? "silent" : side == 0 ? "public-accountability" : "restricted-authority", new CharacterMetrics { parrhesia = result.Number("ParrhesiaDelta"), recognition = result.Number("RecognitionDelta"), authority = result.Number("AuthorityDelta") }, lean: side == 0 ? "democratic" : side == 1 ? "oligarchic" : "mixed");
            return true;
        }

        public static bool Vote(GameState state, int side, int costA, int costB, int war, bool observation = false)
        {
            var record = Current(state);
            if (record.completed || record.knowledge < 0 || side < 0 || side > 2) return false;
            int speech = record.speech < 0 ? 2 : record.speech;
            int support = state.city?.officialSupport?.Count(x => x > 0) ?? 0;
            using var result = ArticyNarrative.Run("Rule_Vote", state, ("Side", side), ("Speech", speech), ("Turn", record.turn), ("Support", support), ("Influence", record.influence), ("CostA", costA), ("CostB", costB), ("WarInput", war), ("Observation", observation));
            record.speech = speech;
            record.vote = side;
            record.majority = result.Number("Majority");
            record.cityDelta = result.Number("CityDelta");
            record.personalDelta = 0;
            record.completed = true;
            state.campaign.war += result.Number("WarDelta");
            state.campaign.readiness += result.Number("ReadinessDelta");
            state.campaign.legitimacy += result.Number("LegitimacyDelta");
            Record(state, "case-" + record.turn + "-vote", (observation ? "reflection-" : "vote-") + side, new CharacterMetrics { consistency = result.Number("ConsistencyDelta") }, record.cityDelta, 0, side == 0 ? "democratic" : side == 1 ? "oligarchic" : "mixed");
            return true;
        }

        public static string Advance(GameState state)
        {
            if (!Current(state).completed) return null;
            using var result = ArticyNarrative.Run("Rule_AdvanceWeek", state);
            state.week = result.Number("Week");
            state.phase = result.Text("NextPhase");
            return state.phase;
        }

        public static string NextPrytany(GameState state)
        {
            if (state.phase != "prytany-transition" || !Current(state).completed) return null;
            using var result = ArticyNarrative.Run("Rule_AdvancePrytany", state);
            state.phase = result.Text("NextPhase");
            if (state.phase == "ending-city") Endings(state);
            else { state.prytany = result.Number("Prytany"); state.week = result.Number("Week"); state.currentTribe = -1; }
            return state.phase;
        }

        public static void Endings(GameState state)
        {
            using var result = ArticyNarrative.Run("Rule_Endings", state);
            state.campaign.cityEnding = result.Text("CityEnding");
            state.campaign.personalEnding = result.Text("PersonalEnding");
        }

        public static bool ValidCampaign(CampaignState campaign)
        {
            if (campaign == null)
                return true; // Saves written before campaign UI were introduced remain readable.
            if (campaign.cases == null || campaign.cases.Count > 50 || campaign.initialAnswers == null || campaign.finalAnswers == null || campaign.initialAnswers.Length != 8 || campaign.finalAnswers.Length != 8 || campaign.survey == null || campaign.survey.Length != 2 || campaign.initialIndex < 0 || campaign.initialIndex > 8 || campaign.finalIndex < 0 || campaign.finalIndex > 8)
                return false;
            if (campaign.initialAnswers.Any(a => a < -1 || a > 2) || campaign.finalAnswers.Any(a => a < -1 || a > 2) || campaign.survey.Any(a => a != -1 && (a < 1 || a > 7)))
                return false;
            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (var record in campaign.cases)
                if (record == null || record.turn < 0 || record.turn > 49 || !seen.Add(record.turn) || record.knowledge < -1 || record.knowledge > 2 || record.speech < -1 || record.speech > 2 || record.vote < -1 || record.vote > 2 || record.majority < -1 || record.majority > 1 || (record.completed && (record.knowledge < 0 || record.vote < 0 || record.majority < 0)))
                    return false;
            return true;
        }

        public static bool ValidPhase(string id) => !string.IsNullOrEmpty(id) && (id == "appeal" || id == "knowledge" || id == "knowledge-correct" || id == "knowledge-incorrect" || id == "procedure-briefing" || id == "deliberation" || id == "speech" || id == "vote" || id == "vote-result" || id == "vote-abstention" || id == "week-report" || id == "week-transition" || id == "prytany-transition" || id == "initial-knowledge" || id == "final-knowledge" || id == "final-survey" || id == "ending-city" || id == "ending-personal" || id == "complete" || new[]
        {
            "institution-history",
            "institution-deme",
            "institution-areopagus",
            "institution-palladion",
            "institution-delphinion",
            "institution-phreatto",
            "institution-prytaneion"
        }.Contains(id));
    }
}
