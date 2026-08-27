using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DemocracyWay.Core;
using DemocracyWay.Data;
using DemocracyWay.Services;
using DemocracyWay.UI;

namespace DemocracyWay.Gameplay
{
    /// <summary>
    /// The six-step character creation screen. All content comes from the
    /// CreationDatabase in <c>ServicesRoot.Config</c> — this controller only
    /// sequences the steps, so adding or editing options never touches code.
    ///
    /// Layout contract: options list on the RIGHT (spawned rows), preview of
    /// the hovered option (image + title + description) on the LEFT. Clicking
    /// selects and auto-advances; Πίσω re-opens the previous step with its
    /// pick cleared, so the player always re-decides consciously.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Character Creation Controller")]
    [DisallowMultipleComponent]
    public class CharacterCreationController : MonoBehaviour
    {
        private const int StepCount = 6;

        [Header("Λίστα επιλογών (δεξιά)")]
        [Tooltip("Το container όπου μπαίνουν τα κουμπιά επιλογών του τρέχοντος βήματος.")]
        [SerializeField] private RectTransform optionsContainer;

        [Tooltip("Prefab μίας επιλογής της λίστας.")]
        [SerializeField] private CreationOptionButton optionButtonPrefab;

        [Header("Προεπισκόπηση (αριστερά)")]
        [Tooltip("Η εικόνα της επιλογής που έχει hover ο παίκτης.")]
        [SerializeField] private Image previewImage;

        [Tooltip("Ο τίτλος της επιλογής που έχει hover ο παίκτης.")]
        [SerializeField] private TMP_Text previewTitleText;

        [Tooltip("Η περιγραφή της επιλογής που έχει hover ο παίκτης.")]
        [SerializeField] private TMP_Text previewDescriptionText;

        [Header("Πλοήγηση")]
        [Tooltip("Ο τίτλος του βήματος, π.χ. 'Βήμα 2/6 — Φυλή'.")]
        [SerializeField] private TMP_Text headerText;

        [Tooltip("Κουμπί επιστροφής στο προηγούμενο βήμα (κρυφό στο βήμα 1).")]
        [SerializeField] private UiButton backButton;

        [Tooltip("Κουμπί έναρξης — εμφανίζεται μόνο όταν ολοκληρωθούν και τα 6 βήματα.")]
        [SerializeField] private UiButton startButton;

        [Header("Κείμενα")]
        [Tooltip("Μορφή του τίτλου βήματος: {0}=αριθμός, {1}=σύνολο, {2}=όνομα βήματος.")]
        [SerializeField] private string headerFormat = "Βήμα {0}/{1} — {2}";

        [Tooltip("Τα ονόματα των 6 βημάτων με τη σειρά.")]
        [SerializeField] private string[] stepNames =
        {
            "Φύλο", "Φυλή", "Τριττύα", "Οικονομική Κατάστασις", "Περίοδος", "Επάγγελμα"
        };

        [Tooltip("Ο τίτλος όταν ολοκληρωθούν και τα 6 βήματα.")]
        [SerializeField] private string readyHeader = "Η δημιουργία ολοκληρώθηκε";

        [Tooltip("Ετικέτα του κουμπιού επιστροφής.")]
        [SerializeField] private string backLabel = "Πίσω";

        [Tooltip("Ετικέτα του κουμπιού έναρξης.")]
        [SerializeField] private string startLabel = "Έναρξη";

        [Header("Ροή")]
        [Tooltip("Η σκηνή που φορτώνει μετά την έναρξη (το intro comic).")]
        [SerializeField] private string comicSceneName = "ComicIntro";

        private CreationDatabase database;
        private readonly CitizenProfile profile = new CitizenProfile();
        private readonly List<CreationOptionButton> spawnedButtons = new List<CreationOptionButton>();

        /// <summary>0..5 while picking; == StepCount when all six are picked.</summary>
        private int currentStep;

        void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(OnBackPressed);
            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartPressed);
                startButton.gameObject.SetActive(false);
            }
        }

        void Start()
        {
            // Labels are set in Start, not Awake: UiButton resolves its TMP
            // label in its own Awake, and Awake order between scripts is not
            // guaranteed.
            if (backButton != null) backButton.Text = backLabel;
            if (startButton != null) startButton.Text = startLabel;

            database = ServicesRoot.Config != null ? ServicesRoot.Config.creationDatabase : null;
            if (database == null)
            {
                Debug.LogError("[CharacterCreation] Δεν βρέθηκε CreationDatabase στο GameConfig — η δημιουργία δεν μπορεί να τρέξει.", this);
                enabled = false;
                return;
            }
            BuildStep();
        }

        // ════════ Step lifecycle ════════

        /// <summary>Rebuilds the right-hand list for <see cref="currentStep"/>.</summary>
        private void BuildStep()
        {
            if (startButton != null) startButton.gameObject.SetActive(false);
            if (backButton != null) backButton.gameObject.SetActive(currentStep > 0);
            if (headerText != null)
                headerText.text = string.Format(headerFormat, currentStep + 1, StepCount, StepName(currentStep));

            ClearPreview();
            ClearOptionButtons();

            int spawned = 0;
            foreach (var option in OptionsForStep(currentStep))
            {
                var button = Instantiate(optionButtonPrefab, optionsContainer);
                button.Init(option, OnOptionHovered, OnOptionClicked);
                spawnedButtons.Add(button);
                spawned++;
            }

            // An empty list means the database filters returned nothing
            // (e.g. no trittyes authored for the chosen tribe) — the player
            // would be stuck, so shout at the author.
            if (spawned == 0)
                Debug.LogWarning($"[CharacterCreation] Το βήμα '{StepName(currentStep)}' δεν έχει καμία επιλογή στη CreationDatabase.", this);
        }

        /// <summary>All six picks made — swap the list for the start button.</summary>
        private void ShowReadyState()
        {
            ClearOptionButtons();
            if (headerText != null) headerText.text = readyHeader;
            if (backButton != null) backButton.gameObject.SetActive(true);
            if (startButton != null)
            {
                startButton.gameObject.SetActive(true);
                startButton.Interactable = profile.IsComplete;
            }
        }

        private string StepName(int step) =>
            (stepNames != null && step >= 0 && step < stepNames.Length) ? stepNames[step] : string.Empty;

        /// <summary>
        /// The options of one step. Steps 3 and 6 depend on earlier picks,
        /// which is why the list is rebuilt from the database every time
        /// instead of being cached.
        /// </summary>
        private IEnumerable<CreationOption> OptionsForStep(int step)
        {
            switch (step)
            {
                case 0: return database.genders;
                case 1: return database.tribes;
                case 2: return database.TrittyesFor(profile.tribeId);
                case 3: return database.wealthClasses;
                case 4: return database.periods;
                case 5: return database.ProfessionsFor(profile.trittysId);
                default: return Array.Empty<CreationOption>();
            }
        }

        // ════════ Option callbacks ════════

        private void OnOptionHovered(CreationOption option)
        {
            if (option == null) return;
            if (previewImage != null)
            {
                previewImage.sprite = option.image;
                previewImage.enabled = option.image != null;
            }
            if (previewTitleText != null) previewTitleText.text = option.title;
            if (previewDescriptionText != null) previewDescriptionText.text = option.description;
        }

        private void OnOptionClicked(CreationOption option)
        {
            if (option == null) return;
            RecordPick(option);
            currentStep++;
            if (currentStep >= StepCount) ShowReadyState();
            else BuildStep();
        }

        private void OnBackPressed()
        {
            if (currentStep <= 0) return;
            currentStep--;
            // The pick of the step we return TO is cleared: dependent lists
            // (Τριττύα, Επάγγελμα) must never filter on a stale parent id.
            ClearPick(currentStep);
            BuildStep();
        }

        private void OnStartPressed()
        {
            if (!profile.IsComplete) return;

            // Normally the main menu stored the chosen empty slot; when a dev
            // plays this scene directly there is no pending slot — fall back
            // to the first empty one so testing still works.
            int slot = PendingNewGame.TargetSlot;
            if (slot < 0) slot = SaveSystem.FirstEmptySlot();
            if (slot < 0)
            {
                Debug.LogError("[CharacterCreation] Δεν υπάρχει κενό slot για νέο παιχνίδι — η έναρξη ακυρώθηκε.", this);
                return;
            }

            var session = ServicesRoot.Session;
            if (session == null)
            {
                Debug.LogError("[CharacterCreation] Το SessionService δεν είναι διαθέσιμο — τρέξε από τη σκηνή Boot.", this);
                return;
            }

            session.StartNewGame(profile, slot);
            ServicesRoot.Flow?.GoToScene(comicSceneName, showLoading: true);
        }

        // ════════ Profile bookkeeping ════════

        private void RecordPick(CreationOption option)
        {
            switch (currentStep)
            {
                case 0:
                    profile.genderId = option.id;
                    profile.genderTitle = option.title;
                    // Copied at pick time so the save stays self-contained
                    // even if the database changes later.
                    profile.suspicionEnabled = option is GenderOption gender && gender.enablesSuspicion;
                    break;
                case 1: profile.tribeId = option.id; profile.tribeTitle = option.title; break;
                case 2: profile.trittysId = option.id; profile.trittysTitle = option.title; break;
                case 3: profile.wealthId = option.id; profile.wealthTitle = option.title; break;
                case 4: profile.periodId = option.id; profile.periodTitle = option.title; break;
                case 5: profile.professionId = option.id; profile.professionTitle = option.title; break;
            }
        }

        private void ClearPick(int step)
        {
            switch (step)
            {
                case 0:
                    profile.genderId = ""; profile.genderTitle = "";
                    profile.suspicionEnabled = false;
                    break;
                case 1: profile.tribeId = ""; profile.tribeTitle = ""; break;
                case 2: profile.trittysId = ""; profile.trittysTitle = ""; break;
                case 3: profile.wealthId = ""; profile.wealthTitle = ""; break;
                case 4: profile.periodId = ""; profile.periodTitle = ""; break;
                case 5: profile.professionId = ""; profile.professionTitle = ""; break;
            }
        }

        // ════════ Housekeeping ════════

        private void ClearOptionButtons()
        {
            foreach (var button in spawnedButtons)
                if (button != null) Destroy(button.gameObject);
            spawnedButtons.Clear();
        }

        private void ClearPreview()
        {
            if (previewImage != null) previewImage.enabled = false;
            if (previewTitleText != null) previewTitleText.text = string.Empty;
            if (previewDescriptionText != null) previewDescriptionText.text = string.Empty;
        }
    }
}
