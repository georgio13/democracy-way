using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    // A separate format/directory leaves the previous project's saves untouched.
    public static class DWStore
    {
        public const int SlotCount = 4;
        public const string Format = "DemocracyWay.ApprovedUI";
        public const int Version = 1;
#if UNITY_EDITOR
        // Set only by isolated Editor integration tests; excluded from all player builds.
        public static string TestDirectory;
#endif
        public static string DirectoryPath
        {
            get
            {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(TestDirectory))
                    return TestDirectory;
#endif
                return Path.Combine(Application.persistentDataPath, "approved-ui-saves-v1");
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void DW_SyncFS();
#endif
        private static string FilePath(int slot)
        {
            if (slot < 0 || slot >= SlotCount)
                throw new ArgumentOutOfRangeException(nameof(slot));
            return Path.Combine(DirectoryPath, "slot-" + (slot + 1) + ".json");
        }

        public static DWSlotResult Read(int slot)
        {
            try
            {
                string path = FilePath(slot);
                if (!File.Exists(path))
                    return new DWSlotResult
                    {
                        status = DWSlotStatus.Empty
                    };
                DWSlotResult result = ReadFile(path, slot);
                if (result.status == DWSlotStatus.Invalid && File.Exists(path + ".bak"))
                {
                    DWSlotResult backup = ReadFile(path + ".bak", slot);
                    if (backup.status == DWSlotStatus.Valid)
                    {
                        backup.recoveredBackup = true;
                        return backup;
                    }
                }

                return result;
            }
            catch (Exception ex)when (StorageException(ex))
            {
                return new DWSlotResult
                {
                    status = DWSlotStatus.Unavailable,
                    error = ex.Message
                };
            }
        }

        private static DWSlotResult ReadFile(string path, int slot)
        {
            string json = File.ReadAllText(path);
            try
            {
                DWEnvelope envelope = JsonUtility.FromJson<DWEnvelope>(json);
                if (envelope == null || envelope.format != Format || envelope.version != Version || envelope.slot != slot || string.IsNullOrWhiteSpace(envelope.payloadJson) || envelope.checksum != Checksum(envelope.payloadJson) || !DateTime.TryParse(envelope.savedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
                    return new DWSlotResult
                    {
                        status = DWSlotStatus.Invalid,
                        error = "Μη έγκυρη ή ασύμβατη αποθήκευση."
                    };
                DWGameState state = JsonUtility.FromJson<DWGameState>(envelope.payloadJson);
                if (!ValidState(state))
                    return new DWSlotResult
                    {
                        status = DWSlotStatus.Invalid,
                        error = "Η κατάσταση παιχνιδιού είναι ελλιπής."
                    };
                return new DWSlotResult
                {
                    status = DWSlotStatus.Valid,
                    state = state,
                    title = envelope.title,
                    savedAtUtc = envelope.savedAtUtc
                };
            }
            catch (Exception ex)when (ex is ArgumentException || ex is NullReferenceException)
            {
                return new DWSlotResult
                {
                    status = DWSlotStatus.Invalid,
                    error = "Το αρχείο δεν διαβάζεται."
                };
            }
        }

        private static bool ValidState(DWGameState state)
        {
            if (state == null || !Guid.TryParse(state.runId, out _) || !DWCampaignRules.ValidCampaign(state.campaign) || state.metrics == null || state.baseline == null || state.choices == null || state.drawnTribes == null || state.prologueIndex < 0 || state.biographyIndex < 0 || state.tribeIndex < 0 || state.tribeIndex >= 10 || state.economicClassIndex < 0 || state.economicClassIndex >= 4 || state.prytany < 1 || state.prytany > 10 || state.week < 1 || state.week > 5 || state.currentTribe < -1 || state.currentTribe >= 10)
                return false;
            if (state.phase != "prologue" && state.phase != "biography" && state.phase != "summary" && state.phase != "prytany-title" && state.phase != "lottery" && state.phase != "map-stela" && !DWCampaignRules.ValidPhase(state.phase))
                return false;
            var seenTribes = new System.Collections.Generic.HashSet<int>();
            foreach (int tribe in state.drawnTribes)
                if (tribe < 0 || tribe >= 10 || !seenTribes.Add(tribe))
                    return false;
            if (state.currentTribe >= 0 && !seenTribes.Contains(state.currentTribe))
                return false;
            var seenSteps = new System.Collections.Generic.HashSet<string>();
            foreach (DWChoiceRecord record in state.choices)
                if (record == null || string.IsNullOrWhiteSpace(record.stepId) || string.IsNullOrWhiteSpace(record.choiceId) || record.metrics == null || !seenSteps.Add(record.stepId))
                    return false;
            return true;
        }

        public static bool Save(int slot, DWGameState state, out string error)
        {
            error = "";
            if (!ValidState(state))
            {
                error = "Δεν αποθηκεύτηκε μη έγκυρη κατάσταση παιχνιδιού.";
                return false;
            }

            try
            {
                DWSlotResult previous = Read(slot);
                if (previous.status == DWSlotStatus.Unavailable || previous.status == DWSlotStatus.Invalid)
                {
                    error = "Η θέση δεν είναι διαθέσιμη για εγγραφή. Χρησιμοποίησε άλλη θέση ή διέγραψέ την από τη Φόρτωση.";
                    return false;
                }

                if (previous.status == DWSlotStatus.Valid && previous.state.runId != state.runId)
                {
                    error = "Η θέση περιέχει διαφορετική πορεία. Δεν αντικαταστάθηκε.";
                    return false;
                }

                string path = FilePath(slot);
                Directory.CreateDirectory(DirectoryPath);
                string payload = JsonUtility.ToJson(state);
                var envelope = new DWEnvelope
                {
                    slot = slot,
                    title = state.playerName + " · Πρυτανεία " + state.prytany + " · Εβδομάδα " + state.week,
                    savedAtUtc = DateTime.UtcNow.ToString("O"),
                    payloadJson = payload,
                    checksum = Checksum(payload)
                };
                string json = JsonUtility.ToJson(envelope, true);
                File.WriteAllText(path + ".tmp", json, new UTF8Encoding(false));
                // Preserve the last valid file. A failed write can be recovered on the next Read.
                if (File.Exists(path) && !previous.recoveredBackup)
                    File.Copy(path, path + ".bak", true);
                if (File.Exists(path))
                    File.Copy(path + ".tmp", path, true);
                else
                    File.Move(path + ".tmp", path);
                if (File.Exists(path + ".tmp"))
                    File.Delete(path + ".tmp");
                FlushWebStorage();
                return true;
            }
            catch (Exception ex)when (StorageException(ex))
            {
                error = "Η αποθήκευση δεν ολοκληρώθηκε. Έλεγξε τον διαθέσιμο χώρο του browser.";
                Debug.LogException(ex);
                return false;
            }
        }

        public static bool Delete(int slot, out string error)
        {
            error = "";
            try
            {
                string path = FilePath(slot);
                foreach (string suffix in new[]
                {
                    "",
                    ".bak",
                    ".tmp"
                }

                )
                    if (File.Exists(path + suffix))
                        File.Delete(path + suffix);
                FlushWebStorage();
                return !File.Exists(path);
            }
            catch (Exception ex)when (StorageException(ex))
            {
                error = "Δεν ολοκληρώθηκε η διαγραφή.";
                Debug.LogException(ex);
                return false;
            }
        }

        public static void FlushWebStorage()
        {
            PlayerPrefs.Save();
#if UNITY_WEBGL && !UNITY_EDITOR
            DW_SyncFS();
#endif
        }

        // Integrity check for accidental damage; not a security or anti-tampering mechanism.
        private static string Checksum(string text)
        {
            unchecked
            {
                uint value = 2166136261;
                foreach (byte b in Encoding.UTF8.GetBytes(text))
                {
                    value ^= b;
                    value *= 16777619;
                }

                return value.ToString("X8", CultureInfo.InvariantCulture);
            }
        }

        private static bool StorageException(Exception ex)
        {
            return ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException || ex is System.Security.SecurityException || ex is ArgumentException;
        }
    }
}
