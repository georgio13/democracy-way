using System;
using System.IO;
using DemocracyWay.Core;
using NUnit.Framework;
using UnityEngine;

namespace DemocracyWay.Tests
{
    /// <summary>
    /// The JSONL file format is the analytics contract — a future backend and
    /// the researcher's pandas scripts both parse it line by line, so exactly
    /// one self-contained JSON object per Log call is what these tests pin.
    /// RootOverride keeps everything inside a throwaway temp directory.
    /// </summary>
    public class AnalyticsLogTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "dw_analytics_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            AnalyticsLog.RootOverride = _root;
        }

        [TearDown]
        public void TearDown()
        {
            AnalyticsLog.RootOverride = null;
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }

        [Test]
        public void Log_AppendsExactlyOneParsableLine()
        {
            AnalyticsLog.Log(new AnalyticsEvent
            {
                type = "choice_made",
                session = "session-abc",
                chapterId = "ch01",
                nodeId = "n3",
                choiceId = "c1",
                prytany = 1,
                week = 2
            });

            var lines = File.ReadAllLines(AnalyticsLog.FilePath);
            Assert.AreEqual(1, lines.Length, "One Log call = one line, nothing more.");

            var parsed = JsonUtility.FromJson<AnalyticsEvent>(lines[0]);
            Assert.AreEqual("choice_made", parsed.type);
            Assert.AreEqual("session-abc", parsed.session);
            Assert.AreEqual("ch01", parsed.chapterId);
            Assert.AreEqual(1, parsed.prytany);
            Assert.AreEqual(2, parsed.week);
            Assert.IsFalse(string.IsNullOrEmpty(parsed.t),
                "Log stamps the timestamp itself so callers can't forget it.");
        }

        [Test]
        public void Log_Null_IsANoOp()
        {
            AnalyticsLog.Log(null);

            Assert.IsFalse(File.Exists(AnalyticsLog.FilePath),
                "A null event must not even create the file.");
        }

        [Test]
        public void Log_TwoCalls_AppendTwoLinesInOrder()
        {
            AnalyticsLog.Log(new AnalyticsEvent { type = "game_started", session = "s1" });
            AnalyticsLog.Log(new AnalyticsEvent { type = "week_advanced", session = "s1", prytany = 1, week = 2 });

            var lines = File.ReadAllLines(AnalyticsLog.FilePath);
            Assert.AreEqual(2, lines.Length);
            Assert.AreEqual("game_started", JsonUtility.FromJson<AnalyticsEvent>(lines[0]).type);
            Assert.AreEqual("week_advanced", JsonUtility.FromJson<AnalyticsEvent>(lines[1]).type,
                "Append order is chronological order — pandas relies on it.");
        }
    }
}
