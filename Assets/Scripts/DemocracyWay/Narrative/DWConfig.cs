using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    [CreateAssetMenu(fileName = "DWConfig", menuName = "Democracy Way/Narrative/Configuration")]
    public sealed class DWConfig : ScriptableObject
    {
        public AudioClip mainMenuMusic;
        public AudioClip introductionMusic;
        public AudioClip gameplayMusic;
        public List<AudioClip> prytanyMusic = new List<AudioClip>();
        public AudioClip clickSound;
        public AudioClip hoverSound;
        public Texture2D cursorTexture;
        public Vector2 cursorHotspot;
        public int contentVersion = 1;
        public int playerAge = 30;
        public int startingPersonalFunds = 120;
        public int startingCityFunds = 10000;
        public string defaultProfession = "Τεχνίτης";
        public DWMetrics startingMetrics = new DWMetrics
        {
            consistency = 50,
            ethics = 50,
            authority = 35,
            persuasion = 35,
            parrhesia = 35,
            recognition = 10
        };
        public List<DWPage> prologue = new List<DWPage>();
        public List<DWBioStep> biography = new List<DWBioStep>();
        public List<DWTribe> tribes = new List<DWTribe>();
        public List<string> economicClasses = new List<string>
        {
            "Πεντακοσιομέδιμνοι",
            "Ιππείς",
            "Ζευγίτες",
            "Θήτες"
        };
        public List<DWQuote> prytanyQuotes = new List<DWQuote>();
        public AudioClip MusicForPrytany(int prytany)
        {
            int index = prytany - 1;
            return prytanyMusic != null && index >= 0 && index < prytanyMusic.Count && prytanyMusic[index] != null
                ? prytanyMusic[index] : gameplayMusic;
        }

        public bool Validate(out string error)
        {
            error = "";
            if (contentVersion < 1 || prologue == null || prologue.Count == 0 || biography == null || biography.Count == 0 || tribes == null || tribes.Count != 10 || economicClasses == null || economicClasses.Count != 4 || startingMetrics == null)
            {
                error = "Το DWConfig χρειάζεται πρόλογο, βιογραφία, δέκα φυλές και τέσσερις οικονομικές τάξεις.";
                return false;
            }

            var ids = new HashSet<string>();
            foreach (DWPage page in prologue)
                if (page == null || string.IsNullOrWhiteSpace(page.id) || !ids.Add(page.id))
                {
                    error = "Ο πρόλογος έχει κενό ή επαναλαμβανόμενο ID σελίδας.";
                    return false;
                }

            ids.Clear();
            foreach (DWTribe tribe in tribes)
                if (tribe == null || string.IsNullOrWhiteSpace(tribe.id) || string.IsNullOrWhiteSpace(tribe.name) || !ids.Add(tribe.id))
                {
                    error = "Οι φυλές χρειάζονται δέκα διαφορετικά ID και ονόματα.";
                    return false;
                }

            ids.Clear();
            int identitySteps = 0;
            foreach (DWBioStep step in biography)
            {
                if (step == null || string.IsNullOrWhiteSpace(step.id) || !ids.Add(step.id) || step.choices == null)
                {
                    error = "Η βιογραφία έχει κενό ή επαναλαμβανόμενο ID βήματος.";
                    return false;
                }

                if (step.kind == DWBioKind.Identity)
                {
                    identitySteps++;
                    if (step.choices.Count != 0 && step.choices.Count != 10)
                    {
                        error = "Το βήμα ταυτότητας δέχεται είτε καμία είτε δέκα επιλογές καταγωγής.";
                        return false;
                    }
                }
                else if (step.choices.Count < 1 || step.choices.Count > 4)
                {
                    error = "Κάθε αφηγηματικό βήμα χρειάζεται μία έως τέσσερις επιλογές.";
                    return false;
                }

                var choiceIds = new HashSet<string>();
                foreach (DWChoice choice in step.choices)
                    if (choice == null || string.IsNullOrWhiteSpace(choice.id) || !choiceIds.Add(choice.id))
                    {
                        error = "Οι επιλογές χρειάζονται διαφορετικό ID μέσα σε κάθε βήμα.";
                        return false;
                    }
            }

            if (identitySteps != 1)
            {
                error = "Η βιογραφία χρειάζεται ακριβώς ένα βήμα ταυτότητας.";
                return false;
            }

            return true;
        }
    }
}
