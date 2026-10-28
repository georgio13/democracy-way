using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Κλικ σε υπογραμμισμένη λέξη κειμένου (link με id λήμματος) → άνοιγμα του ευρετηρίου
    /// στο αντίστοιχο λήμμα. Αδελφάκι του LexiconLinkHandler, για κείμενα εκτός του panel.
    /// </summary>
    public sealed class LexiconTermClicks : MonoBehaviour, IPointerClickHandler
    {
        public LexiconPanel owner;

        public void OnPointerClick(PointerEventData e)
        {
            TMP_Text t = GetComponent<TMP_Text>();
            if (t == null || owner == null)
                return;
            int i = TMP_TextUtilities.FindIntersectingLink(t, e.position, e.pressEventCamera);
            if (i >= 0)
                owner.OpenEntry(t.textInfo.linkInfo[i].GetLinkID());
        }
    }
}
