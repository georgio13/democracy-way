using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.Editor
{
    [Serializable]
    public sealed class DWArticyFieldBinding
    {
        public string technicalName;
        [Tooltip("Articy property or template-feature property path, e.g. Text or Metrics.Consistency.")]
        public string sourceProperty = "Text";
        public string targetProperty;
    }

    [CreateAssetMenu(menuName = "Democracy Way/Articy/Import Map", fileName = "ArticyImportMap")]
    public sealed class DWArticyImportMap : ScriptableObject
    {
        public DemocracyWay.NewUI.DWConfig target;
        public List<DWArticyFieldBinding> fields = new List<DWArticyFieldBinding>();
    }
}
