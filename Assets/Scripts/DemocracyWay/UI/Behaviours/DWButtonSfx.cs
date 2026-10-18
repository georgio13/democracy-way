using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class DWButtonSfx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public DWApp app;
        private Button button;
        private bool pointerInside;
        private bool selected;
        private bool audibleFocus;
        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(Click);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(Click);
        }

        private void OnDisable()
        {
            pointerInside = false;
            selected = false;
            audibleFocus = false;
        }

        private void Click()
        {
            if (app != null)
                app.PlayClick();
        }

        private void UpdateFocus(bool canSound)
        {
            bool next = button != null && button.IsInteractable() && (pointerInside || selected);
            if (next && !audibleFocus && canSound && app != null)
                app.PlayHover();
            audibleFocus = next;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            UpdateFocus(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            UpdateFocus(false);
        }

        public void OnSelect(BaseEventData eventData)
        {
            selected = true;
            UpdateFocus(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
            UpdateFocus(false);
        }
    }
}
