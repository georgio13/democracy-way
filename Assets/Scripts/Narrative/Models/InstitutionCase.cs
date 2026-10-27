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
        public string id, title, source, brief, choiceA, choiceB, presentationMode;
        [NonSerialized] public string argumentA, argumentB, speakerA, speakerB;
        public int site, costA, costB, war;
        public bool observation;
        public string institution, leanA, leanB, leanReasonA, leanReasonB;
        public int attendanceObols;
        public CharacterMetrics metricsA = new CharacterMetrics(), metricsB = new CharacterMetrics();
        public string contentVersion;
        public CaseArgument[] arguments;
        public PoliticalCaseDefinition politics;
    }
}
