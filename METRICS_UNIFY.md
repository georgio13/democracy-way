# Δείκτες παίκτη και πόλης — ένα σύστημα, όχι δύο

Έλεγχος 28/10 15:15 στο `C:\Projects\democracy-way` (μόνο ανάγνωση). Για το Claude Code, ένα commit ανά ενότητα.

## Το πρόβλημα

Στο project ζουν **δύο** συστήματα δεικτών παράλληλα:

| | Παλιό (βιογραφία) | Νέο (Agora, game loop) |
|---|---|---|
| Παίκτης | `CharacterMetrics`: consistency 50, ethics 50, **authority 35**, persuasion 35, parrhesia 35, recognition 10 (`ContentLibrary.startingMetrics`) | `Agora.PlayerState`: Πειθώ 30, Ηθική 60, Συνέπεια 50, Παρρησία 40, Αναγν 10 — **Αυθεντία υπολογίζεται**, δεν αποθηκεύεται |
| Χρήμα | `personalFunds` 120 **οβολοί**, `cityFunds` 10.000 | `citizen.wallet` σε **δραχμές** (60/150/300/600 ανά τάξη), `polis.treasury` 0–100 |
| Πόλη | `CityMetricDelta {food, trust, readiness, war}` στις επιλογές βιογραφίας | `Agora.CityState` 4 δείκτες + κρυφός ΙΔ, ξεκινά πάντα από 50 |

Που φαίνεται στον παίκτη:
- Οθόνες βιογραφίας (`Presentation.cs` 306–314, `BiographyIdentity.cs` 143, 207–210) και `summary` (349–354): δείχνουν τα **παλιά** (`State.metrics`). Ο παίκτης βλέπει «Ηθική 50, Αυθεντία 35» στη δημιουργία χαρακτήρα.
- `profile` (`Presentation.cs` 476–491), χάρτης, απολογισμός, τέλη: δείχνουν τα **νέα** (`State.citizen`). Στο προφίλ βλέπει «Ηθική 60, Αυθεντία 0». Αντίφαση μέσα στο ίδιο παιχνίδι.
- Επιλογές βιογραφίας δείχνουν «Ταμείο πόλης −3 οβ.» και μεταβολές Επισιτισμού/Εμπιστοσύνης/Ετοιμότητας/Πολέμου (`BiographyIdentity.cs` 231–258· 6 επιλογές στο `ContentLibrary.asset` έχουν μη μηδενικά `cityMetrics`), ενώ η απόφαση είναι ότι **η δημιουργία χαρακτήρα δεν αγγίζει την πόλη**. Τα δείχνει, δεν τα εφαρμόζει (η πόλη ξεκινά από 50 στο `InitialiseAgoraCitizen`) — δηλαδή λέει ψέματα στον παίκτη.
- Η γέφυρα `InitialiseAgoraCitizen` (`AgoraCase.cs` 27–42) μεταφέρει τις **διαφορές** `metrics − baseline` στον πολίτη. Σωστή ιδέα· αλλά η `authority` χάνεται σιωπηλά (δεν υπάρχει πεδίο), και το `personalFunds` δεν μεταφέρεται καθόλου — ό,τι «κόστισε» η βιογραφία σε οβολούς εξαφανίζεται.

## Αρχή της λύσης

**Ένας πολίτης, από την πρώτη οθόνη της βιογραφίας.** Οι επιλογές της βιογραφίας είναι *δέλτα* πάνω στις αρχικές τιμές του `GameConfig`· οι οθόνες δείχνουν πάντα «αρχικές + δέλτα» με τα ονόματα και την κλίμακα του Agora. Η πόλη δεν εμφανίζεται πουθενά στη βιογραφία.

## Αλλαγές

### 1. Αρχικές τιμές από ένα σημείο (`GameConfig`)
- Το `ContentLibrary.startingMetrics` **καταργείται**· η `GameConfiguration.Validate` δεν το απαιτεί πια. Οι αρχικές τιμές είναι ΜΟΝΟ στο `GameConfig` (`startPeitho 30, startEthos 60, startSynepeia 50, startParrhesia 40, startAnagnorisimotita 10`).
- `startingPersonalFunds`, `startingCityFunds` καταργούνται· το πορτοφόλι έρχεται από `GameConfig.ClassMoney(class)`.

### 2. `CharacterMetrics` → δέλτα βιογραφίας (Domain)
- Μετονομασία σε `BiographyDeltas` με πέντε πεδία: `peitho, ethos, synepeia, parrhesia, anagnorisimotita` (+ `walletDrachmas`). **Χωρίς `authority`**: είναι υπολογιζόμενη. Όποια επιλογή στο `ContentLibrary` δίνει `authority ±x` → μεταφέρεται σε `anagnorisimotita` ή μηδενίζεται (απόφαση ανά επιλογή· είναι λίγες — λίστα με grep `authority:` στο asset).
- Το `personalFunds` των επιλογών (οβολοί) → `walletDrachmas` (διαίρεση με 6, στρογγυλοποίηση· π.χ. −3 οβ. → −1 δρ. ή απλώς μηδενισμός αν είναι θόρυβος).
- `GameState`: αφαιρούνται `metrics, baseline, basePersonalFunds, personalFunds, baseCityFunds, cityFunds`· μένει `biographyDeltas` (ή υπολογίζεται από `choices` όπως τώρα στο `GameRules.Recompute`). Save version → 4.

### 3. Η πόλη έξω από τη βιογραφία
- `CityMetricDelta` **διαγράφεται**· `ChoiceRecord.cityMetrics`, `ChoiceRecord.cityFunds`, `NarrativeChoice.cityMetrics/cityFunds` επίσης.
- Στο `ContentLibrary.asset` τα 6 `cityMetrics` και όλα τα `cityFunds` μηδενίζονται/αφαιρούνται (ο importer/validator να το ελέγχει: καμία επιλογή βιογραφίας με επίδραση στην πόλη).
- `BiographyIdentity.cs` 231–258: οι γραμμές «Ταμείο πόλης…», «Επισιτισμός…» φεύγουν. Αν η οθόνη έχει αντικείμενο για «επίδραση στην πόλη», μένει κενό/ανενεργό (σκηνή: ρώτα).

### 4. Οι οθόνες βιογραφίας δείχνουν τον πολίτη
- Νέος pure helper (Domain ή Core): `Agora.PlayerState CitizenPreview(GameState s, GameConfig cfg)` = αρχικές τιμές + `biographyDeltas` + τάξη → πορτοφόλι. Καθαρή συνάρτηση, test.
- `Presentation.cs` 306–314 (βήματα βιογραφίας), 349–354 (`summary`), `BiographyIdentity.cs` 143 και 207–210: διαβάζουν από το preview. Έξι θέσεις `metric.0..5` στη σκηνή → σειρά **Πειθώ, Ηθική, Συνέπεια, Παρρησία, Αναγνωρισιμότητα, Πορτοφόλι** (η έκτη θέση παίρνει το πορτοφόλι αντί της Αυθεντίας, που στην αρχή είναι πάντα 0 και δεν λέει τίποτα). Οι ετικέτες (`Copy_5c3117cb…` κ.ά. στο `MetricDeltas`) ενημερώνονται αντίστοιχα.
- `InitialiseAgoraCitizen` γίνεται: `State.citizen = CitizenPreview(State, cfg)` — μία γραμμή, ίδιο αποτέλεσμα με τη βιογραφία. Η πόλη όπως τώρα: `new CityState(cityStart)`.
- `profile` ήδη σωστό· να δείξει και πορτοφόλι.

### 5. Το HUD της πόλης δεν δημιουργείται από κώδικα
- `PresentationPolicy.cs` 59–68: το `cityMetricsHUD` χτίζεται με `CivicUIFactory.Text(...)` σε runtime. Παραβαίνει τον κανόνα «κανένα UI από κώδικα». Λύση: ένα TMP_Text στη σκηνή με key `map.city-metrics`, και εδώ μόνο `SetText`. (Σκηνή: ρώτα πρώτα.)
- Ο ΙΔ δεν εμφανίζεται ήδη — σωστό.

### 6. Τηλεμετρία
- `ResearchTelemetry` / `ChoiceRecord`: όπου καταγράφονται `metrics`/`personalFunds`/`cityFunds` της βιογραφίας, να καταγράφονται τα δέλτα του νέου σχήματος (ώστε η εξαγωγή `Tools/Research` να έχει ένα σχήμα). Έλεγξε και το `Tools/Research` export για τα παλιά ονόματα πεδίων.

### 7. Tests
- `CitizenPreviewTests`: (α) χωρίς επιλογές → ακριβώς τα `GameConfig` defaults και το πορτοφόλι της τάξης· (β) επιλογή με `ethos −10` → Ηθική 50· (γ) clamp 0–100· (δ) καμία επιλογή βιογραφίας δεν έχει επίδραση στην πόλη (content test πάνω στο `ContentLibrary`).

## Τι ΔΕΝ αλλάζει
`AssemblyRules`, `EndingRules`, `GameConfig` (εκτός αν προστεθεί κάτι για το preview), οι 20 υποθέσεις, η ροή χάρτη, τα ερωτηματολόγια.

## Σειρά
0 (commit της τρέχουσας δουλειάς) → 1 → 2 → 4 → 3 → 5 → 6 → 7. Μετά από το 4: Play από Boot, δημιουργία χαρακτήρα, έλεγχος ότι οι αριθμοί της τελευταίας οθόνης βιογραφίας = οι αριθμοί του προφίλ στην πρώτη εβδομάδα.
