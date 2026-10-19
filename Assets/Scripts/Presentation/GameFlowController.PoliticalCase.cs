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
        ScrollRect politicalTranscript;
        RectTransform politicalTranscriptContent;
        GameObject[] legacyArgumentCards;
        Button politicalNext, politicalAsk;
        string renderedPoliticalDialogue;
        readonly System.Collections.Generic.Dictionary<TMP_Text, string> originalResultCaptions = new System.Collections.Generic.Dictionary<TMP_Text, string>();

        bool PoliticalSpeech(int selection)
        {
            var p = PoliticalCaseRules.Definition(Case, State);
            if (p == null) return false;
            if (CaseRecord.speech >= 0) { FollowStory("SpeechContinue"); return true; }
            if (CaseRecord.pendingSpeechSide < 0 && selection < 2)
            {
                CaseRecord.pendingSpeechSide = selection;
                SaveCheckpoint();
                RenderCampaign();
                return true;
            }
            int side = CaseRecord.pendingSpeechSide < 0 ? 2 : CaseRecord.pendingSpeechSide;
            int approach = side == 2 ? -1 : selection;
            int bonus = side < 2 && City.speechUses > 0 ? City.speechBonus : 0;
            var before = State.metrics.Copy();
            if (!CampaignRules.Speak(State, side, bonus, Case, approach)) return true;
            if (side < 2 && City.speechUses > 0) ConsumeSpeechSupport();
            FollowStory("SpeechContinue");
            SetStatus(MetricDeltas(CampaignRules.Difference(before, State.metrics)));
            return true;
        }

        bool RenderPoliticalSpeech()
        {
            var p = PoliticalCaseRules.Definition(Case, State);
            if (p == null) return false;
            bool choosingApproach = CaseRecord.pendingSpeechSide >= 0;
            CampaignText("heading", choosingApproach ? "Πώς θα στηρίξεις τη θέση σου;" : "Ζητάς τον λόγο");
            CampaignText("body", choosingApproach ? "Επιλέγεις τρόπο παρέμβασης. Οι σχετικοί δείκτες καθορίζουν την επιρροή του λόγου σου· η ενίσχυση λογογράφου προστίθεται μόνο αν μιλήσεις."
                : "Ποια πρόταση θα υποστηρίξεις δημόσια; Η ομιλία είναι ξεχωριστή από την τελική σου ψήφο.");
            var details = new StringBuilder();
            for (int i = 0; i < 3; i++)
            {
                CampaignLabel("choice." + i, choosingApproach ? p.approaches[i].label : i == 0 ? p.choiceA : i == 1 ? p.choiceB : "Δεν ζητώ τον λόγο");
                if (choosingApproach) details.Append(p.approaches[i].label).Append(": ").Append(MetricDeltas(p.approaches[i].metrics)).Append('\n');
                var button = Get<Button>("speech.choice." + i);
                var text = button?.GetComponentInChildren<TMP_Text>();
                if (text != null) { text.enableAutoSizing = true; text.fontSizeMin = 20; text.fontSizeMax = 28; }
            }
            CampaignText("role", choosingApproach ? details.ToString().TrimEnd() : "Τεκμηρίωση: Αυθεντία και Πειθώ\nΔημόσια δέσμευση: Συνέπεια, Ηθική και Πειθώ\nΟξεία αντιπαράθεση: Παρρησία και Αναγνωρισιμότητα");
            FitPoliticalText("speech.role", 20, 23);
            CampaignText("source", "Οι αριθμοί επιρροής είναι μονάδες εκπαιδευτικής προσομοίωσης. Το αρμόδιο σώμα αποφασίζει συλλογικά.");
            return true;
        }

        bool RenderPoliticalVote()
        {
            var abstention = Get<Button>("vote.choice.2");
            if (abstention != null) abstention.gameObject.SetActive(false);
            var p = PoliticalCaseRules.Definition(Case, State);
            if (p == null) return false;
            CampaignText("heading", "Η συλλογική απόφαση");
            string statement = "Δεν τοποθετήθηκες δημόσια.";
            if (CaseRecord.speech < 2 && CaseRecord.speech >= 0 && CaseRecord.speechApproach >= 0)
            {
                var a = p.approaches[CaseRecord.speechApproach];
                statement = "Η παρέμβασή σου:\n«" + (CaseRecord.speech == 0 ? a.textForA : a.textForB) + "»";
            }
            CampaignText("body", "Η τελική σου ψήφος καταγράφεται χωριστά από τη δημόσια παρέμβασή σου. Αποφασίζει το σώμα: " + Case.institution + ".");
            CampaignLabel("choice.0", p.choiceA);
            CampaignLabel("choice.1", p.choiceB);
            CampaignText("role", statement + "\n\nΛόγος: " + CaseRecord.speakerComponent + " μον. · Λογογράφος: " + CaseRecord.writerComponent + " μον.");
            FitPoliticalText("vote.role", 19, 22);
            CampaignText("source", "Αμοιβή παρουσίας: " + Case.attendanceObols + " οβ.\n" + ShortCaseSource);
            return true;
        }

        string PoliticalTrace()
        {
            var r = CaseRecord;
            int sign = r.speech == 0 ? 1 : r.speech == 1 ? -1 : 0;
            int contactSign = r.supportSide == 0 ? 1 : r.supportSide == 1 ? -1 : 0;
            return "Αφετηρία Α " + r.baseComponent + " · Πόλη " + Signed(r.foodComponent + r.trustComponent + r.fundsComponent)
                + " · Λόγος " + Signed(sign * r.speakerComponent) + " · Λογογράφος " + Signed(sign * r.writerComponent)
                + " · Δίκτυο " + Signed(contactSign * r.officialComponent) + "\nΑ: " + r.supportTotal + "/100 · Όριο: " + r.supportThreshold + " · Δείκτης προσομοίωσης.";
        }

        string PoliticalInfluenceSummary()
        {
            var r = CaseRecord;
            int speechSign = r.speech == 0 ? 1 : r.speech == 1 ? -1 : 0;
            int contactSign = r.supportSide == 0 ? 1 : r.supportSide == 1 ? -1 : 0;
            return "Λόγος " + Signed(speechSign * r.speakerComponent) + " · Λογογράφος " + Signed(speechSign * r.writerComponent)
                + "\nΔίκτυο " + Signed(contactSign * r.officialComponent) + " · Πόλη " + Signed(r.foodComponent + r.trustComponent + r.fundsComponent)
                + "\nΜεταβολή της στήριξης Α.";
        }

        void FitPoliticalText(string key, int min, int max)
        {
            var text = Get<TMP_Text>(key);
            if (text == null) return;
            text.enableAutoSizing = true; text.fontSizeMin = min; text.fontSizeMax = max;
        }

        static string Signed(int value) => value.ToString("+0;-0;0");

        bool RenderPoliticalResult(bool report)
        {
            var p = PoliticalCaseRules.Definition(Case, State);
            bool political = p != null && CaseRecord.politicalVersion == PoliticalCaseRules.Version;
            if (!report && roots.TryGetValue(currentScreen, out var resultRoot))
                foreach (var caption in resultRoot.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (caption.name != "A meaning" && caption.name != "B meaning") continue;
                    if (!originalResultCaptions.ContainsKey(caption)) originalResultCaptions[caption] = caption.text;
                    caption.text = !political ? originalResultCaptions[caption] : caption.name == "A meaning" ? p.choiceA : "Δείκτης προσομοίωσης, όχι πλήθος ψήφων";
                    if (political) { caption.enableAutoSizing = true; caption.fontSizeMin = 14; caption.fontSizeMax = 18; }
                }
            if (!political) return false;
            var r = CaseRecord;
            string outcome = r.majority == 0 ? p.outcomeA : p.outcomeB;
            string personal = r.vote == 2 ? "Απείχες από την ψηφοφορία." : r.vote == r.majority ? "Η θέση που ψήφισες επικράτησε." : "Η θέση που ψήφισες δεν επικράτησε.";
            if (!r.policyFullyFunded) outcome += " Η ανεπαρκής χρηματοδότηση περιορίζει την επισιτιστική επίδραση της απόφασης.";
            CampaignText("heading", report ? "Η απόφαση αφήνει ίχνος στην πόλη" : "Επικρατεί: " + (r.majority == 0 ? p.choiceA : p.choiceB));
            FitPoliticalText(currentScreen + ".heading", 24, 36);
            CampaignText("body", outcome + "\n\n" + personal);
            if (report)
            {
                CampaignText("role", "Επισιτισμός: " + r.foodBefore + " → " + Campaign.foodSecurity + "/100\nΘεσμική εμπιστοσύνη: " + Signed(r.trustBefore) + " → " + Signed(Campaign.legitimacy)
                    + "\nΤαμείο πόλης: " + Signed(r.cityDelta) + " οβ. · Προσωπικό: " + Signed(r.personalDelta) + " οβ.\n" + MetricDeltas(r.metricDelta)
                    + "\n\n" + PoliticalTrace());
                FitPoliticalText("week-report.role", 18, 23);
                CampaignLabel("primary", "Στην επόμενη εβδομάδα");
            }
            else
            {
                CampaignText("majority.0", "Στήριξη Α · " + r.supportTotal);
                CampaignText("majority.1", "Όριο έγκρισης · " + r.supportThreshold);
                var bar = Get<Image>(currentScreen + ".majority.bar");
                if (bar != null) bar.rectTransform.sizeDelta = new Vector2(898 * 1.2f * r.supportTotal / 100f, 8 * 1.2f);
                CampaignText("city-effect", "Πόλη " + Signed(r.cityDelta) + " οβ. · Επισιτισμός " + Signed(r.foodDelta));
                CampaignText("personal-effect", "Προσωπικό " + Signed(r.personalDelta) + " οβ. · αμοιβή παρουσίας");
                CampaignText("speech-effect", PoliticalInfluenceSummary());
                FitPoliticalText(currentScreen + ".body", 17, 22);
                FitPoliticalText(currentScreen + ".majority.0", 26, 36);
                FitPoliticalText(currentScreen + ".majority.1", 26, 36);
                foreach (string key in new[] { "city-effect", "personal-effect", "speech-effect" })
                {
                    FitPoliticalText(currentScreen + "." + key, 16, 20);
                    var text = Get<TMP_Text>(currentScreen + "." + key);
                    if (text == null) continue;
                    var card = text.transform.parent as RectTransform;
                    if (card != null)
                    {
                        card.anchoredPosition = new Vector2(card.anchoredPosition.x, -516);
                        card.sizeDelta = new Vector2(card.sizeDelta.x, 144);
                    }
                    CivicUIFactory.Box(text.rectTransform, 12, 51.6f, 325.2f, 80);
                }
            }
            return true;
        }

        bool RenderPoliticalDebate()
        {
            var p = PoliticalCaseRules.Definition(Case, State);
            var template = Get<TMP_Text>("deliberation.argument.0");
            if (template == null) return false;
            if (legacyArgumentCards == null)
                legacyArgumentCards = new[] { template.transform.parent.parent.gameObject, Get<TMP_Text>("deliberation.argument.1").transform.parent.parent.gameObject };
            foreach (var card in legacyArgumentCards) card.SetActive(false);
            if (politicalTranscript == null) CreatePoliticalTranscript(template);
            politicalTranscript.gameObject.SetActive(true);
            CaptureCaseHistorySnapshot();
            var lines = p?.dialogue ?? LegacyDialogueLines();
            int last = Math.Min(CaseRecord.debateIndex, lines.Length - 1);
            string key = CampaignRules.Turn(State) + ":" + Case.id + ":" + last + ":" + (p?.version ?? 0);
            if (renderedPoliticalDialogue != key)
            {
                renderedPoliticalDialogue = key;
                foreach (Transform child in politicalTranscriptContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                Canvas.ForceUpdateCanvases();
                float width = Mathf.Max(600, politicalTranscript.viewport.rect.width - 20), y = 10;
                for (int i = 0; i <= last; i++) y += CreateDialogueEntry(lines[i], i, width, y, template.font);
                politicalTranscriptContent.sizeDelta = new Vector2(0, Mathf.Max(y + 10, politicalTranscript.viewport.rect.height));
                Canvas.ForceUpdateCanvases();
                politicalTranscript.verticalNormalizedPosition = 0;
            }
            politicalNext.GetComponentInChildren<TMP_Text>().text = last < lines.Length - 1 ? "Συνέχεια · " + (last + 1) + "/" + lines.Length : Case.observation ? "Στην εκτίμηση των στοιχείων" : "Στην ψηφοφορία";
            politicalAsk.gameObject.SetActive(last >= lines.Length - 1 && !Case.observation && CaseRecord.speech < 0);
            politicalNext.gameObject.SetActive(true);
            for (int i = 0; i < 3; i++) { var old = Get<Button>("deliberation.choice." + i); if (old != null) old.gameObject.SetActive(false); }
            return true;
        }

        PoliticalDialogueLine[] LegacyDialogueLines()
        {
            var result = new List<PoliticalDialogueLine>();
            foreach (int side in new[] { 0, 1 })
                foreach (string text in (side == 0 ? Case.argumentA : Case.argumentB).Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
                    result.Add(new PoliticalDialogueLine { speaker = side == 0 ? Case.speakerA : Case.speakerB, side = side, portraitIndex = side, text = text,
                        stageText = Case.observation ? "Παρακολουθείς τη διαδικασία. Ο ομιλητής αναπτύσσει την τοποθέτησή του." : "Ο ομιλητής λαμβάνει τον λόγο ενώπιον του σώματος." });
            return result.ToArray();
        }

        Texture CivicPortraitAtlas()
        {
            var portrait = Get<RawImage>("official-detail.portrait");
            if (portrait != null && portrait.texture != null) return portrait.texture;
            return roots.TryGetValue("logographers", out var writers) ? writers.GetComponentsInChildren<RawImage>(true).FirstOrDefault(x => x.texture != null)?.texture : null;
        }

        float CreateDialogueEntry(PoliticalDialogueLine line, int number, float width, float y, TMP_FontAsset font)
        {
            var row = CivicUIFactory.Rect(politicalTranscriptContent, "Speaker " + number + " " + line.speaker);
            float textWidth = width - 222;
            var stage = CivicUIFactory.Text(row, "Procedural narration", line.stageText, font, 21, new Color32(97, 99, 84, 255));
            stage.fontStyle = FontStyles.Italic;
            float stageHeight = Mathf.Max(32, stage.GetPreferredValues(stage.text, width - 32, 0).y + 8);
            CivicUIFactory.Box(stage.rectTransform, 16, 4, width - 32, stageHeight);
            var portrait = CivicUIFactory.Portrait(row, "Speaker portrait", CivicPortraitAtlas(), line.portraitIndex);
            CivicUIFactory.Box(portrait.rectTransform, 16, stageHeight + 18, 150, 188);
            var name = CivicUIFactory.Text(row, "Speaker name", line.speaker, font, 29, CivicUIFactory.Teal); name.fontStyle = FontStyles.Bold;
            CivicUIFactory.Box(name.rectTransform, 192, stageHeight + 12, textWidth, 40);
            var role = CivicUIFactory.Text(row, "Speaker role", line.speakerRole, font, 20, new Color32(105, 91, 59, 255));
            CivicUIFactory.Box(role.rectTransform, 192, stageHeight + 52, textWidth, 50);
            var body = CivicUIFactory.Text(row, "Argument", line.text, font, 27, CivicUIFactory.Ink);
            float bodyHeight = body.GetPreferredValues(body.text, textWidth, 0).y + 12;
            CivicUIFactory.Box(body.rectTransform, 192, stageHeight + 106, textWidth, bodyHeight);
            float height = Mathf.Max(stageHeight + 224, stageHeight + 120 + bodyHeight);
            CivicUIFactory.Box(row, 0, y, width, height);
            return height + 24;
        }

        void CreatePoliticalTranscript(TMP_Text template)
        {
            var root = roots["deliberation"].transform;
            var first = (RectTransform)legacyArgumentCards[0].transform;
            var second = (RectTransform)legacyArgumentCards[1].transform;
            politicalTranscript = CivicUIFactory.Scroll(root, "Political dialogue", out politicalTranscriptContent);
            var rt = (RectTransform)politicalTranscript.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = first.anchoredPosition;
            rt.sizeDelta = new Vector2(second.anchoredPosition.x + second.sizeDelta.x - first.anchoredPosition.x, first.sizeDelta.y);
            foreach (string key in new[] { "deliberation.vote", "deliberation.speech" }) { var old = Get<Button>(key); if (old != null) old.gameObject.SetActive(false); }
            politicalNext = CivicUIFactory.Button(root, "Next speaker or vote", "Συνέχεια", template.font, ContinueDebate, this);
            politicalAsk = CivicUIFactory.Button(root, "Request a public intervention", "Ζητώ τον λόγο", template.font, () => FollowStory("DeliberationSpeech"), this);
            foreach (var pair in new[] { new { button = politicalNext, x = .72f }, new { button = politicalAsk, x = .28f } })
            {
                var buttonRect = (RectTransform)pair.button.transform; buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(pair.x, .2f); buttonRect.pivot = new Vector2(.5f, .5f);
                buttonRect.anchoredPosition = Vector2.zero; buttonRect.sizeDelta = new Vector2(600, 76);
            }
            targets["deliberation.vote"] = politicalNext; targets["deliberation.speech"] = politicalAsk;
            targets["deliberation.vote.label"] = politicalNext.GetComponentInChildren<TMP_Text>();
            targets["deliberation.speech.label"] = politicalAsk.GetComponentInChildren<TMP_Text>();
        }
    }
}
