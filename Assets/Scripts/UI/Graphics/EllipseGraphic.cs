using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class EllipseGraphic : MaskableGraphic
    {
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
            const int segments = 64;
            mesh.AddVert(r.center, color, new Vector2(.5f, .5f));
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector2 offset = new Vector2(Mathf.Cos(angle) * r.width * .5f, Mathf.Sin(angle) * r.height * .5f);
                mesh.AddVert(r.center + offset, color, Vector2.zero);
            }

            for (int i = 0; i < segments; i++)
                mesh.AddTriangle(0, i + 1, (i + 1) % segments + 1);
        }
    }
}
