using System.Linq;
using NUnit.Framework;

namespace DemocracyWay.NewUI.Tests
{
    /// <summary>Η ενότητα MEEGA+ έχει μόνο τα πεδία που διαβάζει το ερωτηματολόγιο — και φτάνουν ακέραια στο παιχνίδι.</summary>
    public sealed class MeegaContentTests
    {
        [Test]
        public void MeegaSection_ReachesTheQuestionnaireIntact()
        {
            var e = JsonContent.Evaluation;
            Assert.AreEqual(35, e.experience.Length);
            Assert.IsTrue(e.experience.All(x => !string.IsNullOrWhiteSpace(x.id) && !string.IsNullOrWhiteSpace(x.prompt) && !string.IsNullOrWhiteSpace(x.section)));
            Assert.AreEqual(e.experience.Length, e.experience.Select(x => x.id).Distinct().Count(), "μοναδικά id για την αποθήκευση");
            Assert.AreEqual(5, e.ratingLabels.Length);
            Assert.IsFalse(string.IsNullOrWhiteSpace(e.questionnaireInstructions));
            Assert.IsFalse(string.IsNullOrWhiteSpace(e.openFeedbackPrompt));
            Assert.AreEqual(EvaluationRules.CurrentInstrumentVersion, e.instrumentVersion);
            Assert.AreEqual(1000, e.openFeedbackMaxLength);
        }

        [Test] // Τα δύο επιπλέον ελεύθερα πεδία μετά το MEEGA+ έχουν κείμενο ερώτησης (η διατύπωση είναι του περιεχομένου).
        public void WeaknessesAndCommentsPrompts_Exist()
        {
            var e = JsonContent.Evaluation;
            Assert.IsFalse(string.IsNullOrWhiteSpace(e.weaknessesFeedbackPrompt));
            Assert.IsFalse(string.IsNullOrWhiteSpace(e.commentsFeedbackPrompt));
        }

        [Test] // Κάθε ελεύθερο πεδίο αποθηκεύεται στο δικό του σημείο του save.
        public void FeedbackFields_AreStoredSeparately()
        {
            var content = JsonContent.Evaluation;
            var state = new GameState();
            var e = EvaluationRules.Ensure(state, content);
            var point = new EvaluationPoint { utc = "2026-10-29T10:00:00Z", caseId = "20", contentVersion = "5", prytany = 10, week = 2, turn = 19,
                completedCases = 20, casesPerPrytany = 2, totalCases = 20, progress = 1f };
            e.startPoint = point; e.postComplete = true; e.finalTendencyComplete = true;
            Assert.IsTrue(EvaluationRules.SetFeedback(state, content, " Οι δίκες "));
            Assert.IsTrue(EvaluationRules.SetFeedback(state, content, "Πολύ κείμενο", EvaluationRules.Feedback.Weaknesses));
            Assert.IsTrue(EvaluationRules.SetFeedback(state, content, "Περισσότερες δίκες", EvaluationRules.Feedback.Comments));
            Assert.AreEqual(("Οι δίκες", "Πολύ κείμενο", "Περισσότερες δίκες"), (e.openFeedback, e.weaknessesFeedback, e.commentsFeedback));
            Assert.IsFalse(EvaluationRules.SetFeedback(state, content, new string('α', 1001), EvaluationRules.Feedback.Weaknesses), "όριο 1000 χαρακτήρων");
        }

        [Test] // Χωρίς «Δεν μπορώ να κρίνω»: μόνο 1–5 είναι έγκυρη απάντηση.
        public void RatingsAcceptOnlyOneToFive()
        {
            var item = JsonContent.Evaluation.experience[0];
            Assert.IsFalse(EvaluationRules.ValidRating(item, 0));
            Assert.IsTrue(EvaluationRules.ValidRating(item, 1));
            Assert.IsTrue(EvaluationRules.ValidRating(item, 5));
            Assert.IsFalse(EvaluationRules.ValidRating(item, 6));
        }
    }
}
