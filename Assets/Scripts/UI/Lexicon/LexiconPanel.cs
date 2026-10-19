using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    public sealed class LexiconPanel : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public string id, title, category, text;
            public string[] sources, aliases;
        }

        [Serializable]
        public class Book
        {
            public List<Entry> gameEntries;
        }

        public TextAsset data;
        public RectTransform world, panel, funds, tools;
        public RectTransform[] sites;
        public TMP_InputField search;
        public TMP_Text body, footer;
        public ScrollRect scroll;
        public Button open, close, index, next, full;
        public Button[] tabs;
        public Graphic[] tabLines;
        public bool IsOpen => panel != null && panel.gameObject.activeSelf;

        List<Entry> entries = new List<Entry>();
        int category, selected;
        bool catalogue, fullEntry;
        void Awake()
        {
            if (data != null)
            {
                var book = JsonUtility.FromJson<Book>(data.text);
                if (book != null && book.gameEntries != null)
                    entries = book.gameEntries.OrderBy(e => e.title, StringComparer.Create(CultureInfo.GetCultureInfo("el-GR"), false)).ToList();
            }

            selected = Mathf.Max(0, entries.FindIndex(e => Normalize(e.title).StartsWith("βουλη των")));
            open.onClick.AddListener(() => SetOpen(!IsOpen));
            close.onClick.AddListener(() => SetOpen(false));
            index.onClick.AddListener(() =>
            {
                catalogue = true;
                Render();
            });
            full.onClick.AddListener(() =>
            {
                catalogue = false;
                fullEntry = !fullEntry;
                Render();
            });
            next.onClick.AddListener(Next);
            search.onValueChanged.AddListener(_ =>
            {
                catalogue = true;
                Render();
            });
            for (int i = 0; i < tabs.Length; i++)
            {
                int c = i;
                tabs[i].onClick.AddListener(() =>
                {
                    category = c;
                    catalogue = true;
                    Render();
                });
            }

            var links = body.gameObject.AddComponent<LexiconLinkHandler>();
            links.owner = this;
            SetOpen(false);
        }

        void OnDisable()
        {
            if (panel != null)
                SetOpen(false);
        }

        public void SetOpen(bool value)
        {
            panel.gameObject.SetActive(value);
            float q = 1.2f, w = value ? 1060 : 1600;
            world.sizeDelta = new Vector2(w * q, 900 * q);
            RectTransform caseHeader = world.Find("Active weekly case") as RectTransform;
            if (caseHeader != null)
            {
                float width = value ? 740 : 800;
                caseHeader.anchoredPosition = new Vector2((value ? 200 : 537) * q, -144 * q);
                caseHeader.sizeDelta = new Vector2(width * q, 54 * q);
                var label = caseHeader.GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.rectTransform.sizeDelta = new Vector2((width - 24) * q, 48 * q);
            }

            funds.anchoredPosition = new Vector2((value ? 370 : 704) * q, -34 * q);
            tools.anchoredPosition = new Vector2((w - 92) * q, -238 * q);
            float[] xs = value ? new[]
            {
                .23f,
                .49f,
                .73f,
                .72f
            }

            : new[]
            {
                .24f,
                .52f,
                .76f,
                .75f
            };
            float[] ys = value ? new[]
            {
                450f,
                522f,
                297f,
                675f
            }

            : new[]
            {
                441f,
                522f,
                297f,
                648f
            };
            for (int i = 0; i < sites.Length; i++)
                sites[i].anchoredPosition = new Vector2((w * xs[i] - 110) * q, -(ys[i] - 88) * q);
            if (value)
                Render();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        public void SelectLink(string id)
        {
            int i;
            if (int.TryParse(id, out i) && i >= 0 && i < entries.Count)
            {
                selected = i;
                category = Group(entries[i]);
                catalogue = false;
                fullEntry = false;
                Render();
            }
        }

        static string Normalize(string value)
        {
            var b = new StringBuilder();
            foreach (char c in (value ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    b.Append(c);
            return b.ToString().Normalize();
        }

        static string Safe(string s) => (s ?? "").Replace("<", "‹").Replace(">", "›");
        int Group(Entry e)
        {
            string c = Normalize(e.category);
            if (c.Contains("διαδικασ") || c.Contains("ελεγχ") || c.Contains("δικαι") || c.Contains("πολιτοτητα"))
                return 2;
            if (c.Contains("αξιωμα"))
                return 1;
            return 0;
        }

        List<int> Matches()
        {
            string query = Normalize(search.text);
            return Enumerable.Range(0, entries.Count).Where(i => Group(entries[i]) == category && (query.Length == 0 || Normalize(entries[i].title + " " + string.Join(" ", entries[i].aliases ?? Array.Empty<string>())).Contains(query))).ToList();
        }

        void Next()
        {
            var list = Matches();
            if (list.Count == 0)
                return;
            selected = list[(list.IndexOf(selected) + 1) % list.Count];
            catalogue = false;
            Render();
        }

        public void Render()
        {
            if (footer != null) footer.gameObject.SetActive(false);
            if (entries.Count == 0)
            {
                body.text = "Δεν βρέθηκαν λήμματα.";
                return;
            }

            if (!catalogue)
                category = Group(entries[selected]);
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i].GetComponentInChildren<TMP_Text>().color = i == category ? new Color32(36, 60, 61, 255) : new Color32(117, 110, 86, 255);
                tabLines[i].enabled = i == category;
            }

            var list = Matches();
            var b = new StringBuilder();
            if (catalogue)
            {
                b.Append("<size=28>" + (category == 0 ? "Θεσμοί και έννοιες" : category == 1 ? "Αξιώματα" : "Διαδικασίες") + "</size>\n\n");
                foreach (int i in list)
                    b.Append("<link=\"" + i + "\"><color=#34564B>" + Safe(entries[i].title) + "  ›</color></link>\n<size=14>" + Safe(entries[i].category) + "</size>\n\n");
                if (list.Count == 0)
                    b.Append("Δεν βρέθηκαν λήμματα. Δοκίμασε άλλη λέξη ή κατηγορία.");
                footer.text = list.Count + " λήμματα";
            }
            else
            {
                Entry e = entries[selected];
                string title = Safe(e.title);
                bool boule = Normalize(e.title).StartsWith("βουλη των");
                if (boule)
                    title = "Βουλή των\nΠεντακοσίων";
                b.Append("<size=34.8>" + title + "</size>\n\n<color=#AD9869>────────────────────────</color>\n\n");
                if (fullEntry)
                    b.Append(Safe(e.text));
                else if (boule)
                    b.Append("Η Βουλή αποτελείται από 500 πολίτες, πενήντα από κάθε φυλή, που υπηρετούν για ένα έτος.\n\nΠροετοιμάζει τα ζητήματα που θα συζητήσει η Εκκλησία του Δήμου και ασκεί αρμοδιότητες εποπτείας της διοίκησης.\n\n<size=16.8><color=#73815A>ΣΤΗ ΔΙΚΗ ΣΟΥ ΥΠΟΘΕΣΗ</color></size>\n\nΩς βουλευτής μπορείς να ζητήσεις στοιχεία, να μιλήσεις και να ψηφίσεις. Η απόφαση παραμένει συλλογική.\n\n");
                else
                    b.Append(Safe(e.text.Split(new[] { "\n\n" }, StringSplitOptions.None)[0]));
                // Bibliographic provenance remains in the data and research audit, outside the reader UI.
                footer.text = e.title.Substring(0, 1) + " · " + (category == 0 ? "Θεσμοί" : category == 1 ? "Αξιώματα" : "Διαδικασίες");
            }

            full.GetComponentInChildren<TMP_Text>().text = fullEntry ? "Σύνοψη ‹" : "Πλήρες λήμμα ›";
            body.text = b.ToString();
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1;
        }
    }
}
