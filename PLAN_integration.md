# Οδός Δημοκρατίας — τι χρειάζεται για να εφαρμοστεί το game loop με τον χάρτη

Ανάλυση του project `C:\Projects\democracy-way` (28/10, μόνο ανάγνωση). Προορίζεται για το Claude Code: κάθε ενότητα είναι ένα commit.

## Τι υπάρχει ήδη και είναι σωστό — μην αλλάξει

- **Ο πυρήνας είναι μέσα.** `Assets/Scripts/Core/` (AssemblyRules, EndingRules, CaseData, GameConfig, GameContent, Office/Official/Logographer), 20 `Case_XX.asset` στο `Assets/Content/Cases`, Offices/Officials/Logographers assets, importer (`Editor/AgoraContentTextImport.cs`), validation, και EditMode tests (`Core/Tests/Editor/`: AssemblyRules, EndingRules, Content, Statistical). Αυτό είναι το 60% της δουλειάς.
- **Η Αγορά είναι υποχρεωτική** πριν τη συνεδρίαση: `State.marketVisitedTurn` + modal στο `OpenInstitution` (Campaign.cs). Σωστό, μένει.
- **Η οθόνη της Αγοράς** (`RenderMarketArchive`) δείχνει ήδη: είδηση, τις δύο προτάσεις (`optionA/optionB`), τους ομιλητές, ποιος στηρίζει ποια θέση, ένδειξη καταγγελίας, όρους ευρετηρίου υπογραμμισμένους. Ταιριάζει με το mockup 3. Λείπει μόνο το ΕΚ1 («Τι ορίζει ο νόμος»).
- **Το Ευρετήριο** (LexiconPanel + offices catalogue) ανοίγει από παντού, χωρίς συνέπεια. Σωστό.
- **Τηλεμετρία** `AgoraFirestoreTelemetry` με τα γεγονότα του spec. Μένει.
- **Save με snapshot/rollback** (`MutateAndSave`) και αποτέλεσμα ψήφου αποθηκευμένο τη στιγμή της ψήφου. Μένει.

## Το πρόβλημα — μία απόφαση δομής

Το Claude Code υλοποίησε την **πρώτη, γραμμική εκδοχή** της ροής (είδηση → `case-prep` → λόγοι → `speech` → ψήφος), όχι τη δομή του παιχνιδιού με τον χάρτη. Συγκεκριμένα, στο `GameFlowController.AgoraCase.cs`:

- Πείθω / Δωροδοκώ / Καταγγέλλω έγιναν κουμπιά της οθόνης `case-prep`.
- Η πρόσληψη λογογράφου έγινε κουμπί της οθόνης `speech`, μέσα στη συνέλευση.
- Οι οθόνες της πόλης (`market`, `logographers`, `complaints`, `offices`) μετατράπηκαν σε **αρχεία μόνο ανάγνωσης** (`GameFlowController.CityArchive.cs`): τα κουμπιά ενέργειας που υπάρχουν στη σκηνή απενεργοποιήθηκαν ρητά — `logographers.buy.i`, `complaints.file`, `complaints.primary`, `complaint-detail.choice.i`, `official-detail.persuade`, `official-detail.bribe` (όλα `SetActive(false)`).

Δηλαδή η σκηνή έχει ακριβώς τα κουμπιά που χρειάζεται ο χάρτης, και ο κώδικας τα κρύβει. Η διόρθωση είναι **μετακίνηση**, όχι νέα μηχανική: οι ίδιες κλήσεις στο `AssemblyRules` καλούνται από άλλες οθόνες και σε άλλη στιγμή.

## Οι αλλαγές, με τη σειρά (ένα commit η καθεμία)

### 0. Τακτοποίηση πριν από οτιδήποτε
Το working tree έχει πολλές uncommitted αλλαγές: διαγραφή του `Assets/Agora/`, μετακίνηση στο `Assets/Scripts/Core/`, διαγραφή backgrounds (`BackgroundEnding*.png`). Το τελευταίο commit («New rules», 01:22) δεν τις περιέχει. Πρώτα: έλεγξε ότι το project κάνει compile και τα tests περνούν, και κάνε commit «Move Agora core into Scripts/Core; remove old endings art». Μετά ξεκινούν οι αλλαγές ροής. Χωρίς αυτό δεν θα ξεχωρίζει τι έσπασε.

### 1. Κατάσταση εβδομάδας (`AgoraCaseState.cs`, Domain)
Η υπόθεση δεν ξεκινά πια στη συνέλευση· ξεκινά με την επίσκεψη στην Αγορά και μαζεύει επιλογές από τέσσερις οθόνες. Τα πεδία:
- `caseId`, `stanceSides[]`, `stanceWeights[]` — μένουν.
- `prepAction` (None/Persuade/Bribe/Complain), `prepDone` — μένουν· η ενέργεια γίνεται στους Αξιωματούχους ή στις Καταγγελίες.
- **νέο** `complaintPending` (bool): «τη φέρνω στη συνέλευση» — η δίκη ΔΕΝ εκτελείται στην οθόνη Καταγγελίες, μόνο δεσμεύεται.
- **νέο** `speechBought` (bool), `speechSide` (Side), `logoIndex` — από την οθόνη Λογογράφοι. Αντικαθιστά το `intendedSide`: η πλευρά δηλώνεται εκεί που χρειάζεται (λογογράφος, πείθω, λόγος), όχι σε ξεχωριστό κουμπί.
- `spoke`, `spokeFor`, `convicted`, `trialSycophant`, `trialFine`, `resolved`, `s0`, `s`, `passedA`, `playerFlipped`, `contested`, `vote` — μένουν.
Το `step` αλλάζει τιμές: `city` (στον χάρτη), `trial`, `deliberation`, `speech`, `vote`, `result`. Αύξηση της έκδοσης save (`SaveEnvelope`) — παλιά saves στη μέση υπόθεσης απορρίπτονται με μήνυμα, όχι crash.

### 2. Η υπόθεση ξεκινά στην Αγορά (`CityArchive.cs` → `OpenMarket`)
Στο `OpenMarket`, αν `AgoraState.caseId != CurrentAgoraCase.id`: αρχικοποίηση `AgoraCaseState` με `CloneStances` (ό,τι κάνει τώρα το `BeginAgoraCase`). Έτσι οι Αξιωματούχοι και οι Λογογράφοι βλέπουν ζωντανές στάσεις. Στην `RenderMarketArchive` πρόσθεσε το `c.ek1` κάτω από την είδηση με επικεφαλίδα «Τι ορίζει ο νόμος» (υπάρχει πεδίο `market.after` ή χρειάζεται ένα ακόμη TMP_Text — αν χρειάζεται νέο αντικείμενο στη σκηνή, ρώτα πρώτα).

### 3. Αξιωματούχοι = πράξη (`CityArchive.cs`, `RenderOfficesCatalogue`, `RenderOfficeDetail`)
- Στην κορυφή της λίστας `offices` μια ενότητα «Αυτή την εβδομάδα»: οι αξιωματούχοι της `CurrentAgoraCase` με πλευρά και βάρος (ό,τι είναι τώρα το `body` του `case-prep`). Από κάτω ο κατάλογος των 35 όπως είναι.
- Στο `official-detail` για πρόσωπο της τρέχουσας υπόθεσης: ενεργοποίηση `official-detail.persuade` και `official-detail.bribe`. Το «Πείθω» χρειάζεται πλευρά: ή δύο κουμπιά («να στηρίξει την Α» / «τη Β») ή επαναχρησιμοποίηση των `complaint-detail.choice.0/1`-τύπου κουμπιών. Το κόστος δωροδοκίας στο label («Δωροδοκώ — 90 δρ.»).
- Μετακίνηση των `AgoraPersuade` / `AgoraBribe` από `AgoraCase.cs` εδώ, με την ίδια λογική (μία ενέργεια: `prepDone`, κλείδωμα, feedback, τηλεμετρία). Μετά την ενέργεια τα κουμπιά σβήνουν σε όλα τα πρόσωπα της εβδομάδας.

### 4. Λογογράφοι = αγορά λόγου (`RenderLogographerRoster`)
- Ενεργοποίηση `logographers.buy.i` (τρεις κάρτες, index 1–3 του `GameContent.logographers`). Το κλικ ανοίγει επιλογή πλευράς Α/Β (με τις γραμμές `optionA/optionB`) και «Παραγγέλνω — Χ δρ.».
- Αποθήκευση: `speechBought = true`, `speechSide`, `logoIndex`, πορτοφόλι −κόστος. Μία παραγγελία την εβδομάδα· χωρίς επιστροφή.
- Κάθε λογογράφος λέει πώς βλέπει την υπόθεση από το S₀ (`ComputeS0` με τις τρέχουσες στάσεις): |S₀−50| ≤ 12 → «αμφίρροπη», > 20 → «ο δήμος το έχει αποφασίσει»· ο φτηνός λέει πάντα «χρειάζεσαι λόγο». Μόνο κείμενο, όχι αριθμός.
- Το `status` «Η πρόσληψη γίνεται την ώρα του λόγου σου» αφαιρείται.

### 5. Καταγγελίες = απόφαση, όχι δίκη (`RenderComplaintHistory`, `RenderComplaintDetail`)
- Αν `CurrentAgoraCase.complaint.available` και όχι `sycophantFlag` και όχι `prepDone`: πρώτη γραμμή «Αυτή την εβδομάδα» → `complaint-detail` με charge, τη διαδικασία (κείμενο από Lexicon), την είδηση ξανά, τι ρισκάρει/κερδίζει.
- `complaint-detail.choice.0` = «Τη φέρνω στη συνέλευση» → `complaintPending = true`, `prepAction = Complain`, `prepDone = true` (καταναλώνει την ενέργεια). `choice.1` = «Δεν τη φέρνω». Η `AssemblyRules.Complain` ΔΕΝ καλείται εδώ.
- Το ιστορικό των δικών μένει από κάτω, όπως είναι.

### 6. Η συνέλευση καταναλώνει τις επιλογές (`AgoraCase.cs`)
- `OpenInstitution` → `BeginAgoraCase`: αν `complaintPending` → κάλεσε τώρα `AssemblyRules.Complain`, γράψε `convicted/trialSycophant/trialFine`, `Show("case-trial")`· αλλιώς → `deliberation`. Οι οθόνες `procedure-briefing` και `case-prep` **βγαίνουν από τη ροή** (τα αντικείμενα της σκηνής μπορούν να μείνουν ανενεργά — για αφαίρεση από τη σκηνή, ρώτα).
- `deliberation`: όπως είναι. Το κουμπί `deliberation.speech` πάντα → `speech` (η επιλογή γίνεται εκεί).
- `speech` — τα κουμπιά `speech.logo.0..3` αλλάζουν σημασία:
  - αν `speechBought`: `logo.0` = «Εκφωνώ τον λόγο του {Χ} — υπέρ {speechSide}», `logo.1` = «Μιλώ μόνος — υπέρ Α», `logo.2` = «Μιλώ μόνος — υπέρ Β», `logo.3` = «Σιωπώ»;
  - αλλιώς: `logo.0` ανενεργό, `logo.1/2` μιλώ μόνος Α/Β, `logo.3` σιωπώ.
  - Μιλώ μόνος = `logoIndex 0` (L 0). Αν μίλησε μόνος ενώ είχε πάπυρο άλλης πλευράς → feedback «Ο λόγος του Χ έμεινε στο σακούλι σου». Προεπισκόπηση του λόγου (βλ. 8) στο `speech.body`.
- `vote`, `vote-result`, `week-report`: όπως είναι. Στο `week-report` προσθήκη της γραμμής ασυνέπειας αν `vote != spokeFor`.
- Σβήνονται: `AgoraChooseSide`, `AgoraPersuade/Bribe/Complain` από αυτό το αρχείο (μετακινήθηκαν), `RenderAgoraPrep`, `intendedSide`.

### 7. Χάρτης (`RenderAgoraMap`, Campaign.cs)
- Το σημείο συνεδρίασης (Πνύκα/Βουλευτήριο) κλειδωμένο ως τη στιγμή που `marketVisitedTurn == Turn` — υπάρχει ήδη μέσω modal· πρόσθεσε οπτική ένδειξη (γκρι πλακέτα).
- Κάτω από κάθε κουμπί πόλης μία γραμμή κατάστασης: Αγορά «δεν έχεις περάσει / πέρασες», Αξιωματούχοι «καμία ενέργεια / έπεισες τον Χ», Λογογράφοι «χωρίς λόγο / Λόγος υπέρ Α — Ευθίας», Καταγγελίες «1 διαθέσιμη / τη φέρνεις / —». Αν αυτά χρειάζονται νέα TMP_Text στη σκηνή: ρώτα. Αλλιώς τα `map.market.badge/count`, `map.complaint.badge/count` που τώρα κρύβονται μπορούν να ξαναχρησιμοποιηθούν.

### 8. Περιεχόμενο — δύο νέα πεδία (`CaseData.cs`, `LogographerData.cs`, importer, JSON)
- `CaseData.argumentA`, `argumentB` (TextArea): το κεντρικό επιχείρημα 2 προτάσεων για τον λόγο του παίκτη. 40 κείμενα — θα τα γράψω εγώ στο JSON.
- `LogographerData.openingTemplate`, `closingTemplate`: 2 προτάσεις ύφους το καθένα, με `{side}` placeholder. Ο λόγος του παίκτη = opening + argument + closing. Μόνος = argument μόνο.
- Επέκταση του `AgoraContentTextImport` να διαβάζει τα νέα πεδία. Το `ContentTests` ελέγχει ότι δεν είναι κενά.

### 9. Φάσεις και saves (`CampaignRules.ValidPhase`, `SaveStore`)
- Αφαίρεση `procedure-briefing`, `case-prep` από το `ValidPhase`. Το `map-stela` και οι οθόνες πόλης δεν είναι φάσεις — μένουν όπως είναι (το save κρατά `step` στο `AgoraCaseState`).
- Έλεγχος saves: `AgoraCaseState` με `complaintPending && resolved` είναι άκυρο, όπως και `speechBought && logoIndex < 1`.

### 10. Tests
- `AssemblyRulesTests`/`EndingRulesTests`/`StatisticalTests`: δεν αλλάζουν — οι κανόνες δεν αλλάζουν.
- Νέο `WeekFlowTests`: με έναν ψεύτικο `GameState`, (α) αγορά λόγου Α + μιλώ μόνος Β → το `SpeechPower` υπολογίζεται με L 0 και το πορτοφόλι έχει χάσει το κόστος· (β) `complaintPending` → `Complain` καλείται ακριβώς μία φορά, στη συνέλευση· (γ) Πείθω στους Αξιωματούχους και μετά Καταγγελίες → η δεύτερη αρνείται (`prepDone`).

## Τι ΔΕΝ αλλάζει

`AssemblyRules.cs`, `EndingRules.cs`, `GameConfig`, οι αριθμοί των 20 υποθέσεων, η τηλεμετρία, το σύστημα saves, οι σκηνές (εκτός αν ζητηθεί ρητά), το Ευρετήριο, τα ερωτηματολόγια.

## Σειρά εργασίας που προτείνω

0 → 1 → 2 → 6 (ώστε το παιχνίδι να παίζεται άμεσα, ακόμη και χωρίς 3–5: ο παίκτης απλώς δεν θα έχει ενέργειες) → 4 → 3 → 5 → 7 → 8 → 9 → 10. Μετά από κάθε βήμα: Play από Boot, μία εβδομάδα, `git diff`.
