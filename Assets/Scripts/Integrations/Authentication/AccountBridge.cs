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
        public static bool AuthenticationResolved { get; private set; }
        public static string UserId { get; private set; }
        public static string ResearchParticipantId { get; private set; }
        public static bool ResearchConsent { get; private set; }
        public static bool IsBusy { get; private set; }
        public static bool HasError { get; private set; }
        public static string StatusMessage { get; private set; } = "";
        public static string Email { get; private set; } = "";
        public static bool OfferResearch { get; private set; }
        public static bool IsAccountScreenOpen { get; private set; }
        public static bool ShowingAccount { get; private set; }
        public static event Action StateChanged;
        public static string CloudStatus { get; private set; } = "not-connected";
        public static bool IsAuthenticated => !string.IsNullOrEmpty(UserId);
        public static string LocalNamespace => IsAuthenticated ? "account-" + Digest(UserId).Substring(0, 32) : "not-signed-in";
        public static string LocalDirectory => Path.Combine(Application.persistentDataPath, "approved-ui-saves-v1", LocalNamespace);
        static AccountBridge instance;
#if UNITY_EDITOR
        EditorAccountClient editorClient;
#endif
        [Serializable] sealed class CloudSlot { public int slot = default; public string envelope = default, updatedUtc = default; public bool deleted = default; }
        [Serializable] sealed class AccountPacket { public string uid = default, researchParticipantId = default; public bool researchConsent = default; public CloudSlot[] slots = default; }
        [Serializable] sealed class SyncStatus { public bool ok = default; public string code = default; public int pending = default; }
        [Serializable] sealed class AccountState { public bool busy = default, error = default, resolved = default; public string status = default, email = default; }
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void DW_AccountRegister(string callbackObject);
        [DllImport("__Internal")] static extern void DW_AccountApplied();
        [DllImport("__Internal")] static extern void DW_AccountFailed();
        [DllImport("__Internal")] static extern void DW_AccountUpload(int slot, string envelope);
        [DllImport("__Internal")] static extern void DW_AccountDelete(int slot);
        [DllImport("__Internal")] static extern void DW_AccountLogin(string email, string password);
        [DllImport("__Internal")] static extern void DW_AccountCreate(string email, string password);
        [DllImport("__Internal")] static extern void DW_AccountReset(string email);
        [DllImport("__Internal")] static extern void DW_AccountSignOut();
        [DllImport("__Internal")] static extern void DW_AccountResearchConsent(int consent);
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialise()
        {
            if (instance != null) return;
            AuthenticationResolved = false;
            IsReady = false; UserId = ResearchParticipantId = null; ResearchConsent = false; CloudStatus = "not-connected";
            IsBusy = HasError = OfferResearch = IsAccountScreenOpen = ShowingAccount = false;
            StatusMessage = Email = ""; StateChanged = null;
            var host = new GameObject("PlayerAccountBridge"); instance = host.AddComponent<AccountBridge>(); DontDestroyOnLoad(host);
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = true; DW_AccountRegister(host.name);
#elif UNITY_EDITOR
            instance.editorClient = host.AddComponent<EditorAccountClient>();
            instance.editorClient.Initialize(instance);
            AuthenticationResolved = true;
            instance.editorClient.StateChanged += instance.ReadEditorState;
#else
            IsReady = true; AuthenticationResolved = true; CloudStatus = "local-preview";
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
            if (IsAccountScreenOpen) return;
            IsAccountScreenOpen = true;
            ShowingAccount = IsAuthenticated; ClearStatus();
            SceneManager.LoadSceneAsync("Login", IsAuthenticated ? LoadSceneMode.Additive : LoadSceneMode.Single);
        }
        public static void SignIn(string email, string password)
        {
            if (IsBusy) return;
            OfferResearch = false;
#if UNITY_EDITOR
            instance.editorClient.SignIn(email, password);
#elif UNITY_WEBGL
            DW_AccountLogin(email, password);
#endif
        }
        public static void Register(string email, string password)
        {
            if (IsBusy) return;
            OfferResearch = true;
#if UNITY_EDITOR
            instance.editorClient.Register(email, password);
#elif UNITY_WEBGL
            DW_AccountCreate(email, password);
#endif
        }
        public static void ResetPassword(string email)
        {
            if (IsBusy) return;
#if UNITY_EDITOR
            instance.editorClient.ResetPassword(email);
#elif UNITY_WEBGL
            DW_AccountReset(email);
#endif
        }
        public static void SetResearchConsent(bool consent)
        {
#if UNITY_EDITOR
            instance.editorClient.SetResearchConsent(consent);
#elif UNITY_WEBGL
            DW_AccountResearchConsent(consent ? 1 : 0);
#endif
        }
        public static void SignOut()
        {
#if UNITY_EDITOR
            instance.editorClient.SignOut();
#elif UNITY_WEBGL
            DW_AccountSignOut();
#endif
        }
        public static void ClearStatus()
        {
            StatusMessage = ""; HasError = false;
#if UNITY_EDITOR
            if (instance != null) instance.editorClient.ClearStatus();
#endif
            StateChanged?.Invoke();
        }
        public static void SetScreenOpen(bool open) { IsAccountScreenOpen = open; }
        public static void ResearchDecision(bool consent)
        {
            OfferResearch = false;
            if (consent != ResearchConsent) SetResearchConsent(consent);
            StateChanged?.Invoke();
        }
        public static void CloseAccount()
        {
            if (IsBusy && !IsReady) return;
            var scene = SceneManager.GetSceneByName("Login");
            if (scene.IsValid())
                foreach (var root in scene.GetRootGameObjects())
                {
                    var login = root.GetComponentInChildren<LoginScreen>(true);
                    if (login != null && login.events != null) login.events.enabled = false;
                }
            bool additive = ShowingAccount && SceneManager.sceneCount > 1;
            ShowingAccount = false;
            if (additive && scene.IsValid()) SceneManager.UnloadSceneAsync(scene);
            else SceneManager.LoadSceneAsync("MainMenu");
        }
        public void OnAccountOpen(string ignored) { OpenAccount(); }
        public void OnAccountState(string json)
        {
            var state = JsonUtility.FromJson<AccountState>(json); if (state == null) return;
            AuthenticationResolved |= state.resolved;
            IsBusy = state.busy; HasError = state.error; StatusMessage = state.status ?? ""; Email = state.email ?? "";
            StateChanged?.Invoke();
        }
#if UNITY_EDITOR
        void ReadEditorState()
        {
            IsBusy = editorClient.Busy; HasError = editorClient.HasError;
            StatusMessage = editorClient.Status; Email = editorClient.Email;
            StateChanged?.Invoke();
        }
#endif
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
                IsReady = true; AuthenticationResolved = true;
                // Cloud-wins: στο login το Firestore είναι η μοναδική αλήθεια. Θέσεις που δεν
                // υπάρχουν στο cloud σβήνονται και τοπικά — τα τοπικά αρχεία είναι μόνο cache.
                var covered = new System.Collections.Generic.HashSet<int>();
                foreach (var slot in packet.slots ?? Array.Empty<CloudSlot>()) { Reconcile(slot); covered.Add(slot.slot); }
                for (int i = 0; i < SaveStore.SlotCount; i++)
                    if (!covered.Contains(i)) DeleteLocal(i);
                GameSession.State = null; GameSession.Slot = -1; CloudStatus = "ready";
                SaveStore.FlushWebStorage();
                StateChanged?.Invoke();
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
        // Cloud-wins: ό,τι λέει το Firestore στο login επιβάλλεται στα τοπικά.
        // Τίποτα τοπικό δεν ανεβαίνει στο login και τίποτα τοπικό δεν «ανασταίνει» διαγραμμένη θέση.
        static void Reconcile(CloudSlot slot)
        {
            if (slot.slot < 0 || slot.slot >= SaveStore.SlotCount) throw new InvalidDataException("Invalid cloud slot.");
            if (slot.deleted || string.IsNullOrEmpty(slot.envelope)) { DeleteLocal(slot.slot); return; }
            var cloud = JsonUtility.FromJson<SaveEnvelope>(slot.envelope);
            // Cloud save παλαιότερης έκδοσης = ανύπαρκτη θέση, όχι σφάλμα.
            if (cloud != null && (cloud.format != SaveStore.Format || cloud.version != SaveStore.Version)) { DeleteLocal(slot.slot); return; }
            if (cloud == null || cloud.slot != slot.slot || string.IsNullOrEmpty(cloud.payloadJson) || cloud.checksum != EnvelopeChecksum(cloud.payloadJson)) throw new InvalidDataException("Cloud save validation failed.");
            string path = Path.Combine(LocalDirectory, "slot-" + (slot.slot + 1) + ".json");
            string local = File.Exists(path) ? File.ReadAllText(path) : null;
            if (local != null && local != slot.envelope) File.WriteAllText(path + ".bak", local, new UTF8Encoding(false));
            File.WriteAllText(path + ".tmp", slot.envelope, new UTF8Encoding(false)); File.Copy(path + ".tmp", path, true); File.Delete(path + ".tmp");
        }

        static void DeleteLocal(int slot)
        {
            string path = Path.Combine(LocalDirectory, "slot-" + (slot + 1) + ".json");
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                if (File.Exists(path + suffix)) File.Delete(path + suffix);
        }

        static string Digest(string text) => Hashing.Sha256Hex(text);
        static string EnvelopeChecksum(string text) => Hashing.Fnv1a(text);
        public void OnCloudStatus(string json) { var status = JsonUtility.FromJson<SyncStatus>(json); CloudStatus = status != null && status.ok && status.pending == 0 ? "synced" : "pending"; }
        public void OnResearchConsent(string consent) { ResearchConsent = consent == "true"; StateChanged?.Invoke(); }
        public void OnAccountSignedOut(string ignored)
        {
            IsReady = false; UserId = ResearchParticipantId = null; ResearchConsent = false; CloudStatus = "signed-out";
            IsBusy = HasError = OfferResearch = ShowingAccount = false; StatusMessage = Email = "";
            GameSession.State = null; GameSession.Slot = -1; GameSession.PendingScreen = "main-menu"; Time.timeScale = 1;
            AuthenticationResolved = true;
            SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single); StateChanged?.Invoke();
        }
    }
}
