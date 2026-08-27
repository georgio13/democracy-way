# Πώς στήνεις το επόμενο κεφάλαιο

Χρόνος: ~10 λεπτά. Δεν γράφεις κώδικα — μόνο assets στον Inspector.
(Τα βήματα 1–2 μπορούμε και να τα κάνουμε μαζί: μου λες τι θες να συμβαίνει
στη σκηνή και στήνουμε μαζί το δέντρο και τη σκηνή.)

## 1. Το δέντρο διαλόγου

1. Project window → `Assets/Data/Dialogues/` → δεξί κλικ →
   **Create ▸ DemocracyWay ▸ Dialogue Tree** → ονόμασέ το π.χ. `Chapter02Dialogue`.
2. Στον Inspector πρόσθεσε κόμβους στη λίστα **Nodes**. Κάθε κόμβος:
   - **Id**: μοναδικό μέσα στο δέντρο (π.χ. `intro`, `agora_1`)
   - **Speaker Name / Portrait / Text / Voice Clip**: ό,τι βλέπει κι ακούει ο παίκτης
   - **Next Node Id**: πού συνεχίζει — ή άφησέ το κενό και βάλε **Choices**
   - Κάθε **Choice**: κείμενο κουμπιού, `Next Node Id`, μεταβολές δεικτών
     (**Effects**), flags, και προαιρετικά **Advance Week** (= περνάει βδομάδα
     → γίνεται autosave)
3. Κόμβος με κενό Next Node Id και καθόλου Choices = **τέλος διαλόγου**.
   Αν γράψεις λάθος id, θα δεις warning στην Κονσόλα μόλις κάνεις κλικ αλλού
   (το ελέγχει το OnValidate του δέντρου).

## 2. Το κεφάλαιο

1. `Assets/Data/Chapters/` → **Create ▸ DemocracyWay ▸ Chapter Definition** →
   `Chapter02`.
2. Συμπλήρωσε: **Chapter Id** (`ch02`), **Title** (ο τίτλος στη μαύρη οθόνη),
   **Scene Name** (`Chapter02` — το όνομα της σκηνής του βήματος 3),
   **Background**, **Ambient Music**, **Dialogue** → το `Chapter02Dialogue`.

## 3. Η σκηνή

1. Στο Project window: `Assets/Scenes/Chapter01.unity` → **Ctrl+D** →
   μετονόμασε το αντίγραφο σε `Chapter02.unity` και άνοιξέ το.
2. Στο Hierarchy διάλεξε το αντικείμενο **StorySceneController** και στο πεδίο
   **Chapter** σύρε το `Chapter02` asset. Αυτό είναι όλο το «λογικό» στήσιμο —
   background και μουσική τα τραβάει από το κεφάλαιο.
3. Ό,τι θες οπτικά διαφορετικό (άλλη διάταξη, έξτρα εικόνες) το κάνεις
   ελεύθερα εδώ — η σκηνή σού ανήκει.
4. **File ▸ Build Profiles** (ή Build Settings) → **Add Open Scenes** για να
   μπει το `Chapter02` στη λίστα.

## 4. Η σύνδεση

Άνοιξε το `Assets/Data/Chapters/Chapter01` και στο **Next Chapter** σύρε το
`Chapter02`. Όταν τελειώσει ο διάλογος του πρώτου κεφαλαίου, το παιχνίδι θα
μεταβεί μόνο του: μαύρο → τίτλος «Chapter02.Title» με fade → νέα σκηνή →
διάλογος μετά το delay.

## Τι γίνεται αυτόματα (δεν χρειάζεται να το σκεφτείς)

- Ο τίτλος κεφαλαίου, το fade, το loading screen — από το SceneFlowService.
- `chapter_started` στο analytics log + checkpoint save — από το SessionService.
- Το ESC/pause δουλεύει γιατί το StorySceneController το δηλώνει στη σκηνή.
- Autosave σε κάθε Advance Week των επιλογών.
- Αν το Scene Name είναι λάθος ή λείπει από τα Build Settings, θα δεις καθαρό
  error στην Κονσόλα και το παιχνίδι δεν κολλάει σε μαύρη οθόνη.
