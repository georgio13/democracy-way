using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace DemocracyWay.NewUI.Tests
{
    /// <summary>Agora_FINAL_DESIGN 7.4 (κάρτες πρώτης επίσκεψης) και 7.5 («Πώς κρίνονται τα πράγματα»):
    /// τα λήμματα του Οδηγού στο Ευρετήριο.</summary>
    public sealed class GuideContentTests
    {
        static readonly string[] Places = { "guide_market", "guide_pnyx", "guide_boule", "guide_heliaia", "guide_areopagus", "guide_deme" };

        static LexiconContent Lexicon => AssetDatabase.LoadAssetAtPath<LexiconContent>("Assets/Content/LexiconContent.asset");

        static Agora.GameConfig Config =>
            AssetDatabase.LoadAssetAtPath<Agora.GameContent>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:GameContent").First())).config;

        static LexiconPanel.Entry Entry(string id) => Lexicon.entries.SingleOrDefault(e => e.id == id);

        [Test] // Ο Οδηγός: 6 χώροι και η σελίδα των κανόνων, όλα στην κατηγορία «Οδηγός».
        public void GuideHasSixPlacesAndTheRulesPage()
        {
            var guide = Lexicon.entries.Where(e => e.category == "Οδηγός").Select(e => e.id).OrderBy(id => id).ToArray();
            CollectionAssert.AreEqual(Places.Append("guide_rules").OrderBy(id => id).ToArray(), guide);
        }

        [Test] // Κάθε κάρτα: δύο προτάσεις για τον χώρο και μία για τον παίκτη.
        public void PlaceCardsHaveThreeSentences()
        {
            foreach (string id in Places)
            {
                var e = Entry(id);
                Assert.IsNotNull(e, id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(e.title), id);
                Assert.AreEqual(3, e.text.Count(c => c == '.'), id);
            }
        }

        [Test] // Η σελίδα των κανόνων γράφει τα ίδια όρια με το GameConfig.
        public void RulesPageMatchesGameConfig()
        {
            var cfg = Config;
            string text = Entry("guide_rules").text;
            StringAssert.Contains("Με " + cfg.passThreshold + " ή περισσότερο περνά η Α", text);
            StringAssert.Contains("κάτω από " + cfg.pressureBelow, text);
            StringAssert.Contains("Πίεση ±" + cfg.pressurePoints, text);
            StringAssert.Contains("Πόλεμος −" + cfg.warEmergencyPoints, text);
            StringAssert.Contains("Άρειος Πάγος −" + cfg.areopagusPoints, text);
            StringAssert.Contains("Δίκη ±" + cfg.trialPoints, text);
            StringAssert.Contains("Αξιωματούχος ±" + cfg.officialPoints, text);
            StringAssert.Contains("Πειθώ ÷ " + cfg.speechPeithoDivisor + ", +" + cfg.logographerPoints + " με λογογράφο", text);
            StringAssert.Contains("από −" + cfg.moodRange + " έως +" + cfg.moodRange, text);
            StringAssert.Contains("Με " + cfg.guiltyThreshold + " ή περισσότερο ο κατηγορούμενος είναι ένοχος", text);
            StringAssert.Contains("κάτω από " + cfg.sycophantBelow + ", πληρώνεις " + cfg.accuserFine + " δρ.", text);
        }
    }
}
