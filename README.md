# Οδός Δημοκρατίας — Unity Project

Παιχνίδι για τη δημοκρατία της κλασικής Αθήνας. Ο παίκτης φτιάχνει έναν πολίτη
(φύλο, φυλή, τριττύα, οικονομική τάξη, περίοδο, επάγγελμα) και ζει ένα αθηναϊκό
έτος — δέκα πρυτανείες, μια ανά γύρο, με την πρυτανεύουσα φυλή να ορίζεται με
κλήρωση.

**Unity 6000.4.1f1** · 2D · Input System · TextMeshPro

---

## Quick start

1. Άνοιξε το project στο Unity Hub.
2. Τρέξε **`Tools ▸ DemocracyWay ▸ Init`**.
3. Τρέξε **`Tools ▸ DemocracyWay ▸ Verify`** — 22 έλεγχοι στην Κονσόλα, όλοι PASS.
4. Άνοιξε τη σκηνή `Assets/Scenes/Bootstrap.unity` και πάτα **Play**.

Το Init ξαναχτίζει όλα τα prefabs, τα content assets και τις πέντε σκηνές από
τον κώδικα. Δεν χρειάζεται καμία χειροκίνητη σύνδεση references.

Το Verify ανοίγει τις σκηνές πού παρήχθησαν και ελέγχει ότι κάθε reference και
κάθε λίστα ήρθε πράγματι — ένα editor script μπορεί να αποτύχει σιωπηλά σε μια
ανάθεση, και το σύμπτωμα (άδεια λίστα επιλογών) δεν δείχνει πουθενά κοντά στην
αιτία. Και τα δύο τρέχουν και headless:

```bash
Unity.exe -batchmode -quit -nographics -projectPath . -executeMethod DemocracyWay.EditorTools.DemocracyWayInit.Run
```

---

## Ροή του παιχνιδιού

```
Bootstrap ──► MainMenu ──┬─► Νέο Παιχνίδι ─► επιλογή θέσης (1 από 4)
                         │                    └─► CharacterCreation (6 βήματα)
                         │                          └─► ComicIntro ─► Game
                         ├─► Φόρτωση ─► επιλογή θέσης ─────────────► Game
                         ├─► Ρυθμίσεις
                         └─► Έξοδος
```

### Δημιουργία χαρακτήρα

Έξι βήματα, με τη λίστα επιλογών **δεξιά** (η μια κάτω από την άλλη) και το
γραφικό με την περιγραφή του **αριστερά**. Το hover κάνει preview· το κλικ
επιλέγει.

| # | Βήμα | Επιλογές |
|---|------|----------|
| 1 | Φύλο | Ανήρ / Γυνή |
| 2 | Φυλή | Οι 10 Κλεισθένειες φυλές |
| 3 | Τριττύα | 3 ανά φυλή — **φιλτράρονται από τη φυλή πού διάλεξες** |
| 4 | Οικονομική κατάστασις | Οι 4 Σολώνειες τάξεις |
| 5 | Περίοδος | 5 περίοδοι, 508–338 π.Χ. |
| 6 | Επάγγελμα | 10 επαγγέλματα |

### Δείκτες

Πέντε τιμές 0–100, με **τυχαίες αρχικές τιμές** στην τρέχουσα έκδοση
(`IndicatorSet.Randomise`):

- **Ευνομία** — η τάξη και η νομιμότητα της πόλεως
- **Δήμος / Δημοφιλία** — η στήριξη του πλήθους
- **Ήθος & Ακεραιότητα** — η φήμη για δικαιοσύνη
- **Καχυποψία** — η υποψία των άλλων (ο μόνος δείκτης όπου το υψηλό είναι κακό)
- **Οίκος & Συντήρησις** — η ευημερία της περιουσίας

### Πρυτανείες

Δέκα γύροι, ένας ανά πρυτανεία. Η σειρά των φυλών κληρώνεται στην αρχή κάθε
παιχνιδιού (Fisher–Yates) και καμμία φυλή δεν πρυτανεύει δύο φορές — όπως και
ιστορικά.

---

## Γλώσσα κειμένου

Όλο το κείμενο είναι **μονοτονικό**. Το `IFKargoSans` δεν έχει κανένα glyph στο
Greek Extended block (U+1F00–U+1FFF), οπότε κάθε πολυτονικός χαρακτήρας
εμφανιζόταν ως κενό. Το `Verify` ελέγχει πλέον ότι κάθε χαρακτήρας του
περιεχομένου υπάρχει στα fonts του `Assets/Fonts/` — αν προσθέσεις κείμενο με
χαρακτήρα που λείπει, θα σου το πει πριν το δεις στην οθόνη.

---

## Δομή κώδικα

```
Assets/Scripts/
  Core/
    CitizenProfile.cs     — τα 6 βήματα + οι επιλογές τους
    CreationDatabase.cs   — ScriptableObject με όλες τις επιλογές
    Indicators.cs         — οι 5 δείκτες, τιμές και μεταβολές
    PrytanySchedule.cs    — η κλήρωση των 10 πρυτανειών
    GameSession.cs        — ό,τι σώζεται σε ένα save
    SaveSystem.cs         — 4 θέσεις αποθήκευσης (JSON)
    GameStateService.cs   — persistent singleton, ο ιδιοκτήτης του run
  Dialogue/
    DialogueModels.cs     — γραμμές, επιλογές, αποτελέσματα
    DialogueDatabase.cs   — ScriptableObject + τυχαία επιλογή διαλόγου
  UI/                     — τα panels και τα HUD
  Menu/                   — main menu, pause, settings
  Framework/              — audio, cursor, settings, scene transitions

Assets/Editor/
  DemocracyWayInit.cs            — Run() + prefab builders
  DemocracyWayInit.Scenes.cs     — οι 5 scene builders
  DemocracyWayInit.Content.cs    — φυλές, τριττύες, τάξεις, περίοδοι, επαγγέλματα
  DemocracyWayInit.Dialogues.cs  — οι αρχικοί διάλογοι + το comic
  DemocracyWayInit.AssetFallback.cs — procedural placeholder art

Assets/Content/            — τα generated ScriptableObjects (επεξεργάσιμα)
```

---

## Πώς προσθέτεις περιεχόμενο

**Χωρίς κώδικα** — επεξεργάσου τα assets στο `Assets/Content/`:

- `DialogueDatabase.asset` — νεοι διάλογοι για το κουμπί «Τυχαίος διάλογος»
- `CreationDatabase.asset` — κείμενα, γραφικά, νέες επιλογές σε κάθε βήμα
- `IntroComic.asset` — καρέ, λεζάντες, χρονισμός του intro

⚠️ Το **Init τα ξαναγράφει**. Αν θέλεις οι αλλαγές σου να επιβιώνουν, ή μην
ξανατρέξεις το Init, ή βάλε τις αλλαγές στα `DemocracyWayInit.Content.cs` /
`.Dialogues.cs`.

**Γραφικά**: ρίξε ένα PNG στο `Assets/Art/Backgrounds/` ή
`Assets/Art/Characters/` με το όνομα πού ζητά το content (`BG_…`, `CH_…`) και
αντικαθιστά αυτόματα το procedural placeholder στο επόμενο Init.

---

## Saves

Τέσσερις ανεξάρτητες θέσεις, ως JSON στο `Application.persistentDataPath`:

```
demokratia_slot0.json … demokratia_slot3.json
```

Windows: `%USERPROFILE%\AppData\LocalLow\<Company>\<Product>\`

Κατεστραμμένο ή κενό αρχείο διαβάζεται ως άδεια θέση — δεν μπλοκάρει το μενού.
