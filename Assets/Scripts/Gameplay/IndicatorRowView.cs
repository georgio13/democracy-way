using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DemocracyWay.Core;
using DemocracyWay.Data;
using DemocracyWay.UI;

namespace DemocracyWay.Gameplay
{
    /// <summary>
    /// One indicator line in the top-left HUD: name, value and an optional
    /// fill bar. Spawned per visible catalog entry by
    /// <see cref="IndicatorHudView"/>, which also hands over the shared
    /// tooltip — the row itself only knows how to display and to report
    /// hover, never where its value comes from.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Indicator Row View")]
    [DisallowMultipleComponent]
    public class IndicatorRowView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("Το όνομα του δείκτη.")]
        [SerializeField] private TMP_Text nameText;

        [Tooltip("Η τρέχουσα τιμή του δείκτη (0–100).")]
        [SerializeField] private TMP_Text valueText;

        [Tooltip("Προαιρετική μπάρα πλήρωσης (Image type: Filled). Άφησέ το κενό αν δεν υπάρχει.")]
        [SerializeField] private Image fillImage;

        [Tooltip("Χρώμα της μπάρας όταν η κατάσταση είναι καλή.")]
        [SerializeField] private Color goodColor = new Color(0.55f, 0.72f, 0.35f);

        [Tooltip("Χρώμα της μπάρας όταν η κατάσταση είναι κακή.")]
        [SerializeField] private Color badColor = new Color(0.78f, 0.30f, 0.25f);

        private IndicatorCatalog.Entry entry;
        private TooltipView tooltip;

        /// <summary>The indicator this row displays, for the HUD's refresh loop.</summary>
        public IndicatorId Id => entry != null ? entry.id : default;

        public void Init(IndicatorCatalog.Entry entry, TooltipView tooltip)
        {
            this.entry = entry;
            this.tooltip = tooltip;
            if (nameText != null) nameText.text = entry != null ? entry.displayName : string.Empty;
        }

        public void Refresh(int value)
        {
            if (valueText != null) valueText.text = value.ToString();
            if (fillImage != null)
            {
                float normalized = Mathf.InverseLerp(IndicatorSet.Min, IndicatorSet.Max, value);
                fillImage.fillAmount = normalized;

                // highIsBad (Καχυποψία) flips the color read: a full bar there
                // is a warning, not an achievement.
                float goodness = entry != null && entry.highIsBad ? 1f - normalized : normalized;
                fillImage.color = Color.Lerp(badColor, goodColor, goodness);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tooltip != null && entry != null && !string.IsNullOrEmpty(entry.description))
                tooltip.Show(entry.description, eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltip != null) tooltip.Hide();
        }
    }
}
