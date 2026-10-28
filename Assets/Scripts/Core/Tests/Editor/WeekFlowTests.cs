using DemocracyWay.NewUI;
using NUnit.Framework;

namespace Agora.Tests
{
    /// <summary>
    /// Η ροή της εβδομάδας στον χάρτη: αγορά λόγου στους Λογογράφους, δέσμευση καταγγελίας στις
    /// Καταγγελίες, μία ενέργεια προπαρασκευής. Οι κανόνες (AssemblyRules) δεν αλλάζουν — εδώ
    /// ελέγχεται η λογιστική του WeekFlowRules που καταναλώνει η συνέλευση.
    /// </summary>
    public sealed class WeekFlowTests
    {
        [Test] // (α) Αγορά λόγου Α + ομιλία μόνος υπέρ Β → SpeechPower με L0, το πορτοφόλι έχασε το κόστος.
        public void BoughtSpeechLeftInPouch_SpeaksAloneWithL0()
        {
            var cfg = TestWorld.Cfg();
            var player = TestWorld.Player(cfg);
            player.wallet = 500;
            var logos = TestWorld.Content.logographers;
            var week = new AgoraCaseState { caseId = 1 };

            Assert.IsTrue(WeekFlowRules.BuySpeech(week, player, 2, Side.A, logos[2].costDrachmas), "αγορά λόγου Α");
            Assert.AreEqual(500 - logos[2].costDrachmas, player.wallet, "το κόστος πληρώθηκε στην παραγγελία");
            Assert.IsFalse(WeekFlowRules.BuySpeech(week, player, 3, Side.A, logos[3].costDrachmas), "μία παραγγελία την εβδομάδα");

            Assert.IsTrue(WeekFlowRules.Speak(week, Side.B, delivered: false), "μιλά μόνος υπέρ Β");
            Assert.AreEqual(0, WeekFlowRules.SpeechLogoIndex(week), "ο αγορασμένος λόγος έμεινε στο σακούλι — L0");
            Assert.AreEqual(AssemblyRules.SpeechPower(player, logos[0], cfg),
                AssemblyRules.SpeechPower(player, logos[WeekFlowRules.SpeechLogoIndex(week)], cfg),
                "η δύναμη του λόγου υπολογίζεται σαν να μίλησε μόνος");
            Assert.AreEqual(500 - logos[2].costDrachmas, player.wallet, "χωρίς επιστροφή χρημάτων");
        }

        [Test] // Εκφώνηση του αγορασμένου λόγου → SpeechPower με τον λογογράφο της παραγγελίας.
        public void DeliveredSpeechUsesBoughtLogographer()
        {
            var cfg = TestWorld.Cfg();
            var player = TestWorld.Player(cfg);
            player.wallet = 500;
            var logos = TestWorld.Content.logographers;
            var week = new AgoraCaseState { caseId = 1 };

            Assert.IsTrue(WeekFlowRules.BuySpeech(week, player, 3, Side.B, logos[3].costDrachmas));
            Assert.IsTrue(WeekFlowRules.Speak(week, (Side)week.speechSide, delivered: true));
            Assert.AreEqual(3, WeekFlowRules.SpeechLogoIndex(week));
            Assert.AreEqual((int)Side.B, week.spokeFor);
        }

        [Test] // Σιωπή: δεν γράφεται ομιλία, δεν υπάρχει λογογράφος για το SpeechPower.
        public void SilenceWritesNothing()
        {
            var week = new AgoraCaseState { caseId = 1 };
            Assert.IsTrue(WeekFlowRules.Speak(week, Side.None, delivered: false));
            Assert.IsFalse(week.spoke);
            Assert.AreEqual(-1, WeekFlowRules.SpeechLogoIndex(week));
        }

        [Test] // (β) Η δεσμευμένη καταγγελία παραλαμβάνεται από τη συνέλευση ακριβώς μία φορά.
        public void CommittedComplaintIsTakenExactlyOnce()
        {
            var week = new AgoraCaseState { caseId = 3 };
            Assert.IsTrue(WeekFlowRules.CommitComplaint(week), "δέσμευση στις Καταγγελίες");
            Assert.IsTrue(week.complaintPending && week.prepDone, "η ενέργεια καταναλώθηκε, η δίκη εκκρεμεί");
            Assert.IsTrue(WeekFlowRules.TakePendingComplaint(week), "η συνέλευση την παραλαμβάνει");
            Assert.IsFalse(week.complaintPending);
            Assert.IsFalse(WeekFlowRules.TakePendingComplaint(week), "όχι δεύτερη δίκη");
        }

        [Test] // (γ) Μία ενέργεια την εβδομάδα: μετά το Πείθω, η καταγγελία αρνείται.
        public void OnePreparationActionPerWeek()
        {
            var week = new AgoraCaseState { caseId = 3 };
            Assert.IsTrue(WeekFlowRules.MarkPrep(week, PrepAction.Persuade));
            Assert.IsFalse(WeekFlowRules.CommitComplaint(week), "η ενέργεια της εβδομάδας έχει καταναλωθεί");
            Assert.IsFalse(WeekFlowRules.MarkPrep(week, PrepAction.Bribe));
            Assert.AreEqual((int)PrepAction.Persuade, week.prep);
        }

        [Test] // Κριμένη υπόθεση: καμία αγορά, καμία δέσμευση, καμία ομιλία.
        public void ResolvedWeekAcceptsNothing()
        {
            var cfg = TestWorld.Cfg();
            var player = TestWorld.Player(cfg);
            player.wallet = 500;
            var week = new AgoraCaseState { caseId = 1, resolved = true };
            Assert.IsFalse(WeekFlowRules.BuySpeech(week, player, 2, Side.A, 60));
            Assert.IsFalse(WeekFlowRules.CommitComplaint(week));
            Assert.IsFalse(WeekFlowRules.Speak(week, Side.A, false));
            Assert.AreEqual(500, player.wallet);
        }
    }
}
