using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    [AddComponentMenu("UI/Democracy Way/Checkmark")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DWCheckmark : MaskableGraphic
    {
        public float thickness = 3f;
        // Repair an already-open scene created before the CanvasRenderer requirement.
        protected override void OnEnable()
        {
            if (GetComponent<CanvasRenderer>() == null)
                gameObject.AddComponent<CanvasRenderer>();
            base.OnEnable();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = GetPixelAdjustedRect();
            Vector2 a = r.center + new Vector2(-.30f * r.width, .01f * r.height);
            Vector2 b = r.center + new Vector2(-.07f * r.width, -.23f * r.height);
            Vector2 c = r.center + new Vector2(.34f * r.width, .28f * r.height);
            Stroke(mesh, a, b);
            Stroke(mesh, b, c);
        }

        private void Stroke(VertexHelper mesh, Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;
            Vector2 offset = new Vector2(-delta.y, delta.x).normalized * Mathf.Max(1f, thickness) * .5f;
            int first = mesh.currentVertCount;
            mesh.AddVert(start - offset, color, Vector2.zero);
            mesh.AddVert(start + offset, color, Vector2.zero);
            mesh.AddVert(end + offset, color, Vector2.zero);
            mesh.AddVert(end - offset, color, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
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
