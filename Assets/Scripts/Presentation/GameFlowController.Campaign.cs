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

        InstitutionCase Case => CampaignData.cases[CampaignRules.Turn(State) % CampaignData.cases.Length];

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
            Bind("deliberation.vote", () => FollowStory("DeliberationVote"));
            for (int i = 0; i < 3; i++)
            {
                int n = i;
                Bind("deliberation.choice." + i, () => CastCampaignVote(n));
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
                GoCampaign(CaseRecord.knowledge < 0 ? "knowledge" : "procedure-briefing");
            }
            else
                GoCampaign("institution-history");
        }

        void AnswerCampaign(string screen, int n)
        {
            if (screen == "knowledge")
            {
                if (!CampaignRules.Answer(State, n))
                    return;
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
            if (Case.observation && n < 2)
            {
                SetStatus(ArticyNarrative.Copy("Copy_2afaa10a70e5cf45"));
                return;
            }

            if (CaseRecord.speech < 0)
            {
                int bonus = n == 2 ? 0 : City.speechUses > 0 ? City.speechBonus : 0;
                if (CampaignRules.Speak(State, n, bonus) && n != 2 && City.speechUses > 0)
                    ConsumeSpeechSupport();
            }

            FollowStory("SpeechContinue");
        }

        void CastCampaignVote(int n)
        {
            if (!CampaignRules.Vote(State, n, Case.costA, Case.costB, Case.war, Case.observation))
                return;
            GoCampaign(ArticyNarrative.Route("VoteResult",State,Case.id,n));
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
            if (Campaign.survey.Any(x => x < 1))
                return;
            Campaign.surveyComplete = true;
            FollowStory("FinalSurveyComplete");
        }

        void RenderCampaignMap()
        {
            for (int i = 0; i < 4; i++)
                SetActive("map.institution." + i + ".active", Case.site == i && !CaseRecord.completed);
            SetActive("map.deme", Case.site == 4 && !CaseRecord.completed);
            SetText("map.case.label", CaseRecord.completed ? ArticyNarrative.Copy("Copy_b383cd29aada7bc7") : InstitutionNames[Case.site] + " · " + Case.title);
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

        int[] QuizIndices =
        {
            0,
            1,
            2,
            5,
            6,
            7,
            8,
            9
        };
        void RenderCampaign()
        {
            string id = currentScreen;
            if (campaignContent == null)
                return;
            CampaignText("funds", ArticyNarrative.Copy("Copy_9fd25730f3bc1ab9") + State.cityFunds + ArticyNarrative.Copy("Copy_f9278043e324c0bc") + State.personalFunds + ArticyNarrative.Copy("Copy_6cbc6fd72c7bdc12"));
            if (id == "institution-history")
            {
                RenderInstitutionHistory();
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
                var c = quiz ? CampaignData.cases[QuizIndices[Math.Min(7, index)]] : Case;
                CampaignText("banner", quiz ? (id == "initial-knowledge" ? ArticyNarrative.Copy("Copy_3fbbd6062db820e6") : ArticyNarrative.Copy("Copy_ed29f6b542d3a7ad")) + ArticyNarrative.Copy("Copy_7497f4359296eb12") + (Math.Min(index, 7) + 1) + "/8" : ArticyNarrative.Copy("Copy_53405a8145bf2dfe"));
                CampaignText("heading", quiz && index == 0 ? ArticyNarrative.Copy("Copy_c5878ab2fda6586b") : c.question);
                CampaignText("body", quiz ? ArticyNarrative.Copy("Copy_f2552600441f7137") : ArticyNarrative.Copy("Copy_9f974b6ae4ac0b34") + Case.title + ArticyNarrative.Copy("Copy_c20dc950f5982180"));
                for (int i = 0; i < 3; i++)
                    CampaignLabel("answer." + i, (char)('Α' + i) + "   " + (quiz && index == 0 ? new[] { ArticyNarrative.Copy("Copy_1631e8d5409bcdb6"), ArticyNarrative.Copy("Copy_26b0375e5ed1a510"), ArticyNarrative.Copy("Copy_215939b847d2e67c") }[i] : c.answers[i]));
                CampaignText("source", quiz ? ArticyNarrative.Copy("Copy_1d57091a46f50302") : ArticyNarrative.Copy("Copy_3cc3c8bead627006"));
                return;
            }

            if (id == "knowledge-correct" || id == "knowledge-incorrect")
            {
                CampaignText("seal", "?");
                CampaignText("heading", id == "knowledge-correct" ? (Case.id == "grain" ? ArticyNarrative.Copy("Copy_bb8a0b2b2732efb1") : ArticyNarrative.Copy("Copy_efc30c471c9d9686")) : ArticyNarrative.Copy("Copy_e3f63fadc3624d22"));
                CampaignText("body", Case.explanation);
                CampaignText("source", ArticyNarrative.Copy("Copy_c17ca86d5c33684a") + Case.source + ArticyNarrative.Copy("Copy_105a6ddb13eadf83"));
                return;
            }

            if (id == "procedure-briefing" || id == "appeal")
            {
                CampaignText("heading", id == "appeal" ? ArticyNarrative.Copy("Copy_3027ad69a3db1d67") : ArticyNarrative.Copy("Copy_e5e29d2e7d06934f"));
                CampaignText("body", Case.brief);
                CampaignText("role", Case.observation ? ArticyNarrative.Copy("Copy_f85bd31fc92ebd77") : ArticyNarrative.Copy("Copy_1d174bf1ab96eb21"));
                CampaignText("source", Case.source + ArticyNarrative.Copy("Copy_d639284ef6ba7c02"));
                return;
            }

            if (id == "deliberation")
            {
                CampaignText("heading", Case.title);
                CampaignText("institution", InstitutionNames[Case.site].ToUpperInvariant() + (Case.observation ? ArticyNarrative.Copy("Copy_3f00a343e8208107") : ArticyNarrative.Copy("Copy_7c3f56f6a142be8b")));
                CampaignText("argument.0", "«" + Case.argumentA + "»");
                CampaignText("argument.1", "«" + Case.argumentB + "»");
                CampaignLabel("choice.0", Case.observation ? ArticyNarrative.Copy("Copy_0b9ceeb7aeb4b781") : ArticyNarrative.Copy("Copy_c2ad6d98110ae3a1"));
                CampaignLabel("choice.1", Case.observation ? ArticyNarrative.Copy("Copy_c9cf02439a132666") : ArticyNarrative.Copy("Copy_edefc3af0366ead3"));
                CampaignLabel("choice.2", Case.observation ? ArticyNarrative.Copy("Copy_47f45e8efd2ad58d") : ArticyNarrative.Copy("Copy_cb6ec07aff936e23"));
                SetInteractable("deliberation.speech", !Case.observation && CaseRecord.speech < 0);
                CampaignText("source", Case.source + ArticyNarrative.Copy("Copy_e6397e79400ef6f5"));
                return;
            }

            if (id == "speech" || id == "vote")
            {
                CampaignText("heading", id == "speech" ? ArticyNarrative.Copy("Copy_fd2a025dfbcc5500") : Case.observation ? ArticyNarrative.Copy("Copy_595a4262b7a284e3") : ArticyNarrative.Copy("Copy_111d1a88d8ec5459"));
                CampaignText("body", id == "speech" ? ArticyNarrative.Copy("Copy_4ee6e91c465e068d") : Case.title);
                CampaignLabel("choice.0", id == "speech" ? ArticyNarrative.Copy("Copy_fba2d921d0335163") : ArticyNarrative.Copy("Copy_432df8f8f9826174"));
                CampaignLabel("choice.1", id == "speech" ? ArticyNarrative.Copy("Copy_7381b2d39a137766") : ArticyNarrative.Copy("Copy_6b5411ff18007acd"));
                CampaignLabel("choice.2", id == "speech" ? ArticyNarrative.Copy("Copy_4e4312e35e52b30d") : Case.observation ? ArticyNarrative.Copy("Copy_47f45e8efd2ad58d") : ArticyNarrative.Copy("Copy_5dc4c89644ccd8a8"));
                CampaignText("role", id == "speech" ? (City.speechUses > 0 ? ArticyNarrative.Copy("Copy_3c1124cd8cf502aa") + City.speechBonus + ArticyNarrative.Copy("Copy_787af7ecf0bf6d10") + City.speechUses + ArticyNarrative.Copy("Copy_2f33270ac9233c06") : ArticyNarrative.Copy("Copy_5480bcc4b4f204db")) : ArticyNarrative.Copy("Copy_da46fff6a7f3d4d7"));
                CampaignText("source", Case.source + ArticyNarrative.Copy("Copy_2e9392c7b6c8cc11"));
                return;
            }

            if (id == "vote-result" || id == "vote-abstention")
            {
                CampaignText("heading", Case.observation ? ArticyNarrative.Copy("Copy_c4f590baea4f5c33") : CaseRecord.majority == 0 ? ArticyNarrative.Copy("Copy_4740eb1101ce9ab5") : ArticyNarrative.Copy("Copy_d65c0e418f9160b4"));
                CampaignText("body", Case.observation ? ArticyNarrative.Copy("Copy_5920ca96b1e612f6") : ArticyNarrative.Copy("Copy_c7dfb3d48e356be3"));
                CampaignText("majority.0", Case.observation ? ArticyNarrative.Copy("Copy_61dd64b4cf9142f7") : CaseRecord.majority == 0 ? ArticyNarrative.Copy("Copy_c60eadba92c53be3") : ArticyNarrative.Copy("Copy_6eeafa245849c490"));
                CampaignText("majority.1", Case.observation ? ArticyNarrative.Copy("Copy_a4b8d8086ffb7819") : CaseRecord.majority == 1 ? ArticyNarrative.Copy("Copy_36275f815286c1e4") : ArticyNarrative.Copy("Copy_162f09f7a0be8bbc"));
                var bar = Get<Image>(id + ".majority.bar");
                if (bar != null)
                    bar.rectTransform.sizeDelta = new Vector2((CaseRecord.majority == 0 ? 539 : 359) * 1.2f, 8 * 1.2f);
                CampaignText("city-effect", CaseRecord.cityDelta + ArticyNarrative.Copy("Copy_732101c50b7cbd87"));
                CampaignText("personal-effect", ArticyNarrative.Copy("Copy_c9df44a5f28c34e3"));
                CampaignText("speech-effect", CaseRecord.vote == 2 ? Case.observation ? ArticyNarrative.Copy("Copy_36ce9e21ee6c27e5") : ArticyNarrative.Copy("Copy_5dc4c89644ccd8a8") : CaseRecord.speech < 2 ? ArticyNarrative.Copy("Copy_6aa504d763ff2df3") : ArticyNarrative.Copy("Copy_cfe1dc0915e117fb"));
                CampaignText("source", Case.observation ? ArticyNarrative.Copy("Copy_d89da5913a9fc4db") + Case.source : ArticyNarrative.Copy("Copy_1067f4424df4677d") + Case.source);
                return;
            }

            if (id == "week-report")
            {
                CampaignText("heading", ArticyNarrative.Copy("Copy_ac86789647c262c8"));
                CampaignText("body", InstitutionNames[Case.site] + " · " + Case.title + "\n\n" + (Case.observation ? ArticyNarrative.Copy("Copy_b7406bb31eaa7a39") : CaseRecord.majority == 0 ? ArticyNarrative.Copy("Copy_cf1747a50e2679e1") : ArticyNarrative.Copy("Copy_783127c8f5fefcc5")) + "\n\n" + (CaseRecord.vote == 2 ? ArticyNarrative.Copy("Copy_7df158ede8a0ee70") : ArticyNarrative.Copy("Copy_a961867b82e71202") + (CaseRecord.vote == 0 ? ArticyNarrative.Copy("Copy_d4405127fbc9ae5e") : ArticyNarrative.Copy("Copy_db0db6b241bed857")) + ArticyNarrative.Copy("Copy_eef2b137c134eb50")) + "\n\n" + (CaseRecord.speech < 2 ? ArticyNarrative.Copy("Copy_974969471a8f45d0") : ArticyNarrative.Copy("Copy_99282444044d7650")) + ArticyNarrative.Copy("Copy_d964c26c06f56fa1") + (CaseRecord.knowledge == Case.correct ? ArticyNarrative.Copy("Copy_da91596b84166fad") : ArticyNarrative.Copy("Copy_240915e4424cb107")));
                CampaignText("role", ArticyNarrative.Copy("Copy_612f3cd86f799e4b") + State.cityFunds + ArticyNarrative.Copy("Copy_d20b9c69e20b6771") + State.personalFunds + ArticyNarrative.Copy("Copy_6c48c09043274272") + State.metrics.parrhesia + ArticyNarrative.Copy("Copy_a5423f346f29050d") + State.metrics.recognition + ArticyNarrative.Copy("Copy_84eb35ee4d529996") + CaseRecord.cityDelta + "\n\n" + City.complaintDecisions.Count(string.IsNullOrEmpty) + ArticyNarrative.Copy("Copy_500b35659a4cd66c"));
                CampaignText("source", CampaignData.notice);
                CampaignLabel("primary", State.week == 5 ? ArticyNarrative.Copy("Copy_910d46162f2a5574") : ArticyNarrative.Copy("Copy_39b53c1534b55e68"));
                return;
            }

            if (id == "week-transition" || id == "prytany-transition")
            {
                CampaignText("heading", id == "week-transition" ? ArticyNarrative.Copy("Copy_3e3295844f841450") + State.week + ArticyNarrative.Copy("Copy_c9ca01a1fb6fd52c") : TribeName(State.currentTribe) + ArticyNarrative.Copy("Copy_2ca1530bcc6160dd"));
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
                int score = Campaign.finalAnswers.Select((a, i) => a == (i == 0 ? 1 : CampaignData.cases[QuizIndices[i]].correct) ? 1 : 0).Sum();
                CampaignText("body", ArticyNarrative.Copy("Copy_d470fb1758f2276b") + score + ArticyNarrative.Copy("Copy_b813c0850f701357"));
                return;
            }
        }

        void RenderInstitutionHistory()
        {
            CampaignText("heading", ArticyNarrative.Copy("Copy_6cab354ce8aee2fb"));
            CampaignText("eyebrow", InstitutionNames[Math.Min(4, historySite)]);
            var records = Campaign.cases.Where(x => x.completed && CampaignData.cases[x.turn % CampaignData.cases.Length].site == historySite).OrderByDescending(x => x.turn).Take(4);
            string history = string.Join("\n\n", records.Select(x => ArticyNarrative.Copy("Copy_0008e0f7f5eda78f") + (x.turn / 5 + 1) + ArticyNarrative.Copy("Copy_238d1efb10f3eefa") + (x.turn % 5 + 1) + "\n" + CampaignData.cases[x.turn % CampaignData.cases.Length].title + " · " + (x.majority == 0 ? ArticyNarrative.Copy("Copy_2c47a95787d4bf1c") : ArticyNarrative.Copy("Copy_04b7703dc771f57a"))));
            CampaignText("body", string.IsNullOrEmpty(history) ? ArticyNarrative.Copy("Copy_d27923a55ff7c9e5") + (historySite == 0 ? ArticyNarrative.Copy("Copy_3f1339017bd7d6d8") : historySite == 1 ? ArticyNarrative.Copy("Copy_4879bedd54e11446") : historySite == 2 ? ArticyNarrative.Copy("Copy_4e0e93a7f0ecf5d7") : ArticyNarrative.Copy("Copy_bcab987b8a049de8")) + ArticyNarrative.Copy("Copy_69e8ed290088041a") : history);
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
