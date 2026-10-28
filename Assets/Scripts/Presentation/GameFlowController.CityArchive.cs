using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Οι οθόνες της πόλης στο ενιαίο σύστημα — καθαρά ενημερωτικές, τροφοδοτούμενες από το
    /// περιεχόμενο Agora: αγορά = αρχείο ειδήσεων των υποθέσεων, λογογράφοι = οι 4 με τα bios τους
    /// (η επιλογή γίνεται στον λόγο), καταγγελίες = το ιστορικό των δικών σου, αξιώματα = ο κατάλογος
    /// των 35 σωμάτων με τις περιγραφές ΕΚ και τα πρόσωπά τους. Καμία μηχανική εκτός υπόθεσης.
    /// </summary>
    public sealed partial class GameFlowController
    {
        int marketPage, marketSelected;
        int officesFilter;                 // 0 όλα, 1 κλήρωση, 2 εκλογή, 3 λοιπά
        string officesQuery = "";
        int selectedOfficeIndex = -1;
        int selectedComplaintTurn = -1;
        bool officesSearchWired;

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
            for (int i = 0; i < 4; i++)
            {
                int n = i;
                Bind("offices.filter." + i, () => { officesFilter = n; RenderCity(); });
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

        /// <summary>Η επίσκεψη καταγράφεται στο save: προϋπόθεση για να ανοίξει η υπόθεση του γύρου.</summary>
        void OpenMarket()
        {
            EnsureState();
            MutateAndSave(() => State.marketVisitedTurn = CampaignRules.Turn(State));
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
            CampaignText("page-heading", "Σελίδα " + (marketPage + 1) + " / " + pages);
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
            SetRichText("market.body", LexiconMarkup.Decorate(selected.NewsFor(State.polis.war), lexicon != null ? lexicon.content : null));
            CampaignText("quote", MarketOption(Agora.Side.A, selected) + "\n\n" + MarketOption(Agora.Side.B, selected));
            CampaignText("after", MarketStancesText(selected));
            CampaignText("unread", MarketComplaintNotice(selected));
            CampaignLabel("read", "Επόμενη σελίδα");
            SetInteractable("market.read", pages > 1);
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

        // ---------- Λογογράφοι: οι 4 με τα bios τους ----------

        void RenderLogographerRoster()
        {
            CampaignText("heading", "Λογογράφοι της πόλης");
            CampaignText("status", "Η πρόσληψη γίνεται την ώρα του λόγου σου, μέσα στην υπόθεση.");
            for (int i = 0; i < 3; i++)
            {
                int index = i + 1;   // το 0 είναι ο «χωρίς λογογράφο»
                var logo = index < AgoraContent.logographers.Count ? AgoraContent.logographers[index] : null;
                SetActive("logographers.card." + i, logo != null);
                SetActive("logographers.buy." + i, false);
                if (logo == null) continue;
                SetText("logographers.card." + i, logo.displayName + "\nΠοιότητα λόγου: " + logo.quality + " · Αμοιβή: " + logo.costDrachmas + " δρ."
                    + (string.IsNullOrEmpty(logo.bio) ? "" : "\n\n" + logo.bio));
            }
        }

        // ---------- Καταγγελίες: το ιστορικό των δικών σου ----------

        CaseRecord[] FiledComplaints() =>
            Campaign.cases.Where(r => r != null && r.complaintFiled).OrderByDescending(r => r.turn).ToArray();

        void RenderComplaintHistory()
        {
            var filed = FiledComplaints();
            CampaignText("heading", "Οι καταγγελίες σου");
            CampaignText("page-heading", "");
            CampaignText("state", filed.Length == 0 ? "Δεν έχεις καταθέσει καμία καταγγελία." : filed.Length + " καταγγελίες");
            CampaignText("body", State.citizen.sycophantFlag
                ? "Το δικαστήριο σε χαρακτήρισε συκοφάντη — δεν μπορείς να καταγγείλεις ξανά."
                : "Η καταγγελία κατατίθεται μέσα στην υπόθεση, στη φάση της προπαρασκευής, όπου είναι διαθέσιμη.");
            SetActive("complaints.file", false);
            SetActive("complaints.primary", false);
            for (int i = 0; i < 3; i++)
            {
                bool exists = i < filed.Length;
                SetActive("complaints.row." + i, exists);
                if (!exists) continue;
                var c = AgoraContent.cases[filed[i].turn];
                SetText("complaints.title." + i, ProcedureLabel(c.complaint.procedure) + " κατά " + c.complaint.target.FullName);
                SetText("complaints.meta." + i, c.title + " · " + (filed[i].complaintConvicted ? "Καταδίκη" : "Αθώωση"));
            }
        }

        void OpenComplaintDetail(int row)
        {
            var filed = FiledComplaints();
            if (row >= filed.Length) return;
            selectedComplaintTurn = filed[row].turn;
            Show("complaint-detail");
        }

        void RenderComplaintDetail()
        {
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

        // ---------- Αξιώματα: ο κατάλογος της Αθηναίων Πολιτείας ----------

        Agora.OfficeData[] FilteredOffices()
        {
            var offices = AgoraContent.offices.Where(o => o != null);
            if (officesFilter == 1) offices = offices.Where(o => o.selection == Agora.SelectionMode.Lot);
            else if (officesFilter == 2) offices = offices.Where(o => o.selection == Agora.SelectionMode.Election);
            else if (officesFilter == 3) offices = offices.Where(o => o.selection != Agora.SelectionMode.Lot && o.selection != Agora.SelectionMode.Election);
            if (!string.IsNullOrWhiteSpace(officesQuery))
                offices = offices.Where(o => (o.displayName + " " + o.displayNameWithCount).ToLowerInvariant().Contains(officesQuery.ToLowerInvariant()));
            return offices.ToArray();
        }

        void RenderOfficesCatalogue()
        {
            WireOfficesSearch();
            var filtered = FilteredOffices();
            CampaignText("heading", "Αξιώματα (" + filtered.Length + ")");
            string[] filters = { "Όλα", "Κλήρωση", "Εκλογή", "Λοιπά" };
            for (int i = 0; i < 4; i++)
            {
                CampaignLabel("filter." + i, filters[i]);
                PaintRow("offices.filter." + i, officesFilter == i);
            }
            for (int i = 0; i < 6; i++)
            {
                bool exists = i < filtered.Length;
                SetActive("offices.row." + i, exists);
                if (!exists) continue;
                var office = filtered[i];
                var button = Get<Button>("offices.row." + i);
                var label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
                if (label != null) label.text = office.displayNameWithCount + " · " + SelectionLabel(office.selection);
            }
        }

        void WireOfficesSearch()
        {
            if (officesSearchWired) return;
            var search = Get<TMP_InputField>("offices.search");
            if (search == null) return;
            search.onValueChanged.AddListener(value => { officesQuery = value ?? ""; if (currentScreen == "offices") RenderCity(); });
            officesSearchWired = true;
        }

        void OpenOfficeDetail(int row)
        {
            var filtered = FilteredOffices();
            if (row >= filtered.Length) return;
            selectedOfficeIndex = AgoraContent.offices.IndexOf(filtered[row]);
            Show("official-detail");
        }

        void RenderOfficeDetail()
        {
            if (selectedOfficeIndex < 0 || selectedOfficeIndex >= AgoraContent.offices.Count) { Show("offices"); return; }
            var office = AgoraContent.offices[selectedOfficeIndex];
            var persons = AgoraContent.officials.Where(p => p != null && p.office == office).ToArray();
            CampaignText("heading", office.displayNameWithCount);
            CampaignText("office", office.description);
            CampaignText("name", persons.Length == 0 ? "" : "Στο παιχνίδι: " + string.Join(" · ", persons.Select(p => p.FullName)));
            CampaignText("origin", persons.Length == 0 || string.IsNullOrEmpty(persons[0].bio) ? "" : persons[0].bio);
            CampaignText("method", SelectionLabel(office.selection) + (office.payObols > 0 ? " · Μισθός " + office.payObols + " οβ." : "")
                + (string.IsNullOrEmpty(office.athPolRef) ? "" : " · ΑΠ " + office.athPolRef));
            CampaignText("influence", "Βάρος όταν παίρνει θέση σε υπόθεση: " + office.defaultWeight);
            CampaignText("relationship", "");
            CampaignText("access", "");
            CampaignText("priority", "");
            CampaignText("result", "");
            CampaignText("status", "");
            SetActive("official-detail.persuade", false);
            SetActive("official-detail.bribe", false);
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
