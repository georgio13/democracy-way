using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    public sealed partial class DWApp
    {
        public TextAsset campaignContent;
        DWCampaignContent campaignData;
        int historySite;
        DWCampaignContent CampaignData => campaignData ?? (campaignData = JsonUtility.FromJson<DWCampaignContent>(campaignContent.text));

        DWCampaignState Campaign
        {
            get
            {
                if (State.campaign == null)
                    State.campaign = new DWCampaignState();
                return State.campaign;
            }
        }

        DWInstitutionCase Case => CampaignData.cases[DWCampaignRules.Turn(State) % CampaignData.cases.Length];

        DWCaseRecord CaseRecord => DWCampaignRules.Current(State);

        static readonly string[] InstitutionNames =
        {
            "Εκκλησία του Δήμου",
            "Βουλή των Πεντακοσίων",
            "Άρειος Πάγος",
            "Ηλιαία · λαϊκά δικαστήρια",
            "Συνέλευση του δήμου"
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
        static bool IsCampaignScreen(string id) => DWCampaignRules.ValidPhase(id) || id == "appeal";
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

            Bind("knowledge-correct.primary", () => GoCampaign("procedure-briefing"));
            Bind("knowledge-incorrect.primary", () => GoCampaign("procedure-briefing"));
            Bind("procedure-briefing.primary", () => GoCampaign(Case.id == "appeal" ? "appeal" : "deliberation"));
            Bind("appeal.primary", () => GoCampaign("deliberation"));
            Bind("deliberation.speech", () => GoCampaign("speech"));
            Bind("deliberation.vote", () => GoCampaign("vote"));
            for (int i = 0; i < 3; i++)
            {
                int n = i;
                Bind("deliberation.choice." + i, () => CastCampaignVote(n));
                Bind("vote.choice." + i, () => CastCampaignVote(n));
                Bind("speech.choice." + i, () => CampaignSpeak(n));
            }

            Bind("vote-result.primary", () => GoCampaign("week-report"));
            Bind("vote-abstention.primary", () => GoCampaign("week-report"));
            Bind("week-report.primary", AdvanceWeek);
            Bind("week-transition.primary", () => GoCampaign("map-stela"));
            Bind("prytany-transition.primary", AdvancePrytany);
            Bind("ending-city.primary", () => GoCampaign("ending-personal"));
            Bind("ending-personal.primary", () => GoCampaign("final-knowledge"));
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
                if (!DWCampaignRules.Answer(State, n))
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
                SetStatus("Στην παρούσα υπόθεση καταγράφεις τη δική σου ερμηνεία, χωρίς να παρεμβαίνεις στο δικαστήριο.");
                return;
            }

            if (CaseRecord.speech < 0)
            {
                int bonus = n == 2 ? 0 : City.speechUses > 0 ? City.speechBonus : 0;
                if (DWCampaignRules.Speak(State, n, bonus) && n != 2 && City.speechUses > 0)
                    ConsumeSpeechSupport();
            }

            GoCampaign("vote");
        }

        void CastCampaignVote(int n)
        {
            if (!DWCampaignRules.Vote(State, n, Case.costA, Case.costB, Case.war, Case.observation))
                return;
            GoCampaign(n == 2 ? "vote-abstention" : "vote-result");
        }

        void AdvanceWeek()
        {
            if (currentScreen != "week-report")
                return;
            var snapshot = JsonUtility.ToJson(State);
            string next = DWCampaignRules.Advance(State);
            if (next == null)
                return;
            if (!SaveCheckpoint())
            {
                DWSession.State = JsonUtility.FromJson<DWGameState>(snapshot);
                return;
            }

            Show(next);
        }

        void AdvancePrytany()
        {
            var snapshot = JsonUtility.ToJson(State);
            string next = DWCampaignRules.NextPrytany(State);
            if (next == null)
                return;
            if (!SaveCheckpoint())
            {
                DWSession.State = JsonUtility.FromJson<DWGameState>(snapshot);
                return;
            }

            Show(next);
        }

        void FinishSurvey()
        {
            if (Campaign.survey.Any(x => x < 1))
                return;
            Campaign.surveyComplete = true;
            GoCampaign("complete");
        }

        void RenderCampaignMap()
        {
            for (int i = 0; i < 4; i++)
                SetActive("map.institution." + i + ".active", Case.site == i && !CaseRecord.completed);
            SetActive("map.deme", Case.site == 4 && !CaseRecord.completed);
            SetText("map.case.label", CaseRecord.completed ? "Η υπόθεση ολοκληρώθηκε · Απολογισμός εβδομάδας" : InstitutionNames[Case.site] + " · " + Case.title);
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
            CampaignText("funds", "Πόλη  " + State.cityFunds + "    |    Προσωπικό  " + State.personalFunds + " δρ.");
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
                CampaignText("banner", quiz ? (id == "initial-knowledge" ? "ΑΡΧΙΚΟΣ" : "ΤΕΛΙΚΟΣ") + " ΕΛΕΓΧΟΣ · ΕΡΩΤΗΣΗ " + (Math.Min(index, 7) + 1) + "/8" : "ΠΡΙΝ ΑΠΟ ΤΗ ΣΥΝΕΔΡΙΑΣΗ");
                CampaignText("heading", quiz && index == 0 ? "Πόσοι βουλευτές από κάθε φυλή συγκροτούν τη Βουλή των Πεντακοσίων;" : c.question);
                CampaignText("body", quiz ? "Η ερώτηση εξετάζει γνώση θεσμών. Η πολιτική σου στάση καταγράφεται χωριστά." : "Σήμερα: " + Case.title + ". Πριν ακούσεις τα επιχειρήματα, θυμήσου το θεσμικό πλαίσιο.");
                for (int i = 0; i < 3; i++)
                    CampaignLabel("answer." + i, (char)('Α' + i) + "   " + (quiz && index == 0 ? new[] { "Δέκα πολίτες από κάθε φυλή", "Πενήντα πολίτες από κάθε φυλή", "Εκατό πολίτες από κάθε φυλή" }[i] : c.answers[i]));
                CampaignText("source", quiz ? "Οι απαντήσεις και η πρόοδος αποθηκεύονται με τη διαδρομή σου. Η γνώση εξετάζεται χωριστά από τις πολιτικές επιλογές." : "Η απάντηση γνώσης καταγράφεται χωριστά από την πολιτική επιλογή της υπόθεσης.");
                return;
            }

            if (id == "knowledge-correct" || id == "knowledge-incorrect")
            {
                CampaignText("seal", "?");
                CampaignText("heading", id == "knowledge-correct" ? (Case.id == "grain" ? "Πρώτα η προετοιμασία.\nΈπειτα η απόφαση." : "Η απάντηση είναι σωστή.\nΑς δούμε τη διαδικασία.") : "Ας ξεκαθαρίσουμε την αρμοδιότητα.");
                CampaignText("body", Case.explanation);
                CampaignText("source", "Πηγή: " + Case.source + ". Η υπόθεση είναι εκπαιδευτική αφηγηματική σύνθεση.");
                return;
            }

            if (id == "procedure-briefing" || id == "appeal")
            {
                CampaignText("heading", id == "appeal" ? "Ποιος λογίζεται πολίτης;" : "Ο κήρυκας καλεί τους πολίτες.");
                CampaignText("body", Case.brief);
                CampaignText("role", Case.observation ? "ΠΑΡΑΚΟΛΟΥΘΗΣΗ\nΔεν είσαι δικαστής σε αυτή την υπόθεση. Η επόμενη επιλογή καταγράφει την ερμηνεία σου, χωρίς ψήφο ή λόγο μέσα στο δικαστήριο." : "Η ΣΥΜΜΕΤΟΧΗ ΣΟΥ\nΑκούς τα δύο επιχειρήματα. Μπορείς να πάρεις τον λόγο, να επιλέξεις πρόταση ή να απέχεις. Η προσωπική ψήφος διακρίνεται από τη συλλογική έκβαση.");
                CampaignText("source", Case.source + " · Οι πέντε εβδομάδες ανά πρυτανεία είναι εκπαιδευτική συμπύκνωση.");
                return;
            }

            if (id == "deliberation")
            {
                CampaignText("heading", Case.title);
                CampaignText("institution", InstitutionNames[Case.site].ToUpperInvariant() + (Case.observation ? " · ΠΑΡΑΚΟΛΟΥΘΗΣΗ" : " · ΣΥΝΕΔΡΙΑΣΗ"));
                CampaignText("argument.0", "«" + Case.argumentA + "»");
                CampaignText("argument.1", "«" + Case.argumentB + "»");
                CampaignLabel("choice.0", Case.observation ? "Με πείθει το πρώτο επιχείρημα" : "Δημόσια απόφαση και λογοδοσία");
                CampaignLabel("choice.1", Case.observation ? "Με πείθει το δεύτερο επιχείρημα" : "Ανάθεση σε περιορισμένο κύκλο");
                CampaignLabel("choice.2", Case.observation ? "Δεν επιλέγω ερμηνεία" : "Δεν ψηφίζω · καταγράφεται αποχή");
                SetInteractable("deliberation.speech", !Case.observation && CaseRecord.speech < 0);
                CampaignText("source", Case.source + " · Λόγοι και πρόσωπα μυθοπλαστικά. Η κλίση αφορά την κατανομή εξουσίας, όχι τη θέση υπέρ ή κατά του πολέμου.");
                return;
            }

            if (id == "speech" || id == "vote")
            {
                CampaignText("heading", id == "speech" ? "Δεν αρκεί να ψηφίσεις. Μπορείς να εκτεθείς." : Case.observation ? "Ποια ερμηνεία σε πείθει;" : "Η δική σου ψήφος.");
                CampaignText("body", id == "speech" ? "Ο κήρυκας κοιτάζει τους συγκεντρωμένους. Ποια αρχή θα υπερασπιστείς δημόσια;" : Case.title);
                CampaignLabel("choice.0", id == "speech" ? "Ο έλεγχος ανήκει σε ολόκληρη την πόλη.\nΠαρρησία +3 · Αναγνωρισιμότητα +2" : "Πρώτη πρόταση · Δημόσια λογοδοσία");
                CampaignLabel("choice.1", id == "speech" ? "Ας αποφασίσουν όσοι εγγυώνται με την περιουσία τους.\nΑυθεντία +2 · Αναγνωρισιμότητα +2" : "Δεύτερη πρόταση · Στενότερη διαχείριση");
                CampaignLabel("choice.2", id == "speech" ? "Κατεβαίνω χωρίς να μιλήσω. Η ψήφος παραμένει διαθέσιμη." : Case.observation ? "Δεν επιλέγω ερμηνεία" : "Αποχή από την ψηφοφορία");
                CampaignText("role", id == "speech" ? (City.speechUses > 0 ? "ΕΤΟΙΜΟΣ ΛΟΓΟΣ\nΕπιρροή +" + City.speechBonus + "\nΑπομένουν " + City.speechUses + " κατάλληλες τοποθετήσεις.\nΚαταναλώνεται μόνο όταν μιλήσεις." : "ΔΙΚΗ ΣΟΥ ΦΩΝΗ\nΔεν έχεις ενεργή ανάθεση σε λογογράφο. Μπορείς να μιλήσεις με τις δικές σου δυνάμεις.") : "ΣΥΛΛΟΓΙΚΗ ΑΠΟΦΑΣΗ\nΗ επιλογή σου επηρεάζει τη συζήτηση. Δεν αντικαθιστά τις αποφάσεις των υπολοίπων.\nΗ αποχή καταγράφεται ξεχωριστά από τη σιωπή.");
                CampaignText("source", Case.source + " · Οι αριθμητικές επιδράσεις είναι κανόνες του παιχνιδιού.");
                return;
            }

            if (id == "vote-result" || id == "vote-abstention")
            {
                CampaignText("heading", Case.observation ? "Η υπόθεση αφήνει ερωτήματα." : CaseRecord.majority == 0 ? "Εγκρίθηκε η δημόσια διαχείριση." : "Επικράτησε η στενότερη ανάθεση.");
                CampaignText("body", Case.observation ? "Καταγράφηκε η προσωπική σου ερμηνεία. Δεν ασκήθηκε δικαστική αρμοδιότητα από τον παίκτη." : "Το συλλογικό αποτέλεσμα υπολογίστηκε χωριστά από τη δική σου επιλογή. Τα επιχειρήματα, η δημόσια έκθεση και η διαθέσιμη υποστήριξη επηρέασαν την έκβαση.");
                CampaignText("majority.0", Case.observation ? "Πρώτο επιχείρημα" : CaseRecord.majority == 0 ? "Υπέρ · πλειοψηφία" : "Υπέρ · μειοψηφία");
                CampaignText("majority.1", Case.observation ? "Δεύτερο επιχείρημα" : CaseRecord.majority == 1 ? "Κατά · πλειοψηφία" : "Κατά · μειοψηφία");
                var bar = Get<Image>(id + ".majority.bar");
                if (bar != null)
                    bar.rectTransform.sizeDelta = new Vector2((CaseRecord.majority == 0 ? 539 : 359) * 1.2f, 8 * 1.2f);
                CampaignText("city-effect", CaseRecord.cityDelta + " μονάδες");
                CampaignText("personal-effect", "Καμία μεταβολή");
                CampaignText("speech-effect", CaseRecord.vote == 2 ? Case.observation ? "Χωρίς επιλογή ερμηνείας" : "Αποχή από την ψηφοφορία" : CaseRecord.speech < 2 ? "Ο λόγος σου ακούστηκε" : "Επέλεξες χωρίς ομιλία");
                CampaignText("source", Case.observation ? "Προσωπική ερμηνεία, χωρίς ψήφο δικαστή. " + Case.source : "Σχηματική πλειοψηφία, όχι υποτιθέμενη αρχαία καταμέτρηση. " + Case.source);
                return;
            }

            if (id == "week-report")
            {
                CampaignText("heading", "Η εβδομάδα αφήνει το ίχνος της.");
                CampaignText("body", InstitutionNames[Case.site] + " · " + Case.title + "\n\n" + (Case.observation ? "Παρακολούθησες και εξέτασες τα επιχειρήματα." : CaseRecord.majority == 0 ? "Εγκρίθηκε η πρόταση δημόσιου ελέγχου." : "Επικράτησε η πρόταση στενότερης διαχείρισης.") + "\n\n" + (CaseRecord.vote == 2 ? "Δεν επέλεξες πλευρά." : "Η δική σου επιλογή: " + (CaseRecord.vote == 0 ? "πρώτο" : "δεύτερο") + " επιχείρημα.") + "\n\n" + (CaseRecord.speech < 2 ? "Ο λόγος σου ακούστηκε. Η υποστήριξη λογογράφου έχει περιορισμένη διάρκεια." : "Άκουσες χωρίς να πάρεις τον λόγο.") + "\n\nΗ ερώτηση γνώσης απαντήθηκε " + (CaseRecord.knowledge == Case.correct ? "σωστά." : "με επεξήγηση του λάθους."));
                CampaignText("role", "Η ΠΟΛΗ ΚΑΙ ΕΣΥ\n\nΤαμείο πόλης   " + State.cityFunds + "\nΠροσωπικό ταμείο   " + State.personalFunds + " δρ.\nΠαρρησία   " + State.metrics.parrhesia + "\nΑναγνωρισιμότητα   " + State.metrics.recognition + "\n\nΔαπάνη εβδομάδας   " + CaseRecord.cityDelta + "\n\n" + City.complaintDecisions.Count(string.IsNullOrEmpty) + " αναφορές αναμένουν εξέταση.");
                CampaignText("source", CampaignData.notice);
                CampaignLabel("primary", State.week == 5 ? "Ολοκλήρωση πρυτανείας" : "Στην επόμενη εβδομάδα");
                return;
            }

            if (id == "week-transition" || id == "prytany-transition")
            {
                CampaignText("heading", id == "week-transition" ? "Η " + State.week + "η εβδομάδα αρχίζει." : TribeName(State.currentTribe) + " παραδίδει.");
                CampaignText("body", id == "week-transition" ? "Οι αποφάσεις έφυγαν από την Πνύκα.\nΤώρα θα συναντήσεις τις συνέπειές τους στην πόλη." : "Πέντε εβδομάδες, πέντε θεσμικές δραστηριότητες.\nΆλλες έκλεισαν. Άλλες θα ακολουθήσουν τη νέα πρυτανεία.");
                for (int i = 0; i < 5; i++)
                {
                    var t = Get<DWShape>(id + ".week." + i);
                    if (t != null)
                        t.color = i == State.week - 1 ? new Color32(171, 151, 99, 255) : new Color32(35, 61, 64, 255);
                }

                CampaignText("source", DWSession.Slot < 0 ? "Προεπισκόπηση Editor · Δεν γράφεται πραγματικό slot." : "Η τοπική εγγραφή του checkpoint ολοκληρώθηκε.");
                CampaignLabel("primary", id == "week-transition" ? "Επιστροφή στον χάρτη" : State.prytany == 10 ? "Στον επίλογο" : "Στην επόμενη πρυτανεία");
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
                CampaignText("body", "Γνώρισες θεσμούς, ανέλαβες ευθύνες και είδες πόσο δύσκολα συναντιούνται οι ιδιωτικές ανάγκες με τις αποφάσεις της πόλης.\n\nΟ δικός σου τελικός έλεγχος: " + score + " / 8.\nΟι απαντήσεις διατηρούνται στο τοπικό slot. Δεν παρουσιάζονται ως αποτελέσματα έρευνας παικτών.");
                return;
            }
        }

        void RenderInstitutionHistory()
        {
            CampaignText("heading", "Τι κρατά από την πόλη ο θεσμός;");
            CampaignText("eyebrow", InstitutionNames[Math.Min(4, historySite)]);
            var records = Campaign.cases.Where(x => x.completed && CampaignData.cases[x.turn % CampaignData.cases.Length].site == historySite).OrderByDescending(x => x.turn).Take(4);
            string history = string.Join("\n\n", records.Select(x => "Πρυτανεία " + (x.turn / 5 + 1) + " · Εβδομάδα " + (x.turn % 5 + 1) + "\n" + CampaignData.cases[x.turn % CampaignData.cases.Length].title + " · " + (x.majority == 0 ? "Δημόσιος έλεγχος" : "Στενότερη ανάθεση")));
            CampaignText("body", string.IsNullOrEmpty(history) ? "Δεν έχεις ολοκληρώσει ακόμη δραστηριότητα σε αυτόν τον θεσμό.\n\nΤρέχων απόηχος: " + (historySite == 0 ? "Στην Πνύκα συζητούν την ανάγκη προετοιμασίας της ημερήσιας διάταξης." : historySite == 1 ? "Στο βουλευτήριο ελέγχονται κατάλογοι και προετοιμάζονται εισηγήσεις." : historySite == 2 ? "Η πόλη συζητά τα όρια της αρμοδιότητας των παλαιών αρχόντων." : "Οι πολίτες συζητούν τη διαφορά ανάμεσα στη μαρτυρία και στην απόδειξη.") + "\n\nΟ απόηχος είναι αφηγηματική σύνθεση, όχι καταγεγραμμένο αρχαίο συμβάν." : history);
        }

        void RenderCampaignEnding(bool personal)
        {
            if (string.IsNullOrEmpty(Campaign.cityEnding))
                DWCampaignRules.Endings(State);
            string outcome = personal ? Campaign.personalEnding : Campaign.cityEnding;
            string title = outcome == "victory" ? "Η πόλη κράτησε τη φωνή της." : outcome == "defeat" ? "Η ήττα έφτασε στην Αθήνα." : outcome == "agreement" ? "Μια συμφωνία με τίμημα." : outcome == "subjection" ? "Η πόλη αποφασίζει λιγότερα." : outcome == "sovereignty" ? "Η αυτονομία διατηρήθηκε." : outcome == "honour" ? "Το όνομά σου ακούγεται στην πόλη." : outcome == "exile" ? "Ο δρόμος οδηγεί μακριά από την Αθήνα." : outcome == "trial" ? "Οι λογαριασμοί στρέφονται προς εσένα." : outcome == "envoy" ? "Ο λόγος σου ταξιδεύει έξω από τα τείχη." : outcome == "home" ? "Ο οίκος μένει. Η πολιτική στενεύει." : "Αύριο θα χρειαστεί να πείσεις ξανά.";
            CampaignText("heading", title);
            CampaignImage("ending-" + outcome);
            string body = personal ? "Οι δημόσιες δεσμεύσεις, η αναγνωρισιμότητα, η πειθώ και ο κίνδυνος διαφθοράς έδωσαν στην προσωπική διαδρομή σου αυτή την κατάληξη. Η αναγνώριση δεν σε κάνει κυρίαρχο της Αθήνας ούτε σου απονέμει αυθαίρετα αξίωμα." : outcome == "defeat" ? "Η αντοχή της πόλης δεν στάθηκε αρκετή για τη σύγκρουση που επέλεξε. Οι διαπραγματεύσεις γίνονται πια από ασθενέστερη θέση. Οι αποφάσεις των εβδομάδων συναντούν το κόστος τους." : outcome == "victory" ? "Οι δαπάνες της άμυνας, η προετοιμασία και οι συμμαχίες επέτρεψαν στην πόλη να αντέξει. Στην Πνύκα θα ξανακουστούν διαφωνίες. Κανείς δεν κέρδισε μια πόλη χωρίς αντιρρήσεις." : outcome == "subjection" ? "Η συστηματική μεταφορά της διαχείρισης σε στενότερο κύκλο αποδυνάμωσε τη δημόσια συμμετοχή. Η εξωτερική πίεση βρήκε την πόλη πρόθυμη να παραχωρήσει περισσότερα." : "Οι πολίτες απέφυγαν την αποφασιστική πολεμική αναμέτρηση. Η συμφωνία και η αυτονομία εξαρτήθηκαν από τους πόρους, τη δημόσια νομιμοποίηση και τις δεσμεύσεις των προηγούμενων εβδομάδων.";
            CampaignText("body", body + "\n\n" + (personal ? "Μυθοπλαστική προσωπική διαδρομή· η ανάδειξη σε αξιώματα ακολουθεί την προβλεπόμενη διαδικασία." : "Ιστορική αντιπαραβολή: το 322 π.Χ. η Αθήνα ηττήθηκε στον Λαμιακό πόλεμο και το πολίτευμα περιορίστηκε με περιουσιακό κριτήριο. Οι άλλες εκβάσεις είναι ρητά εναλλακτική ιστορία."));
            CampaignText("source", personal ? "Η έκβαση απορρέει από την αποθηκευμένη κατάσταση του παιχνιδιού." : "Ιστορικό σημείο αναφοράς: Διόδωρος 18.17–18 · Πλούταρχος, Φωκίων 27–28. Οι αριθμητικοί κανόνες είναι σχεδιαστικοί.");
        }
    }
}
