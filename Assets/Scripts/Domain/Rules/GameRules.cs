using System;
using System.Collections.Generic;

namespace DemocracyWay.NewUI
{
    // These state transitions contain no scene or input dependencies and are tested offline.
    public static class GameRules
    {
        public static void ApplyChoice(GameState state, BiographyStep step, NarrativeChoice choice)
        {
            if (state == null || step == null || choice == null || string.IsNullOrWhiteSpace(step.id) || string.IsNullOrWhiteSpace(choice.id))
                throw new ArgumentException("Missing biography choice identity.");
            ChoiceRecord record = state.choices.Find(x => x.stepId == step.id);
            if (record == null)
            {
                record = new ChoiceRecord
                {
                    stepId = step.id
                };
                state.choices.Add(record);
            }

            record.choiceId = choice.id;
            record.metrics = choice.metrics != null ? choice.metrics.Copy() : new CharacterMetrics();
            record.personalFunds = choice.personalFunds;
            record.cityFunds = choice.cityFunds;
            record.institutionalLean = choice.institutionalLean;
            record.economicClassIndex = choice.economicClassIndex;
            record.tribeIndex = choice.tribeIndex;
            record.profession = choice.profession;
            record.playerName = choice.playerName;
            record.demeId = choice.demeId;
            record.trittysId = choice.trittysId;
            record.cityMetrics = choice.cityMetrics?.Copy() ?? new CityMetricDelta();
            record.deme = choice.deme;
            record.trittys = choice.trittys;
            Recalculate(state);
        }

        public static void Recalculate(GameState state)
        {
            if (state == null || state.baseline == null || state.choices == null)
                throw new ArgumentException("Incomplete game state.");
            state.metrics = state.baseline.Copy();
            state.personalFunds = state.basePersonalFunds;
            state.cityFunds = state.baseCityFunds;
            var cityChanges = new CityMetricDelta();
            foreach (ChoiceRecord record in state.choices)
            {
                state.metrics.Add(record.metrics);
                state.metrics.Clamp();
                state.personalFunds += record.personalFunds;
                state.cityFunds += record.cityFunds;
                if (record.cityMetrics != null) { cityChanges.food += record.cityMetrics.food; cityChanges.trust += record.cityMetrics.trust; cityChanges.readiness += record.cityMetrics.readiness; cityChanges.war += record.cityMetrics.war; }
                if (!string.IsNullOrEmpty(record.playerName)) state.playerName = record.playerName;
                if (!string.IsNullOrEmpty(record.demeId)) state.demeId = record.demeId;
                if (!string.IsNullOrEmpty(record.trittysId)) state.trittysId = record.trittysId;
                if (record.economicClassIndex >= 0)
                    state.economicClassIndex = record.economicClassIndex;
                if (record.tribeIndex >= 0)
                    state.tribeIndex = record.tribeIndex;
                if (!string.IsNullOrEmpty(record.profession))
                    state.profession = record.profession;
                if (!string.IsNullOrEmpty(record.deme))
                    state.deme = record.deme;
                if (!string.IsNullOrEmpty(record.trittys))
                    state.trittys = record.trittys;
            }

            state.metrics.Clamp();
            if (state.campaign == null) state.campaign = new CampaignState();
            var previous = state.biographyCityApplied ?? new CityMetricDelta();
            state.campaign.foodSecurity = PoliticalCaseRules.Clamp(state.campaign.foodSecurity + cityChanges.food - previous.food, 0, 100);
            state.campaign.legitimacy += cityChanges.trust - previous.trust;
            state.campaign.readiness += cityChanges.readiness - previous.readiness;
            state.campaign.war += cityChanges.war - previous.war;
            state.biographyCityApplied = cityChanges;
        }

        public static bool Compatible(GameState state, GameConfiguration config)
        {
            if (state == null || config == null || state.contentVersion != config.contentVersion || state.prologueIndex < 0 || state.prologueIndex >= config.prologue.Count || state.biographyIndex < 0 || state.biographyIndex >= config.biography.Count || state.tribeIndex < 0 || state.tribeIndex >= config.tribes.Count || state.economicClassIndex < 0 || state.economicClassIndex >= config.economicClasses.Count)
                return false;
            foreach (var record in state.choices)
            {
                var step = config.biography.Find(x => x != null && x.id == record.stepId);
                if (step != null)
                {
                    if (step.choices == null || !step.choices.Exists(x => x != null && x.id == record.choiceId))
                        return false;
                    continue;
                }

                var match = System.Text.RegularExpressions.Regex.Match(record.stepId, @"^case-(\d{1,2})-(speech|vote)$");
                if (match.Success)
                {
                    int turn = int.Parse(match.Groups[1].Value);
                    var entry = state.campaign?.cases?.Find(x => x.turn == turn);
                    if (entry == null)
                        return false;
                    if (match.Groups[2].Value == "speech")
                    {
                        if (entry.politicalVersion == PoliticalCaseRules.Version)
                        {
                            string expected = entry.speech == 2 ? "silent" : "speech-" + entry.speech + "-" + entry.speechApproachId;
                            if (entry.speech < 0 || entry.speech > 2 || record.choiceId != expected) return false;
                            continue;
                        }
                        if (entry.speech < 0 || entry.speech > 2 || record.choiceId != new[]
                        {
                            "public-accountability",
                            "restricted-authority",
                            "silent"
                        }[entry.speech])
                            return false;
                    }
                    else if (entry.vote < 0 || entry.vote > 2 || (record.choiceId != "vote-" + entry.vote && record.choiceId != "reflection-" + entry.vote))
                        return false;
                    continue;
                }

                if (System.Text.RegularExpressions.Regex.IsMatch(record.stepId, @"^city-logographer-\d+$"))
                    continue;
                if (System.Text.RegularExpressions.Regex.IsMatch(record.stepId, @"^city-complaint-[0-2]-\d+$") && (record.choiceId == "referred-to-boule" || System.Text.RegularExpressions.Regex.IsMatch(record.choiceId, @"^recommendation-[0-2]$")))
                    continue;
                if (System.Text.RegularExpressions.Regex.IsMatch(record.stepId, @"^city-official-[0-5]-\d+$") && (record.choiceId == "public-argument" || record.choiceId == "private-offer"))
                    continue;
                return false;
            }

            return true;
        }

        public static List<int> AvailableTribes(GameState state, int tribeCount)
        {
            var result = new List<int>();
            if (state == null || state.drawnTribes == null || tribeCount < 1 || tribeCount > 10)
                return result;
            for (int i = 0; i < tribeCount; i++)
                if (!state.drawnTribes.Contains(i))
                    result.Add(i);
            return result;
        }

        // availableIndex is selected by the caller's random source, making the rule deterministic in tests.
        public static bool DrawTribe(GameState state, int tribeCount, int availableIndex)
        {
            if (state == null || state.currentTribe >= 0)
                return false;
            List<int> available = AvailableTribes(state, tribeCount);
            if (availableIndex < 0 || availableIndex >= available.Count)
                return false;
            state.currentTribe = available[availableIndex];
            state.drawnTribes.Add(state.currentTribe);
            return true;
        }
    }
}
