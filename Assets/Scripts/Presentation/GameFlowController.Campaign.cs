using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    public sealed partial class GameFlowController
    {
        public TextAsset campaignContent;
        CampaignContent campaignData;
        int historySite;
        CampaignContent CampaignData => campaignData ?? (campaignData = JsonUtility.FromJson<CampaignContent>(campaignContent.text));

        CampaignState Campaign
        {
            get
            {
                if (State.campaign == null)
                    State.campaign = new CampaignState();
                return State.campaign;
            }
        }

        InstitutionCase Case
        {
            get
            {
                var value = CampaignData.cases[CampaignRules.Turn(State) % CampaignData.cases.Length];
                value.contentVersion = CampaignData.contentVersion;
                return value;
            }
        }

        CaseRecord CaseRecord => CampaignRules.Current(State);

        static string[] InstitutionNames => new[]
        {
            ArticyNarrative.Copy("Copy_d843d43a4d9723ef"),
            ArticyNarrative.Copy("Copy_86071228fc28d521"),
            ArticyNarrative.Copy("Copy_2fbc1a362586a8ce"),
            ArticyNarrative.Copy("Copy_f56b01a1d22b0c31"),
            ArticyNarrative.Copy("Copy_b56a13349a0ea9d6")
        };
        static readonly string[] InstitutionIds =
        {
            "institution-deme",
            "institution-areopagus",
            "institution-palladion",
            "institution-delphinion",
            "institution-phreatto",
            "institution-prytaneion"
        };
        static bool IsCampaignScreen(string id) => CampaignRules.ValidPhase(id) || id == "appeal";
        void WireCampaignButtons()
        {
            for (int i = 0; i < 4; i++)
            {
                int n = i;
                Bind("map.institution." + i, () => OpenInstitution(n));
            }

            Bind("map.deme", () => OpenInstitution(4));
            foreach (string id in new[]
            {
                "knowledge",
                "initial-knowledge",
                "final-knowledge"
            }

            )
                for (int i = 0; i < 3; i++)
                {
                    int n = i;
                    string target = id;
                    Bind(id + ".answer." + i, () => AnswerCampaign(target, n));
                }

            Bind("knowledge-correct.primary", () => FollowStory("KnowledgeContinue"));
            Bind("knowledge-incorrect.primary", () => FollowStory("KnowledgeContinue"));
            Bind("procedure-briefing.primary", () => FollowStory("BriefingContinue"));
            Bind("appeal.primary", () => FollowStory("AppealContinue"));
            Bind("deliberation.speech", () => FollowStory("DeliberationSpeech"));
            Bind("deliberation.vote", ContinueDebate);
            for (int i = 0; i < 3; i++)
            {
                int n = i;
                Bind("deliberation.choice." + i, () => { if(!DebateComplete){if(n==2)ContinueDebate();return;} CastCampaignVote(n); });
                Bind("vote.choice." + i, () => CastCampaignVote(n));
                Bind("speech.choice." + i, () => CampaignSpeak(n));
            }

            Bind("vote-result.primary", () => FollowStory("VoteContinue"));
            Bind("vote-abstention.primary", () => FollowStory("VoteContinue"));
            Bind("week-report.primary", AdvanceWeek);
            Bind("week-transition.primary", () => FollowStory("WeekContinue"));
            Bind("prytany-transition.primary", AdvancePrytany);
            Bind("ending-city.primary", () => FollowStory("CityEndingContinue"));
            Bind("ending-personal.primary", () => FollowStory("PersonalEndingContinue"));
            Bind("final-survey.primary", FinishSurvey);
            Bind("complete.primary", () => Show("main-menu"));
            Bind("complete.review", () => GoCampaign("ending-city"));
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 7; i++)
                {
                    int r = row, n = i;
                    Bind("final-survey.rating." + row + "." + i, () =>
                    {
                        Campaign.survey[r] = n + 1;
                        SaveCheckpoint();
                        RenderCampaign();
                    });
                }

            foreach (string id in InstitutionIds)
            {
                string k = id;
                Bind(id + ".primary", () => GoCampaign("map-stela"));
                Bind(id + ".back", () => GoCampaign("institution-history"));
            }

            Bind("institution-history.back", () => GoCampaign("map-stela"));
            Bind("institution-history.primary", () => GoCampaign("map-stela"));
            for (int i = 0; i < InstitutionIds.Length; i++)
            {
                string id = InstitutionIds[i];
                Bind("institution-history.location." + i, () => GoCampaign(id));
            }

            foreach (string id in new[]
            {
                "knowledge",
                "procedure-briefing",
                "deliberation",
                "speech",
                "vote",
                "week-report"
            }

            )
                Bind(id + ".back", () => GoCampaign("map-stela"));
        }

        void FollowStory(string route) => GoCampaign(ArticyNarrative.Route(route,State,Case.id));
        void GoCampaign(string id)
        {
            if (State == null || modalOpen)
                return;
            State.phase = id;
            if (!SaveCheckpoint())
                return;
            Show(id);
        }

        void OpenInstitution(int site)
        {
            EnsureState();
            historySite = site;
            if (site == Case.site && !CaseRecord.completed)
            {
                CaseRecord.caseId = Case.id;
                CaptureCaseHistorySnapshot();
                GoCampaign(CaseRecord.knowledge < 0 ? "knowledge" : "procedure-briefing");
            }
            else
                TryOpenInstitutionHistory(site);
        }

        void AnswerCampaign(string screen, int n)
        {
            if (TryAnswerEvaluation(screen,n)) return;
            if (screen == "knowledge")
            {
                if (!CampaignRules.Answer(State, n))
                    return;
                CaseRecord.questionId=Case.id+":knowledge"; CaseRecord.caseId=Case.id; CaseRecord.questionCorrect=Case.correct; CaseRecord.selectedAnswer=n; CaseRecord.contentVersion=CampaignData.contentVersion;
                GoCampaign(n == Case.correct ? "knowledge-correct" : "knowledge-incorrect");
                return;
            }

            bool initial = screen == "initial-knowledge";
            var answers = initial ? Campaign.initialAnswers : Campaign.finalAnswers;
            int index = initial ? Campaign.initialIndex : Campaign.finalIndex;
            if (index >= 8)
                return;
            if (answers[index] >= 0)
                return;
            answers[index] = n;
            if (initial)
            {
                Campaign.initialIndex++;
                if (Campaign.initialIndex == 8)
                {
                    Campaign.initialComplete = true;
                    GoCampaign("map-stela");
                    return;
                }
            }
            else
            {
                Campaign.finalIndex++;
                if (Campaign.finalIndex == 8)
                {
                    Campaign.finalComplete = true;
                    GoCampaign("final-survey");
                    return;
                }
            }

            SaveCheckpoint();
            RenderCampaign();
        }

        void CampaignSpeak(int n)
        {
            if (PoliticalSpeech(n)) return;
            if (Case.observation && n < 2)
            {
                SetStatus(ArticyNarrative.Copy("Copy_2afaa10a70e5cf45"));
                return;
            }

            var before=State.metrics.Copy();
            if (CaseRecord.speech < 0)
            {
                int bonus = n == 2 ? 0 : City.speechUses > 0 ? City.speechBonus : 0;
                if (CampaignRules.Speak(State, n, bonus, Case) && n != 2 && City.speechUses > 0)
                    ConsumeSpeechSupport();
            }

            FollowStory("SpeechContinue");
            SetStatus(MetricDeltas(CampaignRules.Difference(before,State.metrics)));
        }

        void CastCampaignVote(int n)
        {
            if (!CampaignRules.Vote(State, n, Case.costA, Case.costB, Case.war, Case.observation, Case))
                return;
            GoCampaign(ArticyNarrative.Route("VoteResult",State,Case.id,n));
            SetStatus(MetricDeltas(CaseRecord.metricDelta));
        }

        void AdvanceWeek()
        {
            if (currentScreen != "week-report")
                return;
            var snapshot = JsonUtility.ToJson(State);
            string next = CampaignRules.Advance(State);
            if (next == null)
                return;
            if (!SaveCheckpoint())
            {
                GameSession.State = JsonUtility.FromJson<GameState>(snapshot);
                return;
            }

            Show(next);
        }

        void AdvancePrytany()
        {
            var snapshot = JsonUtility.ToJson(State);
            string next = CampaignRules.NextPrytany(State);
            if (next == null)
                return;
            if (!SaveCheckpoint())
            {
                GameSession.State = JsonUtility.FromJson<GameState>(snapshot);
                return;
            }

            Show(next);
        }

        void FinishSurvey()
        {
            if (TryFinishEvaluationSurvey()) return;
            if (Campaign.survey.Any(x => x < 1))
                return;
            Campaign.surveyComplete = true;
            FollowStory("FinalSurveyComplete");
        }

        void RenderCampaignMap()
        {
            for (int i = 0; i < 4; i++)
            {
                bool active = Case.site == i && !CaseRecord.completed;
                string key = "map.institution." + i;
                var plate = Get<PanelGraphic>(key + ".plate");
                var title = Get<TMP_Text>(key + ".title");
                if (plate != null)
                {
                    plate.color = active ? new Color32(232, 223, 203, 255) : new Color32(25, 47, 52, 255);
                    plate.rectTransform.sizeDelta = new Vector2(plate.rectTransform.sizeDelta.x, active ? 72 : 50.4f);
                }
                if (title != null)
                {
                    title.text = i==3 && Case.site==3 ? "Φάκελος: "+Case.institution : InstitutionNames[i];
                    title.enableAutoSizing=true;title.fontSizeMin=15;title.fontSizeMax=22;
                    title.color = active ? new Color32(23, 43, 48, 255) : new Color32(238, 228, 203, 255);
                    title.rectTransform.anchoredPosition = new Vector2(0, active ? -11 : -9);
                }
                SetActive(key + ".newCase", active);
            }
            SetActive("map.deme", Case.site == 4 && !CaseRecord.completed);
            SetText("map.case.label", CaseRecord.completed ? ArticyNarrative.Copy("Copy_b383cd29aada7bc7") : (Case.institution ?? InstitutionNames[Case.site]) + " · " + Case.title);
            Bind("map.case", () =>
            {
                if (CaseRecord.completed)
                    GoCampaign("week-report");
                else
                    OpenInstitution(Case.site);
            });
        }

        void CampaignText(string suffix, string text) => SetText(currentScreen + "." + suffix, text);
        void CampaignLabel(string suffix, string text)
        {
            var b = Get<Button>(currentScreen + "." + suffix);
            if (b != null)
                b.GetComponentInChildren<TMP_Text>().text = text;
        }

        void CampaignImage(string bg)
        {
            var im = Get<Image>(currentScreen + ".background");
            if (im != null)
            {
                var sprite = Get<Image>("campaign.art." + bg);
                if (sprite != null)
                    im.sprite = sprite.sprite;
            }
        }

        InstitutionCase AssessmentCase(int index)
        {
            string[] fallback={"grain","citizenship","ships","audit","areopagus","defence","scrutiny","generals"};
            string id=CampaignData.assessmentIds!=null&&index<CampaignData.assessmentIds.Length?CampaignData.assessmentIds[index]:fallback[index];
            return CampaignData.cases.FirstOrDefault(c=>c.id==id) ?? CampaignData.cases[0];
        }
        readonly System.Collections.Generic.HashSet<RectTransform> liftedCaseSources = new System.Collections.Generic.HashSet<RectTransform>();
        void LiftCaseSource()
        {
            var text=Get<TMP_Text>(currentScreen+".source");
            if(text!=null&&liftedCaseSources.Add(text.rectTransform))text.rectTransform.anchoredPosition+=new Vector2(0,24);
        }
        string ShortCaseSource => (Case.source ?? "").Split(new[]{" · "},StringSplitOptions.None)[0];
        string[] DebateParts => PoliticalCaseRules.Definition(Case, State)?.dialogue?.Select(x => x.text).ToArray() ?? (Case.argumentA+"\n\n"+Case.argumentB).Split(new[]{"\n\n"},StringSplitOptions.RemoveEmptyEntries);
        bool DebateComplete => CaseRecord.debateIndex >= DebateParts.Length - 1 &&
            CaseRecord.debateChunk >= CurrentDialogueChunks().Length - 1;
        void ContinueDebate()
        {
            string[] chunks = CurrentDialogueChunks();
            if (CaseRecord.debateChunk < chunks.Length - 1)
                CaseRecord.debateChunk++;
            else if (CaseRecord.debateIndex < DebateParts.Length - 1)
            {
                CaseRecord.debateIndex++;
                CaseRecord.debateChunk = 0;
            }
            else
            {
                FollowStory("DeliberationVote");
                return;
            }
            SaveCheckpoint();
            RenderCampaign();
        }
        TMP_Text debateSpeakerA,debateSpeakerB;
        void RenderDebate()
        {
            if (RenderPoliticalDebate()) return;
            if(roots.TryGetValue("deliberation",out var debateRoot))
            {
                foreach(var t in debateRoot.GetComponentsInChildren<TMP_Text>(true))
                {
                    if(t.text=="Δημόκριτος")debateSpeakerA=t;
                    if(t.text=="Λύσανδρος")debateSpeakerB=t;
                    if(System.Text.RegularExpressions.Regex.Replace(t.text,"<[^>]+>|\\s","").IndexOf("Δημοκρατική",StringComparison.OrdinalIgnoreCase)>=0)t.text="Πρώτη πρόταση · εξέτασε τα επιχειρήματα";
                    if(System.Text.RegularExpressions.Regex.Replace(t.text,"<[^>]+>|\\s","").IndexOf("Ολιγαρχική",StringComparison.OrdinalIgnoreCase)>=0)t.text="Δεύτερη πρόταση · εξέτασε τα επιχειρήματα";
                    if(t.text.Contains("ΓΟΝΟΣ")||t.text.Contains("ΔΗΜΟΣΙΟΣ ΕΛΕΓΧΟΣ"))t.text="ΠΡΩΤΟΣ ΟΜΙΛΗΤΗΣ";
                    if(t.text.Contains("ΕΥΠΟΡΟΣ"))t.text="ΔΕΥΤΕΡΟΣ ΟΜΙΛΗΤΗΣ";
                }
            }
            if(debateSpeakerA!=null)debateSpeakerA.text=Case.speakerA;
            if(debateSpeakerB!=null)debateSpeakerB.text=Case.speakerB;
            var parts=DebateParts;int n=Math.Min(CaseRecord.debateIndex,parts.Length-1);
            int aCount=Case.argumentA.Split(new[]{"\n\n"},StringSplitOptions.RemoveEmptyEntries).Length;bool first=n<aCount;
            CampaignText("argument.0", first ? "«"+parts[n]+"»" : "Ο πρώτος ομιλητής ολοκλήρωσε την τοποθέτησή του.");
            CampaignText("argument.1", first ? "Περιμένει να λάβει τον λόγο." : "«"+parts[n]+"»");
            CampaignLabel("vote",DebateComplete ? "Στην ψηφοφορία" : "Συνέχεια λόγου  "+(n+1)+" / "+parts.Length);
            foreach(string key in new[]{"argument.0","argument.1"}){var t=Get<TMP_Text>("deliberation."+key);if(t!=null){t.enableAutoSizing=true;t.fontSizeMin=19;t.fontSizeMax=28;}}
        }
        void RenderCampaign()
        {
            string id = currentScreen;
            if (TryRenderEvaluationQuestion()) return;
            if (id == "final-survey" && RenderEvaluationSurvey()) return;
            if (campaignContent == null)
                return;
            CampaignText("funds", "Πόλη: "+State.cityFunds+" οβ. · Προσωπικό: "+State.personalFunds+" οβ.");
            if (id == "institution-history")
            {
                RenderInstitutionHistoryPanel();
                return;
            }

            if (InstitutionIds.Contains(id))
                return;
            if (new[]
            {
                "knowledge",
                "knowledge-correct",
                "knowledge-incorrect",
                "procedure-briefing",
                "deliberation",
                "speech",
                "vote",
                "vote-result",
                "vote-abstention",
                "appeal"
            }.Contains(id))
                CampaignImage(Case.site == 1 ? "boule" : Case.site == 2 ? "areopagus" : Case.site == 3 ? "courts" : Case.site == 4 ? "deme" : "assembly");
            if (id == "knowledge" || id == "initial-knowledge" || id == "final-knowledge")
            {
                bool quiz = id != "knowledge";
                int index = id == "initial-knowledge" ? Campaign.initialIndex : Campaign.finalIndex;
                var c = quiz ? AssessmentCase(Math.Min(7,index)) : Case;
                CampaignText("banner", quiz ? (id == "initial-knowledge" ? ArticyNarrative.Copy("Copy_3fbbd6062db820e6") : ArticyNarrative.Copy("Copy_ed29f6b542d3a7ad")) + ArticyNarrative.Copy("Copy_7497f4359296eb12") + (Math.Min(index, 7) + 1) + "/8" : ArticyNarrative.Copy("Copy_53405a8145bf2dfe"));
                CampaignText("heading", quiz && index == 0 ? ArticyNarrative.Copy("Copy_c5878ab2fda6586b") : c.question);
                CampaignText("body", quiz ? ArticyNarrative.Copy("Copy_f2552600441f7137") : ArticyNarrative.Copy("Copy_9f974b6ae4ac0b34") + Case.title + ArticyNarrative.Copy("Copy_c20dc950f5982180"));
                for (int i = 0; i < 4; i++)
                {
                    var answer = Get<Button>(id + ".answer." + i);
                    if (answer == null) continue;
                    answer.gameObject.SetActive(i < c.answers.Length);
                    if (i >= c.answers.Length) continue;
                    CampaignLabel("answer." + i, "ΑΒΓΔ"[i] + "   " + c.answers[i]);
                    var rect = (RectTransform)answer.transform;
                    CivicUIFactory.Box(rect, 0, 280 + i * 88, 1058.4f, 78);
                    var label = answer.GetComponentInChildren<TMP_Text>(true);
                    label.enableAutoSizing = true; label.fontSizeMin = 20; label.fontSizeMax = 27;
                }
                CampaignText("source", quiz ? ArticyNarrative.Copy("Copy_1d57091a46f50302") : ArticyNarrative.Copy("Copy_3cc3c8bead627006"));
                return;
            }

            if (id == "knowledge-correct" || id == "knowledge-incorrect")
            {
                CampaignText("seal", "?");
                CampaignText("heading", id == "knowledge-correct" ? (Case.id == "grain" ? ArticyNarrative.Copy("Copy_bb8a0b2b2732efb1") : ArticyNarrative.Copy("Copy_efc30c471c9d9686")) : ArticyNarrative.Copy("Copy_e3f63fadc3624d22"));
                CampaignText("body", Case.explanation);
                CampaignText("source", ShortCaseSource);
                LiftCaseSource();
                return;
            }

            if (id == "procedure-briefing" || id == "appeal")
            {
                CampaignText("heading", Case.institution + " · " + (Case.observation ? "Παρακολούθηση διαδικασίας" : "Η διαδικασία αρχίζει"));
                string context=Case.brief;
                if(Case.site==3)context+="\n\nΟ δείκτης στον χάρτη ανοίγει τον δικαστικό φάκελο· δεν δηλώνει την ακριβή τοποθεσία αυτού του δικαστηρίου.";
                if(Case.id=="audit-followup")context+="\n\nΠροηγούμενος λογιστικός έλεγχος: "+(Campaign.cases.Any(x=>x.completed&&x.caseId=="audit") ? "υπάρχει ήδη φάκελος στη διαδρομή σου." : "παρακολουθείς ανεξάρτητο φάκελο.")+" Εκκρεμείς συστάσεις σου: "+string.Join(" · ",City.complaintDecisions.Where(x=>!string.IsNullOrEmpty(x)));
                CampaignText("body",context);
                CampaignText("role", Case.observation ? ArticyNarrative.Copy("Copy_f85bd31fc92ebd77") : ArticyNarrative.Copy("Copy_1d174bf1ab96eb21"));
                CampaignText("source", ShortCaseSource + " · Εκπαιδευτικό, μυθοπλαστικό περιστατικό.");
                return;
            }

            if (id == "deliberation")
            {
                CampaignText("heading", Case.title);
                CampaignText("institution", (Case.institution ?? InstitutionNames[Case.site]).ToUpperInvariant() + (Case.observation ? ArticyNarrative.Copy("Copy_3f00a343e8208107") : ArticyNarrative.Copy("Copy_7c3f56f6a142be8b")));
                RenderDebate();
                CampaignLabel("choice.0", Case.observation ? ArticyNarrative.Copy("Copy_0b9ceeb7aeb4b781") : ArticyNarrative.Copy("Copy_c2ad6d98110ae3a1"));
                CampaignLabel("choice.1", Case.observation ? ArticyNarrative.Copy("Copy_c9cf02439a132666") : ArticyNarrative.Copy("Copy_edefc3af0366ead3"));
                CampaignLabel("choice.2", Case.observation ? ArticyNarrative.Copy("Copy_47f45e8efd2ad58d") : ArticyNarrative.Copy("Copy_cb6ec07aff936e23"));
                SetInteractable("deliberation.speech", !Case.observation && CaseRecord.speech < 0 && DebateComplete);
                for(int k=0;k<3;k++)SetInteractable("deliberation.choice."+k,k==2||DebateComplete);
                CampaignLabel("choice.0",Case.observation ? "Πρώτη εκτίμηση των στοιχείων" : "Υπέρ της πρώτης πρότασης");
                CampaignLabel("choice.1",Case.observation ? "Δεύτερη εκτίμηση των στοιχείων" : "Υπέρ της δεύτερης πρότασης");
                if(!DebateComplete)CampaignLabel("choice.2","Συνέχεια λόγου  "+(CaseRecord.debateIndex+1)+" / "+DebateParts.Length);
                CampaignText("source", ShortCaseSource);
                LiftCaseSource();
                return;
            }

            if (id == "speech" || id == "vote")
            {
                if (id == "speech" && RenderPoliticalSpeech()) return;
                if (id == "vote" && RenderPoliticalVote()) return;
                CampaignText("heading", id == "speech" ? ArticyNarrative.Copy("Copy_fd2a025dfbcc5500") : Case.observation ? ArticyNarrative.Copy("Copy_595a4262b7a284e3") : ArticyNarrative.Copy("Copy_111d1a88d8ec5459"));
                CampaignText("body", id == "speech" ? ArticyNarrative.Copy("Copy_4ee6e91c465e068d") : Case.title);
                CampaignLabel("choice.0", id == "speech" ? ArticyNarrative.Copy("Copy_fba2d921d0335163") : ArticyNarrative.Copy("Copy_432df8f8f9826174"));
                CampaignLabel("choice.1", id == "speech" ? ArticyNarrative.Copy("Copy_7381b2d39a137766") : ArticyNarrative.Copy("Copy_6b5411ff18007acd"));
                CampaignLabel("choice.2", id == "speech" ? ArticyNarrative.Copy("Copy_4e4312e35e52b30d") : Case.observation ? ArticyNarrative.Copy("Copy_47f45e8efd2ad58d") : ArticyNarrative.Copy("Copy_5dc4c89644ccd8a8"));
                if(id=="speech"){CampaignLabel("choice.0","Υποστηρίζω την πρώτη πρόταση");CampaignLabel("choice.1","Υποστηρίζω τη δεύτερη πρόταση");}
                CampaignText("role", id == "speech" ? (City.speechUses > 0 ? ArticyNarrative.Copy("Copy_3c1124cd8cf502aa") + City.speechBonus + ArticyNarrative.Copy("Copy_787af7ecf0bf6d10") + City.speechUses + ArticyNarrative.Copy("Copy_2f33270ac9233c06") : ArticyNarrative.Copy("Copy_5480bcc4b4f204db")) : ArticyNarrative.Copy("Copy_da46fff6a7f3d4d7"));
                CampaignText("source", Case.source + "\nΑμοιβή συμμετοχής: "+Case.attendanceObols+" οβ. · αποχή / παρατήρηση: 0.");
                return;
            }

            if (id == "vote-result" || id == "vote-abstention")
            {
                if (RenderPoliticalResult(false)) return;
                CampaignText("heading", Case.observation ? ArticyNarrative.Copy("Copy_c4f590baea4f5c33") : CaseRecord.majority == 0 ? ArticyNarrative.Copy("Copy_4740eb1101ce9ab5") : ArticyNarrative.Copy("Copy_d65c0e418f9160b4"));
                CampaignText("body", Case.observation ? ArticyNarrative.Copy("Copy_5920ca96b1e612f6") : ArticyNarrative.Copy("Copy_c7dfb3d48e356be3"));
                CampaignText("majority.0", Case.observation ? ArticyNarrative.Copy("Copy_61dd64b4cf9142f7") : CaseRecord.majority == 0 ? ArticyNarrative.Copy("Copy_c60eadba92c53be3") : ArticyNarrative.Copy("Copy_6eeafa245849c490"));
                CampaignText("majority.1", Case.observation ? ArticyNarrative.Copy("Copy_a4b8d8086ffb7819") : CaseRecord.majority == 1 ? ArticyNarrative.Copy("Copy_36275f815286c1e4") : ArticyNarrative.Copy("Copy_162f09f7a0be8bbc"));
                var bar = Get<Image>(id + ".majority.bar");
                if (bar != null)
                    bar.rectTransform.sizeDelta = new Vector2((CaseRecord.majority == 0 ? 539 : 359) * 1.2f, 8 * 1.2f);
                CampaignText("city-effect", "Ταμείο πόλης: "+CaseRecord.cityDelta.ToString("+0;-0;0")+" οβ.");
                CampaignText("personal-effect", "Προσωπικό ταμείο: "+CaseRecord.personalDelta.ToString("+0;-0;0")+" οβ. · αμοιβή συμμετοχής");
                CampaignText("speech-effect", CaseRecord.vote == 2 ? Case.observation ? ArticyNarrative.Copy("Copy_36ce9e21ee6c27e5") : ArticyNarrative.Copy("Copy_5dc4c89644ccd8a8") : CaseRecord.speech < 2 ? ArticyNarrative.Copy("Copy_6aa504d763ff2df3") : ArticyNarrative.Copy("Copy_cfe1dc0915e117fb"));
                CampaignText("source", Case.observation ? ArticyNarrative.Copy("Copy_d89da5913a9fc4db") + Case.source : ArticyNarrative.Copy("Copy_1067f4424df4677d") + Case.source);
                return;
            }

            if (id == "week-report")
            {
                if (RenderPoliticalResult(true)) return;
                CampaignText("heading", ArticyNarrative.Copy("Copy_ac86789647c262c8"));
                string decision=CaseRecord.vote==2 ? "Δεν υποστήριξες καμία πρόταση." : "Η επιλογή σου: "+(CaseRecord.vote==0 ? "πρώτη" : "δεύτερη")+" πρόταση.";
                string result=Case.observation ? "Κατέγραψες εκτίμηση των στοιχείων, χωρίς δικαστική ψήφο." : "Επικράτησε η "+(CaseRecord.majority==0 ? "πρώτη" : "δεύτερη")+" πρόταση.";
                CampaignText("body", (Case.institution ?? InstitutionNames[Case.site])+" · "+Case.title+"\n\n"+result+"\n\n"+decision+"\n\n"+(CaseRecord.speech<2&&!Case.observation ? "Τοποθετήθηκες δημόσια. Η ενίσχυση λογογράφου έχει περιορισμένη διάρκεια." : "Δεν υπήρξε δημόσια τοποθέτησή σου.")+"\n\n"+(CaseRecord.knowledge==(CaseRecord.questionCorrect>=0 ? CaseRecord.questionCorrect : Case.correct) ? "Απάντησες σωστά στην ερώτηση γνώσης." : "Η απάντηση γνώσης συνοδεύτηκε από επεξήγηση.")+"\n\nΜεταβολή πόλης: "+CaseRecord.cityDelta.ToString("+0;-0;0")+" οβ. · Προσωπικού ταμείου: "+CaseRecord.personalDelta.ToString("+0;-0;0")+" οβ.");
                CampaignText("role", "Ταμείο πόλης: "+State.cityFunds+" οβ.\nΠροσωπικό: "+State.personalFunds+" οβ.\n\nΣυνέπεια "+State.metrics.consistency+" · Ηθική "+State.metrics.ethics+"\nΑυθεντία "+State.metrics.authority+" · Πειθώ "+State.metrics.persuasion+"\nΠαρρησία "+State.metrics.parrhesia+" · Αναγνωρισιμότητα "+State.metrics.recognition);
                CampaignText("source", MetricDeltas(CaseRecord.metricDelta));
                CampaignLabel("primary", State.week == 5 ? ArticyNarrative.Copy("Copy_910d46162f2a5574") : ArticyNarrative.Copy("Copy_39b53c1534b55e68"));
                return;
            }

            if (id == "week-transition" || id == "prytany-transition")
            {
                CampaignText("heading", id == "week-transition" ? ArticyNarrative.Copy("Copy_3e3295844f841450").TrimEnd() + " " + State.week + ArticyNarrative.Copy("Copy_c9ca01a1fb6fd52c") : TribeName(State.currentTribe) + ArticyNarrative.Copy("Copy_2ca1530bcc6160dd"));
                CampaignText("body", id == "week-transition" ? ArticyNarrative.Copy("Copy_1967453206a07b96") : ArticyNarrative.Copy("Copy_aff5a1cc5552abdf"));
                for (int i = 0; i < 5; i++)
                {
                    var t = Get<PanelGraphic>(id + ".week." + i);
                    if (t != null)
                        t.color = i == State.week - 1 ? new Color32(171, 151, 99, 255) : new Color32(35, 61, 64, 255);
                }

                CampaignText("source", GameSession.Slot < 0 ? ArticyNarrative.Copy("Copy_72b41a8f13543b93") : ArticyNarrative.Copy("Copy_e15e406bba032b98"));
                CampaignLabel("primary", id == "week-transition" ? ArticyNarrative.Copy("Copy_7825116e6db6a24f") : State.prytany == 10 ? ArticyNarrative.Copy("Copy_d654bb1a20dc6e0e") : ArticyNarrative.Copy("Copy_df6d19377abefd11"));
                return;
            }

            if (id == "ending-city" || id == "ending-personal")
            {
                RenderCampaignEnding(id == "ending-personal");
                return;
            }

            if (id == "final-survey")
            {
                for (int row = 0; row < 2; row++)
                    for (int i = 0; i < 7; i++)
                        PaintRow("final-survey.rating." + row + "." + i, Campaign.survey[row] == i + 1);
                SetInteractable("final-survey.primary", Campaign.survey.All(x => x >= 1));
                return;
            }

            if (id == "complete")
            {
                int score = Campaign.finalAnswers.Select((a, i) => a == (i == 0 ? 1 : AssessmentCase(i).correct) ? 1 : 0).Sum();
                CampaignText("body", ArticyNarrative.Copy("Copy_d470fb1758f2276b") + score + ArticyNarrative.Copy("Copy_b813c0850f701357"));
                return;
            }
        }

        void RenderCampaignEnding(bool personal)
        {
            if (string.IsNullOrEmpty(Campaign.cityEnding))
                CampaignRules.Endings(State);
            string outcome = personal ? Campaign.personalEnding : Campaign.cityEnding;
            string title = outcome == "victory" ? ArticyNarrative.Copy("Copy_4bb17ff7f55a6ef2") : outcome == "defeat" ? ArticyNarrative.Copy("Copy_ededc4201921eb09") : outcome == "agreement" ? ArticyNarrative.Copy("Copy_6c9f70467572af35") : outcome == "subjection" ? ArticyNarrative.Copy("Copy_9cedc1e6d6109bde") : outcome == "sovereignty" ? ArticyNarrative.Copy("Copy_77cffe238e1d3970") : outcome == "honour" ? ArticyNarrative.Copy("Copy_f9a484682b00cd64") : outcome == "exile" ? ArticyNarrative.Copy("Copy_f31d4b743113ee8f") : outcome == "trial" ? ArticyNarrative.Copy("Copy_a1f99dac5f4358cd") : outcome == "envoy" ? ArticyNarrative.Copy("Copy_299e69bd3f08618f") : outcome == "home" ? ArticyNarrative.Copy("Copy_3efc69aa69c4b713") : ArticyNarrative.Copy("Copy_df10e478d222cdde");
            CampaignText("heading", title);
            CampaignImage("ending-" + outcome);
            string body = personal ? ArticyNarrative.Copy("Copy_33b7a28ec52295c8") : outcome == "defeat" ? ArticyNarrative.Copy("Copy_3884b6b8d2592d35") : outcome == "victory" ? ArticyNarrative.Copy("Copy_7f145869c2ffedb0") : outcome == "subjection" ? ArticyNarrative.Copy("Copy_08afcc0898b77f9a") : ArticyNarrative.Copy("Copy_d642c49d605fe51d");
            CampaignText("body", body + "\n\n" + (personal ? ArticyNarrative.Copy("Copy_f8aaad43f01d24f7") : ArticyNarrative.Copy("Copy_71a1d2cf8794dcb9")));
            CampaignText("source", personal ? ArticyNarrative.Copy("Copy_b60cde7705d54676") : ArticyNarrative.Copy("Copy_c678d22f35f6259d"));
        }
    }
}

