#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    /// <summary>Native counterpart of the browser account screen, for Editor Play.</summary>
    public sealed class EditorAccountPanel : MonoBehaviour
    {
        EditorAccountClient client;
        RectTransform overlay, card, consentRoot;
        GameObject accountControl;
        TMP_FontAsset font;
        TMP_InputField email, password;
        TMP_Text heading, message, primaryLabel, emailLabel;
        Toggle privacy, research;
        Button loginTab, registerTab, primary, reset, close, signOut;
        bool registration, accountOpen, readyLastFrame;

        public void Initialize(EditorAccountClient accountClient)
        {
            client = accountClient;
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/IFKargoSans/IFKargoSans-Regular SDF.asset");
            Build();
            client.StateChanged += Refresh;
            Refresh();
        }

        void OnDestroy() { if (client != null) client.StateChanged -= Refresh; }

        void Update()
        {
            // Also reacts when local reconciliation fails or Play starts without domain reload.
            if (readyLastFrame != AccountBridge.IsReady) Refresh();
        }

        public void OpenAccount() { accountOpen = true; Refresh(); }

        void Build()
        {
            var canvasRoot = CivicUIFactory.Rect(transform, "Editor account canvas");
            var canvas = canvasRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
            var scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            canvasRoot.gameObject.AddComponent<GraphicRaycaster>();

            overlay = CivicUIFactory.Rect(canvasRoot, "Account gate");
            CivicUIFactory.Fill(overlay, Vector2.zero, Vector2.zero);
            overlay.gameObject.AddComponent<CanvasRenderer>();
            overlay.gameObject.AddComponent<Image>().color = new Color32(10, 25, 28, 245);

            card = CivicUIFactory.Panel(overlay, "Account panel", CivicUIFactory.Paper).rectTransform;
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
            card.sizeDelta = new Vector2(1040, 880);
            // BeforeSceneLoad can precede the scene's first paper Image.
            // Load the same authored skin rather than duplicating a UI texture.
            if (card.Find("Approved paper artwork") == null)
            {
                var skin = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Shared/Frames/FrameParchment.png");
                if (skin != null)
                {
                    var art = CivicUIFactory.Rect(card, "Approved paper artwork");
                    CivicUIFactory.Fill(art, Vector2.zero, Vector2.zero);
                    art.gameObject.AddComponent<CanvasRenderer>();
                    var paper = art.gameObject.AddComponent<Image>(); paper.sprite = skin;
                    paper.type = Image.Type.Sliced; paper.pixelsPerUnitMultiplier = 4; paper.raycastTarget = false;
                }
            }
            var accent = CivicUIFactory.Panel(card, "Teal heading", CivicUIFactory.Teal).rectTransform;
            CivicUIFactory.Box(accent, 18, 18, 1004, 116);
            var logo = CivicUIFactory.Rect(accent, "Game logo");
            CivicUIFactory.Box(logo, 24, 9, 190, 98);
            logo.gameObject.AddComponent<CanvasRenderer>();
            var image = logo.gameObject.AddComponent<RawImage>();
            image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/UI/Logo.png"); image.raycastTarget = false;
            if (image.texture != null)
            {
                float scale = Mathf.Min(190f / image.texture.width, 98f / image.texture.height);
                logo.sizeDelta = new Vector2(image.texture.width * scale, image.texture.height * scale);
                logo.anchoredPosition += new Vector2((190 - logo.sizeDelta.x) * .5f, -(98 - logo.sizeDelta.y) * .5f);
            }
            heading = Text(accent, "Heading", "Σύνδεση", 250, 24, 700, 64, 38, CivicUIFactory.Paper);

            loginTab = Button(card, "Login tab", "Σύνδεση", 68, 163, 430, 56, () => SetMode(false));
            registerTab = Button(card, "Register tab", "Δημιουργία λογαριασμού", 542, 163, 430, 56, () => SetMode(true));
            Text(card, "Email heading", "Email", 68, 248, 900, 32, 25, CivicUIFactory.Ink);
            email = Field(card, "Account email", 68, 287, 904, "Το email σου", false);
            emailLabel = Text(card, "Signed in email", "", 68, 245, 904, 80, 29, CivicUIFactory.Ink);
            Text(card, "Password heading", "Κωδικός", 68, 365, 900, 32, 25, CivicUIFactory.Ink);
            password = Field(card, "Account password", 68, 404, 904, "Τουλάχιστον 6 χαρακτήρες", true);

            consentRoot = CivicUIFactory.Rect(card, "Account consent");
            CivicUIFactory.Box(consentRoot, 68, 489, 904, 210);
            Text(consentRoot, "Privacy notice", "Το email χρησιμοποιείται για τη σύνδεση και την ανάκτηση κωδικού. Η πρόοδος αποθηκεύεται ιδιωτικά στον λογαριασμό σου. Η ερευνητική καταγραφή είναι προαιρετική, με ψευδώνυμο αναγνωριστικό και διατήρηση 90 ημερών. Επικοινωνία: giorgoschatziparaskevas@gmail.com", 0, 0, 904, 108, 22, CivicUIFactory.Ink);
            privacy = Checkbox(consentRoot, "Privacy acknowledgement", "Διάβασα την ενημέρωση λογαριασμού.", 122);
            research = Checkbox(consentRoot, "Research consent", "Συναινώ στην προαιρετική ερευνητική καταγραφή.", 169);
            research.onValueChanged.AddListener(value => { if (AccountBridge.IsReady) client.SetResearchConsent(value); });

            message = Text(card, "Account status", "", 68, 720, 904, 55, 24, CivicUIFactory.Ink);
            primary = Button(card, "Account submit", "Σύνδεση", 542, 789, 430, 58, Submit);
            primaryLabel = primary.GetComponentInChildren<TMP_Text>();
            reset = Button(card, "Reset password", "Ξέχασα τον κωδικό", 68, 789, 430, 58, () => client.ResetPassword(email.text));
            close = Button(card, "Close account", "Επιστροφή", 542, 789, 430, 58, () => { accountOpen = false; Refresh(); });
            signOut = Button(card, "Sign out", "Αποσύνδεση", 68, 789, 430, 58, () =>
            {
                email.text = password.text = ""; registration = accountOpen = false;
                privacy.SetIsOnWithoutNotify(false); research.SetIsOnWithoutNotify(false);
                client.SignOut();
            });
            var control = Button(canvasRoot, "Account control", "Λογαριασμός", 0, 0, 240, 52, OpenAccount);
            var controlRect = (RectTransform)control.transform;
            controlRect.anchorMin = controlRect.anchorMax = controlRect.pivot = Vector2.up;
            controlRect.anchoredPosition = new Vector2(26, -22); accountControl = control.gameObject;
        }

        TMP_Text Text(Transform parent, string name, string value, float x, float y, float w, float h, float size, Color color)
        {
            var text = CivicUIFactory.Text(parent, name, value, font, size, color);
            CivicUIFactory.Box(text.rectTransform, x, y, w, h); return text;
        }

        Button Button(Transform parent, string name, string text, float x, float y, float w, float h, UnityEngine.Events.UnityAction action)
        {
            var button = CivicUIFactory.Button(parent, name, text, font, () => action(), null);
            CivicUIFactory.Box((RectTransform)button.transform, x, y, w, h); return button;
        }

        TMP_InputField Field(Transform parent, string name, float x, float y, float width, string placeholder, bool secret)
        {
            var shape = CivicUIFactory.Panel(parent, name, new Color32(245, 238, 218, 255));
            CivicUIFactory.Box(shape.rectTransform, x, y, width, 60);
            var viewport = CivicUIFactory.Rect(shape.transform, "Text viewport");
            CivicUIFactory.Fill(viewport, new Vector2(18, 6), new Vector2(18, 6));
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = CivicUIFactory.Text(viewport, "Value", "", font, 28, CivicUIFactory.Ink);
            CivicUIFactory.Fill(text.rectTransform, Vector2.zero, Vector2.zero);
            text.textWrappingMode = TextWrappingModes.NoWrap; text.richText = false;
            var hint = CivicUIFactory.Text(viewport, "Placeholder", placeholder, font, 26, new Color32(100, 109, 99, 255));
            CivicUIFactory.Fill(hint.rectTransform, Vector2.zero, Vector2.zero);
            var input = shape.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = viewport; input.textComponent = text; input.placeholder = hint;
            input.targetGraphic = shape; input.fontAsset = font; input.pointSize = 28;
            input.contentType = secret ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.EmailAddress;
            input.asteriskChar = '*'; input.characterLimit = secret ? 256 : 254;
            input.navigation = new Navigation { mode = Navigation.Mode.None };
            input.onSubmit.AddListener(_ => Submit()); return input;
        }

        Toggle Checkbox(Transform parent, string name, string label, float y)
        {
            var root = CivicUIFactory.Rect(parent, name); CivicUIFactory.Box(root, 0, y, 904, 38);
            root.gameObject.AddComponent<CanvasRenderer>();
            var target = root.gameObject.AddComponent<Image>(); target.color = Color.clear;
            var box = CivicUIFactory.Panel(root, "Box", CivicUIFactory.Teal);
            CivicUIFactory.Box(box.rectTransform, 0, 2, 34, 34); box.cut = 3;
            var mark = CivicUIFactory.Rect(box.transform, "Tick"); CivicUIFactory.Fill(mark, Vector2.zero, Vector2.zero);
            mark.gameObject.AddComponent<CanvasRenderer>();
            var check = mark.gameObject.AddComponent<CheckmarkGraphic>(); check.color = CivicUIFactory.Gold; check.raycastTarget = false;
            Text(root, "Label", label, 48, 1, 856, 36, 23, CivicUIFactory.Ink);
            var toggle = root.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = target; toggle.graphic = check;
            toggle.navigation = new Navigation { mode = Navigation.Mode.None }; toggle.isOn = false; return toggle;
        }

        void SetMode(bool create)
        {
            registration = create; password.text = "";
            privacy.SetIsOnWithoutNotify(false); research.SetIsOnWithoutNotify(false);
            Refresh();
        }

        void Submit()
        {
            if (client.Busy || AccountBridge.IsReady) return;
            string secret = password.text; password.text = "";
            if (client.ProfileRequired) client.CompleteProfile(privacy.isOn, research.isOn);
            else if (registration) client.Register(email.text, secret, privacy.isOn, research.isOn);
            else client.SignIn(email.text, secret);
        }

        void Refresh()
        {
            if (overlay == null || client == null) return;
            bool ready = AccountBridge.IsReady && AccountBridge.IsAuthenticated;
            readyLastFrame = AccountBridge.IsReady;
            bool visible = !ready || accountOpen;
            overlay.gameObject.SetActive(visible);
            accountControl.SetActive(ready && !accountOpen);
            if (!visible) return;
            bool profile = client.ProfileRequired;
            bool consent = ready || registration || profile;
            heading.text = ready ? "Ο λογαριασμός σου" : profile ? "Ολοκλήρωση λογαριασμού" : registration ? "Δημιουργία λογαριασμού" : "Σύνδεση";
            emailLabel.gameObject.SetActive(ready); emailLabel.text = client.Email;
            email.gameObject.SetActive(!ready); password.gameObject.SetActive(!ready && !profile);
            card.Find("Email heading").gameObject.SetActive(!ready);
            card.Find("Password heading").gameObject.SetActive(!ready && !profile);
            loginTab.gameObject.SetActive(!ready && !profile); registerTab.gameObject.SetActive(!ready && !profile);
            StyleTab(loginTab, !registration); StyleTab(registerTab, registration);
            consentRoot.gameObject.SetActive(consent); privacy.gameObject.SetActive(!ready);
            if (ready) research.SetIsOnWithoutNotify(client.ResearchConsent);
            primary.gameObject.SetActive(!ready); reset.gameObject.SetActive(!ready && !registration && !profile);
            close.gameObject.SetActive(ready); signOut.gameObject.SetActive(ready);
            primaryLabel.text = client.Busy ? "Περίμενε…" : profile ? "Ολοκλήρωση" : registration ? "Εγγραφή" : "Σύνδεση";
            primary.interactable = !client.Busy;
            reset.interactable = loginTab.interactable = registerTab.interactable = !client.Busy;
            email.interactable = !client.Busy && !profile; password.interactable = !client.Busy;
            privacy.interactable = research.interactable = signOut.interactable = close.interactable = !client.Busy;
            message.text = client.Status;
            if (EventSystem.current != null && !email.isFocused && !password.isFocused)
                EventSystem.current.SetSelectedGameObject(null);
        }

        static void StyleTab(Button tab, bool active)
        {
            tab.GetComponent<PanelGraphic>().color = active ? CivicUIFactory.Gold : CivicUIFactory.Teal;
            tab.GetComponentInChildren<TMP_Text>().color = active ? CivicUIFactory.Ink : CivicUIFactory.Paper;
        }
    }
}
#endif
