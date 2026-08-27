# Οδός Δημοκρατίας — Αρχιτεκτονική

Unity 6000.4.1f1 · 2D · uGUI + TextMeshPro · Input System (direct polling, χωρίς .inputactions)

## Θεμελιώδης κανόνας

**Καμία αναγέννηση assets.** Σκηνές, prefabs και ScriptableObjects είναι η πηγή αλήθειας
και τα επεξεργάζεται ο χρήστης στον Unity editor. Το `Assets/Setup/` είναι scaffolder
**μίας χρήσης**: τρέχει μία φορά, αρνείται να ξαναγράψει ό,τι υπάρχει ήδη, και μετά
διαγράφεται ολόκληρος ο φάκελος.

## Layers (assembly definitions)

```
Core      ← καθαρή λογική: session, δείκτες, πρυτανείες, saves, analytics. Καμία σκηνή/UI.
Data      ← ScriptableObject ορισμοί. Τα instances ζουν στο Assets/Data/ και τα γεμίζει ο χρήστης.
Services  ← persistent συστήματα (ένα prefab "Systems" στο Boot scene): audio, settings,
            session, ροή σκηνών, cursor, pause.
UI        ← επαναχρησιμοποιήσιμα widgets + panels μενού.
Gameplay  ← character creation, comic, story scenes, dialogue, HUD.
```

Επιτρεπτές εξαρτήσεις: `Core ← Data ← Services ← UI ← Gameplay`. Ποτέ ανάποδα.

## Πρόσβαση στα services

Ένα root MonoBehaviour, `ServicesRoot`, πάνω στο prefab `Systems` (DontDestroyOnLoad).
Κρατά serialized αναφορές στα child services και τα εκθέτει στατικά:

```csharp
ServicesRoot.Audio     // AudioService    — μουσική, SFX, ομιλία, εντάσεις, UI hover/click
ServicesRoot.Settings  // SettingsService — PlayerPrefs: fullscreen, ανάλυση, 3 εντάσεις
ServicesRoot.Session   // SessionService  — το τρέχον παιχνίδι: profile, δείκτες, ημερολόγιο,
                       //                   saves, autosave, καταγραφή επιλογών
ServicesRoot.Flow      // SceneFlowService— fade overlay, τίτλος κεφαλαίου, loading screen,
                       //                   ασύγχρονη φόρτωση σκηνών
ServicesRoot.Cursor    // CursorService   — custom cursor από το GameConfig
ServicesRoot.Pause     // PauseService    — ESC/pause menu· ενεργό μόνο όταν μια σκηνή το δηλώσει
ServicesRoot.Config    // GameConfig SO   — όλες οι ρυθμίσεις σχεδίασης σε ένα asset
```

Ένας instance-guard, ένα DontDestroyOnLoad — μόνο στο ServicesRoot, πουθενά αλλού.

## Ροή σκηνών

```
Boot ─► MainMenu ─┬─► Νέο Παιχνίδι (κλειδωμένο αν 4/4 slots γεμάτα)
                  │     └─► επιλογή κενού slot ─► CharacterCreation (6 βήματα)
                  │           └─► ComicIntro (skippable) ─► Chapter01 (τίτλος + fade)
                  ├─► Φόρτωση (κλειδωμένο αν 0 saves· λίστα slots + Διαγραφή) ─► κεφάλαιο
                  ├─► Ρυθμίσεις (panel)
                  └─► Έξοδος (confirm popup)
```

- `Boot.unity`: μόνο το Systems prefab + BootLoader → `Flow.GoToScene("MainMenu")`.
- Το overlay του Flow ξεκινά **μαύρο** (alpha 1) ώστε η πρώτη εικόνα να είναι fade-from-black.
- Story σκηνές: το Flow δείχνει τον τίτλο κεφαλαίου πάνω στο μαύρο, κρατά, κάνει fade out·
  μετά από `ChapterDefinition.dialogueStartDelay` δευτ. ξεκινά ο διάλογος.
- Pause: `PauseService.CanPause` γίνεται true ΜΟΝΟ από το `StorySceneController` (όχι λίστες
  ονομάτων σκηνών). Pause menu: Συνέχεια, Ρυθμίσεις, Επιστροφή στην Αρχική (με confirm),
  Έξοδος (με confirm).

## Δεδομένα (Assets/Data/ — ιδιοκτησία χρήστη, ο Setup τα φτιάχνει μία φορά με δείγματα «ΠΑΡΑΔΕΙΓΜΑ»)

| Asset | Τύπος | Περιεχόμενο |
|---|---|---|
| `GameConfig` | SO | weeksPerPrytany (5), αρχικοί δείκτες, cursor texture+hotspot, μουσική μενού, UI SFX, αναφορές σε όλα τα παρακάτω |
| `CreationDatabase` | SO | genders (με `enablesSuspicion` στη Γυνή), tribes, trittyes (`tribeId`), wealthClasses, periods, professions (`trittysId`). Φίλτρα: `TrittyesFor(tribeId)`, `ProfessionsFor(trittysId)` |
| `IndicatorCatalog` | SO | 5 δείκτες: όνομα, περιγραφή tooltip, `onlyWhenSuspicionEnabled` στην Καχυποψία |
| `DialogueTree` (ανά κεφάλαιο) | SO | κόμβοι: id, ομιλητής, portrait, κείμενο, voice clip, nextNodeId Ή choices (text, nextNodeId, indicator effects, setFlags, advanceWeek) |
| `ChapterDefinition` (ανά κεφάλαιο) | SO | chapterId, τίτλος, sceneName, background, ambient μουσική, DialogueTree, dialogueStartDelay, nextChapter |
| `ComicSequence` | SO | panels: εικόνα, καθυστέρηση εμφάνισης, ήχος· allowSkip |

Ό,τι βλέπει ο παίκτης (κείμενα, εικόνες, ήχοι, δέντρα) ζει ΕΔΩ — ποτέ σε κώδικα.

## Session & χρόνος

- `GameSession` (Core): profile, `IndicatorSet` (0–100, clamp), `PrytanyCalendar`
  (10 πρυτανείες × weeksPerPrytany βδομάδες, σειρά φυλών με Fisher–Yates χωρίς επανάληψη),
  flags, ιστορικό επιλογών, playtime.
- Καχυποψία: υπάρχει στο μοντέλο πάντα, **εμφανίζεται/μετράει** μόνο αν
  `profile.suspicionEnabled` (το ορίζει η επιλογή φύλου στη βάση).
- `SessionService.AdvanceWeek()` → προχωρά ημερολόγιο → **autosave** → analytics event.

## Saves (4 slots)

`%USERPROFILE%\AppData\LocalLow\<Company>\<Product>\saves\slot{0..3}.json`
- Versioned JSON (`SaveData.version`), atomic write (tmp + replace), κατεστραμμένο αρχείο = κενό slot.
- Νέο Παιχνίδι: απενεργό όταν και τα 4 γεμάτα. Φόρτωση: λίστα με μεταδεδομένα + Διαγραφή (confirm).

## Analytics

`...\LocalLow\<Company>\<Product>\analytics\events.jsonl` — μία γραμμή JSON ανά γεγονός,
flat πεδία (χρόνος, sessionId, τύπος, chapterId, nodeId, choiceId, choiceText, prytany, week).
Τύποι: `game_started`, `game_loaded`, `chapter_started`, `choice_made`, `week_advanced`.
Ανοίγει απευθείας σε pandas/Excel. Απομακρυσμένη συλλογή = μελλοντικό backend, το format είναι έτοιμο.

## Είσοδος

Direct polling του Input System (`Keyboard.current`, `Mouse.current`) — όχι .inputactions asset.
ESC = pause (μέσω PauseService), Space/κλικ = προώθηση διαλόγου, κουμπί Παράλειψη στο comic.

## Conventions

- Κώδικας/ονόματα: αγγλικά. Strings παίκτη: ελληνικά, ζουν σε Data assets ή serialized fields.
- Namespaces: `DemocracyWay.Core/.Data/.Services/.UI/.Gameplay/.EditorTools/.Setup`.
- Serialized πεδία wired από τον Setup με SerializedObject helper που ΦΩΝΑΖΕΙ αν λείπει property.
- Tests (EditMode) για Core + Data: ημερολόγιο, saves, φίλτρα, clamps, analytics.
- Editor helper που μένει: `PlayFromBoot` (το ▶ μπαίνει πάντα από το Boot).
