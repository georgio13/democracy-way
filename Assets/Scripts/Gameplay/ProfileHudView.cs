using System.Text;
using TMPro;
using UnityEngine;
using DemocracyWay.Services;

namespace DemocracyWay.Gameplay
{
    /// <summary>
    /// The top-right HUD: who the player is (the six creation picks) and
    /// where the year stands (presiding tribe, prytany, week) — one TMP text
    /// block, rebuilt on the session's CalendarChanged event instead of
    /// per-frame polling.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Profile Hud View")]
    [DisallowMultipleComponent]
    public class ProfileHudView : MonoBehaviour
    {
        [Tooltip("Το TMP block όπου γράφονται όλες οι γραμμές του προφίλ.")]
        [SerializeField] private TMP_Text profileText;

        [Tooltip("Μορφή της γραμμής πρυτανεύουσας φυλής: {0}=τίτλος φυλής.")]
        [SerializeField] private string presidingTribeFormat = "Πρυτανεύουσα φυλή: {0}";

        [Tooltip("Μορφή της γραμμής ημερολογίου: {0}=πρυτανεία, {1}=σύνολο, {2}=βδομάδα, {3}=βδομάδες/πρυτανεία.")]
        [SerializeField] private string calendarFormat = "Πρυτανεία {0}/{1} · Βδομάδα {2}/{3}";

        private SessionService session;

        void Start()
        {
            session = ServicesRoot.Session;
            if (session == null || session.Current == null || profileText == null)
            {
                // No run in progress (dev shortcut into the scene) — a HUD
                // full of empty lines would only confuse, so hide.
                gameObject.SetActive(false);
                return;
            }

            session.CalendarChanged += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            // The session service outlives the scene; the handler must not.
            if (session != null) session.CalendarChanged -= Refresh;
        }

        private void Refresh()
        {
            if (session == null || session.Current == null) return;
            var profile = session.Current.profile;
            var calendar = session.Current.calendar;

            var sb = new StringBuilder();
            sb.AppendLine(profile.genderTitle);
            sb.AppendLine(profile.tribeTitle);
            sb.AppendLine(profile.trittysTitle);
            sb.AppendLine(profile.wealthTitle);
            sb.AppendLine(profile.periodTitle);
            sb.AppendLine(profile.professionTitle);
            sb.AppendLine(string.Format(presidingTribeFormat, ResolvePresidingTitle(calendar.PresidingTribeId)));

            // After the year's last week the model points one past the final
            // prytany — clamp for display so the HUD never reads "11/10".
            int prytanyCount = calendar.tribeOrder.Count;
            int shownPrytany = prytanyCount > 0 ? Mathf.Min(calendar.prytanyNumber, prytanyCount) : calendar.prytanyNumber;
            sb.Append(string.Format(calendarFormat,
                shownPrytany, prytanyCount, calendar.weekNumber, calendar.weeksPerPrytany));

            profileText.text = sb.ToString();
        }

        /// <summary>
        /// The presiding tribe id comes from the calendar; its display title
        /// lives in the creation database. Falls back to the raw id so an
        /// edited database never blanks the HUD line.
        /// </summary>
        private string ResolvePresidingTitle(string tribeId)
        {
            if (string.IsNullOrEmpty(tribeId)) return "—"; // year finished / no calendar
            var database = ServicesRoot.Config != null ? ServicesRoot.Config.creationDatabase : null;
            var tribe = database != null ? database.tribes.Find(t => t.id == tribeId) : null;
            return tribe != null && !string.IsNullOrEmpty(tribe.title) ? tribe.title : tribeId;
        }
    }
}
