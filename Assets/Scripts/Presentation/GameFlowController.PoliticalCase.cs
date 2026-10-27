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
        sealed class DebateCard
        {
            public TMP_Text name, role, stage, phrase;
            public RawImage portrait;
        }
        readonly DebateCard[] debateCards = new DebateCard[2];
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

        bool debateLayoutReady;
        bool RenderPoliticalDebate()
        {
            CaptureCaseHistorySnapshot();
            if(!debateLayoutReady) {SetupApprovedDebateLayout();debateLayoutReady=true;}
            var entries=JsonContent.DisplayArguments(Case);
            for(int side=0;side<2;side++) {
                var argument=entries.First(a=>a.side==side);var card=debateCards[side];
                card.name.text=argument.speaker;card.role.text=argument.speakerRole.ToUpperInvariant();
                card.role.color=side==0?new Color32(112,123,85,255):new Color32(153,82,60,255);
                card.stage.text=side==0?Case.leanReasonA:Case.leanReasonB;
                card.phrase.text="«"+JsonContent.DisplayArgument(argument)+"»";
                foreach(var text in new[]{card.name,card.role,card.stage,card.phrase}) {
                    text.maxVisibleCharacters=int.MaxValue;text.maxVisibleWords=int.MaxValue;text.maxVisibleLines=int.MaxValue;
                }
                int index=Mathf.Clamp(argument.portraitIndex,0,19);
                var uv=new Rect((index%4)/4f,1f-(index/4+1)*.2f,.25f,.2f);
                if(card.portrait.texture!=null) {
                    float ratio=card.portrait.texture.width*uv.width/(card.portrait.texture.height*uv.height);
                    if(ratio>1){float width=uv.width/ratio;uv.x+=(uv.width-width)/2;uv.width=width;}
                    else {float height=uv.height*ratio;uv.y+=(uv.height-height)/2;uv.height=height;}
                }
                card.portrait.uvRect=uv;
                var button=Get<Button>("deliberation.choice."+side);button.gameObject.SetActive(true);button.interactable=true;
                button.transform.SetParent(roots["deliberation"].transform,false);
                CivicUIFactory.Box((RectTransform)button.transform,141.6f+side*836.4f,901.2f,800.4f,93.6f);
                var label=button.GetComponentInChildren<TMP_Text>(true);label.gameObject.SetActive(true);
                label.text=side==0?Case.choiceA:Case.choiceB;label.fontSizeMax=26;label.fontSizeMin=20;label.maxVisibleCharacters=int.MaxValue;
                var shape=button.GetComponent<PanelGraphic>();if(shape!=null)shape.color=side==0?CivicUIFactory.Gold:CivicUIFactory.Paper;
            }
            var ask=Get<Button>("deliberation.speech");ask.gameObject.SetActive(!Case.observation&&CaseRecord.speech<0);ask.interactable=true;
            ask.transform.SetParent(roots["deliberation"].transform,false);CivicUIFactory.Box((RectTransform)ask.transform,732,760.8f,456,70);
            var askLabel=ask.GetComponentInChildren<TMP_Text>(true);askLabel.gameObject.SetActive(true);askLabel.text="Ζητώ τον λόγο";askLabel.maxVisibleCharacters=int.MaxValue;
            foreach(string key in new[]{"deliberation.vote","deliberation.choice.2","deliberation.source","deliberation.institution"})SetActive(key,false);
            return true;
        }

        void SetupApprovedDebateLayout()
        {
            var root=roots["deliberation"].transform;
            var heading=Get<TMP_Text>("deliberation.heading");CivicUIFactory.Box(heading.rectTransform,108,78,1310,96);heading.fontSizeMax=45.6f;heading.fontSizeMin=32;heading.enableAutoSizing=true;
            var sourceBanner=Get<TMP_Text>("procedure-briefing.banner");
            if(sourceBanner!=null) {
                var banner=Instantiate(sourceBanner.transform.parent.gameObject,root);banner.name="Banner City deliberation";banner.SetActive(true);
                CivicUIFactory.Box((RectTransform)banner.transform,1560,55,270,48);
                var label=banner.GetComponentInChildren<TMP_Text>(true);label.gameObject.SetActive(true);label.text="Η ΠΟΛΗ ΣΥΖΗΤΑ";label.fontSizeMin=15;label.fontSizeMax=20;label.enableAutoSizing=true;
            }
            var background=Get<Image>("deliberation.background");Transform behind=background!=null?background.transform:null;
            while(behind!=null&&behind.parent!=root)behind=behind.parent;
            for(int i=0;i<2;i++) {
                var rect=CivicUIFactory.Rect(root,i==0?"Debate side shade":"Debate lower shade");CivicUIFactory.Fill(rect,Vector2.zero,Vector2.zero);
                var shade=rect.gameObject.AddComponent<GradientGraphic>();shade.raycastTarget=false;shade.horizontal=i==0;
                shade.ramp.SetKeys(i==0?new[]{new GradientColorKey(new Color32(8,27,34,255),0),new GradientColorKey(new Color32(12,31,39,255),.48f),new GradientColorKey(new Color32(8,27,34,255),1)}:new[]{new GradientColorKey(new Color32(8,24,32,255),0),new GradientColorKey(new Color32(8,24,32,255),1)},i==0?new[]{new GradientAlphaKey(.78f,0),new GradientAlphaKey(.27f,.48f),new GradientAlphaKey(.85f,1)}:new[]{new GradientAlphaKey(.88f,0),new GradientAlphaKey(0,.65f),new GradientAlphaKey(0,1)});
                rect.SetSiblingIndex(behind!=null?behind.GetSiblingIndex()+1+i:i);
            }
            for(int side=0;side<2;side++)debateCards[side]=CreateDebateCard(Get<TMP_Text>("deliberation.argument."+side),side);
        }

        DebateCard CreateDebateCard(TMP_Text original,int side)
        {
            var root=(RectTransform)original.transform.parent.parent;
            CivicUIFactory.Box(root,97.2f+side*880.8f,200.4f,844.8f,538.8f);
            foreach(Transform child in root) {
                // Retain frame artwork and corner ornament; replace the previous content subtree.
                if(child.GetComponentInChildren<TMP_Text>(true)!=null||child.GetComponentInChildren<RawImage>(true)!=null)child.gameObject.SetActive(false);
                else if(child.name.IndexOf("rule",StringComparison.OrdinalIgnoreCase)>=0)child.gameObject.SetActive(false);
            }
            var content=CivicUIFactory.Rect(root,"Static argument card");CivicUIFactory.Fill(content,new Vector2(40,36),new Vector2(40,36));
            var frame=CivicUIFactory.Rect(content,"Speaker medallion");CivicUIFactory.Box(frame,0,0,93.6f,93.6f);
            frame.gameObject.AddComponent<EllipseGraphic>().color=CivicUIFactory.Gold;
            var mask=CivicUIFactory.Rect(frame,"Portrait mask");CivicUIFactory.Fill(mask,new Vector2(3,3),new Vector2(3,3));mask.gameObject.AddComponent<EllipseGraphic>().color=Color.white;mask.gameObject.AddComponent<Mask>().showMaskGraphic=false;
            var portrait=CivicUIFactory.Portrait(mask,"Speaker portrait",CivicPortraitAtlas(),0);CivicUIFactory.Fill(portrait.rectTransform,Vector2.zero,Vector2.zero);
            var card=new DebateCard {portrait=portrait,
                name=CivicUIFactory.Text(content,"Speaker name","",original.font,33.6f,CivicUIFactory.Ink),
                role=CivicUIFactory.Text(content,"Speaker role","",original.font,14.4f,CivicUIFactory.Ink),
                phrase=CivicUIFactory.Text(content,"Static argument phrase","",original.font,25.2f,CivicUIFactory.Ink),
                stage=CivicUIFactory.Text(content,"Argument context","",original.font,14.4f,new Color32(102,113,94,255))};
            CivicUIFactory.Box(card.name.rectTransform,118,7,640,44);card.name.fontStyle=FontStyles.Bold;
            CivicUIFactory.Box(card.role.rectTransform,118,54,640,43);
            CivicUIFactory.Box(card.phrase.rectTransform,0,138,764.8f,210);card.phrase.enableAutoSizing=true;card.phrase.fontSizeMin=21;card.phrase.fontSizeMax=25.2f;
            var rule=CivicUIFactory.Panel(content,"Argument separator",CivicUIFactory.Gold);CivicUIFactory.Box(rule.rectTransform,0,353,764.8f,1);rule.border=0;rule.raycastTarget=false;
            CivicUIFactory.Box(card.stage.rectTransform,0,371,764.8f,94);card.stage.enableAutoSizing=true;card.stage.fontSizeMin=12;card.stage.fontSizeMax=14.4f;
            foreach(var text in new[]{card.name,card.role,card.phrase,card.stage})text.alignment=TextAlignmentOptions.TopLeft;
            return card;
        }

        PoliticalDialogueLine[] LegacyDialogueLines() => (Case.arguments ?? Array.Empty<CaseArgument>()).Select(a => new PoliticalDialogueLine
        { id = a.id, side = a.side, speaker = a.speaker, speakerRole = a.speakerRole, portraitIndex = a.portraitIndex, stageText = a.stageText, text = string.Join(" ", a.fragments) }).ToArray();
        Texture CivicPortraitAtlas()
        {
            var portrait = Get<RawImage>("official-detail.portrait");
            if (portrait != null && portrait.texture != null) return portrait.texture;
            return roots.TryGetValue("logographers", out var writers) ? writers.GetComponentsInChildren<RawImage>(true).FirstOrDefault(x => x.texture != null)?.texture : null;
        }
    }
}
