using System;
using System.Linq;
namespace DemocracyWay.NewUI {
    public static class DemographicsRules {
        public static DemographicsState Ensure(GameState state,DemographicsContent content) {
            var e=state.evaluation ?? (state.evaluation=new EvaluationState());
            var d=e.demographics ?? (e.demographics=new DemographicsState());
            d.answers=d.answers ?? Array.Empty<DemographicAnswer>();
            // Μετά από save/load το JsonUtility δίνει ΚΕΝΟ point αντί για null: χωρίς αυτό το
            // «d.point ?? point» δεν το συμπλήρωνε ποτέ και το Complete αποτύγχανε σιωπηλά.
            if(d.point!=null&&string.IsNullOrEmpty(d.point.utc))d.point=null;
            if(d.completedPoint!=null&&string.IsNullOrEmpty(d.completedPoint.utc))d.completedPoint=null;
            return d;
        }
        public static bool Answer(GameState state,DemographicsContent content,string id,string optionId,string text,EvaluationPoint point) {
            var d=Ensure(state,content);var q=content.questions.FirstOrDefault(x=>x.id==id);
            if(q==null||d.completed||!EvaluationRules.HasPoint(point))return false;
            var answer=new DemographicAnswer{questionId=id};
            if(q.kind=="choice") {
                int index=Array.FindIndex(q.options,x=>x.id==optionId);if(index<0)return false;
                answer.optionId=optionId;answer.optionIndex=index;answer.value=q.options[index].label;
            } else {
                text=(text??"").Trim();if(text.Length>Math.Min(100,q.maxLength)||text.Any(char.IsControl))return false;
                answer.optionId="text";answer.value=text;
            }
            d.point=d.point??point;d.answers=d.answers.Where(x=>x.questionId!=id).Concat(new[]{answer}).ToArray();return true;
        }
        public static bool Ready(GameState state,DemographicsContent content) {
            var d=Ensure(state,content);
            return content.questions.Where(q=>q.required).All(q=>d.answers.Any(a=>a.questionId==q.id&&(q.kind=="choice"?q.options.Any(o=>o.id==a.optionId):!string.IsNullOrWhiteSpace(a.value))));
        }
        public static bool Complete(GameState state,DemographicsContent content) {
            var d=Ensure(state,content);if(d.completed||!Ready(state,content))return false;
            var now=EvaluationRules.Point(state,CampaignRules.CaseIdForTurn(CampaignRules.Turn(state)),JsonContent.Settings.contentVersion.ToString());
            if(!EvaluationRules.HasPoint(d.point))d.point=now;   // saves που κόλλησαν με κενό point πριν τη διόρθωση
            if(!EvaluationRules.HasPoint(d.point))return false;
            d.completed=true;d.completedPoint=now;return true;
        }
        public static bool Valid(DemographicsState d) {
            if(d==null)return true;if(d.answers==null||d.answers.Length>20)return false;
            if(d.answers.Any(a=>a==null||string.IsNullOrWhiteSpace(a.questionId)||a.questionId.Length>64||a.value==null||a.value.Length>100||a.optionIndex< -1||a.optionIndex>10)||d.answers.Select(a=>a.questionId).Distinct().Count()!=d.answers.Length)return false;
            return !d.completed||d.answers.Length>0&&EvaluationRules.HasPoint(d.point);
        }
    }
}
