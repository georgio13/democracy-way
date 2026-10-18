using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    public sealed class DWHoverShift : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public RectTransform label;
        public float distance = 12f;
        private Vector2 origin;
        private bool hovered;
        private bool initialized;
        private void OnEnable()
        {
            if (label == null)
                return;
            if (!initialized)
            {
                origin = label.anchoredPosition;
                initialized = true;
            }

            label.anchoredPosition = origin;
            hovered = false;
        }

        public void OnPointerEnter(PointerEventData data)
        {
            Selectable b = GetComponent<Selectable>();
            hovered = b == null || b.IsInteractable();
        }

        public void OnPointerExit(PointerEventData data)
        {
            hovered = false;
        }

        private void Update()
        {
            if (label == null)
                return;
            Vector2 target = origin + Vector2.right * (hovered ? distance : 0f);
            label.anchoredPosition = Vector2.Lerp(label.anchoredPosition, target, 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime));
        }

        private void OnDisable()
        {
            if (label != null && initialized)
                label.anchoredPosition = origin;
        }
    }
}
