using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    /// <summary>Loads authored entities directly from Resources/Content in Editor and Web.</summary>
    public static class JsonContent
    {
        static readonly Dictionary<string, object> cache = new Dictionary<string, object>();
        static Dictionary<string, string> interfaceText;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() { cache.Clear(); interfaceText = null; }

        public static T Load<T>(string path) where T : class
        {
            if (cache.TryGetValue(path, out var found)) return (T)found;
            var asset = Resources.Load<TextAsset>("Content/" + path);
            if (asset == null) throw new InvalidOperationException("Missing content JSON: Assets/Resources/Content/" + path + ".json");
            var value = JsonUtility.FromJson<T>(asset.text);
            if (value == null) throw new InvalidOperationException("Invalid content JSON: " + path);
            cache[path] = value;
            return value;
        }

        public static string Copy(string id)
        {
            if (interfaceText == null) interfaceText = Load<InterfaceTextFile>("InterfaceText").entries.ToDictionary(x => x.id, x => x.text);
            if (!interfaceText.TryGetValue(id, out var text)) throw new InvalidOperationException("Missing interface text: " + id);
            return text;
        }
        public static GameSettings Settings => Load<GameSettings>("GameSettings");
        public static CampaignPlan Plan => Load<CampaignPlan>("Campaign");

        public static GameConfiguration Configuration(GameConfiguration media)
        {
            var config = UnityEngine.Object.Instantiate(media);
            var settings = Settings;
            config.contentVersion = settings.contentVersion;
            config.playerAge = settings.playerAge;
            config.startingPersonalFunds = settings.startingPersonalFunds;
            config.startingCityFunds = settings.startingCityFunds;
            config.defaultProfession = settings.defaultProfession;
            config.startingMetrics = settings.startingMetrics.Copy();
            config.prologue = Load<PrologueFile>("Prologue").pages.ToList();
            config.prytanyQuotes = Load<QuoteFile>("PrytanyQuotes").quotes.ToList();
            var tribes = Load<TribesFile>("Tribes").tribes;
            Sprite FindSprite(string id) => string.IsNullOrEmpty(id) ? null : media.contentSprites.Find(x => x.id == id)?.sprite;
            config.tribes = tribes.Select(t => new TribeDefinition { id = t.id, name = t.name, emblem = FindSprite(t.emblemId), deme = t.defaultDeme, trittys = t.defaultTrittys }).ToList();
            var classes = Load<EconomicClassesFile>("EconomicClasses");
            config.economicClasses = classes.names.ToList();
            config.biography = Load<BiographyFile>("Biography").steps.ToList();
            foreach (var step in config.biography)
            {
                step.background = FindSprite(step.backgroundId);
                switch (step.selectionKind)
                {
                    case "tribe":
                        step.choices = tribes.Select((t, i) => new NarrativeChoice { id = t.id, text = t.choiceText, tribeIndex = i }).ToList(); break;
                    case "trittys":
                        step.choices = tribes.SelectMany((t, i) => t.trittyes.Select(x => new NarrativeChoice { id = x.id, text = x.choiceText, trittysId = x.id, trittys = x.name, requireTribe = i })).ToList(); break;
                    case "deme":
                        step.choices = tribes.SelectMany((t, i) => t.trittyes.SelectMany(tr => tr.demes.Select(d => new NarrativeChoice { id = d.id, text = d.choiceText, demeId = d.id, deme = d.name, requireTribe = i, requireTrittys = tr.id }))).ToList(); break;
                    case "class": case "economic-class": step.choices = classes.choices.ToList(); break;
                    case "name": step.choices = Load<ChoiceFile>("PlayerNames").choices.ToList(); break;
                    case "profession": step.choices = Load<ChoiceFile>("Professions").choices.ToList(); break;
                }
                if (!string.IsNullOrEmpty(step.backgroundId) && step.background == null) throw new InvalidOperationException("Unknown biography backgroundId: " + step.backgroundId);
            }
            if (!config.Validate(out var error)) throw new InvalidOperationException(error);
            return config;
        }

        public static CampaignContent Campaign
        {
            get
            {
                var cases = Plan.availableCaseIds.Select(id => Load<InstitutionCase>("Cases/" + id)).ToArray();
                foreach (var c in cases) HydrateCase(c);
                return new CampaignContent { cases = cases, contentVersion = Plan.contentVersion, notice = Plan.notice };
            }
        }
        public static string DisplayArgument(CaseArgument argument) => !string.IsNullOrWhiteSpace(argument?.displayText) ? argument.displayText : argument?.fragments?.FirstOrDefault() ?? "";
        public static CaseArgument[] DisplayArguments(InstitutionCase c) => (c.arguments ?? Array.Empty<CaseArgument>()).GroupBy(a=>a.side).OrderBy(g=>g.Key).Select(g=>g.First()).ToArray();
        public static void HydrateCase(InstitutionCase c)
        {
            if(c==null)return;
            if(c.politics!=null&&c.politics.version<1)c.politics=null;
            c.contentVersion=Plan.contentVersion;
            bool single=c.presentationMode=="paired-static";
            var arguments=single?DisplayArguments(c):c.arguments;
            string Text(CaseArgument a)=>single?DisplayArgument(a):string.Join(" ",a.fragments);
            c.argumentA=string.Join("\n\n",arguments.Where(a=>a.side==0).Select(Text));
            c.argumentB=string.Join("\n\n",arguments.Where(a=>a.side==1).Select(Text));
            c.speakerA=arguments.First(a=>a.side==0).speaker;c.speakerB=arguments.First(a=>a.side==1).speaker;
            if(c.politics!=null)c.politics.dialogue=arguments.Select(a=>new PoliticalDialogueLine{id=a.id,side=a.side,speaker=a.speaker,speakerRole=a.speakerRole,portraitIndex=a.portraitIndex,stageText=single?"":a.stageText,text=Text(a)}).ToArray();
        }

        public static CityContent City => new CityContent { news = Load<CityContent>("MarketNews").news, writers = Load<CityContent>("Logographers").writers, complaints = Load<CityContent>("Complaints").complaints, officials = Load<CityContent>("Officials").officials };
        public static EvaluationContent Evaluation
        {
            get
            {
                var result = Load<EvaluationContent>("Assessments/Protocol");
                result.demographics = Load<DemographicsContent>("Assessments/Demographics");
                var knowledge = Load<KnowledgeFile>("Assessments/Knowledge");
                var meega = Load<EvaluationContent>("Assessments/MeegaPlus");
                var tendencies = Load<TendenciesFile>("Assessments/Tendencies");
                result.knowledgeVersion = knowledge.version; result.onboarding = knowledge.baseline; result.postTest = knowledge.final;
                result.showBaselineFeedback = knowledge.showBaselineFeedback;
                result.experience = meega.experience; result.participationIntention = Array.Empty<EvaluationRating>();
                result.tendencies = tendencies.items; result.tendencyVersion = tendencies.version;
                result.tendencyStatus = tendencies.status; result.tendencyInstructions = tendencies.instructions; result.tendencyRatingLabels = tendencies.ratingLabels;
                result.ratingLabels = meega.ratingLabels; result.questionnaireStatus = meega.questionnaireStatus;
                result.questionnaireInstructions = meega.questionnaireInstructions; result.notApplicableLabel = meega.notApplicableLabel;
                result.openFeedbackPrompt = meega.openFeedbackPrompt; result.openFeedbackOptional = meega.openFeedbackOptional; result.openFeedbackMaxLength = meega.openFeedbackMaxLength;
                return result;
            }
        }
    }

    [Serializable] public sealed class NamedSprite { public string id; public Sprite sprite; }
    [Serializable] public sealed class GameSettings
    {
        public int schemaVersion, contentVersion, casesPerPrytany = 2, prytanyCount = 10, playerAge, startingPersonalFunds, startingCityFunds, bribePrice = 30, speechUses = 2;
        public string defaultProfession;
        public CharacterMetrics startingMetrics;
        public EndingSettings endings;
    }
    [Serializable] public sealed class EndingSettings { public int warThreshold = 4, victoryReadiness = 20, victoryCityFunds = 6500, subjectionLegitimacy = -5, exilePersonalWar = 3; }
    [Serializable] public sealed class CampaignPlan { public string contentVersion, notice; public string[] availableCaseIds; public PrytanyPlan[] prytanies; }
    [Serializable] public sealed class PrytanyPlan { public string id; public string[] caseIds; }
    [Serializable] public sealed class PrologueFile { public ProloguePage[] pages; }
    [Serializable] public sealed class BiographyFile { public BiographyStep[] steps; }
    [Serializable] public sealed class QuoteFile { public DemocracyQuote[] quotes; }
    [Serializable] public class ChoiceFile { public NarrativeChoice[] choices; }
    [Serializable] public sealed class EconomicClassesFile : ChoiceFile { public string[] names; }
    [Serializable] public sealed class TribesFile { public TribeData[] tribes; }
    [Serializable] public sealed class TribeData { public string id, name, choiceText, emblemId, defaultDeme, defaultTrittys; public TrittysData[] trittyes; }
    [Serializable] public sealed class TrittysData { public string id, name, choiceText; public DemeData[] demes; }
    [Serializable] public sealed class DemeData { public string id, name, choiceText; }
    [Serializable] public sealed class KnowledgeFile { public string version; public bool showBaselineFeedback; public EvaluationQuestion[] baseline, final; }
    [Serializable] public sealed class TendenciesFile { public string version, status, instructions; public string[] ratingLabels; public EvaluationRating[] items; }
    [Serializable] public sealed class InterfaceTextFile { public InterfaceTextEntry[] entries; }
    [Serializable] public sealed class InterfaceTextEntry { public string id, text; }
    [Serializable] public sealed class CaseArgument { public string id, speaker, speakerRole, stageText, displayText; public int side, portraitIndex; public string[] fragments; }
}
