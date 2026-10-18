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
    public class DWCityContent
    {
        public DWCityNews[] news;
        public DWCityWriter[] writers;
        public DWCityComplaint[] complaints;
        public DWCityOfficial[] officials;
    }
}
