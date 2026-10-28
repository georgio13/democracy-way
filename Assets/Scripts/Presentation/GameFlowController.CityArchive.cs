using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Οι οθόνες της πόλης στη ροή του χάρτη: η Αγορά ξεκινά την υπόθεση της εβδομάδας και δείχνει την
    /// πλήρη είδηση, οι Λογογράφοι πουλούν λόγο (μία παραγγελία/εβδομάδα), οι Αξιωματούχοι δέχονται
    /// Πείθω/Δωροδοκώ (η ΜΙΑ ενέργεια της εβδομάδας), οι Καταγγελίες δεσμεύουν την καταγγελία για τη
    /// συνέλευση — η δίκη γίνεται ΕΚΕΙ. Κανόνες πάντα από Agora.AssemblyRules.
    /// </summary>
    public sealed partial class GameFlowController
    {
        int marketPage, marketSelected;
        int selectedOfficeIndex = -1;
        int selectedComplaintTurn = -1;
        readonly List<Button> officeRowButtons = new List<Button>();
        int logographerSelected = -1;      // 1..3 όσο διαλέγει πλευρά για παραγγελία
        bool complaintDetailCurrent;       // το complaint-detail δείχνει τη ζωντανή καταγγελία της εβδομάδας
        bool officialDetailCase;           // το official-detail δείχνει πρόσωπο της τρέχουσας υπόθεσης
        int selectedCaseStance = -1;       // δείκτης στη λίστα στάσεων της υπόθεσης
        int officialAction;                // 0 τίποτα, 1 εκκρεμεί πλευρά για Πείθω, 2 για Δωροδοκώ
        string officialFeedback = "";
        int[] weeklyStanceRows = System.Array.Empty<int>();   // offices.row.i → δείκτης στάσης

        static bool IsCityScreen(string id) =>
            id == "market" || id == "logographers" || id == "complaints" || id == "complaint-detail" || id == "offices" || id == "official-detail";

        void WireCityButtons()
        {
            Bind("map.market", OpenMarket);
            Bind("map.speech", () => { logographerSelected = -1; Show("logographers"); });
            Bind("map.complaint", () => Show("complaints"));
            Bind("map.offices", () => Show("offices"));
            foreach (string id in new[] { "market", "logographers", "complaints", "offices" })
                Bind(id + ".close", () => Show("map-stela"));
            Bind("complaint-detail.close", () => Show("complaints"));
            Bind("complaint-detail.back", () => Show("complaints"));
            Bind("official-detail.close", () => { officialAction = 0; Show("offices"); });
            Bind("official-detail.back", () => { officialAction = 0; Show("offices"); });
            for (int i = 0; i < 3; i++) { int n = i; Bind("logographers.buy." + i, () => LogographerAction(n)); }
            Bind("complaint-detail.choice.0", () => { if (complaintDetailCurrent) CommitCurrentComplaint(); });
            Bind("complaint-detail.choice.1", () => { if (complaintDetailCurrent) Show("complaints"); });
            Bind("official-detail.persuade", () => OfficialActionClick(0));
            Bind("official-detail.bribe", () => OfficialActionClick(1));
            Bind("market.read", () => { marketPage++; RenderCity(); });
            for (int i = 0; i < 4; i++)
            {
                int n = i;
                Bind("market.row." + i, () =>
                {
                    int available = Mathf.Clamp(CampaignRules.Turn(State) + 1, 1, AgoraContent.cases.Count);
                    marketSelected = Mathf.Max(0, available - 1 - (marketPage * 4 + n));
                    RenderCity();
                });
            }
            for (int i = 0; i < 3; i++)
            {
                int n = i;
                Bind("complaints.row." + i, () => OpenComplaintDetail(n));
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
            else if (id == "complaints") RenderComplaintHistory();
            else if (id == "complaint-detail") RenderComplaintDetail();
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
            // Η Αγορά ανοίγει πάντα στη φρέσκια είδηση της εβδομάδας.
            marketSelected = Mathf.Clamp(CampaignRules.Turn(State), 0, AgoraContent.cases.Count - 1);
            marketPage = 0;
            Show("market");
        }

        void RenderMarketArchive()
        {
            EnsureAgoraReady();
            int available = Mathf.Clamp(CampaignRules.Turn(State) + 1, 1, AgoraContent.cases.Count);
            int pages = Mathf.Max(1, Mathf.CeilToInt(available / 4f));
            marketPage = Mathf.Clamp(marketPage, 0, pages - 1);
            marketSelected = Mathf.Clamp(marketSelected, 0, available - 1);
            CampaignText("heading", "Η Αγορά — Ανταποκρίσεις");
            CampaignText("page-heading", pages > 1 ? "Αγορά · " + (marketPage + 1) + "/" + pages : "Αγορά");
            for (int i = 0; i < 4; i++)
            {
                int index = available - 1 - (marketPage * 4 + i);   // νεότερες πρώτες
                bool exists = index >= 0;
                SetActive("market.row." + i, exists);
                if (!exists) continue;
                var c = AgoraContent.cases[index];
                SetText("market.title." + i, c.title);
                SetText("market.date." + i, "Πρυτανεία " + (index / CampaignRules.CasesPerPrytany + 1) + " · Εβδομάδα " + (index % CampaignRules.CasesPerPrytany + 1));
                SetText("market.meta." + i, c.inCouncil ? "Βουλευτήριο" : "Πνύκα");
                PaintRow("market.row." + i, index == marketSelected);
            }
            var selected = AgoraContent.cases[Mathf.Clamp(marketSelected, 0, available - 1)];
            CampaignText("kind", selected.inCouncil ? "Βουλευτήριο" : "Πνύκα");
            WireLexiconTerms("market.body");
            var lexiconWords = lexicon != null ? lexicon.content : null;
            SetRichText("market.body", LexiconMarkup.Decorate(selected.NewsFor(State.polis.war), lexiconWords)
                + "\n\n<b>" + JsonContent.Copy("Copy_5f91d2c7a43e80b6") + "</b>\n" + LexiconMarkup.Decorate(selected.ek1, lexiconWords));
            CampaignText("quote", MarketOption(Agora.Side.A, selected) + "\n\n" + MarketOption(Agora.Side.B, selected));
            CampaignText("after", MarketStancesText(selected));
            CampaignText("unread", MarketComplaintNotice(selected));
            StackMarketColumn();
            CampaignLabel("read", "Επόμενη σελίδα");
            SetInteractable("market.read", pages > 1);
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
            bool convicted = AgoraState.caseId == c.id && AgoraState.convicted;
            var speaker = Agora.AssemblyRules.SpeakerOf(side == Agora.Side.A ? c.speechA : c.speechB, c, convicted);
            string summary = side == Agora.Side.A ? c.optionA : c.optionB;
            string line = JsonContent.Copy(side == Agora.Side.A ? "Copy_d94a07c2e6f51b38" : "Copy_e2b58d14f93c07a6");
            if (!string.IsNullOrEmpty(summary))
                line += " — " + summary;
            if (speaker != null)
                line += "\n" + JsonContent.Copy("Copy_f7c30a96b24d18e5") + speaker.FullName;
            return line;
        }

        /// <summary>Ποιοι αξιωματούχοι στηρίζουν ποια θέση: ζωντανές στάσεις αν η υπόθεση τρέχει, αλλιώς οι αρχικές.</summary>
        string MarketStancesText(Agora.CaseData c)
        {
            var stances = AgoraState.caseId == c.id ? AgoraStances() : Agora.AssemblyRules.CloneStances(c);
            string Names(Agora.Side side) => string.Join(" · ",
                stances.Where(s => s != null && s.official != null && s.weight > 0 && s.side == side).Select(s => s.official.FullName));
            var lines = new List<string>();
            string forA = Names(Agora.Side.A), forB = Names(Agora.Side.B), neutral = Names(Agora.Side.None);
            if (forA.Length > 0) lines.Add(JsonContent.Copy("Copy_07d2b94e18f5c3a6") + forA);
            if (forB.Length > 0) lines.Add(JsonContent.Copy("Copy_18e3ca05f2964db7") + forB);
            if (neutral.Length > 0) lines.Add(JsonContent.Copy("Copy_29f4db16a3075ec8") + neutral);
            return string.Join("\n", lines);
        }

        string MarketComplaintNotice(Agora.CaseData c)
        {
            if (!c.complaint.available || c.complaint.target == null)
                return "";
            return string.Format(JsonContent.Copy("Copy_3a05ec27b4186fd9"), ProcedureLabel(c.complaint.procedure), c.complaint.target.FullName);
        }

        static string ProcedureLabel(Agora.ComplaintProcedure procedure) => procedure switch
        {
            Agora.ComplaintProcedure.Eisangelia => JsonContent.Copy("Copy_4b16fd38c5297a0e"),
            Agora.ComplaintProcedure.Endeixis => JsonContent.Copy("Copy_5c270e49d63a8b1f"),
            Agora.ComplaintProcedure.Phasis => JsonContent.Copy("Copy_6d381f5ae74b9c20"),
            Agora.ComplaintProcedure.GrapheSykophantias => JsonContent.Copy("Copy_7e492a6bf85cad31"),
            _ => JsonContent.Copy("Copy_8f5a3b7c096dbe42")
        };

        // ---------- Λογογράφοι: αγορά λόγου (μία παραγγελία την εβδομάδα, χωρίς επιστροφή) ----------

        void LogographerAction(int button)
        {
            if (logographerSelected < 1)
            {
                int index = button + 1;   // το 0 είναι ο «χωρίς λογογράφο»
                if (index >= AgoraContent.logographers.Count) return;
                var logo = AgoraContent.logographers[index];
                var week = AgoraCaseCurrent ? AgoraState : new AgoraCaseState();
                if (AgoraCaseResolved || !WeekFlowRules.CanBuySpeech(week, State.citizen.wallet, logo.costDrachmas)) return;
                logographerSelected = index;
                RenderCity();
                return;
            }
            if (button == 0) BuySpeechSide(Agora.Side.A);
            else if (button == 1) BuySpeechSide(Agora.Side.B);
            else { logographerSelected = -1; RenderCity(); }
        }

        void BuySpeechSide(Agora.Side side)
        {
            int index = logographerSelected;
            if (index < 1 || index >= AgoraContent.logographers.Count) return;
            var logo = AgoraContent.logographers[index];
            bool ok = false;
            if (!MutateAndSave(() =>
            {
                EnsureAgoraCaseStarted();
                ok = WeekFlowRules.BuySpeech(AgoraState, State.citizen, index, side, logo.costDrachmas);
            }) || !ok) { logographerSelected = -1; RenderCity(); return; }
            AgoraFirestoreTelemetry.Send("speech_bought", AgoraEvent(("logo", logo.quality), ("side", side), ("cost", logo.costDrachmas)));
            logographerSelected = -1;
            RenderCity();
        }

        /// <summary>Πώς βλέπει ο λογογράφος την υπόθεση, από το ντετερμινιστικό S₀ — μόνο κείμενο, ποτέ αριθμός.</summary>
        string LogographerHint(int index, Agora.CaseData c)
        {
            if (index == 1) return "«Χωρίς λόγο δεν περνάει τίποτα σε τούτη την πόλη — χρειάζεσαι λόγο.»";
            var stances = AgoraCaseCurrent ? AgoraStances() : Agora.AssemblyRules.CloneStances(c);
            float s0 = Agora.AssemblyRules.ComputeS0(c, State.polis, AgoraCfg, stances);
            float distance = Mathf.Abs(s0 - 50f);
            if (distance <= 12f) return "«Η υπόθεση είναι αμφίρροπη — ένας καλός λόγος μπορεί να την κρίνει.»";
            if (distance > 20f) return "«Ο δήμος μοιάζει να το έχει αποφασίσει ήδη. Μίλα μόνο αν το θες για σένα.»";
            return "«Η πλάστιγγα γέρνει, μα όχι αμετάκλητα.»";
        }

        /// <summary>Η κάρτα λογογράφου έχει δικά της παιδιά στη σκηνή (Name/Description/Price/Bonus) —
        /// το δεμένο «logographers.card.i» είναι μόνο το φόντο της.</summary>
        void FillLogographerCard(int i, string name, string description, string price, string bonus)
        {
            string key = "logographers.card." + i;
            SetChildText(key, "Name", name);
            SetChildText(key, "Description", description);
            SetChildText(key, "Price", price);
            SetChildText(key, "Bonus", bonus);
            var description2 = ChildText(key, "Description");
            if (description2 != null)
            {
                description2.enableAutoSizing = true;
                description2.fontSizeMin = 17;
                description2.fontSizeMax = 23;
                description2.verticalAlignment = TMPro.VerticalAlignmentOptions.Top;
                description2.overflowMode = TMPro.TextOverflowModes.Truncate;
            }
        }

        /// <summary>Οι κάρτες της σκηνής είχαν 79px για την περιγραφή — με τα γεμάτα βιογραφικά
        /// δεν έφτανε. Οι κάρτες ανεβαίνουν κοντά στον τίτλο και ψηλώνουν, η περιγραφή παίρνει
        /// τον έξτρα χώρο, και ό,τι είναι από κάτω της (τιμή, κουμπί) κατεβαίνει αντίστοιχα.</summary>
        void StretchLogographerCards()
        {
            const float extra = 84f;   // κάρτα: -165.6 → -96 και +84 ύψος, περιγραφή 79.2 → 163.2
            for (int i = 0; i < 3; i++)
            {
                var card = Get<GameObject>("logographers.card." + i);
                if (card == null) continue;
                var rect = (RectTransform)card.transform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -96f);
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, 484.8f + extra);
                void Move(string child, float y)
                {
                    var t = card.transform.Find(child) as RectTransform;
                    if (t != null) t.anchoredPosition = new Vector2(t.anchoredPosition.x, y);
                }
                var description = ChildText("logographers.card." + i, "Description");
                if (description != null)
                    description.rectTransform.sizeDelta = new Vector2(description.rectTransform.sizeDelta.x, 79.2f + extra);
                Move("Price rule", -340.8f - extra);
                Move("Price", -361.2f - extra);
                Move("Bonus", -368.4f - extra);
                var buy = Get<Button>("logographers.buy." + i);
                if (buy != null)
                    ((RectTransform)buy.transform).anchoredPosition = new Vector2(((RectTransform)buy.transform).anchoredPosition.x, -411.6f - extra);
            }
        }

        /// <summary>Η μεσαία κάρτα είχε authored «προτεινόμενο» στυλ (κόκκινη κορδέλα, χρυσό κουμπί) —
        /// όλες οι κάρτες παίρνουν το στυλ της πρώτης ώστε καμία να μη μοιάζει προεπιλεγμένη.</summary>
        void MatchLogographerStyle()
        {
            var referenceCard = Get<GameObject>("logographers.card.0");
            var referenceBuy = Get<Button>("logographers.buy.0");
            if (referenceCard == null || referenceBuy == null) return;
            var referencePerk = referenceCard.transform.Find("Perk") != null ? referenceCard.transform.Find("Perk").GetComponent<UnityEngine.UI.Graphic>() : null;
            var referencePerkLabel = ChildText("logographers.card.0", "Perk/Perk label");
            var referenceBuyShape = referenceBuy.GetComponent<UnityEngine.UI.Graphic>();
            var referenceBuyLabel = referenceBuy.GetComponentInChildren<TMP_Text>(true);
            for (int i = 1; i < 3; i++)
            {
                var card = Get<GameObject>("logographers.card." + i);
                var buy = Get<Button>("logographers.buy." + i);
                if (card != null)
                {
                    var perk = card.transform.Find("Perk") != null ? card.transform.Find("Perk").GetComponent<UnityEngine.UI.Graphic>() : null;
                    if (perk != null && referencePerk != null) perk.color = referencePerk.color;
                    var perkLabel = ChildText("logographers.card." + i, "Perk/Perk label");
                    if (perkLabel != null && referencePerkLabel != null) perkLabel.color = referencePerkLabel.color;
                }
                if (buy != null)
                {
                    var shape = buy.GetComponent<UnityEngine.UI.Graphic>();
                    if (shape != null && referenceBuyShape != null) shape.color = referenceBuyShape.color;
                    var label = buy.GetComponentInChildren<TMP_Text>(true);
                    if (label != null && referenceBuyLabel != null) label.color = referenceBuyLabel.color;
                    buy.colors = referenceBuy.colors;
                }
            }
        }

        void RenderLogographerRoster()
        {
            EnsureAgoraReady();
            MatchLogographerStyle();
            StretchLogographerCards();
            var c = CurrentAgoraCase;
            CampaignText("heading", "Λογογράφοι της πόλης");
            var pouch = BoughtLogographer;
            if (logographerSelected >= 1 && pouch == null && !AgoraCaseResolved)
            {
                // Βήμα 2ο: επιλογή πλευράς για τον λογογράφο που διάλεξε.
                var logo = AgoraContent.logographers[logographerSelected];
                CampaignText("status", "Από ποια πλευρά θες τον λόγο του " + logo.displayName + "; Αμοιβή " + logo.costDrachmas + " δρ. — χωρίς επιστροφή.");
                SetActive("logographers.card.0", true); SetActive("logographers.card.1", true); SetActive("logographers.card.2", true);
                FillLogographerCard(0, "Πρόταση Α", c.optionA, "", "");
                FillLogographerCard(1, "Πρόταση Β", c.optionB, "", "");
                FillLogographerCard(2, logo.displayName, "Η γνώμη του: " + LogographerHint(logographerSelected, c), "", "");
                for (int i = 0; i < 3; i++) { SetActive("logographers.buy." + i, true); SetInteractable("logographers.buy." + i, true); }
                CampaignLabel("buy.0", "Υπέρ της Α — " + logo.costDrachmas + " δρ.");
                CampaignLabel("buy.1", "Υπέρ της Β — " + logo.costDrachmas + " δρ.");
                CampaignLabel("buy.2", "Άλλαξα γνώμη");
                return;
            }
            logographerSelected = -1;
            CampaignText("status", pouch != null
                ? "Στο σακούλι σου: ο λόγος του " + pouch.displayName + " υπέρ της " + SideLabel((Agora.Side)AgoraState.speechSide) + ". Θα τον εκφωνήσεις στη συνέλευση."
                : AgoraCaseResolved ? "Η υπόθεση της εβδομάδας κρίθηκε — οι λογογράφοι ξεκουράζονται."
                : AgoraCaseCurrent && AgoraState.spoke ? "Μίλησες ήδη αυτή την εβδομάδα."
                : "Διάλεξε λογογράφο για να παραγγείλεις λόγο — μία παραγγελία την εβδομάδα, πληρώνεται τώρα.");
            bool canBuy = pouch == null && !AgoraCaseResolved && !(AgoraCaseCurrent && (AgoraState.spoke || AgoraState.speechBought));
            for (int i = 0; i < 3; i++)
            {
                int index = i + 1;   // το 0 είναι ο «χωρίς λογογράφο»
                var logo = index < AgoraContent.logographers.Count ? AgoraContent.logographers[index] : null;
                SetActive("logographers.card." + i, logo != null);
                SetActive("logographers.buy." + i, logo != null && canBuy);
                if (logo == null) continue;
                FillLogographerCard(i, logo.displayName,
                    (string.IsNullOrEmpty(logo.bio) ? "" : logo.bio + "\n") + LogographerHint(index, c),
                    logo.costDrachmas + " δραχμές", "Ποιότητα " + logo.quality);
                if (!canBuy) continue;
                CampaignLabel("buy." + i, "Παραγγέλνω λόγο — " + logo.costDrachmas + " δρ.");
                SetInteractable("logographers.buy." + i, State.citizen.wallet >= logo.costDrachmas);
            }
        }

        // ---------- Καταγγελίες: το ιστορικό των δικών σου ----------

        CaseRecord[] FiledComplaints() =>
            Campaign.cases.Where(r => r != null && r.complaintFiled).OrderByDescending(r => r.turn).ToArray();

        /// <summary>Η ζωντανή καταγγελία της εβδομάδας έχει δεσμευτεί για τη συνέλευση.</summary>
        bool ComplaintCommitted => AgoraCaseCurrent && AgoraState.complaintPending;

        void RenderComplaintHistory()
        {
            EnsureAgoraReady();
            var current = CurrentAgoraCase;
            bool live = AgoraComplaintAvailable(current);
            bool committed = ComplaintCommitted;
            int offset = live || committed ? 1 : 0;
            var filed = FiledComplaints();
            CampaignText("page-heading", "Καταγγελίες");
            SetText("complaints.banner", "Οι καταγγελίες σου");
            CampaignText("heading", State.citizen.sycophantFlag ? "Συκοφάντης"
                : live ? "Διαθέσιμη καταγγελία" : committed ? "Δεσμευμένη καταγγελία" : "Το ιστορικό σου");
            CampaignText("state", filed.Length == 0 ? "Δεν έχεις καταθέσει καμία καταγγελία." : filed.Length + " καταγγελίες");
            CampaignText("body", State.citizen.sycophantFlag
                ? "Το δικαστήριο σε χαρακτήρισε συκοφάντη — δεν μπορείς να καταγγείλεις ξανά."
                : live ? "Αυτή την εβδομάδα υπάρχει δυνατότητα καταγγελίας — άνοιξέ τη για να αποφασίσεις."
                : committed ? "Δεσμεύτηκες: η καταγγελία θα κριθεί στη συνέλευση, πριν από τους λόγους."
                : "Καμία διαθέσιμη καταγγελία αυτή την εβδομάδα. Από κάτω, το ιστορικό των δικών σου.");
            SetActive("complaints.file", false);
            SetActive("complaints.primary", false);
            for (int i = 0; i < 3; i++)
            {
                bool isLiveRow = i == 0 && offset == 1;
                bool exists = isLiveRow || i - offset < filed.Length && i - offset >= 0;
                SetActive("complaints.row." + i, exists);
                if (!exists) continue;
                if (isLiveRow)
                {
                    SetText("complaints.title." + i, ProcedureLabel(current.complaint.procedure) + " κατά " + current.complaint.target.FullName);
                    SetText("complaints.meta." + i, current.title + " · " + (committed ? "Τη φέρνεις στη συνέλευση" : "Αυτή την εβδομάδα — αποφάσισε"));
                    continue;
                }
                var c = AgoraContent.cases[filed[i - offset].turn];
                SetText("complaints.title." + i, ProcedureLabel(c.complaint.procedure) + " κατά " + c.complaint.target.FullName);
                SetText("complaints.meta." + i, c.title + " · " + (filed[i - offset].complaintConvicted ? "Καταδίκη" : "Αθώωση"));
            }
        }

        void OpenComplaintDetail(int row)
        {
            bool liveRow = AgoraComplaintAvailable(CurrentAgoraCase) || ComplaintCommitted;
            if (liveRow && row == 0)
            {
                complaintDetailCurrent = true;
                Show("complaint-detail");
                return;
            }
            int index = row - (liveRow ? 1 : 0);
            var filed = FiledComplaints();
            if (index < 0 || index >= filed.Length) return;
            complaintDetailCurrent = false;
            selectedComplaintTurn = filed[index].turn;
            Show("complaint-detail");
        }

        void CommitCurrentComplaint()
        {
            var c = CurrentAgoraCase;
            if (!AgoraComplaintAvailable(c)) return;
            bool ok = false;
            if (!MutateAndSave(() =>
            {
                EnsureAgoraCaseStarted();
                ok = WeekFlowRules.CommitComplaint(AgoraState);
            }) || !ok) return;
            AgoraFirestoreTelemetry.Send("complaint_committed", AgoraEvent(("target", c.complaint.target.displayName)));
            RenderCity();
        }

        /// <summary>Το πρώτο κομμάτι του λήμματος του ευρετηρίου για τη διαδικασία — αν υπάρχει.</summary>
        string ProcedureLexiconText(Agora.ComplaintProcedure procedure)
        {
            if (lexicon == null || lexicon.content == null || lexicon.content.entries == null) return "";
            string wanted = LexiconPanel.Normalize(ProcedureLabel(procedure));
            var entry = lexicon.content.entries.FirstOrDefault(e =>
                LexiconPanel.Normalize(e.title) == wanted
                || e.aliases != null && e.aliases.Any(a => LexiconPanel.Normalize(a) == wanted));
            return entry == null || string.IsNullOrEmpty(entry.text) ? ""
                : entry.text.Split(new[] { "\n\n" }, System.StringSplitOptions.None)[0];
        }

        /// <summary>Τα κουτιά της λεπτομέρειας καταγγελίας είναι σταθερά στη σκηνή — τα μακριά
        /// κείμενα (κατηγορία, είδηση, λήμμα διαδικασίας) χωρούν με αυτόματη σμίκρυνση αντί
        /// να ξεχειλίζουν το ένα πάνω στο άλλο.</summary>
        void FitComplaintDetailTexts()
        {
            void Fit(string key, float min, float max)
            {
                var label = Get<TMP_Text>("complaint-detail." + key);
                if (label == null) return;
                label.textWrappingMode = TextWrappingModes.Normal;
                label.verticalAlignment = TMPro.VerticalAlignmentOptions.Top;
                label.overflowMode = TMPro.TextOverflowModes.Truncate;
                label.enableAutoSizing = true;
                label.fontSizeMin = min;
                label.fontSizeMax = max;
            }
            Fit("intro", 15f, 23f);
            Fit("body", 15f, 23f);
            Fit("evidence.0", 14f, 20f);
            Fit("evidence.1", 14f, 20f);
            Fit("note", 14f, 21f);
        }

        void RenderComplaintDetail()
        {
            FitComplaintDetailTexts();
            if (complaintDetailCurrent) { RenderCurrentComplaint(); return; }
            if (selectedComplaintTurn < 0 || selectedComplaintTurn >= AgoraContent.cases.Count) { Show("complaints"); return; }
            var c = AgoraContent.cases[selectedComplaintTurn];
            var record = Campaign.cases.FirstOrDefault(r => r != null && r.turn == selectedComplaintTurn);
            CampaignText("heading", ProcedureLabel(c.complaint.procedure) + " κατά " + c.complaint.target.FullName);
            CampaignText("intro", c.complaint.charge);
            CampaignText("body", "Απολογία: " + c.complaint.defence);
            CampaignText("evidence.0", "Ψήφοι δικαστών: " + c.complaint.votesFor + " – " + c.complaint.votesAgainst);
            CampaignText("evidence.1", c.complaint.verdictText);
            CampaignText("note", record != null && record.complaintConvicted
                ? "Καταδίκη: ο κατηγορούμενος αποσύρθηκε από την υπόθεση."
                : "Αθώωση" + (State.citizen.sycophantFlag ? " — και πρόστιμο συκοφαντίας." : "."));
            CampaignText("status", "");
            for (int i = 0; i < 3; i++) SetActive("complaint-detail.choice." + i, false);
        }

        /// <summary>Η απόφαση της εβδομάδας: δεσμεύω ή όχι. Η δίκη ΔΕΝ γίνεται εδώ — στη συνέλευση.</summary>
        void RenderCurrentComplaint()
        {
            var c = CurrentAgoraCase;
            bool committed = ComplaintCommitted;
            if (!committed && !AgoraComplaintAvailable(c)) { complaintDetailCurrent = false; Show("complaints"); return; }
            var opt = c.complaint;
            CampaignText("heading", ProcedureLabel(opt.procedure) + " κατά " + opt.target.FullName);
            CampaignText("intro", opt.charge);
            CampaignText("body", c.NewsFor(State.polis.war));
            CampaignText("evidence.0", ProcedureLexiconText(opt.procedure));
            CampaignText("evidence.1", "Αν το δικαστήριο καταδικάσει, ο κατηγορούμενος αποσύρεται από την υπόθεση και κερδίζεις κύρος. "
                + "Αν η καταγγελία πέσει κάτω από το ένα πέμπτο των ψήφων, χαρακτηρίζεσαι συκοφάντης και πληρώνεις πρόστιμο. "
                + "Η καταγγελία είναι η ΜΙΑ ενέργειά σου για την εβδομάδα.");
            CampaignText("note", committed ? "Δεσμεύτηκες: η δίκη θα γίνει στη συνέλευση, πριν από τους λόγους." : "");
            CampaignText("status", "");
            SetActive("complaint-detail.choice.0", !committed);
            SetActive("complaint-detail.choice.1", !committed);
            SetActive("complaint-detail.choice.2", false);
            if (!committed)
            {
                CampaignLabel("choice.0", "Τη φέρνω στη συνέλευση");
                CampaignLabel("choice.1", "Δεν τη φέρνω");
            }
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

        /// <summary>Οι στάσεις της υπόθεσης της εβδομάδας (ζωντανές αν έχει ξεκινήσει, αλλιώς οι αρχικές).</summary>
        List<Agora.OfficialStance> WeeklyStances(Agora.CaseData c) =>
            AgoraCaseCurrent ? AgoraStances() : Agora.AssemblyRules.CloneStances(c);

        static string StanceLine(Agora.OfficialStance s) =>
            s.side == Agora.Side.None ? "αναποφάσιστος" : "υπέρ της " + SideLabel(s.side);

        void RenderOfficesCatalogue()
        {
            EnsureAgoraReady();
            var c = CurrentAgoraCase;
            var stances = WeeklyStances(c);
            weeklyStanceRows = Enumerable.Range(0, stances.Count)
                .Where(i => stances[i] != null && stances[i].official != null && stances[i].weight > 0).ToArray();
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
                    SetChildText(key, "Origin", StanceLine(s) + " · βάρος " + s.weight);
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
            officialAction = 0;
            officialFeedback = "";
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
            CampaignText("influence", "Βάρος θέσης: " + office.defaultWeight);
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

        /// <summary>Πρόσωπο της υπόθεσης της εβδομάδας: εδώ γίνεται το Πείθω / Δωροδοκώ (η ΜΙΑ ενέργεια).</summary>
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
            CampaignText("name", StanceLine(s) + " · βάρος " + s.weight);
            SetOfficialBanner(office != null ? office.displayName : "");
            bool canAct = !AgoraCaseResolved && WeekFlowRules.CanPrepare(AgoraCaseCurrent ? AgoraState : new AgoraCaseState());
            int bribeCost = AgoraCfg.bribeCostPerWeight * s.weight;
            bool canPersuade = canAct && s.side == Agora.Side.None && s.weight > 0;
            bool canBribe = canAct && person.bribable && s.weight > 0;
            string officeLine = office != null ? office.displayNameWithCount + " — " + office.description : "";
            CampaignText("origin", string.IsNullOrEmpty(person.bio) ? officeLine
                : officeLine.Length == 0 ? person.bio : person.bio + "\n\n" + officeLine);
            CampaignText("method", office != null ? SelectionLabel(office.selection) : "");
            CampaignText("influence", "Βάρος " + s.weight);
            CampaignText("relationship", "");
            CampaignText("priority", StanceLine(s));
            CampaignText("access", canPersuade && canBribe ? "Πείθω ή Δωροδοκώ"
                : canPersuade ? "Πείθω"
                : canBribe ? "Δωροδοκώ — " + bribeCost + " δρ."
                : AgoraCaseCurrent && AgoraState.prepDone ? "Η ενέργεια έγινε" : "—");
            CampaignText("result", officialFeedback);
            StackOfficialCard();
            if (officialAction != 0)
            {
                // Δεύτερο βήμα: η πλευρά. Το «πίσω» ακυρώνει.
                CampaignText("status", officialAction == 1
                    ? "Να στηρίξει ποια πλευρά; Η ενέργεια της εβδομάδας καταναλώνεται ό,τι κι αν απαντήσει."
                    : "Να στηρίξει ποια πλευρά; Κόστος " + bribeCost + " δρ. — και η Ηθική σου έχει τιμή.");
                SetActive("official-detail.persuade", true);
                SetActive("official-detail.bribe", true);
                SetButtonLabel("official-detail.persuade", "Να στηρίξει την Α");
                SetButtonLabel("official-detail.bribe", "Να στηρίξει τη Β");
                SetInteractable("official-detail.persuade", true);
                SetInteractable("official-detail.bribe", true);
                return;
            }
            CampaignText("status", officialFeedback.Length > 0 ? officialFeedback
                : !canAct
                ? AgoraCaseCurrent && AgoraState.prepDone ? "Η ενέργεια της εβδομάδας έχει γίνει." : ""
                : "Μία ενέργεια την εβδομάδα: Πείθω (αναποφάσιστο) ή Δωροδοκώ (δωροδοκήσιμο).");
            SetActive("official-detail.persuade", canPersuade);
            SetActive("official-detail.bribe", canBribe);
            if (canPersuade) { SetButtonLabel("official-detail.persuade", "Πείθω τον " + person.displayName); SetInteractable("official-detail.persuade", true); }
            if (canBribe)
            {
                SetButtonLabel("official-detail.bribe", "Δωροδοκώ — " + bribeCost + " δρ.");
                SetInteractable("official-detail.bribe", State.citizen.wallet >= bribeCost);
            }
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

        void SetButtonLabel(string key, string text)
        {
            var button = Get<Button>(key);
            var label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
            if (label != null) label.text = text;
        }

        void OfficialActionClick(int button)
        {
            if (!officialDetailCase || currentScreen != "official-detail") return;
            if (officialAction == 0)
            {
                officialAction = button == 0 ? 1 : 2;
                RenderCity();
                return;
            }
            var side = button == 0 ? Agora.Side.A : Agora.Side.B;
            int action = officialAction;
            officialAction = 0;
            if (action == 1) OfficialPersuade(side);
            else OfficialBribe(side);
        }

        void OfficialPersuade(Agora.Side side)
        {
            bool ok = false, did = false;
            string name = null;
            if (!MutateAndSave(() =>
            {
                EnsureAgoraCaseStarted();
                var stances = AgoraStances();
                if (selectedCaseStance < 0 || selectedCaseStance >= stances.Count) return;
                var target = stances[selectedCaseStance];
                if (target.side != Agora.Side.None || target.weight <= 0) return;
                if (!WeekFlowRules.MarkPrep(AgoraState, Agora.PrepAction.Persuade)) return;
                ok = Agora.AssemblyRules.Persuade(target, State.citizen, side);   // η προσπάθεια καταναλώνει την ενέργεια
                StoreAgoraStances(stances);
                name = target.official.displayName;
                did = true;
            }) || !did) { RenderCity(); return; }
            officialFeedback = ok
                ? "Ο " + name + " σε άκουσε και θα στηρίξει την " + SideLabel(side) + "."
                : "Ο " + name + " σε άκουσε ευγενικά και δεν άλλαξε γνώμη.";
            AgoraFirestoreTelemetry.Send("persuade", AgoraEvent(("target", name), ("ok", ok), ("side", side)));
            RenderCity();
        }

        void OfficialBribe(Agora.Side side)
        {
            bool ok = false, did = false, exposed = false;
            string name = null;
            if (!MutateAndSave(() =>
            {
                EnsureAgoraCaseStarted();
                if (!WeekFlowRules.CanPrepare(AgoraState)) return;
                var stances = AgoraStances();
                if (selectedCaseStance < 0 || selectedCaseStance >= stances.Count) return;
                var target = stances[selectedCaseStance];
                if (!target.official.bribable || target.weight <= 0) return;
                ok = Agora.AssemblyRules.Bribe(target, State.citizen, State.polis, AgoraCfg, side, agoraRng, out exposed);
                if (ok) { WeekFlowRules.MarkPrep(AgoraState, Agora.PrepAction.Bribe); StoreAgoraStances(stances); }
                name = target.official.displayName;
                did = true;
            }) || !did) { RenderCity(); return; }
            officialFeedback = !ok ? "Δεν έχεις αρκετές δραχμές."
                : exposed ? "Ο " + name + " πήρε τα χρήματα — αλλά το έμαθε η αγορά."
                : "Ο " + name + " πήρε τα χρήματα. Κανείς δεν είδε τίποτα.";
            AgoraFirestoreTelemetry.Send("bribe", AgoraEvent(("target", name), ("ok", ok), ("exposed", exposed), ("side", side)));
            RenderCity();
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
