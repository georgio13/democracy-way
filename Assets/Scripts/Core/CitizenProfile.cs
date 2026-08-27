using System;

namespace DemocracyWay.Core
{
    /// <summary>
    /// The six character-creation picks. Each pick stores both the stable id
    /// (used by saves, analytics and content filters) and the display title
    /// (so saved games keep their labels even if the database is edited later).
    /// </summary>
    [Serializable]
    public class CitizenProfile
    {
        public string genderId = "";
        public string genderTitle = "";

        public string tribeId = "";
        public string tribeTitle = "";

        public string trittysId = "";
        public string trittysTitle = "";

        public string wealthId = "";
        public string wealthTitle = "";

        public string periodId = "";
        public string periodTitle = "";

        public string professionId = "";
        public string professionTitle = "";

        /// <summary>
        /// True when the chosen gender option enables the Καχυποψία indicator.
        /// Copied from the GenderOption at creation time so the save is
        /// self-contained.
        /// </summary>
        public bool suspicionEnabled;

        public bool IsComplete =>
            genderId.Length > 0 && tribeId.Length > 0 && trittysId.Length > 0 &&
            wealthId.Length > 0 && periodId.Length > 0 && professionId.Length > 0;
    }
}
