using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Ενιαίο σύστημα: η ροή της εβδομάδας μέσα στον GameFlowController (Agora_FINAL_DESIGN ενότητα 1).
    /// Πίνακας εβδομάδας → Αγορά → [δίκη στην Ηλιαία ή στον Άρειο Πάγο] → συνέλευση (λόγοι, βήμα, ψήφος)
    /// → ανάλυση → απολογισμός με ΕΚ3. Κανόνες ΜΟΝΟ από Agora.AssemblyRules/EndingRules μέσω WeekFlowRules·
    /// περιεχόμενο ΜΟΝΟ από το GameContent και το ContentLibrary.
    /// </summary>
    public sealed partial class GameFlowController
    {
        readonly System.Random agoraRng = new System.Random();

        Agora.GameContent AgoraContent => config.agoraContent;   // εγγυημένο από τον έλεγχο του Awake
        Agora.GameConfig AgoraCfg => config.agoraContent.config;
        Agora.CaseData CurrentAgoraCase => AgoraContent.CaseAt(State.prytany, State.week - 1);
        AgoraCaseState AgoraState => State.agoraCase;
        bool AgoraCaseResolved => AgoraState.caseId == CurrentAgoraCase.id && AgoraState.resolved;
        bool AgoraCaseCurrent => AgoraState.caseId == CurrentAgoraCase.id;
        bool MarketVisited => State.marketVisitedTurn == CampaignRules.Turn(State);
        AgoraCaseState WeekState => AgoraCaseCurrent ? AgoraState : new AgoraCaseState();
        bool AgoraTrialPending => WeekFlowRules.TrialPending(WeekState, CurrentAgoraCase);

        const int DemeSite = 4;

        /// <summary>Θέση στον χάρτη: 0 Εκκλησία, 1 Βουλή, 2 Άρειος Πάγος, 3 Ηλιαία, 4 Συνέλευση του δήμου.
        /// Πριν τη δίκη ανάβει το δικαστήριο, μετά ο χώρος της υπόθεσης.</summary>
        int AgoraActiveSite
        {
            get
            {
                var c = CurrentAgoraCase;
                if (AgoraTrialPending) return c.trial.venue == Agora.TrialVenue.Heliaia ? 3 : 2;
                return c.venue == Agora.CaseVenue.Council ? 1 : c.venue == Agora.CaseVenue.DemeAssembly ? DemeSite : 0;
            }
        }

        static string AgoraVenueArt(Agora.CaseData c) => c.venue == Agora.CaseVenue.Council ? "boule" : "assembly";

        static string W(string id, params object[] args) => args.Length == 0 ? JsonContent.Copy(id) : string.Format(JsonContent.Copy(id), args);

        static string VenueLabel(Agora.CaseVenue venue) => W(venue == Agora.CaseVenue.Council ? WeekText.VenueCouncil
            : venue == Agora.CaseVenue.DemeAssembly ? WeekText.VenueDeme : WeekText.VenuePnyx);

        static string TrialVenueLabel(Agora.TrialVenue venue) => W(venue == Agora.TrialVenue.Heliaia ? WeekText.VenueHeliaia : WeekText.VenueAreopagus);

        static string ProcedureLabel(Agora.ComplaintProcedure procedure) => procedure switch
        {
            Agora.ComplaintProcedure.Eisangelia => JsonContent.Copy("Copy_4b16fd38c5297a0e"),
            Agora.ComplaintProcedure.Endeixis => JsonContent.Copy("Copy_5c270e49d63a8b1f"),
            Agora.ComplaintProcedure.Phasis => JsonContent.Copy("Copy_6d381f5ae74b9c20"),
            Agora.ComplaintProcedure.GrapheSykophantias => JsonContent.Copy("Copy_7e492a6bf85cad31"),
            Agora.ComplaintProcedure.Apophasis => W(WeekText.ProcedureApophasis),
            _ => JsonContent.Copy("Copy_8f5a3b7c096dbe42")
        };

        static string TrialTitle(Agora.TrialData t) => W(WeekText.TrialTitle, ProcedureLabel(t.procedure), t.defendant != null ? t.defendant.FullName : "");

        string EvidenceWord(int evidence) => W(Agora.AssemblyRules.Evidence(evidence, AgoraCfg) switch
        {
            Agora.EvidenceStrength.Strong => WeekText.EvidenceStrong,
            Agora.EvidenceStrength.Doubtful => WeekText.EvidenceDoubtful,
            _ => WeekText.EvidenceWeak
        });

        // ---------- Χαρακτήρας (καλείται από FinishBio και από direct scene preview) ----------

        /// <summary>Ο πολίτης όπως διαμορφώνεται από τη βιογραφία: αρχικές τιμές GameConfig + δέλτα + τάξη.
        /// Ίδιος υπολογισμός στις οθόνες βιογραφίας, στη σύνοψη και στην είσοδο στην εκστρατεία.</summary>
        Agora.PlayerState CitizenPreview()
        {
            return CitizenPreviewRules.Preview(AgoraCfg, SocialClassOf(State.economicClassIndex), State.playerName, State.biographyDeltas);
        }

        /// <summary>Οι τάξεις της βιογραφίας είναι σε αντίστροφη σειρά από το Agora.SocialClass.</summary>
        static Agora.SocialClass SocialClassOf(int economicClassIndex) => (Agora.SocialClass)Mathf.Clamp(3 - economicClassIndex, 0, 3);

        void InitialiseAgoraCitizen()
        {
            var cfg = AgoraCfg;
            State.citizen = CitizenPreview();
            State.polis = new Agora.CityState(cfg.cityStart);   // η πόλη ξεκινά ΠΑΝΤΑ από το cityStart
            State.agoraCase = new AgoraCaseState();
            Agora.AssemblyRules.StartPrytany(State.polis, State.citizen, cfg, State.prytany);
        }

        void EnsureAgoraReady()
        {
            if (string.IsNullOrEmpty(State.citizen?.playerName))
                InitialiseAgoraCitizen();   // direct scene preview στον editor
        }

        // ---------- Κουμπιά (καλείται ΠΡΙΝ τα παλιά Wire ώστε να προηγούνται οι νέοι χειριστές) ----------

        void WireAgoraCaseButtons()
        {
            Bind("case-trial.primary", () => { if (AgoraCaseCurrent && AgoraState.trialDone) GoCampaign("map-stela"); else AgoraRunTrial(); });
            Bind("case-trial.judge.0", () => AgoraJudge(true));
            Bind("case-trial.judge.1", () => AgoraJudge(false));
            Bind("deliberation.speech", () => GoCampaign("speech"));
            for (int i = 1; i < 4; i++) { int n = i; Bind("speech.logo." + i, () => AgoraSpeak(n)); }
            Bind("vote.choice.0", () => AgoraVote(Agora.Side.A));
            Bind("vote.choice.1", () => AgoraVote(Agora.Side.B));
            Bind("vote-result.primary", () => GoCampaign("week-report"));
            Bind("week-report.primary", AgoraAdvanceWeek);
            Bind("prytany-transition.primary", AgoraAdvancePrytany);
            Bind("ending-city.primary", () => GoCampaign("ending-personal"));
            // Τα τέλη έρχονται μετά τις τελικές γνώσεις, στάσεις και συμμετοχή (ενότητα 7.7)· μετά: δημογραφικά/MEEGA+,
            // ή η ολοκλήρωση, αν έχουν ήδη δοθεί (όταν ο παίκτης ξαναβλέπει τα τέλη από την οθόνη ολοκλήρωσης).
            Bind("ending-personal.primary", () => GoCampaign(Evaluation.completed ? "complete" : AfterFinalQuestionnaires));
        }

        /// <summary>Αρχικοποίηση της υπόθεσης της εβδομάδας — καλείται ΜΕΣΑ σε MutateAndSave.</summary>
        void EnsureAgoraCaseStarted()
        {
            var c = CurrentAgoraCase;
            if (AgoraState.caseId == c.id) return;
            State.agoraCase = new AgoraCaseState { caseId = c.id, step = "city" };
        }

        void BeginAgoraCase()
        {
            EnsureAgoraReady();
            if (!AgoraCaseCurrent)
                if (!MutateAndSave(EnsureAgoraCaseStarted)) return;
            GoCampaign("deliberation");
            var venue = CurrentAgoraCase.venue;
            ShowPlaceCardIfNeeded(venue == Agora.CaseVenue.Council ? GuideBoule : venue == Agora.CaseVenue.DemeAssembly ? GuideDeme : GuidePnyx);
        }

        // Τα λήμματα του Οδηγού στο Ευρετήριο που διαβάζονται και ως κάρτες πρώτης επίσκεψης.
        const string GuideMarket = "guide_market", GuidePnyx = "guide_pnyx", GuideBoule = "guide_boule",
            GuideHeliaia = "guide_heliaia", GuideAreopagus = "guide_areopagus", GuideDeme = "guide_deme";

        // ---------- Δίκη ----------

        /// <summary>Πρώτη σελίδα της δίκης: κατηγορία και απολογία. Η δίκη δεν έχει γίνει ακόμη.</summary>
        void AgoraOpenTrial()
        {
            EnsureAgoraReady();
            if (!MutateAndSave(() => { EnsureAgoraCaseStarted(); State.phase = "case-trial"; })) return;
            Show("case-trial");
            ShowPlaceCardIfNeeded(CurrentAgoraCase.trial.venue == Agora.TrialVenue.Heliaia ? GuideHeliaia : GuideAreopagus);
        }

        /// <summary>Η ψήφος του παίκτη ως κληρωτού δικαστή: καταγράφεται για την έρευνα, δεν αλλάζει την ετυμηγορία.</summary>
        void AgoraJudge(bool guilty)
        {
            if (currentScreen != "case-trial") return;
            bool ok = false;
            if (!MutateAndSave(() => ok = WeekFlowRules.JudgeVote(AgoraState, CurrentAgoraCase, guilty)) || !ok) return;
            AgoraFirestoreTelemetry.Send("judge_vote", AgoraEvent(("guilty", guilty)));
            AgoraRunTrial();
        }

        /// <summary>Δεύτερη σελίδα: η δίκη γίνεται πάντα· η ανάλυση αποθηκεύεται αμέσως.</summary>
        void AgoraRunTrial()
        {
            EnsureAgoraReady();
            Agora.TrialBreakdown result = null;
            if (!MutateAndSave(() =>
            {
                EnsureAgoraCaseStarted();
                result = WeekFlowRules.RunTrial(AgoraState, CurrentAgoraCase, State.citizen, AgoraCfg, agoraRng);
                if (result != null) State.phase = "case-trial";
            }) || result == null) return;
            var trial = CurrentAgoraCase.trial;
            AgoraFirestoreTelemetry.Send("trial", AgoraEvent(("accuser", AgoraState.accuser), ("total", result.total), ("guilty", result.guilty),
                ("sycophant", result.sycophant), ("lines", ResearchLines(result)), ("mood", result.mood), ("venue", trial.venue),
                ("evidence", trial.evidence), ("logographer", AgoraState.logographerBought && AgoraState.logographerForTrial),
                ("judgeVote", AgoraState.judgeVote == 1 ? "guilty" : AgoraState.judgeVote == 2 ? "acquit" : "none")));
            Show("case-trial");
        }

        // ---------- Βήμα και ψήφος ----------

        /// <summary>Τα κουμπιά του βήματος: 1/2 = μιλώ υπέρ Α/Β, 3 = σιωπώ.</summary>
        void AgoraSpeak(int slot)
        {
            if (currentScreen != "speech" || AgoraState.spoke || AgoraState.resolved) return;
            Agora.Side side = slot == 1 ? Agora.Side.A : slot == 2 ? Agora.Side.B : Agora.Side.None;
            bool ok = false;
            if (!MutateAndSave(() =>
            {
                ok = WeekFlowRules.Speak(AgoraState, side);
                if (!ok) return;
                AgoraState.step = "vote";
                State.phase = "vote";
            }) || !ok) return;
            Show("vote");
        }

        /// <summary>Η ψηφοφορία· η ψήφος του παίκτη καταγράφεται αλλά δεν είναι όρος του κανόνα.</summary>
        void AgoraVote(Agora.Side side)
        {
            if (currentScreen != "vote" || AgoraState.resolved) return;
            var c = CurrentAgoraCase;
            Agora.VoteBreakdown result = null;
            if (!MutateAndSave(() =>
            {
                result = WeekFlowRules.ResolveVote(AgoraState, c, State.polis, State.citizen, AgoraCfg, agoraRng, side);
                if (result == null) return;
                AgoraState.step = "result";
                // Ελάχιστο CaseRecord ώστε αξιολόγηση (FullCampaignComplete) και έλεγχοι saves να βλέπουν την πρόοδο.
                var record = CampaignRules.Current(State);
                record.caseId = c.id.ToString();
                record.vote = side == Agora.Side.A ? 0 : 1;
                record.majority = result.passedA ? 0 : 1;
                record.completed = true;
                State.phase = "vote-result";
            }) || result == null) return;
            AgoraFirestoreTelemetry.Send("case_result", AgoraEvent(("total", result.total), ("passedA", result.passedA),
                ("vote", side), ("spokeFor", (Agora.Side)AgoraState.spokeFor), ("lines", ResearchLines(result)),
                ("mood", result.mood), ("pressureMetric", (Agora.Metric)AgoraState.pressureMetric), ("pressureValue", result.pressureValue),
                ("logographer", WeekFlowRules.LogographerSide(AgoraState)), ("logographerBought", AgoraState.logographerBought),
                ("officialFavors", (Agora.Side)AgoraState.officialFavors), ("officialBribed", AgoraState.officialBribed),
                ("trialFavors", WeekFlowRules.TrialFavors(AgoraState, c))));
            Show("vote-result");
        }

        // ---------- Προώθηση εβδομάδας / πρυτανείας / τελών ----------

        void AgoraAdvanceWeek()
        {
            if (currentScreen != "week-report") return;
            string next = null;
            if (!MutateAndSave(() =>
            {
                State.agoraCase = new AgoraCaseState();
                if (State.week < CampaignRules.CasesPerPrytany) { State.week++; next = "week-transition"; }
                else next = "prytany-transition";
                State.phase = next;
            })) return;
            Show(next);
        }

        /// <summary>Η αναφορά της πόλης θέλει δεύτερη σελίδα πριν από την πρυτανεία που ξεκινά ο πόλεμος.</summary>
        bool WarStartsNext => State.prytany + 1 == AgoraCfg.warStartsAtPrytany;

        void AgoraAdvancePrytany()
        {
            if (currentScreen != "prytany-transition") return;
            if (WarStartsNext && !State.warNoticeSeen)
            {
                if (MutateAndSave(() => State.warNoticeSeen = true)) RenderCampaign();
                return;
            }
            string next = null;
            bool finished = false;
            var cityEnding = default(Agora.CityEnding);
            var personalEnding = default(Agora.PersonalEnding);
            // Οι 9 δείκτες στο τέλος της πρυτανείας, πριν από τη μεταβολή της επόμενης (ενότητα 8, βήμα 9).
            var prytanyEnd = AgoraEvent(
                ("peitho", State.citizen.peitho), ("fame", State.citizen.fame), ("ethos", State.citizen.ethos), ("wallet", State.citizen.wallet),
                ("T", State.polis.treasury), ("Ep", State.polis.food), ("Et", State.polis.readiness),
                ("Em", State.polis.trust), ("ID", State.polis.demosPower),
                ("war", State.polis.war), ("garrison", State.polis.garrison), ("payAbolished", State.polis.payAbolished));
            if (!MutateAndSave(() =>
            {
                if (State.prytany >= JsonContent.Settings.prytanyCount)
                {
                    // Τα τέλη κρίνονται τώρα αλλά δείχνονται μετά τα τελικά ερωτηματολόγια, για να μην τα επηρεάζουν.
                    finished = true;
                    cityEnding = Agora.EndingRules.City(State.polis, AgoraCfg);
                    personalEnding = Agora.EndingRules.Personal(State.citizen, cityEnding, AgoraCfg);
                    Campaign.cityEnding = cityEnding.ToString();
                    Campaign.personalEnding = personalEnding.ToString();
                    next = "final-knowledge";
                }
                else
                {
                    State.prytany++;
                    State.week = 1;
                    State.currentTribe = -1;
                    Agora.AssemblyRules.StartPrytany(State.polis, State.citizen, AgoraCfg, State.prytany);
                    next = "prytany-title";
                }
                State.phase = next;
            })) return;
            AgoraFirestoreTelemetry.Send("prytany_end", prytanyEnd);
            if (finished)
                AgoraFirestoreTelemetry.Send("game_end", AgoraEvent(
                    ("cityEnding", cityEnding), ("personalEnding", personalEnding), ("knowledgeFormOrder", KnowledgeFormOrder),
                    ("spokeForA", State.citizen.spokeForA), ("spokeForB", State.citizen.spokeForB), ("bribes", State.citizen.bribes),
                    ("peitho", State.citizen.peitho), ("fame", State.citizen.fame), ("ethos", State.citizen.ethos), ("wallet", State.citizen.wallet),
                    ("excluded", Agora.EndingRules.ExcludedByPropertyLimit(State.citizen, cityEnding)),
                    ("T", State.polis.treasury), ("Ep", State.polis.food), ("Et", State.polis.readiness),
                    ("Em", State.polis.trust), ("ID", State.polis.demosPower)));
            Show(next);
        }

        // ---------- Rendering ----------

        bool TryRenderAgoraCase()
        {
            EnsureAgoraReady();
            string id = currentScreen;
            var c = CurrentAgoraCase;
            if (id == "case-trial") { RenderAgoraTrial(c); return true; }
            if (id == "deliberation")
            {
                CampaignImage(AgoraVenueArt(c));
                AgoraFunds();
                CampaignText("heading", c.title);
                bool convicted = WeekFlowRules.Convicted(AgoraState, c);
                var speakerA = Agora.AssemblyRules.SpeakerOf(c, Agora.Side.A, convicted);
                var speakerB = Agora.AssemblyRules.SpeakerOf(c, Agora.Side.B, convicted);
                // Οι κάρτες έχουν δικά τους παιδιά (Speaker/Position/Institutional leaning) — αλλιώς μένουν τα αρχειοθετημένα ονόματα.
                SetSiblingText("deliberation.argument.0", "Speaker", speakerA.FullName);
                SetSiblingText("deliberation.argument.1", "Speaker", speakerB.FullName);
                SetSiblingText("deliberation.argument.0", "Position", speakerA.office != null ? speakerA.office.displayName : "");
                SetSiblingText("deliberation.argument.1", "Position", speakerB.office != null ? speakerB.office.displayName : "");
                SetSiblingText("deliberation.argument.0", "Institutional leaning", W(CityText.ForA));
                SetSiblingText("deliberation.argument.1", "Institutional leaning", W(CityText.ForB));
                foreach (string key in new[] { "deliberation.argument.0", "deliberation.argument.1" })
                {
                    var argument = Get<TMPro.TMP_Text>(key);
                    if (argument != null) { argument.enableAutoSizing = true; argument.fontSizeMin = 14; argument.fontSizeMax = 24; }
                }
                SetText("deliberation.argument.0", c.speechA.text);
                SetText("deliberation.argument.1", c.speechB.text);
                CampaignText("source", VenueLabel(c.venue));
                CampaignLabel("speech", W(CityText.ToPlatform));
                return true;
            }
            if (id == "speech") { RenderAgoraSpeech(); return true; }
            if (id == "vote")
            {
                CampaignImage(AgoraVenueArt(c));
                AgoraFunds();
                CampaignText("heading", W(CityText.VoteHeading));
                CampaignText("body", c.title);
                CampaignLabel("choice.0", W(CityText.ForA));
                CampaignLabel("choice.1", W(CityText.ForB));
                // Το πλαϊνό πλαίσιο συνοψίζει τι έκανες στο βήμα — ποτέ άδειο σκούρο κουτί.
                CampaignText("role", AgoraState.spoke
                    ? W(CityText.YouSpokeFor, SideLabel((Agora.Side)AgoraState.spokeFor))
                    : W(CityText.YouWereSilent));
                CampaignText("source", c.athPolRef);
                return true;
            }
            if (id == "vote-result") { RenderAgoraResult(c); return true; }
            if (id == "week-report") { RenderAgoraWeekReport(c); return true; }
            if ((id == "ending-city" || id == "ending-personal" && !string.IsNullOrEmpty(Campaign.personalEnding))
                && !string.IsNullOrEmpty(Campaign.cityEnding) && System.Enum.TryParse(Campaign.cityEnding, out Agora.CityEnding _))
            {
                RenderAgoraEnding(id == "ending-personal");
                return true;
            }
            return false;
        }

        void AgoraFunds() => CampaignText("funds", W(CityText.Wallet, State.citizen.wallet));

        bool mapHintsWired;

        /// <summary>Tooltips στα εικονίδια της πόλης: ένα κοινό πλαίσιο της σκηνής, χειριστές hover στα υπάρχοντα κουμπιά.</summary>
        void WireMapIconHints()
        {
            var panel = Get<PanelGraphic>("map.hint");
            var label = Get<TMPro.TMP_Text>("map.hint.label");
            if (mapHintsWired || panel == null || label == null) return;
            void Attach(Component target, string textId)
            {
                if (target == null) return;
                var hint = target.GetComponent<MapIconHint>();
                if (hint == null) hint = target.gameObject.AddComponent<MapIconHint>();
                hint.label = JsonContent.Copy(textId); hint.panel = panel.rectTransform; hint.display = label;
            }
            Attach(lexicon != null ? lexicon.open : null, HudText.IconLexicon);
            Attach(Get<UnityEngine.UI.Button>("map.market"), HudText.IconMarket);
            Attach(Get<UnityEngine.UI.Button>("map.speech"), HudText.IconLogographers);
            Attach(Get<UnityEngine.UI.Button>("map.complaint"), HudText.IconComplaints);
            Attach(Get<UnityEngine.UI.Button>("map.offices"), HudText.IconOfficials);
            mapHintsWired = true;
        }

        void RenderAgoraMap()
        {
            EnsureAgoraReady();
            WireMapIconHints();
            bool done = AgoraCaseResolved;
            bool informed = MarketVisited;
            int site = AgoraActiveSite;
            // Η Συνέλευση του δήμου υπάρχει στον χάρτη μόνο την εβδομάδα που συνεδριάζει (υπόθεση 1).
            SetActive("map.institution." + DemeSite, CurrentAgoraCase.venue == Agora.CaseVenue.DemeAssembly);
            for (int i = 0; i <= DemeSite; i++)
            {
                bool active = site == i && !done;
                bool locked = active && !informed;   // κλειδωμένο μέχρι την επίσκεψη στην Αγορά
                string key = "map.institution." + i;
                var plate = Get<PanelGraphic>(key + ".plate");
                var title = Get<TMPro.TMP_Text>(key + ".title");
                if (plate != null)
                {
                    plate.color = locked ? new Color32(136, 130, 114, 255) : active ? new Color32(232, 223, 203, 255) : new Color32(25, 47, 52, 255);
                    plate.rectTransform.sizeDelta = new Vector2(plate.rectTransform.sizeDelta.x, active ? 72 : 50.4f);
                }
                if (title != null)
                {
                    title.text = InstitutionNames[i];
                    title.enableAutoSizing = true; title.fontSizeMin = 15; title.fontSizeMax = 22;
                    title.color = active && !locked ? new Color32(23, 43, 48, 255) : new Color32(238, 228, 203, 255);
                    title.rectTransform.anchoredPosition = new Vector2(0, active ? -11 : -9);
                }
                SetActive(key + ".newCase", active && !locked);
            }
            // Ενδείξεις εκκρεμότητας: Αγορά όσο δεν έχεις περάσει· Καταγγελίες όσο η δίκη της εβδομάδας περιμένει απόφασή σου.
            bool trialOpen = !done && WeekFlowRules.CanAccuse(WeekState, CurrentAgoraCase, State.citizen);
            SetActive("map.market.badge", !informed && !done);
            SetActive("map.market.count", !informed && !done);
            SetText("map.market.count", "!");
            SetActive("map.complaint.badge", trialOpen);
            SetActive("map.complaint.count", trialOpen);
            SetText("map.complaint.count", "1");
            ShowMapGuideIfNeeded();
        }

        void RenderAgoraTrial(Agora.CaseData c)
        {
            var s = AgoraState;
            var t = c.trial;
            if (!WeekFlowRules.HasTrial(c) || !AgoraCaseCurrent) { GoCampaign("map-stela"); return; }
            CampaignImage("courts");
            AgoraFunds();
            CampaignText("heading", TrialTitle(t));
            string intro = t.venue == Agora.TrialVenue.Heliaia ? AgoraContent.heliaiaIntro : AgoraContent.areopagusIntro;
            CampaignText("body", intro + "\n\n" + W(WeekText.TrialCharge, t.charge) + "\n\n" + W(WeekText.TrialDefence, t.defence));
            CampaignText("source", TrialVenueLabel(t.venue));
            bool done = s.trialDone && s.trialResult != null;
            // Πρώτη σελίδα: ο ρόλος σου· στην Ηλιαία, αν δεν κατηγορείς, ψηφίζεις ως δικαστής.
            bool judge = !done && WeekFlowRules.CanJudge(s, c);
            SetActive("case-trial.judge.0", judge);
            SetActive("case-trial.judge.1", judge);
            SetActive("case-trial.primary", !judge);
            if (!done)
            {
                CampaignText("role", W(t.venue == Agora.TrialVenue.Areopagus ? CityText.TrialWatchRole
                    : s.accuser ? CityText.TrialAccuserRole : CityText.TrialJudgeRole));
                CampaignLabel("judge.0", W(CityText.JudgeGuilty));
                CampaignLabel("judge.1", W(CityText.JudgeAcquit));
                CampaignLabel("primary", W(CityText.HearVerdict));
                return;
            }
            // Δεύτερη σελίδα: ετυμηγορία και ανάλυση.
            var lines = new List<string> { s.trialGuilty ? t.verdictGuilty : t.verdictAcquitted, TrialBreakdownText(s.trialResult) };
            if (s.judgeVote != 0) lines.Add(W(CityText.YourJudgeVote, W(s.judgeVote == 1 ? CityText.JudgeGuilty : CityText.JudgeAcquit)));
            if (s.accuser)
            {
                lines.Add(s.trialGuilty ? W(WeekText.TrialAccuserWon, AgoraCfg.fameConviction, AgoraCfg.ethosConviction)
                                        : W(WeekText.TrialAccuserLost, Signed(AgoraCfg.fameAcquittal)));
                if (s.trialResult.sycophant) lines.Add(W(WeekText.TrialFine, AgoraCfg.accuserFine));
            }
            lines.Add(W(WeekText.TrialEffect, AgoraCfg.trialPoints, SideLabel(WeekFlowRules.TrialFavors(s, c))));
            CampaignText("role", string.Join("\n\n", lines));
            CampaignLabel("primary", W(WeekText.TrialPrimary));
        }

        /// <summary>«Στοιχεία 58 · Η κατηγορία σου +6 · Διάθεση του πλήθους −3 = 61. Χρειαζόταν 50. Ένοχος.»</summary>
        string TrialBreakdownText(Agora.TrialBreakdown r)
        {
            var parts = r.lines.Select(l => l.term switch
            {
                Agora.TrialTerm.Evidence => W(WeekText.TermEvidence) + " " + l.value,
                Agora.TrialTerm.Accusation => W(WeekText.TermAccusation) + " " + Signed(l.value),
                _ => W(WeekText.TermMood) + " " + Signed(l.value)
            });
            return string.Join(" · ", parts) + " = " + r.total + ". " + W(WeekText.Needed, AgoraCfg.guiltyThreshold) + " "
                + W(r.guilty ? WeekText.Guilty : WeekText.Acquitted);
        }

        /// <summary>«Βάση 54 · Πίεση (Εμπιστοσύνη 31) +5 · Ο λόγος σου +6 · … = 66. Χρειαζόταν 50. Πέρασε η πρόταση Α.»</summary>
        string VoteBreakdownText(Agora.CaseData c, Agora.VoteBreakdown r)
        {
            string pressureMetric = MetricLabel((Agora.Metric)AgoraState.pressureMetric);
            var parts = r.lines.Select(l => l.term switch
            {
                Agora.VoteTerm.Base => W(WeekText.TermBase) + " " + l.value,
                Agora.VoteTerm.Pressure => W(WeekText.TermPressure, pressureMetric, r.pressureValue) + " " + Signed(l.value),
                Agora.VoteTerm.War => W(WeekText.TermWar) + " " + Signed(l.value),
                Agora.VoteTerm.Areopagus => W(WeekText.TermAreopagus) + " " + Signed(l.value),
                Agora.VoteTerm.Trial => W(WeekText.TermTrial) + " " + Signed(l.value),
                Agora.VoteTerm.Official => W(WeekText.TermOfficial, c.undecided != null ? c.undecided.displayName : "") + " " + Signed(l.value),
                Agora.VoteTerm.Speech => W(WeekText.TermSpeech) + " " + Signed(l.value),
                _ => W(WeekText.TermMood) + " " + Signed(l.value)
            });
            return string.Join(" · ", parts) + " = " + r.total + ". " + W(WeekText.Needed, AgoraCfg.passThreshold) + " "
                + W(r.passedA ? WeekText.PassedA : WeekText.PassedB);
        }

        void RenderAgoraSpeech()
        {
            var c = CurrentAgoraCase;
            CampaignImage(AgoraVenueArt(c));
            AgoraFunds();
            CampaignText("heading", W(CityText.SpeechHeading));
            CampaignText("body", W(CityText.SpeechBody));
            var logographer = WeekFlowRules.LogographerSide(AgoraState);
            CampaignText("role", logographer == Agora.Side.None ? ""
                : W(WeekText.LogoBought, DeinarchosName, W(logographer == Agora.Side.A ? WeekText.LogoForA : WeekText.LogoForB)));
            SetActive("speech.logo.0", false);
            SetActive("speech.logo.1", true); SetActive("speech.logo.2", true); SetActive("speech.logo.3", true);
            CampaignLabel("logo.1", W(CityText.SpeakA));
            CampaignLabel("logo.2", W(CityText.SpeakB));
            CampaignLabel("logo.3", W(CityText.Silence));
            // Τα κουμπιά μπαίνουν μέσα στο πλάτος της περγαμηνής (οι αυθεντικές θέσεις έβγαιναν έξω δεξιά).
            for (int i = 1, slot = 0; i < 4; i++, slot++)
            {
                var button = Get<UnityEngine.UI.Button>("speech.logo." + i);
                if (button == null) continue;
                SetInteractable("speech.logo." + i, !AgoraState.spoke);
                var rect = (RectTransform)button.transform;
                rect.anchoredPosition = new Vector2(312f + slot * 470f, -860f);
                rect.sizeDelta = new Vector2(400f, 88f);
            }
            CampaignText("source", "");
        }

        string DeinarchosName => AgoraContent.logographers.Count > 0 && AgoraContent.logographers[0] != null ? AgoraContent.logographers[0].displayName : "";

        void RenderAgoraResult(Agora.CaseData c)
        {
            var s = AgoraState;
            if (!AgoraCaseCurrent || !s.resolved || s.voteResult == null) { GoCampaign("map-stela"); return; }
            CampaignImage(AgoraVenueArt(c));
            AgoraFunds();
            string won = SideLabel(s.passedA ? Agora.Side.A : Agora.Side.B);
            CampaignText("heading", W(CityText.ResultHeading, won));
            CampaignText("body", VoteBreakdownText(c, s.voteResult));
            CampaignText("majority.0", W(s.passedA ? CityText.ProposalWon : CityText.Proposal, SideLabel(Agora.Side.A)));
            CampaignText("majority.1", W(!s.passedA ? CityText.ProposalWon : CityText.Proposal, SideLabel(Agora.Side.B)));
            var bar = Get<UnityEngine.UI.Image>("vote-result.majority.bar");
            if (bar != null)
                bar.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(100f, 900f, Mathf.Clamp01(s.voteResult.total / 100f)), bar.rectTransform.sizeDelta.y);
            // Τα τρία κουτιά (ΤΑΜΕΙΟ ΠΟΛΗΣ / ΠΡΟΣΩΠΙΚΟ ΤΑΜΕΙΟ / Η ΔΙΚΗ ΣΟΥ ΘΕΣΗ) γεμίζουν αντί να μένουν σκελετοί.
            CampaignText("city-effect", DescribeAgoraEffects(s.passedA ? c.effectsA : c.effectsB));
            CampaignText("personal-effect", W(CityText.Wallet, State.citizen.wallet));
            string speech = s.spoke ? W(CityText.SpokeAndVoted, SideLabel((Agora.Side)s.spokeFor), SideLabel((Agora.Side)s.vote))
                : W(CityText.SilentAndVoted, SideLabel((Agora.Side)s.vote));
            var logographer = WeekFlowRules.LogographerSide(s);
            if (logographer != Agora.Side.None)
                speech += "\n" + W(s.spoke && (Agora.Side)s.spokeFor == logographer ? WeekText.LogographerDelivered : WeekText.LogographerUnread, DeinarchosName);
            CampaignText("speech-effect", speech);
            CampaignText("source", c.ek3);
        }

        void RenderAgoraWeekReport(Agora.CaseData c)
        {
            AgoraFunds();
            CampaignText("heading", W(CityText.ReportHeading));
            CampaignText("body", W(CityText.ReportBody, c.title, SideLabel(AgoraState.passedA ? Agora.Side.A : Agora.Side.B),
                SideLabel((Agora.Side)AgoraState.vote), W(AgoraState.spoke ? CityText.ReportSpoke : CityText.ReportSilent), c.ek3));
            var p = State.polis;
            CampaignText("role", W(CityText.ReportCity, p.treasury, p.food, p.readiness, p.trust, p.demosPower)
                + "\n\n" + W(CityText.ReportPlayer, State.citizen.peitho, State.citizen.fame, State.citizen.ethos));
            CampaignText("source", "");
            CampaignLabel("primary", W(State.week == CampaignRules.CasesPerPrytany ? CityText.EndPrytany : CityText.NextWeek));
        }

        void RenderAgoraEnding(bool personal)
        {
            EnsureAgoraReady();
            var city = (Agora.CityEnding)System.Enum.Parse(typeof(Agora.CityEnding), Campaign.cityEnding);
            string key = personal ? Campaign.personalEnding : Campaign.cityEnding;
            CampaignText("heading", AgoraEndingTitle(key));
            CampaignImage("ending-" + (personal ? "home" : key == "Counterfactual" ? "victory" : "defeat"));
            // Πρώτα το «γιατί» με τους αριθμούς του παίκτη/της πόλης, μετά το κείμενο του τέλους (ενότητα 3).
            string body = (personal ? PersonalEndingReason((Agora.PersonalEnding)System.Enum.Parse(typeof(Agora.PersonalEnding), key))
                : CityEndingReason(city)) + "\n\n" + AgoraEndingBody(key, city);
            if (personal && Agora.EndingRules.ExcludedByPropertyLimit(State.citizen, city))
                body += "\n\n" + W(EndText.Excluded);
            if (!personal && !string.IsNullOrEmpty(AgoraContent.whatReallyHappened))
                body += "\n\n" + W(EndText.WhatReallyHappened, AgoraContent.whatReallyHappened);
            CampaignText("body", body);
            CampaignText("source", "");
            // Τα τέλη έρχονται πια μετά το τεστ γνώσεων: το κουμπί της σκηνής («Τελικός έλεγχος γνώσης») δεν ισχύει.
            if (personal) CampaignLabel("primary", W(WeekText.Continue));
        }

        /// <summary>Ο δείκτης που έκρινε το τέλος της πόλης, με τον αριθμό και το όριό του.</summary>
        string CityEndingReason(Agora.CityEnding ending)
        {
            var p = State.polis;
            var cfg = AgoraCfg;
            return ending switch
            {
                Agora.CityEnding.Famine => p.food < cfg.famineFood ? W(EndText.WhyFamine, p.food, cfg.famineFood)
                    : W(EndText.WhyFamineWar, p.readiness, cfg.famineReadiness),
                Agora.CityEnding.PropertyLimit => W(EndText.WhyPropertyLimit, p.demosPower, cfg.coupDemosPower),
                Agora.CityEnding.Stasis => W(EndText.WhyStasis, p.trust, cfg.stasisTrust, p.demosPower, cfg.demosPowerMajority),
                Agora.CityEnding.Counterfactual => W(EndText.WhyCounterfactual, p.demosPower, cfg.demosPowerMajority, cfg.enduresMinAll),
                _ => p.garrison ? W(EndText.WhyGarrison) : W(EndText.WhyGarrisonOther, p.demosPower, p.MinAll())
            };
        }

        /// <summary>«Φήμη 62, Ηθική 44: η πόλη σε ξέρει, αλλά δεν σε εμπιστεύεται.»</summary>
        string PersonalEndingReason(Agora.PersonalEnding ending)
        {
            var c = State.citizen;
            return ending switch
            {
                Agora.PersonalEnding.Hypereides => W(EndText.WhyHypereides, c.spokeForA, c.spokeForB),
                Agora.PersonalEnding.Demades => W(EndText.WhyDemades, c.spokeForB, c.spokeForA),
                Agora.PersonalEnding.HarpalusMan => W(EndText.WhyHarpalusMan, c.bribes),
                Agora.PersonalEnding.Lycurgus => W(EndText.WhyLycurgus, c.fame, c.ethos),
                Agora.PersonalEnding.Stratocles => W(EndText.WhyStratocles, c.fame, c.ethos),
                _ => W(EndText.WhyAnonymous, c.fame, c.ethos)
            };
        }

        static string SideLabel(Agora.Side side) => side == Agora.Side.A ? "Α" : side == Agora.Side.B ? "Β" : "—";

        /// <summary>Οι επιπτώσεις μιας απόφασης· η Ισχύς του Δήμου είναι πια ορατή.</summary>
        static string DescribeAgoraEffects(List<Agora.MetricDelta> effects) =>
            string.Join(" · ", effects.Select(e => MetricLabel(e.metric) + " " + e.amount.ToString("+0;-0;0")));

        static string MetricLabel(Agora.Metric metric) => metric switch
        {
            Agora.Metric.Treasury => "Ταμείο",
            Agora.Metric.Food => "Επισιτισμός",
            Agora.Metric.Readiness => "Ετοιμότητα",
            Agora.Metric.Trust => "Εμπιστοσύνη",
            _ => W(WeekText.MetricDemosPower)
        };

        static string AgoraEndingTitle(string key) => W(key switch
        {
            "Famine" => EndText.TitleFamine,
            "PropertyLimit" => EndText.TitlePropertyLimit,
            "Stasis" => EndText.TitleStasis,
            "Counterfactual" => EndText.TitleCounterfactual,
            "GarrisonOnly" => EndText.TitleGarrisonOnly,
            "Hypereides" => EndText.TitleHypereides,
            "Demades" => EndText.TitleDemades,
            "HarpalusMan" => EndText.TitleHarpalusMan,
            "Lycurgus" => EndText.TitleLycurgus,
            "Stratocles" => EndText.TitleStratocles,
            _ => EndText.TitleAnonymous
        });

        /// <summary>Το κείμενο του τέλους· Υπερείδης και Δημάδης έχουν δύο εκδοχές (όριο των δύο χιλιάδων / φρουρά).</summary>
        string AgoraEndingBody(string key, Agora.CityEnding city)
        {
            var content = AgoraContent;
            bool garrison = city == Agora.CityEnding.GarrisonOnly;
            string text = key switch
            {
                "Famine" => content.endingFamine,
                "PropertyLimit" => content.endingPropertyLimit,
                "Stasis" => content.endingStasis,
                "Counterfactual" => content.endingCounterfactual,
                "GarrisonOnly" => content.endingGarrisonOnly,
                "Hypereides" => garrison ? content.endingHypereidesGarrison : content.endingHypereides,
                "Demades" => garrison ? content.endingDemadesGarrison : content.endingDemades,
                "HarpalusMan" => content.endingHarpalusMan,
                "Lycurgus" => content.endingLycurgus,
                "Stratocles" => content.endingStratocles,
                _ => content.endingAnonymous
            };
            return string.IsNullOrWhiteSpace(text) ? W(EndText.Missing) : text;
        }

        Dictionary<string, object> AgoraEvent(params (string key, object value)[] data)
        {
            var result = new Dictionary<string, object>
            {
                { "runId", State.runId ?? "" }, { "case", AgoraState.caseId }, { "prytany", State.prytany }, { "week", State.week }
            };
            foreach (var pair in data) result[pair.key] = pair.value;
            return result;
        }

        /// <summary>Οι γραμμές της ανάλυσης για την έρευνα: «Base:54,Pressure:5,Speech:6,Mood:-3».</summary>
        static string ResearchLines(Agora.VoteBreakdown r) => string.Join(",", r.lines.Select(l => l.term + ":" + l.value));
        static string ResearchLines(Agora.TrialBreakdown r) => string.Join(",", r.lines.Select(l => l.term + ":" + l.value));

        /// <summary>Χωρίς αντιστάθμιση μορφών (απόφαση βήματος 4): πάντα μορφή Α πριν, μορφή Β μετά.</summary>
        const string KnowledgeFormOrder = "AB";
    }
}
