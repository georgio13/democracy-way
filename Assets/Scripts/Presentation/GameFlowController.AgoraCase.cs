using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Ενιαίο σύστημα: η ροή υπόθεσης του Agora μέσα στον GameFlowController.
    /// Κανόνες ΜΟΝΟ από Agora.AssemblyRules/EndingRules· περιεχόμενο ΜΟΝΟ από το GameContent asset.
    /// Ροή χάρτη: η υπόθεση ξεκινά με την επίσκεψη στην Αγορά → Λόγοι (deliberation) → Βήμα (speech)
    /// → Ψήφος (vote) → Αποτέλεσμα. Η ψηφοφορία, οι κινήσεις και η δίκη της εβδομάδας
    /// συνδέονται με τους νέους κανόνες στο βήμα 5 του Agora_FINAL_DESIGN.
    /// </summary>
    public sealed partial class GameFlowController
    {
        Agora.GameContent AgoraContent => config.agoraContent;   // εγγυημένο από τον έλεγχο του Awake
        Agora.GameConfig AgoraCfg => config.agoraContent.config;
        Agora.CaseData CurrentAgoraCase => AgoraContent.CaseAt(State.prytany, State.week - 1);
        AgoraCaseState AgoraState => State.agoraCase;
        bool AgoraCaseResolved => AgoraState.caseId == CurrentAgoraCase.id && AgoraState.resolved;
        int AgoraSiteIndex => CurrentAgoraCase.venue == Agora.CaseVenue.Council ? 1 : 0;   // θέση στον χάρτη: 0 Πνύκα, 1 Βουλευτήριο
        bool AgoraCaseCurrent => AgoraState.caseId == CurrentAgoraCase.id;
        string AgoraVenueArt(Agora.CaseData c) => c.venue == Agora.CaseVenue.Council ? "boule" : "assembly";

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

        // ---------- Κουμπιά (καλείται ΠΡΙΝ τα παλιά Wire ώστε να προηγούνται οι νέοι χειριστές) ----------

        void WireAgoraCaseButtons()
        {
            Bind("deliberation.speech", () => GoCampaign("speech"));
            for (int i = 1; i < 4; i++) { int n = i; Bind("speech.logo." + i, () => AgoraSpeak(n)); }
            Bind("vote-result.primary", () => GoCampaign("week-report"));
            Bind("week-report.primary", AgoraAdvanceWeek);
            Bind("prytany-transition.primary", AgoraAdvancePrytany);
            Bind("ending-city.primary", () => GoCampaign("ending-personal"));
            // Μετά το τέλος του παίκτη: τα τελικά ερωτηματολόγια — ή η ολοκλήρωση, αν έχουν ήδη δοθεί
            // (π.χ. όταν ο παίκτης ξαναβλέπει τα τέλη από την οθόνη ολοκλήρωσης).
            Bind("ending-personal.primary", () => GoCampaign(Evaluation.completed ? "complete" : "final-knowledge"));
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
        }

        // ---------- Βήμα ----------

        /// <summary>Τα κουμπιά του βήματος: 1/2 = μιλώ υπέρ Α/Β, 3 = σιωπώ.</summary>
        void AgoraSpeak(int slot)
        {
            if (currentScreen != "speech" || AgoraState.spoke || AgoraState.resolved) return;
            Agora.Side side = slot == 1 ? Agora.Side.A : slot == 2 ? Agora.Side.B : Agora.Side.None;
            if (!MutateAndSave(() =>
            {
                if (side != Agora.Side.None) { AgoraState.spoke = true; AgoraState.spokeFor = (int)side; }
                AgoraState.step = "vote";
                State.phase = "vote";
            })) return;
            Show("vote");
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
            if (!MutateAndSave(() =>
            {
                if (State.prytany >= JsonContent.Settings.prytanyCount)
                {
                    finished = true;
                    cityEnding = Agora.EndingRules.City(State.polis, AgoraCfg);
                    Campaign.cityEnding = cityEnding.ToString();
                    next = "ending-city";   // πρώτα το τέλος (πόλη, παίκτης), μετά τα τελικά ερωτηματολόγια
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
                    ("cityEnding", cityEnding),
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
            if (id == "deliberation")
            {
                CampaignImage(AgoraVenueArt(c));
                AgoraFunds();
                CampaignText("heading", c.title);
                var speakerA = c.speechA.speaker;
                var speakerB = c.speechB.speaker;
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
                CampaignImage(AgoraVenueArt(c));
                AgoraFunds();
                CampaignText("heading", "Ψηφοφορία");
                CampaignText("body", c.title);
                CampaignLabel("choice.0", "Υπέρ της πρότασης Α");
                CampaignLabel("choice.1", "Υπέρ της πρότασης Β");
                // Το πλαϊνό πλαίσιο συνοψίζει τι έκανες στο βήμα — ποτέ άδειο σκούρο κουτί.
                CampaignText("role", AgoraState.spoke
                    ? "Μίλησες υπέρ της " + SideLabel((Agora.Side)AgoraState.spokeFor) + "."
                    : "Δεν μίλησες. Η ψήφος σου είναι το μόνο αποτύπωμά σου σε αυτή την υπόθεση.");
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

        void AgoraFunds() => CampaignText("funds", "Πορτοφόλι: " + State.citizen.wallet + " δρ.");

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
            // Ένδειξη εκκρεμότητας: Αγορά όσο δεν έχεις περάσει.
            SetActive("map.market.badge", !informed && !done);
            SetActive("map.market.count", !informed && !done);
            SetText("map.market.count", "!");
            SetActive("map.complaint.badge", false);
            SetActive("map.complaint.count", false);
            ShowMapGuideIfNeeded();
        }

        void RenderAgoraSpeech()
        {
            var c = CurrentAgoraCase;
            CampaignImage(AgoraVenueArt(c));
            AgoraFunds();
            CampaignText("heading", "Το βήμα είναι δικό σου");
            CampaignText("body", "Μπορείς να μιλήσεις για όποια πλευρά θες — ή να μείνεις σιωπηλός.");
            CampaignText("role", "");
            SetActive("speech.logo.0", false);
            SetActive("speech.logo.1", true); SetActive("speech.logo.2", true); SetActive("speech.logo.3", true);
            CampaignLabel("logo.1", "Μιλώ υπέρ της Α");
            CampaignLabel("logo.2", "Μιλώ υπέρ της Β");
            CampaignLabel("logo.3", "Σιωπώ");
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

        void RenderAgoraResult(Agora.CaseData c)
        {
            CampaignImage(AgoraVenueArt(c));
            AgoraFunds();
            CampaignText("heading", "Ἔδοξε τῷ δήμῳ: πρόταση " + (AgoraState.passedA ? "Α" : "Β"));
            CampaignText("body", "");
            CampaignText("majority.0", "Πρόταση Α" + (AgoraState.passedA ? " — επικράτησε" : ""));
            CampaignText("majority.1", "Πρόταση Β" + (!AgoraState.passedA ? " — επικράτησε" : ""));
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
                + "\n\n" + c.ek3);
            CampaignText("role", "Ταμείο " + State.polis.treasury + " · Επισιτισμός " + State.polis.food
                + " · Ετοιμότητα " + State.polis.readiness + " · Εμπιστοσύνη " + State.polis.trust
                + "\n\nΠειθώ " + State.citizen.peitho + " · Φήμη " + State.citizen.fame + " · Ηθική " + State.citizen.ethos);
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
