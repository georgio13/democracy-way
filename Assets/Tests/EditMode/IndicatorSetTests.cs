using System;
using DemocracyWay.Core;
using NUnit.Framework;

namespace DemocracyWay.Tests
{
    /// <summary>
    /// The indicators drive every consequence in the story, so the [0,100]
    /// clamp and the per-id routing must never drift — a cross-wired case in
    /// the Set switch would corrupt runs silently, which is why the roundtrip
    /// test uses a distinct value per indicator.
    /// </summary>
    public class IndicatorSetTests
    {
        [Test]
        public void Apply_ClampsAtMin()
        {
            var set = new IndicatorSet();

            set.Apply(IndicatorId.Eunomia, -1000);

            Assert.AreEqual(IndicatorSet.Min, set.Get(IndicatorId.Eunomia));
        }

        [Test]
        public void Apply_ClampsAtMax()
        {
            var set = new IndicatorSet();

            set.Apply(IndicatorId.Demofilia, +1000);

            Assert.AreEqual(IndicatorSet.Max, set.Get(IndicatorId.Demofilia));
        }

        [Test]
        public void Apply_NegativeDelta_SubtractsWithinRange()
        {
            var set = new IndicatorSet();
            set.Set(IndicatorId.Oikos, 40);

            set.Apply(IndicatorId.Oikos, -15);

            Assert.AreEqual(25, set.Get(IndicatorId.Oikos));
        }

        [Test]
        public void Set_ClampsBelowMinAndAboveMax()
        {
            var set = new IndicatorSet();

            set.Set(IndicatorId.Kachypopsia, -5);
            Assert.AreEqual(IndicatorSet.Min, set.Get(IndicatorId.Kachypopsia));

            set.Set(IndicatorId.Kachypopsia, 150);
            Assert.AreEqual(IndicatorSet.Max, set.Get(IndicatorId.Kachypopsia));
        }

        [Test]
        public void GetSet_RoundtripsEveryIndicatorIndependently()
        {
            var set = new IndicatorSet();

            // Distinct value per id: a switch case writing to the wrong field
            // would surface in the second pass, not just the immediate read-back.
            int value = 13;
            foreach (IndicatorId id in Enum.GetValues(typeof(IndicatorId)))
            {
                set.Set(id, value);
                Assert.AreEqual(value, set.Get(id), $"Immediate roundtrip failed for {id}.");
                value += 7;
            }

            value = 13;
            foreach (IndicatorId id in Enum.GetValues(typeof(IndicatorId)))
            {
                Assert.AreEqual(value, set.Get(id), $"{id} was overwritten by a later Set.");
                value += 7;
            }
        }

        [Test]
        public void Clone_CopiesValuesAndStaysIndependent()
        {
            var original = new IndicatorSet();
            original.Set(IndicatorId.Ethos, 70);

            var clone = original.Clone();

            Assert.AreEqual(70, clone.Get(IndicatorId.Ethos), "Clone must copy current values.");

            original.Set(IndicatorId.Ethos, 10);
            clone.Set(IndicatorId.Demofilia, 90);

            Assert.AreEqual(70, clone.Get(IndicatorId.Ethos),
                "Mutating the original must not touch the clone.");
            Assert.AreEqual(10, original.Get(IndicatorId.Ethos));
            Assert.AreEqual(50, original.Get(IndicatorId.Demofilia),
                "Mutating the clone must not touch the original.");
        }
    }
}
