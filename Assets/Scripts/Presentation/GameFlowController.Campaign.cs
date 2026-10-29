using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Κοινά της εκστρατείας: πλοήγηση, βοηθητικά render, μεταβάσεις εβδομάδας/πρυτανείας, τελικό ερωτηματολόγιο.
    /// Η ροή της υπόθεσης ζει στο GameFlowController.AgoraCase.cs — εδώ δεν υπάρχουν κανόνες παιχνιδιού.
    /// </summary>
    public sealed partial class GameFlowController
    {
        CampaignState Campaign
        {
            get
            {
                if (State.campaign == null)
                    State.campaign = new CampaignState();
                return State.campaign;
            }
        }

        static string[] InstitutionNames => new[]
        {
            JsonContent.Copy("Copy_d843d43a4d9723ef"),
            JsonContent.Copy("Copy_86071228fc28d521"),
            JsonContent.Copy("Copy_2fbc1a362586a8ce"),
            JsonContent.Copy("Copy_f56b01a1d22b0c31"),
            JsonContent.Copy("Copy_b56a13349a0ea9d6")
        };

        static bool IsCampaignScreen(string id) => CampaignRules.ValidPhase(id);

        void WireCampaignButtons()
        {
            for (int i = 0; i <= DemeSite; i++)
            {
                int n = i;
                Bind("map.institution." + i, () => OpenInstitution(n));
            }
            foreach (string id in new[] { "initial-knowledge", "initial-tendencies", "final-tendencies", "final-knowledge" })
                for (int i = 0; i < 5; i++)
                {
                    int n = i;
                    string target = id;
                    Bind(id + ".answer." + i, () => AnswerCampaign(target, n));
                }
            Bind("week-transition.primary", () => GoCampaign("map-stela"));
            Bind("final-survey.primary", FinishSurvey);
            Bind("complete.primary", () => Show("main-menu"));
            Bind("complete.review", () => GoCampaign("ending-city"));
        }

        void GoCampaign(string id)
        {
            if (State == null || modalOpen)
                return;
            if (!MutateAndSave(() => State.phase = id))
                return;
            Show(id);
        }

        /// <summary>Μετάλλαξη με snapshot/rollback: αν η αποθήκευση αποτύχει, το state επανέρχεται ακέραιο.</summary>
        bool MutateAndSave(System.Action mutate)
        {
            var snapshot = JsonUtility.ToJson(State);
            mutate();
            if (SaveCheckpoint())
                return true;
            GameSession.State = JsonUtility.FromJson<GameState>(snapshot);
            return false;
        }

        void OpenInstitution(int site)
        {
            EnsureState();
            EnsureAgoraReady();
            if (site == AgoraActiveSite && !AgoraCaseResolved)
            {
                if (!MarketVisited)
                {
                    AskMarketVisit();   // ερευνητική ροή: πρώτα ενημέρωση στην Αγορά, μετά η συνεδρίαση
                    return;
                }
                if (AgoraTrialPending) AgoraOpenTrial();   // η δίκη της εβδομάδας προηγείται της συνέλευσης
                else BeginAgoraCase();
            }
            else
                SetStatus(W(CityText.NothingHere));
        }

        void AskMarketVisit()
        {
            marketGateConfirmation = true;
            exitConfirmation = false;
            returnToMenuConfirmation = false;
            pendingDelete = -1;
            OpenModal(JsonContent.Copy("Copy_9c2e71a4f58d03b6"));
        }

        void AnswerCampaign(string screen, int answer) { TryAnswerEvaluation(screen, answer); }

        void FinishSurvey() { TryFinishEvaluationSurvey(); }

        static string Signed(int value) => value.ToString("+0;-0;0");

        /// <summary>Πίνακας εβδομάδας: υπόθεση και χώρος, δίκη, ειρήνη ή πόλεμος, τι μένει να κάνει ο παίκτης.</summary>
        string WeekBoardText()
        {
            var c = CurrentAgoraCase;
            bool trial = WeekFlowRules.HasTrial(c);
            var lines = new List<string> { W(WeekText.BoardCase, c.title, VenueLabel(c.venue)) };
            lines.Add(trial ? W(WeekText.BoardTrial, ProcedureLabel(c.trial.procedure), c.trial.defendant.FullName, TrialVenueLabel(c.trial.venue)) : W(WeekText.BoardNoTrial));
            lines.Add(W(State.polis.war ? WeekText.BoardWar : WeekText.BoardPeace));
            lines.Add(W(trial && !State.citizen.sycophantFlag ? WeekText.BoardMovesTrial : WeekText.BoardMoves));
            if (!MarketVisited) lines.Add(W(WeekText.BoardFirst));
            return string.Join("\n\n", lines);
        }

        /// <summary>Η μεταβολή ανά πρυτανεία (ειρήνη ή πόλεμος) σε μία γραμμή: «Ταμείο +3 · Επισιτισμός +2 · Ετοιμότητα −3».</summary>
        string DriftText(bool war)
        {
            var cfg = AgoraCfg;
            return MetricLabel(Agora.Metric.Treasury) + " " + Signed(war ? cfg.warTreasury : cfg.peaceTreasury) + " · "
                + MetricLabel(Agora.Metric.Food) + " " + Signed(war ? cfg.warFood : cfg.peaceFood) + " · "
                + MetricLabel(Agora.Metric.Readiness) + " " + Signed(war ? cfg.warReadiness : cfg.peaceReadiness);
        }

        /// <summary>Αναφορά της πόλης στο τέλος της πρυτανείας: τι άλλαξε με κάθε απόφαση και τι έρχεται.</summary>
        string PrytanyReportText()
        {
            var lines = new List<string>();
            for (int week = 0; week < CampaignRules.CasesPerPrytany; week++)
            {
                int turn = (State.prytany - 1) * CampaignRules.CasesPerPrytany + week;
                var record = Campaign.cases.FirstOrDefault(r => r != null && r.turn == turn && r.completed);
                if (record == null) continue;
                var c = AgoraContent.CaseAt(State.prytany, week);
                bool passedA = record.majority == 0;
                lines.Add(W(WeekText.ReportCase, c.title, passedA ? "Α" : "Β", DescribeAgoraEffects(passedA ? c.effectsA : c.effectsB)));
                if (c.abolishesPayIfB && !passedA) lines.Add(W(WeekText.ReportPayStopped));
            }
            if (State.prytany < JsonContent.Settings.prytanyCount)
            {
                bool warNext = State.polis.war || WarStartsNext;
                lines.Add(W(warNext ? WeekText.ReportWar : WeekText.ReportPeace, DriftText(warNext)));
                if (State.polis.auditFinePending) lines.Add(W(WeekText.ReportAudit, AgoraCfg.auditFineTreasury));
            }
            return string.Join("\n\n", lines);
        }

        /// <summary>Αρχή του πολέμου (πρυτανεία 5): τι αλλάζει από εδώ και πέρα.</summary>
        string WarNoticeText() =>
            AgoraContent.warTransition + "\n\n" + W(WeekText.WarRules, DriftText(true), AgoraCfg.warEmergencyPoints);

        void PaintRow(string key, bool selected) => CivicUIFactory.PaintSelectable(Get<Button>(key), selected);

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

        void RenderCampaignMap() => RenderAgoraMap();

        void RenderCampaign()
        {
            string id = currentScreen;
            if (TryRenderEvaluationQuestion()) return;
            if (id == "final-survey")
            {
                // Το MEEGA+ ξεκινά από τις τελικές γνώσεις· χωρίς αυτές (π.χ. παλιό save) πάμε εκεί.
                if (!RenderEvaluationSurvey()) GoCampaign("final-knowledge");
                return;
            }
            if (TryRenderAgoraCase()) return;

            if (id == "week-transition" || id == "prytany-transition")
            {
                EnsureAgoraReady();
                AgoraFunds();
                bool warPage = id == "prytany-transition" && WarStartsNext && State.warNoticeSeen;
                CampaignText("heading", id == "week-transition" ? W(WeekText.BoardHeading, State.prytany, State.week)
                    : warPage ? W(WeekText.WarHeading) : TribeName(State.currentTribe) + JsonContent.Copy("Copy_2ca1530bcc6160dd"));
                CampaignText("body", id == "week-transition" ? WeekBoardText() : warPage ? WarNoticeText() : PrytanyReportText());
                for (int i = 0; i < 5; i++)
                {
                    var t = Get<PanelGraphic>(id + ".week." + i);
                    if (t != null)
                    {
                        // Στον πίνακα εβδομάδας η εβδομάδα γράφεται στον τίτλο· τα κουτάκια θα έπεφταν πάνω στο κείμενο.
                        t.gameObject.SetActive(id != "week-transition" && i < CampaignRules.CasesPerPrytany);
                        t.color = i == State.week - 1 ? new Color32(171, 151, 99, 255) : new Color32(35, 61, 64, 255);
                    }
                }
                // Τα δύο πλαίσια «Handover» της σκηνής (πάνω από το κουμπί) δεν δένονται με δεδομένα — κρύβονται.
                if (id == "prytany-transition" && roots.TryGetValue(id, out var transitionRoot))
                    foreach (Transform child in transitionRoot.transform)
                        if (child.name.StartsWith("Handover ", System.StringComparison.Ordinal)) child.gameObject.SetActive(false);
                CampaignText("source", GameSession.Slot < 0 ? JsonContent.Copy("Copy_72b41a8f13543b93") : JsonContent.Copy("Copy_e15e406bba032b98"));
                CampaignLabel("primary", id == "week-transition" ? W(WeekText.BoardPrimary)
                    : WarStartsNext && !State.warNoticeSeen ? W(WeekText.Continue)
                    : State.prytany == JsonContent.Settings.prytanyCount ? JsonContent.Copy("Copy_d654bb1a20dc6e0e") : JsonContent.Copy("Copy_df6d19377abefd11"));
                return;
            }

            if (id == "complete")
                CampaignText("body", W(EndText.CompleteBody, CampaignRules.TotalCases, JsonContent.Settings.prytanyCount));
        }
    }
}
