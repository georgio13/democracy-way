#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Editor counterpart of the Web account adapter. Firebase credentials and
    /// refresh tokens exist only in this component's memory, never in assets,
    /// PlayerPrefs, save envelopes, research events, or the Console.
    /// </summary>
    public sealed class EditorAccountClient : MonoBehaviour
    {
        const string ApiKey = "AIzaSyBZS2LfhWldFXmhQnLzogz5KdinkxKfL4U";
        const string Documents = "https://firestore.googleapis.com/v1/projects/democracy-way/databases/(default)/documents/";
        const string ConsentVersion = "first-prytany-20261019";
        const int MaximumEnvelopeLength = 600000;

        public bool Busy { get; private set; }
        public bool ProfileRequired { get; private set; }
        public bool IsSignedIn => auth != null;
        public bool ResearchConsent => profile != null && profile.researchConsent;
        public string Status { get; private set; } = "";
        public string Email { get; private set; } = "";
        public event Action StateChanged;

        AccountBridge bridge;
        Session auth;
        PlayerProfile profile;
        bool ready;
        int generation;
        Coroutine pump;
        readonly List<PendingSave> pending = new List<PendingSave>();
        readonly HashSet<UnityWebRequest> requests = new HashSet<UnityWebRequest>();

        sealed class Session { public string uid, idToken, refreshToken; public DateTime expires; }
        sealed class PlayerProfile { public string researchParticipantId, consentVersion, consentUtc; public bool researchConsent; }
        sealed class PendingSave { public int slot; public string envelope, updatedUtc; public bool deleted; }
        sealed class Response
        {
            public bool ok;
            public long status;
            public string text, code;
        }
        [Serializable] sealed class AuthRequest { public string email, password; public bool returnSecureToken = true; }
        [Serializable] sealed class ResetRequest { public string requestType = "PASSWORD_RESET", email; }
        [Serializable] sealed class AuthReply { public string localId, idToken, refreshToken, expiresIn; }
        [Serializable] sealed class RefreshReply { public string user_id, id_token, refresh_token, expires_in; }
        [Serializable] sealed class ErrorBody { public ErrorDetails error; }
        [Serializable] sealed class ErrorDetails { public string message, status; }
        [Serializable] sealed class Document { public DocumentFields fields; }
        [Serializable] sealed class DocumentFields
        {
            public Value researchParticipantId, researchConsent, consentVersion, consentUtc;
            public Value slot, envelope, updatedUtc, deleted;
        }
        [Serializable] sealed class Value { public string stringValue, integerValue; public bool booleanValue; }
        [Serializable] sealed class CloudSlot { public int slot; public string envelope, updatedUtc; public bool deleted; }
        [Serializable] sealed class AccountPacket { public string uid, researchParticipantId; public bool researchConsent; public CloudSlot[] slots; }
        [Serializable] sealed class SyncStatus { public bool ok; public string code; public int pending; }

        public void Initialize(AccountBridge accountBridge) { bridge = accountBridge; }

        public void SignIn(string email, string password)
        {
            if (Busy) return;
            if (!ValidateCredentials(email, password)) return;
            ClearSession();
            StartCoroutine(Authenticate(email.Trim(), password, false, false));
        }

        public void Register(string email, string password, bool privacyAccepted, bool researchConsent)
        {
            if (Busy) return;
            if (!privacyAccepted) { SetStatus("Χρειάζεται να διαβάσεις την ενημέρωση λογαριασμού."); return; }
            if (!ValidateCredentials(email, password)) return;
            ClearSession();
            StartCoroutine(Authenticate(email.Trim(), password, true, researchConsent));
        }

        public void CompleteProfile(bool privacyAccepted, bool researchConsent)
        {
            if (Busy || auth == null || !ProfileRequired) return;
            if (!privacyAccepted) { SetStatus("Χρειάζεται να διαβάσεις την ενημέρωση λογαριασμού."); return; }
            StartCoroutine(CompleteExistingProfile(researchConsent));
        }

        public void ResetPassword(string email)
        {
            if (Busy) return;
            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@")) { SetStatus("Έλεγξε τη μορφή του email."); return; }
            StartCoroutine(SendReset(email.Trim()));
        }

        public void SetResearchConsent(bool consent)
        {
            if (Busy || auth == null || profile == null || !ready) return;
            // Revocation takes effect immediately, including while offline.
            profile.researchConsent = consent;
            bridge.OnResearchConsent(consent ? "true" : "false");
            StartCoroutine(UpdateConsent(consent));
        }

        public void UploadSlot(int slot, string envelope)
        {
            if (!ready || auth == null || slot < 0 || slot >= SaveStore.SlotCount) return;
            if (!ValidEnvelope(slot, envelope)) { NotifyCloud(false, "invalid-save"); return; }
            Enqueue(new PendingSave { slot = slot, envelope = envelope, updatedUtc = UtcNow() });
        }

        public void DeleteSlot(int slot)
        {
            if (!ready || auth == null || slot < 0 || slot >= SaveStore.SlotCount) return;
            Enqueue(new PendingSave { slot = slot, envelope = "", deleted = true, updatedUtc = UtcNow() });
        }

        public void SignOut()
        {
            ClearSession();
            Email = "";
            Status = "";
            bridge.OnAccountSignedOut("");
            Changed();
        }

        bool ValidateCredentials(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@")) { SetStatus("Έλεγξε τη μορφή του email."); return false; }
            if (string.IsNullOrEmpty(password) || password.Length < 6) { SetStatus("Ο κωδικός χρειάζεται τουλάχιστον 6 χαρακτήρες."); return false; }
            return true;
        }

        IEnumerator Authenticate(string email, string password, bool register, bool researchConsent)
        {
            Busy = true;
            Email = email;
            SetStatus("Σύνδεση με τον λογαριασμό…");
            int operation = generation;
            var reply = new Response();
            string body = JsonUtility.ToJson(new AuthRequest { email = email, password = password });
            password = null;
            yield return Request(AuthUrl(register ? "signUp" : "signInWithPassword"), "POST", body, null, reply);
            body = null;
            if (operation != generation) yield break;
            if (!reply.ok) { FinishError(reply.code); yield break; }
            AuthReply account = Parse<AuthReply>(reply.text);
            if (account == null || string.IsNullOrEmpty(account.localId) || string.IsNullOrEmpty(account.idToken) || string.IsNullOrEmpty(account.refreshToken))
            { FinishError("INVALID_LOGIN_CREDENTIALS"); yield break; }
            auth = new Session { uid = account.localId, idToken = account.idToken, refreshToken = account.refreshToken, expires = Expiry(account.expiresIn) };
            yield return FinishLogin(operation, register, researchConsent);
        }

        IEnumerator CompleteExistingProfile(bool researchConsent)
        {
            Busy = true;
            SetStatus("Αποθήκευση ενημέρωσης λογαριασμού…");
            yield return FinishLogin(generation, true, researchConsent);
        }

        IEnumerator FinishLogin(int operation, bool allowCreate, bool researchConsent)
        {
            var response = new Response();
            yield return Cloud("players/" + auth.uid, "GET", null, response, operation);
            if (operation != generation) yield break;
            if (!response.ok && response.status == 404)
            {
                if (!allowCreate) { ProfileRequired = true; Busy = false; SetStatus("Διάβασε την ενημέρωση και ολοκλήρωσε τον λογαριασμό σου."); yield break; }
                profile = new PlayerProfile { researchParticipantId = Guid.NewGuid().ToString("N"), researchConsent = researchConsent, consentVersion = ConsentVersion, consentUtc = UtcNow() };
                yield return Cloud("players?documentId=" + Uri.EscapeDataString(auth.uid), "POST", ProfileDocument(profile), response, operation);
                if (operation != generation) yield break;
                if (response.status == 409)
                {
                    yield return Cloud("players/" + auth.uid, "GET", null, response, operation);
                    if (operation != generation) yield break;
                    profile = ProfileFrom(response.text);
                }
            }
            else if (response.ok) profile = ProfileFrom(response.text);
            if (!response.ok) { FinishError(response.code); yield break; }
            if (!ValidProfile(profile)) { ProfileRequired = true; Busy = false; SetStatus("Δεν αναγνωρίστηκαν τα στοιχεία του λογαριασμού. Επικοινώνησε με τον διαχειριστή."); yield break; }
            var slots = new CloudSlot[SaveStore.SlotCount];
            for (int slot = 0; slot < slots.Length; slot++)
            {
                yield return Cloud("players/" + auth.uid + "/saves/" + slot, "GET", null, response, operation);
                if (operation != generation) yield break;
                if (!response.ok && response.status != 404) { FinishError(response.code); yield break; }
                if (response.status == 404) { slots[slot] = new CloudSlot { slot = slot, envelope = "", updatedUtc = "" }; continue; }
                var fields = Parse<Document>(response.text)?.fields;
                if (fields == null || fields.slot == null || fields.slot.integerValue != slot.ToString(CultureInfo.InvariantCulture) || fields.deleted == null || fields.envelope == null || fields.updatedUtc == null)
                { FinishError("INVALID_CLOUD_SAVE"); yield break; }
                slots[slot] = new CloudSlot { slot = slot, deleted = fields.deleted.booleanValue, envelope = fields.envelope.stringValue ?? "", updatedUtc = fields.updatedUtc.stringValue ?? "" };
            }
            ProfileRequired = false;
            ready = true; // Reconciliation may enqueue a newer local envelope.
            bridge.OnAccountReady(JsonUtility.ToJson(new AccountPacket { uid = auth.uid, researchParticipantId = profile.researchParticipantId, researchConsent = profile.researchConsent, slots = slots }));
            if (!AccountBridge.IsReady) { ready = false; FinishError("INVALID_CLOUD_SAVE"); yield break; }
            Busy = false;
            SetStatus("Η σύνδεση και ο συγχρονισμός ολοκληρώθηκαν.");
        }

        IEnumerator SendReset(string email)
        {
            Busy = true;
            SetStatus("Αποστολή οδηγιών…");
            int operation = generation;
            var response = new Response();
            yield return Request(AuthUrl("sendOobCode"), "POST", JsonUtility.ToJson(new ResetRequest { email = email }), null, response);
            if (operation != generation) yield break;
            Busy = false;
            if (response.ok || response.code == "EMAIL_NOT_FOUND") SetStatus("Αν υπάρχει λογαριασμός, έλεγξε το email σου για οδηγίες.");
            else SetStatus(ErrorMessage(response.code));
        }

        IEnumerator UpdateConsent(bool consent)
        {
            Busy = true;
            Changed();
            int operation = generation;
            profile.researchConsent = consent;
            profile.consentVersion = ConsentVersion;
            profile.consentUtc = UtcNow();
            var response = new Response();
            yield return Cloud("players/" + auth.uid, "PATCH", ProfileDocument(profile), response, operation);
            if (operation != generation) yield break;
            Busy = false;
            SetStatus(response.ok ? "Η προτίμησή σου αποθηκεύτηκε." : "Η προτίμηση ισχύει στην τρέχουσα συνεδρία, αλλά δεν συγχρονίστηκε. Δοκίμασε ξανά όταν υπάρχει σύνδεση.");
        }

        void Enqueue(PendingSave item)
        {
            // Coalesce queued edits for a slot. An in-flight instance is removed
            // only by reference, so replacing it cannot lose a newer autosave.
            pending.RemoveAll(entry => entry.slot == item.slot);
            pending.Add(item);
            NotifyCloud(false, "queued");
            if (pump == null) pump = StartCoroutine(Pump(generation));
        }

        IEnumerator Pump(int operation)
        {
            // A first yield also allows StartCoroutine's handle to be assigned.
            yield return null;
            while (ready && auth != null && operation == generation && pending.Count > 0)
            {
                PendingSave item = pending[0];
                var response = new Response();
                yield return Cloud("players/" + auth.uid + "/saves/" + item.slot, "PATCH", SaveDocument(item), response, operation);
                if (operation != generation) yield break;
                if (response.ok) { pending.Remove(item); NotifyCloud(true, "saved"); }
                else { NotifyCloud(false, response.code); yield return new WaitForSecondsRealtime(15); }
            }
            if (operation == generation) pump = null;
        }

        IEnumerator Cloud(string path, string method, string body, Response response, int operation)
        {
            if (auth == null || operation != generation) { response.ok = false; response.code = "SIGNED_OUT"; yield break; }
            if (auth.expires <= DateTime.UtcNow.AddMinutes(1))
            {
                string expectedUid = auth.uid;
                var refreshed = new Response();
                string form = "grant_type=refresh_token&refresh_token=" + Uri.EscapeDataString(auth.refreshToken);
                yield return Request("https://securetoken.googleapis.com/v1/token?key=" + ApiKey, "POST", form, null, refreshed, "application/x-www-form-urlencoded");
                if (operation != generation) yield break;
                var token = refreshed.ok ? Parse<RefreshReply>(refreshed.text) : null;
                if (token == null || token.user_id != expectedUid || string.IsNullOrEmpty(token.id_token) || string.IsNullOrEmpty(token.refresh_token))
                { response.ok = false; response.status = refreshed.status; response.code = refreshed.code ?? "INVALID_LOGIN_CREDENTIALS"; yield break; }
                auth.idToken = token.id_token;
                auth.refreshToken = token.refresh_token;
                auth.expires = Expiry(token.expires_in);
            }
            yield return Request(Documents + path, method, body, auth.idToken, response);
        }

        IEnumerator Request(string url, string method, string body, string bearer, Response response, string contentType = "application/json")
        {
            using (var request = new UnityWebRequest(url, method))
            {
                request.timeout = 30;
                request.downloadHandler = new DownloadHandlerBuffer();
                if (body != null) { request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)); request.SetRequestHeader("Content-Type", contentType); }
                if (!string.IsNullOrEmpty(bearer)) request.SetRequestHeader("Authorization", "Bearer " + bearer);
                requests.Add(request);
                try
                {
                    yield return request.SendWebRequest();
                    response.status = request.responseCode;
                    response.text = request.downloadHandler.text;
                    response.ok = request.result == UnityWebRequest.Result.Success;
                    if (response.ok) response.code = null;
                    else
                    {
                        var details = Parse<ErrorBody>(response.text)?.error;
                        response.code = details?.status == "PERMISSION_DENIED" ? "PERMISSION_DENIED" : details?.message ?? details?.status ?? "NETWORK";
                    }
                }
                finally { requests.Remove(request); }
            }
        }

        void ClearSession()
        {
            generation++;
            StopAllCoroutines();
            foreach (var request in requests) request.Abort();
            requests.Clear();
            pending.Clear();
            pump = null;
            auth = null;
            profile = null;
            ready = false;
            Busy = ProfileRequired = false;
        }

        void OnDestroy() { ClearSession(); }
        void FinishError(string code) { Busy = false; ready = false; SetStatus(ErrorMessage(code)); }
        void SetStatus(string message) { Status = message; Changed(); }
        void Changed() { StateChanged?.Invoke(); }
        void NotifyCloud(bool success, string code) { if (bridge != null) bridge.OnCloudStatus(JsonUtility.ToJson(new SyncStatus { ok = success, code = code, pending = pending.Count })); }
        static string UtcNow() => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        static string AuthUrl(string method) => "https://identitytoolkit.googleapis.com/v1/accounts:" + method + "?key=" + ApiKey;
        static DateTime Expiry(string seconds) => DateTime.UtcNow.AddSeconds(double.TryParse(seconds, NumberStyles.Float, CultureInfo.InvariantCulture, out double duration) ? Math.Max(1, duration) : 3600);
        static T Parse<T>(string json) where T : class { try { return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<T>(json); } catch (ArgumentException) { return null; } }
        static string Quote(string value)
        {
            var json = new StringBuilder("\"");
            foreach (char character in value ?? "")
            {
                switch (character)
                {
                    case '"': json.Append("\\\""); break;
                    case '\\': json.Append("\\\\"); break;
                    case '\b': json.Append("\\b"); break;
                    case '\f': json.Append("\\f"); break;
                    case '\n': json.Append("\\n"); break;
                    case '\r': json.Append("\\r"); break;
                    case '\t': json.Append("\\t"); break;
                    default:
                        if (character < 32 || char.IsSurrogate(character) || character == '\u2028' || character == '\u2029')
                            json.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else json.Append(character);
                        break;
                }
            }
            return json.Append('"').ToString();
        }
        static string TextField(string name, string value) => Quote(name) + ":{\"stringValue\":" + Quote(value) + "}";
        static string BooleanField(string name, bool value) => Quote(name) + ":{\"booleanValue\":" + (value ? "true" : "false") + "}";
        static string IntegerField(string name, int value) => Quote(name) + ":{\"integerValue\":" + Quote(value.ToString(CultureInfo.InvariantCulture)) + "}";
        static string ProfileDocument(PlayerProfile value) => "{\"fields\":{" + TextField("researchParticipantId", value.researchParticipantId) + "," + BooleanField("researchConsent", value.researchConsent) + "," + TextField("consentVersion", value.consentVersion) + "," + TextField("consentUtc", value.consentUtc) + "}}";
        static string SaveDocument(PendingSave value) => "{\"fields\":{" + IntegerField("schemaVersion", 1) + "," + IntegerField("slot", value.slot) + "," + TextField("envelope", value.envelope) + "," + BooleanField("deleted", value.deleted) + "," + TextField("updatedUtc", value.updatedUtc) + "}}";
        static PlayerProfile ProfileFrom(string json)
        {
            var fields = Parse<Document>(json)?.fields;
            if (fields?.researchParticipantId == null || fields.researchConsent == null || fields.consentVersion == null || fields.consentUtc == null) return null;
            return new PlayerProfile { researchParticipantId = fields.researchParticipantId.stringValue, researchConsent = fields.researchConsent.booleanValue, consentVersion = fields.consentVersion.stringValue, consentUtc = fields.consentUtc.stringValue };
        }
        static bool ValidProfile(PlayerProfile value)
        {
            if (value == null || value.researchParticipantId == null || value.researchParticipantId.Length != 32) return false;
            foreach (char c in value.researchParticipantId) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }
        static bool ValidEnvelope(int slot, string json)
        {
            if (string.IsNullOrEmpty(json) || json.Length > MaximumEnvelopeLength) return false;
            var value = Parse<SaveEnvelope>(json);
            return value != null && value.slot == slot && value.format == SaveStore.Format && value.version == SaveStore.Version && !string.IsNullOrEmpty(value.payloadJson);
        }
        static string ErrorMessage(string code)
        {
            code = (code ?? "NETWORK").Split(new[] { " : " }, StringSplitOptions.None)[0];
            switch (code)
            {
                case "EMAIL_EXISTS": return "Υπάρχει ήδη λογαριασμός με αυτό το email.";
                case "INVALID_EMAIL": return "Έλεγξε τη μορφή του email.";
                case "WEAK_PASSWORD": return "Ο κωδικός χρειάζεται τουλάχιστον 6 χαρακτήρες.";
                case "INVALID_LOGIN_CREDENTIALS": case "INVALID_PASSWORD": case "EMAIL_NOT_FOUND": return "Το email ή ο κωδικός δεν είναι σωστά.";
                case "TOO_MANY_ATTEMPTS_TRY_LATER": return "Έγιναν πολλές προσπάθειες. Δοκίμασε ξανά αργότερα.";
                case "USER_DISABLED": return "Ο λογαριασμός δεν είναι ενεργός.";
                case "OPERATION_NOT_ALLOWED": return "Η σύνδεση Email/Password δεν έχει ενεργοποιηθεί στο Firebase.";
                case "PERMISSION_DENIED": return "Δεν υπάρχουν δικαιώματα συγχρονισμού. Χρειάζεται έλεγχος των κανόνων Firestore από τον διαχειριστή.";
                case "INVALID_CLOUD_SAVE": return "Δεν ολοκληρώθηκε η εισαγωγή των αποθηκεύσεων. Η πρόοδος δεν διαγράφηκε. Δοκίμασε ξανά τη σύνδεση.";
                case "TOKEN_EXPIRED": case "INVALID_REFRESH_TOKEN": case "USER_NOT_FOUND": case "INVALID_ID_TOKEN": return "Η συνεδρία έληξε. Συνδέσου ξανά.";
                case "NETWORK": return "Δεν υπάρχει σύνδεση με τον διακομιστή. Δοκίμασε ξανά όταν αποκατασταθεί η σύνδεση.";
                default: return "Δεν ολοκληρώθηκε η ενέργεια. Δοκίμασε ξανά.";
            }
        }
    }
}
#endif
