using DemocracyWay.NewUI;
using NUnit.Framework;

namespace Agora.Tests
{
    /// <summary>
    /// Η λογιστική της εβδομάδας (Agora_FINAL_DESIGN ενότητες 1–2): τρεις ανεξάρτητες κινήσεις,
    /// η δίκη πριν τη συνέλευση, η ανάλυση αποθηκεύεται τη στιγμή που γίνεται.
    /// </summary>
    public sealed class WeekFlowTests
    {
        static PlayerState Player(int wallet = 500) { var p = TestWorld.Player(TestWorld.Cfg()); p.wallet = wallet; return p; }
        static CityState City() => TestWorld.City(50, 50, 50, 50, 50);

        [Test] // Μία εβδομάδα μπορείς να κάνεις και τις τρεις κινήσεις.
        public void ThreeIndependentMoves()
        {
            var c = TestWorld.Case(3); var s = new AgoraCaseState { caseId = 3 }; var p = Player(); var cfg = TestWorld.Cfg();
            p.peitho = 45;
            Assert.IsTrue(WeekFlowRules.Accuse(s, c, p), "κατηγορία");
            Assert.IsTrue(WeekFlowRules.Persuade(s, c, p, cfg, Side.A), "πειθώ στον αναποφάσιστο");
            Assert.IsTrue(WeekFlowRules.BuyLogographer(s, c, p, cfg, Side.None, forTrial: true), "λογογράφος για την κατηγορία");
            Assert.AreEqual(460, p.wallet);
        }

        [Test] // Μία κίνηση σε αξιωματούχο την εβδομάδα· η πειθώ χρειάζεται την Πειθώ της δυσκολίας.
        public void OneOfficialMovePerWeek()
        {
            var c = TestWorld.Case(2); var s = new AgoraCaseState { caseId = 2 }; var p = Player(); var cfg = TestWorld.Cfg();
            Assert.IsFalse(WeekFlowRules.Persuade(s, c, p, cfg, Side.A), "Πειθώ 30 < 45 (μέτριος)");
            p.peitho = 45;
            Assert.IsTrue(WeekFlowRules.Persuade(s, c, p, cfg, Side.B));
            Assert.IsFalse(WeekFlowRules.Persuade(s, c, p, cfg, Side.A), "η κίνηση της εβδομάδας έγινε");
            Assert.AreEqual((int)Side.B, s.officialFavors);
        }

        [Test] // Δωροδοκία μόνο σε όσους δέχονται χρήματα (ο Λυκούργος και ο Φωκίων ποτέ).
        public void BribeOnlyBribable()
        {
            var cfg = TestWorld.Cfg();
            var lykourgos = TestWorld.Case(6); var s = new AgoraCaseState { caseId = 6 };
            Assert.IsFalse(WeekFlowRules.Bribe(s, lykourgos, Player(), City(), cfg, new System.Random(1), Side.B));
            var pytheas = TestWorld.Case(5); var s5 = new AgoraCaseState { caseId = 5 }; var p = Player();
            Assert.IsTrue(WeekFlowRules.Bribe(s5, pytheas, p, City(), cfg, new System.Random(1), Side.A));
            Assert.IsTrue(s5.officialBribed && s5.officialMoveDone);
            Assert.AreEqual(1, p.bribes);
        }

        [Test] // Λόγος για την κατηγορία μόνο στην Ηλιαία και μόνο αν κατηγορείς· ένας λογογράφος την εβδομάδα.
        public void LogographerRules()
        {
            var cfg = TestWorld.Cfg();
            var areopagus = TestWorld.Case(10); var s10 = new AgoraCaseState { caseId = 10 }; var p = Player();
            WeekFlowRules.Accuse(s10, areopagus, p);
            Assert.IsFalse(WeekFlowRules.BuyLogographer(s10, areopagus, p, cfg, Side.None, forTrial: true), "όχι στον Άρειο Πάγο");
            var c = TestWorld.Case(3); var s = new AgoraCaseState { caseId = 3 };
            Assert.IsFalse(WeekFlowRules.BuyLogographer(s, c, p, cfg, Side.None, forTrial: true), "χωρίς κατηγορία");
            Assert.IsTrue(WeekFlowRules.BuyLogographer(s, c, p, cfg, Side.A, forTrial: false));
            Assert.IsFalse(WeekFlowRules.BuyLogographer(s, c, p, cfg, Side.B, forTrial: false), "ένας την εβδομάδα");
        }

        [Test] // Ο συκοφάντης δεν κατηγορεί ξανά.
        public void SycophantCannotAccuse()
        {
            var c = TestWorld.Case(9); var s = new AgoraCaseState { caseId = 9 }; var p = Player();
            p.sycophantFlag = true;
            Assert.IsFalse(WeekFlowRules.Accuse(s, c, p));
        }

        [Test] // Η δίκη γίνεται πάντα, και η συνέλευση περιμένει την ετυμηγορία.
        public void TrialAlwaysRunsBeforeTheAssembly()
        {
            var c = TestWorld.Case(9); var s = new AgoraCaseState { caseId = 9 }; var p = Player(); var cfg = TestWorld.Cfg(); var city = City();
            Assert.IsNull(WeekFlowRules.ResolveVote(s, c, city, p, cfg, new System.Random(1), Side.A), "η συνέλευση δεν γίνεται πριν τη δίκη");
            var trial = WeekFlowRules.RunTrial(s, c, p, cfg, new System.Random(1));
            Assert.IsNotNull(trial);
            Assert.IsTrue(s.trialDone);
            Assert.IsNull(WeekFlowRules.RunTrial(s, c, p, cfg, new System.Random(1)), "μία δίκη");
            Assert.AreEqual(AssemblyRules.TrialFavors(c.trial, s.trialGuilty), WeekFlowRules.TrialFavors(s, c));
            var vote = WeekFlowRules.ResolveVote(s, c, city, p, cfg, new System.Random(1), Side.A);
            Assert.IsNotNull(vote);
            Assert.AreEqual(AssemblyRules.TrialFavors(c.trial, s.trialGuilty) == Side.A ? 4 : -4,
                vote.lines.Find(l => l.term == VoteTerm.Trial).value, "η ετυμηγορία μπαίνει στην ίδια εβδομάδα");
        }

        [Test] // Ο λογογράφος μετρά μόνο αν μιλήσεις για την πλευρά που παρήγγειλες.
        public void LogographerLostIfSpeakingForTheOtherSide()
        {
            var c = TestWorld.Case(1); var cfg = TestWorld.Cfg();
            var s = new AgoraCaseState { caseId = 1 }; var p = Player();
            WeekFlowRules.BuyLogographer(s, c, p, cfg, Side.A, forTrial: false);
            WeekFlowRules.Speak(s, Side.B);
            var r = WeekFlowRules.ResolveVote(s, c, City(), p, cfg, new System.Random(1), Side.B);
            Assert.AreEqual(-3, r.lines.Find(l => l.term == VoteTerm.Speech).value, "Πειθώ 30 ÷ 10, χωρίς +3");
        }

        [Test] // Η ανάλυση αποθηκεύεται τη στιγμή της ψήφου και η ψήφος του παίκτη δεν αλλάζει το αποτέλεσμα.
        public void VoteIsStoredAndPlayerVoteDoesNotCount()
        {
            var c = TestWorld.Case(1); var cfg = TestWorld.Cfg();
            var a = new AgoraCaseState { caseId = 1 }; var b = new AgoraCaseState { caseId = 1 };
            var ra = WeekFlowRules.ResolveVote(a, c, City(), Player(), cfg, new System.Random(7), Side.A);
            var rb = WeekFlowRules.ResolveVote(b, c, City(), Player(), cfg, new System.Random(7), Side.B);
            Assert.AreEqual(ra.total, rb.total);
            Assert.AreEqual(ra.passedA, rb.passedA);
            Assert.IsTrue(a.resolved);
            Assert.AreEqual(ra.total, a.voteResult.total);
            Assert.IsNull(WeekFlowRules.ResolveVote(a, c, City(), Player(), cfg, new System.Random(8), Side.A), "μία ψηφοφορία");
        }
    }
}
