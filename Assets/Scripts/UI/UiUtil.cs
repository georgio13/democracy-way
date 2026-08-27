using UnityEngine;

namespace DemocracyWay.UI
{
    /// <summary>Small shared helpers for the dynamically-built UI panels.</summary>
    public static class UiUtil
    {
        /// <summary>
        /// Removes every child of <paramref name="container"/> immediately.
        ///
        /// <c>Object.Destroy</c> only queues the destruction until the end of
        /// the frame, so a naive clear-then-rebuild leaves the old children
        /// parented for the rest of the frame — the layout group counts them,
        /// and the list visibly doubles. Unparenting first takes them out of
        /// the layout right now; the actual destroy can happen whenever.
        /// </summary>
        public static void ClearChildren(Transform container)
        {
            if (container == null) return;
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                child.SetParent(null, false);
                Object.Destroy(child.gameObject);
            }
        }
    }
}
