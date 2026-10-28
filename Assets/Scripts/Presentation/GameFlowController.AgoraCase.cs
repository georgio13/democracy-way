using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Ενιαίο σύστημα: η ροή υπόθεσης του Agora μέσα στον GameFlowController.
    /// Κανόνες ΜΟΝΟ από Agora.AssemblyRules/EndingRules· περιεχόμενο ΜΟΝΟ από το GameContent asset.
    /// Ροή χάρτη: η υπόθεση ξεκινά με την επίσκεψη στην Αγορά και μαζεύει επιλογές από τις οθόνες της
    /// πόλης (Λογογράφοι, Αξιωματούχοι, Καταγγελίες). Η συνέλευση τις καταναλώνει:
    /// [Δίκη (case-trial)] → Λόγοι (deliberation) → Βήμα (speech) → Ψήφος (vote) → Αποτέλεσμα.
    /// </summary>
    public sealed partial class GameFlowController
    {
        readonly System.Random agoraRng = new System.Random();

        Agora.GameContent AgoraContent => config.agoraContent;   // εγγυημένο από τον έλεγχο του Awake
        Agora.GameConfig AgoraCfg => config.agoraContent.config;
        Agora.CaseData CurrentAgoraCase => AgoraContent.CaseAt(State.prytany, State.week - 1);
        AgoraCaseState AgoraState => State.agoraCase;
        bool AgoraCaseResolved => AgoraState.caseId == CurrentAgoraCase.id && AgoraState.resolved;
        int AgoraSiteIndex => CurrentAgoraCase.inCouncil ? 1 : 0;   // θέση στον χάρτη: 0 Πνύκα, 1 Βουλευτήριο
        bool AgoraCaseCurrent => AgoraState.caseId == CurrentAgoraCase.id;
        Agora.LogographerData BoughtLogographer =>
            AgoraCaseCurrent && AgoraState.speechBought && AgoraState.logoIndex >= 1 && AgoraState.logoIndex < AgoraContent.logographers.Count
                ? AgoraContent.logographers[AgoraState.logoIndex] : null;

        // ---------- Χαρακτήρας (καλείται από FinishBio και από direct scene preview) ----------

        /// <summary>Ο πολίτης όπως διαμορφώνεται από τη βιογραφία: αρχικές τιμές GameConfig + δέλτα + τάξη.
        /// Ίδιος υπολογισμός στις οθόνες βιογραφίας, στη σύνοψη και στην είσοδο στην εκστρατεία.</summary>
        Agora.PlayerState CitizenPreview()
        {
            var cls = (Agora.SocialClass)Mathf.Clamp(3 - State.economicClassIndex, 0, 3);   // αντίστροφη σειρά τάξεων
            return CitizenPreviewRules.Preview(AgoraCfg, cls, State.playerName, State.biographyDeltas);
        }

        void InitialiseAgoraCitizen()
        {
            var cfg = AgoraCfg;
            State.citizen = CitizenPreview();
            State.polis = new Agora.CityState(cfg.cityStart);   // η πόλη ξεκινά ΠΑΝΤΑ από το cityStart (spec ΣΤ.1)
            State.agoraCase = new AgoraCaseState();
            Agora.AssemblyRules.StartPrytany(State.polis, State.citizen, cfg, State.prytany);
        }

        void EnsureAgoraReady()
        {
            if (string.IsNullOrEmpty(State.citizen?.playerName))
                InitialiseAgoraCitizen();   // direct scene preview στον editor
        }

        // ---------- Θέσεις αξιωματούχων: υλοποίηση πάνω στο save ----------

        List<Agora.OfficialStance> AgoraStances()
        {
            var list = Agora.AssemblyRules.CloneStances(CurrentAgoraCase);
            if (AgoraState.caseId == CurrentAgoraCase.id && AgoraState.stanceSides.Length == list.Count)
                for (int i = 0; i < list.Count; i++)
                {
                    list[i].side = (Agora.Side)AgoraState.stanceSides[i];
                    list[i].weight = AgoraState.stanceWeights[i];
                }
            return list;
        }

        void StoreAgoraStances(List<Agora.OfficialStance> stances)
        {
            AgoraState.stanceSides = stances.Select(s => (int)s.side).ToArray();
            AgoraState.stanceWeights = stances.Select(s => s.weight).ToArray();
        }

        // ---------- Κουμπιά (καλείται ΠΡΙΝ τα παλιά Wire ώστε να προηγούνται οι νέοι χειριστές) ----------

        void WireAgoraCaseButtons()
        {
            Bind("case-trial.primary", () => GoCampaign("deliberation"));
            Bind("deliberation.speech", () => GoCampaign("speech"));
            for (int i = 0; i < 4; i++) { int n = i; Bind("speech.logo." + i, () => AgoraSpeak(n)); }
            Bind("vote.choice.0", () => AgoraVote(Agora.Side.A));
            Bind("vote.choice.1", () => AgoraVote(Agora.Side.B));
            Bind("vote-result.primary", () => GoCampaign("week-report"));
            Bind("week-report.primary", AgoraAdvanceWeek);
            Bind("prytany-transition.primary", AgoraAdvancePrytany);
            Bind("ending-city.primary", () => GoCampaign("ending-personal"));
            Bind("ending-personal.primary", () => GoCampaign("complete"));
        }

        /// <summary>Αρχικοποίηση της υπόθεσης της εβδομάδας — καλείται ΜΕΣΑ σε MutateAndSave.</summary>
        void EnsureAgoraCaseStarted()
        {
            var c = CurrentAgoraCase;
            if (AgoraState.caseId == c.id) return;
            State.agoraCase = new AgoraCaseState { caseId = c.id, step = "city" };
            StoreAgoraStances(Agora.AssemblyRules.CloneStances(c));
        }

        /// <summary>Η συνεδρίαση: καταναλώνει ό,τι μάζεψε ο παίκτης στην πόλη. Πρώτα η δεσμευμένη
        /// καταγγελία (η δίκη γίνεται ΕΔΩ), μετά οι λόγοι.</summary>
        void BeginAgoraCase()
        {
            EnsureAgoraReady();
            if (!AgoraCaseCurrent)
                if (!MutateAndSave(EnsureAgoraCaseStarted)) return;
            if (AgoraState.complaintPending) { AgoraExecuteComplaint(); return; }
            GoCampaign("deliberation");
        }

        void AgoraExecuteComplaint()
        {
            var c = CurrentAgoraCase;
            var stances = AgoraStances();
            var result = default(Agora.ComplaintResult);
            if (!MutateAndSave(() =>
            {
                if (!WeekFlowRules.TakePendingComplaint(AgoraState)) return;
                result = Agora.AssemblyRules.Complain(c, stances, State.citizen, State.polis, AgoraCfg);
                if (!result.filed) { AgoraState.step = "deliberation"; State.phase = "deliberation"; return; }
                AgoraState.convicted = result.convicted;
                AgoraState.trialSycophant = result.sycophant;
                AgoraState.trialFine = result.fine;
                StoreAgoraStances(stances);
                var record = CampaignRules.Current(State);
                record.caseId = c.id.ToString();
                record.complaintFiled = true;
                record.complaintConvicted = result.convicted;
                AgoraState.step = "trial";
                State.phase = "case-trial";
            })) return;
            if (!result.filed) { Show("deliberation"); return; }
            AgoraFirestoreTelemetry.Send("complaint", AgoraEvent(
                ("target", c.complaint.target.displayName), ("convicted", result.convicted), ("sycophant", result.sycophant)));
            Show("case-trial");
        }

        // ---------- Βήμα και ψήφος ----------

        /// <summary>Τα τέσσερα κουμπιά του βήματος: 0 = εκφωνώ τον αγορασμένο λόγο,
        /// 1/2 = μιλώ μόνος υπέρ Α/Β (L0), 3 = σιωπώ. Ο λόγος πληρώθηκε στους Λογογράφους.</summary>
        void AgoraSpeak(int slot)
        {
            if (currentScreen != "speech" || AgoraState.spoke || AgoraState.resolved) return;
            bool delivered = slot == 0;
            Agora.Side side = slot == 0 ? (Agora.Side)AgoraState.speechSide
                            : slot == 1 ? Agora.Side.A
                            : slot == 2 ? Agora.Side.B
                            : Agora.Side.None;
            if (delivered && BoughtLogographer == null) return;
            bool ok = false;
            if (!MutateAndSave(() =>
            {
                ok = WeekFlowRules.Speak(AgoraState, side, delivered);
                if (!ok) return;
                AgoraState.step = "vote";
                State.phase = "vote";
            }) || !ok) return;
            Show("vote");
        }

        void AgoraVote(Agora.Side side)
        {
            if (currentScreen != "vote" || AgoraState.resolved) return;
            var c = CurrentAgoraCase;
            var stances = AgoraStances();
            int usedLogo = WeekFlowRules.SpeechLogoIndex(AgoraState);   // αγορασμένος αν εκφωνήθηκε, αλλιώς L0
            var logo = usedLogo >= 0 && usedLogo < AgoraContent.logographers.Count ? AgoraContent.logographers[usedLogo] : null;
            var spokeFor = AgoraState.spoke ? (Agora.Side)AgoraState.spokeFor : Agora.Side.None;
            var result = default(Agora.VoteResult);
            if (!MutateAndSave(() =>
            {
                float s0 = Agora.AssemblyRules.ComputeS0(c, State.polis, AgoraCfg, stances);
                float power = AgoraState.spoke ? Agora.AssemblyRules.SpeechPower(State.citizen, logo, AgoraCfg) : 0f;
                result = Agora.AssemblyRules.Resolve(s0, spokeFor, power, agoraRng, AgoraCfg);
                Agora.AssemblyRules.ApplyOutcome(c, result.passedA, State.polis, AgoraCfg);
                Agora.AssemblyRules.ApplyPlayerAfterCase(State.citizen, AgoraCfg, AgoraState.spoke, spokeFor, side, result);
                AgoraState.resolved = true;
                AgoraState.step = "result";
                AgoraState.vote = (int)side;
                AgoraState.s0 = result.s0; AgoraState.s = result.s;
                AgoraState.passedA = result.passedA; AgoraState.playerFlipped = result.playerFlipped; AgoraState.contested = result.contested;
                // Ελάχιστο CaseRecord ώστε αξιολόγηση (FullCampaignComplete) και έλεγχοι saves να βλέπουν την πρόοδο.
                var record = CampaignRules.Current(State);
                record.caseId = c.id.ToString();
                record.vote = side == Agora.Side.A ? 0 : 1;
                record.majority = result.passedA ? 0 : 1;
                record.completed = true;
                State.phase = "vote-result";
            })) return;

            AgoraFirestoreTelemetry.Send("case_result", AgoraEvent(
                ("s0", result.s0), ("s", result.s), ("passedA", result.passedA),
                ("speechBought", AgoraState.speechBought), ("speechSide", (Agora.Side)AgoraState.speechSide),
                ("delivered", AgoraState.speechDelivered), ("spoke", AgoraState.spoke), ("spokeFor", spokeFor),
                ("logo", logo != null ? logo.quality : -1), ("vote", side), ("prep", (Agora.PrepAction)AgoraState.prep),
                ("flipped", result.playerFlipped), ("contested", result.contested),
                ("T", State.polis.treasury), ("Ep", State.polis.food), ("Et", State.polis.readiness),
                ("Em", State.polis.trust), ("ID", State.polis.demosPower),
                ("wallet", State.citizen.wallet), ("peitho", State.citizen.peitho), ("ethos", State.citizen.ethos),
                ("synepeia", State.citizen.synepeia), ("parrhesia", State.citizen.parrhesia),
                ("anagn", State.citizen.anagnorisimotita), ("authority", State.citizen.Authority)));
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

        void AgoraAdvancePrytany()
        {
            if (currentScreen != "prytany-transition") return;
            string next = null;
            bool finished = false;
            var cityEnding = default(Agora.CityEnding);
            var personalEnding = default(Agora.PersonalEnding);
            if (!MutateAndSave(() =>
            {
                if (State.prytany >= JsonContent.Settings.prytanyCount)
                {
                    finished = true;
                    cityEnding = Agora.EndingRules.City(State.polis, AgoraCfg);
                    personalEnding = Agora.EndingRules.Personal(State.citizen, cityEnding, AgoraCfg);
                    Campaign.cityEnding = cityEnding.ToString();
                    Campaign.personalEnding = personalEnding.ToString();
                    next = "final-knowledge";   // το τέλος ΔΕΙΧΝΕΤΑΙ μετά τα ερωτηματολόγια (spec ΣΤ.2)
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
            if (finished)
                AgoraFirestoreTelemetry.Send("game_end", AgoraEvent(
                    ("cityEnding", cityEnding), ("personalEnding", personalEnding),
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
                CampaignImage(c.inCouncil ? "boule" : "assembly");
                AgoraFunds();
                CampaignText("heading", c.title);
                bool convicted = AgoraState.convicted;
                var speakerA = Agora.AssemblyRules.SpeakerOf(c.speechA, c, convicted);
                var speakerB = Agora.AssemblyRules.SpeakerOf(c.speechB, c, convicted);
                // Οι κάρτες έχουν δικά τους παιδιά (Speaker/Position/Institutional leaning) — αλλιώς μένουν τα αρχειοθετημένα ονόματα.
                SetSiblingText("deliberation.argument.0", "Speaker", speakerA.FullName);
                SetSiblingText("deliberation.argument.1", "Speaker", speakerB.FullName);
                SetSiblingText("deliberation.argument.0", "Position", speakerA.office != null ? speakerA.office.displayName : "");
                SetSiblingText("deliberation.argument.1", "Position", speakerB.office != null ? speakerB.office.displayName : "");
                SetSiblingText("deliberation.argument.0", "Institutional leaning", "Υπέρ της πρότασης Α");
                SetSiblingText("deliberation.argument.1", "Institutional leaning", "Υπέρ της πρότασης Β");
                foreach (string key in new[] { "deliberation.argument.0", "deliberation.argument.1" })
                {
                    var argument = Get<TMPro.TMP_Text>(key);
                    if (argument != null) { argument.enableAutoSizing = true; argument.fontSizeMin = 14; argument.fontSizeMax = 24; }
                }
                SetText("deliberation.argument.0", c.speechA.text);
                SetText("deliberation.argument.1", c.speechB.text);
                CampaignLabel("speech", "Προς το βήμα");
                return true;
            }
            if (id == "speech") { RenderAgoraSpeech(); return true; }
            if (id == "vote")
            {
                CampaignImage(c.inCouncil ? "boule" : "assembly");
                AgoraFunds();
                CampaignText("heading", "Ψηφοφορία");
                var pouch = BoughtLogographer;
                CampaignText("body", c.title + (AgoraState.spoke ? "\n\nΜίλησες. Η ψήφος σου κρίνει μόνο τη Συνέπειά σου." : ""));
                CampaignLabel("choice.0", "Υπέρ της πρότασης Α");
                CampaignLabel("choice.1", "Υπέρ της πρότασης Β");
                // Το πλαϊνό πλαίσιο συνοψίζει τι έκανες στο βήμα — ποτέ άδειο σκούρο κουτί.
                CampaignText("role", AgoraState.spoke
                    ? "Μίλησες υπέρ της " + SideLabel((Agora.Side)AgoraState.spokeFor) + "."
                        + (pouch != null && AgoraState.speechDelivered ? "\n\nΕκφώνησες τον λόγο του " + pouch.displayName + "."
                            : pouch != null ? "\n\nΟ λόγος του " + pouch.displayName + " έμεινε στο σακούλι σου." : "")
                    : "Δεν μίλησες. Η ψήφος σου είναι το μόνο αποτύπωμά σου σε αυτή την υπόθεση.");
                CampaignText("source", c.athPolRef);
                return true;
            }
            if (id == "vote-result") { RenderAgoraResult(c); return true; }
            if (id == "week-report") { RenderAgoraWeekReport(c); return true; }
            if ((id == "ending-city" || id == "ending-personal") && !string.IsNullOrEmpty(Campaign.cityEnding)
                && System.Enum.TryParse(Campaign.cityEnding, out Agora.CityEnding _))
            {
                RenderAgoraEnding(id == "ending-personal");
                return true;
            }
            return false;
        }

        void AgoraFunds() => CampaignText("funds", "Πορτοφόλι: " + State.citizen.wallet + " δρ.");

        /// <summary>Η καταγγελία της εβδομάδας είναι ακόμη στο χέρι του παίκτη (ούτε δεσμευμένη ούτε χαμένη ευκαιρία).</summary>
        bool AgoraComplaintAvailable(Agora.CaseData c) =>
            c.complaint.available && c.complaint.target != null && !State.citizen.sycophantFlag
            && !AgoraCaseResolved && !(AgoraCaseCurrent && AgoraState.prepDone);

        bool mapHintsWired;

        /// <summary>Tooltips στα εικονίδια της πόλης: ένα κοινό πλαίσιο, χειριστές hover στα υπάρχοντα κουμπιά.</summary>
        void WireMapIconHints()
        {
            if (mapHintsWired || lexicon == null || lexicon.tools == null) return;
            var font = Get<TMPro.TMP_Text>("map.city") != null ? Get<TMPro.TMP_Text>("map.city").font : null;
            var panel = CivicUIFactory.Panel(lexicon.tools, "Icon hint", new Color32(24, 46, 50, 240));
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(300f, 52f);
            var label = CivicUIFactory.Text(panel.transform, "Icon hint label", "", font, 26, new Color32(238, 228, 203, 255));
            CivicUIFactory.Fill(label.rectTransform, new Vector2(16, 8), new Vector2(16, 8));
            label.alignment = TMPro.TextAlignmentOptions.Right;
            panel.gameObject.SetActive(false);
            void Attach(Component target, string text)
            {
                if (target == null) return;
                var hint = target.GetComponent<MapIconHint>();
                if (hint == null) hint = target.gameObject.AddComponent<MapIconHint>();
                hint.label = text; hint.panel = rect; hint.display = label;
            }
            Attach(lexicon.open, "Ευρετήριο");
            Attach(Get<UnityEngine.UI.Button>("map.market"), "Αγορά");
            Attach(Get<UnityEngine.UI.Button>("map.speech"), "Λογογράφοι");
            Attach(Get<UnityEngine.UI.Button>("map.complaint"), "Καταγγελίες");
            Attach(Get<UnityEngine.UI.Button>("map.offices"), "Αξιωματούχοι");
            mapHintsWired = true;
        }

        void RenderAgoraMap()
        {
            EnsureAgoraReady();
            WireMapIconHints();
            var c = CurrentAgoraCase;
            bool done = AgoraCaseResolved;
            bool informed = State.marketVisitedTurn == CampaignRules.Turn(State);
            for (int i = 0; i < 4; i++)
            {
                bool active = AgoraSiteIndex == i && !done;
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
            // Τα chips δείχνουν ΜΟΝΟ μία σύντομη τιμή — οι τέσσερις δείκτες ζουν στο City indicators HUD.
            SetText("map.city", State.polis.treasury + " / 100");
            SetText("map.personal", State.citizen.wallet + " δρ.");
            // Ενδείξεις εκκρεμοτήτων: Αγορά όσο δεν έχεις περάσει, Καταγγελίες όσο υπάρχει διαθέσιμη.
            bool complaintOpen = !done && AgoraComplaintAvailable(c);
            SetActive("map.market.badge", !informed && !done);
            SetActive("map.market.count", !informed && !done);
            SetText("map.market.count", "!");
            SetActive("map.complaint.badge", complaintOpen);
            SetActive("map.complaint.count", complaintOpen);
            SetText("map.complaint.count", "1");
            ShowMapGuideIfNeeded();
        }

        void RenderAgoraTrial(Agora.CaseData c)
        {
            CampaignImage("courts");
            AgoraFunds();
            var opt = c.complaint;
            CampaignText("heading", ProcedureLabel(opt.procedure) + " κατά " + opt.target.FullName);
            CampaignText("body", opt.charge + "\n\nΑπολογία: " + opt.defence);
            CampaignText("role", opt.votesFor + "–" + opt.votesAgainst + ". " + opt.verdictText
                + (AgoraState.trialSycophant ? "\nΚάτω από το ένα πέμπτο: πρόστιμο " + AgoraState.trialFine + " δρ. Δεν μπορείς να καταγγείλεις ξανά." : ""));
            CampaignText("source", c.athPolRef);
        }

        void RenderAgoraSpeech()
        {
            var c = CurrentAgoraCase;
            CampaignImage(c.inCouncil ? "boule" : "assembly");
            AgoraFunds();
            var pouch = BoughtLogographer;
            bool delivered = pouch != null;
            CampaignText("heading", "Το βήμα είναι δικό σου");
            CampaignText("body", delivered
                ? "Στο σακούλι σου: ο λόγος του " + pouch.displayName + " υπέρ της " + SideLabel((Agora.Side)AgoraState.speechSide)
                    + ". Διάλεξε αν θα τον εκφωνήσεις, θα μιλήσεις μόνος ή θα σωπάσεις."
                : "Δεν παρήγγειλες λόγο. Μπορείς να μιλήσεις μόνος για όποια πλευρά θες — ή να μείνεις σιωπηλός.");
            // Το πλαϊνό πλαίσιο «Speech support» δείχνει την προεπισκόπηση του λόγου — ποτέ άδειο σκούρο κουτί.
            var support = Get<TMPro.TMP_Text>("speech.role");
            if (support != null) { support.enableAutoSizing = true; support.fontSizeMin = 14; support.fontSizeMax = 24; }
            CampaignText("role", delivered
                ? ComposedSpeech(c, (Agora.Side)AgoraState.speechSide, pouch)
                : "Χωρίς πάπυρο λογογράφου θα μιλήσεις με δικά σου λόγια — το επιχείρημα γυμνό, χωρίς προοίμιο και επίλογο.");
            SetActive("speech.logo.0", delivered);
            if (delivered)
                CampaignLabel("logo.0", "Εκφωνώ τον λόγο του " + pouch.displayName + " — υπέρ της " + SideLabel((Agora.Side)AgoraState.speechSide));
            SetActive("speech.logo.1", true); SetActive("speech.logo.2", true); SetActive("speech.logo.3", true);
            CampaignLabel("logo.1", "Μιλώ μόνος — υπέρ της Α");
            CampaignLabel("logo.2", "Μιλώ μόνος — υπέρ της Β");
            CampaignLabel("logo.3", "Σιωπώ");
            // Τα κουμπιά μπαίνουν μέσα στο πλάτος της περγαμηνής (οι αυθεντικές θέσεις έβγαιναν έξω δεξιά).
            int visible = delivered ? 4 : 3, slot = 0;
            float width = visible == 4 ? 328f : 400f, step = visible == 4 ? 352f : 470f, start = visible == 4 ? 276f : 312f;
            for (int i = 0; i < 4; i++)
            {
                var button = Get<UnityEngine.UI.Button>("speech.logo." + i);
                if (button == null) continue;
                SetInteractable("speech.logo." + i, !AgoraState.spoke);
                if (i == 0 && !delivered) continue;
                var rect = (RectTransform)button.transform;
                rect.anchoredPosition = new Vector2(start + slot * step, -860f);
                rect.sizeDelta = new Vector2(width, 88f);
                slot++;
            }
            CampaignText("source", "");
        }

        /// <summary>Ο λόγος του παίκτη: προοίμιο + επιχείρημα + επίλογος στο ύφος του λογογράφου.
        /// Μόνος (χωρίς λογογράφο) = μόνο το επιχείρημα.</summary>
        string ComposedSpeech(Agora.CaseData c, Agora.Side side, Agora.LogographerData logo)
        {
            string argument = side == Agora.Side.A ? c.argumentA : c.argumentB;
            if (logo == null || string.IsNullOrWhiteSpace(logo.openingTemplate))
                return argument ?? "";
            string sideName = SideLabel(side);
            return logo.openingTemplate.Replace("{side}", sideName)
                + "\n\n" + argument
                + "\n\n" + (logo.closingTemplate ?? "").Replace("{side}", sideName);
        }

        void RenderAgoraResult(Agora.CaseData c)
        {
            CampaignImage(c.inCouncil ? "boule" : "assembly");
            AgoraFunds();
            int percent = Mathf.RoundToInt(AgoraState.passedA ? AgoraState.s : 100 - AgoraState.s);
            CampaignText("heading", "Ἔδοξε τῷ δήμῳ: πρόταση " + (AgoraState.passedA ? "Α" : "Β") + " (" + percent + "%)");
            CampaignText("body", AgoraState.playerFlipped ? "Ο λόγος σου έκρινε την ψηφοφορία."
                : AgoraState.contested ? "Η υπόθεση ήταν αμφίρροπη ως το τέλος."
                : "Η απόφαση γράφτηκε στα ψηφίσματα της πόλης.");
            CampaignText("majority.0", "Πρόταση Α" + (AgoraState.passedA ? " — επικράτησε" : ""));
            CampaignText("majority.1", "Πρόταση Β" + (!AgoraState.passedA ? " — επικράτησε" : ""));
            var bar = Get<UnityEngine.UI.Image>("vote-result.majority.bar");
            if (bar != null)
                bar.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(100f, 900f, Mathf.Clamp01(percent / 100f)), bar.rectTransform.sizeDelta.y);
            // Τα τρία κουτιά (ΤΑΜΕΙΟ ΠΟΛΗΣ / ΠΡΟΣΩΠΙΚΟ ΤΑΜΕΙΟ / Η ΔΙΚΗ ΣΟΥ ΘΕΣΗ) γεμίζουν αντί να μένουν σκελετοί.
            CampaignText("city-effect", DescribeAgoraEffects(AgoraState.passedA ? c.effectsA : c.effectsB));
            CampaignText("personal-effect", "Πορτοφόλι " + State.citizen.wallet + " δρ.");
            CampaignText("speech-effect", (AgoraState.spoke ? "Μίλησες υπέρ της " + SideLabel((Agora.Side)AgoraState.spokeFor) : "Δεν μίλησες")
                + " · ψήφισες " + SideLabel((Agora.Side)AgoraState.vote));
            CampaignText("source", c.ek3);
        }

        void RenderAgoraWeekReport(Agora.CaseData c)
        {
            AgoraFunds();
            CampaignText("heading", "Απολογισμός");
            string vote = AgoraState.vote == (int)Agora.Side.A ? "Α" : "Β";
            CampaignText("body", c.title + "\n\nΕπικράτησε η πρόταση " + (AgoraState.passedA ? "Α" : "Β") + ". Ψήφισες " + vote + "."
                + (AgoraState.spoke ? " Μίλησες δημόσια." : " Δεν ζήτησες τον λόγο.")
                + (AgoraState.spoke && AgoraState.vote != AgoraState.spokeFor
                    ? "\nΜίλησες υπέρ της " + SideLabel((Agora.Side)AgoraState.spokeFor) + " και ψήφισες " + vote + " — η ασυνέπεια σημειώθηκε στη Συνέπειά σου." : "")
                + "\n\n" + c.ek3);
            CampaignText("role", "Ταμείο " + State.polis.treasury + " · Επισιτισμός " + State.polis.food
                + " · Ετοιμότητα " + State.polis.readiness + " · Εμπιστοσύνη " + State.polis.trust
                + "\n\nΠειθώ " + State.citizen.peitho + " · Ηθική " + State.citizen.ethos
                + " · Συνέπεια " + State.citizen.synepeia + "\nΠαρρησία " + State.citizen.parrhesia
                + " · Αναγνωρισιμότητα " + State.citizen.anagnorisimotita + " · Αυθεντία " + State.citizen.Authority);
            CampaignText("source", "");
            CampaignLabel("primary", State.week == CampaignRules.CasesPerPrytany ? "Τέλος πρυτανείας" : "Επόμενη εβδομάδα");
        }

        void RenderAgoraEnding(bool personal)
        {
            string key = personal ? Campaign.personalEnding : Campaign.cityEnding;
            CampaignText("heading", AgoraEndingTitle(key));
            CampaignImage("ending-" + (personal ? "home" : key == "Counterfactual" ? "victory" : "defeat"));
            string body = AgoraEndingBody(key);
            if (personal && Agora.EndingRules.ExcludedByPropertyLimit(State.citizen,
                    (Agora.CityEnding)System.Enum.Parse(typeof(Agora.CityEnding), Campaign.cityEnding)))
                body += "\n\nΩς θήτης, είσαι ανάμεσα στους αποκλεισμένους από το νέο πολίτευμα.";
            if (!personal && !string.IsNullOrEmpty(AgoraContent.whatReallyHappened))
                body += "\n\nΤι έγινε πραγματικά: " + AgoraContent.whatReallyHappened;
            CampaignText("body", body);
            CampaignText("source", "");
        }

        static string SideLabel(Agora.Side side) => side == Agora.Side.A ? "Α" : side == Agora.Side.B ? "Β" : "—";

        string DescribeAgoraEffects(List<Agora.MetricDelta> effects) =>
            string.Join(" · ", effects.Where(e => e.metric != Agora.Metric.DemosPower)   // το ΙΔ δεν εμφανίζεται ποτέ (ΣΤ.6)
                .Select(e => MetricLabel(e.metric) + " " + e.amount.ToString("+0;-0;0")));

        static string MetricLabel(Agora.Metric metric) => metric switch
        {
            Agora.Metric.Treasury => "Ταμείο",
            Agora.Metric.Food => "Επισιτισμός",
            Agora.Metric.Readiness => "Ετοιμότητα",
            Agora.Metric.Trust => "Εμπιστοσύνη",
            _ => ""
        };

        string AgoraEndingTitle(string key) => key switch
        {
            "Famine" => "Λιμός",
            "PropertyLimit" => "Το τίμημα της περιουσίας",
            "Stasis" => "Στάσις",
            "Counterfactual" => "Η πόλη που άντεξε",
            "GarrisonOnly" => "Φρουρά στη Μουνιχία",
            "Hypereides" => "Ο δρόμος του Υπερείδη",
            "Demades" => "Ο δρόμος του Δημάδη",
            "HarpalusMan" => "Ο άνθρωπος του Αρπάλου",
            "Lycurgus" => "Ο δρόμος του Λυκούργου",
            "Stratocles" => "Ο δρόμος του Στρατοκλή",
            _ => "Ένας από τους πολλούς"
        };

        string AgoraEndingBody(string key)
        {
            var content = AgoraContent;
            string text = key switch
            {
                "Famine" => content.endingFamine,
                "PropertyLimit" => content.endingPropertyLimit,
                "Stasis" => content.endingStasis,
                "Counterfactual" => content.endingCounterfactual,
                "GarrisonOnly" => content.endingGarrisonOnly,
                "Hypereides" => content.endingHypereides,
                "Demades" => content.endingDemades,
                "HarpalusMan" => content.endingHarpalusMan,
                "Lycurgus" => content.endingLycurgus,
                "Stratocles" => content.endingStratocles,
                _ => content.endingAnonymous
            };
            return string.IsNullOrWhiteSpace(text) ? "(Το κείμενο αυτού του τέλους δεν έχει γραφτεί ακόμη στο GameContent.)" : text;
        }

        Dictionary<string, object> AgoraEvent(params (string key, object value)[] data)
        {
            var result = new Dictionary<string, object> { { "case", AgoraState.caseId }, { "prytany", State.prytany } };
            foreach (var pair in data) result[pair.key] = pair.value;
            return result;
        }
    }
}
