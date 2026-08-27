using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DemocracyWay.Services;

namespace DemocracyWay.UI
{
    /// <summary>
    /// The one button style of every menu (main menu, pause, settings, slots):
    /// transparent background, a TMP label and a border Image child that fades
    /// in on hover. Uses its own tiny fade instead of uGUI's Button so the
    /// animation runs on unscaled time — the pause menu must still respond
    /// while timeScale is 0. Hover/click SFX go through ServicesRoot.Audio,
    /// with null-checks so the button also works in scenes opened without Boot.
    /// </summary>
    [AddComponentMenu("DemocracyWay/UI Button")]
    [DisallowMultipleComponent]
    public class UiButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
        ISelectHandler, IDeselectHandler
    {
        [Header("Αναφορές")]
        [Tooltip("Το border Image παιδί που εμφανίζεται στο hover. Κενό = ψάχνει παιδί με όνομα 'Border'.")]
        [SerializeField] private Image border;

        [Tooltip("Το TMP label παιδί με το κείμενο του κουμπιού. Κενό = πρώτο TMP_Text στα παιδιά.")]
        [SerializeField] private TMP_Text label;

        [Tooltip("Image για hit detection (μπορεί να είναι πλήρως διάφανο). Κενό = προστίθεται διάφανο αυτόματα.")]
        [SerializeField] private Image hitTarget;

        [Header("Hover")]
        [Tooltip("Δευτερόλεπτα για το fade in/out του border στο hover (unscaled).")]
        [SerializeField] private float hoverFadeDuration = 0.12f;

        [Tooltip("Χρώμα του label σε ηρεμία.")]
        [SerializeField] private Color labelIdleColor = Color.white;

        [Tooltip("Χρώμα του label στο hover ή όταν είναι επιλεγμένο με πληκτρολόγιο.")]
        [SerializeField] private Color labelHoverColor = new Color(1f, 0.92f, 0.65f, 1f);

        [Tooltip("Χρώμα του label όταν το κουμπί είναι απενεργοποιημένο.")]
        [SerializeField] private Color labelDisabledColor = new Color(0.32f, 0.30f, 0.26f, 1f);

        [Header("Αλληλεπίδραση")]
        [Tooltip("Αν ανταποκρίνεται σε ποντίκι/πληκτρολόγιο. Απενεργό = θαμπό label, καμία είσοδος.")]
        [SerializeField] private bool interactable = true;

        [Header("Συμβάντα")]
        public UnityEvent onClick = new UnityEvent();

        // Border alpha is animated manually in Update (not via coroutine) so a
        // disable/enable mid-fade can never leave the border half-visible.
        private bool isHovering;
        private float borderAlpha;
        private float borderTargetAlpha;

        /// <summary>Dimmed and deaf to input when off — used by the main menu
        /// to lock Νέο Παιχνίδι / Φόρτωση and by rows for non-pickable slots.</summary>
        public bool Interactable
        {
            get => interactable;
            set
            {
                interactable = value;
                if (!value)
                {
                    // A button can be disabled while hovered (e.g. after a slot
                    // delete) — drop the hover state so the border isn't frozen on.
                    isHovering = false;
                    borderTargetAlpha = 0f;
                    borderAlpha = 0f;
                    ApplyBorderAlpha();
                }
                ApplyLabelState();
            }
        }

        /// <summary>Player-facing label text (Greek, authored in the scene or set by code).</summary>
        public string Text
        {
            get => label != null ? label.text : string.Empty;
            set { if (label != null) label.text = value; }
        }

        void Awake()
        {
            if (border == null)
            {
                var borderTransform = transform.Find("Border");
                if (borderTransform != null) border = borderTransform.GetComponent<Image>();
            }
            if (label == null) label = GetComponentInChildren<TMP_Text>(true);

            // The button has no visible background, but uGUI needs SOME
            // raycastable Graphic on the clickable area — add a transparent one.
            if (hitTarget == null)
            {
                hitTarget = GetComponent<Image>();
                if (hitTarget == null)
                {
                    hitTarget = gameObject.AddComponent<Image>();
                    hitTarget.color = new Color(0f, 0f, 0f, 0f);
                }
            }
            hitTarget.raycastTarget = true;

            borderAlpha = 0f;
            borderTargetAlpha = 0f;
            ApplyBorderAlpha();
            // Respect whatever `interactable` already is — a controller's Awake
            // may have disabled the button before ours ran.
            ApplyLabelState();
        }

        void OnDisable()
        {
            // Panels hide with SetActive(false) — never keep a stale hover
            // when the panel comes back.
            isHovering = false;
            borderAlpha = 0f;
            borderTargetAlpha = 0f;
            ApplyBorderAlpha();
            ApplyLabelState();
        }

        void Update()
        {
            if (border == null || Mathf.Approximately(borderAlpha, borderTargetAlpha)) return;

            // Unscaled so the hover fade works while the game is paused.
            float step = hoverFadeDuration <= 0f ? 1f : Time.unscaledDeltaTime / hoverFadeDuration;
            borderAlpha = Mathf.MoveTowards(borderAlpha, borderTargetAlpha, step);
            ApplyBorderAlpha();
        }

        private void ApplyBorderAlpha()
        {
            if (border == null) return;
            var c = border.color;
            c.a = borderAlpha;
            border.color = c;
        }

        private void ApplyLabelState()
        {
            if (label == null) return;
            if (!interactable)
            {
                // Recolor AND dim, so "off" reads even on backgrounds where the
                // dark disabled colour alone would blend in.
                label.color = labelDisabledColor;
                label.alpha = 0.75f;
            }
            else
            {
                label.color = isHovering ? labelHoverColor : labelIdleColor;
                label.alpha = 1f;
            }
        }

        private void SetHovered(bool hovered)
        {
            if (!interactable) return;
            isHovering = hovered;
            borderTargetAlpha = hovered ? 1f : 0f;
            ApplyLabelState();
        }

        // ════════ Pointer ════════

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!interactable) return;
            SetHovered(true);
            ServicesRoot.Audio?.PlayUiHover();
        }

        public void OnPointerExit(PointerEventData eventData) => SetHovered(false);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!interactable) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;
            ServicesRoot.Audio?.PlayUiClick();
            onClick?.Invoke();
        }

        // ════════ Keyboard navigation ════════
        // Selection mirrors hover visually so keyboard users see where they are.

        public void OnSelect(BaseEventData eventData) => SetHovered(true);
        public void OnDeselect(BaseEventData eventData) => SetHovered(false);
    }
}
