using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DemocracyWay.UI
{
    /// <summary>
    /// Full-screen credits overlay shown after the story ends (post-test →
    /// <c>-> END</c>). Builds itself lazily at runtime — no prefab or scene
    /// hierarchy required. The panel auto-scrolls the credits text upward and
    /// provides a "Αρχική Σελίδα" button to return to the main menu.
    ///
    /// Usage: call <see cref="Show"/> from <c>SceneController.HandleStoryEnd</c>.
    /// The panel parents itself under the first active Canvas it can find.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Credits Panel")]
    [DisallowMultipleComponent]
    public class CreditsPanel : MonoBehaviour
    {
        public event Action OnReturnToMenu;

        private CanvasGroup canvasGroup;
        private RectTransform scrollContent;
        private float scrollSpeed = 30f;
        private bool scrolling;

        /// <summary>
        /// Creates and shows a credits panel. Returns the instance so the
        /// caller can subscribe to <see cref="OnReturnToMenu"/>.
        /// </summary>
        public static CreditsPanel Create(Transform parent, Action onReturnToMenu)
        {
            // Own Canvas + GraphicRaycaster so clicks always work regardless
            // of which parent Canvas we end up under.
            var go = new GameObject("CreditsPanel", typeof(RectTransform),
                typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster),
                typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 200; // above scene UI and gestures

            StretchFill(go.GetComponent<RectTransform>());

            var panel = go.AddComponent<CreditsPanel>();
            panel.canvasGroup = go.GetComponent<CanvasGroup>();
            go.GetComponent<Image>().color = new Color(0.03f, 0.03f, 0.08f, 0.98f);

            if (onReturnToMenu != null)
                panel.OnReturnToMenu += onReturnToMenu;

            panel.Build();
            panel.StartCoroutine(panel.FadeIn());
            return panel;
        }

        private void Build()
        {
            var root = GetComponent<RectTransform>();

            // ─── Top decorative line ───
            MakeLine(root, new Vector2(0.2f, 0.92f), new Vector2(0.8f, 0.922f));

            // ─── Title ───
            PlaceTMP(root, "Title", "Οδός Δημοκρατίας", 52,
                new Color(0.95f, 0.88f, 0.68f), TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.92f));

            // ─── Subtitle ───
            PlaceTMP(root, "Subtitle", "Democracy Way", 28,
                new Color(0.75f, 0.72f, 0.60f), TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.80f), new Vector2(0.9f, 0.85f));

            MakeLine(root, new Vector2(0.2f, 0.79f), new Vector2(0.8f, 0.792f));

            // ─── Scrollable credits area ───
            // Mask container
            var maskGO = new GameObject("CreditsMask", typeof(RectTransform),
                typeof(Image), typeof(Mask));
            maskGO.transform.SetParent(root, false);
            SetAnchors(maskGO.GetComponent<RectTransform>(),
                new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.78f));
            maskGO.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f); // nearly invisible but mask needs Image
            maskGO.GetComponent<Mask>().showMaskGraphic = false;

            // Scrolling content
            var contentGO = new GameObject("ScrollContent", typeof(RectTransform));
            contentGO.transform.SetParent(maskGO.transform, false);
            scrollContent = contentGO.GetComponent<RectTransform>();
            scrollContent.anchorMin = new Vector2(0f, 0f);
            scrollContent.anchorMax = new Vector2(1f, 0f);
            scrollContent.pivot = new Vector2(0.5f, 1f);
            // Content starts at the bottom of the mask area
            scrollContent.anchoredPosition = new Vector2(0, 0);

            string creditsText = GetCreditsText();
            var tmp = PlaceTMP(scrollContent, "CreditsText", creditsText, 24,
                new Color(0.88f, 0.86f, 0.80f), TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one);
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;

            // Let text dictate content height
            var fitter = contentGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            // TMP label should also fit
            var tmpFitter = tmp.gameObject.AddComponent<ContentSizeFitter>();
            tmpFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var tmpRT = tmp.GetComponent<RectTransform>();
            tmpRT.anchorMin = Vector2.zero;
            tmpRT.anchorMax = new Vector2(1f, 1f);
            tmpRT.offsetMin = tmpRT.offsetMax = Vector2.zero;

            // ─── Bottom decorative line ───
            MakeLine(root, new Vector2(0.2f, 0.16f), new Vector2(0.8f, 0.162f));

            // ─── Return button ───
            var btnGO = new GameObject("ReturnBtn", typeof(RectTransform),
                typeof(Image), typeof(Button));
            btnGO.transform.SetParent(root, false);
            SetAnchors(btnGO.GetComponent<RectTransform>(),
                new Vector2(0.25f, 0.05f), new Vector2(0.75f, 0.14f));
            btnGO.GetComponent<Image>().color = new Color(0.85f, 0.75f, 0.40f);

            PlaceTMP(btnGO.transform, "BtnLabel", "Αρχική Σελίδα", 30,
                new Color(0.05f, 0.05f, 0.10f), TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one);

            btnGO.GetComponent<Button>().onClick.AddListener(() =>
            {
                OnReturnToMenu?.Invoke();
            });

            scrolling = true;
        }

        void Update()
        {
            if (!scrolling || scrollContent == null) return;
            scrollContent.anchoredPosition += Vector2.up * scrollSpeed * Time.deltaTime;
        }

        private IEnumerator FadeIn()
        {
            if (canvasGroup == null) yield break;
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 1.5f;
                canvasGroup.alpha = Mathf.Clamp01(t);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }

        private string GetCreditsText()
        {
            return
                "<size=32><color=#F0E0A0>Σενάριο & Σχεδιασμός</color></size>\n" +
                "Γιώργος\n\n" +

                "<size=32><color=#F0E0A0>Ανάπτυξη</color></size>\n" +
                "Unity 6 / C# / Ink\n\n" +

                "<size=32><color=#F0E0A0>Αφηγηματική Μηχανή</color></size>\n" +
                "Ink by Inkle Studios\n\n" +

                "<size=32><color=#F0E0A0>Μουσική & Ήχος</color></size>\n" +
                "...\n\n" +

                "<size=32><color=#F0E0A0>Γραφικά & Εικονογράφηση</color></size>\n" +
                "...\n\n" +

                "<size=32><color=#F0E0A0>Ιστορική Σύμβουλος</color></size>\n" +
                "...\n\n" +

                "<size=32><color=#F0E0A0>Ερευνητικό Πλαίσιο</color></size>\n" +
                "Βασίζεται σε θεωρίες πολιτικής αγωγής\n" +
                "και ψηφιακής αφηγηματικής μάθησης\n\n" +

                "<size=32><color=#F0E0A0>Εργαλεία</color></size>\n" +
                "Unity  •  Ink  •  TextMeshPro\n" +
                "New Input System  •  Claude AI\n\n" +

                "<size=32><color=#F0E0A0>Ειδικές Ευχαριστίες</color></size>\n" +
                "Στους αρχαίους Αθηναίους πολίτες,\n" +
                "που τόλμησαν να φανταστούν\n" +
                "ότι ο λαός μπορεί να κυβερνήσει τον εαυτό του.\n\n\n" +

                "<size=28><color=#C0B888>« Ο μη μετέχων τούτων ου μέτοικον\n" +
                "αλλ' αχρείον νομίζομεν »</color></size>\n" +
                "<size=22>— Θουκυδίδης, Περικλέους Επιτάφιος</size>\n\n\n" +

                "<size=36><color=#F0E0A0>Ευχαριστούμε που έπαιξες.</color></size>\n\n\n";
        }

        // ─── UI helpers ───

        private static void MakeLine(RectTransform parent, Vector2 min, Vector2 max)
        {
            var go = new GameObject("Line", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            SetAnchors(go.GetComponent<RectTransform>(), min, max);
            go.GetComponent<Image>().color = new Color(0.85f, 0.75f, 0.40f, 0.4f);
        }

        private static TextMeshProUGUI PlaceTMP(Transform parent, string goName,
            string text, float size, Color color, TextAlignmentOptions align,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(goName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            SetAnchors(go.GetComponent<RectTransform>(), anchorMin, anchorMax);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void StretchFill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
