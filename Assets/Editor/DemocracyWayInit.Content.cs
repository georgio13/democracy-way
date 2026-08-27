#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DemocracyWay.EditorTools
{
    using DemocracyWay.Core;

    /// <summary>
    /// Seeds the two content databases and the intro comic.
    ///
    /// The Athenian data below follows the Kleisthenic reform of 508/7 BC: ten
    /// tribes, each split into three trittyes drawn from a different region —
    /// άστυ (city), παραλία (coast), μεσόγαια (inland) — precisely so that no
    /// tribe was a single regional bloc. Each trittys is named here after one of
    /// its better-attested demes.
    ///
    /// Deme-to-trittys assignments are reconstructed from inscriptions and are
    /// not certain for every deme in the scholarship; treat the names as
    /// representative rather than authoritative, and edit the generated
    /// CreationDatabase asset freely — nothing here re-runs unless you re-run
    /// Init.
    /// </summary>
    public static partial class DemocracyWayInit
    {
        private const string ContentFolder     = "Assets/Content";
        private const string CreationDbPath    = "Assets/Content/CreationDatabase.asset";
        private const string DialogueDbPath    = "Assets/Content/DialogueDatabase.asset";
        private const string ComicSequencePath = "Assets/Content/IntroComic.asset";

        // ═════════════════════════════════════════════════════════════════════
        // TRIBES + TRITTYES
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>One tribe and the three demes that name its trittyes,
        /// in the order city / coast / inland.</summary>
        private readonly struct TribeSeed
        {
            public readonly string Id;
            public readonly string Name;      // Ερεχθηΐς
            public readonly string Hero;      // Ερεχθεύς
            public readonly string Blurb;
            public readonly string CityDeme;
            public readonly string CoastDeme;
            public readonly string InlandDeme;

            public TribeSeed(string id, string name, string hero, string blurb,
                             string cityDeme, string coastDeme, string inlandDeme)
            {
                Id = id; Name = name; Hero = hero; Blurb = blurb;
                CityDeme = cityDeme; CoastDeme = coastDeme; InlandDeme = inlandDeme;
            }
        }

        private static readonly TribeSeed[] Tribes =
        {
            new TribeSeed("tribe_erechtheis", "Ερεχθηΐς", "Ερεχθεύς",
                "Η πρώτη φυλή, υπό τον μυθικό βασιλέα πού ανέθρεψε η ίδια η Αθηνά. Παλαιά γένη, ισχυρές διασυνδέσεις με την Ακρόπολη.",
                "Αγρυλή", "Αναγυρούς", "Κηφισιά"),

            new TribeSeed("tribe_aigeis", "Αιγηΐς", "Αιγεύς",
                "Η φυλή του πατέρα του Θησέως. Εκτείνεται από την καρδιά του άστεως ως τις ανατολικές ακτές.",
                "Κολλυτός", "Αλαί Αραφηνίδες", "Τείθρας"),

            new TribeSeed("tribe_pandionis", "Πανδιονίς", "Πανδίων",
                "Φυλή με ισχυρή παρουσία στο κέντρο της πόλεως και στις εύφορες πεδιάδες της Μεσογαίας.",
                "Κυδαθήναιον", "Πρασιαί", "Παιανία"),

            new TribeSeed("tribe_leontis", "Λεοντίς", "Λέως",
                "Από τον ήρωα πού θυσίασε τις θυγατέρες του για τη σωτηρία της πόλεως. Φήμη για αυστηρή αίσθηση καθήκοντος.",
                "Σκαμβωνίδαι", "Φρεάρριοι", "Παιονίδαι"),

            new TribeSeed("tribe_akamantis", "Ακαμαντίς", "Ακάμας",
                "Περιλαμβάνει τον Κεραμεικό — εργαστήρια, αγγειοπλάστες, και τα μεταλλεία του Θορικού στην ακτή.",
                "Κεραμείς", "Θορικός", "Σφηττός"),

            new TribeSeed("tribe_oineis", "Οινηΐς", "Οινεύς",
                "Η φυλή των Αχαρνών, του μεγαλύτερου δήμου της Αττικής. Ανθρακείς, γεωργοί, σκληροί οπλίτες.",
                "Λακιάδαι", "Θριά", "Αχαρναί"),

            new TribeSeed("tribe_kekropis", "Κεκροπίς", "Κέκροψ",
                "Υπό τον πρώτο βασιλέα της Αττικής. Εύπορες παράλιες κοινότητες και πυκνοκατοικημένο άστυ.",
                "Μελίτη", "Αλαί Αιξωνίδες", "Αθμονόν"),

            new TribeSeed("tribe_hippothontis", "Ιπποθοντίς", "Ιπποθόων",
                "Κρατά την Ελευσίνα με τα Μυστήρια — και, μετά τον Θεμιστοκλή, τον Πειραιά.",
                "Πειραιεύς", "Ελευσίς", "Δεκέλεια"),

            new TribeSeed("tribe_aiantis", "Αιαντίς", "Αίας",
                "Η μόνη φυλή με επώνυμο ήρωα εκτός Αττικής — τον Αίαντα της Σαλαμίνος. Στρατιωτική φήμη.",
                "Φαληρόν", "Ραμνούς", "Αφίδνα"),

            new TribeSeed("tribe_antiochis", "Αντιοχίς", "Αντίοχος",
                "Εκτείνεται ως τον Αναφλυστό και τα αργυρωρυχεία του Λαυρίου — πλούτος βγαλμένος από τη γη.",
                "Αλωπεκή", "Αναφλυστός", "Παλλήνη"),
        };

        // ═════════════════════════════════════════════════════════════════════
        // DATABASE BUILDERS
        // ═════════════════════════════════════════════════════════════════════

        private static CreationDatabase BuildCreationDatabase()
        {
            // Every list is built BEFORE the asset is touched. The builders
            // resolve artwork, which can import a PNG, and an import tears down
            // and reloads assets — including a ScriptableObject we are holding.
            // Assigning into a stale one leaves the file on disk with empty
            // lists and no error anywhere. So: build, then load, then assign,
            // then flush, with nothing in between that could trigger an import.
            var genders       = BuildGenders();
            var tribes        = BuildTribes();
            var trittyes      = BuildTrittyes();
            var wealthClasses = BuildWealthClasses();
            var periods       = BuildPeriods();
            var occupations   = BuildOccupations();

            EnsureFolder(ContentFolder);

            var db = AssetDatabase.LoadAssetAtPath<CreationDatabase>(CreationDbPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<CreationDatabase>();
                AssetDatabase.CreateAsset(db, CreationDbPath);
            }

            db.genders       = genders;
            db.tribes        = tribes;
            db.trittyes      = trittyes;
            db.wealthClasses = wealthClasses;
            db.periods       = periods;
            db.occupations   = occupations;

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();   // flush now, while db is still valid
            Debug.Log($"[Init] CreationDatabase: {db.tribes.Count} φυλές, {db.trittyes.Count} τριττύες, " +
                      $"{db.occupations.Count} επαγγέλματα.");
            return db;
        }

        private static List<CreationOption> BuildGenders() => new List<CreationOption>
        {
            new CreationOption
            {
                id = "gender_male",
                title = "Ανήρ",
                subtitle = "Πολίτης με πλήρη δικαιώματα",
                description =
                    "Ως ενήλικος άρρην πολίτης έχεις ό,τι η Αθήνα ονομάζει πολιτικά δικαιώματα: " +
                    "ψήφο στην Εκκλησία του Δήμου, θέση στα δικαστήρια, δικαίωμα κληρώσεως στα " +
                    "αξιώματα. Έχεις επίσης τις υποχρεώσεις — στρατεία, λειτουργίες, ευθύνη.",
                artwork = LoadCharOrFallback("CH_CitizenMale")
            },
            new CreationOption
            {
                id = "gender_female",
                title = "Γυνή",
                subtitle = "Χωρίς πολιτικά δικαιώματα",
                description =
                    "Η Αθηναία δεν ψηφίζει, δεν δικάζει, δεν κληρώνεται. Όμως η επιρροή της " +
                    "διέρχεται από τον οίκο, τη θρησκευτική ζωή και τα δίκτυα των γενών — " +
                    "και η δημοκρατία πού θα γνωρίσεις είναι μια εντελώς άλλη εμπειρία.",
                artwork = LoadCharOrFallback("CH_CitizenFemale")
            },
        };

        private static List<CreationOption> BuildTribes()
        {
            var list = new List<CreationOption>(Tribes.Length);
            foreach (var t in Tribes)
            {
                list.Add(new CreationOption
                {
                    id = t.Id,
                    title = t.Name,
                    subtitle = $"Επώνυμος ήρως: {t.Hero}",
                    description =
                        $"{t.Blurb}\n\n" +
                        $"Όπως κάθε Κλεισθένεια φυλή, συντίθεται από τρεις τριττύες: μια αστική " +
                        $"({t.CityDeme}), μια παράλια ({t.CoastDeme}) και μια μεσόγαια ({t.InlandDeme}). " +
                        $"Ο Κλεισθένης το σχεδίασε έτσι ώστε καμμία φυλή να μην είναι ενιαίο " +
                        $"περιφερειακό μπλοκ.",
                    artwork = LoadBgOrFallback($"BG_Tribe_{t.Id}")
                });
            }
            return list;
        }

        private static List<CreationOption> BuildTrittyes()
        {
            var list = new List<CreationOption>(Tribes.Length * 3);
            foreach (var t in Tribes)
            {
                list.Add(MakeTrittys(t, "astu",     "Άστυ",     t.CityDeme,
                    "Ζεις μέσα ή δίπλα στην πόλη. Η Πνύκα απέχει λίγα λεπτά· είσαι στην Εκκλησία " +
                    "κάθε φορά πού συνέρχεται, και οι φήμες σε φτάνουν πρώτον."));

                list.Add(MakeTrittys(t, "paralia",  "Παραλία",  t.CoastDeme,
                    "Ζεις στην ακτή. Η θάλασσα είναι εισόδημα και κίνδυνος μαζί· ο στόλος περνά " +
                    "από εδώ, και μαζί του το εμπόριο, οι ξένοι και οι ειδήσεις."));

                list.Add(MakeTrittys(t, "mesogaia", "Μεσόγαια", t.InlandDeme,
                    "Ζεις στην ενδοχώρα. Η γη ορίζει τον χρόνο σου· η διαδρομή προς το άστυ " +
                    "κοστίζει μια ολόκληρη μέρα, και δεν την κάνεις για κάθε ψηφοφορία."));
            }
            return list;
        }

        private static CreationOption MakeTrittys(TribeSeed tribe, string key, string kind, string deme, string body)
        {
            return new CreationOption
            {
                id = $"trittys_{tribe.Id.Substring("tribe_".Length)}_{key}",
                parentId = tribe.Id,
                title = $"{kind} — {deme}",
                subtitle = $"{tribe.Name}",
                description = $"{body}\n\nΔήμος: {deme}. Φυλή: {tribe.Name}.",
                artwork = LoadBgOrFallback($"BG_Trittys_{key}_{deme}")
            };
        }

        private static List<CreationOption> BuildWealthClasses() => new List<CreationOption>
        {
            new CreationOption
            {
                id = "wealth_pentakosiomedimnoi",
                title = "Πεντακοσιομέδιμνος",
                subtitle = "500+ μέδιμνοι ετησίως",
                description =
                    "Η ανώτατη τιμηματική τάξη του Σόλωνος. Από εδώ βγαίνουν οι ταμίες και οι " +
                    "μεγάλοι χορηγοί. Ο πλούτος σου είναι επίσης βάρος: η πόλις θα σου ζητήσει " +
                    "λειτουργίες — τριηραρχίες, χορηγίες — και όλοι θα βλέπουν αν τις αποφεύγεις.",
                artwork = LoadCharOrFallback("CH_Wealth_Pentakosio")
            },
            new CreationOption
            {
                id = "wealth_hippeis",
                title = "Ιππεύς",
                subtitle = "300–500 μέδιμνοι",
                description =
                    "Μπορείς να συντηρήσεις ίππο — και να υπηρετήσεις στο ιππικό. Ευκατάστατος " +
                    "χωρίς να είσαι των πρώτων οίκων· αρκετά κοντά στην εξουσία για να σε αφορά, " +
                    "αρκετά μακριά για να σε εκθέτει λιγότερο.",
                artwork = LoadCharOrFallback("CH_Wealth_Hippeus")
            },
            new CreationOption
            {
                id = "wealth_zeugitai",
                title = "Ζευγίτης",
                subtitle = "200–300 μέδιμνοι",
                description =
                    "Η τάξη των οπλιτών — όσοι μπορούν να αγοράσουν πανοπλία και να σταθούν στη " +
                    "φάλαγγα. Η ραχοκοκκαλιά του αθηναϊκού στρατού και, όλο και περισσότερο, " +
                    "της πολιτικής ζωής.",
                artwork = LoadCharOrFallback("CH_Wealth_Zeugites")
            },
            new CreationOption
            {
                id = "wealth_thetes",
                title = "Θης",
                subtitle = "Κάτω από 200 μεδίμνους",
                description =
                    "Εργάτης, ναύτης, μισθωτός. Δεν κατέχεις γη πού να σε θρέψει, αλλά κωπηλατείς " +
                    "στις τριήρεις — και ο στόλος είναι η πραγματική δύναμη της Αθήνας. Η ψήφος " +
                    "σου μετρά όσο και κάθε άλλη· ο χρόνος σου όμως κοστίζει.",
                artwork = LoadCharOrFallback("CH_Wealth_Thes")
            },
        };

        private static List<CreationOption> BuildPeriods() => new List<CreationOption>
        {
            new CreationOption
            {
                id = "period_kleisthenes",
                title = "Η Κλεισθένειος μεταρρύθμισις",
                subtitle = "508/7 – 491 π.Χ.",
                description =
                    "Η δημοκρατία μόλις γεννήθηκε. Οι δέκα φυλές είναι καινούργιες, η Βουλή των " +
                    "Πεντακοσίων ακόμη μαθαίνει τη δουλειά της, και κανείς δεν είναι σίγουρος αν " +
                    "το πείραμα θα αντέξει.",
                artwork = LoadBgOrFallback("BG_Period_Kleisthenes")
            },
            new CreationOption
            {
                id = "period_persian_wars",
                title = "Οι Περσικοί πόλεμοι",
                subtitle = "490 – 479 π.Χ.",
                description =
                    "Μαραθών, Σαλαμίς, Πλαταιαί. Η πόλις επιβιώνει του μεγαλύτερου κινδύνου της " +
                    "και ανακαλύπτει ότι τη σώζουν οι κωπηλάτες όσο και οι οπλίτες. Η ισορροπία " +
                    "της εξουσίας δεν θα ξαναγίνει η ίδια.",
                artwork = LoadBgOrFallback("BG_Period_PersianWars")
            },
            new CreationOption
            {
                id = "period_pentekontaetia",
                title = "Η Πεντηκονταετία",
                subtitle = "478 – 432 π.Χ.",
                description =
                    "Ο χρυσός αιών. Ο Παρθενών ανεγείρεται, ο Περικλής κυριαρχεί στην Πνύκα, " +
                    "και η Δηλιακή συμμαχία γίνεται σιωπηλά αθηναϊκή αρχή. Η δημοκρατία στο " +
                    "απόγειό της — και με αυτοκρατορία στα χέρια.",
                artwork = LoadBgOrFallback("BG_Period_Pentekontaetia")
            },
            new CreationOption
            {
                id = "period_peloponnesian",
                title = "Ο Πελοποννησιακός πόλεμος",
                subtitle = "431 – 404 π.Χ.",
                description =
                    "Λοιμός, Σικελία, στάσεις, και στο τέλος η ήττα. Η Εκκλησία παίρνει αποφάσεις " +
                    "υπό πίεση και τις αναιρεί την επομένη. Η δημοκρατία δοκιμάζεται στα όριά της.",
                artwork = LoadBgOrFallback("BG_Period_Peloponnesian")
            },
            new CreationOption
            {
                id = "period_restoration",
                title = "Η αποκατάστασις",
                subtitle = "403 – 338 π.Χ.",
                description =
                    "Μετά τους Τριάκοντα, η πόλις ψηφίζει αμνηστία και ξαναχτίζει. Πιο προσεκτική, " +
                    "πιο νομικίστικη, πιο γραφειοκρατική — και σε αυτή την περίοδο δικάζεται " +
                    "ο Σωκράτης.",
                artwork = LoadBgOrFallback("BG_Period_Restoration")
            },
        };

        private static List<CreationOption> BuildOccupations() => new List<CreationOption>
        {
            Occupation("occ_georgos", "Γεωργός", "Η γη και οι εποχές",
                "Ελιές, κριθάρι, αμπέλι. Η πολιτική σου ζωή υποτάσσεται στο ημερολόγιο της " +
                "συγκομιδής — και όταν ο πόλεμος καίει τα χωράφια, το πληρώνεις πρώτος."),

            Occupation("occ_kerameus", "Κεραμεύς", "Ο τροχός και ο κλίβανος",
                "Αμφορείς για λάδι, κρατήρες για συμπόσια, ληκύθους για νεκρούς. Το εργαστήριό " +
                "σου στον Κεραμεικό ακούει όλες τις φήμες της πόλεως πριν φτάσουν στην Αγορά."),

            Occupation("occ_emporos", "Έμπορος", "Σιτάρι, ασήμι, χρέη",
                "Το σιτάρι του Ευξείνου κρατά την Αθήνα ζωντανή, και εσύ το φέρνεις. Ξέρεις τιμές " +
                "πού η Εκκλησία αγνοεί — και αυτό σε κάνει χρήσιμο και ύποπτο ταυτόχρονα."),

            Occupation("occ_nautes", "Ναύτης", "Η κώπη και το κύμα",
                "Κωπηλατείς στις τριήρεις. Η δύναμη της Αθήνας περνά κυριολεκτικά από τα χέρια " +
                "σου, και το ξέρεις — όπως το ξέρουν και όσοι φοβούνται τον δήμο."),

            Occupation("occ_hoplites", "Οπλίτης", "Ασπίς και φάλαγξ",
                "Πλήρωσες μόνος σου την πανοπλία σου και στέκεσαι στη γραμμή. Η αξιοπρέπειά σου " +
                "στηρίζεται στο ότι κρατάς τη θέση σου — στη μάχη και στην Πνύκα."),

            Occupation("occ_lithoxoos", "Λιθοξόος", "Μάρμαρο και επιγραφές",
                "Χαράσσεις τα ψηφίσματα πού η Εκκλησία εγκρίνει. Κανείς δεν διαβάζει τους νόμους " +
                "τόσο προσεκτικά όσο αυτός πού πρέπει να τους σκαλίσει σε πέτρα."),

            Occupation("occ_rhetor", "Ρήτωρ", "Ο λόγος ως όπλο",
                "Ζεις από το να πείθεις — στο βήμα, στο δικαστήριο, στα παρασκήνια. Η Αθήνα " +
                "λατρεύει τον καλό ρήτορα και δεν εμπιστεύεται κανέναν."),

            Occupation("occ_iatros", "Ιατρός", "Το σώμα και ο λοιμός",
                "Σε καλούν σε σπίτια όλων των τάξεων. Βλέπεις την πόλη από μέσα — ποιος πεινά, " +
                "ποιος αρρωσταίνει, ποιος κρύβει τι."),

            Occupation("occ_trapezites", "Τραπεζίτης", "Δάνεια και εγγυήσεις",
                "Κρατάς καταθέσεις και δανείζεις με τόκο, συχνά ως μέτοικος ή απελεύθερος. " +
                "Γνωρίζεις τα χρέη μισής πόλεως — η πιο επικίνδυνη γνώση πού υπάρχει."),

            Occupation("occ_skytotomos", "Σκυτοτόμος", "Δέρμα και υποδήματα",
                "Χειρωνακτική τέχνη πού οι ευγενείς περιφρονούν και όλοι χρειάζονται. Το " +
                "εργαστήρι σου είναι τόπος όπου οι άνθρωποι μιλούν ελεύθερα."),
        };

        private static CreationOption Occupation(string id, string title, string subtitle, string description) =>
            new CreationOption
            {
                id = id,
                title = title,
                subtitle = subtitle,
                description = description,
                artwork = LoadCharOrFallback($"CH_{char.ToUpperInvariant(id[4])}{id.Substring(5)}")
            };
    }
}
#endif
