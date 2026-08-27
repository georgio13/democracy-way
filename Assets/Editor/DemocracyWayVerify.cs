#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DemocracyWay.EditorTools
{
    using DemocracyWay.Core;

    /// <summary>
    /// Post-Init sanity check: opens the generated scenes and asserts that
    /// every reference and every content list actually came through.
    ///
    /// Worth running after any Init change — an editor script can fail to
    /// assign a field silently, and the symptom (an empty option list at
    /// runtime) points nowhere near the cause.
    /// </summary>
    public static class DemocracyWayVerify
    {
        [MenuItem("Tools/DemocracyWay/Verify")]
        public static void Check()
        {
            int fail = 0;
            void Expect(bool ok, string what)
            {
                Debug.Log($"[Check] {(ok ? "PASS" : "FAIL")}  {what}");
                if (!ok) fail++;
            }

            // 1. Scene wiring: the field that was null.
            EditorSceneManager.OpenScene("Assets/Scenes/CharacterCreation.unity");
            var ctrl = Object.FindAnyObjectByType<DemocracyWay.UI.CharacterCreationController>();
            Expect(ctrl != null, "CharacterCreationController present in scene");

            var so = new SerializedObject(ctrl);
            var db = so.FindProperty("database").objectReferenceValue as CreationDatabase;
            Expect(db != null, "controller.database is wired");
            Expect(so.FindProperty("optionPrefab").objectReferenceValue != null, "controller.optionPrefab is wired");
            Expect(so.FindProperty("optionContainer").objectReferenceValue != null, "controller.optionContainer is wired");

            if (db == null) { Debug.LogError($"[Check] {fail} failure(s)"); return; }

            // 2. The step the player got stuck on.
            var profile = new CitizenProfile();
            var genders = db.OptionsFor(CreationStep.Gender, profile);
            Expect(genders.Count == 2, $"Gender step offers 2 options (got {genders.Count})");
            Expect(genders.Count > 0 && genders[0].artwork != null, "Gender option has artwork");

            // 3. Tribe -> trittys dependency.
            var tribes = db.OptionsFor(CreationStep.Tribe, profile);
            Expect(tribes.Count == 10, $"10 tribes (got {tribes.Count})");
            Expect(db.OptionsFor(CreationStep.Trittys, profile).Count == 0, "no trittyes before a tribe is chosen");

            // Everything below indexes into these lists, so bail out rather
            // than throwing an IndexOutOfRange that hides the real failures.
            if (tribes.Count < 4)
            {
                Debug.LogError($"[Check] {fail} failure(s) — too few tribes to continue.");
                return;
            }

            profile.Set(CreationStep.Tribe, tribes[0]);
            var tr = db.OptionsFor(CreationStep.Trittys, profile);
            Expect(tr.Count == 3, $"3 trittyes for {tribes[0].title} (got {tr.Count})");

            if (tr.Count > 0)
            {
                // Changing tribe must clear the trittys underneath it.
                profile.Set(CreationStep.Trittys, tr[0]);
                profile.Set(CreationStep.Tribe, tribes[3]);
                Expect(string.IsNullOrEmpty(profile.trittysId), "changing tribe clears the chosen trittys");

                var tr2 = db.OptionsFor(CreationStep.Trittys, profile);
                Expect(tr2.Count > 0 && tr2[0].parentId == tribes[3].id,
                       "trittys list follows the new tribe");
            }

            // 4. Remaining steps.
            Expect(db.OptionsFor(CreationStep.Wealth, profile).Count == 4, "4 wealth classes");
            Expect(db.OptionsFor(CreationStep.Period, profile).Count == 5, "5 periods");
            Expect(db.OptionsFor(CreationStep.Occupation, profile).Count == 10, "10 occupations");

            // 5. Every option has artwork and a description.
            int noArt = 0, noDesc = 0;
            foreach (CreationStep s in System.Enum.GetValues(typeof(CreationStep)))
                foreach (var o in db.AllFor(s))
                {
                    if (o.artwork == null) noArt++;
                    if (string.IsNullOrWhiteSpace(o.description)) noDesc++;
                }
            Expect(noArt == 0, $"every option has artwork ({noArt} missing)");
            Expect(noDesc == 0, $"every option has a description ({noDesc} missing)");

            // 6. Prytany draw.
            var sched = PrytanySchedule.Draw(db.tribes);
            Expect(sched.TotalRounds == 10, $"10 prytany rounds (got {sched.TotalRounds})");
            var seen = new System.Collections.Generic.HashSet<string>();
            bool dupes = false;
            for (int i = 0; i < sched.TotalRounds; i++) { if (!seen.Add(sched.CurrentTribeId)) dupes = true; sched.Advance(); }
            Expect(!dupes, "no tribe presides twice");
            Expect(sched.IsFinished, "year ends after the last prytany");

            // 7. Dialogue pool.
            var dlg = AssetDatabase.LoadAssetAtPath<DemocracyWay.Dialogue.DialogueDatabase>(
                "Assets/Content/DialogueDatabase.asset");
            Expect(dlg != null && dlg.Count == 8, $"8 dialogues (got {(dlg == null ? -1 : dlg.Count)})");
            Expect(dlg != null && dlg.PickRandom(null) != null, "random dialogue draws");

            // 8. Font coverage. The bundled font has no glyphs in the Greek
            //    Extended block, so any polytonic text that creeps back into
            //    the content renders as blank spaces — TMP only says so at
            //    runtime, one character at a time. Check the corpus up front.
            int badChars = CheckFontCoverage(db, dlg);
            Expect(badChars == 0, $"every content character exists in the font ({badChars} missing)");

            Debug.Log(fail == 0 ? "[Check] ALL PASSED" : $"[Check] {fail} FAILURE(S)");
        }

        /// <summary>Returns how many distinct characters used by the content are
        /// absent from the project's TMP fonts, logging each one.</summary>
        private static int CheckFontCoverage(CreationDatabase db, DemocracyWay.Dialogue.DialogueDatabase dlg)
        {
            var fonts = new System.Collections.Generic.List<TMPro.TMP_FontAsset>();
            foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/Fonts" }))
            {
                var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (font != null) fonts.Add(font);
            }
            if (fonts.Count == 0)
            {
                Debug.LogWarning("[Check] no TMP fonts under Assets/Fonts — skipping coverage check.");
                return 0;
            }

            var text = new System.Text.StringBuilder();
            if (db != null)
                foreach (CreationStep step in System.Enum.GetValues(typeof(CreationStep)))
                    foreach (var o in db.AllFor(step))
                        text.Append(o.title).Append(o.subtitle).Append(o.description);
            if (dlg != null)
                foreach (var e in dlg.Entries)
                {
                    text.Append(e.title);
                    foreach (var l in e.lines) text.Append(l.speaker).Append(l.text);
                    foreach (var c in e.choices) text.Append(c.text).Append(c.outcome);
                }

            var missing = new System.Collections.Generic.SortedSet<char>();
            foreach (char ch in text.ToString())
            {
                if (ch < 0x80 || char.IsWhiteSpace(ch)) continue;
                bool everywhere = true;
                foreach (var font in fonts)
                    if (!font.HasCharacter(ch)) { everywhere = false; break; }
                if (!everywhere) missing.Add(ch);
            }

            foreach (var ch in missing)
                Debug.LogWarning($"[Check] U+{(int)ch:X4} '{ch}' is missing from at least one font in Assets/Fonts.");

            return missing.Count;
        }
    }
}
#endif
