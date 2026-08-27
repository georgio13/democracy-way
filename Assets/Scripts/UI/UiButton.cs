using System.Collections.Generic;
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

        [Tooltip("Προαιρετικά γραφικά που εμφανίζονται μαζί με το border στο hover (π.χ. διακοσμητικά dividers). Κενό = ψάχνει παιδιά 'LeftDivider' και 'RightDivider'.")]
        [SerializeField] private Graphic[] hoverDecorations;

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

        // Hover alpha is animated manually in Update (not via coroutine) so a
        // disable/enable mid-fade can never leave the visuals half-visible.
        // One value drives the border AND every decoration together.
        private bool isHovering;
        private float hoverAlpha;
        private float hoverTargetAlpha;

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
                    // delete) — drop the hover state so the visuals aren't frozen on.
                    isHovering = false;
                    hoverTargetAlpha = 0f;
                    hoverAlpha = 0f;
                    ApplyHoverAlpha();
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
            if (hoverDecorations == null || hoverDecorations.Length == 0)
            {
                var found = new List<Graphic>(2);
                foreach (var childName in new[] { "LeftDivider", "RightDivider" })
                {
                    var child = transform.Find(childName);
                    if (child != null && child.TryGetComponent(out Graphic graphic))
                        found.Add(graphic);
                }
                hoverDecorations = found.ToArray();
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

            hoverAlpha = 0f;
            hoverTargetAlpha = 0f;
            ApplyHoverAlpha();
            // Respect whatever `interactable` already is — a controller's Awake
            // may have disabled the button before ours ran.
            ApplyLabelState();
        }

        void OnDisable()
        {
            // Panels hide with SetActive(false) — never keep a stale hover
            // when the panel comes back.
            isHovering = false;
            hoverAlpha = 0f;
            hoverTargetAlpha = 0f;
            ApplyHoverAlpha();
            ApplyLabelState();
        }

        void Update()
        {
            bool hasVisuals = border != null || (hoverDecorations != null && hoverDecorations.Length > 0);
            if (!hasVisuals || Mathf.Approximately(hoverAlpha, hoverTargetAlpha)) return;

            // Unscaled so the hover fade works while the game is paused.
            float step = hoverFadeDuration <= 0f ? 1f : Time.unscaledDeltaTime / hoverFadeDuration;
            hoverAlpha = Mathf.MoveTowards(hoverAlpha, hoverTargetAlpha, step);
            ApplyHoverAlpha();
        }

        private void ApplyHoverAlpha()
        {
            // Null-tolerant: a controller's Awake may toggle Interactable
            // before this button's own Awake has filled the references.
            if (border != null) SetAlpha(border, hoverAlpha);
            if (hoverDecorations == null) return;
            foreach (var decoration in hoverDecorations)
                if (decoration != null) SetAlpha(decoration, hoverAlpha);
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            var c = graphic.color;
            c.a = alpha;
            graphic.color = c;
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
            hoverTargetAlpha = hovered ? 1f : 0f;
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
