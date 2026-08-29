using System.Linq;
using DemocracyWay.Data;
using NUnit.Framework;
using UnityEngine;

namespace DemocracyWay.Tests
{
    /// <summary>
    /// The dependent creation steps (Τριττύα φιλτραρισμένη από Φυλή, Επάγγελμα
    /// από Τριττύα) trust these filters blindly: a null return would throw in
    /// the UI and a leaked foreign option would let the player build an
    /// impossible citizen — both shapes are pinned here on a synthetic asset.
    /// </summary>
    public class CreationDatabaseTests
    {
        private CreationDatabase _db;

        [SetUp]
        public void SetUp()
        {
            _db = ScriptableObject.CreateInstance<CreationDatabase>();

            _db.tribes.Add(new CreationOption { id = "erechtheis", title = "Ερεχθηίς" });
            _db.tribes.Add(new CreationOption { id = "aigeis", title = "Αιγηίς" });
            _db.tribes.Add(new CreationOption { id = "pandionis", title = "Πανδιονίς" });

            // Trittys ids follow the real convention {tribe}_{zone} — the
            // profession filter derives the zone from the last segment.
            _db.trittyes.Add(new TrittysOption { id = "erechtheis_asty", title = "Άστυ", tribeId = "erechtheis" });
            _db.trittyes.Add(new TrittysOption { id = "aigeis_paralia", title = "Παραλία", tribeId = "aigeis" });
            _db.trittyes.Add(new TrittysOption { id = "erechtheis_mesogeia", title = "Μεσογεία", tribeId = "erechtheis" });

            _db.professions.Add(new ProfessionOption { id = "kerameus", title = "Κεραμεύς", zoneId = "asty", wealthId = "thetis" });
            _db.professions.Add(new ProfessionOption { id = "emporos", title = "Έμπορος", zoneId = "asty", wealthId = "pentakosiomedimnos" });
            _db.professions.Add(new ProfessionOption { id = "halieus", title = "Αλιεύς", zoneId = "paralia", wealthId = "thetis" });
            _db.professions.Add(new ProfessionOption { id = "georgos", title = "Γεωργός", zoneId = "mesogeia", wealthId = "zeugitis" });
        }

        [TearDown]
        public void TearDown()
        {
            // CreateInstance'd ScriptableObjects leak into the editor session
            // unless destroyed explicitly.
            Object.DestroyImmediate(_db);
        }

        [Test]
        public void TrittyesFor_ReturnsOnlyTheTribesOwnTrittyes()
        {
            var result = _db.TrittyesFor("erechtheis");

            CollectionAssert.AreEqual(new[] { "erechtheis_asty", "erechtheis_mesogeia" },
                result.Select(t => t.id).ToList(),
                "Filter must keep only matching trittyes, in authored order.");
        }

        [Test]
        public void TrittyesFor_UnknownTribe_ReturnsEmptyNotNull()
        {
            var result = _db.TrittyesFor("no_such_tribe");

            Assert.IsNotNull(result, "The UI iterates the result directly — null would throw.");
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ProfessionsFor_FiltersByZoneAndWealthClass()
        {
            var result = _db.ProfessionsFor("erechtheis_asty", "thetis");

            // Not emporos (same zone, other class), not halieus (other zone).
            CollectionAssert.AreEqual(new[] { "kerameus" },
                result.Select(p => p.id).ToList());
        }

        [Test]
        public void ProfessionsFor_SameZoneOfAnyTribe_SharesTheProfessions()
        {
            // Professions are authored once per zone; every tribe's trittys of
            // that zone must resolve to the same list.
            var result = _db.ProfessionsFor("aigeis_asty", "thetis");

            CollectionAssert.AreEqual(new[] { "kerameus" },
                result.Select(p => p.id).ToList());
        }

        [Test]
        public void ProfessionsFor_UnknownTrittys_ReturnsEmptyNotNull()
        {
            var result = _db.ProfessionsFor("no_such_trittys", "thetis");

            Assert.IsNotNull(result, "The UI iterates the result directly — null would throw.");
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void TribeIds_PreservesAuthoredOrder()
        {
            // The calendar draw and the creation list both consume TribeIds;
            // reordering would silently shuffle what the author arranged.
            CollectionAssert.AreEqual(new[] { "erechtheis", "aigeis", "pandionis" },
                _db.TribeIds.ToList());
        }
    }
}
