using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.UI
{
    using DemocracyWay.Core;

    /// <summary>
    /// The five-indicator sidebar. Builds one bar per <see cref="IndicatorType"/>
    /// from <see cref="barPrefab"/>.
    ///
    /// Each bar chases the live value in Update rather than reacting to an
    /// event, so a jump from a dialogue outcome reads as movement rather than a
    /// snap — and no change can be missed while this HUD is disabled.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Indicator HUD")]
    public class IndicatorHud : MonoBehaviour
    {
        [SerializeField] private RectTransform barContainer;
        [SerializeField] private GameObject barPrefab;

        [Tooltip("Units of fill (0-100) travelled per second when a value changes.")]
        [SerializeField] private float animationSpeed = 60f;

        private readonly Dictionary<IndicatorType, Image> fills = new();
        private readonly Dictionary<IndicatorType, TMP_Text> valueLabels = new();
        private readonly Dictionary<IndicatorType, float> displayed = new();

        private GameStateService state;

        void Start()
        {
            state = GameStateService.Instance;
            BuildBars();
            SnapToCurrent();
        }

        void Update()
        {
            var set = state != null ? state.Indicators : null;
            if (set == null) return;

            float step = animationSpeed * Time.deltaTime;
            foreach (var type in IndicatorInfo.All)
            {
                if (!fills.TryGetValue(type, out var fill) || fill == null) continue;

                float target = set[type];
                float current = displayed.TryGetValue(type, out var d) ? d : target;
                if (!Mathf.Approximately(current, target))
                {
                    current = Mathf.MoveTowards(current, target, step);
                    displayed[type] = current;
                    fill.fillAmount = current / IndicatorInfo.Max;
                    if (valueLabels.TryGetValue(type, out var label) && label != null)
                        label.text = Mathf.RoundToInt(current).ToString();
                }
            }
        }

        private void BuildBars()
        {
            if (barContainer == null || barPrefab == null)
            {
                Debug.LogError("[IndicatorHud] barContainer/barPrefab not wired — re-run Tools > DemocracyWay > Init.");
                return;
            }

            UiUtil.ClearChildren(barContainer);

            fills.Clear();
            valueLabels.Clear();
            displayed.Clear();

            foreach (var type in IndicatorInfo.All)
            {
                var go = Instantiate(barPrefab, barContainer);
                go.name = $"Indicator_{type}";

                var nameLabel = go.transform.Find("Name")?.GetComponent<TMP_Text>();
                if (nameLabel != null) nameLabel.text = IndicatorInfo.Name(type);

                var valueLabel = go.transform.Find("Value")?.GetComponent<TMP_Text>();
                if (valueLabel != null) valueLabels[type] = valueLabel;

                var fill = go.transform.Find("Track/Fill")?.GetComponent<Image>();
                if (fill != null)
                {
                    fill.color = IndicatorInfo.BarColor(type);
                    fills[type] = fill;
                }
            }
        }

        /// <summary>Jumps every bar to the live value with no animation — used
        /// on first bind and after a load.</summary>
        private void SnapToCurrent()
        {
            var set = state != null ? state.Indicators : null;
            if (set == null) return;

            foreach (var type in IndicatorInfo.All)
            {
                float value = set[type];
                displayed[type] = value;
                if (fills.TryGetValue(type, out var fill) && fill != null)
                    fill.fillAmount = value / IndicatorInfo.Max;
                if (valueLabels.TryGetValue(type, out var label) && label != null)
                    label.text = Mathf.RoundToInt(value).ToString();
            }
        }
    }
}
