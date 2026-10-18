using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    [Serializable]
    public sealed class InstitutionCase
    {
        public string id, title, question, explanation, source, brief, argumentA, argumentB;
        public string[] answers;
        public int site, correct, costA, costB, war;
        public bool observation;
    }
}
