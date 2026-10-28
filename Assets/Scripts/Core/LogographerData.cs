using UnityEngine;

namespace Agora
{
    /// <summary>Ο λογογράφος (Δείναρχος ο Κορίνθιος). Κόστος και πόντοι λόγου στο GameConfig.</summary>
    [CreateAssetMenu(menuName = "Agora/Logographer", fileName = "Logographer_")]
    public class LogographerData : ScriptableObject
    {
        public string displayName;       // «Δείναρχος»
        [TextArea(2, 5)] public string bio;
        [TextArea(2, 4)] public string openingTemplate;   // προοίμιο στο ύφος του, με {side} για την πλευρά
        [TextArea(2, 4)] public string closingTemplate;   // επίλογος στο ύφος του, με {side} για την πλευρά
    }
}
