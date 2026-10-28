using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Πρόσβαση στο αφηγηματικό/ερευνητικό περιεχόμενο. Η πηγή είναι πλέον το ContentLibrary asset
    /// (όχι JSON σε Resources)· το όνομα της κλάσης κρατήθηκε για να μην αλλάξουν οι 90 καταναλωτές.
    /// Δένεται από το GameFlowController.Awake· στον editor (tests, validators, batch) φορτώνει
    /// αυτόματα το Assets/Content/ContentLibrary.asset.
    /// </summary>
    public static class JsonContent
    {
        static ContentLibrary library;
        static Dictionary<string, string> interfaceText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() { library = null; interfaceText = null; }

        public static void Bind(GameConfiguration config)
        {
            if (config == null || config.library == null || ReferenceEquals(library, config.library)) return;
            library = config.library;
            interfaceText = null;
        }

        static ContentLibrary Library
        {
            get
            {
                if (library != null) return library;
#if UNITY_EDITOR
                library = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentLibrary>("Assets/Content/ContentLibrary.asset");
                if (library != null) return library;
#endif
                throw new InvalidOperationException("Το ContentLibrary δεν έχει συνδεθεί: λείπει το Assets/Content/ContentLibrary.asset ή το Bind του GameConfiguration.");
            }
        }

        public static string Copy(string id)
        {
            if (interfaceText == null) interfaceText = Library.interfaceText.ToDictionary(x => x.id, x => x.text);
            if (!interfaceText.TryGetValue(id, out var text)) throw new InvalidOperationException("Missing interface text: " + id);
            return text;
        }

        public static GameSettings Settings => Library.settings;
        public static TribeData[] Tribes => Library.tribes;

        public static GameConfiguration Configuration(GameConfiguration media)
        {
            var config = UnityEngine.Object.Instantiate(media);
            var content = media.library != null ? media.library : Library;
            var settings = content.settings;
            config.contentVersion = settings.contentVersion;
            config.playerAge = settings.playerAge;
            config.defaultProfession = settings.defaultProfession;
            config.prologue = content.prologuePages.ToList();
            config.prytanyQuotes = content.prytanyQuotes.ToList();
            var tribes = content.tribes;
            Sprite FindSprite(string id) => string.IsNullOrEmpty(id) ? null : media.contentSprites.Find(x => x.id == id)?.sprite;
            config.tribes = tribes.Select(t => new TribeDefinition { id = t.id, name = t.name, emblem = FindSprite(t.emblemId), deme = t.defaultDeme, trittys = t.defaultTrittys }).ToList();
            config.economicClasses = content.economicClassNames.ToList();
            // Τα raw βήματα της βιβλιοθήκης δεν πειράζονται: κάθε Configuration παίρνει δικά του αντίγραφα.
            config.biography = content.biographySteps.Select(s => JsonUtility.FromJson<BiographyStep>(JsonUtility.ToJson(s))).ToList();
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
                    case "class": case "economic-class": step.choices = content.economicClassChoices.ToList(); break;
                    case "name": step.choices = content.playerNameChoices.ToList(); break;
                    case "profession": step.choices = content.professionChoices.ToList(); break;
                }
                if (!string.IsNullOrEmpty(step.backgroundId) && step.background == null) throw new InvalidOperationException("Unknown biography backgroundId: " + step.backgroundId);
            }
            if (!config.Validate(out var error)) throw new InvalidOperationException(error);
            return config;
        }

        public static EvaluationContent Evaluation
        {
            get
            {
                // Βαθύ αντίγραφο: το asset της βιβλιοθήκης δεν πρέπει να μεταλλάσσεται σε runtime.
                var result = JsonUtility.FromJson<EvaluationContent>(JsonUtility.ToJson(Library.protocol));
                result.demographics = Library.demographics;
                var knowledge = Library.knowledge;
                var meega = Library.meega;
                var tendencies = Library.tendencies;
                result.onboarding = knowledge.pre; result.postTest = knowledge.post; result.knowledgeInstructions = knowledge.instructions;
                result.experience = meega.experience; result.participationIntention = Array.Empty<EvaluationRating>();
                result.tendencies = tendencies.items; result.tendencyInstructions = tendencies.instructions; result.tendencyRatingLabels = tendencies.ratingLabels;
                var participation = Library.participation;
                result.participation = participation.items; result.participationInstructions = participation.instructions; result.participationRatingLabels = participation.ratingLabels;
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
        public int contentVersion, casesPerPrytany = 2, prytanyCount = 10, playerAge;
        public string defaultProfession;
    }
    [Serializable] public sealed class TribeData { public string id, name, choiceText, emblemId, defaultDeme, defaultTrittys; public TrittysData[] trittyes; }
    [Serializable] public sealed class TrittysData { public string id, name, choiceText; public DemeData[] demes; }
    [Serializable] public sealed class DemeData { public string id, name, choiceText; }
    [Serializable] public sealed class KnowledgeFile { public string instructions; public EvaluationQuestion[] pre, post; }
    [Serializable] public sealed class TendenciesFile { public string instructions; public string[] ratingLabels; public TendencyItem[] items; }
    [Serializable] public sealed class InterfaceTextFile { public InterfaceTextEntry[] entries; }
    [Serializable] public sealed class InterfaceTextEntry { public string id, text; }
}
