using System;

namespace DemocracyWay.NewUI
{
    // Political support is calculated from authored case data and the current city/character state.
    public static class PoliticalCaseRules
    {
        public const string Version = "political-core-1";
        public static PoliticalCaseDefinition Definition(InstitutionCase c) => c?.politics;
        // Keep an already spoken/completed legacy case on its original rule set.
        // Changing the current export must not combine old influence with new vote rules.
        public static PoliticalCaseDefinition Definition(InstitutionCase c, GameState state)
        {
            var definition = Definition(c);
            if (definition == null || state == null) return definition;
            var record = CampaignRules.Current(state);
            return (record.speech >= 0 || record.completed) && record.politicalVersion != Version ? null : definition;
        }
        public static int WriterInfluence(PoliticalCaseDefinition definition, int bonus) => Math.Min(definition.maxWriterInfluence, Math.Max(0, (bonus + 2) / 3));
        public static int Clamp(int n, int min, int max) => Math.Max(min, Math.Min(max, n));

        public static bool Speak(GameState state, InstitutionCase c, int side, int approachIndex, int writerBonus)
        {
            var p = Definition(c, state);
            var record = CampaignRules.Current(state);
            if (p == null || record.completed || record.speech >= 0 || side < 0 || side > 2 || (side < 2 && (approachIndex < 0 || approachIndex >= p.approaches.Length))) return false;
            var approach = side < 2 ? p.approaches[approachIndex] : null;
            var w = approach?.weights ?? new CharacterMetrics();
            int weightTotal = w.consistency + w.ethics + w.authority + w.persuasion + w.parrhesia + w.recognition;
            var result = DecisionRules.PoliticalSpeech(state, side, w, weightTotal, WriterInfluence(p, writerBonus), p.maxSpeakerInfluence, approach?.argumentBonus ?? 0);
            record.politicalVersion = Version;
            record.speech = side;
            record.pendingSpeechSide = -1;
            record.speechApproach = approachIndex;
            record.speechApproachId = approach?.id;
            record.speechMetricsBefore = state.metrics.Copy();
            record.speakerComponent = result.speakerInfluence;
            record.writerComponent = result.writerInfluence;
            record.influence = result.influence;
            var effects = approach?.metrics?.Copy() ?? new CharacterMetrics();
            CampaignRules.Record(state, "case-" + record.turn + "-speech", side == 2 ? "silent" : "speech-" + side + "-" + approach.id,
                effects, lean: side == 0 ? c.leanA : side == 1 ? c.leanB : "mixed");
            return true;
        }

        public static bool Vote(GameState state, InstitutionCase c, int side)
        {
            var p = Definition(c, state);
            var record = CampaignRules.Current(state);
            if (p == null || record.completed || side < 0 || side > 2) return false;
            int food = state.campaign.foodSecurity;
            int foodContext = Clamp((p.foodBaseline - food) / Math.Max(1, p.foodContextDivisor), -3, 3);
            int trustContext = Clamp(state.campaign.legitimacy / Math.Max(1, p.trustContextDivisor), -3, 3);
            int fundsContext = state.cityFunds < p.lowFundsThreshold ? p.lowFundsPenalty : 0;
            int support = Math.Min(p.maxOfficialInfluence, CampaignRules.OfficialSupport(state, c.site));
            int speech = record.speech < 0 ? 2 : record.speech;
            int supportSide = speech < 2 ? speech : side;
            var result = DecisionRules.PoliticalVote(state, c, p, speech, supportSide, support, record.influence, foodContext + trustContext + fundsContext);
            record.politicalVersion = Version;
            record.speech = speech;
            record.vote = side;
            record.majority = result.majority;
            record.baseComponent = p.baseSupportA;
            record.foodComponent = foodContext;
            record.trustComponent = trustContext;
            record.fundsComponent = fundsContext;
            record.officialComponent = supportSide == 2 ? 0 : support;
            record.supportSide = supportSide;
            record.supportTotal = result.score;
            record.supportThreshold = p.requiredSupport;
            record.foodBefore = food;
            record.trustBefore = state.campaign.legitimacy;
            int requestedCost = record.majority == 0 ? c.costA : c.costB;
            int actualPolicyCost = result.cityDelta;
            record.cityDelta = actualPolicyCost;
            // All three ballot choices occur after attendance; abstention is not non-attendance.
            record.personalDelta = Math.Min(Math.Max(0, c.attendanceObols), Math.Max(0, state.cityFunds + actualPolicyCost));
            record.cityDelta -= record.personalDelta;
            int foodChange = result.foodDelta;
            record.policyFullyFunded = requestedCost >= 0 || actualPolicyCost <= requestedCost;
            if (!record.policyFullyFunded && requestedCost < 0)
                foodChange = foodChange * Math.Max(0, -actualPolicyCost) / -requestedCost;
            state.campaign.foodSecurity = Clamp(food + foodChange, 0, 100);
            record.foodDelta = state.campaign.foodSecurity - food;
            record.caseId = c.id;
            record.argumentId = c.id + (side == 0 ? ":A" : side == 1 ? ":B" : ":abstain");
            record.institutionalLean = side == 0 ? c.leanA : side == 1 ? c.leanB : "mixed";
            record.leanReason = side == 0 ? c.leanReasonA : side == 1 ? c.leanReasonB : "Αποχή από την ψηφοφορία, χωρίς αυτόματη πολιτειακή ταξινόμηση.";
            record.sourceRef = c.source;
            record.contentVersion = c.contentVersion;
            state.campaign.war += result.warDelta;
            int readinessChange = result.readinessDelta;
            if (!record.policyFullyFunded && requestedCost < 0 && readinessChange > 0)
                readinessChange = readinessChange * Math.Max(0, -actualPolicyCost) / -requestedCost;
            state.campaign.readiness += readinessChange;
            int trustChange = record.policyFullyFunded ? result.legitimacyDelta : 0;
            state.campaign.legitimacy += trustChange;
            record.trustDelta = trustChange;
            if (side != 2) state.campaign.personalWar += side == 0 ? c.war : -c.war;
            var before = state.metrics.Copy();
            var effects = (side == 0 ? c.metricsA : side == 1 ? c.metricsB : null)?.Copy() ?? new CharacterMetrics();
            record.completed = true;
            CampaignRules.Record(state, "case-" + record.turn + "-vote", "vote-" + side, effects, record.cityDelta, record.personalDelta, record.institutionalLean);
            record.metricDelta = CampaignRules.Difference(before, state.metrics);
            return true;
        }
    }
}
