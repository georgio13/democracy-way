using UnityEngine;

namespace DemocracyWay.Framework
{
    /// <summary>
    /// Persistent singleton (lives in Bootstrap). Installs the project's custom cursor
    /// (UI_Cursor) at startup and survives scene loads. The sprite is wired via the
    /// serialized field by DemocracyWayInit.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Cursor Manager")]
    [DisallowMultipleComponent]
    public class CursorManager : MonoBehaviour
    {
        public static CursorManager Instance { get; private set; }

        [Header("Cursor Texture")]
        [Tooltip("Custom cursor image. Wire to Art/UI/UI_Cursor.")]
        [SerializeField] private Texture2D cursorTexture;

        [Tooltip("Hotspot in pixels from top-left of the cursor texture.")]
        [SerializeField] private Vector2 hotspot = Vector2.zero;

        [SerializeField] private CursorMode cursorMode = CursorMode.Auto;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent != null) transform.SetParent(null, false);
            DontDestroyOnLoad(gameObject);

            Apply();
        }

        public void Apply()
        {
            if (cursorTexture == null) return;
            Cursor.SetCursor(cursorTexture, hotspot, cursorMode);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void Reset()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }
}
