using UnityEngine;
using UnityEngine.EventSystems;

namespace DemocracyWay.NewUI
{
    [DisallowMultipleComponent]
    public sealed class FullscreenPointerHandler : MonoBehaviour, IPointerDownHandler
    {
        public GameFlowController app;
        public void OnPointerDown(PointerEventData eventData)
        {
            if (app == null || !GameFlowController.IsWeb || eventData.button != PointerEventData.InputButton.Left)
                return;
            UnityEngine.UI.Selectable control = GetComponent<UnityEngine.UI.Selectable>();
            if (control != null && control.IsInteractable())
                app.ToggleFullscreen();
        }
    }
}
