using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    public sealed partial class GameFlowController
    {
        CityContent cityData;
        int selectedNews, selectedComplaint, selectedOfficial, officeFilter;
        int selectedWriter = -1;
        readonly HashSet<int> wiredWriterCards = new HashSet<int>();
        readonly List<Button> weeklyNewsRows = new List<Button>();
        ScrollRect weeklyNewsScroll;
        RectTransform weeklyNewsContent;
        float weeklyNewsWidth, weeklyNewsHeight;
        GameObject officesCataloguePanel;
        RectTransform officesCatalogueContent;
        ScrollRect officesCatalogueScroll;
        Button officesCatalogueButton;
        CivicOfficesCatalogue officesCatalogue;
        int catalogueCategory;
        readonly List<Button> catalogueTabs = new List<Button>();
        bool confirmBribe;
        CityContent CityData
        {
            get
            {
                if (cityData == null) cityData = JsonContent.City;
                return cityData;
            }
        }

        CityState City
        {
            get
            {
                if (State.city == null)
                    State.city = new CityState();
                if (State.city.readNews == null)
                    State.city.readNews = new List<int>();
                if (State.city.complaintDecisions == null || State.city.complaintDecisions.Length != 3)
                    State.city.complaintDecisions = new string[3];
                if (State.city.officialApproaches == null || State.city.officialApproaches.Length != 6)
                    State.city.officialApproaches = new int[6];
                if (State.city.officialSupport == null || State.city.officialSupport.Length != 6)
                    State.city.officialSupport = new int[6];
                if(State.city.supportUntilTurn==null||State.city.supportUntilTurn.Length!=6) State.city.supportUntilTurn=new int[6];
                return State.city;
            }
        }

        static bool IsCityScreen(string id) => id == "market" || id == "logographers" || id == "complaints" || id == "complaint-detail" || id == "offices" || id == "official-detail";
        void WireCityButtons()
        {
            Bind("map.market", () => OpenCity("market"));
            Bind("map.speech", () => OpenCity("logographers"));
            Bind("map.complaint", () => OpenCity("complaints"));
            Bind("map.offices", () => OpenCity("offices"));
            foreach (string screen in new[]
            {
                "market",
                "logographers",
                "complaints",
                "complaint-detail",
                "offices",
                "official-detail"
            }

            )
            {
                Bind(screen + ".back", Back);
                Bind(screen + ".close", Back);
            }

            for (int i = 0; i < 4; i++)
            {
                int n = i;
                Bind("offices.filter." + i, () =>
                {
                    officeFilter = n;
                    RenderOffices();
                });
            }

            for (int i = 0; i < 3; i++)
            {
                int n = i;
                Bind("logographers.buy." + i, () => BuySpeech(n));
                Bind("complaints.row." + i, () =>
                {
                    selectedComplaint = n;
                    RenderComplaints();
                });
                Bind("complaint-detail.choice." + i, () => DecideComplaint(n));
            }

            for (int i = 0; i < 6; i++)
            {
                int n = i;
                Bind("offices.row." + i, () =>
                {
                    selectedOfficial = n;
                    confirmBribe = false;
                    Show("official-detail");
                });
            }

            Bind("complaints.primary", () => { if (ComplaintAvailable) { selectedComplaint = 0; Show("complaint-detail"); } });
            Bind("official-detail.persuade", () => ApproachOfficial(false));
            Bind("official-detail.bribe", () => ApproachOfficial(true));
            TMP_InputField search = Get<TMP_InputField>("offices.search");
            if (search != null)
                search.onValueChanged.AddListener(_ => RenderOffices());
        }

        void OpenCity(string screen)
        {
            if (modalOpen || routing || gamePaused)
                return;
            EnsureState();
            if (screen == "market")
            {
                selectedNews = Mathf.Clamp(CampaignRules.Turn(State), 0, CampaignRules.TotalCases - 1);
                ReadNews();
            }
            if (screen == "complaints" || screen == "complaint-detail") selectedComplaint = 0;
            if (screen == "offices" && officesCataloguePanel != null) officesCataloguePanel.SetActive(false);
            confirmBribe = false;
            Show(screen);
        }

        void ReadNews()
        {
            int id = 1000 + selectedNews;
            if (!City.readNews.Contains(id))
            {
                City.readNews.Add(id);
                SaveCheckpoint();
            }
        }

        void RenderCityBadges()
        {
            if (CityData == null)
                return;
            int unread = Enumerable.Range(0, Mathf.Clamp(CampaignRules.Turn(State) + 1, 1, CampaignRules.TotalCases)).Count(t => !City.readNews.Contains(1000 + t));
            int pending = ComplaintAvailable && string.IsNullOrEmpty(City.complaintDecisions[0]) ? 1 : 0;
            foreach (var pair in new[]
            {
                new KeyValuePair<string, int>("market", unread),
                new KeyValuePair<string, int>("complaint", pending)
            }

            )
            {
                GameObject badge = Get<GameObject>("map." + pair.Key + ".badge");
                if (badge != null)
                    badge.SetActive(pair.Value > 0);
                SetText("map." + pair.Key + ".count", pair.Value.ToString());
            }
        }

        void RenderCity()
        {
            if (CityData == null)
                return;
            SetText(currentScreen + ".funds", JsonContent.Copy("Copy_9fd25730f3bc1ab9") + State.cityFunds.ToString("N0") + JsonContent.Copy("Copy_226a8ac686f3de22") + State.personalFunds.ToString("N0") + " οβ.");
            if (currentScreen == "market")
                RenderMarket();
            else if (currentScreen == "logographers")
                RenderWriters();
            else if (currentScreen == "complaints")
                RenderComplaints();
            else if (currentScreen == "complaint-detail")
                RenderComplaintDetail();
            else if (currentScreen == "offices")
                RenderOffices();
            else if (currentScreen == "official-detail")
                RenderOfficial();
        }

        void RenderMarket()
        {
            EnsureWeeklyNewsList();
            int count = Mathf.Clamp(CampaignRules.Turn(State) + 1, 1, CampaignRules.TotalCases);
            selectedNews = Mathf.Clamp(selectedNews, 0, count - 1);
            SetText("market.banner", "Ανταποκρίσεις · Πρυτανεία " + State.prytany + " · Εβδομάδα " + State.week);
            SetText("market.unread", Enumerable.Range(0, count).Count(t => !City.readNews.Contains(1000 + t)) + " αδιάβαστες");
            var font = Get<TMP_Text>("market.heading")?.font;
            for (int i = weeklyNewsRows.Count; i < count; i++)
            {
                int turn = i;
                var row = CivicUIFactory.Button(weeklyNewsContent, "Weekly news " + turn, "", font, () => { selectedNews = turn; ReadNews(); RenderMarket(); }, this);
                CivicUIFactory.Box((RectTransform)row.transform, 0, i * (weeklyNewsHeight + 12), weeklyNewsWidth, weeklyNewsHeight);
                row.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Left;
                weeklyNewsRows.Add(row); targets["market.row." + i] = row;
            }
            for (int i = 0; i < weeklyNewsRows.Count; i++)
            {
                bool visible = i < count; weeklyNewsRows[i].gameObject.SetActive(visible); if (!visible) continue;
                var definition = CityNewsCase(i);
                var text = weeklyNewsRows[i].GetComponentInChildren<TMP_Text>();
                text.text = definition.title + "\n<size=75%>" + NewsDate(i) + " · " + (City.readNews.Contains(1000 + i) ? "Διαβασμένη" : "Νέα · αδιάβαστη") + "</size>";
                PaintRow("market.row." + i, i == selectedNews);
            }
            weeklyNewsContent.sizeDelta = new Vector2(weeklyNewsContent.sizeDelta.x, count * (weeklyNewsHeight + 12));
            var item = CityNewsCase(selectedNews);
            var record = Campaign.cases.FirstOrDefault(x => x.turn == selectedNews && x.completed);
            SetText("market.kind", NewsDate(selectedNews) + " · " + (selectedNews == count - 1 ? "Τρέχουσα ανταπόκριση" : "Αρχείο ανταποκρίσεων"));
            SetText("market.heading", item.title);
            SetText("market.body", item.brief);
            SetText("market.quote", "Προαναγγελία του εκπαιδευτικού σεναρίου. Οι ομιλητές και το περιστατικό είναι μυθοπλασία.");
            SetText("market.after", record == null ? "Η υπόθεση δεν έχει ολοκληρωθεί ακόμη στη διαδρομή σου." : "Η διαδικασία ολοκληρώθηκε. Το ιστορικό και οι συνέπειες βρίσκονται στον αντίστοιχο θεσμό του χάρτη.");
            SetText("market.read", City.readNews.Contains(1000 + selectedNews) ? "Διαβασμένη · " + NewsDate(selectedNews) : "Αδιάβαστη");
            LayoutWeeklyNewsArticle();
        }

        void PositionCityText(string key, float x, float y, float width, float height, int min, int max)
        {
            var text = Get<TMP_Text>(key);
            if (text == null) return;
            CivicUIFactory.Box(text.rectTransform, x, y, width, height);
            text.enableAutoSizing = true; text.fontSizeMin = min; text.fontSizeMax = max;
            text.textWrappingMode = TextWrappingModes.Normal;
        }

        void LayoutWeeklyNewsArticle()
        {
            PositionCityText("market.unread", 360, 118.8f, 216, 32, 16, 20);
            PositionCityText("market.kind", 628.8f, 128, 936, 32, 18, 21);
            PositionCityText("market.heading", 628.8f, 174, 936, 96, 29, 38);
            PositionCityText("market.body", 628.8f, 286, 936, 240, 22, 26);
            var quote = Get<TMP_Text>("market.quote");
            if (quote != null)
            {
                var box = quote.transform.parent as RectTransform;
                if (box != null) CivicUIFactory.Box(box, 628.8f, 540, 936, 76);
                CivicUIFactory.Box(quote.rectTransform, 26, 12, 884, 52);
                quote.enableAutoSizing = true; quote.fontSizeMin = 18; quote.fontSizeMax = 22;
            }
            PositionCityText("market.after", 628.8f, 634, 936, 66, 19, 23);
            PositionCityText("market.read", 628.8f, 712, 936, 28, 17, 20);
        }

        static string NewsDate(int turn) => "Πρυτανεία " + (turn / CampaignRules.CasesPerPrytany + 1) + " · Εβδομάδα " + (turn % CampaignRules.CasesPerPrytany + 1);

        InstitutionCase CityNewsCase(int turn)
        {
            var record = Campaign.cases.FirstOrDefault(x => x.turn == turn && x.completed);
            return record == null ? CampaignData.cases[turn % CampaignData.cases.Length] : HistoryCase(record);
        }

        void EnsureWeeklyNewsList()
        {
            if (weeklyNewsContent != null) return;
            var first = Get<GameObject>("market.row.0");
            if (first == null) return;
            var r = first.GetComponent<RectTransform>(); weeklyNewsWidth = Mathf.Max(280, r.rect.width); weeklyNewsHeight = Mathf.Max(132, r.rect.height);
            for (int i = 0; i < 4; i++) { var old = Get<GameObject>("market.row." + i); if (old != null) old.SetActive(false); }
            weeklyNewsScroll = r.GetComponentInParent<ScrollRect>();
            if (weeklyNewsScroll != null && weeklyNewsScroll.content != null && r.IsChildOf(weeklyNewsScroll.content)) weeklyNewsContent = weeklyNewsScroll.content;
            else
            {
                weeklyNewsScroll = CivicUIFactory.Scroll(r.parent, "Weekly news archive", out weeklyNewsContent, false);
                var list = (RectTransform)weeklyNewsScroll.transform; list.anchorMin = r.anchorMin; list.anchorMax = r.anchorMax; list.pivot = r.pivot;
                list.anchoredPosition = r.anchoredPosition; list.sizeDelta = new Vector2(weeklyNewsWidth + 32, 4 * (weeklyNewsHeight + 12));
            }
        }

        void RenderWriters()
        {
            var politics = PoliticalCaseRules.Definition(Case, State);
            for (int i = 0; i < CityData.writers.Length; i++)
            {
                var w = CityData.writers[i];
                bool active = City.speechUses > 0 && City.speechWriter == i;
                PaintRow("logographers.card." + i, selectedWriter == i);
                var card = Get<PanelGraphic>("logographers.card." + i);
                if (card != null)
                {
                    if (wiredWriterCards.Add(i))
                    {
                        int writer = i;
                        var select = card.GetComponent<Button>() ?? card.gameObject.AddComponent<Button>(); select.targetGraphic = card;
                        select.transition = Selectable.Transition.None; select.navigation = new Navigation { mode = Navigation.Mode.None };
                        select.onClick.AddListener(() => { selectedWriter = writer; RenderWriters(); });
                    }
                    SetChildText(card.transform, "Name", w.name);
                    SetChildText(card.transform, "Description", w.body);
                    SetChildText(card.transform, "Price", w.price + " οβολοί");
                    int influence = politics == null ? w.bonus : PoliticalCaseRules.WriterInfluence(politics, w.bonus);
                    SetChildText(card.transform, "Bonus", "Επιρροή +" + influence);
                    SetChildText(card.transform, "Perk/Perk label", w.perk);
                    RawImage writerPortrait = card.GetComponentInChildren<RawImage>(true);
                    if (writerPortrait != null)
                        writerPortrait.uvRect = new Rect((w.portrait % 4) * .25f, 1 - (w.portrait / 4 + 1) * .2f, .25f, .2f);
                    var perk = card.transform.Find("Perk/Perk label");
                    if (perk != null)
                        perk.GetComponent<TMP_Text>().color = new Color32(239, 227, 197, 255);
                }

                Button b = Get<Button>("logographers.buy." + i);
                if (b != null)
                {
                    b.interactable = City.speechUses == 0 && State.personalFunds >= w.price;
                    var reference = Get<Button>("logographers.buy.0");
                    if (reference != null && b != reference)
                    {
                        var frame = b.GetComponent<PanelGraphic>(); var original = reference.GetComponent<PanelGraphic>();
                        if (frame != null && original != null) { frame.color = original.color; frame.borderColor = original.borderColor; frame.border = original.border; frame.cut = original.cut; frame.SetVerticesDirty(); }
                        ((RectTransform)b.transform).sizeDelta = ((RectTransform)reference.transform).sizeDelta;
                        b.colors = reference.colors; b.transition = reference.transition;
                    }
                    var t = b.GetComponentInChildren<TMP_Text>();
                    t.color = new Color32(36, 60, 61, 255);
                    t.text = active ? JsonContent.Copy("Copy_907154af23cc95af") : State.personalFunds < w.price ? JsonContent.Copy("Copy_27014bd19c0d887d") : JsonContent.Copy("Copy_b766598ece531f8c") + w.price + " οβ.";
                }
            }

            int shownBonus = politics == null ? City.speechBonus : PoliticalCaseRules.WriterInfluence(politics, City.speechBonus);
            SetText("logographers.status", City.speechUses > 0 ? JsonContent.Copy("Copy_74fa0a67608bb82a") + CityData.writers[City.speechWriter].name + JsonContent.Copy("Copy_3c3a2782a9f1924d") + shownBonus + " · " + City.speechUses + JsonContent.Copy("Copy_e53ef65a97afbaff") : JsonContent.Copy("Copy_71824ea340238d3d"));
        }

        private static void SetChildText(Transform root, string path, string value)
        {
            TMP_Text label = root.Find(path)?.GetComponent<TMP_Text>();
            if (label != null) label.text = value ?? "";
        }

        void BuySpeech(int i)
        {
            var w = CityData.writers[i];
            if (City.speechUses > 0 || State.personalFunds < w.price)
                return;
            City.speechWriter = i;
            City.speechBonus = w.bonus;
            City.speechUses = JsonContent.Settings.speechUses;
            State.personalFunds -= w.price;
            RecordCity("logographer", w.name, -w.price);
            SaveCheckpoint();
            RenderCity();
        }

        // Consume once after a successful speech choice; the caller persists the resulting transaction.
        public int ConsumeSpeechSupport()
        {
            if (State == null || City.speechUses <= 0)
                return 0;
            int bonus = City.speechBonus;
            City.speechUses--;
            return bonus;
        }

        void RenderComplaints()
        {
            selectedComplaint = 0;
            int pending = ComplaintAvailable && string.IsNullOrEmpty(City.complaintDecisions[0]) ? 1 : 0;
            SetText("complaints.banner", ComplaintAvailable ? "Μία καταγγελία · " + pending + " εκκρεμής" : "Δεν υπάρχουν διαθέσιμες καταγγελίες");
            for (int i = 0; i < 3; i++)
            {
                var row = Get<GameObject>("complaints.row." + i);
                if (row != null) row.SetActive(i == 0 && ComplaintAvailable);
                if (i != 0) continue;
                PaintRow("complaints.row." + i, i == selectedComplaint);
                SetText("complaints.title." + i, CityData.complaints[i].title);
                SetText("complaints.meta." + i, "Πρυτανεία 1 · Εβδομάδα 2\n" + ComplaintStatus(i));
            }
            var open = Get<Button>("complaints.primary"); if (open != null) open.interactable = ComplaintAvailable;
            var c = CityData.complaints[selectedComplaint];
            SetText("complaints.heading", ComplaintAvailable ? c.title : "Δεν έχει υποβληθεί ακόμη καταγγελία");
            SetText("complaints.body", ComplaintAvailable ? c.summary : "Η πρώτη καταγγελία γίνεται διαθέσιμη την δεύτερη εβδομάδα. Ο τακτικός λογιστικός έλεγχος δεν εξαρτάται από το αν θα επιλέξεις να την παραπέμψεις.");
            SetText("complaints.state", ComplaintAvailable ? ComplaintStatus(selectedComplaint) : "Χωρίς εκκρεμότητα");
            SetText("complaints.file", ComplaintAvailable ? "Φάκελος 01 · Πρυτανεία 1 / Εβδομάδα 2" : "Αρχείο καταγγελιών");
        }

        bool ComplaintAvailable => CampaignRules.Turn(State) >= 1 && CityData.complaints != null && CityData.complaints.Length > 0;
        string ComplaintStatus(int i) => string.IsNullOrEmpty(City.complaintDecisions[i]) ? JsonContent.Copy("Copy_d799930f31ec453d") : City.complaintDecisions[i];
        void RenderComplaintDetail()
        {
            selectedComplaint = 0;
            var c = CityData.complaints[selectedComplaint];
            SetText("complaint-detail.eyebrow", "Καταγγελία 01 · Πρυτανεία 1 · Εβδομάδα 2");
            SetText("complaint-detail.heading", c.title);
            SetText("complaint-detail.intro", c.intro);
            SetText("complaint-detail.evidence.0", c.evidenceA + "\nΚαταχώριση φακέλου: εβδομάδα 4 · " + c.amountA + " οβ.");
            SetText("complaint-detail.evidence.1", c.evidenceB + "\nΚαταχώριση φακέλου: εβδομάδα 4 · " + c.amountB + " οβ.");
            SetText("complaint-detail.body", c.detail);
            SetText("complaint-detail.note", c.note);
            for (int i = 0; i < 3; i++)
            {
                Button b = Get<Button>("complaint-detail.choice." + i);
                if (b != null)
                {
                    b.gameObject.SetActive(i == 0);
                    b.interactable = ComplaintAvailable;
                    if (i == 0)
                    {
                        var label = b.GetComponentInChildren<TMP_Text>();
                        if (label != null) { label.text = string.IsNullOrEmpty(City.complaintDecisions[0]) ? "Παραπομπή στον φάκελο της Βουλής" : "Στον φάκελο της Βουλής"; label.enableAutoSizing = true; label.fontSizeMin = 18; label.fontSizeMax = 27; }
                    }
                }
            }

            SetText("complaint-detail.status", string.IsNullOrEmpty(City.complaintDecisions[selectedComplaint]) ? JsonContent.Copy("Copy_40ba4a4d8a585b6a") : ComplaintStatus(selectedComplaint));
        }

        void DecideComplaint(int choice)
        {
            if (!ComplaintAvailable || choice != 0) return;
            selectedComplaint = 0;
            if (string.IsNullOrEmpty(City.complaintDecisions[0]))
            {
                City.complaintDecisions[0] = "Παραπέμφθηκε στη Βουλή · έγγραφο στον φάκελο audit";
                RecordCity("complaint-0", "referred-to-boule", 0);
                SaveCheckpoint();
            }
            if (Case.id == "audit" && !CaseRecord.completed) OpenInstitution(1);
            else TryOpenInstitutionHistory(1);
        }

        static string CityNormalize(string value)
        {
            var b = new StringBuilder();
            foreach (char c in (value ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    b.Append(c);
            return b.ToString();
        }

        void RenderOffices()
        {
            EnsureOfficesCatalogue();
            string query = CityNormalize(Get<TMP_InputField>("offices.search")?.text);
            int count = 0;
            for (int i = 0; i < 6; i++)
            {
                var o = CityData.officials[i];
                bool visible = (officeFilter == 0 || o.group == officeFilter) && CityNormalize(o.name + " " + o.office + " " + o.tribe + " " + o.deme).Contains(query);
                GameObject card = Get<GameObject>("offices.row." + i);
                if (card != null)
                {
                    card.SetActive(visible);
                    if (visible)
                    {
                        RectTransform rt = card.transform as RectTransform;
                        rt.anchoredPosition = new Vector2((count % 3) * 434 * 1.2f, -(count / 3) * 310 * 1.2f);
                        count++;
                    }
                }
            }

            var first = Get<GameObject>("offices.row.0");
            if (first != null)
            {
                RectTransform content = first.transform.parent as RectTransform;
                content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(1, Mathf.CeilToInt(count / 3f) * 310 - 23) * 1.2f);
                var sc = content.GetComponentInParent<ScrollRect>();
                Canvas.ForceUpdateCanvases();
                if (sc != null)
                    sc.verticalNormalizedPosition = 1;
            }

            for (int i = 0; i < 4; i++)
                PaintRow("offices.filter." + i, i == officeFilter);
            SetText("offices.count", count + " μυθοπλαστικοί συνομιλητές");
            if (officesCataloguePanel != null && officesCataloguePanel.activeSelf) RenderOfficesCatalogue();
        }

        void EnsureOfficesCatalogue()
        {
            if (officesCatalogueButton != null || !roots.TryGetValue("offices", out var root)) return;
            var asset = Resources.Load<TextAsset>("Content/OfficesCatalogue");
            if (asset != null)
            {
                string text = System.Text.RegularExpressions.Regex.Replace(asset.text, "\"count\"\\s*:\\s*null", "\"count\":0");
                officesCatalogue = JsonUtility.FromJson<CivicOfficesCatalogue>(text);
            }
            var font = Get<TMP_Text>("official-detail.name")?.font ?? Get<TMP_Text>("offices.count")?.font;
            int total = officesCatalogue?.offices?.Length ?? 0;
            officesCatalogueButton = CivicUIFactory.Button(root.transform, "Complete offices catalogue", "Θεσμικός κατάλογος · " + total + " ρόλοι", font, () =>
            {
                if (officesCataloguePanel == null) CreateOfficesCatalogue(font);
                officesCataloguePanel.SetActive(true); RenderOfficesCatalogue();
            }, this);
            var rect = (RectTransform)officesCatalogueButton.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.76f, .89f); rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(550, 64);
            officesCatalogueButton.interactable = total > 0;
        }

        void CreateOfficesCatalogue(TMP_FontAsset font)
        {
            var shape = CivicUIFactory.Panel(roots["offices"].transform, "Full catalogue of civic offices and roles", CivicUIFactory.Teal);
            officesCataloguePanel = shape.gameObject;
            var panel = shape.rectTransform; panel.anchorMin = new Vector2(.035f, .06f); panel.anchorMax = new Vector2(.965f, .94f); panel.offsetMin = panel.offsetMax = Vector2.zero;
            var title = CivicUIFactory.Text(panel, "Catalogue title", "Αρχές, σώματα και δημόσιοι ρόλοι", font, 34, CivicUIFactory.Paper);
            CivicUIFactory.Box(title.rectTransform, 30, 20, 1500, 52);
            var scope = CivicUIFactory.Text(panel, "Catalogue scope", "Τεκμηριωμένος κατάλογος · όχι πλήρης κατάλογος ιστορικά εξακριβωμένων προσώπων. Οι έξι συνομιλητές της πόλης είναι μυθοπλασία.", font, 21, CivicUIFactory.Paper);
            CivicUIFactory.Box(scope.rectTransform, 30, 76, 1650, 58);
            string[] labels = { "Όλες οι κατηγορίες", "Δημόσιες αρχές", "Σώματα της Βουλής", "Λοιποί ρόλοι και υπηρεσίες" };
            for (int i = 0; i < labels.Length; i++)
            {
                int selected = i;
                var tab = CivicUIFactory.Button(panel, "Category " + i, labels[i], font, () => { catalogueCategory = selected; RenderOfficesCatalogue(); }, this);
                CivicUIFactory.Box((RectTransform)tab.transform, 30 + i * 390, 140, 370, 58); catalogueTabs.Add(tab);
            }
            var close = CivicUIFactory.Button(panel, "Close catalogue", "Κλείσιμο", font, () => officesCataloguePanel.SetActive(false), this);
            var closeRect = (RectTransform)close.transform; closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = Vector2.one; closeRect.anchoredPosition = new Vector2(-24, -20); closeRect.sizeDelta = new Vector2(210, 60);
            officesCatalogueScroll = CivicUIFactory.Scroll(panel, "Catalogue entries", out officesCatalogueContent);
            var scrollRect = (RectTransform)officesCatalogueScroll.transform; CivicUIFactory.Fill(scrollRect, new Vector2(26, 24), new Vector2(26, 218));
        }

        void RenderOfficesCatalogue()
        {
            if (officesCatalogue == null || officesCatalogueContent == null) return;
            foreach (Transform child in officesCatalogueContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            foreach (var tab in catalogueTabs) tab.GetComponent<PanelGraphic>().color = catalogueTabs.IndexOf(tab) == catalogueCategory ? new Color32(210, 192, 144, 255) : CivicUIFactory.Paper;
            Canvas.ForceUpdateCanvases();
            float width = Mathf.Max(900, officesCatalogueScroll.viewport.rect.width - 20), y = 10;
            var font = Get<TMP_Text>("official-detail.name")?.font;
            foreach (var item in officesCatalogue.offices.Where(x => catalogueCategory == 0 || catalogueCategory == 1 && x.category == "magistracy" || catalogueCategory == 2 && x.category == "council-subgroup" || catalogueCategory == 3 && x.category != "magistracy" && x.category != "council-subgroup"))
            {
                var row = CivicUIFactory.Panel(officesCatalogueContent, item.id, new Color32(235, 226, 204, 255));
                var name = CivicUIFactory.Text(row.transform, "Name", item.name, font, 28, CivicUIFactory.Ink); name.fontStyle = FontStyles.Bold;
                CivicUIFactory.Box(name.rectTransform, 20, 14, width - 40, 38);
                string category = item.category == "magistracy" ? "Δημόσια αρχή" : item.category == "council-subgroup" ? "Υποσύνολο της Βουλής" : "Συναφής ρόλος/σώμα/υπηρεσία";
                string count = item.count > 0 && item.countStatus == "explicit" ? " · Αριθμός: " + item.count : " · Ο αριθμός δεν γενικεύεται χωρίς τεκμηρίωση";
                string body = category + " · " + item.institution + count + "\nΑνάδειξη: " + item.selection + "\n" + item.role + "\n" + item.notes ;
                var description = CivicUIFactory.Text(row.transform, "Role and primary source", body, font, 23, CivicUIFactory.Ink);
                float height = description.GetPreferredValues(body, width - 40, 0).y + 10;
                CivicUIFactory.Box(description.rectTransform, 20, 58, width - 40, height);
                CivicUIFactory.Box(row.rectTransform, 0, y, width, height + 78); y += height + 96;
            }
            officesCatalogueContent.sizeDelta = new Vector2(0, y); Canvas.ForceUpdateCanvases(); officesCatalogueScroll.verticalNormalizedPosition = 1;
        }

        void RenderOfficial()
        {
            var o = CityData.officials[selectedOfficial];
            SetText("official-detail.relationship", City.officialApproaches[selectedOfficial] == 0 ? JsonContent.Copy("Copy_ba2f3162cc86a84e") : City.officialSupport[selectedOfficial] > 0 ? JsonContent.Copy("Copy_63275b04f9ff9fb7") : JsonContent.Copy("Copy_7321982a1b882482"));
            SetText("official-detail.name", o.name);
            SetText("official-detail.office", o.office.ToUpperInvariant());
            var officeLabel = Get<TMP_Text>("official-detail.office");
            if (officeLabel != null) { officeLabel.enableAutoSizing = true; officeLabel.fontSizeMin = 10; officeLabel.fontSizeMax = 16; officeLabel.textWrappingMode = TextWrappingModes.NoWrap; }
            SetText("official-detail.origin", o.tribe + "\nΤριττύς: " + o.trittys + " · Δήμος: " + o.deme);
            SetText("official-detail.method", o.method);
            SetText("official-detail.influence", "Στήριξη για 5 εβδομάδες.\nΕπανάληψη μετά τη λήξη.");
            PositionCityText("official-detail.influence", 786, 122, 270, 50, 17, 20);
            LayoutOfficialSummaryRows();
            SetText("official-detail.priority", o.priority);
            SetText("official-detail.access", City.supportUntilTurn[selectedOfficial] <= CampaignRules.Turn(State) ? JsonContent.Copy("Copy_aaf8784979b7165d") : JsonContent.Copy("Copy_5d6a15042eef889c"));
            RawImage portrait = Get<RawImage>("official-detail.portrait");
            if (portrait != null)
                portrait.uvRect = new Rect((o.portrait % 4) * .25f, 1 - (o.portrait / 4 + 1) * .2f, .25f, .2f);
            Button p = Get<Button>("official-detail.persuade"), b = Get<Button>("official-detail.bribe");
            bool available = City.officialApproaches[selectedOfficial] == 0 || City.supportUntilTurn[selectedOfficial] <= CampaignRules.Turn(State);
            if (p != null)
                p.interactable = available;
            if (b != null)
            {
                b.interactable = available && State.personalFunds >= JsonContent.Settings.bribePrice;
                b.GetComponentInChildren<TMP_Text>().text = (confirmBribe ? "Επιβεβαίωσε προσφορά" : "Εξέτασε προσφορά") + " · " + JsonContent.Settings.bribePrice + " οβ.";
            }

            SetText("official-detail.result", available ? (confirmBribe ? JsonContent.Copy("Copy_df0affe0d1e2f2df") : JsonContent.Copy("Copy_1d3d53e5a1e5a7f2")) : (City.officialSupport[selectedOfficial] > 0 ? JsonContent.Copy("Copy_f93c7d5843c765cc") : JsonContent.Copy("Copy_2ffdcc0db1feecc7")));
        }

        void LayoutOfficialSummaryRows()
        {
            var influence = Get<TMP_Text>("official-detail.influence");
            if (influence == null) return;
            var content = influence.transform.parent;
            for (int i = 0; i < 4; i++)
            {
                var rule = content.Find("Rule " + i) as RectTransform;
                if (rule != null) CivicUIFactory.Box(rule, i % 2 == 0 ? 0 : 540, i < 2 ? 176 : 230, 516, 1.2f);
                if (i < 2) continue;
                var label = content.Find("Label " + i) as RectTransform;
                if (label != null) CivicUIFactory.Box(label, i == 2 ? 0 : 540, 190, 240, 36);
            }
            var priority = Get<TMP_Text>("official-detail.priority");
            var access = Get<TMP_Text>("official-detail.access");
            if (priority != null) CivicUIFactory.Box(priority.rectTransform, 246, 190, 270, 36);
            if (access != null) CivicUIFactory.Box(access.rectTransform, 786, 190, 270, 36);
        }

        void ApproachOfficial(bool bribe)
        {
            if (City.officialApproaches[selectedOfficial] != 0 && City.supportUntilTurn[selectedOfficial] > CampaignRules.Turn(State))
                return;
            if (bribe && !confirmBribe)
            {
                confirmBribe = true;
                RenderOfficial();
                return;
            }

            if (bribe && State.personalFunds < JsonContent.Settings.bribePrice)
                return;
            var official = CityData.officials[selectedOfficial];
            var result=DecisionRules.Official(State,bribe,official.integrity,official.persuasionThreshold);
            City.officialApproaches[selectedOfficial] = bribe ? 2 : 1;
            City.officialSupport[selectedOfficial] = result.supported ? 1 : 0;
            City.supportUntilTurn[selectedOfficial] = CampaignRules.Turn(State)+5;
            State.personalFunds += result.personalDelta;
            City.corruptionRisk += result.riskDelta;
            RecordCity("official-" + selectedOfficial, bribe ? "private-offer" : "public-argument", result.personalDelta, DecisionRules.Effects(State,bribe ? 2 : 3,bribe ? 1 : 0,success:result.supported));
            SaveCheckpoint();
            confirmBribe = false;
            RenderCity();
        }

        void RecordCity(string step, string choice, int funds, CharacterMetrics effects = null)
        {
            State.choices.Add(new ChoiceRecord { stepId = "city-" + step + "-" + State.choices.Count, choiceId = choice, metrics = effects ?? new CharacterMetrics(), personalFunds = funds, institutionalLean = "mixed" });
            GameRules.Recalculate(State);
            SetStatus(MetricDeltas(effects));
        }

        void PaintRow(string key, bool selected)
        {
            var shape = Get<PanelGraphic>(key);
            if (shape == null)
                return;
            shape.color = selected ? new Color32(35, 62, 68, 255) : new Color32(216, 204, 176, 255);
            foreach (var t in shape.GetComponentsInChildren<TMP_Text>())
                t.color = selected ? new Color32(234, 223, 197, 255) : new Color32(41, 67, 69, 255);
        }
    }
}

