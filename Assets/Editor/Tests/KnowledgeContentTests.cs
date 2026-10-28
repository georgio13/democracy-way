using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace DemocracyWay.NewUI.Tests
{
    /// <summary>Agora_FINAL_DESIGN ενότητα 6 (τεστ γνώσεων) και Agora_PROLOGUE_QUOTES_NAMES (ονόματα, quotes).</summary>
    public sealed class KnowledgeContentTests
    {
        static ContentLibrary Library => AssetDatabase.LoadAssetAtPath<ContentLibrary>("Assets/Content/ContentLibrary.asset");

        [Test] // 14 έννοιες: μορφή Α (πριν) k01a…k14a, μορφή Β (μετά) k01b…k14b, ids μοναδικά.
        public void EveryConceptHasExactlyOneFormAAndOneFormB()
        {
            var e = JsonContent.Evaluation;
            CollectionAssert.AreEqual(Enumerable.Range(1, 14).Select(n => "k" + n.ToString("00") + "a").ToArray(), e.onboarding.Select(q => q.id).ToArray());
            CollectionAssert.AreEqual(Enumerable.Range(1, 14).Select(n => "k" + n.ToString("00") + "b").ToArray(), e.postTest.Select(q => q.id).ToArray());
            var all = e.onboarding.Concat(e.postTest).ToArray();
            Assert.AreEqual(all.Length, all.Select(q => q.id).Distinct().Count(), "μοναδικά ids");
        }

        [Test] // Κάθε ερώτηση: 4 επιλογές με μοναδικά ids, η σωστή ανάμεσά τους· καμία ερώτηση για τον οστρακισμό.
        public void QuestionsAreWellFormed()
        {
            var e = JsonContent.Evaluation;
            foreach (var q in e.onboarding.Concat(e.postTest))
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(q.prompt), q.id);
                Assert.AreEqual(4, q.answers.Length, q.id);
                Assert.AreEqual(4, q.answers.Select(a => a.id).Distinct().Count(), q.id);
                Assert.IsTrue(q.answers.Any(a => a.id == q.correctId), q.id + ": η σωστή απάντηση υπάρχει");
                Assert.IsFalse(q.prompt.Contains("οστρακ"), q.id + ": ο οστρακισμός δεν διδάσκεται στο παιχνίδι");
            }
        }

        [Test] // Κ9 νέα (ικετηρία)· Κ11: οι δύο ερωτήσεις άλλαξαν θέση.
        public void K9IsHiketeriaAndK11Swapped()
        {
            var e = JsonContent.Evaluation;
            StringAssert.Contains("ικετηρία", e.onboarding[8].prompt);
            StringAssert.Contains("μέτοικος", e.postTest[8].prompt);
            StringAssert.Contains("τριήρων", e.onboarding[10].prompt);
            StringAssert.Contains("μεταλλεία", e.postTest[10].prompt);
        }

        [Test] // Το κουμπί του ονόματος γράφει ό,τι αποθηκεύεται.
        public void NameButtonEqualsStoredName()
        {
            var names = Library.playerNameChoices;
            CollectionAssert.AreEqual(new[] { "Νικόστρατος", "Θεόδωρος", "Χαιρέστρατος", "Αριστοκλής" }, names.Select(n => n.playerName).ToArray());
            foreach (var n in names)
                Assert.AreEqual(n.playerName, n.text, n.id);
        }

        [Test] // Τα quotes 5, 7, 9 δεν δίνουν πια απαντήσεις του τεστ.
        public void PrytanyQuotesFollowTheDocument()
        {
            var quotes = Library.prytanyQuotes;
            Assert.AreEqual(10, quotes.Length);
            StringAssert.StartsWith("Θουκυδίδης", quotes[4].attribution);
            StringAssert.StartsWith("Ευριπίδης", quotes[6].attribution);
            Assert.AreEqual("«Ποιος θέλει να μιλήσει;»", quotes[8].text);
            Assert.AreEqual("«Τη στιγμή που ένας λαός δίνει σε άλλους να τον εκπροσωπούν, δεν είναι πια ελεύθερος.»", quotes[9].text);
        }

        [Test] // Η σειρά των επιλογών που είδε ο παίκτης αποθηκεύεται ανά ερώτηση.
        public void ShownOptionOrderIsRecorded()
        {
            var content = JsonContent.Evaluation;
            var state = new GameState();
            var point = EvaluationRules.Point(state, CampaignRules.CaseIdForTurn(0), "test");
            Assert.IsTrue(EvaluationRules.AnswerBaseline(state, content, 2, point, "c,a,d,b"));
            Assert.AreEqual("c,a,d,b", state.evaluation.baselineOptionOrders[0]);
            Assert.AreEqual(content.onboarding.Length, state.evaluation.baselineOptionOrders.Length);
            Assert.AreEqual("", state.evaluation.baselineOptionOrders[1]);
        }
    }
}
