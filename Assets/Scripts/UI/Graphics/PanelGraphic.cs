using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    // Native uGUI geometry: no PNG texture for flat button/card colors.
    [AddComponentMenu("Democracy Way/UI/Cut Corner Shape")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PanelGraphic : MaskableGraphic
    {
        public enum Silhouette { CutCorners, TimeStela, SlantedTick, TribeShield }
        public Silhouette silhouette;
        [Min(0)]
        public float cut = 12;
        [Min(0)]
        public float border = 1;
        public Color borderColor = new Color32(180, 154, 98, 255);
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
            float c = Mathf.Clamp(cut, 0, Mathf.Min(r.width, r.height) * 0.5f);
            float b = Mathf.Clamp(border, 0, Mathf.Min(r.width, r.height) * 0.49f);
            Vector2[] outer = Points(r, c, silhouette);
            Rect innerRect = new Rect(r.x + b, r.y + b, Mathf.Max(0, r.width - 2 * b), Mathf.Max(0, r.height - 2 * b));
            Vector2[] inner = Points(innerRect, Mathf.Max(0, c - b * 0.5857864f), silhouette);
            for (int i = 0; i < outer.Length; i++)
                vh.AddVert(outer[i], b > 0 ? borderColor : color, Vector2.zero);
            for (int i = 0; i < outer.Length; i++)
                vh.AddVert(inner[i], color, Vector2.zero);
            vh.AddVert(innerRect.center, color, Vector2.zero);
            for (int i = 0; i < outer.Length; i++)
            {
                int n = (i + 1) % outer.Length;
                if (b > 0)
                {
                    vh.AddTriangle(i, n, outer.Length + i);
                    vh.AddTriangle(n, outer.Length + n, outer.Length + i);
                }

                vh.AddTriangle(outer.Length * 2, outer.Length + i, outer.Length + n);
            }
        }

        static Vector2[] Points(Rect r, float c, Silhouette shape)
        {
            c = Mathf.Min(c, Mathf.Min(r.width, r.height) * 0.5f);
            if (shape == Silhouette.TimeStela)
                return new[] { new Vector2(r.center.x, r.yMin), new Vector2(r.xMax, r.yMin + c), new Vector2(r.xMax, r.yMax - c), new Vector2(r.xMax - c, r.yMax), new Vector2(r.xMin + c, r.yMax), new Vector2(r.xMin, r.yMax - c), new Vector2(r.xMin, r.yMin + c) };
            if (shape == Silhouette.SlantedTick)
                return new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax - c, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin + c, r.yMax) };
            if (shape == Silhouette.TribeShield)
                return new[] { new Vector2(r.center.x, r.yMin), new Vector2(r.xMax, r.yMin + r.height * .28f), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin + r.height * .28f) };
            return new[]
            {
                new Vector2(r.xMin + c, r.yMin),
                new Vector2(r.xMax - c, r.yMin),
                new Vector2(r.xMax, r.yMin + c),
                new Vector2(r.xMax, r.yMax - c),
                new Vector2(r.xMax - c, r.yMax),
                new Vector2(r.xMin + c, r.yMax),
                new Vector2(r.xMin, r.yMax - c),
                new Vector2(r.xMin, r.yMin + c)
            };
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
