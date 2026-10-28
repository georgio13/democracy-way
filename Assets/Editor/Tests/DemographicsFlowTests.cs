using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DemocracyWay.NewUI.Tests
{
    /// <summary>Τα δημογραφικά ζητούνται στο τέλος της καμπάνιας, όταν το state έχει ήδη περάσει από save/load.</summary>
    public sealed class DemographicsFlowTests
    {
        static GameState EndOfCampaign()
        {
            var state = new GameState { prytany = 10, week = 2 };
            for (int turn = 0; turn < CampaignRules.TotalCases; turn++)
                state.campaign.cases.Add(new CaseRecord { turn = turn, caseId = CampaignRules.CaseIdForTurn(turn), completed = true });
            return state;
        }

        static GameState RoundTrip(GameState state) => JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state));

        [Test] // Πριν τη διόρθωση: το «Συνέχεια» δεν έκανε τίποτα γιατί το point έμενε κενό μετά το load.
        public void AfterSaveLoad_ContinueCompletesDemographics()
        {
            var content = JsonContent.Evaluation.demographics;
            var state = EndOfCampaign();
            DemographicsRules.Ensure(state, content);
            state = RoundTrip(state);
            var point = EvaluationRules.Point(state, CampaignRules.CaseIdForTurn(CampaignRules.Turn(state)), "test");
            Assert.IsTrue(EvaluationRules.HasPoint(point), "το σημείο του τέλους της καμπάνιας πρέπει να είναι έγκυρο");
            foreach (var q in content.questions.Where(q => q.required && q.kind == "choice"))
                Assert.IsTrue(DemographicsRules.Answer(state, content, q.id, q.options[0].id, null, point), q.id);
            Assert.IsTrue(DemographicsRules.Ready(state, content));
            Assert.IsTrue(DemographicsRules.Complete(state, content));
            Assert.IsTrue(EvaluationRules.HasPoint(state.evaluation.demographics.point));
        }

        [Test] // Save που ήδη κόλλησε: απαντήσεις υπάρχουν, το point είναι κενό — το Complete πρέπει να περνά.
        public void StuckSave_WithEmptyPoint_StillCompletes()
        {
            var content = JsonContent.Evaluation.demographics;
            var state = EndOfCampaign();
            var d = DemographicsRules.Ensure(state, content);
            d.point = new EvaluationPoint();   // όπως στο πραγματικό save: άδειο αντικείμενο
            d.answers = content.questions.Where(q => q.required && q.kind == "choice")
                .Select(q => new DemographicAnswer { questionId = q.id, optionId = q.options[0].id, optionIndex = 0, value = q.options[0].label }).ToArray();
            state = RoundTrip(state);
            Assert.IsTrue(DemographicsRules.Complete(state, content));
        }
    }
}
