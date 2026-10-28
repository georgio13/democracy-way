using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    // Small uGUI construction helpers shared by the civic panels; authored assets stay in the scenes.
    internal static class CivicUIFactory
    {
        internal static readonly Color Ink = new Color32(35, 59, 61, 255);
        internal static readonly Color Paper = new Color32(228, 218, 194, 255);
        internal static readonly Color Gold = new Color32(178, 151, 92, 255);
        internal static readonly Color Teal = new Color32(34, 61, 66, 255);
        const string ApprovedPaperSpriteName = "FrameParchment";
        static Sprite approvedPaperSprite;
        static Scene searchedPaperScene;
        static bool searchedForPaper;

        static Sprite ApprovedPaperSprite()
        {
            if (approvedPaperSprite != null) return approvedPaperSprite;
            var scene = SceneManager.GetActiveScene();
            if (searchedForPaper && searchedPaperScene == scene) return null;
            searchedPaperScene = scene; searchedForPaper = true;
            foreach (var existing in Resources.FindObjectsOfTypeAll<Image>())
                if (existing != null && existing.gameObject.scene.IsValid() && existing.sprite != null && existing.sprite.name == ApprovedPaperSpriteName)
                {
                    approvedPaperSprite = existing.sprite;
                    break;
                }
            return approvedPaperSprite;
        }

        internal static RectTransform Rect(Transform parent, string name)
        {
            var value = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            value.SetParent(parent, false); return value;
        }
        internal static void Box(RectTransform r, float x, float y, float width, float height)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(width, height);
        }
        internal static void Fill(RectTransform r, Vector2 lower, Vector2 upper)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = lower; r.offsetMax = -upper;
        }
        internal static PanelGraphic Panel(Transform parent, string name, Color color)
        {
            var r = Rect(parent, name); r.gameObject.AddComponent<CanvasRenderer>();
            var shape = r.gameObject.AddComponent<PanelGraphic>(); shape.color = color; shape.border = 1.5f; shape.cut = 12;
            shape.borderColor = Gold;
            if (color.r > .72f && color.g > .68f && color.b > .55f)
            {
                var skin = ApprovedPaperSprite();
                if (skin != null)
                {
                    shape.border = 0;
                    var artwork = Rect(r, "Approved paper artwork"); Fill(artwork, Vector2.zero, Vector2.zero);
                    artwork.gameObject.AddComponent<CanvasRenderer>();
                    var image = artwork.gameObject.AddComponent<Image>(); image.sprite = skin;
                    image.type = skin.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple;
                    image.pixelsPerUnitMultiplier = 4f;
                    image.preserveAspect = false; image.raycastTarget = false; image.color = Color.white;
                }
            }
            return shape;
        }
        internal static TMP_Text Text(Transform parent, string name, string value, TMP_FontAsset font, float size, Color color)
        {
            var r = Rect(parent, name); r.gameObject.AddComponent<CanvasRenderer>();
            var t = r.gameObject.AddComponent<TextMeshProUGUI>(); t.font = font; t.text = value ?? "";
            t.fontSize = size; t.color = color; t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow; t.richText = true; return t;
        }
        internal static Button Button(Transform parent, string name, string label, TMP_FontAsset font, Action action, GameFlowController app)
        {
            var shape = Panel(parent, name, Gold); var b = shape.gameObject.AddComponent<Button>(); b.targetGraphic = shape;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = b.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1, 1, 1, 1);
            colors.pressedColor = new Color(.8f, .85f, .85f, 1); colors.selectedColor = Color.white; colors.disabledColor = new Color(1, 1, 1, .4f); b.colors = colors;
            var text = Text(shape.transform, "Label", label, font, 26, Ink); Fill(text.rectTransform, new Vector2(16, 8), new Vector2(16, 8));
            text.alignment = TextAlignmentOptions.Center; text.enableAutoSizing = true; text.fontSizeMin = 18; text.fontSizeMax = 26;
            if (action != null) b.onClick.AddListener(() => action());
            var audio = b.gameObject.AddComponent<ButtonAudio>(); audio.app = app; return b;
        }
        internal static ScrollRect Scroll(Transform parent, string name, out RectTransform content, bool framed = true)
        {
            var root = framed ? Panel(parent, name, Paper).rectTransform : Rect(parent, name);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            var view = Rect(root, "Viewport"); Fill(view, new Vector2(12, 12), new Vector2(28, 12)); view.gameObject.AddComponent<RectMask2D>();
            content = Rect(view, "Content"); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            scroll.viewport = view; scroll.content = content; scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 36; scroll.movementType = ScrollRect.MovementType.Clamped;
            var track = Rect(root, "Scroll rail"); track.gameObject.AddComponent<CanvasRenderer>(); var rail = track.gameObject.AddComponent<Image>(); rail.color = new Color32(125, 116, 94, 70);
            track.anchorMin = new Vector2(1, 0); track.anchorMax = Vector2.one; track.pivot = new Vector2(1, .5f); track.offsetMin = new Vector2(-16, 16); track.offsetMax = new Vector2(-8, -16);
            var handle = Rect(track, "Bronze grip"); Fill(handle, Vector2.zero, Vector2.zero); handle.gameObject.AddComponent<CanvasRenderer>(); var image = handle.gameObject.AddComponent<Image>(); image.color = Gold;
            var bar = track.gameObject.AddComponent<Scrollbar>(); bar.direction = Scrollbar.Direction.BottomToTop; bar.handleRect = handle; bar.targetGraphic = image;
            bar.navigation = new Navigation { mode = Navigation.Mode.None }; scroll.verticalScrollbar = bar; return scroll;
        }

        /// <summary>Ενιαίο βάψιμο «επιλεγμένου» κουμπιού (Teal/Paper), μία υλοποίηση για όλα τα ερωτηματολόγια.</summary>
        internal static void PaintSelectable(Button button, bool selected)
        {
            if (button == null) return;
            var shape = button.GetComponent<PanelGraphic>();
            if (shape != null) shape.color = selected ? Teal : Paper;
            else
            {
                var image = button.GetComponent<Image>();
                if (image != null) image.color = selected ? Teal : Paper;
            }
            var label = button.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (label != null) label.color = selected ? Paper : Ink;
        }
    }
}
