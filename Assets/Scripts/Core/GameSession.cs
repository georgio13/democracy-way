using System;
using System.Collections.Generic;

namespace DemocracyWay.Core
{
    /// <summary>One recorded player decision — the unit of the analytics log
    /// and of the in-save choice history.</summary>
    [Serializable]
    public class ChoiceRecord
    {
        public string chapterId = "";
        public string nodeId = "";
        public string choiceId = "";
        public string choiceText = "";
        public int prytany;
        public int week;
        public string atIso = "";
    }

    /// <summary>
    /// Everything that defines a run in progress. This whole object is what a
    /// save slot stores — if it's not in here, it doesn't survive a reload.
    /// </summary>
    [Serializable]
    public class GameSession
    {
        public CitizenProfile profile = new CitizenProfile();
        public IndicatorSet indicators = new IndicatorSet();
        public PrytanyCalendar calendar = new PrytanyCalendar();

        /// <summary>Chapter the player is currently in (id + display title so
        /// the load screen can label the slot without loading content).</summary>
        public string currentChapterId = "";
        public string currentChapterTitle = "";

        /// <summary>Dialogue node the player is at inside the current chapter,
        /// updated as the dialogue runs. A mid-chapter autosave (week advance)
        /// captures it, so loading resumes at that node instead of replaying
        /// the chapter — replaying would re-apply every choice's effects.
        /// Empty = the chapter starts from its first node.</summary>
        public string currentDialogueNodeId = "";

        /// <summary>Story flags set by dialogue choices ("met_socrates", …).</summary>
        public List<string> flags = new List<string>();

        /// <summary>Full decision history of this run, in order.</summary>
        public List<ChoiceRecord> choices = new List<ChoiceRecord>();

        public double playtimeSeconds;
        public string createdAtIso = "";

        /// <summary>Ties this run's analytics events together across sessions.</summary>
        public string analyticsSessionId = "";

        public bool HasFlag(string flag) => flags.Contains(flag);

        public void SetFlag(string flag)
        {
            if (!string.IsNullOrEmpty(flag) && !flags.Contains(flag))
                flags.Add(flag);
        }
    }
}
