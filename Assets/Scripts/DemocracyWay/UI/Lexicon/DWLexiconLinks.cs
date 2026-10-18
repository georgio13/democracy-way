using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DemocracyWay.NewUI
{
    public sealed class DWLexiconLinks : MonoBehaviour, IPointerClickHandler
    {
        public DWMapLexicon owner;
        public void OnPointerClick(PointerEventData e)
        {
            TMP_Text t = GetComponent<TMP_Text>();
            int i = TMP_TextUtilities.FindIntersectingLink(t, e.position, e.pressEventCamera);
            if (i >= 0)
                owner.SelectLink(t.textInfo.linkInfo[i].GetLinkID());
        }
    }
}
