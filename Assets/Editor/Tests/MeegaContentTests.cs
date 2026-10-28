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
            Assert.AreEqual(34, e.experience.Length);
            Assert.IsTrue(e.experience.All(x => !string.IsNullOrWhiteSpace(x.id) && !string.IsNullOrWhiteSpace(x.prompt) && !string.IsNullOrWhiteSpace(x.section)));
            Assert.AreEqual(e.experience.Length, e.experience.Select(x => x.id).Distinct().Count(), "μοναδικά id για την αποθήκευση");
            Assert.AreEqual(5, e.ratingLabels.Length);
            Assert.IsFalse(string.IsNullOrWhiteSpace(e.questionnaireInstructions));
            Assert.IsFalse(string.IsNullOrWhiteSpace(e.openFeedbackPrompt));
            Assert.AreEqual(EvaluationRules.CurrentInstrumentVersion, e.instrumentVersion);
            Assert.AreEqual(1000, e.openFeedbackMaxLength);
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
