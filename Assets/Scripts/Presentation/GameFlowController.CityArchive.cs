using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Οι οθόνες της πόλης στη ροή του χάρτη: η Αγορά ξεκινά την υπόθεση της εβδομάδας και δείχνει την
    /// πλήρη είδηση· οι Αξιωματούχοι δείχνουν τις θέσεις της εβδομάδας και τον κατάλογο αξιωμάτων.
    /// Οι κινήσεις (λογογράφος, πειθώ/δωροδοκία, κατηγορία) συνδέονται στο βήμα 5 του Agora_FINAL_DESIGN.
    /// </summary>
    public sealed partial class GameFlowController
    {
        int marketSelected;
        readonly List<Button> marketRowButtons = new List<Button>();
        ScrollRect marketScroll;
        int selectedOfficeIndex = -1;
        readonly List<Button> officeRowButtons = new List<Button>();
        bool officialDetailCase;           // το official-detail δείχνει πρόσωπο της τρέχουσας υπόθεσης
        int selectedCaseStance = -1;       // δείκτης στη λίστα στάσεων της υπόθεσης
        int[] weeklyStanceRows = System.Array.Empty<int>();   // offices.row.i → δείκτης στάσης

        static bool IsCityScreen(string id) =>
            id == "market" || id == "logographers" || id == "complaints" || id == "complaint-detail" || id == "offices" || id == "official-detail";

        void WireCityButtons()
        {
            Bind("map.market", OpenMarket);
            Bind("map.speech", () => Show("logographers"));
            Bind("map.complaint", () => Show("complaints"));
            Bind("map.offices", () => Show("offices"));
            foreach (string id in new[] { "market", "logographers", "complaints", "offices" })
                Bind(id + ".close", () => Show("map-stela"));
            Bind("complaint-detail.close", () => Show("complaints"));
            Bind("complaint-detail.back", () => Show("complaints"));
            Bind("official-detail.close", () => Show("offices"));
            Bind("official-detail.back", () => Show("offices"));
            for (int i = 0; i < 4; i++)
            {
                int n = i;
                Bind("market.row." + i, () => SelectMarketRow(n));
            }
            for (int i = 0; i < 6; i++)
            {
                int n = i;
                Bind("offices.row." + i, () => OpenOfficeDetail(n));
            }
        }

        void RenderCity()
        {
            string id = currentScreen;
            if (id == "market") RenderMarketArchive();
            else if (id == "logographers") RenderLogographerRoster();
            else if (id == "complaints") RenderComplaints();
            else if (id == "complaint-detail") Show("complaints");
            else if (id == "offices") RenderOfficesCatalogue();
            else if (id == "official-detail") RenderOfficeDetail();
        }

        // ---------- Αγορά: αρχείο ειδήσεων ----------

        /// <summary>Η επίσκεψη καταγράφεται στο save ΚΑΙ ξεκινά την υπόθεση της εβδομάδας:
        /// από εδώ και πέρα Αξιωματούχοι και Λογογράφοι βλέπουν τις ζωντανές στάσεις.</summary>
        void OpenMarket()
        {
            EnsureState();
            EnsureAgoraReady();
            MutateAndSave(() =>
            {
                State.marketVisitedTurn = CampaignRules.Turn(State);
                EnsureAgoraCaseStarted();
            });
            // Η Αγορά ανοίγει πάντα στη φρέσκια είδηση της εβδομάδας, με τη λίστα στην κορυφή.
            marketSelected = Mathf.Clamp(CampaignRules.Turn(State), 0, AgoraContent.cases.Count - 1);
            Show("market");
            if (marketScroll != null) marketScroll.verticalNormalizedPosition = 1f;
        }

        int MarketAvailableCases => Mathf.Clamp(CampaignRules.Turn(State) + 1, 1, AgoraContent.cases.Count);

        void SelectMarketRow(int row)
        {
            marketSelected = Mathf.Max(0, MarketAvailableCases - 1 - row);   // νεότερες πρώτες
            RenderCity();
        }

        const float MarketRowPitch = 140.4f;

        /// <summary>Η σκηνή έχει 4 σειρές ειδήσεων με σελιδοποίηση· εδώ μπαίνουν σε κυλιόμενη λίστα
        /// στην ίδια θέση και κλωνοποιούνται όσες ακόμη χρειάζονται (ίδιο μοτίβο με EnsureOfficeRows).</summary>
        void EnsureMarketRows(int needed)
        {
            if (marketRowButtons.Count == 0)
                for (int i = 0; i < 4; i++)
                {
                    var row = Get<Button>("market.row." + i);
                    if (row != null) marketRowButtons.Add(row);
                }
            if (marketRowButtons.Count == 0) return;
            if (marketScroll == null)
            {
                var parent = marketRowButtons[0].transform.parent;
                marketScroll = CivicUIFactory.Scroll(parent, "News scroll", out var listContent, framed: false);
                marketScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;   // ράγα μόνο όταν χρειάζεται κύλιση
                var rect = (RectTransform)marketScroll.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                // Το viewport έχει περιθώριο 12 γύρω του: έτσι οι σειρές μένουν ακριβώς όπου ήταν,
                // και η ράγα κύλισης στέκεται πριν τη διαχωριστική γραμμή της στήλης (x 604.8).
                rect.anchoredPosition = new Vector2(-12f, -169.2f + 12f);
                rect.sizeDelta = new Vector2(600f, 741.6f - 169.2f + 12f);
                foreach (var row in marketRowButtons) row.transform.SetParent(listContent, false);
            }
            var template = marketRowButtons[0];
            while (marketRowButtons.Count < needed)
            {
                int row = marketRowButtons.Count;
                var clone = Instantiate(template.gameObject, template.transform.parent).GetComponent<Button>();
                clone.name = "News " + row;
                clone.onClick.RemoveAllListeners();
                clone.onClick.AddListener(() => { PlayClick(); SelectMarketRow(row); });
                marketRowButtons.Add(clone);
            }
            for (int i = 0; i < marketRowButtons.Count; i++)
            {
                var r = (RectTransform)marketRowButtons[i].transform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0f, 1f);
                r.anchoredPosition = new Vector2(0f, -i * MarketRowPitch);
            }
            marketScroll.content.sizeDelta = new Vector2(0f, Mathf.Max(1, needed) * MarketRowPitch);
        }

        /// <summary>Τα κείμενα μιας σειράς (date/title/meta): δεμένα για τις 4 της σκηνής, με όνομα για τους κλώνους.</summary>
        void SetMarketRowText(int row, string part, string text)
        {
            TMP_Text label = row < 4 ? Get<TMP_Text>("market." + part + "." + row) : null;
            if (label == null)
                foreach (Transform child in marketRowButtons[row].transform)
                    if (child.name.StartsWith("market." + part + ".", System.StringComparison.Ordinal)) { label = child.GetComponent<TMP_Text>(); break; }
            if (label == null) return;
            label.richText = false;
            label.text = text ?? "";
        }

        void RenderMarketArchive()
        {
            EnsureAgoraReady();
            int available = MarketAvailableCases;
            marketSelected = Mathf.Clamp(marketSelected, 0, available - 1);
            CampaignText("heading", "Η Αγορά — Ανταποκρίσεις");
            CampaignText("page-heading", "Αγορά");
            EnsureMarketRows(available);
            for (int i = 0; i < marketRowButtons.Count; i++)
            {
                int index = available - 1 - i;   // νεότερες πρώτες
                bool exists = index >= 0;
                marketRowButtons[i].gameObject.SetActive(exists);
                if (!exists) continue;
                var c = AgoraContent.cases[index];
                SetMarketRowText(i, "title", c.title);
                SetMarketRowText(i, "date", "Πρυτανεία " + (index / CampaignRules.CasesPerPrytany + 1) + " · Εβδομάδα " + (index % CampaignRules.CasesPerPrytany + 1));
                SetMarketRowText(i, "meta", c.venue == Agora.CaseVenue.Council ? "Βουλευτήριο" : "Πνύκα");
                CivicUIFactory.PaintSelectable(marketRowButtons[i], index == marketSelected);
            }
            var selected = AgoraContent.cases[Mathf.Clamp(marketSelected, 0, available - 1)];
            CampaignText("kind", selected.venue == Agora.CaseVenue.Council ? "Βουλευτήριο" : "Πνύκα");
            WireLexiconTerms("market.body");
            var lexiconWords = lexicon != null ? lexicon.content : null;
            SetRichText("market.body", LexiconMarkup.Decorate(selected.NewsFor(State.polis.war), lexiconWords)
                + "\n\n<b>" + JsonContent.Copy("Copy_5f91d2c7a43e80b6") + "</b>\n" + LexiconMarkup.Decorate(selected.ek1, lexiconWords));
            CampaignText("quote", MarketOption(Agora.Side.A, selected) + "\n\n" + MarketOption(Agora.Side.B, selected));
            CampaignText("after", MarketStancesText(selected));
            CampaignText("unread", "");
            StackMarketColumn();
            SetActive("market.read", false);   // η σελιδοποίηση αντικαταστάθηκε από κύλιση
        }

        /// <summary>Κείμενο-παιδί ενός δεμένου αντικειμένου, με το όνομά του στη σκηνή (π.χ. κάρτες με Name/Price).</summary>
        TMP_Text ChildText(string key, string child)
        {
            var owner = Get<GameObject>(key);
            var t = owner != null ? owner.transform.Find(child) : null;
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }

        void SetChildText(string key, string child, string text)
        {
            var label = ChildText(key, child);
            if (label != null) label.text = text ?? "";
        }

        /// <summary>Κείμενο-αδελφάκι ενός δεμένου αντικειμένου (π.χ. ο «Speaker» δίπλα στο argument της κάρτας).</summary>
        void SetSiblingText(string key, string sibling, string text)
        {
            var owner = Get<GameObject>(key);
            var t = owner != null && owner.transform.parent != null ? owner.transform.parent.Find(sibling) : null;
            var label = t != null ? t.GetComponent<TMP_Text>() : null;
            if (label != null) label.text = text ?? "";
        }

        /// <summary>Στοιβάζει κατακόρυφα τα κείμενα της είδησης (σώμα, προτάσεις, στάσεις, καταγγελία)
        /// ώστε να μην πέφτει το ένα πάνω στο άλλο — τα κουτιά της σκηνής ήταν για πολύ μικρότερο περιεχόμενο.</summary>
        void StackMarketColumn()
        {
            var body = Get<TMP_Text>("market.body");
            var quote = Get<TMP_Text>("market.quote");
            var after = Get<TMP_Text>("market.after");
            var unread = Get<TMP_Text>("market.unread");
            if (body == null || quote == null || after == null) return;
            var quotePanel = quote.rectTransform.parent as RectTransform;
            // topY: αμέσως κάτω από τον τίτλο (heading: y 181.2 + ύψος κειμένου ~65) — το 308 άφηνε
            // μεγάλο κενό ΚΑΙ στρίμωχνε τη στήλη τόσο που το πλαίσιο των προτάσεων έχανε τον Β ομιλητή.
            const float left = 628.8f, width = 936f, topY = 252f, bottomY = 724f, gap = 14f;
            const float quotePad = 56f, quoteInset = 52.8f;
            var labels = new[] { body, quote, after, unread };
            var heights = new float[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                var label = labels[i];
                if (label == null || string.IsNullOrEmpty(label.text)) continue;
                label.textWrappingMode = TextWrappingModes.Normal;
                // Σταθερό, ευανάγνωστο μέγεθος — το ύψος του κουτιού ακολουθεί το κείμενο.
                label.enableAutoSizing = false;
                label.fontSize = i == 0 ? 23f : 21f;
                float w = label == quote ? width - quoteInset : width;
                // Το πλαίσιο προτάσεων παίρνει έξτρα buffer ώστε να μη χάνεται η τελευταία γραμμή (Β ομιλητής).
                heights[i] = label.GetPreferredValues(label.text, w, 0).y + (label == quote ? 18f : 6f);
            }
            float total = heights[0] + heights[1] + heights[2] + heights[3] + (heights[1] > 0 ? quotePad : 0) + 3 * gap;
            float available = bottomY - topY;
            float squeeze = total > available ? available / total : 1f;
            for (int i = 0; i < labels.Length; i++)
            {
                var label = labels[i];
                if (label == null || heights[i] <= 0) continue;
                heights[i] *= squeeze;
                // Αν παρ' όλα αυτά δεν χωρά, η γραμματοσειρά μικραίνει αυτόματα — αλλά όχι κάτω από 18.
                label.enableAutoSizing = squeeze < 1f;
                if (squeeze < 1f) { label.fontSizeMin = 18f; label.fontSizeMax = i == 0 ? 23f : 21f; }
            }
            float y = topY;
            void Place(RectTransform rect, float height)
            {
                rect.anchoredPosition = new Vector2(left, -y);
                rect.sizeDelta = new Vector2(width, height);
                y += height + gap;
            }
            Place(body.rectTransform, heights[0]);
            if (quotePanel != null)
            {
                quotePanel.gameObject.SetActive(heights[1] > 0);
                if (heights[1] > 0)
                {
                    Place(quotePanel, heights[1] + quotePad);
                    quote.rectTransform.anchoredPosition = new Vector2(26.4f, -20.4f);
                    quote.rectTransform.sizeDelta = new Vector2(width - quoteInset, heights[1]);
                }
            }
            Place(after.rectTransform, Mathf.Max(heights[2], 1f));
            if (unread != null)
            {
                unread.gameObject.SetActive(heights[3] > 0);
                if (heights[3] > 0) Place(unread.rectTransform, heights[3]);
            }
        }

        /// <summary>Μία φορά ανά κείμενο: ο χειριστής κλικ που ανοίγει το ευρετήριο στη λέξη που πατήθηκε.
        /// AddComponent χειριστή σε υπάρχον αντικείμενο σκηνής — όχι δημιουργία UI (ίδιο μοτίβο με LexiconPanel).</summary>
        void WireLexiconTerms(string key)
        {
            var label = Get<TMP_Text>(key);
            if (label == null || lexicon == null)
                return;
            if (label.GetComponent<LexiconTermClicks>() == null)
                label.gameObject.AddComponent<LexiconTermClicks>().owner = lexicon;
        }

        /// <summary>Μία πρόταση (Α ή Β): περίληψη από το asset + ομιλητής, όπως θα εμφανιστεί στους λόγους.</summary>
        string MarketOption(Agora.Side side, Agora.CaseData c)
        {
            var speaker = (side == Agora.Side.A ? c.speechA : c.speechB).speaker;
            string summary = side == Agora.Side.A ? c.optionA : c.optionB;
            string line = JsonContent.Copy(side == Agora.Side.A ? "Copy_d94a07c2e6f51b38" : "Copy_e2b58d14f93c07a6");
            if (!string.IsNullOrEmpty(summary))
                line += " — " + summary;
            if (speaker != null)
                line += "\n" + JsonContent.Copy("Copy_f7c30a96b24d18e5") + speaker.FullName;
            return line;
        }

        /// <summary>Ποιοι αξιωματούχοι στηρίζουν ποια θέση στην υπόθεση.</summary>
        string MarketStancesText(Agora.CaseData c)
        {
            var stances = Agora.AssemblyRules.CloneStances(c);
            string Names(Agora.Side side) => string.Join(" · ",
                stances.Where(s => s != null && s.official != null && s.side == side).Select(s => s.official.FullName));
            var lines = new List<string>();
            string forA = Names(Agora.Side.A), forB = Names(Agora.Side.B), neutral = Names(Agora.Side.None);
            if (forA.Length > 0) lines.Add(JsonContent.Copy("Copy_07d2b94e18f5c3a6") + forA);
            if (forB.Length > 0) lines.Add(JsonContent.Copy("Copy_18e3ca05f2964db7") + forB);
            if (neutral.Length > 0) lines.Add(JsonContent.Copy("Copy_29f4db16a3075ec8") + neutral);
            return string.Join("\n", lines);
        }

        // ---------- Λογογράφοι και Καταγγελίες: οι κινήσεις συνδέονται στο βήμα 5 ----------

        void RenderLogographerRoster()
        {
            CampaignText("heading", "Λογογράφοι της πόλης");
            CampaignText("status", "");
            for (int i = 0; i < 3; i++)
            {
                SetActive("logographers.card." + i, false);
                SetActive("logographers.buy." + i, false);
            }
        }

        void RenderComplaints()
        {
            CampaignText("page-heading", "Καταγγελίες");
            SetText("complaints.banner", "Οι καταγγελίες σου");
            CampaignText("heading", "");
            CampaignText("state", "");
            CampaignText("body", "");
            SetActive("complaints.file", false);
            SetActive("complaints.primary", false);
            for (int i = 0; i < 3; i++) SetActive("complaints.row." + i, false);
        }

        // ---------- Αξιωματούχοι: πρώτα οι φωνές της εβδομάδας, μετά ολόκληρος ο κατάλογος ----------

        Agora.OfficeData[] AllOffices() => AgoraContent.offices.Where(o => o != null).ToArray();

        /// <summary>Η σκηνή έχει 6 κάρτες — για τον πλήρη κατάλογο κλωνοποιούνται στο ίδιο πλέγμα
        /// (ίδιο μοτίβο με το SetupEvaluationUI: επέκταση υπάρχοντος προτύπου, όχι νέο UI).</summary>
        void EnsureOfficeRows(int needed)
        {
            if (officeRowButtons.Count == 0)
                for (int i = 0; i < 6; i++)
                {
                    var existing = Get<Button>("offices.row." + i);
                    if (existing != null) officeRowButtons.Add(existing);
                }
            if (officeRowButtons.Count == 0) return;
            var template = officeRowButtons[0];
            while (officeRowButtons.Count < needed)
            {
                int index = officeRowButtons.Count;
                var clone = Instantiate(template.gameObject, template.transform.parent).GetComponent<Button>();
                clone.name = "Official " + index;
                clone.onClick.RemoveAllListeners();
                clone.onClick.AddListener(() => { PlayClick(); OpenOfficeDetail(index); });
                targets["offices.row." + index] = clone;
                officeRowButtons.Add(clone);
            }
            // Πλέγμα 3 στηλών, όπως οι αυθεντικές κάρτες, και ύψος περιεχομένου για το scroll.
            for (int i = 0; i < officeRowButtons.Count; i++)
            {
                var rect = (RectTransform)officeRowButtons[i].transform;
                rect.anchoredPosition = new Vector2(i % 3 * 520.8f, -(i / 3) * 372f);
            }
            var content = (RectTransform)template.transform.parent;
            content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(720f, Mathf.CeilToInt(needed / 3f) * 372f + 12f));
        }

        /// <summary>Οι θέσεις των αξιωματούχων στην υπόθεση της εβδομάδας.</summary>
        List<Agora.OfficialStance> WeeklyStances(Agora.CaseData c) => Agora.AssemblyRules.CloneStances(c);

        static string StanceLine(Agora.OfficialStance s) =>
            s.side == Agora.Side.None ? "αναποφάσιστος" : "υπέρ της " + SideLabel(s.side);

        void RenderOfficesCatalogue()
        {
            EnsureAgoraReady();
            var c = CurrentAgoraCase;
            var stances = WeeklyStances(c);
            weeklyStanceRows = Enumerable.Range(0, stances.Count)
                .Where(i => stances[i] != null && stances[i].official != null).ToArray();
            var offices = AllOffices();
            CampaignText("heading", "Αξιωματούχοι");
            var heading = Get<TMP_Text>("offices.heading");
            if (heading != null) heading.raycastTarget = false;
            var close = Get<Button>("offices.close");
            if (close != null) close.transform.SetAsLastSibling();   // πάνω από ό,τι κι αν το σκεπάζει
            // Χωρίς φίλτρα και αναζήτηση: ο κατάλογος φαίνεται ολόκληρος με κύλιση.
            for (int i = 0; i < 4; i++) SetActive("offices.filter." + i, false);
            SetActive("offices.search", false);
            // Η κύλιση ανεβαίνει στη θέση των κρυμμένων φίλτρων (από -192 σε -100) και ψηλώνει ανάλογα.
            var row0 = Get<GameObject>("offices.row.0");
            var viewport = row0 != null ? row0.transform.parent.parent as RectTransform : null;
            var scroll = viewport != null ? viewport.parent as RectTransform : null;
            if (scroll != null && viewport != null)
            {
                scroll.anchoredPosition = new Vector2(scroll.anchoredPosition.x, -100f);
                scroll.sizeDelta = new Vector2(scroll.sizeDelta.x, 584f);
                viewport.sizeDelta = new Vector2(viewport.sizeDelta.x, 584f);
            }
            int weekly = weeklyStanceRows.Length;
            int total = weekly + offices.Length;
            EnsureOfficeRows(total);
            for (int i = 0; i < officeRowButtons.Count; i++)
            {
                bool exists = i < total;
                SetActive("offices.row." + i, exists);
                if (!exists) continue;
                // Η κάρτα έχει δικά της παιδιά: Office banner/Office, Name, Origin, Method, Meet.
                string key = "offices.row." + i;
                if (i < weekly)
                {
                    var s = stances[weeklyStanceRows[i]];
                    SetChildText(key, "Office banner/Office", "Αυτή την εβδομάδα — " + c.title);
                    SetChildText(key, "Name", s.official.FullName);
                    SetChildText(key, "Origin", StanceLine(s));
                    SetChildText(key, "Method", s.official.office != null ? SelectionLabel(s.official.office.selection) : "Χωρίς αξίωμα");
                    SetChildText(key, "Meet", "Γνωριμία ›");
                }
                else
                {
                    var office = offices[i - weekly];
                    var persons = AgoraContent.officials.Where(p => p != null && p.office == office).Select(p => p.FullName).ToArray();
                    SetChildText(key, "Office banner/Office", SelectionLabel(office.selection));
                    SetChildText(key, "Name", office.displayNameWithCount);
                    SetChildText(key, "Origin", persons.Length > 0 ? string.Join(" · ", persons) : "Χωρίς επώνυμο πρόσωπο στο παιχνίδι");
                    SetChildText(key, "Method", office.payObols > 0 ? "Μισθός " + office.payObols + " οβ." : "Χωρίς μισθό");
                    SetChildText(key, "Meet", "Άνοιγμα ›");
                }
            }
        }

        void OpenOfficeDetail(int row)
        {
            int weekly = weeklyStanceRows.Length;
            if (row < weekly)
            {
                officialDetailCase = true;
                selectedCaseStance = weeklyStanceRows[row];
                Show("official-detail");
                return;
            }
            var offices = AllOffices();
            int index = row - weekly;
            if (index >= offices.Length) return;
            officialDetailCase = false;
            selectedOfficeIndex = AgoraContent.offices.IndexOf(offices[index]);
            Show("official-detail");
        }

        void RenderOfficeDetail()
        {
            if (officialDetailCase) { RenderCaseOfficialDetail(); return; }
            if (selectedOfficeIndex < 0 || selectedOfficeIndex >= AgoraContent.offices.Count) { Show("offices"); return; }
            var office = AgoraContent.offices[selectedOfficeIndex];
            var persons = AgoraContent.officials.Where(p => p != null && p.office == office).ToArray();
            CampaignText("heading", office.displayNameWithCount);
            SetOfficialBanner(office.displayName);
            // Το «office» binding είναι το label ΜΕΣΑ στην κορδέλα (168×19) — δεν χωρά περιγραφή.
            // Η περιγραφή του αξιώματος μπαίνει μαζί με το βιογραφικό στο στοιβαγμένο κουτί.
            CampaignText("name", persons.Length == 0 ? "" : "Στο παιχνίδι: " + string.Join(" · ", persons.Select(p => p.FullName)));
            string personBio = persons.Length == 0 || string.IsNullOrEmpty(persons[0].bio) ? "" : persons[0].bio;
            CampaignText("origin", string.IsNullOrEmpty(office.description) ? personBio
                : personBio.Length == 0 ? office.description : office.description + "\n\n" + personBio);
            CampaignText("method", SelectionLabel(office.selection) + (office.payObols > 0 ? " · Μισθός " + office.payObols + " οβ." : "")
                + (string.IsNullOrEmpty(office.athPolRef) ? "" : " · ΑΠ " + office.athPolRef));
            CampaignText("influence", "");
            CampaignText("relationship", "");
            CampaignText("access", "");
            CampaignText("priority", "");
            CampaignText("result", "");
            CampaignText("status", "");
            SetActive("official-detail.persuade", false);
            SetActive("official-detail.bribe", false);
            StackOfficialCard();
        }

        /// <summary>Στοιβάζει την αριστερή κάρτα του αξιωματούχου. Με τα γεμάτα πια βιογραφικά,
        /// τα σταθερά κουτιά της σκηνής (79px για το bio, κεντρική στοίχιση) υπερχείλιζαν
        /// πάνω στο πορτρέτο και στα διακοσμητικά. Το bio παίρνει ό,τι χώρο απομένει
        /// μέχρι τη γραμμή «Σχέση» και μικραίνει αυτόματα ώστε να χωρά.</summary>
        void StackOfficialCard()
        {
            // Τα κουτιά τιμών δεξιά (270×36) είναι μονόγραμμα: ό,τι δεν χωρά μικραίνει, δεν τυλίγεται.
            foreach (string key in new[] { "method", "influence", "priority", "access" })
            {
                var value = Get<TMP_Text>("official-detail." + key);
                if (value == null) continue;
                value.textWrappingMode = TextWrappingModes.NoWrap;
                value.overflowMode = TMPro.TextOverflowModes.Truncate;
                value.enableAutoSizing = true;
                value.fontSizeMin = 12f;
                value.fontSizeMax = 20f;
            }
            var nameLabel = Get<TMP_Text>("official-detail.name");
            var bio = Get<TMP_Text>("official-detail.origin");
            if (nameLabel == null || bio == null) return;
            var parent = nameLabel.rectTransform.parent;
            RectTransform banner = null;
            foreach (Transform sibling in parent)
            {
                if (sibling.name.StartsWith("Banner ", System.StringComparison.Ordinal)) banner = (RectTransform)sibling;
                else if (sibling.name == "Rule" || sibling.name == "Quote") sibling.gameObject.SetActive(false);
            }
            const float width = 403.2f, bottom = 637f, gap = 10f;
            float y = 264f;   // κάτω από το πορτρέτο, όπως στη σκηνή
            nameLabel.textWrappingMode = TextWrappingModes.Normal;
            nameLabel.enableAutoSizing = false;
            nameLabel.overflowMode = TMPro.TextOverflowModes.Truncate;
            float nameHeight = Mathf.Min(110f, nameLabel.GetPreferredValues(nameLabel.text, width, 0).y + 4f);
            nameLabel.rectTransform.anchoredPosition = new Vector2(nameLabel.rectTransform.anchoredPosition.x, -y);
            nameLabel.rectTransform.sizeDelta = new Vector2(nameLabel.rectTransform.sizeDelta.x, nameHeight);
            y += nameHeight + gap;
            if (banner != null && banner.gameObject.activeSelf)
            {
                banner.anchoredPosition = new Vector2(banner.anchoredPosition.x, -y);
                y += banner.sizeDelta.y + gap;
            }
            bio.textWrappingMode = TextWrappingModes.Normal;
            bio.verticalAlignment = TMPro.VerticalAlignmentOptions.Top;
            bio.overflowMode = TMPro.TextOverflowModes.Truncate;
            bio.enableAutoSizing = true;
            bio.fontSizeMin = 14f;
            bio.fontSizeMax = 20f;
            bio.rectTransform.anchoredPosition = new Vector2(bio.rectTransform.anchoredPosition.x, -y);
            bio.rectTransform.sizeDelta = new Vector2(bio.rectTransform.sizeDelta.x, Mathf.Max(60f, bottom - y));
        }

        /// <summary>Πρόσωπο της υπόθεσης της εβδομάδας: η θέση του και το βιογραφικό του.</summary>
        void RenderCaseOfficialDetail()
        {
            var c = CurrentAgoraCase;
            var stances = WeeklyStances(c);
            if (selectedCaseStance < 0 || selectedCaseStance >= stances.Count || stances[selectedCaseStance]?.official == null)
            { officialDetailCase = false; Show("offices"); return; }
            var s = stances[selectedCaseStance];
            var person = s.official;
            var office = person.office;
            CampaignText("heading", person.FullName);
            CampaignText("name", StanceLine(s));
            SetOfficialBanner(office != null ? office.displayName : "");
            string officeLine = office != null ? office.displayNameWithCount + " — " + office.description : "";
            CampaignText("origin", string.IsNullOrEmpty(person.bio) ? officeLine
                : officeLine.Length == 0 ? person.bio : person.bio + "\n\n" + officeLine);
            CampaignText("method", office != null ? SelectionLabel(office.selection) : "");
            CampaignText("influence", "");
            CampaignText("relationship", "");
            CampaignText("priority", StanceLine(s));
            CampaignText("access", "");
            CampaignText("result", "");
            CampaignText("status", "");
            SetActive("official-detail.persuade", false);
            SetActive("official-detail.bribe", false);
            StackOfficialCard();
        }

        /// <summary>Η αρχειοθετημένη κορδέλα δίπλα στο πορτρέτο (π.χ. «ΣΤΡΑΤΗΓΟΣ») παίρνει το σωστό αξίωμα.</summary>
        void SetOfficialBanner(string text)
        {
            var nameLabel = Get<TMP_Text>("official-detail.name");
            if (nameLabel == null) return;
            foreach (Transform sibling in nameLabel.transform.parent)
            {
                if (!sibling.name.StartsWith("Banner ", System.StringComparison.Ordinal)) continue;
                var label = sibling.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = text ?? "";
                return;
            }
        }

        static string SelectionLabel(Agora.SelectionMode mode) => mode switch
        {
            Agora.SelectionMode.Lot => "Κλήρωση",
            Agora.SelectionMode.Election => "Εκλογή",
            Agora.SelectionMode.FromCouncil => "Από τη Βουλή",
            Agora.SelectionMode.ByAge => "Κατά ηλικία",
            _ => "Ισόβιο"
        };
    }
}
