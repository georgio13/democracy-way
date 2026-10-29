using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace DemocracyWay.NewUI.Tests
{
    /// <summary>Βήμα 7: κάθε τέλος έχει κείμενο (Υπερείδης/Δημάδης σε δύο εκδοχές) και τα κείμενα των
    /// οθονών των τελών υπάρχουν στο ContentLibrary.</summary>
    public sealed class EndingContentTests
    {
        static Agora.GameContent Content =>
            AssetDatabase.LoadAssetAtPath<Agora.GameContent>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:GameContent").First()));

        [Test]
        public void EveryEndingHasText()
        {
            var c = Content;
            foreach (var (name, text) in new[]
            {
                ("Famine", c.endingFamine), ("PropertyLimit", c.endingPropertyLimit), ("Stasis", c.endingStasis),
                ("Counterfactual", c.endingCounterfactual), ("GarrisonOnly", c.endingGarrisonOnly),
                ("Hypereides", c.endingHypereides), ("HypereidesGarrison", c.endingHypereidesGarrison),
                ("Demades", c.endingDemades), ("DemadesGarrison", c.endingDemadesGarrison),
                ("HarpalusMan", c.endingHarpalusMan), ("Lycurgus", c.endingLycurgus),
                ("Stratocles", c.endingStratocles), ("Anonymous", c.endingAnonymous)
            })
                Assert.IsFalse(string.IsNullOrWhiteSpace(text), name);
        }

        [Test]
        public void EveryEndTextExists()
        {
            var type = Type.GetType("DemocracyWay.NewUI.EndText, Assembly-CSharp");
            Assert.IsNotNull(type);
            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral))
                Assert.DoesNotThrow(() => JsonContent.Copy((string)f.GetRawConstantValue()), f.Name);
        }
    }
}
