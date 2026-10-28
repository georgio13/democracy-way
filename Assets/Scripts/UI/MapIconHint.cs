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
            // Το πλαίσιο ακολουθεί το πλάτος της λέξης — όχι σταθερά 300 με κενό αριστερά.
            // Η μέτρηση γίνεται με το panel ενεργό (σε ανενεργό TMP μπορεί να βγει μηδενική).
            float width = display.GetPreferredValues(display.text).x + 36f;
            panel.sizeDelta = new Vector2(Mathf.Max(120f, width), panel.sizeDelta.y);
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
