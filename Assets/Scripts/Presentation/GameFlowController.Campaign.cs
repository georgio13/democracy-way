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
            for (int i = 0; i < 4; i++)
            {
                int n = i;
                Bind("map.institution." + i, () => OpenInstitution(n));
            }
            Bind("map.deme", () => OpenInstitution(4));
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
            if (site == AgoraSiteIndex && !AgoraCaseResolved)
            {
                if (State.marketVisitedTurn != CampaignRules.Turn(State))
                {
                    AskMarketVisit();   // ερευνητική ροή: πρώτα ενημέρωση στην Αγορά, μετά η συνεδρίαση
                    return;
                }

                BeginAgoraCase();
            }
            else
                SetStatus("Δεν συνεδριάζει κάτι εδώ αυτή την εβδομάδα.");
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
                AgoraFunds();
                CampaignText("heading", id == "week-transition" ? JsonContent.Copy("Copy_3e3295844f841450").TrimEnd() + " " + State.week + JsonContent.Copy("Copy_c9ca01a1fb6fd52c") : TribeName(State.currentTribe) + JsonContent.Copy("Copy_2ca1530bcc6160dd"));
                CampaignText("body", id == "week-transition" ? JsonContent.Copy("Copy_1967453206a07b96") : JsonContent.Copy("Copy_aff5a1cc5552abdf"));
                for (int i = 0; i < 5; i++)
                {
                    var t = Get<PanelGraphic>(id + ".week." + i);
                    if (t != null)
                    {
                        t.gameObject.SetActive(i < CampaignRules.CasesPerPrytany);
                        t.color = i == State.week - 1 ? new Color32(171, 151, 99, 255) : new Color32(35, 61, 64, 255);
                    }
                }
                // Τα δύο πλαίσια «Handover» της σκηνής (πάνω από το κουμπί) δεν δένονται με δεδομένα — κρύβονται.
                if (id == "prytany-transition" && roots.TryGetValue(id, out var transitionRoot))
                    foreach (Transform child in transitionRoot.transform)
                        if (child.name.StartsWith("Handover ", System.StringComparison.Ordinal)) child.gameObject.SetActive(false);
                CampaignText("source", GameSession.Slot < 0 ? JsonContent.Copy("Copy_72b41a8f13543b93") : JsonContent.Copy("Copy_e15e406bba032b98"));
                CampaignLabel("primary", id == "week-transition" ? JsonContent.Copy("Copy_7825116e6db6a24f") : State.prytany == JsonContent.Settings.prytanyCount ? JsonContent.Copy("Copy_d654bb1a20dc6e0e") : JsonContent.Copy("Copy_df6d19377abefd11"));
                return;
            }

            if (id == "complete")
                CampaignText("body", "Ολοκλήρωσες " + CampaignRules.TotalCases + " υποθέσεις σε " + JsonContent.Settings.prytanyCount + " πρυτανείες. Οι απαντήσεις σου αποθηκεύτηκαν.\n\nΟι απαντήσεις γνώσεων, τάσεων και εμπειρίας καταγράφονται χωριστά.");
        }
    }
}
