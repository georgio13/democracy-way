#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DemocracyWay.EditorTools
{
    using DemocracyWay.Core;
    using DemocracyWay.Dialogue;
    using DemocracyWay.UI;

    /// <summary>
    /// Seeds the starter dialogue pool and the intro comic.
    ///
    /// These eight dialogues exist to exercise the loop — draw one, read it,
    /// choose, watch the five indicators move. Add more by editing
    /// <c>Assets/Content/DialogueDatabase.asset</c> in the Inspector; nothing
    /// here overwrites it unless Init is re-run.
    /// </summary>
    public static partial class DemocracyWayInit
    {
        private static DialogueDatabase BuildDialogueDatabase()
        {
            // Built before the asset is loaded, and flushed straight after —
            // see BuildCreationDatabase for why holding a ScriptableObject
            // across anything that can import is not safe.
            var entries = BuildStarterDialogues();

            EnsureFolder(ContentFolder);

            var db = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(DialogueDbPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<DialogueDatabase>();
                AssetDatabase.CreateAsset(db, DialogueDbPath);
            }

            db.EditorSetEntries(entries);
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Init] DialogueDatabase: {db.Count} διάλογοι.");
            return db;
        }

        // ── Tiny builders so the dialogue data below stays readable ──

        private static DialogueLine Line(string speaker, string text) =>
            new DialogueLine { speaker = speaker, text = text };

        private static DialogueLine Narration(string text) =>
            new DialogueLine { speaker = null, text = text };

        private static IndicatorDelta D(IndicatorType type, int delta) =>
            new IndicatorDelta { indicator = type, delta = delta };

        private static DialogueChoice Choice(string text, string outcome, params IndicatorDelta[] effects) =>
            new DialogueChoice
            {
                text = text,
                outcome = outcome,
                effects = new List<IndicatorDelta>(effects)
            };

        private static List<DialogueEntry> BuildStarterDialogues() => new List<DialogueEntry>
        {
            new DialogueEntry
            {
                id = "dlg_ekklesia_stolos",
                title = "Ψήφισμα για τον στόλο",
                lines = new List<DialogueLine>
                {
                    Narration("Η Πνύκα γεμίζει πριν από την αυγή. Ο κήρυξ ανεβαίνει στο βήμα."),
                    Line("Κήρυξ", "Τις αγορεύειν βούλεται; Το θέμα: είκοσι νέες τριήρεις, και ο φόρος πού τις πληρώνει."),
                    Line("Ρήτωρ", "Χωρίς στόλο δεν υπάρχει Αθήνα. Όποιος διστάζει σήμερα, θα κωπηλατεί αύριο για άλλον."),
                    Line("Γεωργός πλαι σου", "Εύκολα μιλά. Δεν είναι δικά του χωράφια αυτά πού θα φορολογηθούν."),
                },
                choices = new List<DialogueChoice>
                {
                    Choice("Υπερψηφίζεις τον στόλο.",
                        "Σηκώνεις το χέρι. Το ψήφισμα περνά άνετα — και όσοι θα πληρώσουν το σημειώνουν.",
                        D(IndicatorType.Eunomia, 6), D(IndicatorType.Demos, 4), D(IndicatorType.Oikos, -5)),

                    Choice("Ζητάς να βαρύνει τους πλουσιωτέρους.",
                        "Η πρότασή σου ακούγεται. Ο δήμος επευφημεί· τρεις άνδρες στην πρώτη σειρά όχι.",
                        D(IndicatorType.Demos, 9), D(IndicatorType.Ethos, 4), D(IndicatorType.Kachypopsia, 7)),

                    Choice("Σιωπάς και ψηφίζεις με την πλειοψηφία.",
                        "Κανείς δεν θυμάται τι ψήφισες. Ούτε κι εσύ είσαι σίγουρος.",
                        D(IndicatorType.Eunomia, 2), D(IndicatorType.Ethos, -3)),
                }
            },

            new DialogueEntry
            {
                id = "dlg_geiton_katigoria",
                title = "Ο γείτων κατηγορείται",
                lines = new List<DialogueLine>
                {
                    Narration("Ο Δημέας χτυπά την πόρτα σου πριν νυχτώσει. Τον κατηγορούν για ασέβεια."),
                    Line("Δημέας", "Δεν έκανα τίποτα. Ο Κλέων με θέλει εκτός — του χρωστώ, και το ξέρει όλη η φυλή."),
                    Line("Δημέας", "Χρειάζομαι κάποιον να καταθέσει ότι ήμουν στο χωράφι εκείνη τη μέρα."),
                    Narration("Ήσουν. Τον είδες. Αλλά ο Κλέων έχει μακρύ χέρι στην Ηλιαία."),
                },
                choices = new List<DialogueChoice>
                {
                    Choice("Καταθέτεις υπέρ αυτού.",
                        "Στέκεσαι στο δικαστήριο και λες την αλήθεια. Ο Δημέας αθωώνεται· ο Κλέων σε κοιτάζει καθώς φεύγεις.",
                        D(IndicatorType.Ethos, 10), D(IndicatorType.Demos, 3), D(IndicatorType.Kachypopsia, 8)),

                    Choice("Αρνείσαι. Δεν είναι ο αγώνας σου.",
                        "Ο Δημέας φεύγει χωρίς να πει λέξη. Καταδικάζεται. Ο οίκος σου παραμένει ανενόχλητος.",
                        D(IndicatorType.Ethos, -9), D(IndicatorType.Oikos, 4), D(IndicatorType.Kachypopsia, -3)),

                    Choice("Προτείνεις να πληρώσεις το χρέος του στον Κλέωνα.",
                        "Η κατηγορία αποσύρεται ήσυχα. Κανείς δεν λέει τίποτα — και όλοι καταλαβαίνουν.",
                        D(IndicatorType.Oikos, -8), D(IndicatorType.Eunomia, -5), D(IndicatorType.Demos, 5)),
                }
            },

            new DialogueEntry
            {
                id = "dlg_leitourgia",
                title = "Η λειτουργία",
                lines = new List<DialogueLine>
                {
                    Narration("Οι στρατηγοί ανακοίνωσαν τους φετινούς τριηράρχους. Το όνομά σου είναι στον κατάλογο."),
                    Line("Γραμματεύς", "Μια τριήρης, ένα έτος. Πλήρωμα, επισκευές, εξοπλισμός. Η πόλις δίνει το σκάφος."),
                    Line("Γραμματεύς", "Ή μπορείς να προκαλέσεις σε αντίδοσιν όποιον θεωρείς πλουσιώτερο."),
                },
                choices = new List<DialogueChoice>
                {
                    Choice("Αναλαμβάνεις — και μάλιστα με φιλοτιμία.",
                        "Η τριήρης σου είναι η ταχύτερη του στόλου. Το κόστος σε πονά για χρόνια.",
                        D(IndicatorType.Demos, 12), D(IndicatorType.Ethos, 6), D(IndicatorType.Oikos, -14)),

                    Choice("Προκαλείς σε αντίδοσιν τον Νικία.",
                        "Ο Νικίας υποχωρεί και αναλαμβάνει. Κέρδισες — και απέκτησες εχθρό με μνήμη.",
                        D(IndicatorType.Oikos, 3), D(IndicatorType.Kachypopsia, 11), D(IndicatorType.Demos, -4)),

                    Choice("Αναλαμβάνεις, αλλά κόβεις όπου δεν φαίνεται.",
                        "Η τριήρης πλέει. Ο κυβερνήτης παραπονείται για τα σχοινιά — σε κάποιον άλλον.",
                        D(IndicatorType.Oikos, -5), D(IndicatorType.Ethos, -6), D(IndicatorType.Demos, 4)),
                }
            },

            new DialogueEntry
            {
                id = "dlg_klerosis_boule",
                title = "Η κλήρωσις",
                lines = new List<DialogueLine>
                {
                    Narration("Το κληρωτήριον σταματά. Ο λευκός κύβος πέφτει. Το πινάκιόν σου είναι στη σειρά."),
                    Line("Άρχων", "Βουλευτής για το έτος. Παρουσιάζεσαι αύριο για δοκιμασίαν."),
                    Narration("Πεντακόσιοι άνδρες, ένας χρόνος, και η υποχρέωση να δώσεις ευθύνας στο τέλος."),
                },
                choices = new List<DialogueChoice>
                {
                    Choice("Δέχεσαι με σοβαρότητα.",
                        "Παρουσιάζεσαι στην ώρα σου. Η δοκιμασία περνά χωρίς ερωτήσεις — αυτό κιόλας είναι σπάνιο.",
                        D(IndicatorType.Eunomia, 8), D(IndicatorType.Ethos, 5), D(IndicatorType.Oikos, -4)),

                    Choice("Δέχεσαι, βλέποντας τις ευκαιρίες.",
                        "Μέσα σε έναν μήνα ξέρεις ποιος οφείλει χάρη σε ποιον. Η γνώση είναι χρήσιμη και βαριά.",
                        D(IndicatorType.Demos, 7), D(IndicatorType.Oikos, 6), D(IndicatorType.Ethos, -7), D(IndicatorType.Kachypopsia, 5)),

                    Choice("Ζητάς απαλλαγή για λόγους υγείας.",
                        "Σε πιστεύουν. Ο επόμενος κληρώνεται. Κάποιοι σημειώνουν ότι δεν ήσουν άρρωστος την επομένη.",
                        D(IndicatorType.Eunomia, -4), D(IndicatorType.Demos, -6), D(IndicatorType.Oikos, 5)),
                }
            },

            new DialogueEntry
            {
                id = "dlg_ostrakismos",
                title = "Τα όστρακα",
                lines = new List<DialogueLine>
                {
                    Narration("Η Αγορά έχει περιφραχθεί. Δέκα είσοδοι, μια για κάθε φυλή. Κρατάς ένα κομμάτι πηλού."),
                    Line("Αγορανόμος", "Ένα όνομα. Όποιο θέλεις. Αν συγκεντρωθούν έξι χιλιάδες όστρακα, ο πρώτος φεύγει για δέκα χρόνια."),
                    Line("Αγράμματος γέρων", "Ξένε, γράψε μου εσύ ένα όνομα. Δεν ξέρω γράμματα."),
                    Line("Αγράμματος γέρων", "Γράψε «Αριστείδης». Με κούρασε πού τον λένε όλοι Δίκαιο."),
                },
                choices = new List<DialogueChoice>
                {
                    Choice("Γράφεις ό,τι σου ζήτησε.",
                        "Του δίνεις το όστρακο. Δεν μαθαίνεις ποτέ αν κατάλαβε τι έκανε.",
                        D(IndicatorType.Demos, 3), D(IndicatorType.Ethos, -5), D(IndicatorType.Eunomia, -3)),

                    Choice("Του εξηγείς πρώτα ποιος είναι ο Αριστείδης.",
                        "Ο γέρων σε κοιτάζει, ανασηκώνει τους ώμους, και ζητά το ίδιο όνομα. Του το γράφεις.",
                        D(IndicatorType.Ethos, 7), D(IndicatorType.Eunomia, 4), D(IndicatorType.Demos, -2)),

                    Choice("Αρνείσαι και αφήνεις το δικό σου όστρακο λευκό.",
                        "Δύο όστρακα λιγώτερα. Η διαδικασία δεν σε χρειάζεται — και αυτό σε ενοχλεί περισσότερο απ' όσο περίμενες.",
                        D(IndicatorType.Ethos, 4), D(IndicatorType.Demos, -5), D(IndicatorType.Kachypopsia, -4)),
                }
            },

            new DialogueEntry
            {
                id = "dlg_sitodeia",
                title = "Σιτοδεία",
                lines = new List<DialogueLine>
                {
                    Narration("Τα πλοία από τον Εύξεινο άργησαν. Στην Αγορά η τιμή του σίτου τριπλασιάστηκε σε τρεις ημέρες."),
                    Line("Σιτοπώλης", "Δεν φταίω εγώ. Αγόρασα ακριβά, πουλώ ακριβά."),
                    Line("Γυνή στη σειρά", "Έχει τρεις αποθήκες γεμάτες. Το ξέρουν όλοι στον Πειραιά."),
                },
                choices = new List<DialogueChoice>
                {
                    Choice("Τον καταγγέλλεις στους σιτοφύλακας.",
                        "Οι αποθήκες ανοίγουν. Η τιμή πέφτει. Τρεις έμποροι μαθαίνουν το όνομά σου.",
                        D(IndicatorType.Eunomia, 9), D(IndicatorType.Demos, 8), D(IndicatorType.Kachypopsia, 9)),

                    Choice("Αγοράζεις όσο μπορείς πριν ανέβει άλλο.",
                        "Ο οίκος σου έχει σιτάρι για τον μήνα. Η ουρά πίσω σου είναι ακόμη εκεί.",
                        D(IndicatorType.Oikos, 8), D(IndicatorType.Ethos, -6), D(IndicatorType.Demos, -4)),

                    Choice("Μοιράζεσαι ό,τι έχεις με τους γείτονες.",
                        "Δεν αρκεί για όλους, αλλά φτάνει για να το θυμούνται.",
                        D(IndicatorType.Demos, 11), D(IndicatorType.Ethos, 8), D(IndicatorType.Oikos, -9)),
                }
            },

            new DialogueEntry
            {
                id = "dlg_euthyna",
                title = "Εύθυναι",
                lines = new List<DialogueLine>
                {
                    Narration("Ο ταμίας της φυλής δίνει λογαριασμό για το έτος. Οι λογισταί διαβάζουν φωναχτά."),
                    Line("Λογιστής", "Εξήντα δραχμές για θυσίες. Η απόδειξις;"),
                    Line("Ταμίας", "Χάθηκε. Αλλά όλη η φυλή έφαγε από εκείνη τη θυσία."),
                    Narration("Έφαγες κι εσύ. Και όμως εξήντα δραχμές είναι πολλές για ένα βόδι."),
                },
                choices = new List<DialogueChoice>
                {
                    Choice("Επιμένεις να βρεθεί απόδειξις.",
                        "Ο ταμίας καταδικάζεται σε διπλή αποζημίωση. Η φυλή διχάζεται για μήνες.",
                        D(IndicatorType.Eunomia, 11), D(IndicatorType.Ethos, 6), D(IndicatorType.Demos, -7), D(IndicatorType.Kachypopsia, 6)),

                    Choice("Δέχεσαι την εξήγηση. Όλοι έφαγαν.",
                        "Οι λογαριασμοί εγκρίνονται. Του χρόνου ο επόμενος ταμίας θα χάσει κι αυτός αποδείξεις.",
                        D(IndicatorType.Eunomia, -8), D(IndicatorType.Demos, 5), D(IndicatorType.Ethos, -4)),

                    Choice("Ζητάς να επιστρέψει τη διαφορά χωρίς δίκη.",
                        "Ο ταμίας πληρώνει σιωπηλά. Δεν καταγράφεται πουθενά — πού είναι ακριβώς το πρόβλημα.",
                        D(IndicatorType.Eunomia, 3), D(IndicatorType.Demos, 4), D(IndicatorType.Kachypopsia, -3)),
                }
            },

            new DialogueEntry
            {
                id = "dlg_metoikos",
                title = "Ο μέτοικος",
                lines = new List<DialogueLine>
                {
                    Narration("Ο Κηφισόδωρος εργάζεται στην Αθήνα είκοσι χρόνια. Πληρώνει μετοίκιον. Δεν ψηφίζει."),
                    Line("Κηφισόδωρος", "Ο γιος μου γεννήθηκε εδώ. Πολέμησε στη Σαλαμίνα. Δεν είναι Αθηναίος."),
                    Line("Κηφισόδωρος", "Ζητώ κάποιον πολίτη να με προστατεύσει στη δίκη μου. Δεν μπορώ να σταθώ μόνος."),
                },
                choices = new List<DialogueChoice>
                {
                    Choice("Γίνεσαι προστάτης του.",
                        "Στέκεσαι μαζί του. Κερδίζει. Κάποιοι ρωτούν γιατί ενδιαφέρθηκες για ξένον.",
                        D(IndicatorType.Ethos, 9), D(IndicatorType.Kachypopsia, 6), D(IndicatorType.Demos, -3)),

                    Choice("Τον παραπέμπεις σε κάποιον άλλον.",
                        "Βρίσκει προστάτη. Πληρώνει γι' αυτόν. Δεν σου ξαναμιλά.",
                        D(IndicatorType.Ethos, -4), D(IndicatorType.Oikos, 2)),

                    Choice("Προτείνεις στην Εκκλησία πολιτογράφηση του γιου του.",
                        "Η πρόταση καταψηφίζεται συντριπτικά. Αλλά έγινε — και κάποιοι τη θυμούνται.",
                        D(IndicatorType.Ethos, 12), D(IndicatorType.Demos, -9), D(IndicatorType.Kachypopsia, 8), D(IndicatorType.Eunomia, -3)),
                }
            },
        };

        // ═════════════════════════════════════════════════════════════════════
        // INTRO COMIC
        // ═════════════════════════════════════════════════════════════════════

        private static ComicSequence BuildComicSequence()
        {
            var panels = BuildComicPanels();

            EnsureFolder(ContentFolder);

            var seq = AssetDatabase.LoadAssetAtPath<ComicSequence>(ComicSequencePath);
            if (seq == null)
            {
                seq = ScriptableObject.CreateInstance<ComicSequence>();
                AssetDatabase.CreateAsset(seq, ComicSequencePath);
            }

            seq.title = "Πριν από την πρώτη πρυτανεία";
            seq.fadeDuration = 0.7f;
            seq.gapBetweenPanels = 0.55f;
            seq.panels = panels;

            EditorUtility.SetDirty(seq);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Init] IntroComic: {seq.panels.Count} καρέ.");
            return seq;
        }

        /// <summary>The intro comic frames. Split out from BuildComicSequence
        /// so PregenerateArtwork can trigger their placeholder PNGs without
        /// creating or touching the ComicSequence asset.</summary>
        private static List<ComicPanel> BuildComicPanels() => new List<ComicPanel>
        {
            new ComicPanel
            {
                artwork = LoadBgOrFallback("BG_Comic_01_Dawn"),
                caption = "Ξυπνάς πριν από τον ήλιο. Σήμερα η πόλις κληρώνει."
            },
            new ComicPanel
            {
                artwork = LoadBgOrFallback("BG_Comic_02_Road"),
                caption = "Ο δρόμος προς το άστυ γεμίζει ανθρώπους πού δεν γνωρίζεις — και πού τώρα είναι η φυλή σου."
            },
            new ComicPanel
            {
                artwork = LoadBgOrFallback("BG_Comic_03_Agora"),
                caption = "Στην Αγορά, δέκα ονόματα σε δέκα πινακίδες. Ένα από αυτά θα κυβερνά τον επόμενο μήνα."
            },
            new ComicPanel
            {
                artwork = LoadBgOrFallback("BG_Comic_04_Kleroterion"),
                caption = "Το κληρωτήριον δεν ρωτά ποιος είσαι. Ούτε ποιον γνωρίζεις."
            },
            new ComicPanel
            {
                artwork = LoadBgOrFallback("BG_Comic_05_Pnyx"),
                caption = "Η Πνύκα περιμένει. Δέκα πρυτανείες ως το τέλος του έτους."
            },
            new ComicPanel
            {
                artwork = LoadBgOrFallback("BG_Comic_06_You"),
                caption = "Και εσύ είσαι εκεί. Αυτό αρκεί για να αρχίσει."
            }
        };
    }
}
#endif
