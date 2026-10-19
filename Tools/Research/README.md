# Εξαγωγή πραγματικών δεδομένων αξιολόγησης

Το `pull_firestore_events.py` διαβάζει τα ερευνητικά συμβάντα και δημιουργεί τοπικό JSON κατάλληλο για το υπάρχον `export_evaluation.py`. Δεν έχει εκτελεστεί σε πραγματικά δεδομένα: ελέγχθηκε μόνο με συνθετικές εγγραφές και mock αιτήματα, όπως αναφέρεται στο `Admin-Pull-QA.json`.

## 1. Ετοίμασε την πρόσβαση

Χρειάζεσαι Python 3.10+ και το [Google Cloud CLI](https://cloud.google.com/sdk/docs/install). Αν έχεις ήδη συνδέσει στο CLI τον δικό σου λογαριασμό που διαθέτει πρόσβαση στο project, χρησιμοποίησέ τον. Διαφορετικά εκτέλεσε εσύ:

```powershell
gcloud auth login
```

Ο λογαριασμός χρειάζεται δικαιώματα ανάγνωσης Firestore, όπως ο ρόλος `roles/datastore.viewer`, ή ισοδύναμα υπάρχοντα δικαιώματα διαχειριστή. Δεν χρειάζεται service-account key. Η διαχειριστική πρόσβαση με Google OAuth αξιολογείται μέσω IAM και δεν απαιτεί να ανοίξεις τους κανόνες πρόσβασης του παιχνιδιού. [Firestore REST και έλεγχος πρόσβασης](https://firebase.google.com/docs/firestore/use-rest-api), [δικαιώματα IAM Firestore](https://docs.cloud.google.com/firestore/native/docs/security/iam).

Το script καλεί εσωτερικά `gcloud auth print-access-token` και κρατά την έξοδο μόνο στη μνήμη. **Μην εκτελέσεις αυτή την εντολή χωριστά για να αντιγράψεις token και μην στείλεις token ή κλειδί σε συνομιλία.** [Τεκμηρίωση της εντολής gcloud](https://docs.cloud.google.com/sdk/gcloud/reference/auth/print-access-token).

## 2. Κράτησε τα ακριβή αρχεία της έκδοσης που αξιολογείται

Πριν αρχίσουν οι συμμετέχοντες, αντέγραψε τα δύο παρακάτω αρχεία από την **επιτυχώς δημοσιευμένη έκδοση που έγινε build** σε ιδιωτικό φάκελο. Μην τα αντικαταστήσεις με μεταγενέστερη έκδοση και μην αλλάξεις τη μορφοποίησή τους:

- `Assets/Data/Gameplay/CampaignContent.json`
- `Assets/Resources/Narrative/EvaluationContent.json`

Παράδειγμα PowerShell:

```powershell
$study = 'C:\Users\giorg\Documents\ResearchPrivate\Study-v3'
New-Item -ItemType Directory -Force -Path "$study\Inputs", "$study\Exports" | Out-Null
Copy-Item -LiteralPath 'C:\Projects\democracy-way\Assets\Data\Gameplay\CampaignContent.json' -Destination "$study\Inputs\CampaignContent.json"
Copy-Item -LiteralPath 'C:\Projects\democracy-way\Assets\Resources\Narrative\EvaluationContent.json' -Destination "$study\Inputs\EvaluationContent.json"
```

Η εξαγωγή επιλέγει μόνο συμβάντα με την ίδια **έκδοση τράπεζας υποθέσεων, hash ακριβούς περιεχομένου και έκδοση ερωτηματολογίου**. Το hash είναι αυτό που υπολογίζει και η Web τηλεμετρία. Έτσι δεν αναμειγνύονται σιωπηρά διαφορετικές εκδόσεις, ακόμη κι αν το λεκτικό version έμεινε ίδιο μετά από αλλαγή κειμένου.

## 3. Έλεγξε το πλάνο χωρίς σύνδεση

Από τον φάκελο όπου βρίσκονται τα scripts:

```powershell
python .\pull_firestore_events.py --project democracy-way --campaign-content "$study\Inputs\CampaignContent.json" --evaluation-content "$study\Inputs\EvaluationContent.json" --output "$study\Exports\events.json" --dry-run
```

Το `--dry-run` εμφανίζει μόνο το πλάνο και τις εκδόσεις. Δεν καλεί gcloud, δεν συνδέεται στο Firebase και δεν δημιουργεί αρχεία εξαγωγής.

## 4. Εκτέλεσε την ανάγνωση

Τρέξε την ίδια εντολή χωρίς το `--dry-run`:

```powershell
python .\pull_firestore_events.py --project democracy-way --campaign-content "$study\Inputs\CampaignContent.json" --evaluation-content "$study\Inputs\EvaluationContent.json" --output "$study\Exports\events.json"
```

Πρόσθεσε `--after` και `--before` αν θέλεις να ορίσεις τη χρονική περίοδο της πραγματικής έρευνας και να αποκλείσεις παλιότερες δικές σου δοκιμές. Δώσε πλήρη ώρα με ζώνη: η αρχή περιλαμβάνεται και το τέλος εξαιρείται. Παράδειγμα μορφής: `--after "2026-10-19T15:00:00+03:00"`. **Βάλε την πραγματική ώρα έναρξης· η ώρα του παραδείγματος δεν είναι κριτήριο επιλογής του δείγματός σου.** Η επιλογή χρονικού διαστήματος δεν αναγνωρίζει αυτόματα ποιες συνεδρίες ήταν ερευνητές ή συμμετέχοντες.

Παράγονται:

- `events.json`: μόνο οι επιλεγμένες, περιορισμένες ερευνητικές εγγραφές.
- `events.manifest.json`: έκδοση/hash, σταθερό στιγμιότυπο ανάγνωσης, αριθμοί εγγραφών και λόγοι αποκλεισμού.

Δεν γράφονται ακατέργαστα responses, ονόματα διαδρομών εγγράφων, Authentication UIDs, email ή credentials. Το Firestore επιστρέφει αναγκαστικά το όνομα του εγγράφου για τη σελιδοποίηση, αλλά αυτό χρησιμοποιείται μόνο στη μνήμη. Υπάρχει allowlist πεδίων και περιορισμός της εμφωλευμένης μεταπληροφορίας πριν από κάθε εγγραφή στον δίσκο. Οι ερευνητικοί κωδικοί και οι συνεδρίες διατηρούνται: τα δεδομένα παραμένουν **ψευδωνυμοποιημένα προσωπικά δεδομένα**, όχι ανώνυμα. Μην ανεβάσεις τα αρχεία εξαγωγής σε δημόσιο repository.

Η ανάγνωση χρησιμοποιεί collection-group `events` με `allDescendants`, επομένως δεν εξαρτάται από το αν υπάρχει έγγραφο-γονέας `participants/{uid}`. Επιλέγει τοπικά μόνο ακριβείς διαδρομές `participants/*/events/*`. Η σελιδοποίηση γίνεται με `limit`, ταξινόμηση κατά `__name__` και cursor μετά το τελευταίο έγγραφο. Όλες οι σελίδες διαβάζονται στο ίδιο server `readTime`. Το HTTP `POST .../documents:runQuery` εκτελεί ερώτημα ανάγνωσης· το script δεν χρησιμοποιεί write/delete endpoints. [StructuredQuery και cursors](https://firebase.google.com/docs/firestore/reference/rest/v1/StructuredQuery), [runQuery και readTime](https://firebase.google.com/docs/firestore/reference/rest/v1/projects.databases.documents/runQuery).

## 5. Δημιούργησε τους πίνακες ανάλυσης

```powershell
python .\export_evaluation.py --input "$study\Exports\events.json" --output "$study\Exports\CSV" --content "$study\Inputs\EvaluationContent.json"
```

Πρώτα διάβασε τα `Quality.json` και `Summary.json`. Στη συνέχεια:

- `knowledge.csv`: απαντήσεις/ορθότητα, έκδοση, στάδιο και πρόοδος κατά την έναρξη αξιολόγησης.
- `ratings.csv`: οι τελικές τιμές των προσαρμοσμένων δηλώσεων 1–5.
- `decisions.csv`: ψήφοι, δημόσιες παρεμβάσεις, κλίση επιλογής και επιδράσεις. Οι παρατηρήσεις δικαστικών διαδικασιών και οι παλιές αποχές δεν μπαίνουν αυθαίρετα στον παρονομαστή έγκυρων πολιτικών ψήφων.
- `assessment_points.csv`: έναρξη, προσφορά, αναβολή και ολοκλήρωση αξιολόγησης.
- `sessions.csv`: πληρότητα pre/post, βαθμολογίες μόνο για πλήρη ερωτηματολόγια και αριθμός υποθέσεων κατά το post.

Μην αναμείξεις χωρίς αναφορά συμμετέχοντες που αξιολογήθηκαν μετά από πέντε υποθέσεις με όσους έπαιξαν περισσότερες. Το `active_seconds` είναι σωρευτικός ενεργός χρόνος παιχνιδιού, όχι αυτομάτως χρόνος απάντησης μιας ερώτησης. Δες τα αποθηκευμένα σημεία έναρξης/υποβολής και τη μεταβλητή έκθεσης `biography+baseline-feedback`. Η αρχική ανατροφοδότηση αποτελεί ήδη μέρος της μάθησης. Οι δώδεκα δηλώσεις εμπειρίας και οι δύο πρόθεσης δεν είναι επικυρωμένη κλίμακα, και η κλίση των επιλογών δεν αποδεικνύει πραγματική πολιτική ιδεολογία ή μεταβολή πραγματικής συμμετοχής.

## Συνήθη προβλήματα

- **403:** έλεγξε ποιος λογαριασμός είναι ενεργός στο gcloud και τα IAM δικαιώματά του. Μην αλλάξεις σε δημόσια ανάγνωση τους κανόνες του παιχνιδιού. Αν απαιτείται quota project, χρησιμοποίησε `--quota-project democracy-way` μόνο με το αντίστοιχο δικαίωμα `serviceusage.services.use`. [Προϋπόθεση quota project](https://docs.cloud.google.com/sdk/gcloud/reference/auth/print-access-token).
- **Μηδέν συμβάντα:** έλεγξε το manifest για αποκλεισμούς λόγω έκδοσης/hash, χρονικού παραθύρου ή απουσίας καταγεγραμμένης συγκατάθεσης. Χρησιμοποίησε τα αρχεία του πραγματικού build, όχι τα πιο πρόσφατα αρχεία ανάπτυξης. Μηδενική εξαγωγή δεν σημαίνει αυτομάτως μηδενικές συνεδρίες.
- **Υπάρχον αρχείο:** διάλεξε νέο όνομα ή δώσε συνειδητά `--overwrite`. Δεν αντικαθίστανται σιωπηρά προηγούμενα δεδομένα.
- **Διακοπή ή όριο εγγραφών:** δεν παραδίδεται μερικό αποτέλεσμα σαν πλήρες. Επανάλαβε την εξαγωγή και έλεγξε το τελικό manifest. Οι αναγνώσεις Firebase μπορεί να καταναλώνουν το διαθέσιμο quota του project.

Η εξαγωγή περιέχει μόνο συμβάντα με καταγεγραμμένη συγκατάθεση τη στιγμή της συλλογής. Δεν μπορεί από μόνη της να γνωρίζει μεταγενέστερο αίτημα διαγραφής ή να αποδείξει ότι ο διαχειριστής το εφάρμοσε. Τήρησε χωριστά τη δηλωμένη πολιτική διατήρησης και τα σχετικά αιτήματα.
