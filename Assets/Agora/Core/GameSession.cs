using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace Agora
{
    /// <summary>
    /// Η ροή του παιχνιδιού: μία μηχανή καταστάσεων, ένα panel ανά φάση, όλα προκατασκευασμένα στη σκηνή.
    /// Τα ερωτηματολόγια (pre/post test, στάσεις, MEEGA+) είναι δικά σου panels: τα ανοίγει αυτή η κλάση
    /// και εσύ καλείς τα OnXxxDone() όταν τελειώσουν. Κανένα Instantiate, καμία δημιουργία UI από κώδικα.
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        [Header("Περιεχόμενο")]
        public GameContent content;

        [Header("Panels φάσεων (υπάρχουν στη σκηνή, απενεργοποιημένα)")]
        public GameObject prologuePanel, preTestPanel, attitudePanel, characterPanel, prytanyIntroPanel;
        public CasePanelView casePanel;
        public GameObject postTestPanel, attitudePostPanel, meegaPanel, epiloguePanel;

        [Header("Τηλεμετρία (οποιοδήποτε component που υλοποιεί ITelemetry)")]
        public MonoBehaviour telemetryComponent;

        [Header("Γεγονότα για άλλα scripts (προαιρετικά)")]
        public UnityEvent<CityState, PlayerState> onCaseResolved;
        public UnityEvent<CityEnding, PersonalEnding> onGameEnded;

        // ---- κατάσταση ----
        public CityState City { get; private set; }
        public PlayerState Player { get; private set; }
        public Phase Current { get; private set; }

        private ITelemetry _log;
        private System.Random _rng;
        private int _prytany = 1, _caseIndex;
        private CaseData _case;
        private List<OfficialStance> _stances;
        private float _s0;
        private Side _intendedSide, _spokeFor, _vote;
        private PrepAction _prep;
        private bool _convicted, _spoke;
        private LogographerData _logo;
        private VoteResult _result;

        private GameConfig Cfg => content.config;

        // ======================= Έναρξη =======================

        private void Awake()
        {
            _log = telemetryComponent as ITelemetry ?? new ConsoleTelemetry();
            _rng = new System.Random();
            WireCaseButtons();
        }

        private void Start() => Enter(Phase.Prologue);

        /// <summary>Καλείται από το panel του προλόγου.</summary>
        public void OnPrologueDone() => Enter(Phase.PreTest);
        public void OnPreTestDone() => Enter(Phase.Attitude);
        public void OnAttitudeDone() => Enter(Phase.CharacterCreation);

        /// <summary>Καλείται από το panel δημιουργίας χαρακτήρα. Η πόλη ξεκινά ΠΑΝΤΑ από 50.</summary>
        public void OnCharacterCreated(string playerName, SocialClass cls, int peithoBonus, int ethosBonus, int synepeiaBonus)
        {
            City = new CityState(Cfg.cityStart);
            Player = new PlayerState(Cfg, cls, playerName);
            Player.peitho = PlayerState.Clamp(Player.peitho + peithoBonus);
            Player.ethos = PlayerState.Clamp(Player.ethos + ethosBonus);
            Player.synepeia = PlayerState.Clamp(Player.synepeia + synepeiaBonus);
            Log("character_created", ("class", cls), ("peitho", Player.peitho), ("ethos", Player.ethos), ("synepeia", Player.synepeia));
            _prytany = 1;
            BeginPrytany();
        }

        public void OnPrytanyIntroDone() { _caseIndex = 0; BeginCase(); }
        public void OnPostTestDone() => Enter(Phase.AttitudePost);
        public void OnAttitudePostDone() => Enter(Phase.Meega);
        public void OnMeegaDone() => Enter(Phase.Epilogue);

        // ======================= Πρυτανεία =======================

        private void BeginPrytany()
        {
            AssemblyRules.StartPrytany(City, Player, Cfg, _prytany);
            Log("prytany_start", ("prytany", _prytany), ("war", City.war), ("T", City.treasury), ("Ep", City.food), ("Et", City.readiness), ("Em", City.trust), ("ID", City.demosPower));
            Enter(Phase.PrytanyIntro);
        }

        // ======================= Υπόθεση =======================

        private void BeginCase()
        {
            _case = content.CaseAt(_prytany, _caseIndex);
            _stances = AssemblyRules.CloneStances(_case);
            _intendedSide = _spokeFor = _vote = Side.None;
            _prep = PrepAction.None; _convicted = _spoke = false; _logo = null;

            casePanel.BindHeader(_case, _prytany, content.prytanyTribes[_prytany - 1], City.war);
            casePanel.BindCity(City); casePanel.BindPlayer(Player);
            casePanel.newsText.text = _case.NewsFor(City.war);
            casePanel.ek1Text.text = _case.ek1;
            Log("case_start", ("case", _case.id), ("war", City.war));
            Enter(Phase.CaseNews);
        }

        private void ShowPrep()
        {
            _s0 = AssemblyRules.ComputeS0(_case, City, Cfg, _stances);
            casePanel.BindOfficials(_stances);
            casePanel.prepFeedback.text = "";
            bool hasNeutral = _stances.Any(s => s.side == Side.None && s.weight > 0);
            bool hasBribable = _stances.Any(s => s.official.bribable && s.weight > 0);
            casePanel.persuadeButton.interactable = hasNeutral;
            casePanel.bribeButton.interactable = hasBribable;
            casePanel.complainButton.gameObject.SetActive(_case.complaint.available && !Player.sycophantFlag);
            SetPrepButtons(false);
            Enter(Phase.CasePrep);
        }

        private void SetPrepButtons(bool sideChosen)
        {
            casePanel.persuadeButton.gameObject.SetActive(sideChosen);
            casePanel.bribeButton.gameObject.SetActive(sideChosen);
            casePanel.skipPrepButton.gameObject.SetActive(true);
        }

        private void ChooseSide(Side s)
        {
            _intendedSide = s;
            casePanel.prepFeedback.text = s == Side.None ? "Δεν παίρνεις θέση." : $"Υποστηρίζεις την πρόταση {s}.";
            SetPrepButtons(s != Side.None);
        }

        private void DoPersuade()
        {
            var neutral = _stances.First(s => s.side == Side.None && s.weight > 0);
            bool ok = AssemblyRules.Persuade(neutral, Player, _intendedSide);
            _prep = PrepAction.Persuade;
            casePanel.prepFeedback.text = ok
                ? $"Ο {neutral.official.displayName} σε άκουσε και θα στηρίξει την {_intendedSide}."
                : $"Ο {neutral.official.displayName} σε άκουσε ευγενικά και δεν άλλαξε γνώμη.";
            Log("persuade", ("case", _case.id), ("target", neutral.official.displayName), ("ok", ok));
            AfterPrep();
        }

        private void DoBribe()
        {
            var target = _stances.FirstOrDefault(s => s.side != Side.None && s.side != _intendedSide && s.official.bribable && s.weight > 0)
                         ?? _stances.First(s => s.official.bribable && s.weight > 0);
            bool ok = AssemblyRules.Bribe(target, Player, City, Cfg, _intendedSide, _rng, out bool exposed);
            _prep = PrepAction.Bribe;
            casePanel.prepFeedback.text = !ok ? "Δεν έχεις αρκετές δραχμές."
                : exposed ? $"Ο {target.official.displayName} πήρε τα χρήματα — αλλά το έμαθε η αγορά."
                : $"Ο {target.official.displayName} πήρε τα χρήματα. Κανείς δεν είδε τίποτα.";
            Log("bribe", ("case", _case.id), ("target", target.official.displayName), ("ok", ok), ("exposed", exposed));
            AfterPrep();
        }

        private void DoComplain()
        {
            var r = AssemblyRules.Complain(_case, _stances, Player, City, Cfg);
            _prep = PrepAction.Complain; _convicted = r.convicted;
            var opt = _case.complaint;
            casePanel.trialCharge.text = $"{opt.procedure} κατά {opt.target.FullName}: {opt.charge}";
            casePanel.trialDefence.text = opt.defence;
            casePanel.trialVerdict.text = $"{opt.votesFor}–{opt.votesAgainst}. {opt.verdictText}"
                + (r.sycophant ? $"\nΚάτω από το ένα πέμπτο: πρόστιμο {r.fine} δρ. Δεν μπορείς να καταγγείλεις ξανά." : "");
            Log("complaint", ("case", _case.id), ("target", opt.target.displayName), ("convicted", r.convicted), ("sycophant", r.sycophant));
            casePanel.BindPlayer(Player);
            casePanel.ShowOnly(casePanel.trialPanel);
        }

        private void AfterPrep()
        {
            _s0 = AssemblyRules.ComputeS0(_case, City, Cfg, _stances);
            casePanel.BindOfficials(_stances); casePanel.BindPlayer(Player);
            casePanel.persuadeButton.interactable = casePanel.bribeButton.interactable = false;
            casePanel.complainButton.gameObject.SetActive(false);
        }

        private void ShowSpeeches()
        {
            _s0 = AssemblyRules.ComputeS0(_case, City, Cfg, _stances);
            casePanel.speakerAName.text = AssemblyRules.SpeakerOf(_case.speechA, _case, _convicted).FullName;
            casePanel.speechAText.text = _case.speechA.text;
            casePanel.speakerBName.text = AssemblyRules.SpeakerOf(_case.speechB, _case, _convicted).FullName;
            casePanel.speechBText.text = _case.speechB.text;
            Enter(Phase.CaseSpeeches);
        }

        private void ShowPlayerSpeech()
        {
            if (_intendedSide == Side.None) { ShowVote(); return; }   // χωρίς θέση δεν μιλάς
            casePanel.BindLogographers(content.logographers, Player.wallet);
            Enter(Phase.CasePlayerSpeech);
        }

        private void Speak(int logoIndex)
        {
            _logo = content.logographers[logoIndex];
            if (Player.wallet < _logo.costDrachmas) return;
            Player.wallet -= _logo.costDrachmas;
            _spoke = true; _spokeFor = _intendedSide;
            ShowVote();
        }

        private void ShowVote()
        {
            casePanel.votePrompt.text = _spoke ? "Μίλησες. Τώρα ψήφισε — η ψήφος σου κρίνει μόνο τη Συνέπειά σου." : "Ψήφισε.";
            Enter(Phase.CaseVote);
        }

        private void Vote(Side v)
        {
            _vote = v;
            float contribution = _spoke ? AssemblyRules.SpeechPower(Player, _logo, Cfg) : 0f;
            _result = AssemblyRules.Resolve(_s0, _spoke ? _spokeFor : Side.None, contribution, _rng, Cfg);

            AssemblyRules.ApplyOutcome(_case, _result.passedA, City, Cfg);
            AssemblyRules.ApplyPlayerAfterCase(Player, Cfg, _spoke, _spokeFor, _vote, _result);

            casePanel.resultHeadline.text = $"Ἔδοξε τῷ δήμῳ: πρόταση {(_result.passedA ? "Α" : "Β")} ({Mathf.RoundToInt(_result.passedA ? _result.s : 100 - _result.s)}%)"
                                            + (_result.playerFlipped ? " — ο λόγος σου έκρινε την ψηφοφορία." : "");
            casePanel.resultEffects.text = DescribeEffects(_result.passedA ? _case.effectsA : _case.effectsB);
            casePanel.ek3Text.text = _case.ek3;
            casePanel.BindCity(City); casePanel.BindPlayer(Player);

            Log("case_result", ("case", _case.id), ("s0", _result.s0), ("s", _result.s), ("passedA", _result.passedA),
                ("intended", _intendedSide), ("spoke", _spoke), ("spokeFor", _spokeFor), ("logo", _logo ? _logo.quality : -1),
                ("vote", _vote), ("prep", _prep), ("flipped", _result.playerFlipped), ("contested", _result.contested),
                ("T", City.treasury), ("Ep", City.food), ("Et", City.readiness), ("Em", City.trust), ("ID", City.demosPower),
                ("wallet", Player.wallet), ("peitho", Player.peitho), ("ethos", Player.ethos), ("synepeia", Player.synepeia),
                ("parrhesia", Player.parrhesia), ("anagn", Player.anagnorisimotita), ("authority", Player.Authority));
            onCaseResolved?.Invoke(City, Player);
            Enter(Phase.CaseResult);
        }

        private void NextCase()
        {
            _caseIndex++;
            if (_caseIndex < 2) { BeginCase(); return; }
            _prytany++;
            if (_prytany <= 10) { BeginPrytany(); return; }
            EndGame();
        }

        private void EndGame()
        {
            var ce = EndingRules.City(City, Cfg);
            var pe = EndingRules.Personal(Player, ce);
            Log("game_end", ("cityEnding", ce), ("personalEnding", pe), ("excluded", EndingRules.ExcludedByPropertyLimit(Player, ce)),
                ("T", City.treasury), ("Ep", City.food), ("Et", City.readiness), ("Em", City.trust), ("ID", City.demosPower));
            onGameEnded?.Invoke(ce, pe);
            Enter(Phase.PostTest);     // το τέλος ΔΕΙΧΝΕΤΑΙ στον επίλογο, μετά τα ερωτηματολόγια
        }

        // ======================= Φάσεις / panels =======================

        private void Enter(Phase p)
        {
            Current = p;
            prologuePanel.SetActive(p == Phase.Prologue);
            preTestPanel.SetActive(p == Phase.PreTest);
            attitudePanel.SetActive(p == Phase.Attitude);
            characterPanel.SetActive(p == Phase.CharacterCreation);
            prytanyIntroPanel.SetActive(p == Phase.PrytanyIntro);
            postTestPanel.SetActive(p == Phase.PostTest);
            attitudePostPanel.SetActive(p == Phase.AttitudePost);
            meegaPanel.SetActive(p == Phase.Meega);
            epiloguePanel.SetActive(p == Phase.Epilogue);

            bool inCase = p >= Phase.CaseNews && p <= Phase.CaseResult;
            casePanel.gameObject.SetActive(inCase);
            if (!inCase) return;
            casePanel.ShowOnly(p switch
            {
                Phase.CaseNews => casePanel.newsPanel,
                Phase.CasePrep => casePanel.prepPanel,
                Phase.CaseSpeeches => casePanel.speechesPanel,
                Phase.CasePlayerSpeech => casePanel.playerSpeechPanel,
                Phase.CaseVote => casePanel.votePanel,
                _ => casePanel.resultPanel
            });
        }

        private void WireCaseButtons()
        {
            var v = casePanel;
            v.newsContinue.onClick.AddListener(ShowPrep);
            v.sideAButton.onClick.AddListener(() => ChooseSide(Side.A));
            v.sideBButton.onClick.AddListener(() => ChooseSide(Side.B));
            v.noSideButton.onClick.AddListener(() => ChooseSide(Side.None));
            v.persuadeButton.onClick.AddListener(DoPersuade);
            v.bribeButton.onClick.AddListener(DoBribe);
            v.complainButton.onClick.AddListener(DoComplain);
            v.trialContinue.onClick.AddListener(() => { AfterPrep(); v.ShowOnly(v.prepPanel); });
            v.skipPrepButton.onClick.AddListener(ShowSpeeches);
            v.speechesContinue.onClick.AddListener(ShowPlayerSpeech);
            for (int i = 0; i < v.logographerButtons.Length; i++) { int k = i; v.logographerButtons[i].onClick.AddListener(() => Speak(k)); }
            v.staySilentButton.onClick.AddListener(ShowVote);
            v.voteAButton.onClick.AddListener(() => Vote(Side.A));
            v.voteBButton.onClick.AddListener(() => Vote(Side.B));
            v.resultContinue.onClick.AddListener(NextCase);
        }

        // ======================= βοηθητικά =======================

        private static string DescribeEffects(IEnumerable<MetricDelta> effects)
        {
            string Name(Metric m) => m switch
            {
                Metric.Treasury => "Ταμείο", Metric.Food => "Επισιτισμός", Metric.Readiness => "Ετοιμότητα",
                Metric.Trust => "Θεσμική εμπιστοσύνη", _ => null   // ο ΙΔ δεν δείχνεται
            };
            return string.Join("   ", effects.Where(e => Name(e.metric) != null).Select(e => $"{Name(e.metric)} {(e.amount >= 0 ? "+" : "")}{e.amount}"));
        }

        private void Log(string ev, params (string key, object value)[] data)
        {
            var d = new Dictionary<string, object> { ["t"] = Time.realtimeSinceStartup, ["prytany"] = _prytany };
            foreach (var (k, val) in data) d[k] = val is System.Enum ? val.ToString() : val;
            _log.Log(ev, d);
        }
    }
}
