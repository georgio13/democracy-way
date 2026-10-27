using System;
namespace DemocracyWay.NewUI {
    [Serializable] public sealed class DemographicsContent { public string version,sourceUrl,adaptationNote,instructions; public DemographicQuestion[] questions; }
    [Serializable] public sealed class DemographicQuestion { public string id,prompt,kind; public bool required; public int maxLength=100; public DemographicOption[] options; }
    [Serializable] public sealed class DemographicOption { public string id,label; }
    [Serializable] public sealed class DemographicAnswer { public string questionId,optionId,value; public int optionIndex=-1; }
    [Serializable] public sealed class DemographicsState { public string version; public bool completed; public DemographicAnswer[] answers=Array.Empty<DemographicAnswer>(); public EvaluationPoint point,completedPoint; }
}
