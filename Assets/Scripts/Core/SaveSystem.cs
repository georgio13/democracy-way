using System;
using System.IO;
using UnityEngine;

namespace DemocracyWay.Core
{
    /// <summary>
    /// Four independent save slots, each a JSON file in
    /// <c>Application.persistentDataPath</c>. Slots are addressed 0..3; the UI
    /// shows them as "Θέση 1".."Θέση 4".
    ///
    /// Every method is defensive: a missing, unreadable or corrupt file reads
    /// back as an empty slot rather than throwing, so one bad file can never
    /// block the load menu from opening.
    /// </summary>
    public static class SaveSystem
    {
        public const int SlotCount = 4;

        private static string SlotPath(int slot) =>
            Path.Combine(Application.persistentDataPath, $"demokratia_slot{slot}.json");

        private static bool IsValidSlot(int slot) => slot >= 0 && slot < SlotCount;

        /// <summary>What the load menu needs to draw one row, without paying to
        /// deserialise the whole session.</summary>
        public readonly struct SlotInfo
        {
            public readonly int Slot;
            public readonly bool IsEmpty;
            public readonly string CitizenSummary;
            public readonly string SavedAt;
            public readonly string Playtime;
            public readonly int RoundNumber;
            public readonly int TotalRounds;

            public SlotInfo(int slot)
            {
                Slot = slot;
                IsEmpty = true;
                CitizenSummary = null;
                SavedAt = null;
                Playtime = null;
                RoundNumber = 0;
                TotalRounds = 0;
            }

            public SlotInfo(int slot, GameSession session)
            {
                Slot = slot;
                IsEmpty = false;
                CitizenSummary = session.profile != null ? session.profile.Summary() : "—";
                SavedAt = session.SavedAtDisplay();
                Playtime = session.PlaytimeDisplay();
                RoundNumber = session.prytany != null ? session.prytany.RoundNumber : 0;
                TotalRounds = session.prytany != null ? session.prytany.TotalRounds : 0;
            }
        }

        public static bool Exists(int slot)
        {
            if (!IsValidSlot(slot)) return false;
            try { return File.Exists(SlotPath(slot)); }
            catch { return false; }
        }

        /// <summary>True when at least one slot holds a save — drives whether
        /// the main menu's "Φόρτωση" button is interactable.</summary>
        public static bool AnySaveExists()
        {
            for (int i = 0; i < SlotCount; i++)
                if (Exists(i)) return true;
            return false;
        }

        public static GameSession Load(int slot)
        {
            if (!IsValidSlot(slot)) return null;
            try
            {
                string path = SlotPath(slot);
                if (!File.Exists(path)) return null;

                var session = JsonUtility.FromJson<GameSession>(File.ReadAllText(path));
                // A file that parses to null (empty/whitespace) or carries no
                // profile is treated as corrupt rather than surfaced as a
                // half-loaded run.
                if (session == null || session.profile == null) return null;

                session.indicators ??= new IndicatorSet();
                session.prytany    ??= new PrytanySchedule();
                return session;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Slot {slot} unreadable: {e.Message}");
                return null;
            }
        }

        public static bool Save(int slot, GameSession session)
        {
            if (!IsValidSlot(slot) || session == null) return false;
            try
            {
                session.schemaVersion = GameSession.SchemaVersion;
                session.savedAtUtc = DateTime.UtcNow.ToString("o");
                File.WriteAllText(SlotPath(slot), JsonUtility.ToJson(session, prettyPrint: true));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Could not write slot {slot}: {e.Message}");
                return false;
            }
        }

        public static bool Delete(int slot)
        {
            if (!IsValidSlot(slot)) return false;
            try
            {
                string path = SlotPath(slot);
                if (File.Exists(path)) File.Delete(path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Could not delete slot {slot}: {e.Message}");
                return false;
            }
        }

        /// <summary>Metadata for all four slots, index-aligned to slot number.</summary>
        public static SlotInfo[] ReadAllSlots()
        {
            var infos = new SlotInfo[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                var session = Load(i);
                infos[i] = session == null ? new SlotInfo(i) : new SlotInfo(i, session);
            }
            return infos;
        }

        /// <summary>First empty slot, or -1 when all four are taken (the UI then
        /// asks the player to pick one to overwrite).</summary>
        public static int FirstEmptySlot()
        {
            for (int i = 0; i < SlotCount; i++)
                if (!Exists(i)) return i;
            return -1;
        }
    }
}
