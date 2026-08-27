using System;
using System.IO;
using DemocracyWay.Core;
using NUnit.Framework;
using UnityEngine;

namespace DemocracyWay.Tests
{
    /// <summary>
    /// Saves are the only state that survives a restart, so three things are
    /// pinned here: the full JSON roundtrip, the slot bookkeeping the menu
    /// trusts (Νέο Παιχνίδι lock, load list), and the fallbacks for corrupt or
    /// too-new files. RootOverride keeps every test inside a throwaway temp
    /// directory — the player's real persistentDataPath is never touched.
    /// </summary>
    public class SaveSystemTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "dw_save_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.RootOverride = null;
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }

        /// <summary>Mirrors SaveSystem's private on-disk layout (saves/slot{n}.json)
        /// so tests can plant corrupt and newer-version files directly.</summary>
        private string SlotPath(int slot) => Path.Combine(_root, "saves", $"slot{slot}.json");

        private void WriteRawSlotFile(int slot, string content)
        {
            Directory.CreateDirectory(Path.Combine(_root, "saves"));
            File.WriteAllText(SlotPath(slot), content);
        }

        private static GameSession MakeSession()
        {
            var session = new GameSession
            {
                profile = new CitizenProfile
                {
                    genderId = "gyne", genderTitle = "Γυνή",
                    tribeId = "erechtheis", tribeTitle = "Ερεχθηίς",
                    trittysId = "asty1", trittysTitle = "Άστυ",
                    wealthId = "zeugites", wealthTitle = "Ζευγίτης",
                    periodId = "pericles", periodTitle = "Εποχή του Περικλή",
                    professionId = "kerameus", professionTitle = "Κεραμεύς",
                    suspicionEnabled = true
                },
                currentChapterId = "ch01",
                currentChapterTitle = "Η Εκκλησία του Δήμου",
                playtimeSeconds = 123.5,
                createdAtIso = "2026-08-27T00:00:00.0000000Z",
                analyticsSessionId = "session-abc"
            };
            session.indicators.Set(IndicatorId.Eunomia, 61);
            session.indicators.Set(IndicatorId.Kachypopsia, 33);
            session.calendar = PrytanyCalendar.CreateNew(new[] { "a", "b", "c" }, weeksPerPrytany: 5, seed: 11);
            session.calendar.AdvanceWeek(); // prytany 1, week 2 — asserted below
            session.SetFlag("met_socrates");
            session.SetFlag("spoke_at_pnyx");
            session.choices.Add(new ChoiceRecord
            {
                chapterId = "ch01",
                nodeId = "n3",
                choiceId = "c1",
                choiceText = "Ναι, θα μιλήσω",
                prytany = 1,
                week = 2,
                atIso = "2026-08-27T00:01:00.0000000Z"
            });
            return session;
        }

        [Test]
        public void SaveLoad_RoundtripsTheWholeSession()
        {
            var original = MakeSession();

            SaveSystem.Save(0, original);
            var loaded = SaveSystem.Load(0);

            Assert.IsNotNull(loaded);

            // Profile: ids are the stable keys, titles keep the save self-labelled.
            Assert.AreEqual(original.profile.genderId, loaded.profile.genderId);
            Assert.AreEqual(original.profile.genderTitle, loaded.profile.genderTitle);
            Assert.AreEqual(original.profile.tribeId, loaded.profile.tribeId);
            Assert.AreEqual(original.profile.tribeTitle, loaded.profile.tribeTitle);
            Assert.AreEqual(original.profile.trittysId, loaded.profile.trittysId);
            Assert.AreEqual(original.profile.trittysTitle, loaded.profile.trittysTitle);
            Assert.AreEqual(original.profile.wealthId, loaded.profile.wealthId);
            Assert.AreEqual(original.profile.wealthTitle, loaded.profile.wealthTitle);
            Assert.AreEqual(original.profile.periodId, loaded.profile.periodId);
            Assert.AreEqual(original.profile.periodTitle, loaded.profile.periodTitle);
            Assert.AreEqual(original.profile.professionId, loaded.profile.professionId);
            Assert.AreEqual(original.profile.professionTitle, loaded.profile.professionTitle);
            Assert.IsTrue(loaded.profile.suspicionEnabled);

            // Indicators, including the ones left at their defaults.
            foreach (IndicatorId id in Enum.GetValues(typeof(IndicatorId)))
                Assert.AreEqual(original.indicators.Get(id), loaded.indicators.Get(id),
                    $"Indicator {id} did not survive the roundtrip.");

            // Calendar: the drawn order and the position inside the year.
            CollectionAssert.AreEqual(original.calendar.tribeOrder, loaded.calendar.tribeOrder);
            Assert.AreEqual(original.calendar.prytanyNumber, loaded.calendar.prytanyNumber);
            Assert.AreEqual(original.calendar.weekNumber, loaded.calendar.weekNumber);
            Assert.AreEqual(original.calendar.weeksPerPrytany, loaded.calendar.weeksPerPrytany);

            // Chapter position, flags, choice history, bookkeeping.
            Assert.AreEqual(original.currentChapterId, loaded.currentChapterId);
            Assert.AreEqual(original.currentChapterTitle, loaded.currentChapterTitle);
            CollectionAssert.AreEqual(original.flags, loaded.flags);
            Assert.AreEqual(1, loaded.choices.Count);
            Assert.AreEqual("ch01", loaded.choices[0].chapterId);
            Assert.AreEqual("n3", loaded.choices[0].nodeId);
            Assert.AreEqual("c1", loaded.choices[0].choiceId);
            Assert.AreEqual("Ναι, θα μιλήσω", loaded.choices[0].choiceText);
            Assert.AreEqual(1, loaded.choices[0].prytany);
            Assert.AreEqual(2, loaded.choices[0].week);
            Assert.AreEqual(original.playtimeSeconds, loaded.playtimeSeconds);
            Assert.AreEqual(original.createdAtIso, loaded.createdAtIso);
            Assert.AreEqual(original.analyticsSessionId, loaded.analyticsSessionId);
        }

        [Test]
        public void Save_OverwritesAnExistingSlot()
        {
            // Second save of a slot goes through the File.Replace branch of the
            // atomic write — worth exercising separately from the first-save path.
            var first = MakeSession();
            SaveSystem.Save(0, first);

            var second = MakeSession();
            second.currentChapterId = "ch02";
            second.calendar.AdvanceWeek();
            SaveSystem.Save(0, second);

            var loaded = SaveSystem.Load(0);
            Assert.AreEqual("ch02", loaded.currentChapterId);
            Assert.AreEqual(second.calendar.weekNumber, loaded.calendar.weekNumber);
        }

        [Test]
        public void Save_RejectsInvalidSlotAndNullSession()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SaveSystem.Save(-1, MakeSession()));
            Assert.Throws<ArgumentOutOfRangeException>(() => SaveSystem.Save(SaveSystem.SlotCount, MakeSession()));
            Assert.Throws<ArgumentNullException>(() => SaveSystem.Save(0, null));
        }

        [Test]
        public void EmptyDirectory_ReportsNoSaves()
        {
            for (int i = 0; i < SaveSystem.SlotCount; i++)
                Assert.IsFalse(SaveSystem.Exists(i), $"Slot {i} must start empty.");
            Assert.IsFalse(SaveSystem.AnyExists());
            Assert.IsFalse(SaveSystem.AllFull());
            Assert.AreEqual(0, SaveSystem.FirstEmptySlot());
        }

        [Test]
        public void SlotBookkeeping_TransitionsWhileFillingAllFourSlots()
        {
            // The menu derives everything from these four queries, so walk the
            // exact transitions it will see as slots fill up one by one.
            for (int slot = 0; slot < SaveSystem.SlotCount; slot++)
            {
                Assert.AreEqual(slot, SaveSystem.FirstEmptySlot(),
                    $"With {slot} slots filled, slot {slot} is the first free one.");
                Assert.IsFalse(SaveSystem.AllFull());

                SaveSystem.Save(slot, MakeSession());

                Assert.IsTrue(SaveSystem.Exists(slot));
                Assert.IsTrue(SaveSystem.AnyExists());
            }

            Assert.IsTrue(SaveSystem.AllFull(), "Four saves must lock Νέο Παιχνίδι.");
            Assert.AreEqual(-1, SaveSystem.FirstEmptySlot());
        }

        [Test]
        public void Delete_FreesTheSlot()
        {
            for (int slot = 0; slot < SaveSystem.SlotCount; slot++)
                SaveSystem.Save(slot, MakeSession());

            SaveSystem.Delete(2);

            Assert.IsFalse(SaveSystem.Exists(2));
            Assert.IsFalse(SaveSystem.AllFull());
            Assert.AreEqual(2, SaveSystem.FirstEmptySlot());
            Assert.IsTrue(SaveSystem.AnyExists(), "The other three slots are untouched.");
        }

        [Test]
        public void Delete_OnEmptySlot_IsANoOp()
        {
            Assert.DoesNotThrow(() => SaveSystem.Delete(1));
            Assert.IsFalse(SaveSystem.Exists(1));
        }

        [Test]
        public void CorruptFile_LoadsAsNull_ButTheFileStillOccupiesTheSlot()
        {
            WriteRawSlotFile(0, "όχι json");

            // Exists is a pure File.Exists check, so the broken file still
            // counts as taken — while Load and Summarize treat it as empty.
            // (Load logs a warning; the test framework only fails on errors.)
            Assert.IsTrue(SaveSystem.Exists(0));
            Assert.IsNull(SaveSystem.Load(0));

            var summary = SaveSystem.Summarize(0);
            Assert.IsFalse(summary.exists, "Summarize reports a corrupt slot as empty.");
            Assert.AreEqual(0, summary.slot);
        }

        [Test]
        public void NewerVersionFile_LoadsAsNull()
        {
            // A save from a future build must be ignored, never half-migrated.
            var data = new SaveData
            {
                version = SaveData.CurrentVersion + 1,
                savedAtIso = DateTime.UtcNow.ToString("o"),
                session = MakeSession()
            };
            WriteRawSlotFile(0, JsonUtility.ToJson(data));

            Assert.IsNull(SaveSystem.Load(0));
        }

        [Test]
        public void Summarize_OnASavedSlot_FillsTheLoadScreenMetadata()
        {
            SaveSystem.Save(1, MakeSession());

            var summary = SaveSystem.Summarize(1);

            Assert.IsTrue(summary.exists);
            Assert.AreEqual(1, summary.slot);
            Assert.AreEqual("Γυνή · Ερεχθηίς · Κεραμεύς", summary.profileLine,
                "The profile line is gender · tribe · profession, skipping empty titles.");
            Assert.AreEqual("Η Εκκλησία του Δήμου", summary.chapterTitle);
            Assert.AreEqual(1, summary.prytany);
            Assert.AreEqual(2, summary.week);
            Assert.AreEqual(123.5, summary.playtimeSeconds);
            Assert.IsFalse(string.IsNullOrEmpty(summary.savedAtIso),
                "Save stamps the timestamp; Summarize must surface it.");
        }

        [Test]
        public void Summarize_OnAnEmptySlot_ReportsNotExists()
        {
            var summary = SaveSystem.Summarize(3);

            Assert.IsFalse(summary.exists);
            Assert.AreEqual(3, summary.slot);
            Assert.AreEqual("", summary.profileLine);
        }
    }
}
