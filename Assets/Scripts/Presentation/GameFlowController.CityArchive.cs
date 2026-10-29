using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Οι οθόνες της πόλης στη ροή του χάρτη: η Αγορά ξεκινά την υπόθεση της εβδομάδας και δείχνει την
    /// πλήρη είδηση. Οι τρεις ανεξάρτητες κινήσεις: Αξιωματούχοι (πειθώ ή δωροδοκία στον αναποφάσιστο),
    /// Λογογράφοι (ο Δείναρχος, για Α, Β ή την κατηγορία), Καταγγελίες (κατήγορος στη δίκη της εβδομάδας).
    /// Κανόνες πάντα από Agora.AssemblyRules μέσω WeekFlowRules.
    /// </summary>
    public sealed partial class GameFlowController
    {
        int marketSelected;
        readonly List<Button> marketRowButtons = new List<Button>();
        ScrollRect marketScroll;
        bool officialDetailCase;           // το official-detail δείχνει πρόσωπο της τρέχουσας υπόθεσης
        int selectedCaseStance = -1;       // δείκτης στη λίστα στάσεων της υπόθεσης
        int[] weeklyStanceRows = System.Array.Empty<int>();   // offices.row.i → δείκτης στάσης
        int officialAction;                // 0 τίποτα, 1 εκκρεμεί πλευρά για πειθώ, 2 για δωροδοκία
        string officialFeedback = "", logographerFeedback = "";

        static bool IsCityScreen(string id) =>
            id == "market" || id == "logographers" || id == "complaints" || id == "complaint-detail" || id == "offices" || id == "official-detail";

        void WireCityButtons()
        {
            Bind("map.market", OpenMarket);
            Bind("map.speech", () => { logographerFeedback = ""; Show("logographers"); });
            Bind("map.complaint", () => Show("complaints"));
            Bind("map.offices", () => Show("offices"));
            foreach (string id in new[] { "market", "logographers", "complaints", "offices" })
                Bind(id + ".close", () => Show("map-stela"));
            Bind("complaint-detail.close", () => Show("complaints"));
            Bind("complaint-detail.back", () => Show("complaints"));
            Bind("complaint-detail.choice.0", AccuseThisWeek);
            Bind("complaint-detail.choice.1", () => Show("complaints"));
            for (int i = 0; i < 3; i++) { int n = i; Bind("complaints.row." + i, () => { if (n == 0 && WeekFlowRules.HasTrial(CurrentAgoraCase)) Show("complaint-detail"); }); }
            for (int i = 0; i < 3; i++) { int n = i; Bind("logographers.buy." + i, () => BuyLogographerSpeech(n)); }
            Bind("official-detail.close", () => { officialAction = 0; Show("offices"); });
            Bind("official-detail.back", () => { officialAction = 0; Show("offices"); });
            Bind("official-detail.persuade", () => OfficialActionClick(0));
            Bind("official-detail.bribe", () => OfficialActionClick(1));
            for (int i = 0; i < 4; i++)
            {
                int n = i;
                Bind("market.row." + i, () => SelectMarketRow(n));
            }
            for (int i = 0; i < OfficialCards; i++)
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
            else if (id == "complaint-detail") RenderTrialDetail();
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
            ShowPlaceCardIfNeeded(GuideMarket);
        }

        int MarketAvailableCases => Mathf.Clamp(CampaignRules.Turn(State) + 1, 1, AgoraContent.cases.Count);

        void SelectMarketRow(int row)
        {
            marketSelected = Mathf.Max(0, MarketAvailableCases - 1 - row);   // νεότερες πρώτες
            RenderCity();
        }

        const float MarketRowPitch = 140.4f;
        const int MarketRows = 20;

        /// <summary>Οι 20 σειρές ειδήσεων είναι αντικείμενα της σκηνής, μέσα σε κυλιόμενη λίστα — μία ανά υπόθεση.</summary>
        void CollectMarketRows()
        {
            if (marketRowButtons.Count > 0) return;
            for (int i = 0; i < MarketRows; i++)
            {
                var row = Get<Button>("market.row." + i);
                if (row != null) marketRowButtons.Add(row);
            }
            marketScroll = Get<ScrollRect>("market.news-scroll");
        }

        void SetMarketRowText(int row, string part, string text)
        {
            var label = Get<TMP_Text>("market." + part + "." + row);
            if (label == null) return;
            label.richText = false;
            label.text = text ?? "";
        }

        void RenderMarketArchive()
        {
            EnsureAgoraReady();
            int available = MarketAvailableCases;
            marketSelected = Mathf.Clamp(marketSelected, 0, available - 1);
            CampaignText("heading", W(CityText.MarketHeading));
            CampaignText("page-heading", W(CityText.MarketPage));
            CollectMarketRows();
            for (int i = 0; i < marketRowButtons.Count; i++)
            {
                int index = available - 1 - i;   // νεότερες πρώτες
                bool exists = index >= 0;
                marketRowButtons[i].gameObject.SetActive(exists);
                if (!exists) continue;
                var c = AgoraContent.cases[index];
                SetMarketRowText(i, "title", c.title);
                SetMarketRowText(i, "date", W(CityText.MarketDate, index / CampaignRules.CasesPerPrytany + 1, index % CampaignRules.CasesPerPrytany + 1));
                SetMarketRowText(i, "meta", VenueLabel(c.venue));
                CivicUIFactory.PaintSelectable(marketRowButtons[i], index == marketSelected);
            }
            // Η κύλιση φτάνει ως την τελευταία ορατή σειρά, όχι ως τις 20.
            if (marketScroll != null) marketScroll.content.sizeDelta = new Vector2(0f, available * MarketRowPitch);
            var selected = AgoraContent.cases[Mathf.Clamp(marketSelected, 0, available - 1)];
            CampaignText("kind", VenueLabel(selected.venue));
            WireLexiconTerms("market.body");
            var lexiconWords = lexicon != null ? lexicon.content : null;
            SetRichText("market.body", LexiconMarkup.Decorate(selected.NewsFor(State.polis.war), lexiconWords)
                + "\n\n<b>" + JsonContent.Copy("Copy_5f91d2c7a43e80b6") + "</b>\n" + LexiconMarkup.Decorate(selected.ek1, lexiconWords));
            CampaignText("quote", MarketOption(Agora.Side.A, selected) + "\n\n" + MarketOption(Agora.Side.B, selected));
            CampaignText("after", MarketStancesText(selected));
            // Η δίκη της εβδομάδας και, όπου υπάρχει, το στοιχείο που κυκλοφορεί στην αγορά.
            var trial = selected.trial;
            CampaignText("unread", WeekFlowRules.HasTrial(selected)
                ? W(WeekText.BoardTrial, ProcedureLabel(trial.procedure), trial.defendant.FullName, TrialVenueLabel(trial.venue))
                    + (string.IsNullOrWhiteSpace(trial.marketClue) ? "" : "\n" + trial.marketClue)
                : "");
            // «Πού γέρνει η πόλη» (7.2): μόνο για την υπόθεση της εβδομάδας, όσο δεν έχει κριθεί.
            bool current = marketSelected == CampaignRules.Turn(State);
            CampaignText("read", current && !AgoraCaseResolved ? W(CityText.LeanLine, LeaningText()) : "");
            StackMarketColumn();
        }

        string LeaningText()
        {
            var input = WeekFlowRules.KnownVoteInput(WeekState, CurrentAgoraCase, State.polis, State.citizen);
            return W(Agora.AssemblyRules.Leaning(input, AgoraCfg) switch
            {
                Agora.CityLeaning.TowardA => CityText.LeanA,
                Agora.CityLeaning.TowardB => CityText.LeanB,
                _ => CityText.LeanContested
            });
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
            var lean = Get<TMP_Text>("market.read");
            if (body == null || quote == null || after == null) return;
            var quotePanel = quote.rectTransform.parent as RectTransform;
            // topY: αμέσως κάτω από τον τίτλο (heading: y 181.2 + ύψος κειμένου ~65) — το 308 άφηνε
            // μεγάλο κενό ΚΑΙ στρίμωχνε τη στήλη τόσο που το πλαίσιο των προτάσεων έχανε τον Β ομιλητή.
            const float left = 628.8f, width = 936f, topY = 252f, bottomY = 724f, gap = 14f;
            const float quotePad = 56f, quoteInset = 52.8f;
            var labels = new[] { body, quote, after, unread, lean };
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
            float total = heights.Sum() + (heights[1] > 0 ? quotePad : 0) + (labels.Length - 1) * gap;
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
            foreach (var (label, height) in new[] { (unread, heights[3]), (lean, heights[4]) })
            {
                if (label == null) continue;
                label.gameObject.SetActive(height > 0);
                if (height > 0) Place(label.rectTransform, height);
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

        // ---------- Λογογράφοι: ο Δείναρχος, ένας λόγος την εβδομάδα για Α, Β ή την κατηγορία ----------

        /// <summary>Η κάρτα έχει δικά της παιδιά στη σκηνή (Name/Description/Price/Bonus) — το δεμένο
        /// «logographers.card.i» είναι μόνο το φόντο της.</summary>
        void FillLogographerCard(int i, string name, string description, string price, string bonus)
        {
            string key = "logographers.card." + i;
            SetChildText(key, "Name", name);
            SetChildText(key, "Description", description);
            SetChildText(key, "Price", price);
            SetChildText(key, "Bonus", bonus);
            var text = ChildText(key, "Description");
            if (text != null)
            {
                text.enableAutoSizing = true; text.fontSizeMin = 13; text.fontSizeMax = 21;
                text.verticalAlignment = VerticalAlignmentOptions.Top;
                text.overflowMode = TextOverflowModes.Truncate;
            }
        }

        void RenderLogographerRoster()
        {
            EnsureAgoraReady();
            var c = CurrentAgoraCase;
            var s = WeekState;
            var logo = AgoraContent.logographers.FirstOrDefault(l => l != null);
            string name = logo != null ? logo.displayName : "";
            CampaignText("heading", W(CityText.LogographersHeading));
            // Η κορδέλα κάθε κάρτας γράφει τον λογογράφο: ο ίδιος άνθρωπος, τρεις παραγγελίες.
            for (int i = 0; i < 3; i++) SetChildText("logographers.card." + i, "Perk/Perk label", name.ToUpperInvariant());
            bool canBuy = WeekFlowRules.CanBuyLogographer(s, State.citizen, AgoraCfg);
            string[] titles = { W(WeekText.LogoForA), W(WeekText.LogoForB), W(WeekText.LogoForTrial) };
            for (int i = 0; i < 3; i++)
            {
                bool trialCard = i == 2;
                bool trialAllowed = WeekFlowRules.CanOrderForTrial(s, c);
                SetActive("logographers.card." + i, true);
                FillLogographerCard(i, titles[i],
                    trialCard && !trialAllowed ? W(WeekText.LogoTrialOnly) : logo != null ? logo.bio : "",
                    W(WeekText.LogoPrice, AgoraCfg.logographerCost), W(WeekText.LogoBonus, AgoraCfg.logographerPoints));
                bool show = canBuy && (!trialCard || trialAllowed);
                SetActive("logographers.buy." + i, show);
                if (show) { CampaignLabel("buy." + i, W(WeekText.LogoBuy, AgoraCfg.logographerCost)); SetInteractable("logographers.buy." + i, true); }
            }
            string bought = !s.logographerBought ? null : W(WeekText.LogoBought, name,
                titles[s.logographerForTrial ? 2 : (Agora.Side)s.logographerFor == Agora.Side.A ? 0 : 1]);
            CampaignText("status", logographerFeedback.Length > 0 ? logographerFeedback
                : bought ?? (State.citizen.wallet < AgoraCfg.logographerCost && !AgoraCaseResolved ? W(WeekText.LogoNoMoney) : W(WeekText.LogoStatus, name)));
        }

        void BuyLogographerSpeech(int card)
        {
            if (currentScreen != "logographers") return;
            bool ok = false;
            if (!MutateAndSave(() =>
            {
                EnsureAgoraCaseStarted();
                ok = WeekFlowRules.BuyLogographer(AgoraState, CurrentAgoraCase, State.citizen, AgoraCfg,
                    card == 0 ? Agora.Side.A : card == 1 ? Agora.Side.B : Agora.Side.None, forTrial: card == 2);
            }) || !ok) { RenderCity(); return; }
            AgoraFirestoreTelemetry.Send("logographer", AgoraEvent(("for", card == 2 ? "trial" : card == 0 ? "A" : "B"), ("cost", AgoraCfg.logographerCost)));
            logographerFeedback = "";
            RenderCity();
        }

        // ---------- Καταγγελίες: η δίκη της εβδομάδας και η απόφαση να κατηγορήσεις ----------

        void RenderComplaints()
        {
            EnsureAgoraReady();
            var c = CurrentAgoraCase;
            bool trial = WeekFlowRules.HasTrial(c);
            CampaignText("page-heading", W(CityText.ComplaintsPage));
            SetText("complaints.banner", W(CityText.ComplaintsBanner));
            CampaignText("heading", trial ? TrialTitle(c.trial) : "");
            CampaignText("state", "");
            CampaignText("body", State.citizen.sycophantFlag ? W(WeekText.Sycophant)
                : trial ? W(WeekText.ComplaintsThisWeek, ProcedureLabel(c.trial.procedure), c.trial.defendant.FullName, TrialVenueLabel(c.trial.venue))
                : W(WeekText.ComplaintsNone));
            SetActive("complaints.file", false);
            SetActive("complaints.primary", false);
            for (int i = 0; i < 3; i++) SetActive("complaints.row." + i, trial && i == 0);
            if (!trial) return;
            SetText("complaints.title.0", TrialTitle(c.trial));
            SetText("complaints.meta.0", W(WeekText.EvidenceLine, EvidenceWord(c.trial.evidence)) + " · " + TrialVenueLabel(c.trial.venue));
        }

        /// <summary>Τα κουτιά της λεπτομέρειας είναι σταθερά στη σκηνή — τα μακριά κείμενα μικραίνουν αντί να ξεχειλίζουν.</summary>
        void FitTrialDetailTexts()
        {
            foreach (var (key, min, max) in new[] { ("intro", 15f, 23f), ("body", 15f, 23f), ("evidence.0", 14f, 20f), ("evidence.1", 14f, 20f), ("note", 14f, 21f) })
            {
                var label = Get<TMP_Text>("complaint-detail." + key);
                if (label == null) continue;
                label.textWrappingMode = TextWrappingModes.Normal;
                label.verticalAlignment = VerticalAlignmentOptions.Top;
                label.overflowMode = TextOverflowModes.Truncate;
                label.enableAutoSizing = true; label.fontSizeMin = min; label.fontSizeMax = max;
            }
        }

        /// <summary>Η δίκη της εβδομάδας: στοιχεία σε λέξη και ο κίνδυνος γραμμένος καθαρά, πριν αποφασίσεις.</summary>
        void RenderTrialDetail()
        {
            EnsureAgoraReady();
            var c = CurrentAgoraCase;
            if (!WeekFlowRules.HasTrial(c)) { Show("complaints"); return; }
            FitTrialDetailTexts();
            var t = c.trial;
            var s = WeekState;
            bool areopagus = t.venue == Agora.TrialVenue.Areopagus;
            CampaignText("heading", TrialTitle(t));
            CampaignText("intro", W(WeekText.TrialCharge, t.charge));
            CampaignText("body", W(WeekText.TrialDefence, t.defence));
            CampaignText("evidence.0", W(WeekText.EvidenceLine, EvidenceWord(t.evidence)));
            CampaignText("evidence.1", W(WeekText.TrialRisk, AgoraCfg.sycophantBelow, AgoraCfg.accuserFine) + (areopagus ? "\n" + W(WeekText.TrialAreopagusNote) : ""));
            CampaignText("note", s.trialDone ? (s.trialGuilty ? t.verdictGuilty : t.verdictAcquitted)
                : s.accuser ? W(WeekText.YouAccuse) : W(WeekText.TrialOtherAccuser, t.defaultAccuser));
            CampaignText("status", State.citizen.sycophantFlag ? W(WeekText.Sycophant) : "");
            bool canAccuse = WeekFlowRules.CanAccuse(s, c, State.citizen);
            SetText("complaint-detail.evidence.0.heading", W(CityText.EvidenceHeading));
            SetText("complaint-detail.evidence.1.heading", W(CityText.RiskHeading));
            SetText("complaint-detail.decision", canAccuse ? W(areopagus ? CityText.DecisionInform : CityText.DecisionAccuse) : W(CityText.DecisionTaken));
            SetText("complaint-detail.decision-intro", canAccuse ? W(areopagus ? CityText.DecisionInformIntro : CityText.DecisionAccuseIntro) : "");
            SetActive("complaint-detail.choice.0", canAccuse);
            SetActive("complaint-detail.choice.1", canAccuse);
            SetActive("complaint-detail.choice.2", false);
            if (canAccuse)
            {
                CampaignLabel("choice.0", W(areopagus ? WeekText.InformButton : WeekText.AccuseButton));
                CampaignLabel("choice.1", W(WeekText.NoButton));
            }
        }

        void AccuseThisWeek()
        {
            if (currentScreen != "complaint-detail") return;
            bool ok = false;
            if (!MutateAndSave(() =>
            {
                EnsureAgoraCaseStarted();
                ok = WeekFlowRules.Accuse(AgoraState, CurrentAgoraCase, State.citizen);
            }) || !ok) return;
            AgoraFirestoreTelemetry.Send("accuse", AgoraEvent(("defendant", CurrentAgoraCase.trial.defendant.displayName)));
            RenderCity();
        }

        // ---------- Αξιωματούχοι: μόνο τα πρόσωπα της εβδομάδας (ο κατάλογος των αξιωμάτων ζει στο Ευρετήριο) ----------

        const int OfficialCards = 7;   // όσες κάρτες έχει η σκηνή (η υπόθεση 20 έχει 7 πρόσωπα)

        /// <summary>Οι θέσεις των αξιωματούχων στην υπόθεση της εβδομάδας.</summary>
        List<Agora.OfficialStance> WeeklyStances(Agora.CaseData c) => Agora.AssemblyRules.CloneStances(c);

        static string StanceLine(Agora.OfficialStance s) =>
            W(s.side == Agora.Side.A ? CityText.StanceA : s.side == Agora.Side.B ? CityText.StanceB : CityText.StanceUndecided);

        /// <summary>Τι μπορεί να κάνει ο παίκτης με αυτό το πρόσωπο — λέγεται από πριν, χωρίς έκπληξη.</summary>
        string OfficialAbility(Agora.CaseData c, Agora.OfficialStance s)
        {
            if (s.official != c.undecided || s.side != Agora.Side.None) return W(CityText.InBase);
            string ability = W(CityText.CanPersuade, AgoraCfg.PersuadeThreshold(c.difficulty));
            return s.official.bribable ? ability + " · " + W(CityText.AcceptsMoney) : ability;
        }

        void RenderOfficesCatalogue()
        {
            EnsureAgoraReady();
            var c = CurrentAgoraCase;
            var stances = WeeklyStances(c);
            weeklyStanceRows = Enumerable.Range(0, stances.Count)
                .Where(i => stances[i] != null && stances[i].official != null).Take(OfficialCards).ToArray();
            CampaignText("heading", W(CityText.OfficesHeading));
            var heading = Get<TMP_Text>("offices.heading");
            if (heading != null) heading.raycastTarget = false;
            var close = Get<Button>("offices.close");
            if (close != null) close.transform.SetAsLastSibling();   // πάνω από ό,τι κι αν το σκεπάζει
            for (int i = 0; i < OfficialCards; i++)
            {
                string key = "offices.row." + i;
                bool exists = i < weeklyStanceRows.Length;
                SetActive(key, exists);
                if (!exists) continue;
                // Η κάρτα έχει δικά της παιδιά: Office banner/Office, Name, Origin, Method, Meet.
                var s = stances[weeklyStanceRows[i]];
                SetChildText(key, "Office banner/Office", s.official.office != null ? s.official.office.displayName : W(CityText.NoOffice));
                SetChildText(key, "Name", s.official.FullName);
                SetChildText(key, "Origin", StanceLine(s));
                SetChildText(key, "Method", OfficialAbility(c, s));
                SetChildText(key, "Meet", W(CityText.Meet));
            }
        }

        void OpenOfficeDetail(int row)
        {
            officialAction = 0;
            officialFeedback = "";
            if (row < 0 || row >= weeklyStanceRows.Length) return;
            officialDetailCase = true;
            selectedCaseStance = weeklyStanceRows[row];
            Show("official-detail");
        }

        void RenderOfficeDetail() => RenderCaseOfficialDetail();

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

        /// <summary>Πρόσωπο της υπόθεσης της εβδομάδας. Μόνο ο αναποφάσιστος δέχεται κίνηση (πειθώ ή δωροδοκία)·
        /// κάθε κουμπί λέει από πριν τι χρειάζεται και τι κοστίζει.</summary>
        void RenderCaseOfficialDetail()
        {
            var c = CurrentAgoraCase;
            var stances = WeeklyStances(c);
            if (selectedCaseStance < 0 || selectedCaseStance >= stances.Count || stances[selectedCaseStance]?.official == null)
            { officialDetailCase = false; Show("offices"); return; }
            var s = stances[selectedCaseStance];
            var person = s.official;
            var office = person.office;
            var week = WeekState;
            var cfg = AgoraCfg;
            bool undecided = person == c.undecided && s.side == Agora.Side.None;
            CampaignText("heading", person.FullName);
            CampaignText("name", StanceLine(s));
            SetOfficialBanner(office != null ? office.displayName : "");
            string officeLine = office != null ? office.displayNameWithCount + " — " + office.description : "";
            CampaignText("origin", string.IsNullOrEmpty(person.bio) ? officeLine
                : officeLine.Length == 0 ? person.bio : person.bio + "\n\n" + officeLine);
            int threshold = cfg.PersuadeThreshold(c.difficulty);
            bool canPersuade = Agora.AssemblyRules.CanPersuade(State.citizen.peitho, c.difficulty, cfg);
            bool bribable = undecided && person.bribable;
            // Οι τέσσερις γραμμές της κάρτας και τα δύο κουτιά (πειθώ, δωροδοκία) λένε από πριν τι γίνεται.
            string[] labels = { CityText.DetailLabelMethod, CityText.DetailLabelStance, CityText.DetailLabelOffice, CityText.DetailLabelMove };
            for (int i = 0; i < labels.Length; i++) SetText("official-detail.label." + i, W(labels[i]));
            CampaignText("method", office != null ? SelectionLabel(office.selection) : "");
            CampaignText("influence", StanceLine(s));
            CampaignText("priority", office != null ? office.displayName : W(CityText.NoOffice));
            CampaignText("access", W(!undecided ? CityText.MoveNone : bribable ? CityText.MovePersuadeBribe : CityText.MovePersuade));
            SetText("official-detail.approach.0.tag", W(CityText.PersuadeTag));
            SetText("official-detail.approach.0.title", W(CityText.PersuadeTitle));
            SetText("official-detail.approach.0.body", undecided ? W(CityText.PersuadeBody, threshold, cfg.officialPoints) : W(CityText.NotUndecidedBody));
            SetText("official-detail.approach.1.tag", W(CityText.BribeTag));
            SetText("official-detail.approach.1.title", W(CityText.BribeTitle));
            SetText("official-detail.approach.1.body", !undecided ? W(CityText.NotUndecidedBody)
                : bribable ? W(CityText.BribeBody, cfg.bribeCost, cfg.bribeEthosLoss, cfg.officialPoints, cfg.bribeExposureOneIn)
                : W(CityText.NoBribeBody, person.displayName));
            StackOfficialCard();
            bool canMove = undecided && WeekFlowRules.CanMoveOfficial(week) && !AgoraCaseResolved;
            if (canMove && officialAction != 0)
            {
                // Δεύτερο βήμα: ποια πρόταση θα στηρίξει. Το «πίσω» ακυρώνει.
                CampaignText("result", W(WeekText.ChooseSide));
                SetActive("official-detail.persuade", true);
                SetActive("official-detail.bribe", true);
                SetButtonLabel("official-detail.persuade", W(WeekText.SideA));
                SetButtonLabel("official-detail.bribe", W(WeekText.SideB));
                SetInteractable("official-detail.persuade", true);
                SetInteractable("official-detail.bribe", true);
                return;
            }
            string info = !undecided ? W(WeekText.OfficialInBase)
                : officialFeedback.Length > 0 ? officialFeedback
                : week.officialMoveDone ? W(WeekText.MoveDone)
                : W(WeekText.OfficialUndecided) + (bribable ? "\n" + W(WeekText.BribeRisk, cfg.bribeExposureOneIn, cfg.bribeExposureFameLoss, cfg.bribeExposureTrustLoss) : "");
            CampaignText("result", info);
            SetActive("official-detail.persuade", canMove);
            SetActive("official-detail.bribe", canMove && bribable);
            if (canMove)
            {
                SetButtonLabel("official-detail.persuade", canPersuade ? W(WeekText.PersuadeButton, threshold, State.citizen.peitho)
                    : W(WeekText.PersuadeMissing, threshold, threshold - State.citizen.peitho));
                SetInteractable("official-detail.persuade", canPersuade);
            }
            if (canMove && bribable)
            {
                SetButtonLabel("official-detail.bribe", W(WeekText.BribeButton, cfg.bribeCost, cfg.bribeEthosLoss));
                SetInteractable("official-detail.bribe", State.citizen.wallet >= cfg.bribeCost);
            }
        }

        void SetButtonLabel(string key, string text)
        {
            var button = Get<Button>(key);
            var label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
            if (label != null) label.text = text;
        }

        /// <summary>Πρώτο κλικ: πειθώ ή δωροδοκία· δεύτερο κλικ: η πρόταση που θα στηρίξει.</summary>
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
            var c = CurrentAgoraCase;
            bool ok = false;
            if (!MutateAndSave(() =>
            {
                EnsureAgoraCaseStarted();
                ok = action == 1
                    ? WeekFlowRules.Persuade(AgoraState, c, State.citizen, AgoraCfg, side)
                    : WeekFlowRules.Bribe(AgoraState, c, State.citizen, State.polis, AgoraCfg, agoraRng, side);
            }) || !ok) { RenderCity(); return; }
            string name = c.undecided.displayName;
            officialFeedback = action == 1 ? W(WeekText.Persuaded, name, SideLabel(side))
                : AgoraState.bribeExposed ? W(WeekText.BribeExposed, name, AgoraCfg.bribeExposureFameLoss, AgoraCfg.bribeExposureTrustLoss)
                : W(WeekText.Bribed, name, SideLabel(side));
            AgoraFirestoreTelemetry.Send(action == 1 ? "persuade" : "bribe", AgoraEvent(("target", name), ("side", side), ("exposed", AgoraState.bribeExposed)));
            RenderCity();
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
