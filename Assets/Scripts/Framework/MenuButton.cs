using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DemocracyWay.Framework
{
    /// <summary>
    /// Unified button component for MainMenu, PauseMenu, Settings and all menu UI.
    /// No background image; transparent. Shows a border on hover, plays hover/click SFX
    /// via <see cref="AudioManager"/>. Attach to a GameObject with at least a TMP label
    /// child ("Label") and a border Image child ("Border") that starts invisible.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Menu Button")]
    [DisallowMultipleComponent]
    public class MenuButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler,
        IPointerClickHandler,
        ISelectHandler, IDeselectHandler
    {
        [Header("References")]
        [Tooltip("Border Image child that fades in on hover. Leave null to auto-find child named 'Border'.")]
        [SerializeField] private Image border;

        [Tooltip("TMP label child. Leave null to auto-find child TMP_Text.")]
        [SerializeField] private TMP_Text label;

        [Tooltip("Optional raycast target image for hit detection. Leave null to auto-add transparent Image on this GameObject.")]
        [SerializeField] private Image hitTarget;

        [Header("Hover Animation")]
        [Tooltip("Seconds for border alpha to fade in/out on hover.")]
        [SerializeField] private float hoverFadeDuration = 0.12f;

        [Tooltip("Label color when idle.")]
        [SerializeField] private Color labelIdleColor = Color.white;

        [Tooltip("Label color when hovered/selected.")]
        [SerializeField] private Color labelHoverColor = new Color(1f, 0.92f, 0.65f, 1f);

        [Tooltip("Label color when the button is not interactable (e.g. Continue with no save).")]
        [SerializeField] private Color labelDisabledColor = new Color(0.32f, 0.30f, 0.26f, 1f);

        [Header("Interaction")]
        [SerializeField] private bool interactable = true;

        [Header("Events")]
        public UnityEvent onClick = new();

        // Runtime state
        private bool isHovering;
        private float borderAlpha;
        private float borderTargetAlpha;

        public bool Interactable
        {
            get => interactable;
            set
            {
                interactable = value;
                if (label != null)
                {
                    // Both recolor and dim so the button is unambiguously "off"
                    // even on backgrounds where the dark colour alone blends in.
                    label.color = value ? labelIdleColor : labelDisabledColor;
                    label.alpha = value ? 1f : 0.75f;
                }
                if (border != null && !value)
                {
                    // Make sure a stale hover border isn't frozen visible when
                    // a button flips from interactable → disabled at runtime.
                    var c = border.color;
                    c.a = 0f;
                    border.color = c;
                    borderAlpha = 0f;
                    borderTargetAlpha = 0f;
                }
            }
        }

        public string Text
        {
            get => label != null ? label.text : string.Empty;
            set { if (label != null) label.text = value; }
        }

        void Reset()
        {
            // Auto-wire on Add Component
            if (border == null)
            {
                var borderTransform = transform.Find("Border");
                if (borderTransform != null) border = borderTransform.GetComponent<Image>();
            }
            if (label == null) label = GetComponentInChildren<TMP_Text>();
            if (hitTarget == null) hitTarget = GetComponent<Image>();
        }

        void Awake()
        {
            // Auto-wire if serialized fields are empty at runtime
            if (border == null)
            {
                var borderTransform = transform.Find("Border");
                if (borderTransform != null) border = borderTransform.GetComponent<Image>();
            }
            if (label == null) label = GetComponentInChildren<TMP_Text>();

            // Ensure a raycast target exists — if no Image on this GO, add a transparent one.
            if (hitTarget == null)
            {
                hitTarget = GetComponent<Image>();
                if (hitTarget == null)
                {
                    hitTarget = gameObject.AddComponent<Image>();
                    hitTarget.color = new Color(0, 0, 0, 0); // transparent but raycastable
                }
            }
            hitTarget.raycastTarget = true;

            // Initialize visuals — respect whatever `interactable` is at
            // Awake time, including values a sibling controller may have
            // written earlier in its own Awake. If we blindly wrote
            // labelIdleColor here we'd clobber a pre-set disabled state.
            if (border != null)
            {
                var c = border.color;
                c.a = 0f;
                border.color = c;
            }
            if (label != null)
            {
                label.color = interactable ? labelIdleColor : labelDisabledColor;
                label.alpha = interactable ? 1f : 0.75f;
            }
            borderAlpha = 0f;
            borderTargetAlpha = 0f;
        }

        void Update()
        {
            if (border == null) return;
            if (Mathf.Approximately(borderAlpha, borderTargetAlpha)) return;

            float step = (hoverFadeDuration <= 0f) ? 1f : (Time.unscaledDeltaTime / hoverFadeDuration);
            borderAlpha = Mathf.MoveTowards(borderAlpha, borderTargetAlpha, step);
            var c = border.color;
            c.a = borderAlpha;
            border.color = c;
        }

        private void SetHovered(bool hovered)
        {
            if (!interactable) return;
            isHovering = hovered;
            borderTargetAlpha = hovered ? 1f : 0f;
            if (label != null) label.color = hovered ? labelHoverColor : labelIdleColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!interactable) return;
            SetHovered(true);
            AudioManager.Instance?.PlayButtonHover();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHovered(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!interactable) return;
            // Subtle press feedback: nudge label color toward hover color
            if (label != null) label.color = labelHoverColor;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (label == null) return;
            if (!interactable) { label.color = labelDisabledColor; return; }
            label.color = isHovering ? labelHoverColor : labelIdleColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!interactable) return;
            AudioManager.Instance?.PlayButtonClick();
            onClick?.Invoke();
        }

        // Keyboard/gamepad navigation support
        public void OnSelect(BaseEventData eventData) => SetHovered(true);
        public void OnDeselect(BaseEventData eventData) => SetHovered(false);
    }
}
