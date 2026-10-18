using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    [Serializable]
    public class CityComplaint
    {
        public string title, summary, intro, evidenceA, evidenceB, detail, note;
        public int amountA, amountB;
    }
}
