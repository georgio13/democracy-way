using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    // Optional transport boundary: gameplay and local saving never depend on network availability.
    public static class ResearchTelemetry
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void DW_RecordCheckpoint(string json);
        [Serializable] sealed class Packet
        {
            public string runId,phase,caseContent,evaluationContent,currentCaseId;
            public int casesPerPrytany,totalCases;
            public int contentVersion;
            public float activeSeconds;
            public int prytany,week,biographyIndex,prologueIndex,cityFunds,personalFunds;
            public CharacterMetrics metrics;public ResearchChoice[] choices;public CampaignState campaign;
            public EvaluationState evaluation;
        }
        [Serializable] sealed class ResearchChoice
        {
            public string stepId,choiceId,institutionalLean;
            public CharacterMetrics metrics;
            public int personalFunds,cityFunds;
            public CityMetricDelta cityMetrics;
        }
#endif
        public static void Capture(GameState state)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if(state==null||!AccountBridge.ResearchConsent)return;
            try
            {
                // The transport omits account identity and persona names. Firebase necessarily receives network metadata.
                if(state.evaluation!=null)state.evaluation.researchParticipantId=AccountBridge.ResearchParticipantId;
                var instrument=JsonUtility.ToJson(JsonContent.Evaluation);
                var choices=(state.choices??new List<ChoiceRecord>()).Select(c=>new ResearchChoice{stepId=c.stepId,choiceId=c.choiceId,institutionalLean=c.institutionalLean,metrics=c.metrics,personalFunds=c.personalFunds,cityFunds=c.cityFunds,cityMetrics=c.cityMetrics}).ToArray();
                var data=new Packet{casesPerPrytany=CampaignRules.CasesPerPrytany,totalCases=CampaignRules.TotalCases,currentCaseId=JsonContent.Plan.prytanies[state.prytany-1].caseIds[state.week-1],contentVersion=state.contentVersion,activeSeconds=state.activeSeconds,runId=state.runId,phase=state.phase,prytany=state.prytany,week=state.week,biographyIndex=state.biographyIndex,prologueIndex=state.prologueIndex,cityFunds=state.cityFunds,personalFunds=state.personalFunds,metrics=state.metrics,choices=choices,campaign=state.campaign,caseContent=JsonUtility.ToJson(JsonContent.Campaign),evaluation=state.evaluation,evaluationContent=instrument};
                DW_RecordCheckpoint(JsonUtility.ToJson(data));
            }
            catch(Exception e){Debug.LogWarning("Research event queued unsuccessfully: "+e.GetType().Name);}
#endif
        }
    }
}
