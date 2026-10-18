using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    // Optional transport boundary: gameplay and local saving never depend on network availability.
    public static class DWTelemetry
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void DW_RecordCheckpoint(string json);
        [Serializable] sealed class Packet
        {
            public string runId,phase,caseContent;
            public int prytany,week,biographyIndex,prologueIndex,cityFunds,personalFunds;
            public DWMetrics metrics;public List<DWChoiceRecord> choices;public DWCampaignState campaign;
        }
#endif
        public static void Capture(DWGameState state, TextAsset caseContent)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if(state==null)return;
            try
            {
                // No player name, contact information, IP address, auth secret or device fingerprint is included.
                var data=new Packet{runId=state.runId,phase=state.phase,prytany=state.prytany,week=state.week,biographyIndex=state.biographyIndex,prologueIndex=state.prologueIndex,cityFunds=state.cityFunds,personalFunds=state.personalFunds,metrics=state.metrics,choices=state.choices,campaign=state.campaign,caseContent=caseContent==null?null:caseContent.text};
                DW_RecordCheckpoint(JsonUtility.ToJson(data));
            }
            catch(Exception e){Debug.LogWarning("Research event queued unsuccessfully: "+e.GetType().Name);}
#endif
        }
    }
}
