using DemocracyWay.NewUI;
using NUnit.Framework;

namespace Agora.Tests
{
    /// <summary>Agora_FINAL_DESIGN 7.2 «Πού γέρνει η πόλη» και η ψήφος του παίκτη ως δικαστή.</summary>
    public sealed class LeaningTests
    {
        [TestCase(56, CityLeaning.TowardA)]
        [TestCase(55, CityLeaning.Contested)]
        [TestCase(45, CityLeaning.Contested)]
        [TestCase(44, CityLeaning.TowardB)]
        public void Leaning_Thresholds(int known, CityLeaning expected)
            => Assert.AreEqual(expected, AssemblyRules.Leaning(new VoteInput { baseSupport = known, pressureValue = 50 }, TestWorld.Cfg()));

        [Test] // Ίδιος κανόνας με την ψηφοφορία, χωρίς τη διάθεση: 54 + 5 + 4 = 63.
        public void KnownSum_IsTheVoteWithoutMood()
        {
            var v = new VoteInput { baseSupport = 54, pressureValue = 31, pressureFavors = Side.A, officialFavors = Side.A };
            Assert.AreEqual(63, AssemblyRules.KnownSum(v, TestWorld.Cfg()));
            Assert.AreEqual(AssemblyRules.Vote(v, TestWorld.Cfg(), 0).total, AssemblyRules.KnownSum(v, TestWorld.Cfg()));
        }

        [Test] // Η γραμμή ενημερώνεται μόλις ο παίκτης πείσει αξιωματούχο ή πάρει λογογράφο.
        public void Leaning_ChangesAfterOfficialAndLogographer()
        {
            var cfg = TestWorld.Cfg();
            var c = TestWorld.Case(2);                       // βάση 50
            var city = TestWorld.City(50, 50, 50, 50, 50);
            var p = TestWorld.Player(cfg); p.peitho = 45; p.wallet = 500;
            var s = new AgoraCaseState { caseId = 2 };
            Assert.AreEqual(50, AssemblyRules.KnownSum(WeekFlowRules.KnownVoteInput(s, c, city, p), cfg));
            WeekFlowRules.Persuade(s, c, p, cfg, Side.A);
            Assert.AreEqual(54, AssemblyRules.KnownSum(WeekFlowRules.KnownVoteInput(s, c, city, p), cfg));
            WeekFlowRules.BuyLogographer(s, c, p, cfg, Side.A, forTrial: false);
            Assert.AreEqual(54 + 4 + 3, AssemblyRules.KnownSum(WeekFlowRules.KnownVoteInput(s, c, city, p), cfg), "Πειθώ 45 ÷ 10 + 3");
            Assert.AreEqual(CityLeaning.TowardA, AssemblyRules.Leaning(WeekFlowRules.KnownVoteInput(s, c, city, p), cfg));
        }

        [Test] // Ψήφος δικαστή μόνο στην Ηλιαία, μόνο αν δεν κατηγορείς, πριν τη δίκη· δεν αλλάζει την ετυμηγορία.
        public void JudgeVote_OnlyHeliaiaWhenNotAccusing()
        {
            var cfg = TestWorld.Cfg();
            var heliaia = TestWorld.Case(3);
            var s = new AgoraCaseState { caseId = 3 };
            Assert.IsTrue(WeekFlowRules.JudgeVote(s, heliaia, guilty: false));
            Assert.AreEqual(2, s.judgeVote);

            var areopagus = TestWorld.Case(10);
            Assert.IsFalse(WeekFlowRules.JudgeVote(new AgoraCaseState { caseId = 10 }, areopagus, true), "Άρειος Πάγος");

            var accuser = new AgoraCaseState { caseId = 3, accuser = true };
            Assert.IsFalse(WeekFlowRules.JudgeVote(accuser, heliaia, true), "ο κατήγορος δεν δικάζει");

            var a = new AgoraCaseState { caseId = 3 }; var b = new AgoraCaseState { caseId = 3 };
            WeekFlowRules.JudgeVote(a, heliaia, true); WeekFlowRules.JudgeVote(b, heliaia, false);
            var ra = WeekFlowRules.RunTrial(a, heliaia, TestWorld.Player(cfg), cfg, new System.Random(5));
            var rb = WeekFlowRules.RunTrial(b, heliaia, TestWorld.Player(cfg), cfg, new System.Random(5));
            Assert.AreEqual(ra.total, rb.total);
            Assert.AreEqual(ra.guilty, rb.guilty);
            Assert.IsFalse(WeekFlowRules.JudgeVote(a, heliaia, true), "μετά τη δίκη");
        }
    }
}
