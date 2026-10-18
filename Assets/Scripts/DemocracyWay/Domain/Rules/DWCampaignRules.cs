using System;
using System.Linq;

namespace DemocracyWay.NewUI
{
    // Pure rules: never require scene objects and never invent participant observations.
    public static class DWCampaignRules
    {
        public static int Turn(DWGameState state) => (state.prytany - 1) * 5 + state.week - 1;
        public static DWCaseRecord Current(DWGameState state)
        {
            if (state.campaign == null)
                state.campaign = new DWCampaignState();
            var found = state.campaign.cases.Find(x => x.turn == Turn(state));
            if (found == null)
            {
                found = new DWCaseRecord
                {
                    turn = Turn(state)
                };
                state.campaign.cases.Add(found);
            }

            return found;
        }

        public static void Record(DWGameState state, string key, string choice, DWMetrics metrics, int city = 0, int personal = 0, string lean = "mixed")
        {
            var record = state.choices.Find(x => x.stepId == key);
            if (record == null)
            {
                record = new DWChoiceRecord
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
            DWGameRules.Recalculate(state);
        }

        public static bool Answer(DWGameState state, int answer)
        {
            var record = Current(state);
            if (answer < 0 || answer > 2 || record.knowledge >= 0)
                return false;
            record.knowledge = answer;
            return true;
        }

        public static bool Speak(DWGameState state, int side, int bonus)
        {
            var record = Current(state);
            if (record.completed || record.speech >= 0 || side < 0 || side > 2)
                return false;
            record.speech = side;
            record.influence = side == 2 ? 0 : Math.Min(22, Math.Max(0, state.metrics.persuasion) / 10 + Math.Max(0, bonus));
            Record(state, "case-" + record.turn + "-speech", side == 2 ? "silent" : side == 0 ? "public-accountability" : "restricted-authority", new DWMetrics { parrhesia = side == 2 ? 0 : 3, recognition = side == 2 ? 0 : 2, authority = side == 1 ? 2 : 0 }, lean: side == 0 ? "democratic" : side == 1 ? "oligarchic" : "mixed");
            return true;
        }

        public static bool Vote(DWGameState state, int side, int costA, int costB, int war, bool observation = false)
        {
            var record = Current(state);
            if (record.completed || record.knowledge < 0 || side < 0 || side > 2)
                return false;
            if (record.speech < 0)
                record.speech = 2;
            record.vote = side;
            int support = state.city == null || state.city.officialSupport == null ? 0 : state.city.officialSupport.Count(x => x > 0) * 2;
            int publicScore = 48 + (record.turn % 5 - 2) * 5 + (record.speech == 0 ? record.influence : record.speech == 1 ? -record.influence : 0) + (side == 0 ? 6 : side == 1 ? -6 : 0) + (side == 0 ? support : side == 1 ? -support : 0);
            record.majority = publicScore >= 50 ? 0 : 1;
            record.cityDelta = observation ? 0 : record.majority == 0 ? costA : costB;
            // In-game budgets cannot spend below zero. Units are game units, not ancient price claims.
            record.cityDelta = Math.Max(-state.cityFunds, record.cityDelta);
            record.personalDelta = 0;
            record.completed = true;
            if (!observation)
            {
                state.campaign.war += record.majority == 0 ? war : -war;
                state.campaign.readiness += record.majority == 0 ? 2 : 1;
                state.campaign.legitimacy += record.majority == 0 ? 1 : -1;
            }

            Record(state, "case-" + record.turn + "-vote", (observation ? "reflection-" : "vote-") + side, new DWMetrics { consistency = side == record.speech && side != 2 ? 1 : 0 }, record.cityDelta, 0, side == 0 ? "democratic" : side == 1 ? "oligarchic" : "mixed");
            return true;
        }

        public static string Advance(DWGameState state)
        {
            if (!Current(state).completed)
                return null;
            if (state.week < 5)
            {
                state.week++;
                state.phase = "week-transition";
                return state.phase;
            }

            state.phase = "prytany-transition";
            return state.phase;
        }

        public static string NextPrytany(DWGameState state)
        {
            if (state.phase != "prytany-transition" || !Current(state).completed)
                return null;
            if (state.prytany >= 10)
            {
                Endings(state);
                state.phase = "ending-city";
                return state.phase;
            }

            state.prytany++;
            state.week = 1;
            state.currentTribe = -1;
            state.phase = "prytany-title";
            return state.phase;
        }

        public static void Endings(DWGameState state)
        {
            var campaign = state.campaign;
            campaign.cityEnding = campaign.war >= 8 ? (campaign.readiness >= 65 && state.cityFunds >= 60 ? "victory" : "defeat") : campaign.war <= -8 ? (campaign.legitimacy <= -20 ? "subjection" : "agreement") : campaign.legitimacy >= 0 ? "sovereignty" : "agreement";
            campaign.personalEnding = state.city != null && state.city.corruptionRisk >= 4 ? "trial" : campaign.cityEnding == "defeat" && campaign.cases.Count(x => x.vote == 0) > 20 ? "exile" : state.metrics.recognition >= 55 && campaign.cityEnding == "victory" ? "honour" : campaign.cityEnding == "agreement" && state.metrics.persuasion >= 45 ? "envoy" : campaign.cityEnding == "subjection" ? "home" : "citizen";
        }

        public static bool ValidCampaign(DWCampaignState campaign)
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
