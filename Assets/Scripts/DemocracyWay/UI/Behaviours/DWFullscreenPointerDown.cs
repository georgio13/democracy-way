using UnityEngine;
using UnityEngine.EventSystems;

namespace DemocracyWay.NewUI
{
    [DisallowMultipleComponent]
    public sealed class DWFullscreenPointerDown : MonoBehaviour, IPointerDownHandler
    {
        public DWApp app;
        public void OnPointerDown(PointerEventData eventData)
        {
            if (app == null || !DWApp.IsWeb || eventData.button != PointerEventData.InputButton.Left)
                return;
            UnityEngine.UI.Selectable control = GetComponent<UnityEngine.UI.Selectable>();
            if (control != null && control.IsInteractable())
                app.ToggleFullscreen();
        }
    }
}
