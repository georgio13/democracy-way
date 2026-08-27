using UnityEngine;
using DemocracyWay.Core;

namespace DemocracyWay.Data
{
    /// <summary>
    /// The one asset that holds every design knob and every content reference.
    /// The ServicesRoot carries a reference to this; everything else reaches it
    /// through <c>ServicesRoot.Config</c>. Author-owned — edit freely.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "DemocracyWay/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("Ημερολόγιο")]
        [Tooltip("Βδομάδες ανά πρυτανεία. Ιστορικά ~5 (35–36 μέρες).")]
        [Range(1, 10)]
        public int weeksPerPrytany = 5;

        [Header("Αρχικές τιμές δεικτών")]
        public IndicatorSet startingIndicators = new IndicatorSet();

        [Header("Δείκτης ποντικιού")]
        [Tooltip("Η εικόνα του cursor. Import ως Texture Type: Cursor.")]
        public Texture2D cursorTexture;

        [Tooltip("Το 'ενεργό σημείο' του cursor σε pixels από πάνω-αριστερά.")]
        public Vector2 cursorHotspot = Vector2.zero;

        [Header("Περιεχόμενο")]
        public CreationDatabase creationDatabase;
        public IndicatorCatalog indicatorCatalog;
        public ComicSequence introComic;

        [Tooltip("Το πρώτο κεφάλαιο μετά το intro comic.")]
        public ChapterDefinition firstChapter;

        [Header("Ήχος")]
        [Tooltip("Ambient μουσική του κεντρικού μενού.")]
        public AudioClip mainMenuMusic;

        [Tooltip("Ήχος όταν ο κέρσορας περνά πάνω από κουμπί/επιλογή.")]
        public AudioClip uiHoverSfx;

        [Tooltip("Ήχος όταν πατιέται κουμπί/επιλογή.")]
        public AudioClip uiClickSfx;

        [Header("Μεταβάσεις")]
        [Tooltip("Διάρκεια fade σε/από μαύρο (δευτερόλεπτα).")]
        [Range(0.1f, 3f)]
        public float fadeDuration = 0.6f;

        [Tooltip("Πόσο μένει ο τίτλος κεφαλαίου πάνω στο μαύρο (δευτερόλεπτα).")]
        [Range(0f, 5f)]
        public float chapterTitleHold = 1.8f;

        [Tooltip("Ελάχιστη διάρκεια loading screen ώστε να μην αναβοσβήνει (δευτερόλεπτα).")]
        [Range(0f, 2f)]
        public float minLoadingTime = 0.4f;

        /// <summary>
        /// Finds a chapter by id by walking the firstChapter → nextChapter
        /// chain (used when loading a save). Null when the id is unknown.
        /// </summary>
        public ChapterDefinition FindChapter(string chapterId)
        {
            var current = firstChapter;
            for (int hops = 0; current != null && hops < 100; hops++)
            {
                if (current.chapterId == chapterId) return current;
                current = current.nextChapter;
            }
            return null;
        }
    }
}
