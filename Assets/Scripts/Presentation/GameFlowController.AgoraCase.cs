using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Ενιαίο σύστημα: η ροή υπόθεσης του Agora μέσα στον GameFlowController.
    /// Κανόνες ΜΟΝΟ από Agora.AssemblyRules/EndingRules· περιεχόμενο ΜΟΝΟ από το GameContent asset.
    /// Ροή: Είδηση (procedure-briefing) → Προπαρασκευή (case-prep) → [Δίκη (case-trial)] →
    /// Λόγοι (deliberation) → Λόγος παίκτη (speech) → Ψήφος (vote) → Αποτέλεσμα (vote-result, week-report).
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
        Agora.Side AgoraIntendedSide => (Agora.Side)AgoraState.intendedSide;

        // ---------- Χαρακτήρας (καλείται από FinishBio και από direct scene preview) ----------

        void InitialiseAgoraCitizen()
        {
            var cfg = AgoraCfg;
            var cls = (Agora.SocialClass)Mathf.Clamp(3 - State.economicClassIndex, 0, 3);   // αντίστροφη σειρά τάξεων
            var citizen = new Agora.PlayerState(cfg, cls, State.playerName);
            citizen.peitho = Agora.PlayerState.Clamp(citizen.peitho + State.metrics.persuasion - State.baseline.persuasion);
            citizen.ethos = Agora.PlayerState.Clamp(citizen.ethos + State.metrics.ethics - State.baseline.ethics);
            citizen.synepeia = Agora.PlayerState.Clamp(citizen.synepeia + State.metrics.consistency - State.baseline.consistency);
            citizen.parrhesia = Agora.PlayerState.Clamp(citizen.parrhesia + State.metrics.parrhesia - State.baseline.parrhesia);
            citizen.anagnorisimotita = Agora.PlayerState.Clamp(citizen.anagnorisimotita + State.metrics.recognition - State.baseline.recognition);
            State.citizen = citizen;
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
            Bind("procedure-briefing.primary", () => GoCampaign("case-prep"));
            for (int i = 0; i < 3; i++) { int n = i; Bind("case-prep.side." + i, () => AgoraChooseSide(n)); }
            Bind("case-prep.act.persuade", AgoraPersuade);
            Bind("case-prep.act.bribe", AgoraBribe);
            Bind("case-prep.act.complain", AgoraComplain);
            Bind("case-prep.act.skip", () => { MutateAndSave(() => AgoraState.prepDone = true); RenderCampaign(); });
            Bind("case-prep.primary", () => { if (AgoraState.prepDone) GoCampaign("deliberation"); });
            Bind("case-prep.back", () => GoCampaign("map-stela"));
            Bind("case-trial.primary", () => GoCampaign("deliberation"));
            Bind("deliberation.speech", () => GoCampaign(AgoraIntendedSide != Agora.Side.None ? "speech" : "vote"));
            for (int i = 0; i < 4; i++) { int n = i; Bind("speech.logo." + i, () => AgoraSpeak(n)); }
            Bind("vote.choice.0", () => AgoraVote(Agora.Side.A));
            Bind("vote.choice.1", () => AgoraVote(Agora.Side.B));
            Bind("vote-result.primary", () => GoCampaign("week-report"));
            Bind("week-report.primary", AgoraAdvanceWeek);
            Bind("prytany-transition.primary", AgoraAdvancePrytany);
            Bind("ending-city.primary", () => GoCampaign("ending-personal"));
            Bind("ending-personal.primary", () => GoCampaign("complete"));
        }

        void BeginAgoraCase()
        {
            EnsureAgoraReady();
            var c = CurrentAgoraCase;
            if (AgoraState.caseId != c.id)
            {
                State.agoraCase = new AgoraCaseState { caseId = c.id };
                StoreAgoraStances(Agora.AssemblyRules.CloneStances(c));
            }
            GoCampaign("procedure-briefing");
        }

        // ---------- Προπαρασκευή: θέση + ΜΙΑ ενέργεια ----------

        void AgoraChooseSide(int n)
        {
            if (currentScreen != "case-prep" || AgoraState.prepDone) return;
            MutateAndSave(() => AgoraState.intendedSide = (int)(n == 0 ? Agora.Side.A : n == 1 ? Agora.Side.B : Agora.Side.None));
            RenderCampaign();
        }

        void AgoraPersuade()
        {
            if (currentScreen != "case-prep" || AgoraState.prepDone || AgoraIntendedSide == Agora.Side.None) return;
            var stances = AgoraStances();
            var neutral = stances.FirstOrDefault(s => s.side == Agora.Side.None && s.weight > 0);
            if (neutral == null) return;
            bool ok = false;
            if (!MutateAndSave(() =>
            {
                ok = Agora.AssemblyRules.Persuade(neutral, State.citizen, AgoraIntendedSide);
                AgoraState.prep = (int)Agora.PrepAction.Persuade;
                AgoraState.prepDone = true;
                StoreAgoraStances(stances);
            })) return;
            SetText("case-prep.feedback", ok
                ? "Ο " + neutral.official.displayName + " σε άκουσε και θα στηρίξει την " + SideLabel(AgoraIntendedSide) + "."
                : "Ο " + neutral.official.displayName + " σε άκουσε ευγενικά και δεν άλλαξε γνώμη.");
            AgoraFirestoreTelemetry.Send("persuade", AgoraEvent(("target", neutral.official.displayName), ("ok", ok)));
            RenderCampaign();
        }

        void AgoraBribe()
        {
            if (currentScreen != "case-prep" || AgoraState.prepDone || AgoraIntendedSide == Agora.Side.None) return;
            var stances = AgoraStances();
            var target = stances.FirstOrDefault(s => s.side != Agora.Side.None && s.side != AgoraIntendedSide && s.official.bribable && s.weight > 0)
                         ?? stances.FirstOrDefault(s => s.official.bribable && s.weight > 0);
            if (target == null) return;
            bool ok = false, exposed = false;
            if (!MutateAndSave(() =>
            {
                ok = Agora.AssemblyRules.Bribe(target, State.citizen, State.polis, AgoraCfg, AgoraIntendedSide, agoraRng, out exposed);
                if (ok) { AgoraState.prep = (int)Agora.PrepAction.Bribe; AgoraState.prepDone = true; StoreAgoraStances(stances); }
            })) return;
            SetText("case-prep.feedback", !ok ? "Δεν έχεις αρκετές δραχμές."
                : exposed ? "Ο " + target.official.displayName + " πήρε τα χρήματα — αλλά το έμαθε η αγορά."
                : "Ο " + target.official.displayName + " πήρε τα χρήματα. Κανείς δεν είδε τίποτα.");
            AgoraFirestoreTelemetry.Send("bribe", AgoraEvent(("target", target.official.displayName), ("ok", ok), ("exposed", exposed)));
            RenderCampaign();
        }

        void AgoraComplain()
        {
            if (currentScreen != "case-prep" || AgoraState.prepDone) return;
            var stances = AgoraStances();
            var result = default(Agora.ComplaintResult);
            if (!MutateAndSave(() =>
            {
                result = Agora.AssemblyRules.Complain(CurrentAgoraCase, stances, State.citizen, State.polis, AgoraCfg);
                if (!result.filed) return;
                AgoraState.prep = (int)Agora.PrepAction.Complain;
                AgoraState.prepDone = true;
                AgoraState.convicted = result.convicted;
                AgoraState.trialSycophant = result.sycophant;
                AgoraState.trialFine = result.fine;
                StoreAgoraStances(stances);
                var record = CampaignRules.Current(State);
                record.caseId = CurrentAgoraCase.id.ToString();
                record.complaintFiled = true;
                record.complaintConvicted = result.convicted;
                State.phase = "case-trial";
            }) || !result.filed) return;
            AgoraFirestoreTelemetry.Send("complaint", AgoraEvent(
                ("target", CurrentAgoraCase.complaint.target.displayName), ("convicted", result.convicted), ("sycophant", result.sycophant)));
            Show("case-trial");
        }

        // ---------- Λόγος παίκτη και ψήφος ----------

        void AgoraSpeak(int logoIndex)
        {
            if (currentScreen != "speech" || AgoraState.spoke || AgoraIntendedSide == Agora.Side.None) return;
            if (logoIndex < 0 || logoIndex >= AgoraContent.logographers.Count) return;
            var logo = AgoraContent.logographers[logoIndex];
            if (State.citizen.wallet < logo.costDrachmas) return;
            if (!MutateAndSave(() =>
            {
                State.citizen.wallet -= logo.costDrachmas;
                AgoraState.spoke = true;
                AgoraState.spokeFor = AgoraState.intendedSide;
                AgoraState.logoIndex = logoIndex;
                State.phase = "vote";
            })) return;
            Show("vote");
        }

        void AgoraVote(Agora.Side side)
        {
            if (currentScreen != "vote" || AgoraState.resolved) return;
            var c = CurrentAgoraCase;
            var stances = AgoraStances();
            var logo = AgoraState.spoke && AgoraState.logoIndex >= 0 ? AgoraContent.logographers[AgoraState.logoIndex] : null;
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
                ("intended", AgoraIntendedSide), ("spoke", AgoraState.spoke), ("spokeFor", spokeFor),
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
            if (id == "procedure-briefing")
            {
                CampaignImage(c.inCouncil ? "boule" : "assembly");
                AgoraFunds();
                CampaignText("heading", (c.inCouncil ? "Βουλευτήριο" : "Πνύκα") + " · " + c.title);
                CampaignText("body", c.NewsFor(State.polis.war) + "\n\n" + c.ek1);
                CampaignText("role", State.polis.war ? "Σημαία: Πόλεμος" : "Σημαία: Ειρήνη");
                CampaignText("source", c.athPolRef + " · Εκπαιδευτικό, μυθοπλαστικό περιστατικό.");
                return true;
            }
            if (id == "case-prep") { RenderAgoraPrep(c); return true; }
            if (id == "case-trial") { RenderAgoraTrial(c); return true; }
            if (id == "deliberation")
            {
                CampaignImage(c.inCouncil ? "boule" : "assembly");
                AgoraFunds();
                CampaignText("heading", c.title);
                bool convicted = AgoraState.convicted;
                SetText("deliberation.argument.0", Agora.AssemblyRules.SpeakerOf(c.speechA, c, convicted).FullName + "\n\n" + c.speechA.text);
                SetText("deliberation.argument.1", Agora.AssemblyRules.SpeakerOf(c.speechB, c, convicted).FullName + "\n\n" + c.speechB.text);
                CampaignLabel("speech", AgoraIntendedSide != Agora.Side.None ? "Ζητώ τον λόγο" : "Προς την ψηφοφορία");
                return true;
            }
            if (id == "speech") { RenderAgoraSpeech(); return true; }
            if (id == "vote")
            {
                CampaignImage(c.inCouncil ? "boule" : "assembly");
                AgoraFunds();
                CampaignText("heading", "Ψηφοφορία");
                CampaignText("body", c.title + (AgoraState.spoke ? "\n\nΜίλησες. Η ψήφος σου κρίνει μόνο τη Συνέπειά σου." : ""));
                CampaignLabel("choice.0", "Υπέρ της πρότασης Α");
                CampaignLabel("choice.1", "Υπέρ της πρότασης Β");
                CampaignText("role", "");
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

        void RenderAgoraMap()
        {
            EnsureAgoraReady();
            var c = CurrentAgoraCase;
            bool done = AgoraCaseResolved;
            for (int i = 0; i < 4; i++)
            {
                bool active = AgoraSiteIndex == i && !done;
                string key = "map.institution." + i;
                var plate = Get<PanelGraphic>(key + ".plate");
                var title = Get<TMPro.TMP_Text>(key + ".title");
                if (plate != null)
                {
                    plate.color = active ? new Color32(232, 223, 203, 255) : new Color32(25, 47, 52, 255);
                    plate.rectTransform.sizeDelta = new Vector2(plate.rectTransform.sizeDelta.x, active ? 72 : 50.4f);
                }
                if (title != null)
                {
                    title.text = InstitutionNames[i];
                    title.enableAutoSizing = true; title.fontSizeMin = 15; title.fontSizeMax = 22;
                    title.color = active ? new Color32(23, 43, 48, 255) : new Color32(238, 228, 203, 255);
                    title.rectTransform.anchoredPosition = new Vector2(0, active ? -11 : -9);
                }
                SetActive(key + ".newCase", active);
            }
            SetText("map.city", "Ταμείο " + State.polis.treasury + " · Επισιτισμός " + State.polis.food
                + " · Ετοιμότητα " + State.polis.readiness + " · Εμπιστοσύνη " + State.polis.trust);
            SetText("map.personal", "Πορτοφόλι " + State.citizen.wallet + " δρ. · Αναγνωρισιμότητα " + State.citizen.anagnorisimotita);
            // Τα παλιά badges «νέων» δεν έχουν νόημα στο αρχείο — μένουν κρυφά.
            foreach (string key in new[] { "map.market.badge", "map.market.count", "map.complaint.badge", "map.complaint.count" })
                SetActive(key, false);
        }

        void RenderAgoraPrep(Agora.CaseData c)
        {
            CampaignImage(c.inCouncil ? "boule" : "assembly");
            AgoraFunds();
            var stances = AgoraStances();
            CampaignText("heading", "Προπαρασκευή · " + c.title);
            CampaignText("body", string.Join("\n", stances.Where(s => s.weight > 0).Select(s =>
                s.official.FullName + " — " + (s.side == Agora.Side.None ? "αναποφάσιστος" : "υπέρ της " + SideLabel(s.side)))));
            CampaignLabel("side.0", "Υποστηρίζω την Α");
            CampaignLabel("side.1", "Υποστηρίζω τη Β");
            CampaignLabel("side.2", "Δεν παίρνω θέση");
            for (int i = 0; i < 3; i++)
                PaintRow("case-prep.side." + i, AgoraState.intendedSide == (i == 0 ? 1 : i == 1 ? 2 : 0) && (i < 2 || AgoraState.intendedSide == 0));
            bool sideChosen = AgoraIntendedSide != Agora.Side.None;
            bool hasNeutral = stances.Any(s => s.side == Agora.Side.None && s.weight > 0);
            bool hasBribable = stances.Any(s => s.official.bribable && s.weight > 0);
            SetInteractable("case-prep.act.persuade", !AgoraState.prepDone && sideChosen && hasNeutral);
            SetInteractable("case-prep.act.bribe", !AgoraState.prepDone && sideChosen && hasBribable);
            SetActive("case-prep.act.complain", c.complaint.available && !State.citizen.sycophantFlag);
            SetInteractable("case-prep.act.complain", !AgoraState.prepDone);
            SetInteractable("case-prep.act.skip", !AgoraState.prepDone);
            SetInteractable("case-prep.primary", AgoraState.prepDone);
            CampaignText("role", AgoraState.prepDone ? "Η ενέργειά σου έγινε. Συνέχισε προς τους λόγους." : "Διάλεξε θέση και ΜΙΑ ενέργεια — ή προσπέρασε.");
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
            CampaignImage(CurrentAgoraCase.inCouncil ? "boule" : "assembly");
            AgoraFunds();
            CampaignText("heading", "Ο λόγος σου υπέρ της " + SideLabel(AgoraIntendedSide));
            CampaignText("body", "Διάλεξε πώς θα προετοιμαστείς. Ο λογογράφος κοστίζει, αλλά δυναμώνει τον λόγο σου.");
            for (int i = 0; i < 4; i++)
            {
                var logo = i < AgoraContent.logographers.Count ? AgoraContent.logographers[i] : null;
                SetActive("speech.logo." + i, logo != null);
                if (logo == null) continue;
                CampaignLabel("logo." + i, logo.displayName + (logo.costDrachmas > 0 ? " · " + logo.costDrachmas + " δρ." : " · δωρεάν"));
                SetInteractable("speech.logo." + i, State.citizen.wallet >= logo.costDrachmas);
            }
            CampaignText("role", "");
            CampaignText("source", "");
        }

        void RenderAgoraResult(Agora.CaseData c)
        {
            CampaignImage(c.inCouncil ? "boule" : "assembly");
            AgoraFunds();
            int percent = Mathf.RoundToInt(AgoraState.passedA ? AgoraState.s : 100 - AgoraState.s);
            CampaignText("heading", "Ἔδοξε τῷ δήμῳ: πρόταση " + (AgoraState.passedA ? "Α" : "Β") + " (" + percent + "%)");
            CampaignText("body", (AgoraState.playerFlipped ? "Ο λόγος σου έκρινε την ψηφοφορία.\n\n" : AgoraState.contested ? "Η υπόθεση ήταν αμφίρροπη.\n\n" : "")
                + DescribeAgoraEffects(AgoraState.passedA ? c.effectsA : c.effectsB));
            CampaignText("majority.0", "Πρόταση Α" + (AgoraState.passedA ? " — επικράτησε" : ""));
            CampaignText("majority.1", "Πρόταση Β" + (!AgoraState.passedA ? " — επικράτησε" : ""));
            var bar = Get<UnityEngine.UI.Image>("vote-result.majority.bar");
            if (bar != null)
                bar.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(100f, 900f, Mathf.Clamp01(percent / 100f)), bar.rectTransform.sizeDelta.y);
            CampaignText("city-effect", "");
            CampaignText("personal-effect", "");
            CampaignText("speech-effect", "");
            CampaignText("source", c.ek3);
        }

        void RenderAgoraWeekReport(Agora.CaseData c)
        {
            AgoraFunds();
            CampaignText("heading", "Απολογισμός");
            string vote = AgoraState.vote == (int)Agora.Side.A ? "Α" : "Β";
            CampaignText("body", c.title + "\n\nΕπικράτησε η πρόταση " + (AgoraState.passedA ? "Α" : "Β") + ". Ψήφισες " + vote + "."
                + (AgoraState.spoke ? " Μίλησες δημόσια." : " Δεν ζήτησες τον λόγο.") + "\n\n" + c.ek3);
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
