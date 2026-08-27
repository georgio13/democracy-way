using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.UI
{
    /// <summary>
    /// A small text bubble (background + TMP text) that follows the pointer,
    /// clamped inside the canvas so long indicator descriptions never run off
    /// screen. Deliberately NOT a singleton: each HUD/menu holds a serialized
    /// reference to its own instance, so two canvases can never fight over one
    /// shared tooltip. Neither graphic is a raycast target — a tooltip that
    /// caught the pointer would flicker by blocking its own hover source.
    /// The GameObject is saved inactive; callers use Show/Hide.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Tooltip View")]
    [DisallowMultipleComponent]
    public class TooltipView : MonoBehaviour
    {
        [Tooltip("Το Image φόντου του tooltip.")]
        [SerializeField] private Image background;

        [Tooltip("Το κείμενο του tooltip.")]
        [SerializeField] private TMP_Text text;

        [Tooltip("Μετατόπιση από τον δείκτη σε pixels (θετικό x = δεξιά, αρνητικό y = κάτω).")]
        [SerializeField] private Vector2 pointerOffset = new Vector2(18f, -18f);

        [Tooltip("Ελάχιστη απόσταση του tooltip από τις άκρες του canvas σε pixels.")]
        [SerializeField] private float edgePadding = 8f;

        private RectTransform rectTransform;
        private Canvas rootCanvas;

        void Awake()
        {
            rectTransform = (RectTransform)transform;

            // Enforced in code: a raycast-catching tooltip steals the hover
            // that opened it and flickers forever.
            if (background != null) background.raycastTarget = false;
            if (text != null) text.raycastTarget = false;
        }

        /// <summary>
        /// Shows <paramref name="tooltipText"/> next to
        /// <paramref name="screenPosition"/> (e.g. Mouse.current.position),
        /// keeping the whole panel inside the canvas.
        /// </summary>
        public void Show(string tooltipText, Vector2 screenPosition)
        {
            if (rectTransform == null) rectTransform = (RectTransform)transform;
            if (text != null) text.text = tooltipText;
            gameObject.SetActive(true);

            // The text just changed, but layout normally waits for end of
            // frame — rebuild now so clamping sees the REAL size, not last
            // tooltip's size for one flickering frame.
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

            if (rootCanvas == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                rootCanvas = canvas != null ? canvas.rootCanvas : null;
            }
            if (rootCanvas == null) return;   // no canvas: nothing sensible to clamp against

            var canvasRect = (RectTransform)rootCanvas.transform;
            var camera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPosition, camera, out Vector2 local);
            local += pointerOffset;

            // Clamp so the whole rect (measured from its pivot) stays inside
            // the canvas, with a small margin.
            Rect area = canvasRect.rect;
            Vector2 size = rectTransform.rect.size;
            Vector2 pivot = rectTransform.pivot;
            float minX = area.xMin + edgePadding + size.x * pivot.x;
            float maxX = area.xMax - edgePadding - size.x * (1f - pivot.x);
            float minY = area.yMin + edgePadding + size.y * pivot.y;
            float maxY = area.yMax - edgePadding - size.y * (1f - pivot.y);
            if (minX <= maxX) local.x = Mathf.Clamp(local.x, minX, maxX);
            if (minY <= maxY) local.y = Mathf.Clamp(local.y, minY, maxY);

            // Place via world space: the clamped point was computed in CANVAS
            // local space, and going through TransformPoint keeps it correct
            // even when the tooltip is nested deeper than the canvas root.
            rectTransform.position = canvasRect.TransformPoint(new Vector3(local.x, local.y, 0f));
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
