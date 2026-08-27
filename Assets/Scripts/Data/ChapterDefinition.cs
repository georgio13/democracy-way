using UnityEngine;

namespace DemocracyWay.Data
{
    /// <summary>
    /// Everything one story scene needs, in one author-owned asset: the title
    /// card text, which Unity scene to load, its look and sound, and its
    /// dialogue. Creating the next chapter = duplicating the template scene +
    /// creating one of these + one DialogueTree (see Docs/NEA_SKINI.md).
    /// </summary>
    [CreateAssetMenu(fileName = "ChapterDefinition", menuName = "DemocracyWay/Chapter Definition")]
    public class ChapterDefinition : ScriptableObject
    {
        [Tooltip("Σταθερό αναγνωριστικό κεφαλαίου — μπαίνει στα saves και στο analytics log.")]
        public string chapterId = "";

        [Tooltip("Ο τίτλος που εμφανίζεται στη μαύρη οθόνη πριν τη σκηνή.")]
        public string title = "";

        [Tooltip("Το όνομα της Unity σκηνής του κεφαλαίου (πρέπει να είναι στα Build Settings).")]
        public string sceneName = "";

        [Header("Εμφάνιση & ήχος")]
        [Tooltip("Το background της σκηνής.")]
        public Sprite background;

        [Tooltip("Ambient μουσική που παίζει σε loop σε όλη τη σκηνή.")]
        public AudioClip ambientMusic;

        [Header("Διάλογος")]
        [Tooltip("Το δέντρο διαλόγου της σκηνής.")]
        public DialogueTree dialogue;

        [Tooltip("Δευτερόλεπτα μετά το fade-in μέχρι να ξεκινήσει ο διάλογος.")]
        [Range(0f, 10f)]
        public float dialogueStartDelay = 1.5f;

        [Header("Ροή")]
        [Tooltip("Το επόμενο κεφάλαιο όταν τελειώσει ο διάλογος. Κενό = μένει στη σκηνή.")]
        public ChapterDefinition nextChapter;
    }
}
