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
            record.metrics = choice.metrics != null ? choice.metrics.Copy() : new BiographyDeltas();
            record.institutionalLean = choice.institutionalLean;
            record.economicClassIndex = choice.economicClassIndex;
            record.tribeIndex = choice.tribeIndex;
            record.profession = choice.profession;
            record.playerName = choice.playerName;
            record.demeId = choice.demeId;
            record.trittysId = choice.trittysId;
            record.deme = choice.deme;
            record.trittys = choice.trittys;
            Recalculate(state);
        }

        /// <summary>Αθροίζει τα δέλτα της βιογραφίας και την ταυτότητα. Η πόλη δεν αγγίζεται (METRICS_UNIFY).</summary>
        public static void Recalculate(GameState state)
        {
            if (state == null || state.choices == null)
                throw new ArgumentException("Incomplete game state.");
            state.biographyDeltas = new BiographyDeltas();
            foreach (ChoiceRecord record in state.choices)
            {
                state.biographyDeltas.Add(record.metrics);
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

            if (state.campaign == null) state.campaign = new CampaignState();
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

                // Στο ενιαίο σύστημα τα choices προέρχονται μόνο από τη βιογραφία.
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
