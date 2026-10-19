using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DemocracyWay.NewUI
{
    // Account credentials never cross this boundary into research events.
    public sealed class AccountBridge : MonoBehaviour
    {
        public static bool IsReady { get; private set; }
        public static string UserId { get; private set; }
        public static string ResearchParticipantId { get; private set; }
        public static bool ResearchConsent { get; private set; }
        public static string CloudStatus { get; private set; } = "not-connected";
        public static bool IsAuthenticated => !string.IsNullOrEmpty(UserId);
        public static string LocalNamespace => IsAuthenticated ? "account-" + Digest(UserId).Substring(0, 32) : "not-signed-in";
        public static string LocalDirectory => Path.Combine(Application.persistentDataPath, "approved-ui-saves-v1", LocalNamespace);
        static AccountBridge instance;
#if UNITY_EDITOR
        EditorAccountClient editorClient;
        EditorAccountPanel editorPanel;
#endif
        [Serializable] sealed class CloudSlot { public int slot; public string envelope, updatedUtc; public bool deleted; }
        [Serializable] sealed class AccountPacket { public string uid, researchParticipantId; public bool researchConsent; public CloudSlot[] slots; }
        [Serializable] sealed class SyncStatus { public bool ok; public string code; public int pending; }
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void DW_AccountRegister(string callbackObject);
        [DllImport("__Internal")] static extern void DW_AccountApplied();
        [DllImport("__Internal")] static extern void DW_AccountFailed();
        [DllImport("__Internal")] static extern void DW_AccountUpload(int slot, string envelope);
        [DllImport("__Internal")] static extern void DW_AccountDelete(int slot);
        [DllImport("__Internal")] static extern void DW_AccountOpen();
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialise()
        {
            if (instance != null) return;
            IsReady = false; UserId = ResearchParticipantId = null; ResearchConsent = false; CloudStatus = "not-connected";
            var host = new GameObject("PlayerAccountBridge"); instance = host.AddComponent<AccountBridge>(); DontDestroyOnLoad(host);
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = false; IsReady = false; DW_AccountRegister(host.name);
#elif UNITY_EDITOR
            instance.editorClient = host.AddComponent<EditorAccountClient>();
            instance.editorClient.Initialize(instance);
            instance.editorPanel = host.AddComponent<EditorAccountPanel>();
            instance.editorPanel.Initialize(instance.editorClient);
#else
            IsReady = true; CloudStatus = "local-preview";
#endif
        }
        public static void UploadSlot(int slot, string envelope)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (IsReady && IsAuthenticated) DW_AccountUpload(slot, envelope);
#elif UNITY_EDITOR
            if (IsReady && IsAuthenticated && string.IsNullOrEmpty(SaveStore.TestDirectory)) instance?.editorClient.UploadSlot(slot, envelope);
#endif
        }
        public static void DeleteSlot(int slot)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (IsReady && IsAuthenticated) DW_AccountDelete(slot);
#elif UNITY_EDITOR
            if (IsReady && IsAuthenticated && string.IsNullOrEmpty(SaveStore.TestDirectory)) instance?.editorClient.DeleteSlot(slot);
#endif
        }
        public static void OpenAccount()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            DW_AccountOpen();
#elif UNITY_EDITOR
            instance?.editorPanel.OpenAccount();
#endif
        }
        public void OnAccountReady(string json)
        {
            try
            {
                var packet = JsonUtility.FromJson<AccountPacket>(json);
                if (packet == null || string.IsNullOrWhiteSpace(packet.uid) || !Guid.TryParse(packet.researchParticipantId, out _)) throw new InvalidDataException("Invalid account response.");
                UserId = packet.uid; ResearchParticipantId = packet.researchParticipantId; ResearchConsent = packet.researchConsent;
                Directory.CreateDirectory(LocalDirectory);
                // Namespace is established before reconciliation; local newer writes
                // must be allowed back into that same account's upload queue.
                IsReady = true;
                foreach (var slot in packet.slots ?? Array.Empty<CloudSlot>()) Reconcile(slot);
                GameSession.State = null; GameSession.Slot = -1; CloudStatus = "ready";
                SaveStore.FlushWebStorage();
                var controller = FindAnyObjectByType<GameFlowController>(); if (controller != null) controller.Show("main-menu");
#if UNITY_WEBGL && !UNITY_EDITOR
                DW_AccountApplied();
#endif
            }
            catch (Exception error) { IsReady = false; CloudStatus = "local-sync-failed"; Debug.LogWarning("Account save synchronisation failed: " + error.GetType().Name);
#if UNITY_WEBGL && !UNITY_EDITOR
                DW_AccountFailed();
#endif
            }
        }
        static void Reconcile(CloudSlot slot)
        {
            if (slot.slot < 0 || slot.slot >= SaveStore.SlotCount) throw new InvalidDataException("Invalid cloud slot.");
            string path = Path.Combine(LocalDirectory, "slot-" + (slot.slot + 1) + ".json");
            string local = File.Exists(path) ? File.ReadAllText(path) : null;
            if (slot.deleted)
            {
                if (local != null && Date(slot.updatedUtc) >= EnvelopeDate(local)) { File.Delete(path); if (File.Exists(path + ".bak")) File.Delete(path + ".bak"); }
                else if (local != null) UploadSlot(slot.slot, local);
                return;
            }
            if (string.IsNullOrEmpty(slot.envelope)) { if (local != null) UploadSlot(slot.slot, local); return; }
            var cloud = JsonUtility.FromJson<SaveEnvelope>(slot.envelope);
            if (cloud == null || cloud.slot != slot.slot || cloud.format != SaveStore.Format || cloud.version != SaveStore.Version || string.IsNullOrEmpty(cloud.payloadJson) || cloud.checksum != EnvelopeChecksum(cloud.payloadJson)) throw new InvalidDataException("Cloud save validation failed.");
            if (local != null && EnvelopeDate(local) > EnvelopeDate(slot.envelope)) { UploadSlot(slot.slot, local); return; }
            if (local != null && local != slot.envelope) File.WriteAllText(path + ".bak", local, new UTF8Encoding(false));
            File.WriteAllText(path + ".tmp", slot.envelope, new UTF8Encoding(false)); File.Copy(path + ".tmp", path, true); File.Delete(path + ".tmp");
        }
        static DateTime EnvelopeDate(string json) { try { return Date(JsonUtility.FromJson<SaveEnvelope>(json)?.savedAtUtc); } catch { return DateTime.MinValue; } }
        static DateTime Date(string text) => DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date) ? date.ToUniversalTime() : DateTime.MinValue;
        static string Digest(string text) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant(); }
        static string EnvelopeChecksum(string text) { unchecked { uint value = 2166136261; foreach (byte b in Encoding.UTF8.GetBytes(text)) { value ^= b; value *= 16777619; } return value.ToString("X8", CultureInfo.InvariantCulture); } }
        public void OnCloudStatus(string json) { var status = JsonUtility.FromJson<SyncStatus>(json); CloudStatus = status != null && status.ok && status.pending == 0 ? "synced" : "pending"; }
        public void OnResearchConsent(string consent) { ResearchConsent = consent == "true"; }
        public void OnAccountSignedOut(string ignored)
        {
            IsReady = false; UserId = ResearchParticipantId = null; ResearchConsent = false; CloudStatus = "signed-out";
            GameSession.State = null; GameSession.Slot = -1; GameSession.PendingScreen = "main-menu"; Time.timeScale = 1; SceneManager.LoadScene("MainMenu");
        }
    }
}
