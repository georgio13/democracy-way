using UnityEngine;

namespace Agora
{
    /// <summary>Λογογράφος: αγοράζεις λόγο ποιότητας L έναντι κόστους.</summary>
    [CreateAssetMenu(menuName = "Agora/Logographer", fileName = "Logographer_")]
    public class LogographerData : ScriptableObject
    {
        public string displayName;       // «Δείναρχος»
        [Range(0, 9)] public int quality; // L: 0 (μόνος), 3, 6, 9
        public int costDrachmas;          // 0, 20, 60, 150
        [TextArea(2, 5)] public string bio;
    }
}
