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
            for (int i = 0; i < 3; i++)
            {
                CampaignLabel("choice." + i, choosingApproach ? p.approaches[i].label : i == 0 ? p.choiceA : i == 1 ? p.choiceB : "Δεν ζητώ τον λόγο");
                var button = Get<Button>("speech.choice." + i);
                var text = button?.GetComponentInChildren<TMP_Text>();
                if (text != null) { text.enableAutoSizing = true; text.fontSizeMin = 20; text.fontSizeMax = 28; }
            }
            CampaignText("role", "Τεκμηρίωση: Αυθεντία και Πειθώ\nΔημόσια δέσμευση: Συνέπεια, Ηθική και Πειθώ\nΟξεία αντιπαράθεση: Παρρησία και Αναγνωρισιμότητα");
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
            if (lines.Length == 0) return false;
            int last = Math.Min(CaseRecord.debateIndex, lines.Length - 1);
            string[] chunks = SplitDialogueText(lines[last].text);
            int chunk = Mathf.Clamp(CaseRecord.debateChunk, 0, chunks.Length - 1);
            string key = CampaignRules.Turn(State) + ":" + Case.id + ":" + last + ":" + chunk + ":" + (p?.version ?? 0);
            if (renderedPoliticalDialogue != key)
            {
                renderedPoliticalDialogue = key;
                foreach (Transform child in politicalTranscriptContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                Canvas.ForceUpdateCanvases();
                float width = Mathf.Max(600, politicalTranscript.viewport.rect.width - 20), y = 10;
                for (int i = 0; i <= last; i++)
                {
                    var line = lines[i];
                    string[] revealed = i < last ? SplitDialogueText(line.text) : chunks.Take(chunk + 1).ToArray();
                    for (int part = 0; part < revealed.Length; part++)
                        y += CreateDialogueEntry(line, revealed[part], part == 0, part == 0 && (i == 0 || lines[i - 1].speaker != line.speaker), i * 1000 + part, width, y, template.font);
                }
                politicalTranscriptContent.sizeDelta = new Vector2(0, Mathf.Max(y + 10, politicalTranscript.viewport.rect.height));
                Canvas.ForceUpdateCanvases();
                politicalTranscript.verticalNormalizedPosition = 0;
            }
            politicalNext.GetComponentInChildren<TMP_Text>().text = !DebateComplete ? "Συνέχεια  ›" : Case.observation ? "Στην εκτίμηση των στοιχείων" : "Στην ψηφοφορία";
            politicalAsk.gameObject.SetActive(DebateComplete && !Case.observation && CaseRecord.speech < 0);
            politicalAsk.interactable = DebateComplete && !Case.observation && CaseRecord.speech < 0;
            politicalNext.gameObject.SetActive(true);
            var nextLabel = politicalNext.GetComponentInChildren<TMP_Text>();
            ((RectTransform)politicalNext.transform).sizeDelta = new Vector2(Mathf.Clamp(nextLabel.GetPreferredValues(nextLabel.text).x + 60, 220, 420), 72);
            for (int i = 0; i < 3; i++) { var old = Get<Button>("deliberation.choice." + i); if (old != null) old.gameObject.SetActive(false); }
            return true;
        }

        string[] CurrentDialogueChunks()
        {
            var lines = PoliticalCaseRules.Definition(Case, State)?.dialogue ?? LegacyDialogueLines();
            return lines.Length == 0 ? new[] { "" } : SplitDialogueText(lines[Mathf.Clamp(CaseRecord.debateIndex, 0, lines.Length - 1)].text);
        }

        // One click reveals at most two sentences. Sentence boundaries are kept verbatim,
        // so the saved transcript is still the complete authored speech, not a summary.
        internal static string[] SplitDialogueText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new[] { "" };
            string[] sentences = System.Text.RegularExpressions.Regex.Split(text.Trim(), @"(?<=[.!?;;])\s+|(?<=[.!?;;][»”""])\s+");
            var chunks = new List<string>();
            string current = "";
            int count = 0;
            foreach (string sentence in sentences)
            {
                if (string.IsNullOrWhiteSpace(sentence)) continue;
                if (count >= 2 || (current.Length > 0 && current.Length + sentence.Length + 1 > 420))
                {
                    chunks.Add(current);
                    current = "";
                    count = 0;
                }
                current += (current.Length == 0 ? "" : " ") + sentence;
                count++;
            }
            if (current.Length > 0) chunks.Add(current);
            return chunks.Count > 0 ? chunks.ToArray() : new[] { "" };
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

        float CreateDialogueEntry(PoliticalDialogueLine line, string visibleText, bool firstChunk, bool showHeader, int number, float width, float y, TMP_FontAsset font)
        {
            var row = CivicUIFactory.Rect(politicalTranscriptContent, "Speaker " + number + " " + line.speaker);
            float textWidth = width - 222;
            float stageHeight = 0;
            if (firstChunk && !string.IsNullOrWhiteSpace(line.stageText))
            {
                var stage = CivicUIFactory.Text(row, "Procedural narration", line.stageText, font, 21, new Color32(97, 99, 84, 255));
                stage.fontStyle = FontStyles.Italic;
                stageHeight = Mathf.Max(32, stage.GetPreferredValues(stage.text, width - 32, 0).y + 8);
                CivicUIFactory.Box(stage.rectTransform, 16, 4, width - 32, stageHeight);
            }
            float headerHeight = showHeader ? 112 : 0;
            if (showHeader)
            {
                var frame = CivicUIFactory.Rect(row, "Circular portrait frame");
                CivicUIFactory.Box(frame, 20, stageHeight + 12, 100, 100);
                frame.gameObject.AddComponent<CanvasRenderer>();
                frame.gameObject.AddComponent<EllipseGraphic>().color = CivicUIFactory.Gold;
                var mask = CivicUIFactory.Rect(frame, "Circular portrait mask");
                CivicUIFactory.Box(mask, 3, 3, 94, 94);
                mask.gameObject.AddComponent<CanvasRenderer>();
                mask.gameObject.AddComponent<EllipseGraphic>().color = Color.white;
                mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                var portrait = CivicUIFactory.Portrait(mask, "Speaker portrait", CivicPortraitAtlas(), line.portraitIndex);
                CivicUIFactory.Fill(portrait.rectTransform, Vector2.zero, Vector2.zero);
                if (portrait.texture != null)
                {
                    var uv = portrait.uvRect; float cellRatio = portrait.texture.width * uv.width / (portrait.texture.height * uv.height);
                    if (cellRatio > 1) { float size = uv.width / cellRatio; uv.x += (uv.width - size) / 2; uv.width = size; }
                    else { float size = uv.height * cellRatio; uv.y += (uv.height - size) / 2; uv.height = size; }
                    portrait.uvRect = uv;
                }
                var name = CivicUIFactory.Text(row, "Speaker name", line.speaker, font, 29, CivicUIFactory.Teal); name.fontStyle = FontStyles.Bold;
                CivicUIFactory.Box(name.rectTransform, 144, stageHeight + 12, width - 170, 40);
                var role = CivicUIFactory.Text(row, "Speaker role", line.speakerRole, font, 20, new Color32(105, 91, 59, 255));
                CivicUIFactory.Box(role.rectTransform, 144, stageHeight + 52, width - 170, 48);
            }
            textWidth = width - 48;
            var body = CivicUIFactory.Text(row, "Argument", visibleText, font, 27, CivicUIFactory.Ink);
            float bodyHeight = body.GetPreferredValues(body.text, textWidth, 0).y + 12;
            CivicUIFactory.Box(body.rectTransform, 24, stageHeight + headerHeight + 8, textWidth, bodyHeight);
            float height = stageHeight + headerHeight + bodyHeight + 16;
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
            rt.sizeDelta = new Vector2(second.anchoredPosition.x + second.sizeDelta.x - first.anchoredPosition.x, Mathf.Max(first.sizeDelta.y, 1080 - Mathf.Abs(first.anchoredPosition.y) - 140));
            foreach (string key in new[] { "deliberation.vote", "deliberation.speech" }) { var old = Get<Button>(key); if (old != null) old.gameObject.SetActive(false); }
            politicalNext = CivicUIFactory.Button(root, "Next speaker or vote", "Συνέχεια", template.font, ContinueDebate, this);
            politicalAsk = CivicUIFactory.Button(root, "Request a public intervention", "Ζητώ τον λόγο", template.font, () => FollowStory("DeliberationSpeech"), this);
            foreach (var pair in new[] { new { button = politicalNext, x = .72f }, new { button = politicalAsk, x = .28f } })
            {
                var buttonRect = (RectTransform)pair.button.transform; buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(pair.x == .72f ? .96f : .04f, .075f); buttonRect.pivot = new Vector2(pair.x == .72f ? 1 : 0, .5f);
                buttonRect.anchoredPosition = Vector2.zero; buttonRect.sizeDelta = new Vector2(270, 72);
            }
            targets["deliberation.vote"] = politicalNext; targets["deliberation.speech"] = politicalAsk;
            targets["deliberation.vote.label"] = politicalNext.GetComponentInChildren<TMP_Text>();
            targets["deliberation.speech.label"] = politicalAsk.GetComponentInChildren<TMP_Text>();
        }
    }
}

