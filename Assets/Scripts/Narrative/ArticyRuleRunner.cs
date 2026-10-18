using System;
using System.Collections.Generic;
using Articy.Unity;
using Articy.Unity.Interfaces;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    // Executes native graphs through the vendor FlowPlayer. No expression parser lives in game code.
    public sealed class ArticyRuleRunner : MonoBehaviour, IArticyFlowPlayerCallbacks
    {
        bool completed;
        string operation;
        public void OnFlowPlayerPaused(IFlowObject node)
        {
            if (node is IFlowFragment && node is ArticyObject obj && obj.TechnicalName.StartsWith(operation + "_", StringComparison.Ordinal))
                completed = true;
        }
        public void OnBranchesUpdated(IList<Branch> branches) { }

        public static BaseGlobalVariables Execute(string operation, IDictionary<string, object> inputs)
        {
            if (!ArticyDatabase.IsDatabaseAvailable()) throw new InvalidOperationException("Native narrative database is unavailable.");
            var entry = ArticyDatabase.GetObject(operation + "_1");
            if (entry == null) throw new InvalidOperationException("Missing native operation: " + operation);
            var variables = BaseGlobalVariables.CreateGlobalVariablesClone<Articy.Odjos_DjimokraTias.GlobalVariables.ArticyGlobalVariables>();
            foreach (var input in inputs) variables.SetVariableByString(input.Key, input.Value);
            var host = new GameObject("Narrative operation");
            try
            {
                var runner = host.AddComponent<ArticyRuleRunner>(); runner.operation = operation;
                var player = host.AddComponent<ArticyFlowPlayer>();
                player.pauseOn = PausableObjectTypes.FlowFragments | PausableObjectTypes.Instructions | PausableObjectTypes.Conditions | PausableObjectTypes.Hubs;
                player.ignoreInvalidBranches = true;
                player.GlobalVariables = variables;
                player.StartOn = entry;
                for(int step=0;!runner.completed && step<1024;step++)
                {
                    player.Play();
                }
                if (!runner.completed) throw new InvalidOperationException("Native operation did not reach its result: " + operation);
                return variables;
            }
            catch { DestroyVariables(variables); throw; }
            finally { host.SetActive(false); if (Application.isPlaying) Destroy(host); else DestroyImmediate(host); }
        }
        public static void DestroyVariables(BaseGlobalVariables variables)
        {
            if (Application.isPlaying) Destroy(variables); else DestroyImmediate(variables);
        }
    }
}
