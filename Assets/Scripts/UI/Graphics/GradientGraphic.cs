using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    // Vertex colors reproduce CSS vignettes without a large gradient PNG.
    [AddComponentMenu("Democracy Way/UI/Gradient")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GradientGraphic : MaskableGraphic
    {
        public bool horizontal;
        public Gradient ramp = new Gradient();
        // Repair an already-open scene created before the CanvasRenderer requirement.
        protected override void OnEnable()
        {
            if (GetComponent<CanvasRenderer>() == null)
                gameObject.AddComponent<CanvasRenderer>();
            base.OnEnable();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            const int segments = 48;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments, b = (i + 1) / (float)segments;
                Color ca = ramp.Evaluate(a) * color, cb = ramp.Evaluate(b) * color;
                int n = vh.currentVertCount;
                if (horizontal)
                {
                    float x0 = Mathf.Lerp(r.xMin, r.xMax, a), x1 = Mathf.Lerp(r.xMin, r.xMax, b);
                    vh.AddVert(new Vector2(x0, r.yMin), ca, Vector2.zero);
                    vh.AddVert(new Vector2(x0, r.yMax), ca, Vector2.zero);
                    vh.AddVert(new Vector2(x1, r.yMax), cb, Vector2.zero);
                    vh.AddVert(new Vector2(x1, r.yMin), cb, Vector2.zero);
                }
                else
                {
                    float y0 = Mathf.Lerp(r.yMin, r.yMax, a), y1 = Mathf.Lerp(r.yMin, r.yMax, b);
                    vh.AddVert(new Vector2(r.xMin, y0), ca, Vector2.zero);
                    vh.AddVert(new Vector2(r.xMin, y1), cb, Vector2.zero);
                    vh.AddVert(new Vector2(r.xMax, y1), cb, Vector2.zero);
                    vh.AddVert(new Vector2(r.xMax, y0), ca, Vector2.zero);
                }

                vh.AddTriangle(n, n + 1, n + 2);
                vh.AddTriangle(n + 2, n + 3, n);
            }
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
