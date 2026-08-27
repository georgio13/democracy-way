using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.Dialogue
{
    /// <summary>
    /// Pool of dialogues the "Τυχαίος διάλογος" button draws from. Seeded by
    /// <c>Tools ▸ DemocracyWay ▸ Init</c> with a starter set; add more by
    /// editing this asset in the Inspector — no code change needed.
    /// </summary>
    [CreateAssetMenu(
        fileName = "DialogueDatabase",
        menuName = "DemocracyWay/Dialogue Database")]
    public class DialogueDatabase : ScriptableObject
    {
        [SerializeField] private List<DialogueEntry> entries = new List<DialogueEntry>();

        public IReadOnlyList<DialogueEntry> Entries => entries;
        public int Count => entries != null ? entries.Count : 0;

        /// <summary>
        /// Picks a random dialogue, preferring ones whose id is not in
        /// <paramref name="seenIds"/>. Once every dialogue has been seen the
        /// whole pool becomes eligible again, so the button never dead-ends.
        /// </summary>
        public DialogueEntry PickRandom(ICollection<string> seenIds)
        {
            if (entries == null || entries.Count == 0) return null;

            var unseen = new List<DialogueEntry>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || !e.IsValid) continue;
                if (seenIds == null || !seenIds.Contains(e.id)) unseen.Add(e);
            }

            var pool = unseen;
            if (pool.Count == 0)
            {
                // Every dialogue seen — fall back to the full valid set.
                pool = new List<DialogueEntry>(entries.Count);
                for (int i = 0; i < entries.Count; i++)
                    if (entries[i] != null && entries[i].IsValid) pool.Add(entries[i]);
            }

            if (pool.Count == 0) return null;
            return pool[Random.Range(0, pool.Count)];
        }

        public DialogueEntry Find(string id)
        {
            if (string.IsNullOrEmpty(id) || entries == null) return null;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].id == id) return entries[i];
            return null;
        }

#if UNITY_EDITOR
        /// <summary>Editor-only seeding hook used by Init.</summary>
        public void EditorSetEntries(List<DialogueEntry> newEntries) => entries = newEntries;
#endif
    }
}
