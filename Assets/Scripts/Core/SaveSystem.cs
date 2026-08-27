using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DemocracyWay.Core
{
    /// <summary>Versioned wrapper actually written to disk.</summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string savedAtIso = "";
        public GameSession session;
    }

    /// <summary>Cheap metadata for one slot, for the load/save-slot UI.</summary>
    [Serializable]
    public class SaveSummary
    {
        public int slot;
        public bool exists;
        public string profileLine = "";   // e.g. "Γυνή · Ερεχθηΐς · Κεραμεικός"
        public string chapterTitle = "";
        public int prytany;
        public int week;
        public double playtimeSeconds;
        public string savedAtIso = "";
    }

    /// <summary>
    /// Four independent JSON save slots under persistentDataPath/saves/.
    /// Writes are atomic (temp file, then swap) so a crash mid-save can never
    /// corrupt an existing slot. A corrupt or unreadable file reads as an
    /// empty slot — it must never block the menu.
    /// </summary>
    public static class SaveSystem
    {
        public const int SlotCount = 4;

        /// <summary>Tests point this at a temp directory so they never touch
        /// real saves. Null (always, in the game itself) = persistentDataPath.</summary>
        public static string RootOverride;

        private static string SavesDir =>
            Path.Combine(RootOverride ?? Application.persistentDataPath, "saves");
        private static string SlotPath(int slot) => Path.Combine(SavesDir, $"slot{slot}.json");

        public static bool IsValidSlot(int slot) => slot >= 0 && slot < SlotCount;

        public static bool Exists(int slot) => IsValidSlot(slot) && File.Exists(SlotPath(slot));

        public static bool AnyExists()
        {
            for (int i = 0; i < SlotCount; i++)
                if (Exists(i)) return true;
            return false;
        }

        /// <summary>True when every slot is taken — the menu then disables Νέο Παιχνίδι.</summary>
        public static bool AllFull()
        {
            for (int i = 0; i < SlotCount; i++)
                if (!Exists(i)) return false;
            return true;
        }

        /// <summary>First free slot, or -1 when all four are taken.</summary>
        public static int FirstEmptySlot()
        {
            for (int i = 0; i < SlotCount; i++)
                if (!Exists(i)) return i;
            return -1;
        }

        public static void Save(int slot, GameSession session)
        {
            if (!IsValidSlot(slot))
                throw new ArgumentOutOfRangeException(nameof(slot), $"Slot {slot} — valid: 0..{SlotCount - 1}");
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            Directory.CreateDirectory(SavesDir);

            var data = new SaveData
            {
                savedAtIso = DateTime.UtcNow.ToString("o"),
                session = session
            };
            string json = JsonUtility.ToJson(data, prettyPrint: true);

            // Atomic write: never truncate the real file before the new
            // content is fully on disk.
            string finalPath = SlotPath(slot);
            string tempPath = finalPath + ".tmp";
            File.WriteAllText(tempPath, json);
            if (File.Exists(finalPath))
                File.Replace(tempPath, finalPath, destinationBackupFileName: null);
            else
                File.Move(tempPath, finalPath);
        }

        /// <summary>Loaded session, or null for a missing/corrupt/newer-version slot.</summary>
        public static GameSession Load(int slot)
        {
            if (!Exists(slot)) return null;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SlotPath(slot)));
                if (data?.session == null) return null;
                if (data.version > SaveData.CurrentVersion)
                {
                    Debug.LogWarning($"[SaveSystem] Slot {slot} is version {data.version}, " +
                                     $"this build reads up to {SaveData.CurrentVersion} — ignoring it.");
                    return null;
                }
                // Older versions: migrate here when the schema changes.
                return data.session;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Slot {slot} unreadable, treating as empty: {e.Message}");
                return null;
            }
        }

        public static SaveSummary Summarize(int slot)
        {
            var summary = new SaveSummary { slot = slot };
            var session = Load(slot);
            if (session == null) return summary;   // exists stays false

            summary.exists = true;
            summary.profileLine = string.Join(" · ", new[]
            {
                session.profile.genderTitle,
                session.profile.tribeTitle,
                session.profile.professionTitle
            }.Where(s => !string.IsNullOrEmpty(s)));
            summary.chapterTitle = session.currentChapterTitle;
            summary.prytany = session.calendar.prytanyNumber;
            summary.week = session.calendar.weekNumber;
            summary.playtimeSeconds = session.playtimeSeconds;
            try
            {
                summary.savedAtIso = JsonUtility
                    .FromJson<SaveData>(File.ReadAllText(SlotPath(slot))).savedAtIso;
            }
            catch { /* summary works without the timestamp */ }
            return summary;
        }

        public static void Delete(int slot)
        {
            if (Exists(slot))
                File.Delete(SlotPath(slot));
        }
    }
}
