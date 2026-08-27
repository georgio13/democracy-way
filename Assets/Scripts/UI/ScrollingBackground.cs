using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.UI
{
    /// <summary>
    /// Endless horizontal drift for a smoke/atmosphere layer: instead of
    /// moving a transform (which would need two tiled copies and a wrap jump),
    /// it slides the RawImage's uvRect — the GPU tiles the texture for free.
    /// REQUIRES the texture's Wrap Mode to be Repeat in its importer,
    /// otherwise the edge pixels smear. Unscaled time so the drift continues
    /// behind the pause menu instead of freezing conspicuously.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Scrolling Background")]
    [DisallowMultipleComponent]
    public class ScrollingBackground : MonoBehaviour
    {
        [Tooltip("Το RawImage με την texture (Wrap Mode: Repeat!). Κενό = το RawImage αυτού του GameObject.")]
        [SerializeField] private RawImage target;

        [Tooltip("Ταχύτητα οριζόντιας κύλισης σε UV μονάδες/δευτερόλεπτο (1 = ένα πλήρες πλάτος texture).")]
        [SerializeField] private float speed = 0.01f;

        void Awake()
        {
            if (target == null) target = GetComponent<RawImage>();
            if (target == null)
                Debug.LogError("[ScrollingBackground] Δεν βρέθηκε RawImage.", this);
        }

        void Update()
        {
            if (target == null) return;
            var uv = target.uvRect;
            // Mathf.Repeat keeps x in [0,1) forever — a raw += would lose
            // float precision after hours of menu idling.
            uv.x = Mathf.Repeat(uv.x + speed * Time.unscaledDeltaTime, 1f);
            target.uvRect = uv;
        }
    }
}
