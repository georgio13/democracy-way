using DemocracyWay.Core;
using NUnit.Framework;

namespace DemocracyWay.Tests
{
    /// <summary>
    /// Story flags gate dialogue branches, so a duplicate or an accidental
    /// empty entry would corrupt every HasFlag check downstream — SetFlag must
    /// stay idempotent and reject blanks.
    /// </summary>
    public class GameSessionTests
    {
        [Test]
        public void SetFlag_AddsTheFlagOnce()
        {
            var session = new GameSession();

            session.SetFlag("met_socrates");

            Assert.AreEqual(1, session.flags.Count);
            Assert.IsTrue(session.HasFlag("met_socrates"));
        }

        [Test]
        public void SetFlag_DedupesRepeatedCalls()
        {
            var session = new GameSession();

            session.SetFlag("met_socrates");
            session.SetFlag("met_socrates");
            session.SetFlag("met_socrates");

            Assert.AreEqual(1, session.flags.Count,
                "Re-running a dialogue node must not multiply the flag.");
        }

        [Test]
        public void SetFlag_IgnoresNullAndEmpty()
        {
            var session = new GameSession();

            session.SetFlag(null);
            session.SetFlag("");

            Assert.AreEqual(0, session.flags.Count);
        }

        [Test]
        public void HasFlag_IsFalseForUnsetFlags()
        {
            var session = new GameSession();
            session.SetFlag("met_socrates");

            Assert.IsFalse(session.HasFlag("spoke_at_pnyx"));
            Assert.IsFalse(session.HasFlag(""));
        }
    }
}
