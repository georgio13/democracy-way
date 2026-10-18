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
    public class CityOfficial
    {
        public string name, office, tribe, deme, trittys, method, priority, influence;
        public int portrait, group, persuasionThreshold, integrity;
    }
}
