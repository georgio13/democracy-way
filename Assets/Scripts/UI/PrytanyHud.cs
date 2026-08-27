using TMPro;
using UnityEngine;

namespace DemocracyWay.UI
{
    using DemocracyWay.Core;

    /// <summary>
    /// Top bar: which tribe currently holds the prytany, which round of the year
    /// this is, and who the player is. Refreshes on
    /// <see cref="GameStateService.OnRoundChanged"/>.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Prytany HUD")]
    public class PrytanyHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text prytanyLabel;
        [SerializeField] private TMP_Text roundLabel;
        [SerializeField] private TMP_Text citizenLabel;

        [Header("Strings")]
        [SerializeField] private string prytanyFormat = "Πρυτανεύουσα φυλή: {0}";
        [SerializeField] private string ownTribeSuffix = "  (η φυλή σου)";
        [SerializeField] private string roundFormat = "Γύρος {0} / {1}";
        [SerializeField] private string yearOverText = "Το έτος έληξε";

        private GameStateService state;

        void Start()
        {
            state = GameStateService.Instance;
            if (state != null) state.OnRoundChanged += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (state != null) state.OnRoundChanged -= Refresh;
        }

        private void Refresh()
        {
            var session = state != null ? state.Session : null;
            var prytany = session != null ? session.prytany : null;

            if (prytany == null || prytany.TotalRounds == 0)
            {
                if (prytanyLabel != null) prytanyLabel.text = "—";
                if (roundLabel != null)   roundLabel.text   = "—";
                if (citizenLabel != null) citizenLabel.text = string.Empty;
                return;
            }

            if (prytany.IsFinished)
            {
                if (prytanyLabel != null) prytanyLabel.text = yearOverText;
                if (roundLabel != null)
                    roundLabel.text = string.Format(roundFormat, prytany.TotalRounds, prytany.TotalRounds);
            }
            else
            {
                if (prytanyLabel != null)
                {
                    string text = string.Format(prytanyFormat, prytany.CurrentTribeName);
                    if (prytany.IsPlayerTribePresiding(session.profile)) text += ownTribeSuffix;
                    prytanyLabel.text = text;
                }
                if (roundLabel != null)
                    roundLabel.text = string.Format(roundFormat, prytany.RoundNumber, prytany.TotalRounds);
            }

            if (citizenLabel != null && session.profile != null)
                citizenLabel.text = session.profile.Summary();
        }
    }
}
