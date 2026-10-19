using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    /// <summary>Resolution-independent gradients without a large background texture.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LoginGradientGraphic : MaskableGraphic
    {
        public Color startColor = new Color32(41, 75, 80, 255);
        public Color endColor = new Color32(16, 31, 36, 255);
        public bool radial = true;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); Rect rect = GetPixelAdjustedRect(); const int divisions = 20;
            for (int y = 0; y <= divisions; y++)
                for (int x = 0; x <= divisions; x++)
                {
                    var uv = new Vector2((float)x / divisions, (float)y / divisions);
                    float blend = radial ? Mathf.Clamp01(Vector2.Distance(uv, new Vector2(.35f, .7f)) / .82f) : 1 - uv.y;
                    mesh.AddVert(new Vector3(rect.x + uv.x * rect.width, rect.y + uv.y * rect.height), Color.Lerp(startColor, endColor, blend), Vector2.zero);
                }
            for (int y = 0; y < divisions; y++)
                for (int x = 0; x < divisions; x++)
                {
                    int i = y * (divisions + 1) + x;
                    mesh.AddTriangle(i, i + divisions + 1, i + 1);
                    mesh.AddTriangle(i + 1, i + divisions + 1, i + divisions + 2);
                }
        }
    }
}

