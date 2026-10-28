using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Agora.Tests
{
    /// <summary>Τα assets ταυτίζονται με το Agora_FINAL_DESIGN (ενότητες 4–5) και το Agora_FINAL_texts.</summary>
    public sealed class ContentTests
    {
        static List<T> LoadAll<T>() where T : UnityEngine.Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(x => x != null).ToList();

        [Test] // Β1
        public void B1_AssetCounts()
        {
            Assert.AreEqual(20, LoadAll<CaseData>().Count, "CaseData");
            Assert.AreEqual(35, LoadAll<OfficeData>().Count, "OfficeData");
            Assert.AreEqual(21, LoadAll<OfficialData>().Count, "OfficialData");
            Assert.AreEqual(1, LoadAll<LogographerData>().Count, "LogographerData (μόνο ο Δείναρχος)");
            Assert.AreEqual(1, LoadAll<GameConfig>().Count, "GameConfig");
            Assert.AreEqual(1, LoadAll<GameContent>().Count, "GameContent");
        }

        [Test] // Β2
        public void B2_CaseOrder()
        {
            var cases = TestWorld.Content.cases;
            Assert.AreEqual(20, cases.Count);
            for (int i = 0; i < cases.Count; i++)
            {
                Assert.AreEqual(i + 1, cases[i].id, "id στη θέση " + i);
                Assert.AreEqual(i / 2 + 1, cases[i].prytany, "prytany της υπόθεσης " + (i + 1));
            }
        }

        [Test] // Β3
        public void B3_Texts()
        {
            foreach (var c in TestWorld.Content.cases)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(c.newsPeace), c.name + ".newsPeace");
                Assert.IsFalse(string.IsNullOrWhiteSpace(c.ek1), c.name + ".ek1");
                Assert.IsFalse(string.IsNullOrWhiteSpace(c.ek3), c.name + ".ek3");
                Assert.IsFalse(string.IsNullOrWhiteSpace(c.speechA?.text), c.name + ".speechA");
                Assert.IsFalse(string.IsNullOrWhiteSpace(c.speechB?.text), c.name + ".speechB");
                Assert.IsFalse(string.IsNullOrWhiteSpace(c.argumentA), c.name + ".argumentA");
                Assert.IsFalse(string.IsNullOrWhiteSpace(c.argumentB), c.name + ".argumentB");
                Assert.IsNotNull(c.speechA.speaker, c.name + ".speakerA");
                Assert.IsNotNull(c.speechB.speaker, c.name + ".speakerB");
            }
        }

        [Test] // Β5
        public void B5_SpecialFlags()
        {
            foreach (var c in TestWorld.Content.cases)
            {
                Assert.AreEqual(c.id == 12, c.abolishesPayIfB, c.name + ".abolishesPayIfB");
                Assert.AreEqual(c.id == 15, c.decidesWarEnd, c.name + ".decidesWarEnd");
                Assert.AreEqual(c.id == 17, c.auditFineIfA, c.name + ".auditFineIfA");
                Assert.AreEqual(c.id == 18, c.areopagusIfB, c.name + ".areopagusIfB");
                Assert.AreEqual(c.id == 19 || c.id == 20, c.affectedByAreopagus, c.name + ".affectedByAreopagus");
            }
        }

        [Test] // Β6
        public void B6_OfficeCounts()
        {
            var expected = new Dictionary<string, int>
            {
                { "Office_Boule", 500 }, { "Office_Prytaneis", 50 }, { "Office_Thesmothetai", 6 },
                { "Office_Strategoi", 10 }, { "Office_Sitophylakes", 35 }, { "Office_Hendeka", 11 },
                { "Office_Tettarakonta", 40 }, { "Office_Eisagogeis", 5 }, { "Office_Hodopoioi", 5 },
                { "Office_Dikastai", 6000 }, { "Office_Hipparchoi", 2 }
            };
            var offices = LoadAll<OfficeData>().ToDictionary(o => o.name);
            foreach (var pair in expected)
            {
                Assert.IsTrue(offices.ContainsKey(pair.Key), "Λείπει το " + pair.Key);
                Assert.AreEqual(pair.Value, offices[pair.Key].count, pair.Key + ".count");
            }
        }

        // Β9 — οι αριθμοί των 20 υποθέσεων (ενότητα 4) και οι θέσεις των αξιωματούχων (αρχείο κειμένων).
        // Μορφή: id, prytany, venue, base, "πίεση:πλευρά", έκτακτη, "αναποφάσιστος:δυσκολία", "effectsA", "effectsB", "officials"
        // effects: "Em+3 Et-2" · πίεση ANY = ο χαμηλότερος από τους 4 ορατούς · officials: "Thrasyllos:A Kephalos:N"
        static readonly object[][] Table =
        {
            new object[] { 1, 1, "DemeAssembly", 57, "Em:B", false, "Kephalos:Easy", "Em+2 ID+2", "Em-2 ID-2", "Thrasyllos:A Archias:B Kephalos:N" },
            new object[] { 2, 1, "Pnyx", 50, "Ep:B", true, "Aristonikos:Medium", "Ep+12 T-10 ID+2", "Ep+8 Em-3 ID-2", "Timokrates:A Demades:B Aristonikos:N" },
            new object[] { 3, 2, "Pnyx", 50, "Et:B", true, "Aristonikos:Medium", "Em+3 Et-3 ID+3", "Et+5 Em-3 ID-2", "Hypereides:A Phokion:B Aristonikos:N" },
            new object[] { 4, 2, "Council", 50, "Em:B", false, "Kephalos:Easy", "Em+2 ID+3", "T+3 Em-2 ID-3", "Kephisodoros:A Demades:B Kephalos:N" },
            new object[] { 5, 3, "Pnyx", 55, "Em:B", false, "Pytheas:Easy", "Em+2 ID+2", "Em-1 ID-2", "Agnonides:A Eurymedon:B Pytheas:N" },
            new object[] { 6, 3, "Council", 52, "Em:B", false, "Lykourgos:Hard", "Em+2 ID+3", "Et+4 T+3 Em-2 ID-3", "Mnesitheos:A Pytheas:B Lykourgos:N" },
            new object[] { 7, 4, "Council", 50, "Et:A", true, "Mnesitheos:Easy", "Et+10 T-8 ID+2", "Et+12 T-3 Em-2 ID-2", "Lykourgos:A Euthydemos:B Mnesitheos:N" },
            new object[] { 8, 4, "Council", 54, "T:A", false, "Demades:Medium", "T+8 Em+2 ID+2", "T+12 Em-3 ID-2", "Kephalos:A Archias:B Demades:N" },
            new object[] { 9, 5, "Pnyx", 56, "Em:B", false, "Hypereides:Medium", "Em+4 T+3 ID+2", "T+5 Em-4 ID-2", "Phokion:A Leosthenes:B Hypereides:N" },
            new object[] { 10, 5, "Pnyx", 54, "Et:A", true, "Euthydemos:Medium", "Em+3 ID+3", "Et+4 Em-3 ID-3", "Hypereides:A Demades:B Euthydemos:N" },
            new object[] { 11, 6, "Pnyx", 58, "Em:B", true, "Pytheas:Easy", "Em+4 ID+3", "T+3 Em-3 ID-3", "Phokion:A Agnonides:B Pytheas:N" },
            new object[] { 12, 6, "Pnyx", 58, "T:B", true, "Demades:Medium", "T-8 Em+2 ID+4", "T+12 Em-3 ID-4", "Hypereides:A Euthydemos:B Demades:N" },
            new object[] { 13, 7, "Pnyx", 60, "Et:B", true, "Kephisodoros:Easy", "Em+4 T+4 ID+3", "Et+5 Em-4 ID-2", "Lykourgos:A Stratokles:B Kephisodoros:N" },
            new object[] { 14, 7, "Pnyx", 52, "Em:B", false, "Demosthenes:Medium", "Em+2 Et-3 ID+3", "Et+3 Em-2 ID-2", "Aristonikos:A Pytheas:B Demosthenes:N" },
            new object[] { 15, 8, "Pnyx", 53, "Ep:B", true, "Demades:Hard", "Em+2 ID+2", "Et+5 Em-3 ID-2", "Hypereides:A Phokion:B Demades:N" },
            new object[] { 16, 8, "Pnyx", 49, "Ep:A", false, "Lykourgos:Easy", "Ep+5 Em+2 T-4 ID+2", "T+5 Ep-3 Em-2 ID-2", "Timokrates:A Archias:B Lykourgos:N" },
            new object[] { 17, 9, "Pnyx", 53, "T:B", true, "Kephalos:Easy", "Em+3 T-3 ID+3", "T+5 Em-3 ID-2", "Thrasyllos:A Lykourgos:A Demades:B Kephalos:N" },
            new object[] { 18, 9, "Pnyx", 51, "Em:B", true, "Phokion:Hard", "Em+2 ID+2", "T+2 Em-2 ID-2", "Demosthenes:A Polyeuktos:B Eurymedon:B Phokion:N" },
            new object[] { 19, 10, "Pnyx", 49, "Et:B", true, "Demades:Hard", "Et+6 Em+2 ID+3", "T+4 Em-3 ID-4", "Lykourgos:A Agnonides:A Pytheas:B Stratokles:B Demades:N" },
            new object[] { 20, 10, "Pnyx", 47, "ANY:B", true, "Phokion:Hard", "Em+4 ID+6", "Et+4 T+4 Em-4 ID-12", "Hypereides:A Demosthenes:A Lykourgos:A Demades:B Polyeuktos:B Euthydemos:B Phokion:N" },
        };

        [TestCaseSource(nameof(Table))] // Β9
        public void B9_CaseNumbers(object[] row)
        {
            int id = (int)row[0];
            var c = TestWorld.Case(id);
            Assert.AreEqual((int)row[1], c.prytany, "prytany");
            Assert.AreEqual((string)row[2], c.venue.ToString(), "venue");
            Assert.AreEqual((int)row[3], c.baseSupport, "baseSupport");
            var pressure = ((string)row[4]).Split(':');
            Assert.AreEqual(pressure[0] == "ANY", c.pressureAnyMetric, "pressureAnyMetric");
            if (pressure[0] != "ANY") Assert.AreEqual(TestWorld.ParseMetric(pressure[0]), c.pressureMetric, "pressureMetric");
            Assert.AreEqual(pressure[1] == "A" ? Side.A : Side.B, c.pressureFavors, "pressureFavors");
            Assert.AreEqual((bool)row[5], c.emergencyB, "emergencyB");
            var undecided = ((string)row[6]).Split(':');
            Assert.AreEqual("Official_" + undecided[0], c.undecided != null ? c.undecided.name : null, "undecided");
            Assert.AreEqual(undecided[1], c.difficulty.ToString(), "difficulty");

            AssertDeltas("effectsA", (string)row[7], c.effectsA.Select(e => (e.metric, e.amount)));
            AssertDeltas("effectsB", (string)row[8], c.effectsB.Select(e => (e.metric, e.amount)));

            var expectedOfficials = ((string)row[9]).Split(' ').Select(ParseOfficial).ToList();
            Assert.AreEqual(expectedOfficials.Count, c.officials.Count, "πλήθος αξιωματούχων");
            foreach (var e in expectedOfficials)
            {
                var stance = c.officials.FirstOrDefault(s => s.official != null && s.official.name == "Official_" + e.key);
                Assert.IsNotNull(stance, "λείπει ο " + e.key);
                Assert.AreEqual(e.side, stance.side, e.key + ".side");
            }
        }

        [Test] // Κάθε υπόθεση: βάση, δείκτης πίεσης, ένας αναποφάσιστος (ο ίδιος με τη θέση «αναποφάσιστος» της λίστας).
        public void B10_EveryCaseHasBasePressureAndUndecided()
        {
            foreach (var c in TestWorld.Content.cases)
            {
                Assert.That(c.baseSupport, Is.InRange(47, 60), c.name + ".baseSupport");
                Assert.AreNotEqual(Side.None, c.pressureFavors, c.name + ".pressureFavors");
                Assert.IsNotNull(c.undecided, c.name + ".undecided");
                var neutral = c.officials.Where(s => s.side == Side.None).ToList();
                Assert.AreEqual(1, neutral.Count, c.name + ": ένας αναποφάσιστος");
                Assert.AreEqual(c.undecided, neutral[0].official, c.name + ": ο αναποφάσιστος της λίστας");
            }
        }

        // id υπόθεσης, χώρος, διαδικασία, κατηγορούμενος, στοιχεία, πλευρά της καταδίκης, αναπληρωτής ομιλητής
        static readonly object[][] Trials =
        {
            new object[] { 3, TrialVenue.Heliaia, ComplaintProcedure.Eisangelia, "Mnesitheos", 58, Side.B, "" },
            new object[] { 9, TrialVenue.Heliaia, ComplaintProcedure.Endeixis, "Leosthenes", 18, Side.A, "Antiphilos" },
            new object[] { 10, TrialVenue.Areopagus, ComplaintProcedure.Apophasis, "Demades", 62, Side.A, "Pytheas" },
            new object[] { 11, TrialVenue.Heliaia, ComplaintProcedure.GrapheSykophantias, "Agnonides", 52, Side.A, "Archias" },
            new object[] { 13, TrialVenue.Heliaia, ComplaintProcedure.Endeixis, "Stratokles", 18, Side.A, "Pytheas" },
        };

        [TestCaseSource(nameof(Trials))] // Οι 5 δίκες της ενότητας 5
        public void B11_Trials(object[] row)
        {
            var t = TestWorld.Case((int)row[0]).trial;
            Assert.IsTrue(t.exists, "exists");
            Assert.AreEqual((TrialVenue)row[1], t.venue, "venue");
            Assert.AreEqual((ComplaintProcedure)row[2], t.procedure, "procedure");
            Assert.AreEqual("Official_" + row[3], t.defendant != null ? t.defendant.name : null, "defendant");
            Assert.AreEqual((int)row[4], t.evidence, "evidence");
            Assert.AreEqual((Side)row[5], t.convictionFavors, "convictionFavors");
            string replacement = (string)row[6];
            Assert.AreEqual(replacement.Length == 0 ? null : "Official_" + replacement, t.replacementSpeaker != null ? t.replacementSpeaker.name : null, "replacementSpeaker");
            foreach (var (label, text) in new[] { ("defaultAccuser", t.defaultAccuser), ("charge", t.charge), ("defence", t.defence), ("verdictGuilty", t.verdictGuilty), ("verdictAcquitted", t.verdictAcquitted) })
                Assert.IsFalse(string.IsNullOrWhiteSpace(text), label);
        }

        [Test]
        public void B11_OnlyFiveWeeksHaveTrials()
        {
            var withTrial = TestWorld.Content.cases.Where(c => c.trial != null && c.trial.exists).Select(c => c.id).ToArray();
            CollectionAssert.AreEqual(new[] { 3, 9, 10, 11, 13 }, withTrial);
            Assert.IsFalse(string.IsNullOrWhiteSpace(TestWorld.Content.heliaiaIntro), "heliaiaIntro");
            Assert.IsFalse(string.IsNullOrWhiteSpace(TestWorld.Content.areopagusIntro), "areopagusIntro");
        }

        [Test] // Καμία σημείωση σχεδιασμού σε αγκύλες («[Εμπιστοσύνη: 50]») σε κανένα κείμενο.
        public void B12_NoDesignNotesInTexts()
        {
            foreach (var c in TestWorld.Content.cases)
            {
                var texts = new[] { c.title, c.newsPeace, c.newsWar, c.ek1, c.ek3, c.optionA, c.optionB, c.argumentA, c.argumentB,
                    c.speechA?.text, c.speechB?.text, c.trial?.marketClue, c.trial?.charge, c.trial?.defence, c.trial?.verdictGuilty, c.trial?.verdictAcquitted };
                foreach (var text in texts)
                    Assert.IsFalse((text ?? "").Contains("["), c.name + ": «[» στο «" + (text ?? "") + "»");
            }
            Assert.IsFalse((TestWorld.Content.heliaiaIntro + TestWorld.Content.areopagusIntro).Contains("["));
        }

        [Test] // Υπόθεση 14: τη θέση του Δείναρχου (μέτοικος) παίρνει ο Πυθέας· ο Δείναρχος μένει μόνο λογογράφος.
        public void B13_Case14SpeakerIsPytheas()
        {
            var c = TestWorld.Case(14);
            Assert.AreEqual("Official_Pytheas", c.speechB.speaker.name);
            Assert.IsFalse(c.officials.Any(s => s.official.name == "Official_Deinarchos"));
            Assert.AreEqual("Δείναρχος", TestWorld.Content.logographers.Single().displayName);
        }

        static void AssertDeltas(string label, string spec, IEnumerable<(Metric metric, int amount)> actual)
        {
            var expected = spec.Split(' ')
                .Select(token =>
                {
                    int split = token.IndexOfAny(new[] { '+', '-' });
                    return (metric: TestWorld.ParseMetric(token.Substring(0, split)), amount: int.Parse(token.Substring(split)));
                })
                .OrderBy(x => x.metric).ToList();
            var got = actual.OrderBy(x => x.metric).ToList();
            Assert.AreEqual(expected.Count, got.Count, label + ": πλήθος");
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].metric, got[i].metric, label + "[" + i + "].metric");
                Assert.AreEqual(expected[i].amount, got[i].amount, label + "[" + i + "]." + expected[i].metric);
            }
        }

        static (string key, Side side) ParseOfficial(string token)
        {
            var parts = token.Split(':');
            return (parts[0], parts[1] == "A" ? Side.A : parts[1] == "B" ? Side.B : Side.None);
        }
    }
}
