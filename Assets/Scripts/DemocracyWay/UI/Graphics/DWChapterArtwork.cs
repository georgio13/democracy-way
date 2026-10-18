using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    // Native geometry matching the approved CSS curtain and hanging cloth; no bitmap.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DWChapterArtwork : MaskableGraphic
    {
        public bool pennant;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (pennant)
            {
                for (int i = 0; i < 40; i++)
                {
                    float a = i / 40f, b = (i + 1) / 40f;
                    Color ca = Cloth(a), cb = Cloth(b);
                    float ya = r.yMin + r.height * .17f * Mathf.Abs(a - .5f) * 2;
                    float yb = r.yMin + r.height * .17f * Mathf.Abs(b - .5f) * 2;
                    Quad(vh, new Vector2(Mathf.Lerp(r.xMin, r.xMax, a), ya), new Vector2(Mathf.Lerp(r.xMin, r.xMax, a), r.yMax), new Vector2(Mathf.Lerp(r.xMin, r.xMax, b), r.yMax), new Vector2(Mathf.Lerp(r.xMin, r.xMax, b), yb), ca, ca, cb, cb);
                }

                Color edge = new Color32(192, 163, 107, 255);
                float scale = r.width / 208f;
                foreach (float x in new[]
                {
                    0f,
                    3f,
                    204f,
                    207f
                }

                )
                    Quad(vh, new Vector2(r.xMin + x * scale, r.yMin + r.height * .17f), new Vector2(r.xMin + x * scale, r.yMax), new Vector2(r.xMin + (x + 1) * scale, r.yMax), new Vector2(r.xMin + (x + 1) * scale, r.yMin + r.height * .17f), edge, edge, edge, edge);
            }
            else
            {
                const int nx = 48, ny = 30;
                for (int y = 0; y < ny; y++)
                    for (int x = 0; x < nx; x++)
                    {
                        float a = x / (float)nx, b = (x + 1) / (float)nx, c = y / (float)ny, d = (y + 1) / (float)ny;
                        Quad(vh, Point(r, a, c), Point(r, a, d), Point(r, b, d), Point(r, b, c), Curtain(a, c), Curtain(a, d), Curtain(b, d), Curtain(b, c));
                    }
            }
        }

        static Vector2 Point(Rect r, float x, float y) => new Vector2(Mathf.Lerp(r.xMin, r.xMax, x), Mathf.Lerp(r.yMin, r.yMax, y));
        static Color Cloth(float x) => x < .48f ? Color.Lerp(new Color32(69, 84, 66, 255), new Color32(91, 100, 72, 255), x / .48f) : Color.Lerp(new Color32(91, 100, 72, 255), new Color32(59, 75, 62, 255), (x - .48f) / .52f);
        static Color Curtain(float x, float y)
        {
            float radius = Mathf.Sqrt(Mathf.Pow((x - .5f) / .8f, 2) + Mathf.Pow((y - .65f) / .78f, 2));
            return Color.Lerp(new Color32(32, 51, 54, 255), new Color32(10, 23, 29, 255), Mathf.Clamp01(radius / .8f));
        }

        static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
        {
            int n = vh.currentVertCount;
            vh.AddVert(a, ca, Vector2.zero);
            vh.AddVert(b, cb, Vector2.zero);
            vh.AddVert(c, cc, Vector2.zero);
            vh.AddVert(d, cd, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2);
            vh.AddTriangle(n, n + 2, n + 3);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }
}
