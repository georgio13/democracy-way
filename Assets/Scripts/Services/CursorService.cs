using UnityEngine;

namespace DemocracyWay.Services
{
    /// <summary>
    /// Installs the custom mouse cursor from <see cref="Data.GameConfig"/> at
    /// startup. To change the cursor: swap the texture on the GameConfig asset
    /// (import it with Texture Type: Cursor) — no code involved.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Cursor Service")]
    [DisallowMultipleComponent]
    public class CursorService : MonoBehaviour
    {
        void Start() => Apply();

        public void Apply()
        {
            var config = ServicesRoot.Config;
            if (config == null || config.cursorTexture == null) return;

            UnityEngine.Cursor.SetCursor(config.cursorTexture, config.cursorHotspot, CursorMode.Auto);
            UnityEngine.Cursor.visible = true;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
        }
    }
}
