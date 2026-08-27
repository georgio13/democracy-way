using System.Collections.Generic;
using System.Linq;
using DemocracyWay.Core;
using NUnit.Framework;

namespace DemocracyWay.Tests
{
    /// <summary>
    /// The calendar is the game's clock. A shuffle bug would let a tribe
    /// preside twice (historically impossible) and an off-by-one in the week
    /// roll would silently shorten or lengthen the year, so both the draw and
    /// the advance arithmetic are pinned down here.
    /// </summary>
    public class PrytanyCalendarTests
    {
        private static List<string> MakeIds(int count) =>
            Enumerable.Range(0, count).Select(i => $"tribe{i}").ToList();

        [Test]
        public void CreateNew_EveryTribeAppearsExactlyOnce()
        {
            var ids = MakeIds(10);

            var calendar = PrytanyCalendar.CreateNew(ids, weeksPerPrytany: 5, seed: 42);

            Assert.AreEqual(ids.Count, calendar.tribeOrder.Count,
                "Order must contain as many entries as tribes were given.");
            CollectionAssert.AreEquivalent(ids, calendar.tribeOrder,
                "Fisher–Yates is a permutation: same members, no duplicates, none dropped.");
        }

        [Test]
        public void CreateNew_StartsAtPrytanyOneWeekOne()
        {
            var calendar = PrytanyCalendar.CreateNew(MakeIds(3), weeksPerPrytany: 5, seed: 1);

            Assert.AreEqual(1, calendar.prytanyNumber);
            Assert.AreEqual(1, calendar.weekNumber);
            Assert.IsFalse(calendar.IsYearFinished);
        }

        [Test]
        public void CreateNew_SameSeed_ProducesSameOrder()
        {
            var first = PrytanyCalendar.CreateNew(MakeIds(10), weeksPerPrytany: 5, seed: 7);
            var second = PrytanyCalendar.CreateNew(MakeIds(10), weeksPerPrytany: 5, seed: 7);

            CollectionAssert.AreEqual(first.tribeOrder, second.tribeOrder,
                "The draw must be reproducible from the seed alone (saves store only numbers).");
        }

        [Test]
        public void CreateNew_DifferentSeeds_ProduceDifferentOrders()
        {
            var one = PrytanyCalendar.CreateNew(MakeIds(10), weeksPerPrytany: 5, seed: 1).tribeOrder;
            var other = PrytanyCalendar.CreateNew(MakeIds(10), weeksPerPrytany: 5, seed: 2).tribeOrder;

            // 10! permutations make a seed collision astronomically unlikely,
            // but it is legal — if seeds 1 and 2 happen to agree, seed 3 must not.
            if (one.SequenceEqual(other))
                other = PrytanyCalendar.CreateNew(MakeIds(10), weeksPerPrytany: 5, seed: 3).tribeOrder;

            CollectionAssert.AreNotEqual(one, other,
                "Different seeds must produce a different presiding order.");
        }

        [Test]
        public void CreateNew_ClampsWeeksPerPrytanyToAtLeastOne()
        {
            var calendar = PrytanyCalendar.CreateNew(MakeIds(3), weeksPerPrytany: 0, seed: 1);

            Assert.AreEqual(1, calendar.weeksPerPrytany,
                "A zero-week prytany would make AdvanceWeek loop forever in one call site or another.");
        }

        [Test]
        public void AdvanceWeek_IncrementsWeek_WithoutRollingPrytany()
        {
            var calendar = PrytanyCalendar.CreateNew(MakeIds(3), weeksPerPrytany: 5, seed: 1);

            var result = calendar.AdvanceWeek();

            Assert.AreEqual(2, calendar.weekNumber);
            Assert.AreEqual(1, calendar.prytanyNumber);
            Assert.IsFalse(result.prytanyChanged);
            Assert.IsFalse(result.yearFinished);
        }

        [Test]
        public void AdvanceWeek_RollsToNextPrytany_AfterWeeksPerPrytanyAdvances()
        {
            const int weeks = 5;
            var calendar = PrytanyCalendar.CreateNew(MakeIds(3), weeks, seed: 1);

            WeekAdvanceResult last = default;
            for (int i = 0; i < weeks; i++)
                last = calendar.AdvanceWeek();

            Assert.IsTrue(last.prytanyChanged, "The advance past the last week must flag the roll.");
            Assert.IsFalse(last.yearFinished);
            Assert.AreEqual(2, calendar.prytanyNumber);
            Assert.AreEqual(1, calendar.weekNumber, "A new prytany restarts at week 1.");
        }

        [Test]
        public void PresidingTribeId_FollowsTheDrawnOrder()
        {
            const int tribes = 4, weeks = 2;
            var calendar = PrytanyCalendar.CreateNew(MakeIds(tribes), weeks, seed: 9);

            for (int p = 0; p < tribes; p++)
            {
                Assert.AreEqual(calendar.tribeOrder[p], calendar.PresidingTribeId,
                    $"Prytany {p + 1} must be presided by tribeOrder[{p}].");
                for (int w = 0; w < weeks; w++)
                    calendar.AdvanceWeek();
            }
        }

        [Test]
        public void Year_FinishesAfterTribeCountTimesWeeksAdvances()
        {
            const int tribes = 3, weeks = 4;
            var calendar = PrytanyCalendar.CreateNew(MakeIds(tribes), weeks, seed: 5);

            WeekAdvanceResult last = default;
            for (int i = 0; i < tribes * weeks; i++)
            {
                Assert.IsFalse(calendar.IsYearFinished, $"Year ended early, at advance {i}.");
                last = calendar.AdvanceWeek();
            }

            Assert.IsTrue(last.yearFinished, "The final advance must report the year's end.");
            Assert.IsTrue(last.prytanyChanged, "The year ends by rolling past the last prytany.");
            Assert.IsTrue(calendar.IsYearFinished);
            Assert.AreEqual("", calendar.PresidingTribeId,
                "No tribe presides once the year is over.");
        }

        [Test]
        public void AdvanceWeek_AfterYearEnd_ReportsFinishedAndChangesNothing()
        {
            const int tribes = 2, weeks = 2;
            var calendar = PrytanyCalendar.CreateNew(MakeIds(tribes), weeks, seed: 3);
            for (int i = 0; i < tribes * weeks; i++)
                calendar.AdvanceWeek();
            int prytanyBefore = calendar.prytanyNumber;
            int weekBefore = calendar.weekNumber;

            var result = calendar.AdvanceWeek();

            Assert.IsTrue(result.yearFinished);
            Assert.IsFalse(result.prytanyChanged);
            Assert.AreEqual(prytanyBefore, calendar.prytanyNumber, "A finished year is frozen.");
            Assert.AreEqual(weekBefore, calendar.weekNumber, "A finished year is frozen.");
        }
    }
}
