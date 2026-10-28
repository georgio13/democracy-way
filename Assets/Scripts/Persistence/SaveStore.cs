using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    // A separate format/directory leaves the previous project's saves untouched.
    public static class SaveStore
    {
        public const int SlotCount = 4;
        public const string Format = "DemocracyWay.ApprovedUI";
        public const int Version = 2;   // v2: προσθήκη Agora polis/citizen/agoraCase — τα v1 saves απορρίπτονται
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
#if UNITY_WEBGL || UNITY_EDITOR
                return AccountBridge.LocalDirectory;
#else
                return Path.Combine(Application.persistentDataPath, "approved-ui-saves-v1");
#endif
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

        public static SaveSlotResult Read(int slot)
        {
            try
            {
                string path = FilePath(slot);
                if (!File.Exists(path))
                    return new SaveSlotResult
                    {
                        status = SaveSlotStatus.Empty
                    };
                SaveSlotResult result = ReadFile(path, slot);
                if (result.status == SaveSlotStatus.Invalid && File.Exists(path + ".bak"))
                {
                    SaveSlotResult backup = ReadFile(path + ".bak", slot);
                    if (backup.status == SaveSlotStatus.Valid)
                    {
                        backup.recoveredBackup = true;
                        return backup;
                    }
                }

                return result;
            }
            catch (Exception ex)when (StorageException(ex))
            {
                return new SaveSlotResult
                {
                    status = SaveSlotStatus.Unavailable,
                    error = ex.Message
                };
            }
        }

        private static SaveSlotResult ReadFile(string path, int slot)
        {
            string json = File.ReadAllText(path);
            try
            {
                SaveEnvelope envelope = JsonUtility.FromJson<SaveEnvelope>(json);
                // Save παλαιότερης έκδοσης = κενή θέση (επανεγγράψιμη), όχι «μη συμβατή».
                if (envelope != null && envelope.format == Format && envelope.version != Version)
                    return new SaveSlotResult { status = SaveSlotStatus.Empty };
                if (envelope == null || envelope.format != Format || envelope.slot != slot || string.IsNullOrWhiteSpace(envelope.payloadJson) || envelope.checksum != Checksum(envelope.payloadJson) || !DateTime.TryParse(envelope.savedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
                    return new SaveSlotResult
                    {
                        status = SaveSlotStatus.Invalid,
                        error = "Μη έγκυρη ή ασύμβατη αποθήκευση."
                    };
                GameState state = JsonUtility.FromJson<GameState>(envelope.payloadJson);
                if (!ValidState(state))
                    return new SaveSlotResult
                    {
                        status = SaveSlotStatus.Invalid,
                        error = "Η κατάσταση παιχνιδιού είναι ελλιπής."
                    };
                return new SaveSlotResult
                {
                    status = SaveSlotStatus.Valid,
                    state = state,
                    title = envelope.title,
                    savedAtUtc = envelope.savedAtUtc
                };
            }
            catch (Exception ex)when (ex is ArgumentException || ex is NullReferenceException)
            {
                return new SaveSlotResult
                {
                    status = SaveSlotStatus.Invalid,
                    error = "Το αρχείο δεν διαβάζεται."
                };
            }
        }

        private static bool ValidState(GameState state)
        {
            if (state == null || !Guid.TryParse(state.runId, out _) || !CampaignRules.ValidCampaign(state.campaign) || !EvaluationRules.Valid(state.evaluation) || state.metrics == null || state.baseline == null || state.choices == null || state.drawnTribes == null || state.prologueIndex < 0 || state.biographyIndex < 0 || state.tribeIndex < 0 || state.tribeIndex >= 10 || state.economicClassIndex < 0 || state.economicClassIndex >= 4 || state.prytany < 1 || state.prytany > 10 || state.week < 1 || state.week > 5 || state.currentTribe < -1 || state.currentTribe >= 10)
                return false;
            if (state.phase != "prologue" && state.phase != "biography" && state.phase != "summary" && state.phase != "prytany-title" && state.phase != "lottery" && state.phase != "map-stela" && !CampaignRules.ValidPhase(state.phase))
                return false;
            if (state.polis == null || state.citizen == null || state.agoraCase == null)
                return false;
            foreach (int value in new[] { state.polis.treasury, state.polis.food, state.polis.readiness, state.polis.trust, state.polis.demosPower })
                if (value < 0 || value > 100)
                    return false;
            var seenTribes = new System.Collections.Generic.HashSet<int>();
            foreach (int tribe in state.drawnTribes)
                if (tribe < 0 || tribe >= 10 || !seenTribes.Add(tribe))
                    return false;
            if (state.currentTribe >= 0 && !seenTribes.Contains(state.currentTribe))
                return false;
            var seenSteps = new System.Collections.Generic.HashSet<string>();
            foreach (ChoiceRecord record in state.choices)
                if (record == null || string.IsNullOrWhiteSpace(record.stepId) || string.IsNullOrWhiteSpace(record.choiceId) || record.metrics == null || !seenSteps.Add(record.stepId))
                    return false;
            return true;
        }

        public static bool Save(int slot, GameState state, out string error)
        {
            error = "";
            if (!ValidState(state))
            {
                error = "Δεν αποθηκεύτηκε μη έγκυρη κατάσταση παιχνιδιού.";
                return false;
            }

            try
            {
                SaveSlotResult previous = Read(slot);
                // Save παλαιότερης έκδοσης δεν είναι «κατεστραμμένο»: η θέση ξαναγράφεται ελεύθερα.
                if (previous.status == SaveSlotStatus.Unavailable || (previous.status == SaveSlotStatus.Invalid && !StaleVersion(slot)))
                {
                    error = "Η θέση δεν είναι διαθέσιμη για εγγραφή. Χρησιμοποίησε άλλη θέση ή διέγραψέ την από τη Φόρτωση.";
                    return false;
                }

                if (previous.status == SaveSlotStatus.Valid && previous.state.runId != state.runId)
                {
                    error = "Η θέση περιέχει διαφορετική πορεία. Δεν αντικαταστάθηκε.";
                    return false;
                }

                string path = FilePath(slot);
                Directory.CreateDirectory(DirectoryPath);
                string payload = JsonUtility.ToJson(state);
                var envelope = new SaveEnvelope
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
                AccountBridge.UploadSlot(slot, json);
                return true;
            }
            catch (Exception ex)when (StorageException(ex))
            {
                error = "Η αποθήκευση δεν ολοκληρώθηκε. Έλεγξε τον διαθέσιμο χώρο του browser.";
                Debug.LogException(ex);
                return false;
            }
        }

        private static bool StaleVersion(int slot)
        {
            try
            {
                string path = FilePath(slot);
                if (!File.Exists(path)) return false;
                var envelope = JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(path));
                return envelope != null && envelope.format == Format && envelope.version != Version;
            }
            catch { return false; }
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
                bool deleted = !File.Exists(path);
                if (deleted) AccountBridge.DeleteSlot(slot);
                return deleted;
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
        private static string Checksum(string text) => Hashing.Fnv1a(text);

        private static bool StorageException(Exception ex)
        {
            return ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException || ex is System.Security.SecurityException || ex is ArgumentException;
        }
    }
}
