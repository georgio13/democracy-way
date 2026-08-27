using System.Collections.Generic;
using UnityEngine;
using DemocracyWay.Data;
using DemocracyWay.Services;
using DemocracyWay.UI;

namespace DemocracyWay.Gameplay
{
    /// <summary>
    /// The top-left indicator HUD. Rows are built once from the catalog's
    /// visible entries for this run (Καχυποψία appears only when the profile
    /// enables it) and refreshed through the session's IndicatorsChanged
    /// event — no per-frame polling of game state.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Indicator Hud View")]
    [DisallowMultipleComponent]
    public class IndicatorHudView : MonoBehaviour
    {
        [Tooltip("Prefab μίας γραμμής δείκτη.")]
        [SerializeField] private IndicatorRowView rowPrefab;

        [Tooltip("Το container όπου μπαίνουν οι γραμμές των δεικτών.")]
        [SerializeField] private RectTransform rowsContainer;

        [Tooltip("Το κοινό tooltip της σκηνής — δείχνει την περιγραφή του δείκτη στο hover.")]
        [SerializeField] private TooltipView tooltipView;

        private SessionService session;
        private readonly List<IndicatorRowView> rows = new List<IndicatorRowView>();
        private readonly List<IndicatorCatalog.Entry> rowEntries = new List<IndicatorCatalog.Entry>();

        void Start()
        {
            session = ServicesRoot.Session;
            var catalog = ServicesRoot.Config != null ? ServicesRoot.Config.indicatorCatalog : null;

            // No run (dev played the scene directly) or missing wiring: an
            // empty or lying HUD is worse than none — hide entirely.
            if (session == null || session.Current == null ||
                catalog == null || rowPrefab == null || rowsContainer == null)
            {
                gameObject.SetActive(false);
                return;
            }

            foreach (var entry in catalog.VisibleFor(session.Current.profile.suspicionEnabled))
            {
                var row = Instantiate(rowPrefab, rowsContainer);
                row.Init(entry, tooltipView);
                rows.Add(row);
                rowEntries.Add(entry);
            }

            session.IndicatorsChanged += RefreshValues;
            RefreshValues();
        }

        void OnDestroy()
        {
            // The session service outlives every scene — an un-unsubscribed
            // handler would keep this dead view alive in the event list.
            if (session != null) session.IndicatorsChanged -= RefreshValues;
        }

        private void RefreshValues()
        {
            if (session == null || session.Current == null) return;
            for (int i = 0; i < rows.Count; i++)
                rows[i].Refresh(session.Current.indicators.Get(rowEntries[i].id));
        }
    }
}
