using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    [Serializable]
    public sealed class CharacterMetrics
    {
        public int consistency;
        public int ethics;
        public int authority;
        public int persuasion;
        public int parrhesia;
        public int recognition;
        public CharacterMetrics Copy()
        {
            return new CharacterMetrics
            {
                consistency = consistency,
                ethics = ethics,
                authority = authority,
                persuasion = persuasion,
                parrhesia = parrhesia,
                recognition = recognition
            };
        }

        public void Add(CharacterMetrics other)
        {
            if (other == null)
                return;
            consistency += other.consistency;
            ethics += other.ethics;
            authority += other.authority;
            persuasion += other.persuasion;
            parrhesia += other.parrhesia;
            recognition += other.recognition;
        }

        public void Clamp()
        {
            consistency = Mathf.Clamp(consistency, 0, 100);
            ethics = Mathf.Clamp(ethics, 0, 100);
            authority = Mathf.Clamp(authority, 0, 100);
            persuasion = Mathf.Clamp(persuasion, 0, 100);
            parrhesia = Mathf.Clamp(parrhesia, 0, 100);
            recognition = Mathf.Clamp(recognition, 0, 100);
        }

        public string ToGreekLines()
        {
            return "Συνέπεια  " + consistency + "\nΗθική  " + ethics + "\nΑυθεντία  " + authority + "\nΠειθώ  " + persuasion + "\nΠαρρησία  " + parrhesia + "\nΑναγνωρισιμότητα  " + recognition;
        }
    }
}
