using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    /// <summary>One authored account view, shared by Editor and Web builds.</summary>
    public sealed class LoginScreen : MonoBehaviour
    {
        public TMP_InputField email, password;
        public RectTransform card;
        public TMP_Text title, message, submitLabel, information, profileText;
        public PanelGraphic messagePanel;
        public GameObject tabs, form, passwordRow, statusRoot, informationRoot, profileRoot, researchRoot;
        public Button loginTab, registerTab, submit, resetLink, informationLink, informationBack;
        public Button profileBack, signOut, researchPreferences, researchYes, researchNo;
        public EventSystem events;
        enum View { Login, Register, Reset, Information, Profile, Research }
        View view, returnView;
        EventSystem[] previousEvents;
        bool restorePreviousEvents;
        bool leaving;
        CanvasScaler scaler;
        int viewportWidth, viewportHeight;
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern double DW_AccountPixelRatio();
#endif
        static readonly Color Paper = new Color32(238, 228, 205, 255);
        static readonly Color Gold = new Color32(168, 140, 82, 255);
        static readonly Color Ink = new Color32(16, 31, 36, 255);

        void Awake()
        {
            scaler = GetComponent<CanvasScaler>(); Resize();
            AccountBridge.SetScreenOpen(true);
            restorePreviousEvents = AccountBridge.ShowingAccount;
            // An account view opened additively must own input without replacing
            // the current case or destroying its transient conversation state.
            previousEvents = System.Array.FindAll(FindObjectsByType<EventSystem>(), previous => previous != events && previous.enabled);
            foreach (var previous in previousEvents)
                if (previous != events && previous.enabled) previous.enabled = false;
            if (events != null) { events.enabled = true; events.firstSelectedGameObject = null; events.SetSelectedGameObject(null); }
            loginTab.onClick.AddListener(() => ChangeView(View.Login));
            registerTab.onClick.AddListener(() => ChangeView(View.Register));
            resetLink.onClick.AddListener(() => ChangeView(view == View.Reset ? View.Login : View.Reset));
            submit.onClick.AddListener(Submit);
            email.onSubmit.AddListener(_ => Submit()); password.onSubmit.AddListener(_ => Submit());
            informationLink.onClick.AddListener(() => { returnView = view; view = View.Information; Refresh(); });
            informationBack.onClick.AddListener(() => { view = returnView; Refresh(); });
            profileBack.onClick.AddListener(AccountBridge.CloseAccount);
            signOut.onClick.AddListener(() => { email.text = password.text = ""; AccountBridge.SignOut(); });
            researchPreferences.onClick.AddListener(() => { view = View.Research; AccountBridge.ClearStatus(); Refresh(); });
            researchYes.onClick.AddListener(() => ResearchDecision(true));
            researchNo.onClick.AddListener(() => ResearchDecision(false));
            AccountBridge.StateChanged += Refresh;
        }

        void Start() { Refresh(); }

        void Update()
        {
            if (Screen.width != viewportWidth || Screen.height != viewportHeight) Resize();
        }

        void Resize()
        {
            viewportWidth = Screen.width; viewportHeight = Screen.height;
            if (scaler == null || viewportWidth <= 0 || viewportHeight <= 0) return;
            float pixelsPerCssPixel = 1;
#if UNITY_WEBGL && !UNITY_EDITOR
            pixelsPerCssPixel = Mathf.Max(.25f, (float)DW_AccountPixelRatio());
#endif
            // Match the original CSS card: a fixed readable size when there is
            // room, otherwise fit the entire panel with a small safe margin.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = Mathf.Max(.1f, Mathf.Min(pixelsPerCssPixel,
                (viewportWidth - 44 * pixelsPerCssPixel) / 640f,
                (viewportHeight - 44 * pixelsPerCssPixel) / 820f));
        }

        void OnDestroy()
        {
            AccountBridge.StateChanged -= Refresh;
            AccountBridge.SetScreenOpen(false);
            if (restorePreviousEvents)
                foreach (var previous in previousEvents ?? System.Array.Empty<EventSystem>())
                    if (previous != null && previous != events) previous.enabled = true;
        }

        void ChangeView(View next)
        {
            if (AccountBridge.IsBusy) return;
            view = next; password.text = ""; AccountBridge.ClearStatus(); Refresh();
        }

        void Submit()
        {
            if (AccountBridge.IsBusy || AccountBridge.IsReady) return;
            string secret = password.text; password.text = "";
            if (view == View.Register) AccountBridge.Register(email.text, secret);
            else if (view == View.Reset) AccountBridge.ResetPassword(email.text);
            else AccountBridge.SignIn(email.text, secret);
        }

        void ResearchDecision(bool consent)
        {
            AccountBridge.ResearchDecision(consent);
            if (AccountBridge.ShowingAccount) { view = View.Profile; Refresh(); }
            else Leave();
        }

        void Leave()
        {
            if (leaving) return;
            leaving = true;
            if (events != null) events.enabled = false;
            AccountBridge.CloseAccount();
        }

        void Refresh()
        {
            if (!isActiveAndEnabled || leaving) return;
            bool ready = AccountBridge.IsReady;
            if (ready && !AccountBridge.ShowingAccount && view != View.Information)
            {
                if (AccountBridge.OfferResearch) view = View.Research;
                else { if (!AccountBridge.IsBusy) Leave(); return; }
            }
            if (ready && AccountBridge.ShowingAccount && view != View.Information && view != View.Research) view = View.Profile;
            bool showForm = view == View.Login || view == View.Register || view == View.Reset;
            bool reset = view == View.Reset;
            card.sizeDelta = new Vector2(640, reset ? 650 : 820);
            ((RectTransform)form.transform).anchoredPosition = new Vector2(40, reset ? -270 : -324);
            ((RectTransform)statusRoot.transform).anchoredPosition = new Vector2(40, reset ? -378 : -548);
            ((RectTransform)submit.transform).anchoredPosition = new Vector2(40, reset ? -480 : -643);
            ((RectTransform)resetLink.transform).anchoredPosition = new Vector2(40, reset ? -552 : -710);
            tabs.SetActive(showForm && !reset); form.SetActive(showForm);
            passwordRow.SetActive(view != View.Reset);
            informationRoot.SetActive(view == View.Information);
            informationBack.gameObject.SetActive(view == View.Information);
            profileRoot.SetActive(view == View.Profile);
            researchRoot.SetActive(view == View.Research);
            submit.gameObject.SetActive(showForm);
            resetLink.gameObject.SetActive(view == View.Login || reset);
            resetLink.GetComponentInChildren<TMP_Text>().text = reset ? "Επιστροφή στη σύνδεση" : "Ξέχασα τον κωδικό";
            informationLink.gameObject.SetActive(false);
            title.text = view == View.Information ? "Ενημέρωση λογαριασμού" : view == View.Profile ? "Ο λογαριασμός σου" : view == View.Research ? "Συμμετοχή στην έρευνα" : view == View.Reset ? "Επαναφορά κωδικού" : "Είσοδος στην πόλη";
            submitLabel.text = AccountBridge.IsBusy ? "Περίμενε…" : view == View.Register ? "Εγγραφή" : view == View.Reset ? "Αποστολή οδηγιών" : "Σύνδεση";
            StyleTab(loginTab, view != View.Register); StyleTab(registerTab, view == View.Register);
            bool hasStatus = !string.IsNullOrWhiteSpace(AccountBridge.StatusMessage);
            statusRoot.SetActive(hasStatus && view != View.Information && view != View.Research && !ready);
            message.text = AccountBridge.StatusMessage;
            message.color = AccountBridge.HasError ? new Color32(255, 222, 213, 255) : Paper;
            messagePanel.color = AccountBridge.HasError ? new Color32(71, 34, 40, 255) : new Color32(29, 55, 61, 255);
            messagePanel.borderColor = AccountBridge.HasError ? new Color32(197, 107, 92, 255) : Gold;
            messagePanel.SetVerticesDirty();
            profileText.text = AccountBridge.Email + "\n\nΗ πρόοδός σου αποθηκεύεται ιδιωτικά στον λογαριασμό σου.\n\nΕρευνητική καταγραφή: " + (AccountBridge.ResearchConsent ? "ενεργή" : "ανενεργή") + ".";
            researchPreferences.GetComponentInChildren<TMP_Text>().text = "Ρύθμιση ερευνητικής συμμετοχής";
            foreach (var button in GetComponentsInChildren<Button>(true)) button.interactable = !AccountBridge.IsBusy;
            email.interactable = password.interactable = !AccountBridge.IsBusy;
            if (events != null && !email.isFocused && !password.isFocused) events.SetSelectedGameObject(null);
        }

        static void StyleTab(Button button, bool active)
        {
            var shape = button.GetComponent<PanelGraphic>();
            shape.color = active ? Gold : new Color32(25, 49, 55, 255); shape.SetVerticesDirty();
            button.GetComponentInChildren<TMP_Text>().color = active ? Ink : Paper;
        }
    }
}
