using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Agora
{
    /// <summary>
    /// Το ΕΝΑ panel της υπόθεσης. Όλα τα στοιχεία υπάρχουν ήδη στη σκηνή και συνδέονται από τον Inspector.
    /// Η κλάση μόνο γεμίζει κείμενα και ανοιγοκλείνει υπο-panels. Δεν δημιουργεί τίποτα.
    /// </summary>
    public class CasePanelView : MonoBehaviour
    {
        [Header("Κεφαλίδα")]
        public TMP_Text prytanyLabel;       // «Πρυτανεία 4 — Λεοντίς»
        public TMP_Text caseTitle;
        public TMP_Text venueLabel;         // «Πνύκα» / «Βουλευτήριο»
        public TMP_Text warLabel;           // «Ειρήνη» / «Πόλεμος»

        [Header("Δείκτες")]
        public Slider treasury, food, readiness, trust;
        public TMP_Text walletText, peithoText, ethosText, synepeiaText, parrhesiaText, anagnText, authorityText;

        [Header("Υπο-panels (ένα ανά βήμα)")]
        public GameObject newsPanel, prepPanel, speechesPanel, playerSpeechPanel, votePanel, resultPanel, trialPanel;

        [Header("Είδηση")]
        public TMP_Text newsText, ek1Text;
        public Button newsContinue;

        [Header("Προπαρασκευή")]
        public Button sideAButton, sideBButton, noSideButton;
        public Button persuadeButton, bribeButton, complainButton, skipPrepButton;
        public TMP_Text officialsText;      // λίστα: «Φωκίων (στρατηγοί, 15) — υπέρ Α»
        public TMP_Text prepFeedback;

        [Header("Δίκη (καταγγελία)")]
        public TMP_Text trialCharge, trialDefence, trialVerdict;
        public Button trialContinue;

        [Header("Λόγοι")]
        public TMP_Text speakerAName, speechAText, speakerBName, speechBText;
        public Button speechesContinue;

        [Header("Ο λόγος του παίκτη")]
        public Button[] logographerButtons;  // 4: μόνος + 3 λογογράφοι, με τη σειρά του GameContent
        public TMP_Text[] logographerLabels;
        public Button staySilentButton;

        [Header("Ψήφος")]
        public Button voteAButton, voteBButton;
        public TMP_Text votePrompt;

        [Header("Αποτέλεσμα")]
        public TMP_Text resultHeadline;     // «Ἔδοξε τῷ δήμῳ: πρόταση Α (56%)»
        public TMP_Text resultEffects;
        public TMP_Text ek3Text;
        public Button resultContinue;

        public void ShowOnly(GameObject panel)
        {
            foreach (var p in new[] { newsPanel, prepPanel, speechesPanel, playerSpeechPanel, votePanel, resultPanel, trialPanel })
                if (p != null) p.SetActive(p == panel);
        }

        public void BindHeader(CaseData c, int prytany, string tribe, bool war)
        {
            prytanyLabel.text = $"Πρυτανεία {prytany} — {tribe}";
            caseTitle.text = $"Υπόθεση {c.id} — {c.title}";
            venueLabel.text = c.inCouncil ? "Βουλευτήριο" : "Πνύκα";
            warLabel.text = war ? "Πόλεμος" : "Ειρήνη";
        }

        public void BindCity(CityState city)
        {
            treasury.value = city.treasury; food.value = city.food; readiness.value = city.readiness; trust.value = city.trust;
        }

        public void BindPlayer(PlayerState p)
        {
            walletText.text = $"{p.wallet} δρ.";
            peithoText.text = p.peitho.ToString(); ethosText.text = p.ethos.ToString();
            synepeiaText.text = p.synepeia.ToString(); parrhesiaText.text = p.parrhesia.ToString();
            anagnText.text = p.anagnorisimotita.ToString(); authorityText.text = p.Authority.ToString();
        }

        public void BindOfficials(IList<OfficialStance> stances)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var s in stances)
            {
                if (s.weight == 0) { sb.AppendLine($"{s.official.FullName} — καταδικάστηκε, απουσιάζει"); continue; }
                string side = s.side == Side.A ? "υπέρ Α" : s.side == Side.B ? "υπέρ Β" : "αναποφάσιστος";
                sb.AppendLine($"{s.official.FullName} ({s.official.office.displayNameWithCount}, βάρος {s.weight}) — {side}");
            }
            officialsText.text = sb.ToString();
        }

        public void BindLogographers(IList<LogographerData> logos, int wallet)
        {
            for (int i = 0; i < logographerButtons.Length; i++)
            {
                bool has = i < logos.Count;
                logographerButtons[i].gameObject.SetActive(has);
                if (!has) continue;
                var l = logos[i];
                logographerLabels[i].text = l.costDrachmas == 0 ? $"{l.displayName} (μόνος μου)" : $"{l.displayName} — {l.costDrachmas} δρ.";
                logographerButtons[i].interactable = wallet >= l.costDrachmas;
            }
        }
    }
}
