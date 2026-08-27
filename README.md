# Οδός Δημοκρατίας — Unity Project

Παιχνίδι για τη δημοκρατία της κλασικής Αθήνας. Ο παίκτης πλάθει έναν πολίτη
(φύλο, φυλή, τριττύα, οικονομική τάξη, περίοδο, επάγγελμα) και ζει ένα αθηναϊκό
έτος: δέκα πρυτανείες, με την πρυτανεύουσα φυλή να ορίζεται με κλήρωση, και τις
αποφάσεις του να κινούν πέντε δείκτες.

**Unity 6000.4.1f1** · 2D · uGUI + TextMeshPro · Input System

---

## Πρώτο τρέξιμο (μία φορά μόνο)

1. Άνοιξε το project στο Unity Hub.
2. Τρέξε **Tools ▸ DemocracyWay ▸ Setup (μία φορά)** — φτιάχνει τα Data assets,
   τα prefabs και τις 5 σκηνές, και **αρνείται** να πειράξει ό,τι ήδη υπάρχει.
3. Πάτα **Play** από οπουδήποτε — το ▶ μπαίνει πάντα από τη σκηνή Boot
   (`Assets/Scripts/Editor/PlayFromBoot.cs`).

Μετά το πρώτο Setup, **οι σκηνές/prefabs/assets σού ανήκουν**: ό,τι αλλάξεις
στον editor μένει. Όταν είσαι ικανοποιημένος, μπορείς να διαγράψεις ολόκληρο
τον φάκελο `Assets/Setup/` — τίποτα δεν τον χρειάζεται στο runtime.

Headless (για CI ή έλεγχο):

```bash
"C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Unity.exe" -batchmode -quit -nographics -projectPath . -executeMethod DemocracyWay.Setup.OneShotSetup.Run
```

---

## Πού βάζεις το περιεχόμενό σου

Όλο το περιεχόμενο ζει σε assets που επεξεργάζεσαι στον Inspector — ποτέ σε
κώδικα, και κανένα εργαλείο δεν τα ξαναγράφει:

| Θέλεις να αλλάξεις… | Άνοιξε… |
|---|---|
| Επιλογές δημιουργίας (φύλα, φυλές, τριττύες, τάξεις, περιόδους, επαγγέλματα) | `Assets/Data/CreationDatabase` |
| Ονόματα/περιγραφές δεικτών (tooltips) | `Assets/Data/IndicatorCatalog` |
| Διαλόγους, επιλογές, effects, ποια επιλογή περνάει βδομάδα | `Assets/Data/Dialogues/…` |
| Τίτλο κεφαλαίου, background, μουσική, επόμενο κεφάλαιο | `Assets/Data/Chapters/…` |
| Καρέ/χρονισμό/ήχους του intro comic | `Assets/Data/IntroComic` |
| Βδομάδες ανά πρυτανεία, cursor, αρχικούς δείκτες, μουσική μενού, UI SFX | `Assets/Data/GameConfig` |

Οι επιλογές με πρόθεμα **«ΠΑΡΑΔΕΙΓΜΑ:»** είναι δείγματα — αντικατέστησέ τες.

**Εικόνες/ήχοι**: κάθε αρχείο στα `Assets/Art/` και `Assets/Audio/` είναι
placeholder. Ρίξε το δικό σου αρχείο **με το ίδιο όνομα** στη θέση του (ή
πρόσθεσε νέα αρχεία και σύρε τα στα αντίστοιχα πεδία των assets).

**Νέο κεφάλαιο**: δες τον οδηγό [Docs/NEA_SKINI.md](Docs/NEA_SKINI.md) (~10 λεπτά).

---

## Αρχιτεκτονική

Αναλυτικά στο [Docs/ARCHITECTURE.md](Docs/ARCHITECTURE.md). Σύνοψη:

```
Assets/Scripts/
  Core/      καθαρή λογική: session, δείκτες, πρυτανείες, saves, analytics
  Data/      οι ορισμοί των ScriptableObjects (τα instances ζουν στο Assets/Data/)
  Services/  persistent συστήματα (Boot ▸ Systems prefab): ServicesRoot.Audio/
             .Settings/.Session/.Flow/.Cursor/.Pause/.Config
  UI/        κουμπιά, panels, μενού
  Gameplay/  δημιουργία χαρακτήρα, comic, story σκηνές, διάλογοι, HUD
  Editor/    PlayFromBoot (μόνιμο εργαλείο editor)
Assets/Setup/  one-shot scaffolder — διαγράψιμος μετά το πρώτο τρέξιμο
Assets/Tests/  EditMode tests για Core + Data
```

Ροή: `Boot → MainMenu → (Νέο) επιλογή slot → CharacterCreation → ComicIntro →
Chapter01 → …` και `(Φόρτωση) → κατευθείαν στο κεφάλαιο του save`.

- **Autosave** σε κάθε βδομάδα πρυτανείας (και checkpoint σε κάθε νέο κεφάλαιο)·
  το save θυμάται και τον κόμβο διαλόγου, ώστε η φόρτωση να συνεχίζει από εκεί.
- **Pause (ESC)** μόνο μέσα σε story σκηνές — το δηλώνει ο StorySceneController.
- Η **Καχυποψία** εμφανίζεται μόνο όταν η επιλογή φύλου την ενεργοποιεί
  (ρύθμιση `enablesSuspicion` στη CreationDatabase).

---

## Saves & Analytics

`%USERPROFILE%\AppData\LocalLow\<Company>\<Product>\`

- `saves\slot0..3.json` — 4 θέσεις, versioned JSON, atomic write· κατεστραμμένο
  αρχείο εμφανίζεται ως «Κατεστραμμένη αποθήκευση» και διαγράφεται από το μενού.
- `analytics\events.jsonl` — μία γραμμή JSON ανά γεγονός (game_started,
  chapter_started, choice_made, week_advanced, game_loaded), με flat πεδία.
  Ανοίγει απευθείας σε pandas/Excel για ανάλυση των επιλογών των παικτών.

---

## Έλεγχος

EditMode tests (ημερολόγιο, saves, φίλτρα βάσης, δείκτες, analytics):

```bash
"C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Logs/test-results.xml
```
