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

        // Β8/Β9 — ο πλήρης πίνακας αριθμών των 20 υποθέσεων.
        // Μορφή: id, prytany, base, emergencyB, "effectsA", "effectsB", "officials"
        // effects: "Em+3 Et-2" · officials: "Thrasyllos:A Kephalos:N"
        static readonly object[][] Table =
        {
            new object[] { 1, 1, 60, false, "Em+2 ID+2", "Em-2 ID-2", "Thrasyllos:A Archias:B Kephalos:N" },
            new object[] { 2, 1, 61, true, "Ep+12 T-10 ID+2", "Ep+8 Em-3 ID-2", "Timokrates:A Demades:B Aristonikos:N" },
            new object[] { 3, 2, 51, true, "Em+3 Et-3 ID+3", "Et+5 Em-3 ID-2", "Hypereides:A Phokion:B Aristonikos:N" },
            new object[] { 4, 2, 56, false, "Em+2 ID+3", "T+3 Em-2 ID-3", "Kephisodoros:A Demades:B Kephalos:N" },
            new object[] { 5, 3, 58, false, "Em+2 ID+2", "Em-1 ID-2", "Agnonides:A Eurymedon:B Pytheas:N" },
            new object[] { 6, 3, 55, false, "Em+2 ID+3", "Et+4 T+3 Em-2 ID-3", "Mnesitheos:A Pytheas:B Lykourgos:N" },
            new object[] { 7, 4, 48, true, "Et+10 T-8 ID+2", "Et+12 T-3 Em-2 ID-2", "Lykourgos:A Euthydemos:B Mnesitheos:N" },
            new object[] { 8, 4, 54, false, "T+8 Em+2 ID+2", "T+12 Em-3 ID-2", "Kephalos:A Archias:B Demades:N" },
            new object[] { 9, 5, 56, false, "Em+4 T+3 ID+2", "T+5 Em-4 ID-2", "Phokion:A Leosthenes:B Hypereides:N" },
            new object[] { 10, 5, 66, true, "Em+3 ID+3", "Et+4 Em-3 ID-3", "Hypereides:A Demades:B Euthydemos:N" },
            new object[] { 11, 6, 56, true, "Em+4 ID+3", "T+3 Em-3 ID-3", "Phokion:A Agnonides:B Pytheas:N" },
            new object[] { 12, 6, 57, true, "T-8 Em+2 ID+4", "T+12 Em-3 ID-4", "Hypereides:A Euthydemos:B Demades:N" },
            new object[] { 13, 7, 60, true, "Em+4 T+4 ID+3", "Et+5 Em-4 ID-2", "Lykourgos:A Stratokles:B Kephisodoros:N" },
            new object[] { 14, 7, 55, false, "Em+2 Et-3 ID+3", "Et+3 Em-2 ID-2", "Aristonikos:A Deinarchos:B Demosthenes:N" },
            new object[] { 15, 8, 63, true, "Em+2 ID+2", "Et+5 Em-3 ID-2", "Hypereides:A Phokion:B Demades:N" },
            new object[] { 16, 8, 53, false, "Ep+5 Em+2 T-4 ID+2", "T+5 Ep-3 Em-2 ID-2", "Timokrates:A Archias:B Lykourgos:N" },
            new object[] { 17, 9, 56, true, "Em+3 T-3 ID+3", "T+5 Em-3 ID-2", "Thrasyllos:A Lykourgos:A Demades:B Kephalos:N" },
            new object[] { 18, 9, 54, true, "Em+2 ID+2", "T+2 Em-2 ID-2", "Demosthenes:A Polyeuktos:B Eurymedon:B Phokion:N" },
            new object[] { 19, 10, 47, true, "Et+6 Em+2 ID+3", "T+4 Em-3 ID-4", "Lykourgos:A Agnonides:A Pytheas:B Stratokles:B Demades:N" },
            new object[] { 20, 10, 53, true, "Em+4 ID+4", "Et+4 T+4 Em-4 ID-5", "Hypereides:A Demosthenes:A Lykourgos:A Demades:B Polyeuktos:B Euthydemos:B Phokion:N" },
        };

        [TestCaseSource(nameof(Table))] // Β9
        public void B9_CaseNumbers(object[] row)
        {
            int id = (int)row[0];
            var c = TestWorld.Case(id);
            Assert.AreEqual((int)row[1], c.prytany, "prytany");
            Assert.AreEqual((int)row[2], c.baseSupport, "baseSupport");
            Assert.AreEqual((bool)row[3], c.emergencyB, "emergencyB");

            AssertDeltas("effectsA", (string)row[4], c.effectsA.Select(e => (e.metric, e.amount)));
            AssertDeltas("effectsB", (string)row[5], c.effectsB.Select(e => (e.metric, e.amount)));

            var expectedOfficials = ((string)row[6]).Split(' ').Select(ParseOfficial).ToList();
            Assert.AreEqual(expectedOfficials.Count, c.officials.Count, "πλήθος αξιωματούχων");
            foreach (var e in expectedOfficials)
            {
                var stance = c.officials.FirstOrDefault(s => s.official != null && s.official.name == "Official_" + e.key);
                Assert.IsNotNull(stance, "λείπει ο " + e.key);
                Assert.AreEqual(e.side, stance.side, e.key + ".side");
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

        static (string key, Side side) ParseOfficial(string token)
        {
            var parts = token.Split(':');
            return (parts[0], parts[1] == "A" ? Side.A : parts[1] == "B" ? Side.B : Side.None);
        }
    }
}
