using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;

namespace DemocracyWay.NewUI.Tests
{
    /// <summary>Βήμα 6δ: οι επιλογές της βιογραφίας χωρούν στις θέσεις της σκηνής (χωρίς κλώνους στο runtime)
    /// και τα κείμενα βιογραφίας/σύνοψης/προφίλ υπάρχουν στο ContentLibrary.</summary>
    public sealed class BiographySceneTests
    {
        static int SceneSlots(string scene, string screen) =>
            Regex.Matches(File.ReadAllText(scene), @"key: " + Regex.Escape(screen) + @"\.option\.(\d+)\r?\n")
                .Cast<Match>().Select(m => m.Groups[1].Value).Distinct().Count();

        [Test] // Το μεγαλύτερο βήμα (η φυλή) θέλει 10 θέσεις· κανένα δεν πρέπει να ξεπερνά τη σκηνή.
        public void EveryBiographyStepFitsTheSceneSlots()
        {
            var media = AssetDatabase.LoadAssetAtPath<GameConfiguration>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:GameConfiguration").First()));
            var config = JsonContent.Configuration(media);
            foreach (var step in config.biography)
            {
                string screen = step.kind == BiographyStepKind.Family ? "bio-family" : step.kind == BiographyStepKind.Oath ? "bio-oath" : "bio-choice";
                int slots = SceneSlots("Assets/Scenes/Introduction.unity", screen);
                int max = 0;
                for (int tribe = 0; tribe < config.tribes.Count; tribe++)
                    for (int cls = 0; cls < config.economicClasses.Count; cls++)
                        foreach (var trittys in JsonContent.Tribes[tribe].trittyes.Select(t => t.id))
                            max = Math.Max(max, step.choices.Count(c => (c.requireTribe < 0 || c.requireTribe == tribe) && (c.requireClass < 0 || c.requireClass == cls)
                                && (string.IsNullOrEmpty(c.requireTrittys) || c.requireTrittys == trittys)));
                Assert.LessOrEqual(max, slots, step.id + " στο " + screen);
            }
        }

        [Test] // Τα πάνελ «Ο πολίτης σου» και ταυτότητας είναι αντικείμενα σκηνής στις τρεις οθόνες της βιογραφίας.
        public void BiographyPanelsAreBoundInTheScene()
        {
            string scene = File.ReadAllText("Assets/Scenes/Introduction.unity");
            foreach (var screen in new[] { "bio-family", "bio-choice", "bio-oath" })
            {
                StringAssert.Contains("key: " + screen + ".citizen-identity", scene);
                StringAssert.Contains("key: " + screen + ".citizen-metrics", scene);
            }
        }

        [Test]
        public void EveryBioTextExists()
        {
            var type = Type.GetType("DemocracyWay.NewUI.BioText, Assembly-CSharp");
            Assert.IsNotNull(type);
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral).ToArray();
            Assert.Greater(fields.Length, 0);
            foreach (var f in fields)
                Assert.DoesNotThrow(() => JsonContent.Copy((string)f.GetRawConstantValue()), f.Name);
        }
    }
}
