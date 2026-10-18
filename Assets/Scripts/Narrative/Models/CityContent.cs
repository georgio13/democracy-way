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
    public class CityContent
    {
        public CityNews[] news;
        public LogographerDefinition[] writers;
        public CityComplaint[] complaints;
        public CityOfficial[] officials;
    }
}
