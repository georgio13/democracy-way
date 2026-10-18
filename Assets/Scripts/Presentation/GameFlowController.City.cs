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
        public TextAsset cityContent;
        CityContent cityData;
        int selectedNews, selectedComplaint, selectedOfficial, officeFilter;
        bool confirmBribe;
        CityContent CityData
        {
            get
            {
                if (cityData == null && cityContent != null)
                    cityData = JsonUtility.FromJson<CityContent>(cityContent.text);
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
                Bind("market.row." + i, () =>
                {
                    selectedNews = n;
                    ReadNews();
                    RenderCity();
                });
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

            Bind("complaints.primary", () => Show("complaint-detail"));
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
                ReadNews();
            confirmBribe = false;
            Show(screen);
        }

        void ReadNews()
        {
            if (!City.readNews.Contains(selectedNews))
            {
                City.readNews.Add(selectedNews);
                SaveCheckpoint();
            }
        }

        void RenderCityBadges()
        {
            if (CityData == null)
                return;
            int unread = CityData.news.Length - City.readNews.Count;
            int pending = City.complaintDecisions.Count(string.IsNullOrEmpty);
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
            SetText(currentScreen + ".funds", ArticyNarrative.Copy("Copy_9fd25730f3bc1ab9") + State.cityFunds.ToString("N0") + ArticyNarrative.Copy("Copy_226a8ac686f3de22") + State.personalFunds.ToString("N0") + ArticyNarrative.Copy("Copy_6cbc6fd72c7bdc12"));
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
            SetText("market.banner", State.prytany + ArticyNarrative.Copy("Copy_7b64b1f0928447bd") + State.week);
            SetText("market.unread", (CityData.news.Length - City.readNews.Count) + ArticyNarrative.Copy("Copy_8aabdbb554a8d426"));
            for (int i = 0; i < CityData.news.Length; i++)
            {
                var n = CityData.news[i];
                bool chosen = i == selectedNews, read = City.readNews.Contains(i);
                PaintRow("market.row." + i, chosen);
                SetText("market.title." + i, n.title);
                SetText("market.date." + i, ArticyNarrative.Copy("Copy_3350d9c87162d943") + State.week + " · " + (read ? ArticyNarrative.Copy("Copy_4fca06624535968d") : ArticyNarrative.Copy("Copy_0ab2faa0e2fded67")));
                SetText("market.meta." + i, n.origin + " · " + n.kind);
            }

            var item = CityData.news[selectedNews];
            SetText("market.kind", item.kind + "   ·   " + State.prytany + ArticyNarrative.Copy("Copy_549e01fd9cc8fa66") + State.week + ArticyNarrative.Copy("Copy_8c2d2cc8b8586d1f"));
            SetText("market.heading", item.title);
            SetText("market.body", item.body);
            SetText("market.quote", item.quote);
            SetText("market.after", item.after);
            SetText("market.read", ArticyNarrative.Copy("Copy_fdfa0491ed3081aa"));
        }

        void RenderWriters()
        {
            for (int i = 0; i < CityData.writers.Length; i++)
            {
                var w = CityData.writers[i];
                bool active = City.speechUses > 0 && City.speechWriter == i;
                PaintRow("logographers.card." + i, City.speechUses > 0 ? active : i == 1);
                var card = Get<PanelGraphic>("logographers.card." + i);
                if (card != null)
                {
                    SetChildText(card.transform, "Name", w.name);
                    SetChildText(card.transform, "Description", w.body);
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
                    var t = b.GetComponentInChildren<TMP_Text>();
                    t.color = new Color32(36, 60, 61, 255);
                    t.text = active ? ArticyNarrative.Copy("Copy_907154af23cc95af") : State.personalFunds < w.price ? ArticyNarrative.Copy("Copy_27014bd19c0d887d") : ArticyNarrative.Copy("Copy_b766598ece531f8c") + w.price + ArticyNarrative.Copy("Copy_6cbc6fd72c7bdc12");
                }
            }

            SetText("logographers.status", City.speechUses > 0 ? ArticyNarrative.Copy("Copy_74fa0a67608bb82a") + CityData.writers[City.speechWriter].name + ArticyNarrative.Copy("Copy_3c3a2782a9f1924d") + City.speechBonus + " · " + City.speechUses + ArticyNarrative.Copy("Copy_e53ef65a97afbaff") : ArticyNarrative.Copy("Copy_71824ea340238d3d"));
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
            City.speechUses = ArticyNarrative.DefaultNumber("SpeechUses");
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
            int pending = City.complaintDecisions.Count(string.IsNullOrEmpty);
            SetText("complaints.banner", pending + ArticyNarrative.Copy("Copy_c77cdd99f9f7bd37"));
            for (int i = 0; i < 3; i++)
            {
                PaintRow("complaints.row." + i, i == selectedComplaint);
                SetText("complaints.title." + i, CityData.complaints[i].title);
                SetText("complaints.meta." + i, State.prytany + ArticyNarrative.Copy("Copy_b5bda75af0b99acf") + State.week + "\n" + ComplaintStatus(i));
            }

            var c = CityData.complaints[selectedComplaint];
            SetText("complaints.heading", selectedComplaint == 0 ? ArticyNarrative.Copy("Copy_e91b3eaa7987884f") : c.title);
            SetText("complaints.body", c.summary);
            SetText("complaints.state", ComplaintStatus(selectedComplaint));
            SetText("complaints.file", ArticyNarrative.Copy("Copy_2d2067f76dad3c34") + (selectedComplaint + 1).ToString("00"));
        }

        string ComplaintStatus(int i) => string.IsNullOrEmpty(City.complaintDecisions[i]) ? ArticyNarrative.Copy("Copy_d799930f31ec453d") : City.complaintDecisions[i];
        void RenderComplaintDetail()
        {
            var c = CityData.complaints[selectedComplaint];
            SetText("complaint-detail.eyebrow", ArticyNarrative.Copy("Copy_2d2067f76dad3c34") + (selectedComplaint + 1).ToString("00") + ArticyNarrative.Copy("Copy_238d1efb10f3eefa") + State.week);
            SetText("complaint-detail.heading", c.title);
            SetText("complaint-detail.intro", c.intro);
            SetText("complaint-detail.evidence.0", c.evidenceA + ArticyNarrative.Copy("Copy_f270d4df3385c44f") + State.week + ArticyNarrative.Copy("Copy_d2207d27f6999a61") + c.amountA + ArticyNarrative.Copy("Copy_642a95fab3b74c87"));
            SetText("complaint-detail.evidence.1", c.evidenceB + ArticyNarrative.Copy("Copy_f270d4df3385c44f") + State.week + ArticyNarrative.Copy("Copy_d2207d27f6999a61") + c.amountB + ArticyNarrative.Copy("Copy_642a95fab3b74c87"));
            SetText("complaint-detail.body", c.detail);
            SetText("complaint-detail.note", c.note);
            for (int i = 0; i < 3; i++)
            {
                Button b = Get<Button>("complaint-detail.choice." + i);
                if (b != null)
                    b.interactable = string.IsNullOrEmpty(City.complaintDecisions[selectedComplaint]);
            }

            SetText("complaint-detail.status", string.IsNullOrEmpty(City.complaintDecisions[selectedComplaint]) ? ArticyNarrative.Copy("Copy_40ba4a4d8a585b6a") : ComplaintStatus(selectedComplaint));
        }

        void DecideComplaint(int choice)
        {
            if (!string.IsNullOrEmpty(City.complaintDecisions[selectedComplaint]))
                return;
            string[] decisions =
            {
                ArticyNarrative.Copy("Copy_288257cd77248586"),
                ArticyNarrative.Copy("Copy_d049ab86afc865a1"),
                ArticyNarrative.Copy("Copy_0faeede5a911bca4")
            };
            City.complaintDecisions[selectedComplaint] = decisions[choice];
            RecordCity("complaint-" + selectedComplaint, "recommendation-" + choice, 0);
            SaveCheckpoint();
            RenderCity();
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
            SetText("offices.count", count + ArticyNarrative.Copy("Copy_97debce937837a2a"));
        }

        void RenderOfficial()
        {
            var o = CityData.officials[selectedOfficial];
            SetText("official-detail.relationship", City.officialApproaches[selectedOfficial] == 0 ? ArticyNarrative.Copy("Copy_ba2f3162cc86a84e") : City.officialSupport[selectedOfficial] > 0 ? ArticyNarrative.Copy("Copy_63275b04f9ff9fb7") : ArticyNarrative.Copy("Copy_7321982a1b882482"));
            SetText("official-detail.name", o.name);
            SetText("official-detail.office", o.office.ToUpperInvariant());
            SetText("official-detail.origin", o.tribe + ArticyNarrative.Copy("Copy_9da88ba17913653a") + o.trittys + ArticyNarrative.Copy("Copy_82faa4beeeaf83cb") + o.deme);
            SetText("official-detail.method", o.method);
            SetText("official-detail.influence", o.influence);
            SetText("official-detail.priority", o.priority);
            SetText("official-detail.access", City.officialApproaches[selectedOfficial] == 0 ? ArticyNarrative.Copy("Copy_aaf8784979b7165d") : ArticyNarrative.Copy("Copy_5d6a15042eef889c"));
            RawImage portrait = Get<RawImage>("official-detail.portrait");
            if (portrait != null)
                portrait.uvRect = new Rect((o.portrait % 4) * .25f, 1 - (o.portrait / 4 + 1) * .2f, .25f, .2f);
            Button p = Get<Button>("official-detail.persuade"), b = Get<Button>("official-detail.bribe");
            bool available = City.officialApproaches[selectedOfficial] == 0;
            if (p != null)
                p.interactable = available;
            if (b != null)
            {
                b.interactable = available && State.personalFunds >= ArticyNarrative.DefaultNumber("BribePrice");
                b.GetComponentInChildren<TMP_Text>().text = confirmBribe ? ArticyNarrative.Copy("Copy_177af8892ab719cf") : ArticyNarrative.Copy("Copy_90510655f09b86a2");
            }

            SetText("official-detail.result", available ? (confirmBribe ? ArticyNarrative.Copy("Copy_df0affe0d1e2f2df") : ArticyNarrative.Copy("Copy_1d3d53e5a1e5a7f2")) : (City.officialSupport[selectedOfficial] > 0 ? ArticyNarrative.Copy("Copy_f93c7d5843c765cc") : ArticyNarrative.Copy("Copy_2ffdcc0db1feecc7")));
        }

        void ApproachOfficial(bool bribe)
        {
            if (City.officialApproaches[selectedOfficial] != 0)
                return;
            if (bribe && !confirmBribe)
            {
                confirmBribe = true;
                RenderOfficial();
                return;
            }

            if (bribe && State.personalFunds < ArticyNarrative.DefaultNumber("BribePrice"))
                return;
            var official = CityData.officials[selectedOfficial];
            using var result=ArticyNarrative.Run("Rule_Official",State,("Bribe",bribe),("Integrity",official.integrity),("Threshold",official.persuasionThreshold));
            City.officialApproaches[selectedOfficial] = bribe ? 2 : 1;
            City.officialSupport[selectedOfficial] = result.Flag("Supported") ? 1 : 0;
            State.personalFunds += result.Number("PersonalDelta");
            City.corruptionRisk += result.Number("RiskDelta");
            RecordCity("official-" + selectedOfficial, bribe ? "private-offer" : "public-argument", result.Number("PersonalDelta"));
            SaveCheckpoint();
            confirmBribe = false;
            RenderCity();
        }

        void RecordCity(string step, string choice, int funds)
        {
            State.choices.Add(new ChoiceRecord { stepId = "city-" + step + "-" + State.choices.Count, choiceId = choice, metrics = new CharacterMetrics(), personalFunds = funds, institutionalLean = "mixed" });
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
