using System;
using System.Collections.Generic;
using System.Linq;
using Articy.Unity;
using Articy.Unity.Interfaces;

namespace DemocracyWay.NewUI
{
    public static class ArticyNarrative
    {
        public sealed class Result : IDisposable
        {
            readonly BaseGlobalVariables variables;
            internal Result(BaseGlobalVariables value) { variables = value; }
            public int Number(string name) => variables.GetVariableByString<int>("Story." + name);
            public bool Flag(string name) => variables.GetVariableByString<bool>("Story." + name);
            public string Text(string name) => variables.GetVariableByString<string>("Story." + name);
            public void Dispose() => ArticyRuleRunner.DestroyVariables(variables);
        }
        public static Result Run(string operation, GameState state, params (string name, object value)[] parameters)
        {
            var m = state.metrics;
            var inputs = new Dictionary<string,object>
            {
                ["Character.Consistency"]=m.consistency,["Character.Ethics"]=m.ethics,
                ["Character.Authority"]=m.authority,["Character.Persuasion"]=m.persuasion,
                ["Character.Parrhesia"]=m.parrhesia,["Character.Recognition"]=m.recognition,
                ["Story.CityFunds"]=state.cityFunds,["Story.Week"]=state.week,["Story.Prytany"]=state.prytany,
                ["Story.War"]=state.campaign.war,["Story.Readiness"]=state.campaign.readiness,
                ["Story.Legitimacy"]=state.campaign.legitimacy,["Story.Corruption"]=state.city?.corruptionRisk ?? 0,
                ["Story.InitialComplete"]=state.campaign.initialComplete,
                ["Story.PublicVotes"]=state.campaign.cases.Count(x=>x.vote==0)
            };
            foreach(var p in parameters) inputs["Story."+p.name]=p.value;
            return new Result(ArticyRuleRunner.Execute(operation,inputs));
        }
        public static string Route(string name,GameState state,string caseId="",int side=-1)
        {
            using var result=Run("Route_"+name,state,("CaseId",caseId),("Side",side));
            return result.Text("NextPhase");
        }
        public static int DefaultNumber(string name) => ArticyDatabase.DefaultGlobalVariables.GetVariableByString<int>("Story."+name);
        public static string Next(string technicalName)
        {
            var node=ArticyDatabase.GetObject(technicalName);
            if(node==null)throw new InvalidOperationException("Missing native flow node: "+technicalName);
            var branches=ArticyFlowPlayer.GetBranchesOfNode(node,PausableObjectTypes.FlowFragments,null,ArticyDatabase.DefaultGlobalVariables);
            var targets=branches.Where(b=>b.IsValid).Select(b=>b.Target as ArticyObject).Where(n=>n!=null&&n.TechnicalName!=technicalName).Distinct().ToArray();
            if(targets.Length!=1)throw new InvalidOperationException("Expected one authored continuation at "+technicalName+"; found "+targets.Length);
            return targets[0].TechnicalName;
        }
        public static string Copy(string name)
        {
            var obj=ArticyDatabase.GetObject(name);
            if(obj is IObjectWithLocalizableText local) return local.Text;
            if(obj is IObjectWithText plain) return plain.Text;
            throw new InvalidOperationException("Missing native narrative text: "+name);
        }
        public static string Title(string name)
        {
            var obj=ArticyDatabase.GetObject(name);
            if(obj is IObjectWithLocalizableDisplayName local) return local.DisplayName;
            if(obj is IObjectWithDisplayName plain) return plain.DisplayName;
            throw new InvalidOperationException("Missing native narrative title: "+name);
        }
    }
}
