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
    /// transparent background with a TMP label. Hovering plays a sound and
    /// fades in the optional decoration graphics, which are positioned right
    /// next to the label's actual text width — nothing else changes visually.
    /// The fade runs on its own tiny animation instead of uGUI's Button so it
    /// works on unscaled time — the pause menu must respond while timeScale
    /// is 0. SFX go through ServicesRoot.Audio with null-checks so the button
    /// also works in scenes opened without Boot.
    /// </summary>
    [AddComponentMenu("DemocracyWay/UI Button")]
    [DisallowMultipleComponent]
    public class UiButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
        ISelectHandler, IDeselectHandler
    {
        [Header("Αναφορές")]
        [Tooltip("Διακοσμητικό που εμφανίζεται αριστερά της λέξης στο hover. Κενό = ψάχνει παιδί 'LeftDivider'.")]
        [SerializeField] private Graphic leftDecoration;

        [Tooltip("Διακοσμητικό που εμφανίζεται δεξιά της λέξης στο hover. Κενό = ψάχνει παιδί 'RightDivider'.")]
        [SerializeField] private Graphic rightDecoration;

        [Tooltip("Το TMP label παιδί με το κείμενο του κουμπιού. Κενό = πρώτο TMP_Text στα παιδιά.")]
        [SerializeField] private TMP_Text label;

        [Tooltip("Image για hit detection (μπορεί να είναι πλήρως διάφανο). Κενό = προστίθεται διάφανο αυτόματα.")]
        [SerializeField] private Image hitTarget;

        [Header("Hover")]
        [Tooltip("Δευτερόλεπτα για το fade in/out των διακοσμητικών στο hover (unscaled).")]
        [SerializeField] private float hoverFadeDuration = 0.12f;

        [Tooltip("Απόσταση σε pixels ανάμεσα στην άκρη της λέξης και το διακοσμητικό.")]
        [SerializeField] private float decorationGap = 12f;

        [Tooltip("Χρώμα του label (δεν αλλάζει στο hover).")]
        [SerializeField] private Color labelIdleColor = Color.white;

        [Tooltip("Χρώμα του label όταν το κουμπί είναι απενεργοποιημένο.")]
        [SerializeField] private Color labelDisabledColor = new Color(0.32f, 0.30f, 0.26f, 1f);

        [Header("Αλληλεπίδραση")]
        [Tooltip("Αν ανταποκρίνεται σε ποντίκι/πληκτρολόγιο. Απενεργό = θαμπό label, καμία είσοδος.")]
        [SerializeField] private bool interactable = true;

        [Header("Συμβάντα")]
        public UnityEvent onClick = new UnityEvent();

        // Hover alpha is animated manually in Update (not via coroutine) so a
        // disable/enable mid-fade can never leave the decorations half-visible.
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
                    // delete) — drop the hover state so the decorations aren't frozen on.
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
            set
            {
                if (label == null) return;
                label.text = value;
                // The word changed width — the decorations must hug it again.
                PositionDecorations();
            }
        }

        void Awake()
        {
            if (leftDecoration == null) leftDecoration = FindDecoration("LeftDivider");
            if (rightDecoration == null) rightDecoration = FindDecoration("RightDivider");
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

        private Graphic FindDecoration(string childName)
        {
            var child = transform.Find(childName);
            return child != null ? child.GetComponent<Graphic>() : null;
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
            bool hasVisuals = leftDecoration != null || rightDecoration != null;
            if (!hasVisuals || Mathf.Approximately(hoverAlpha, hoverTargetAlpha)) return;

            // Unscaled so the hover fade works while the game is paused.
            float step = hoverFadeDuration <= 0f ? 1f : Time.unscaledDeltaTime / hoverFadeDuration;
            hoverAlpha = Mathf.MoveTowards(hoverAlpha, hoverTargetAlpha, step);
            ApplyHoverAlpha();
        }

        /// <summary>
        /// Puts each decoration right next to the label's rendered text — the
        /// button is wider than the word, so a fixed offset can't hug labels
        /// of different lengths («Ρυθμίσεις» vs «Φόρτωση Παιχνιδιού»).
        /// </summary>
        private void PositionDecorations()
        {
            if (label == null) return;
            float halfWord = label.preferredWidth * 0.5f;
            Place(leftDecoration, -1f);
            Place(rightDecoration, 1f);

            void Place(Graphic decoration, float side)
            {
                if (decoration == null) return;
                var rt = decoration.rectTransform;
                float x = side * (halfWord + decorationGap + rt.rect.width * 0.5f);
                rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
            }
        }

        private void ApplyHoverAlpha()
        {
            if (leftDecoration != null) SetAlpha(leftDecoration, hoverAlpha);
            if (rightDecoration != null) SetAlpha(rightDecoration, hoverAlpha);
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
                label.color = labelIdleColor;
                label.alpha = 1f;
            }
        }

        private void SetHovered(bool hovered)
        {
            if (!interactable) return;
            isHovering = hovered;
            if (hovered) PositionDecorations(); // the text is final by now
            hoverTargetAlpha = hovered ? 1f : 0f;
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
        // Selection mirrors hover so keyboard users see where they are.

        public void OnSelect(BaseEventData eventData) => SetHovered(true);
        public void OnDeselect(BaseEventData eventData) => SetHovered(false);
    }
}
