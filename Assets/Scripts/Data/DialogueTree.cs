using System;
using System.Collections.Generic;
using UnityEngine;
using DemocracyWay.Core;

namespace DemocracyWay.Data
{
    /// <summary>A change one dialogue choice makes to one indicator.</summary>
    [Serializable]
    public class IndicatorEffect
    {
        public IndicatorId indicator;

        [Tooltip("Θετικό = ανεβαίνει, αρνητικό = πέφτει. Οι τιμές κόβονται στο 0–100.")]
        public int delta;
    }

    /// <summary>One selectable answer at a branching node.</summary>
    [Serializable]
    public class DialogueChoice
    {
        [Tooltip("Σταθερό αναγνωριστικό της επιλογής — έτσι εμφανίζεται στο analytics log.")]
        public string id = "";

        [Tooltip("Το κείμενο του κουμπιού που βλέπει ο παίκτης.")]
        [TextArea(1, 3)]
        public string text = "";

        [Tooltip("Το id του κόμβου όπου συνεχίζει ο διάλογος. Κενό = ο διάλογος τελειώνει εδώ.")]
        public string nextNodeId = "";

        [Tooltip("Μεταβολές δεικτών όταν επιλεγεί.")]
        public List<IndicatorEffect> effects = new List<IndicatorEffect>();

        [Tooltip("Story flags που ενεργοποιούνται όταν επιλεγεί (π.χ. 'met_socrates').")]
        public List<string> setFlags = new List<string>();

        [Tooltip("Αν ενεργό, με αυτή την επιλογή περνά μία βδομάδα πρυτανείας (γίνεται και autosave).")]
        public bool advanceWeek;
    }

    /// <summary>One line of dialogue. Either it flows to nextNodeId, or it
    /// branches via choices, or (both empty) it ends the tree.</summary>
    [Serializable]
    public class DialogueNode
    {
        [Tooltip("Σταθερό αναγνωριστικό του κόμβου, μοναδικό μέσα στο δέντρο.")]
        public string id = "";

        [Tooltip("Το όνομα που εμφανίζεται δίπλα στο πορτρέτο.")]
        public string speakerName = "";

        [Tooltip("Το πορτρέτο στην πάνω-αριστερή γωνία του panel διαλόγου.")]
        public Sprite portrait;

        [Tooltip("Τα λόγια του ομιλητή.")]
        [TextArea(2, 6)]
        public string text = "";

        [Tooltip("Προαιρετικό ηχητικό ομιλίας για αυτή τη γραμμή.")]
        public AudioClip voiceClip;

        [Tooltip("Το id του επόμενου κόμβου όταν ΔΕΝ υπάρχουν επιλογές. Κενό + καθόλου επιλογές = τέλος διαλόγου.")]
        public string nextNodeId = "";

        [Tooltip("Αν έχει στοιχεία, ο κόμβος διακλαδώνεται: ο παίκτης διαλέγει.")]
        public List<DialogueChoice> choices = new List<DialogueChoice>();

        public bool HasChoices => choices != null && choices.Count > 0;
        public bool IsEnd => !HasChoices && string.IsNullOrEmpty(nextNodeId);
    }

    /// <summary>
    /// The branching dialogue of one scene, authored entirely in the
    /// Inspector. The runner starts at <see cref="startNodeId"/> (or the first
    /// node) and follows nextNodeId / choice targets until an end node.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueTree", menuName = "DemocracyWay/Dialogue Tree")]
    public class DialogueTree : ScriptableObject
    {
        [Tooltip("Το id του πρώτου κόμβου. Κενό = ο πρώτος της λίστας.")]
        public string startNodeId = "";

        public List<DialogueNode> nodes = new List<DialogueNode>();

        public DialogueNode GetNode(string id) =>
            string.IsNullOrEmpty(id) ? null : nodes.Find(n => n.id == id);

        public DialogueNode StartNode =>
            string.IsNullOrEmpty(startNodeId)
                ? (nodes.Count > 0 ? nodes[0] : null)
                : GetNode(startNodeId);

        /// <summary>Editor-time sanity check: every referenced node id must exist.</summary>
        private void OnValidate()
        {
            foreach (var node in nodes)
            {
                if (!string.IsNullOrEmpty(node.nextNodeId) && GetNode(node.nextNodeId) == null)
                    Debug.LogWarning($"[{name}] Ο κόμβος '{node.id}' δείχνει σε ανύπαρκτο nextNodeId '{node.nextNodeId}'.", this);
                foreach (var choice in node.choices)
                    if (!string.IsNullOrEmpty(choice.nextNodeId) && GetNode(choice.nextNodeId) == null)
                        Debug.LogWarning($"[{name}] Η επιλογή '{choice.id}' του κόμβου '{node.id}' δείχνει σε ανύπαρκτο κόμβο '{choice.nextNodeId}'.", this);
            }
        }
    }
}
