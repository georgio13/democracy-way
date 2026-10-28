using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Agora.Tests
{
    /// <summary>VERIFICATION.md ενότητα Β: τα assets ταυτίζονται με τον πίνακα σχεδίασης.</summary>
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
            Assert.AreEqual(4, LoadAll<LogographerData>().Count, "LogographerData");
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

        [Test] // Β4
        public void B4_Complaints()
        {
            var expected = new Dictionary<int, (bool guilty, int votesFor, int votesAgainst)>
            {
                { 3, (true, 318, 183) }, { 9, (false, 87, 414) }, { 10, (true, 301, 200) },
                { 11, (true, 276, 225) }, { 13, (false, 94, 407) }
            };
            foreach (var c in TestWorld.Content.cases)
            {
                if (expected.TryGetValue(c.id, out var e))
                {
                    Assert.IsTrue(c.complaint.available, c.name + ": complaint.available");
                    Assert.AreEqual(e.guilty, c.complaint.guilty, c.name + ": guilty");
                    Assert.AreEqual(e.votesFor, c.complaint.votesFor, c.name + ": votesFor");
                    Assert.AreEqual(e.votesAgainst, c.complaint.votesAgainst, c.name + ": votesAgainst");
                    Assert.IsNotNull(c.complaint.target, c.name + ": target");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(c.complaint.charge), c.name + ": charge");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(c.complaint.defence), c.name + ": defence");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(c.complaint.verdictText), c.name + ": verdictText");
                }
                else
                    Assert.IsFalse(c.complaint != null && c.complaint.available, c.name + ": δεν έπρεπε να έχει καταγγελία");
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
                Assert.AreEqual(c.id == 18 ? 52 : -1, c.lowTrustBaseOverride, c.name + ".lowTrustBaseOverride");
                Assert.AreEqual(c.id == 20 ? 40 : -1, c.lowAnyMetricBaseOverride, c.name + ".lowAnyMetricBaseOverride");
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

        [Test] // Β7
        public void B7_Logographers()
        {
            var logos = TestWorld.Content.logographers;
            Assert.AreEqual(4, logos.Count);
            int[] quality = { 0, 3, 6, 9 };
            int[] cost = { 0, 20, 60, 150 };
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(quality[i], logos[i].quality, "quality στη θέση " + i);
                Assert.AreEqual(cost[i], logos[i].costDrachmas, "κόστος στη θέση " + i);
                if (i == 0) continue;   // ο «χωρίς λογογράφο» δεν έχει ύφος
                Assert.IsFalse(string.IsNullOrWhiteSpace(logos[i].openingTemplate), "openingTemplate στη θέση " + i);
                Assert.IsFalse(string.IsNullOrWhiteSpace(logos[i].closingTemplate), "closingTemplate στη θέση " + i);
            }
        }

        // Β8/Β9 — ο πλήρης πίνακας αριθμών των 20 υποθέσεων.
        // Μορφή: id, prytany, inCouncil, base, emergencyB, "sens", "effectsA", "effectsB", "officials"
        // sens/effects: "Em+3 Et-2" · officials: "Thrasyllos:A:5 Kephalos:N:5:50"
        static readonly object[][] Table =
        {
            new object[] { 1, 1, false, 60, false, "Em+3", "Em+2 ID+2", "Em-2 ID-2", "Thrasyllos:A:5 Archias:B:5 Kephalos:N:5:50" },
            new object[] { 2, 1, false, 61, true, "Ep+5", "Ep+12 T-10 ID+2", "Ep+8 Em-3 ID-2", "Timokrates:A:5 Demades:B:15 Aristonikos:N:10:60" },
            new object[] { 3, 2, false, 51, true, "Et+4", "Em+3 Et-3 ID+3", "Et+5 Em-3 ID-2", "Hypereides:A:15 Phokion:B:15 Aristonikos:N:10:60" },
            new object[] { 4, 2, true, 56, false, "Em+3", "Em+2 ID+3", "T+3 Em-2 ID-3", "Kephisodoros:A:10 Demades:B:15 Kephalos:N:5:50" },
            new object[] { 5, 3, false, 58, false, "Em+2", "Em+2 ID+2", "Em-1 ID-2", "Agnonides:A:10 Eurymedon:B:10 Pytheas:N:10:50" },
            new object[] { 6, 3, true, 55, false, "Em+3 Et-2", "Em+2 ID+3", "Et+4 T+3 Em-2 ID-3", "Mnesitheos:A:5 Pytheas:B:10 Lykourgos:N:10:80" },
            new object[] { 7, 4, true, 48, true, "Et-5", "Et+10 T-8 ID+2", "Et+12 T-3 Em-2 ID-2", "Lykourgos:A:10 Euthydemos:B:10 Mnesitheos:N:5:45" },
            new object[] { 8, 4, true, 54, false, "T-4", "T+8 Em+2 ID+2", "T+12 Em-3 ID-2", "Kephalos:A:5 Archias:B:5 Demades:N:15:60" },
            new object[] { 9, 5, false, 56, false, "Em+2", "Em+4 T+3 ID+2", "T+5 Em-4 ID-2", "Phokion:A:15 Leosthenes:B:15 Hypereides:N:10:55" },
            new object[] { 10, 5, false, 66, true, "Et-4", "Em+3 ID+3", "Et+4 Em-3 ID-3", "Hypereides:A:10 Demades:B:15 Euthydemos:N:10:60" },
            new object[] { 11, 6, false, 56, true, "Em+2", "Em+4 ID+3", "T+3 Em-3 ID-3", "Phokion:A:15 Agnonides:B:10 Pytheas:N:5:50" },
            new object[] { 12, 6, false, 57, true, "T+4 Em+2", "T-8 Em+2 ID+4", "T+12 Em-3 ID-4", "Hypereides:A:10 Euthydemos:B:10 Demades:N:15:65" },
            new object[] { 13, 7, false, 60, true, "Et-3 Em+2", "Em+4 T+4 ID+3", "Et+5 Em-4 ID-2", "Lykourgos:A:10 Stratokles:B:10 Kephisodoros:N:5:50" },
            new object[] { 14, 7, false, 55, false, "Em+2", "Em+2 Et-3 ID+3", "Et+3 Em-2 ID-2", "Aristonikos:A:5 Deinarchos:B:10 Demosthenes:N:15:55" },
            new object[] { 15, 8, false, 63, true, "Et+3 Ep-3", "Em+2 ID+2", "Et+5 Em-3 ID-2", "Hypereides:A:10 Phokion:B:15 Demades:N:15:70" },
            new object[] { 16, 8, false, 53, false, "Ep-3", "Ep+5 Em+2 T-4 ID+2", "T+5 Ep-3 Em-2 ID-2", "Timokrates:A:5 Archias:B:5 Lykourgos:N:10:50" },
            new object[] { 17, 9, false, 56, true, "Em+2", "Em+3 T-3 ID+3", "T+5 Em-3 ID-2", "Thrasyllos:A:5 Lykourgos:A:10 Demades:B:15 Kephalos:N:5:40" },
            new object[] { 18, 9, false, 54, true, "Em+2", "Em+2 ID+2", "T+2 Em-2 ID-2", "Demosthenes:A:15 Polyeuktos:B:10 Eurymedon:B:5 Phokion:N:15:75" },
            new object[] { 19, 10, false, 47, true, "Et-5 Em+2", "Et+6 Em+2 ID+3", "T+4 Em-3 ID-4", "Lykourgos:A:10 Agnonides:A:10 Pytheas:B:5 Stratokles:B:10 Demades:N:15:70" },
            new object[] { 20, 10, false, 53, true, "Em+2 Et+2 Ep+2", "Em+4 ID+4", "Et+4 T+4 Em-4 ID-5", "Hypereides:A:10 Demosthenes:A:15 Lykourgos:A:10 Demades:B:15 Polyeuktos:B:10 Euthydemos:B:10 Phokion:N:15:85" },
        };

        [TestCaseSource(nameof(Table))] // Β9
        public void B9_CaseNumbers(object[] row)
        {
            int id = (int)row[0];
            var c = TestWorld.Case(id);
            Assert.AreEqual((int)row[1], c.prytany, "prytany");
            Assert.AreEqual((bool)row[2], c.inCouncil, "inCouncil (Βουλή/Πνύκα)");
            Assert.AreEqual((int)row[3], c.baseSupport, "baseSupport");
            Assert.AreEqual((bool)row[4], c.emergencyB, "emergencyB");

            AssertDeltas("sensitivities", (string)row[5], c.sensitivities.Select(s => (s.metric, s.perTen)));
            AssertDeltas("effectsA", (string)row[6], c.effectsA.Select(e => (e.metric, e.amount)));
            AssertDeltas("effectsB", (string)row[7], c.effectsB.Select(e => (e.metric, e.amount)));

            var expectedOfficials = ((string)row[8]).Split(' ').Select(ParseOfficial).ToList();
            Assert.AreEqual(expectedOfficials.Count, c.officials.Count, "πλήθος αξιωματούχων");
            foreach (var e in expectedOfficials)
            {
                var stance = c.officials.FirstOrDefault(s => s.official != null && s.official.name == "Official_" + e.key);
                Assert.IsNotNull(stance, "λείπει ο " + e.key);
                Assert.AreEqual(e.side, stance.side, e.key + ".side");
                Assert.AreEqual(e.weight, stance.weight, e.key + ".weight");
                if (e.side == Side.None) Assert.AreEqual(e.threshold, stance.threshold, e.key + ".threshold");
            }
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

        static (string key, Side side, int weight, int threshold) ParseOfficial(string token)
        {
            var parts = token.Split(':');
            var side = parts[1] == "A" ? Side.A : parts[1] == "B" ? Side.B : Side.None;
            return (parts[0], side, int.Parse(parts[2]), parts.Length > 3 ? int.Parse(parts[3]) : 0);
        }
    }
}
