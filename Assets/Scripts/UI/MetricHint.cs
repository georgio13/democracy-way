using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Tooltip μιας μετρικής του HUD: στο hover δείχνει κάτω από το κελί πώς ανεβαίνει και πώς πέφτει.
    /// Το πλαίσιο είναι κοινό αντικείμενο της σκηνής· το κείμενο το γράφει ο GameFlowController από το περιεχόμενο.
    /// </summary>
    public sealed class MetricHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public RectTransform panel;   // το κοινό πλαίσιο, στον «Map world» ώστε να σχεδιάζεται πάνω από τον χάρτη
        public TMP_Text display;
        [TextArea] public string text;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (panel == null || display == null || string.IsNullOrEmpty(text)) return;
            display.text = text;
            panel.gameObject.SetActive(true);
            panel.SetAsLastSibling();
            var cell = (RectTransform)transform;
            panel.position = cell.TransformPoint(new Vector3(cell.rect.xMin, cell.rect.yMin - 6f, 0f));
            float width = panel.sizeDelta.x - 28f;
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, display.GetPreferredValues(text, width, 0).y + 24f);
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
