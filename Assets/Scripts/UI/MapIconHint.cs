using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Tooltip για τα εικονίδια της πόλης στον χάρτη: στο hover εμφανίζει δίπλα στο εικονίδιο
    /// το όνομά του. Προστίθεται με AddComponent σε υπάρχοντα κουμπιά της σκηνής — όχι νέο UI
    /// (ίδιο μοτίβο με LexiconTermClicks).
    /// </summary>
    public sealed class MapIconHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string label;
        public RectTransform panel;   // το κοινό πλαίσιο του tooltip
        public TMP_Text display;      // το κείμενό του

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (panel == null || display == null) return;
            display.text = label ?? "";
            var rect = (RectTransform)transform;
            panel.anchoredPosition = new Vector2(rect.anchoredPosition.x - 24f, rect.anchoredPosition.y - rect.sizeDelta.y * 0.5f + 22f);
            panel.gameObject.SetActive(true);
            panel.SetAsLastSibling();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (panel != null) panel.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            if (panel != null) panel.gameObject.SetActive(false);
        }
    }
}
