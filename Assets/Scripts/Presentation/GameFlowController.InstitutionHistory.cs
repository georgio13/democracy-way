using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    public sealed partial class GameFlowController
    {
        GameObject institutionArchivePanel;
        TMP_Text institutionArchiveTitle, institutionArchiveDate, institutionArchiveDevelopment, institutionArchiveBody;
        RectTransform institutionArchiveList, institutionArchiveDetail;
        ScrollRect institutionArchiveListScroll, institutionArchiveDetailScroll;
        int selectedInstitutionRecord = -1;
        readonly List<Button> institutionArchiveButtons = new List<Button>();
        const string HistoryTurnReference = "@case-turn:";
        CampaignState compactedHistoryCampaign;

        void CaptureCaseHistorySnapshot()
        {
            CompactCaseHistorySnapshots();
            var record = CaseRecord;
            if (!string.IsNullOrWhiteSpace(record.authoredCaseSnapshot)) return;
            if (record.completed || record.speech >= 0) return; // A newly installed version cannot reconstruct an old, already spoken script.
            record.caseId = Case.id;
            string snapshot = JsonUtility.ToJson(Case);
            var same = Campaign.cases.FirstOrDefault(x => x.turn < record.turn && x.authoredCaseSnapshot == snapshot);
            record.authoredCaseSnapshot = same == null ? snapshot : HistoryTurnReference + same.turn;
            // The case snapshot contains the authored arguments; do not duplicate a transcript.
            record.authoredDialogueSnapshot = "";
        }

        void CompactCaseHistorySnapshots()
        {
            if (ReferenceEquals(compactedHistoryCampaign, Campaign)) return;
            compactedHistoryCampaign = Campaign;
            var originals = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var record in Campaign.cases.OrderBy(x => x.turn))
            {
                if (!TryReadHistorySnapshot(record, out var authored)) continue;
                string snapshot = JsonUtility.ToJson(authored);
                if (originals.TryGetValue(snapshot, out int earlierTurn))
                    record.authoredCaseSnapshot = HistoryTurnReference + earlierTurn;
                else
                {
                    originals.Add(snapshot, record.turn);
                    record.authoredCaseSnapshot = snapshot;
                }
                record.authoredDialogueSnapshot = "";
            }
        }

        bool TryReadHistorySnapshot(CaseRecord record, out InstitutionCase authored)
        {
            authored = null;
            if (record == null) return false;
            // References only point backwards to another record kept in this save, never to mutable published content.
            var original = record;
            for (int remaining = Campaign.cases.Count; remaining >= 0; remaining--)
            {
                string value = original.authoredCaseSnapshot;
                if (string.IsNullOrWhiteSpace(value)) return false;
                if (!value.StartsWith(HistoryTurnReference, StringComparison.Ordinal))
                {
                    try { authored = JsonUtility.FromJson<InstitutionCase>(value); }
                    catch (ArgumentException) { return false; }
                    if (authored == null) return false;
                    // Earlier saves had an explicit dialogue field. Preserve it even if the nested copy differs.
                    if (authored != null) JsonContent.HydrateCase(authored);
                    return true;
                }
                if (!int.TryParse(value.Substring(HistoryTurnReference.Length), out int turn) || turn >= original.turn) return false;
                original = Campaign.cases.FirstOrDefault(x => x.turn == turn);
                if (original == null) return false;
            }
            return false;
        }

        bool TryOpenInstitutionHistory(int site)
        {
            historySite = Mathf.Clamp(site, 0, 4);
            selectedInstitutionRecord = -1;
            GoCampaign("institution-history");
            return true;
        }

        InstitutionCase HistoryCase(CaseRecord record)
        {
            if (TryReadHistorySnapshot(record, out var authored)) return authored;
            return CampaignData.cases.FirstOrDefault(x => x.id == record.caseId) ?? CampaignData.cases[record.turn % CampaignData.cases.Length];
        }

        static string CivicInstitutionName(int site)
        {
            string[] names = { "Εκκλησία του Δήμου", "Βουλή των Πεντακοσίων", "Άρειος Πάγος", "Ηλιαία και δικαστικοί φάκελοι", "Συνέλευση των δημοτών" };
            return names[Mathf.Clamp(site, 0, names.Length - 1)];
        }

        string InstitutionDevelopment()
        {
            var last = Campaign.cases.Where(x => x.completed && HistoryCase(x).site == historySite).OrderByDescending(x => x.turn).FirstOrDefault();
            if (historySite == 1 && ComplaintAvailable && !string.IsNullOrEmpty(City.complaintDecisions[0]))
                return "Η καταγγελία για τη διπλή καταχώριση ξυλείας έχει καταχωριστεί για τον φάκελο της Βουλής. Η παραπομπή δεν αποτελεί από μόνη της τελική κρίση ενοχής.";
            if (last != null)
            {
                var c = HistoryCase(last);
                return "Τελευταία καταγεγραμμένη εξέλιξη · " + NewsDate(last.turn) + ": «" + c.title + "». " + (c.observation ? "Ο φάκελος παρακολουθήθηκε ως διαδικασία." : "Το σώμα ολοκλήρωσε τη συλλογική του κρίση.");
            }
            int current = CampaignRules.Turn(State);
            for (int turn = current; turn < CampaignRules.TotalCases; turn++)
            {
                var upcoming = CampaignData.cases.First(c => c.id == CampaignRules.CaseIdForTurn(turn));
                if (upcoming.site != historySite) continue;
                return "Προετοιμασία εκπαιδευτικού φακέλου · " + NewsDate(turn) + ": «" + upcoming.title + "». Η αναγγελία δεν προεξοφλεί την απόφαση ή τη δικαστική κρίση.";
            }
            return "Δεν υπάρχει νέα κεντρική υπόθεση για αυτόν τον θεσμό στην υπόλοιπη εκπαιδευτική διαδρομή. Οι ήδη ολοκληρωμένες διαδικασίες παραμένουν διαθέσιμες στο αρχείο.";
        }

        void RenderInstitutionHistoryPanel()
        {
            if (institutionArchivePanel == null) CreateInstitutionHistoryPanel();
            if (institutionArchivePanel == null) return;
            institutionArchivePanel.SetActive(true);
            institutionArchiveTitle.text = CivicInstitutionName(historySite);
            institutionArchiveDate.text = NewsDate(CampaignRules.Turn(State));
            institutionArchiveDevelopment.text = InstitutionDevelopment();
            var records = Campaign.cases.Where(x => x.completed && HistoryCase(x).site == historySite).OrderByDescending(x => x.turn).ToArray();
            if (records.Length == 0) selectedInstitutionRecord = -1;
            else if (!records.Any(x => x.turn == selectedInstitutionRecord)) selectedInstitutionRecord = records[0].turn;
            foreach (var b in institutionArchiveButtons) { b.gameObject.SetActive(false); Destroy(b.gameObject); }
            institutionArchiveButtons.Clear();
            Canvas.ForceUpdateCanvases();
            float width = Mathf.Max(300, institutionArchiveListScroll.viewport.rect.width - 12);
            var font = institutionArchiveTitle.font;
            for (int i = 0; i < records.Length; i++)
            {
                var record = records[i];
                var c = HistoryCase(record);
                var button = CivicUIFactory.Button(institutionArchiveList, "Completed case " + record.turn,
                    c.title + "\n<size=75%>" + NewsDate(record.turn) + "</size>", font, () => { selectedInstitutionRecord = record.turn; RenderInstitutionHistoryPanel(); }, this);
                CivicUIFactory.Box((RectTransform)button.transform, 0, i * 136, width, 120);
                button.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Left;
                button.GetComponent<PanelGraphic>().color = record.turn == selectedInstitutionRecord ? CivicUIFactory.Gold : CivicUIFactory.Paper;
                institutionArchiveButtons.Add(button);
            }
            institutionArchiveList.sizeDelta = new Vector2(0, Mathf.Max(1, records.Length) * 136);
            var selected = records.FirstOrDefault(x => x.turn == selectedInstitutionRecord);
            institutionArchiveBody.text = selected == null ? "Δεν έχεις ολοκληρώσει ακόμη διαδικασία εδώ.\n\n" + InstitutionDevelopment() : InstitutionHistoryText(selected);
            float bodyWidth = Mathf.Max(500, institutionArchiveDetailScroll.viewport.rect.width - 32);
            float height = institutionArchiveBody.GetPreferredValues(institutionArchiveBody.text, bodyWidth, 0).y + 32;
            CivicUIFactory.Box(institutionArchiveBody.rectTransform, 16, 10, bodyWidth, height);
            institutionArchiveDetail.sizeDelta = new Vector2(0, height + 24);
            Canvas.ForceUpdateCanvases(); institutionArchiveDetailScroll.verticalNormalizedPosition = 1;
        }

        string InstitutionHistoryText(CaseRecord record)
        {
            var c = HistoryCase(record);
            var result = new StringBuilder();
            result.Append("<b>").Append(c.title).Append("</b>\n").Append(NewsDate(record.turn)).Append("\n\n");
            bool exact = TryReadHistorySnapshot(record, out _);
            if (!exact) result.Append("<i>Η παλιά αποθήκευση δεν περιέχει το αρχικό πλήρες κείμενο. Εμφανίζονται η καταγεγραμμένη επιλογή και η τρέχουσα σύντομη περιγραφή.</i>\n\n");
            result.Append(c.brief).Append("\n\n");
            PoliticalCaseDefinition p = null;
            if (exact) p = c.politics;
            if (p != null)
            {
                foreach (var line in p.dialogue)
                    result.Append("<i>").Append(line.stageText).Append("</i>\n<b>").Append(line.speaker).Append("</b>\n").Append(line.text).Append("\n\n");
                if (record.speech >= 0 && record.speech < 2 && record.speechApproach >= 0 && record.speechApproach < p.approaches.Length)
                {
                    var a = p.approaches[record.speechApproach];
                    result.Append("<b>Η δική σου παρέμβαση</b>\n").Append(record.speech == 0 ? a.textForA : a.textForB).Append("\n\n");
                }
            }
            else if (exact) result.Append("<b>").Append(c.speakerA).Append("</b>\n").Append(c.argumentA).Append("\n\n<b>").Append(c.speakerB).Append("</b>\n").Append(c.argumentB).Append("\n\n");
            result.Append("<b>Η καταγεγραμμένη επιλογή σου</b>\n").Append(record.vote == 2 ? "Αποχή παλιάς αποθήκευσης" : record.vote == 0 ? p?.choiceA ?? "Πρώτη πρόταση / εκτίμηση" : p?.choiceB ?? "Δεύτερη πρόταση / εκτίμηση").Append("\n\n");
            if (p != null) result.Append("<b>Συλλογική έκβαση</b>\n").Append(record.majority == 0 ? p.outcomeA : p.outcomeB).Append("\n\n");
            else result.Append("<b>Καταγεγραμμένη έκβαση</b>\n").Append(record.majority == 0 ? "Επικράτησε η πρώτη πρόταση/εκτίμηση." : "Επικράτησε η δεύτερη πρόταση/εκτίμηση.").Append("\n\n");
            result.Append("Ταμείο πόλης: ").Append(Signed(record.cityDelta)).Append(" οβ. · Προσωπικό: ").Append(Signed(record.personalDelta)).Append(" οβ.\n").Append(MetricDeltas(record.metricDelta));
            result.Append("\n\nΠηγή θεσμικής τεκμηρίωσης: ").Append(record.sourceRef ?? c.source);
            return result.ToString();
        }

        void CreateInstitutionHistoryPanel()
        {
            if (!roots.TryGetValue("institution-history", out var root)) return;
            var template = Get<TMP_Text>("institution-history.body");
            TMP_FontAsset font = template?.font ?? Get<TMP_Text>("deliberation.argument.0")?.font;
            foreach (Transform child in root.transform) if (child.GetComponent<RawImage>() == null) child.gameObject.SetActive(false);
            var panel = CivicUIFactory.Panel(root.transform, "Institution archive", CivicUIFactory.Teal);
            institutionArchivePanel = panel.gameObject;
            panel.rectTransform.anchorMin = new Vector2(.035f, .06f); panel.rectTransform.anchorMax = new Vector2(.965f, .94f); panel.rectTransform.offsetMin = panel.rectTransform.offsetMax = Vector2.zero;
            institutionArchiveTitle = CivicUIFactory.Text(panel.transform, "Institution name", "", font, 36, CivicUIFactory.Paper); LayoutInstitutionArchiveHeading(institutionArchiveTitle, 20, 58);
            institutionArchiveDate = CivicUIFactory.Text(panel.transform, "Current week", "", font, 21, CivicUIFactory.Gold); LayoutInstitutionArchiveHeading(institutionArchiveDate, 80, 36);
            institutionArchiveDevelopment = CivicUIFactory.Text(panel.transform, "Current development", "", font, 23, CivicUIFactory.Paper); LayoutInstitutionArchiveHeading(institutionArchiveDevelopment, 122, 102);
            institutionArchiveDevelopment.enableAutoSizing = true; institutionArchiveDevelopment.fontSizeMin = 19; institutionArchiveDevelopment.fontSizeMax = 23;
            institutionArchiveListScroll = CivicUIFactory.Scroll(panel.transform, "Completed procedures", out institutionArchiveList);
            var left = (RectTransform)institutionArchiveListScroll.transform; left.anchorMin = new Vector2(0, .12f); left.anchorMax = new Vector2(.30f, 1); left.offsetMin = new Vector2(26, 0); left.offsetMax = new Vector2(-14, -240);
            institutionArchiveDetailScroll = CivicUIFactory.Scroll(panel.transform, "Exact authored history", out institutionArchiveDetail);
            var right = (RectTransform)institutionArchiveDetailScroll.transform; right.anchorMin = new Vector2(.30f, .12f); right.anchorMax = Vector2.one; right.offsetMin = new Vector2(8, 0); right.offsetMax = new Vector2(-26, -240);
            institutionArchiveBody = CivicUIFactory.Text(institutionArchiveDetail, "Procedure and consequence record", "", font, 24, CivicUIFactory.Ink);
            var back = CivicUIFactory.Button(panel.transform, "Back to city map", "Πίσω στον χάρτη", font, () => GoCampaign("map-stela"), this);
            var b = (RectTransform)back.transform; b.anchorMin = b.anchorMax = new Vector2(.5f, .055f); b.pivot = new Vector2(.5f, .5f); b.anchoredPosition = Vector2.zero; b.sizeDelta = new Vector2(430, 68);
            targets["institution-history.back"] = back;
            targets["institution-history.primary"] = back;
        }

        static void LayoutInstitutionArchiveHeading(TMP_Text text, float top, float height)
        {
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, -top); rect.sizeDelta = new Vector2(-60, height);
        }
    }
}
