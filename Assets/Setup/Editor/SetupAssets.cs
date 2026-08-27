#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DemocracyWay.Core;
using DemocracyWay.Data;

namespace DemocracyWay.Setup
{
    /// <summary>
    /// Phase 1 of the one-shot Setup: importer settings for the placeholder
    /// art (they must be right BEFORE anything references the sprites) and the
    /// six Data assets under Assets/Data/ — created once with sample Greek
    /// content and then owned by the author forever. Sample entries the author
    /// is expected to replace carry the «ΠΑΡΑΔΕΙΓΜΑ: » prefix on their title;
    /// structurally final entries (the two genders, the four Solonian classes)
    /// do not.
    /// </summary>
    internal static class SetupAssets
    {
        // ═════════════════════════════════════════════════════════════════════
        // IMPORTERS
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Applies each texture's required import settings, re-importing only
        /// when something actually differs — re-running Setup must not churn
        /// user assets. Also creates the additive Firefly particle material.
        /// </summary>
        public static void ConfigureImporters()
        {
            // UI/world sprites (Sprite (2D and UI), single, transparent alpha).
            EnsureSpriteImport(SetupPaths.AthenaStatuePng);
            EnsureSpriteImport(SetupPaths.FireflyPng);
            EnsureSpriteImport(SetupPaths.BgChapter01Png);
            EnsureSpriteImport(SetupPaths.HeroPortraitPng);
            EnsureSpriteImport(SetupPaths.Comic1Png);
            EnsureSpriteImport(SetupPaths.Comic2Png);
            EnsureSpriteImport(SetupPaths.Comic3Png);

            // Cursor: its own texture type so Cursor.SetCursor accepts it.
            EnsureCursorImport(SetupPaths.CursorPng);

            // Smoke: stays a plain texture (RawImage uses it directly) and MUST
            // wrap-repeat, otherwise the uvRect scroll smears the edge pixels.
            EnsureSmokeImport(SetupPaths.MenuSmokePng);

            CreateFireflyMaterial();
        }

        private static TextureImporter GetImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                Debug.LogError($"[Setup] Δεν βρέθηκε TextureImporter για: {path} — λείπει το placeholder;");
            return importer;
        }

        private static void EnsureSpriteImport(string path)
        {
            var importer = GetImporter(path);
            if (importer == null) return;
            if (importer.textureType == TextureImporterType.Sprite &&
                importer.spriteImportMode == SpriteImportMode.Single &&
                importer.alphaIsTransparency)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            Debug.Log($"[Setup] Importer → Sprite: {path}");
        }

        private static void EnsureCursorImport(string path)
        {
            var importer = GetImporter(path);
            if (importer == null) return;
            if (importer.textureType == TextureImporterType.Cursor && importer.alphaIsTransparency)
                return;

            importer.textureType = TextureImporterType.Cursor;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            Debug.Log($"[Setup] Importer → Cursor: {path}");
        }

        private static void EnsureSmokeImport(string path)
        {
            var importer = GetImporter(path);
            if (importer == null) return;
            if (importer.textureType == TextureImporterType.Default &&
                importer.wrapMode == TextureWrapMode.Repeat &&
                importer.alphaIsTransparency)
                return;

            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;   // uvRect scrolling loops through this
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            Debug.Log($"[Setup] Importer → Default/Repeat: {path}");
        }

        /// <summary>
        /// Additive unlit particle material for the menu fireflies. Built-in
        /// render pipeline (verified — no SRP package installed), so the
        /// Standard Particles Unlit shader is present; the blend state is set
        /// by hand because the shader GUI that normally does it never runs for
        /// materials created from code.
        /// </summary>
        private static void CreateFireflyMaterial()
        {
            if (SetupCommon.SkipIfExists(SetupPaths.FireflyMat)) return;

            var shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
            {
                Debug.LogError("[Setup] Δεν βρέθηκε shader 'Particles/Standard Unlit' — το Firefly.mat δεν δημιουργήθηκε.");
                return;
            }

            var mat = new Material(shader);
            var texture = SetupCommon.Load<Texture2D>(SetupPaths.FireflyPng);
            if (texture != null) mat.SetTexture("_MainTex", texture);

            // Rendering mode: Additive (what StandardParticlesShaderGUI would set).
            mat.SetFloat("_Mode", 4f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            AssetDatabase.CreateAsset(mat, SetupPaths.FireflyMat);
            SetupCommon.MarkCreated(SetupPaths.FireflyMat);
        }

        // ═════════════════════════════════════════════════════════════════════
        // DATA ASSETS
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates the six Data assets. Order matters only for references:
        /// the dialogue before its chapter, everything before the GameConfig
        /// that points at all of it. Fields are set directly (they are public
        /// by design on Data types); SerializedObject is reserved for the
        /// [SerializeField] wiring of scene/prefab components.
        /// </summary>
        public static void CreateDataAssets()
        {
            CreateCreationDatabase();
            CreateIndicatorCatalog();
            CreateIntroComic();
            CreateChapter01Dialogue();
            CreateChapter01();
            CreateGameConfig();
        }

        private static void CreateAsset(ScriptableObject asset, string path)
        {
            AssetDatabase.CreateAsset(asset, path);
            EditorUtility.SetDirty(asset);
            SetupCommon.MarkCreated(path);
        }

        // ── CreationDatabase ──

        private static void CreateCreationDatabase()
        {
            if (SetupCommon.SkipIfExists(SetupPaths.CreationDbAsset)) return;

            var hero = SetupCommon.Load<Sprite>(SetupPaths.HeroPortraitPng);
            var db = ScriptableObject.CreateInstance<CreationDatabase>();

            // Βήμα 1 — Φύλο (structural: exactly these two, no ΠΑΡΑΔΕΙΓΜΑ prefix).
            db.genders = new List<GenderOption>
            {
                new GenderOption
                {
                    id = "aner",
                    title = "Ανήρ",
                    description = "Ελεύθερος Αθηναίος πολίτης με πλήρη πολιτικά δικαιώματα: " +
                                  "συμμετέχεις στην εκκλησία του δήμου, στα δικαστήρια και στις αρχές. " +
                                  "Ο δρόμος σου είναι ανοιχτός — αλλά και γεμάτος ανταγωνιστές.",
                    image = hero
                },
                new GenderOption
                {
                    id = "gyne",
                    title = "Γυνή",
                    description = "Στην κλασική Αθήνα οι γυναίκες δεν είχαν πολιτικά δικαιώματα. " +
                                  "Για να περπατήσεις την Οδό της Δημοκρατίας θα κρυφτείς πίσω από άλλο πρόσωπο — " +
                                  "και ο δείκτης Καχυποψία θα μετρά διαρκώς πόσο κοντά είσαι στην αποκάλυψη.",
                    image = hero,
                    enablesSuspicion = true
                }
            };

            // Βήμα 2 — Φυλή (δείγματα: 3 από τις 10 φυλές του Κλεισθένη).
            db.tribes = new List<CreationOption>
            {
                new CreationOption
                {
                    id = "erechtheis",
                    title = "ΠΑΡΑΔΕΙΓΜΑ: Ερεχθηΐς",
                    description = "Η πρώτη από τις δέκα φυλές του Κλεισθένη, με επώνυμο ήρωα " +
                                  "τον μυθικό βασιλιά Ερεχθέα. Οι δήμοι της απλώνονται από την πόλη ως τη θάλασσα.",
                    image = hero
                },
                new CreationOption
                {
                    id = "aigeis",
                    title = "ΠΑΡΑΔΕΙΓΜΑ: Αιγηΐς",
                    description = "Φυλή με επώνυμο ήρωα τον Αιγέα, πατέρα του Θησέα. " +
                                  "Περιλαμβάνει δήμους της πόλης και της μεσογαίας.",
                    image = hero
                },
                new CreationOption
                {
                    id = "pandionis",
                    title = "ΠΑΡΑΔΕΙΓΜΑ: Πανδιονίς",
                    description = "Φυλή με επώνυμο ήρωα τον Πανδίονα. Οι δήμοι της κρατούν " +
                                  "γερούς δεσμούς με τα λιμάνια και το εμπόριο.",
                    image = hero
                }
            };

            // Βήμα 3 — Τριττύες (2 ανά φυλή, φιλτράρονται από το tribeId).
            db.trittyes = new List<TrittysOption>
            {
                new TrittysOption
                {
                    id = "erechtheis_asty", tribeId = "erechtheis",
                    title = "ΠΑΡΑΔΕΙΓΜΑ: Άστυ Ερεχθηΐδος",
                    description = "Οι δήμοι της Ερεχθηΐδας μέσα στα τείχη: εργαστήρια, αγορά, " +
                                  "και η εκκλησία δυο βήματα από το σπίτι σου.",
                    image = hero
                },
                new TrittysOption
                {
                    id = "erechtheis_paralia", tribeId = "erechtheis",
                    title = "ΠΑΡΑΔΕΙΓΜΑ: Παραλία Ερεχθηΐδος",
                    description = "Οι παραθαλάσσιοι δήμοι της Ερεχθηΐδας: ψαράδες, ναυτικοί " +
                                  "και το αλάτι του Σαρωνικού στον αέρα.",
                    image = hero
                },
                new TrittysOption
                {
                    id = "aigeis_asty", tribeId = "aigeis",
                    title = "ΠΑΡΑΔΕΙΓΜΑ: Άστυ Αιγηΐδος",
                    description = "Οι αστικοί δήμοι της Αιγηΐδας, κολλητά στον Κεραμεικό — " +
                                  "εκεί όπου η πολιτική συζητιέται στα εργαστήρια των αγγειοπλαστών.",
                    image = hero
                },
                new TrittysOption
                {
                    id = "aigeis_mesogeia", tribeId = "aigeis",
                    title = "ΠΑΡΑΔΕΙΓΜΑ: Μεσογαία Αιγηΐδος",
                    description = "Οι αγροτικοί δήμοι της Αιγηΐδας στη μεσογαία: ελιές, αμπέλια " +
                                  "και δρόμος μιας μέρας ως την Πνύκα.",
                    image = hero
                },
                new TrittysOption
                {
                    id = "pandionis_asty", tribeId = "pandionis",
                    title = "ΠΑΡΑΔΕΙΓΜΑ: Άστυ Πανδιονίδος",
                    description = "Οι αστικοί δήμοι της Πανδιονίδας, ανάμεσα στην Ακρόπολη " +
                                  "και την αγορά — στο κέντρο των πάντων.",
                    image = hero
                },
                new TrittysOption
                {
                    id = "pandionis_paralia", tribeId = "pandionis",
                    title = "ΠΑΡΑΔΕΙΓΜΑ: Παραλία Πανδιονίδος",
                    description = "Οι παράλιοι δήμοι της Πανδιονίδας: καΐκια, πορθμεία " +
                                  "και τα νέα του κόσμου φτάνουν πρώτα εδώ.",
                    image = hero
                }
            };

            // Βήμα 4 — Οικονομική Κατάστασις (οι 4 τάξεις του Σόλωνα — οριστικές).
            db.wealthClasses = new List<CreationOption>
            {
                new CreationOption
                {
                    id = "pentakosiomedimnoi",
                    title = "Πεντακοσιομέδιμνοι",
                    description = "Η ανώτερη τάξη του Σόλωνα: εισόδημα τουλάχιστον πεντακοσίων μεδίμνων. " +
                                  "Ανοίγει τα μεγάλα αξιώματα — και τις μεγάλες λειτουργίες που θα κληθείς να πληρώσεις.",
                    image = hero
                },
                new CreationOption
                {
                    id = "hippeis",
                    title = "Ιππείς",
                    description = "Εισόδημα τριακοσίων μεδίμνων και ένα πολεμικό άλογο στον στάβλο. " +
                                  "Κύρος στο πεδίο της μάχης, υπολήψεις στην πόλη.",
                    image = hero
                },
                new CreationOption
                {
                    id = "zeugitai",
                    title = "Ζευγίται",
                    description = "Εισόδημα διακοσίων μεδίμνων — όσο χρειάζεται ένα ζευγάρι βόδια. " +
                                  "Η ραχοκοκαλιά της φάλαγγας των οπλιτών και της εκκλησίας.",
                    image = hero
                },
                new CreationOption
                {
                    id = "thetes",
                    title = "Θήτες",
                    description = "Η φτωχότερη τάξη: μεροκαματιάρηδες και κωπηλάτες των τριήρων. " +
                                  "Χωρίς αξιώματα, αλλά με ψήφο — και ο στόλος δεν κινείται χωρίς εσένα.",
                    image = hero
                }
            };

            // Βήμα 5 — Περίοδος (δείγματα).
            db.periods = new List<CreationOption>
            {
                new CreationOption
                {
                    id = "kleisthenes_508",
                    title = "ΠΑΡΑΔΕΙΓΜΑ: Κλεισθένης (508 π.Χ.)",
                    description = "Η γέννηση της δημοκρατίας: ο Κλεισθένης μόλις μοίρασε τους πολίτες " +
                                  "σε δέκα νέες φυλές και όλα είναι ακόμη ρευστά — και όλα είναι δυνατά.",
                    image = hero
                },
                new CreationOption
                {
                    id = "perikles_450",
                    title = "ΠΑΡΑΔΕΙΓΜΑ: Περικλής (450 π.Χ.)",
                    description = "Ο χρυσός αιώνας: μισθός για τα δικαστήρια, έργα στην Ακρόπολη, " +
                                  "η Αθήνα ηγεμονεύει — και η εκκλησία του δήμου αποφασίζει για τα πάντα.",
                    image = hero
                }
            };

            // Βήμα 6 — Επάγγελμα (2 ανά τριττύα, φιλτράρονται από το trittysId).
            db.professions = new List<ProfessionOption>
            {
                new ProfessionOption
                {
                    id = "kerameus_er_asty", trittysId = "erechtheis_asty",
                    title = "ΠΑΡΑΔΕΙΓΜΑ: Κεραμεύς",
                    description = "Αγγειοπλάστης του άστεως. Τα χέρια σου λερώνονται με πηλό, " +
                                  "αλλά τα αγγεία σου ταξιδεύουν σε όλη τη Μεσόγειο.",
                    image = hero
                },
                new ProfessionOption
                {
                    id = "emporos_er_asty", trittysId = "erechtheis_asty",
                    title = "ΠΑΡΑΔΕΙΓΜΑ: Έμπορος",
                    description = "Πουλάς και αγοράζεις στην αγορά. Ξέρεις τις τιμές, τα νέα " +
                                  "και — το πολυτιμότερο — τους ανθρώπους.",
                    image = hero
                },
                new ProfessionOption
                {
                    id = "halieus_er_paralia", trittysId = "erechtheis_paralia",
                    title = "ΠΑΡΑΔΕΙΓΜΑ: Αλιεύς",
                    description = "Ψαράς της παραλίας. Ξυπνάς πριν τον ήλιο και ξέρεις τη θάλασσα " +
                                  "καλύτερα από κάθε στρατηγό.",
                    image = hero
                },
                new ProfessionOption
                {
                    id = "naupegos_er_paralia", trittysId = "erechtheis_paralia",
                    title = "ΠΑΡΑΔΕΙΓΜΑ: Ναυπηγός",
                    description = "Χτίζεις τριήρεις στα νεώρια. Η δύναμη της Αθήνας " +
                                  "βγαίνει από τα δικά σου δοκάρια.",
                    image = hero
                },
                new ProfessionOption
                {
                    id = "chalkeus_ai_asty", trittysId = "aigeis_asty",
                    title = "ΠΑΡΑΔΕΙΓΜΑ: Χαλκεύς",
                    description = "Σιδεράς του άστεως: ασπίδες, δρεπάνια, καρφιά. " +
                                  "Όλοι σε χρειάζονται, στην ειρήνη και στον πόλεμο.",
                    image = hero
                },
                new ProfessionOption
                {
                    id = "skytotomos_ai_asty", trittysId = "aigeis_asty",
                    title = "ΠΑΡΑΔΕΙΓΜΑ: Σκυτοτόμος",
                    description = "Δουλεύεις το δέρμα: σανδάλια, ζώνες, ασκοί. " +
                                  "Ταπεινή τέχνη — μα ο Κλέων από εργαστήρι δέρματος ξεκίνησε.",
                    image = hero
                },
                new ProfessionOption
                {
                    id = "georgos_ai_mesogeia", trittysId = "aigeis_mesogeia",
                    title = "ΠΑΡΑΔΕΙΓΜΑ: Γεωργός",
                    description = "Καλλιεργείς σιτάρι και ελιές στη μεσογαία. Η γη είναι σκληρή, " +
                                  "αλλά δική σου — και αυτό στην Αττική μετράει.",
                    image = hero
                },
                new ProfessionOption
                {
                    id = "ampelourgos_ai_mesogeia", trittysId = "aigeis_mesogeia",
                    title = "ΠΑΡΑΔΕΙΓΜΑ: Αμπελουργός",
                    description = "Το κρασί σου πίνεται στα συμπόσια της πόλης — " +
                                  "εκεί όπου κλείνονται οι πολιτικές συμμαχίες.",
                    image = hero
                },
                new ProfessionOption
                {
                    id = "lithoxoos_pa_asty", trittysId = "pandionis_asty",
                    title = "ΠΑΡΑΔΕΙΓΜΑ: Λιθοξόος",
                    description = "Πελεκάς μάρμαρο για ναούς και στήλες. Τα χέρια σου " +
                                  "θα αφήσουν σημάδι πιο ανθεκτικό από κάθε ψήφισμα.",
                    image = hero
                },
                new ProfessionOption
                {
                    id = "argyramoibos_pa_asty", trittysId = "pandionis_asty",
                    title = "ΠΑΡΑΔΕΙΓΜΑ: Αργυραμοιβός",
                    description = "Αλλάζεις νομίσματα στην αγορά και μυρίζεσαι τον κάλπικο άργυρο " +
                                  "από μακριά — όπως και τον κάλπικο ρήτορα.",
                    image = hero
                },
                new ProfessionOption
                {
                    id = "porthmeus_pa_paralia", trittysId = "pandionis_paralia",
                    title = "ΠΑΡΑΔΕΙΓΜΑ: Πορθμεύς",
                    description = "Περνάς ανθρώπους και εμπορεύματα με το πορθμείο σου. " +
                                  "Ακούς όλες τις κουβέντες — και δεν ξεχνάς καμία.",
                    image = hero
                },
                new ProfessionOption
                {
                    id = "naukleros_pa_paralia", trittysId = "pandionis_paralia",
                    title = "ΠΑΡΑΔΕΙΓΜΑ: Ναύκληρος",
                    description = "Δικό σου καράβι, δικά σου φορτία. Το κέρδος μεγάλο, " +
                                  "το ρίσκο μεγαλύτερο — σαν την ίδια την πολιτική.",
                    image = hero
                }
            };

            CreateAsset(db, SetupPaths.CreationDbAsset);
        }

        // ── IndicatorCatalog ──

        private static void CreateIndicatorCatalog()
        {
            if (SetupCommon.SkipIfExists(SetupPaths.IndicatorCatalogAsset)) return;

            var catalog = ScriptableObject.CreateInstance<IndicatorCatalog>();
            catalog.entries = new List<IndicatorCatalog.Entry>
            {
                new IndicatorCatalog.Entry
                {
                    id = IndicatorId.Eunomia,
                    displayName = "Ευνομία",
                    description = "Η τάξη και η νομιμότητα της πόλεως. Όσο ψηλότερα στέκει, " +
                                  "τόσο πιο σταθερή είναι η ζωή στην Αθήνα — και τόσο πιο δύσκολα ανατρέπεται."
                },
                new IndicatorCatalog.Entry
                {
                    id = IndicatorId.Demofilia,
                    displayName = "Δημοφιλία",
                    description = "Η στήριξη του πλήθους. Ανεβάζει τα χέρια στην εκκλησία υπέρ σου — " +
                                  "αλλά ο δήμος αγαπά γρήγορα και ξεχνά γρηγορότερα."
                },
                new IndicatorCatalog.Entry
                {
                    id = IndicatorId.Ethos,
                    displayName = "Ήθος",
                    description = "Η φήμη σου για δικαιοσύνη και αρετή. Οι δίκαιοι ακούγονται " +
                                  "ακόμη κι όταν μιλούν σιγά."
                },
                new IndicatorCatalog.Entry
                {
                    id = IndicatorId.Kachypopsia,
                    displayName = "Καχυποψία",
                    description = "Πόσο σε υποψιάζονται οι γύρω σου. Όσο ανεβαίνει, " +
                                  "τόσο πλησιάζει η μέρα που κάποιος θα κοιτάξει πιο προσεκτικά.",
                    onlyWhenSuspicionEnabled = true,
                    highIsBad = true
                },
                new IndicatorCatalog.Entry
                {
                    id = IndicatorId.Oikos,
                    displayName = "Οίκος",
                    description = "Η ευημερία του σπιτιού και της περιουσίας σου. Χωρίς γερό οίκο " +
                                  "δεν αντέχεις ούτε λειτουργίες ούτε εχθρούς."
                }
            };

            CreateAsset(catalog, SetupPaths.IndicatorCatalogAsset);
        }

        // ── IntroComic ──

        private static void CreateIntroComic()
        {
            if (SetupCommon.SkipIfExists(SetupPaths.IntroComicAsset)) return;

            var beat = SetupCommon.Load<AudioClip>(SetupPaths.ComicBeatWav);
            var comic = ScriptableObject.CreateInstance<ComicSequence>();
            comic.allowSkip = true;
            comic.holdAfterLastPanel = 2f;
            comic.panels = new List<ComicSequence.Panel>
            {
                new ComicSequence.Panel
                {
                    image = SetupCommon.Load<Sprite>(SetupPaths.Comic1Png),
                    delayBeforeShow = 1.2f,
                    fadeInDuration = 0.6f,
                    sound = beat
                },
                new ComicSequence.Panel
                {
                    image = SetupCommon.Load<Sprite>(SetupPaths.Comic2Png),
                    delayBeforeShow = 1.6f,
                    fadeInDuration = 0.6f,
                    sound = beat
                },
                new ComicSequence.Panel
                {
                    image = SetupCommon.Load<Sprite>(SetupPaths.Comic3Png),
                    delayBeforeShow = 1.6f,
                    fadeInDuration = 0.6f,
                    sound = beat
                }
            };

            CreateAsset(comic, SetupPaths.IntroComicAsset);
        }

        // ── Chapter01 dialogue tree ──

        /// <summary>
        /// A small but REAL demo tree (7 nodes): narrator intro → elder →
        /// a 3-way branch (distinct indicator effects, one flag, one
        /// advanceWeek) → per-choice responses → one closing node.
        /// </summary>
        private static void CreateChapter01Dialogue()
        {
            if (SetupCommon.SkipIfExists(SetupPaths.Chapter01DialogueAsset)) return;

            var hero = SetupCommon.Load<Sprite>(SetupPaths.HeroPortraitPng);
            var tree = ScriptableObject.CreateInstance<DialogueTree>();
            tree.startNodeId = "intro";
            tree.nodes = new List<DialogueNode>
            {
                new DialogueNode
                {
                    id = "intro",
                    speakerName = "Αφηγητής",
                    text = "Χαράματα. Ανηφορίζεις για πρώτη φορά προς την Πνύκα, εκεί όπου συνεδριάζει " +
                           "η εκκλησία του δήμου. Το πλήθος μαζεύεται ήδη γύρω από το βήμα, " +
                           "κι ο αέρας μυρίζει θυμάρι και πολιτική.",
                    nextNodeId = "elder_greets"
                },
                new DialogueNode
                {
                    id = "elder_greets",
                    speakerName = "Γέροντας",
                    portrait = hero,
                    text = "Καινούργιο πρόσωπο στην εκκλησία, ε; Κάθισε κοντά μου. Σήμερα ψηφίζουμε " +
                           "για το σιτάρι — και οι δημαγωγοί θα πουν πολλά και ωραία λόγια.",
                    nextNodeId = "elder_question"
                },
                new DialogueNode
                {
                    id = "elder_question",
                    speakerName = "Γέροντας",
                    portrait = hero,
                    text = "Πες μου όμως — εσύ γιατί ανέβηκες σήμερα εδώ;",
                    choices = new List<DialogueChoice>
                    {
                        new DialogueChoice
                        {
                            id = "listen_elder",
                            text = "«Για να ακούσω και να μάθω. Πες μου εσύ, που ξέρεις.»",
                            nextNodeId = "resp_listen",
                            effects = new List<IndicatorEffect>
                            {
                                new IndicatorEffect { indicator = IndicatorId.Ethos, delta = 5 },
                                new IndicatorEffect { indicator = IndicatorId.Eunomia, delta = 2 }
                            },
                            setFlags = new List<string> { "listened_to_elder" }
                        },
                        new DialogueChoice
                        {
                            id = "speak_bold",
                            text = "«Για να μιλήσω! Ο δήμος πρέπει να με μάθει από σήμερα.»",
                            nextNodeId = "resp_bold",
                            effects = new List<IndicatorEffect>
                            {
                                new IndicatorEffect { indicator = IndicatorId.Demofilia, delta = 5 },
                                new IndicatorEffect { indicator = IndicatorId.Ethos, delta = -3 }
                            }
                        },
                        new DialogueChoice
                        {
                            id = "stay_quiet",
                            text = "«Θα μείνω μια βδομάδα να παρατηρώ, πριν ανοίξω το στόμα μου.»",
                            nextNodeId = "resp_quiet",
                            effects = new List<IndicatorEffect>
                            {
                                new IndicatorEffect { indicator = IndicatorId.Oikos, delta = 3 },
                                new IndicatorEffect { indicator = IndicatorId.Demofilia, delta = -2 }
                            },
                            advanceWeek = true
                        }
                    }
                },
                new DialogueNode
                {
                    id = "resp_listen",
                    speakerName = "Γέροντας",
                    portrait = hero,
                    text = "Σοφά μιλάς. Όποιος ακούει πρώτα, ψηφίζει καλύτερα. Θα σου δείχνω " +
                           "ποιος ρήτορας λέει αλήθεια και ποιος πουλάει αέρα.",
                    nextNodeId = "closing"
                },
                new DialogueNode
                {
                    id = "resp_bold",
                    speakerName = "Γέροντας",
                    portrait = hero,
                    text = "Χα! Θράσος έχεις, δεν θα σου το αρνηθώ. Πρόσεξε μόνο — ο δήμος σηκώνει " +
                           "ψηλά όποιον αγαπά, μα τον γκρεμίζει κι από ψηλότερα.",
                    nextNodeId = "closing"
                },
                new DialogueNode
                {
                    id = "resp_quiet",
                    speakerName = "Γέροντας",
                    portrait = hero,
                    text = "Μια βδομάδα πέρασε λοιπόν, κι εσύ όλο κοιτάς και σωπαίνεις. " +
                           "Η υπομονή είναι αρετή — φτάνει να μη γίνει συνήθεια.",
                    nextNodeId = "closing"
                },
                new DialogueNode
                {
                    id = "closing",
                    speakerName = "Αφηγητής",
                    text = "Ο κήρυκας ζητά ησυχία και το πλήθος σιγεί. Η πρώτη σου εκκλησία αρχίζει — " +
                           "και μαζί της, η δική σου Οδός της Δημοκρατίας."
                    // Χωρίς nextNodeId και χωρίς επιλογές: κόμβος τέλους (IsEnd).
                }
            };

            CreateAsset(tree, SetupPaths.Chapter01DialogueAsset);
        }

        // ── Chapter01 definition ──

        private static void CreateChapter01()
        {
            if (SetupCommon.SkipIfExists(SetupPaths.Chapter01Asset)) return;

            var chapter = ScriptableObject.CreateInstance<ChapterDefinition>();
            chapter.chapterId = "ch01";
            chapter.title = "Κεφάλαιο Α΄ — Η Πρώτη Εκκλησία";
            chapter.sceneName = "Chapter01";
            chapter.background = SetupCommon.Load<Sprite>(SetupPaths.BgChapter01Png);
            chapter.ambientMusic = SetupCommon.Load<AudioClip>(SetupPaths.ChapterMusicWav);
            chapter.dialogue = SetupCommon.Load<DialogueTree>(SetupPaths.Chapter01DialogueAsset);
            chapter.dialogueStartDelay = 1.5f;
            chapter.nextChapter = null;   // τελευταίο (και μοναδικό) κεφάλαιο προς το παρόν

            CreateAsset(chapter, SetupPaths.Chapter01Asset);
        }

        // ── GameConfig ──

        private static void CreateGameConfig()
        {
            if (SetupCommon.SkipIfExists(SetupPaths.GameConfigAsset)) return;

            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.weeksPerPrytany = 5;
            config.startingIndicators = new IndicatorSet();   // όλα 50 από το μοντέλο
            config.cursorTexture = SetupCommon.Load<Texture2D>(SetupPaths.CursorPng);
            config.cursorHotspot = Vector2.zero;   // η μύτη του placeholder cursor είναι πάνω-αριστερά

            config.creationDatabase = SetupCommon.Load<CreationDatabase>(SetupPaths.CreationDbAsset);
            config.indicatorCatalog = SetupCommon.Load<IndicatorCatalog>(SetupPaths.IndicatorCatalogAsset);
            config.introComic = SetupCommon.Load<ComicSequence>(SetupPaths.IntroComicAsset);
            config.firstChapter = SetupCommon.Load<ChapterDefinition>(SetupPaths.Chapter01Asset);

            config.mainMenuMusic = SetupCommon.Load<AudioClip>(SetupPaths.MainMenuMusicWav);
            config.uiHoverSfx = SetupCommon.Load<AudioClip>(SetupPaths.UiHoverWav);
            config.uiClickSfx = SetupCommon.Load<AudioClip>(SetupPaths.UiClickWav);

            config.fadeDuration = 0.6f;
            config.chapterTitleHold = 1.8f;
            config.minLoadingTime = 0.4f;

            CreateAsset(config, SetupPaths.GameConfigAsset);
        }
    }
}
#endif
