using System;

namespace DemocracyWay.NewUI
{
    /// <summary>Deterministic calculations. Authored deltas and thresholds come from entity JSON.</summary>
    public static class DecisionRules
    {
        public sealed class Outcome
        {
            public int influence, speakerInfluence, writerInfluence, majority, score;
            public int cityDelta, personalDelta, warDelta, readinessDelta, legitimacyDelta, foodDelta, riskDelta;
            public bool supported;
        }
        public static Outcome Speech(GameState state, int side, int bonus)
        {
            var m = state.metrics;
            int influence = m.persuasion / 10 + m.authority / 25 + m.consistency / 25 + m.ethics / 25 + m.parrhesia / 25 + m.recognition / 25 + Math.Max(0, bonus);
            return new Outcome { influence = side == 2 ? 0 : PoliticalCaseRules.Clamp(influence, 0, 22) };
        }
        public static Outcome Vote(GameState state, int side, int speech, int support, int influence, int costA, int costB, int war, bool observation)
        {
            int score = 48 + (CampaignRules.Turn(state) % 5 - 2) * 5;
            score += speech == 0 ? influence : speech == 1 ? -influence : 0;
            score += side == 0 ? 6 + support * 2 : side == 1 ? -6 - support * 2 : 0;
            bool first = score >= 50;
            return new Outcome { score = score, majority = first ? 0 : 1,
                cityDelta = observation ? 0 : Math.Max(-state.cityFunds, first ? costA : costB),
                warDelta = observation ? 0 : first ? war : -war,
                readinessDelta = observation ? 0 : first ? 2 : 1,
                legitimacyDelta = observation ? 0 : first ? 1 : -1 };
        }
        public static Outcome PoliticalSpeech(GameState state, int side, CharacterMetrics w, int total, int bonus, int maximum, int argumentBonus)
        {
            var m = state.metrics;
            int weighted = m.consistency * w.consistency + m.ethics * w.ethics + m.authority * w.authority + m.persuasion * w.persuasion + m.parrhesia * w.parrhesia + m.recognition * w.recognition;
            int speaker = side == 2 ? 0 : Math.Min(maximum, weighted / (Math.Max(1, total) * 10) + argumentBonus);
            int writer = side == 2 ? 0 : bonus;
            return new Outcome { speakerInfluence = speaker, writerInfluence = writer, influence = speaker + writer };
        }
        public static Outcome PoliticalVote(GameState state, InstitutionCase c, PoliticalCaseDefinition p, int speech, int supportSide, int support, int influence, int context)
        {
            int score = p.baseSupportA + context + (speech == 0 ? influence : speech == 1 ? -influence : 0) + (supportSide == 0 ? support : supportSide == 1 ? -support : 0);
            score = PoliticalCaseRules.Clamp(score, 0, 100);
            bool first = score >= p.requiredSupport;
            return new Outcome { score = score, majority = first ? 0 : 1,
                cityDelta = Math.Max(-state.cityFunds, first ? c.costA : c.costB),
                foodDelta = first ? p.foodDeltaA : p.foodDeltaB,
                legitimacyDelta = first ? p.trustDeltaA : p.trustDeltaB,
                readinessDelta = first ? p.readinessDeltaA : p.readinessDeltaB,
                warDelta = first ? c.war : -c.war };
        }
        public static Outcome Official(GameState state, bool bribe, int integrity, int threshold) => new Outcome
        {
            supported = bribe ? integrity < 55 : state.metrics.persuasion + state.metrics.authority >= threshold,
            personalDelta = bribe ? -JsonContent.Settings.bribePrice : 0,
            riskDelta = bribe ? integrity < 55 ? 1 : 2 : 0
        };
        public static CharacterMetrics Effects(GameState state, int kind, int side, CharacterMetrics profile = null, int speech = 2, bool observation = false, bool success = false)
        {
            var m = new CharacterMetrics();
            if (kind == 0 && side != 2) { m.persuasion = 1; m.parrhesia = 1; m.recognition = 1; }
            if (kind == 1)
            {
                m = profile?.Copy() ?? new CharacterMetrics();
                if (!observation && side != 2 && side == speech) m.consistency++;
                if (!observation && side != 2 && speech != 2 && side != speech) m.consistency -= 2;
                if (!observation && side == 2) { m.consistency = -1; m.parrhesia = -1; m.recognition = -1; }
            }
            if (kind == 2) { m.ethics = -6; m.consistency = -3; m.parrhesia = -1; m.authority = 1; m.recognition = -1; }
            if (kind == 3) { m.persuasion = 1; m.consistency = success ? 1 : 0; m.authority = success ? 1 : -1; }
            if (kind == 4)
            {
                if (side == 0) { m.consistency = 1; m.ethics = 2; m.authority = 1; m.persuasion = -1; }
                if (side == 1) { m.consistency = 1; m.ethics = 1; m.persuasion = 1; m.authority = -1; }
                if (side == 2) { m.ethics = -1; m.recognition = -1; m.parrhesia = -1; }
            }
            return m;
        }
    }
    public static class StoryRoutes
    {
        public static string Next(string route, GameState state, string caseId = "", int side = -1)
        {
            switch (route)
            {
                case "KnowledgeContinue": return "procedure-briefing";
                case "BriefingContinue": return caseId == "appeal" ? "appeal" : "deliberation";
                case "AppealContinue": return "deliberation";
                case "DeliberationSpeech": return "speech";
                case "DeliberationVote": case "SpeechContinue": return "vote";
                case "VoteContinue": return "week-report";
                case "WeekContinue": case "LotteryContinue": return "map-stela";
                case "CityEndingContinue": return "ending-personal";
                case "PersonalEndingContinue": return "final-knowledge";
                case "BiographyComplete": return "prytany-title";
                case "FinalSurveyComplete": return "complete";
                case "PrytanyTitleContinue": return "lottery";
                case "VoteResult": return side == 2 ? "vote-abstention" : "vote-result";
                default: throw new ArgumentException("Unknown story transition: " + route);
            }
        }
    }
}
