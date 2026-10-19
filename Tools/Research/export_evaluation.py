"""Offline, privacy-minimised tidy CSV export of already authorised event JSON.

No network, credentials, Firestore connection, or modification of input records.
Accepts {events:[...]}, a JSON list, or Firestore REST {documents:[{fields:...}]}.
"""
from pathlib import Path
import argparse
import csv
import json
from collections import Counter, defaultdict
from statistics import mean, median

P = Path(__file__).resolve().parent
METRICS = ('consistency','ethics','authority','persuasion','parrhesia','recognition')
CITY_METRICS = ('food','trust','readiness','war')

def unwrap(value):
    if not isinstance(value, dict): return value
    if 'stringValue' in value: return value['stringValue']
    if 'integerValue' in value: return int(value['integerValue'])
    if 'doubleValue' in value: return float(value['doubleValue'])
    if 'booleanValue' in value: return value['booleanValue']
    if 'nullValue' in value: return None
    if 'timestampValue' in value: return value['timestampValue']
    if 'mapValue' in value: return {k:unwrap(v) for k,v in value['mapValue'].get('fields',{}).items()}
    if 'arrayValue' in value: return [unwrap(v) for v in value['arrayValue'].get('values',[])]
    return {k:unwrap(v) for k,v in value.items()}

def load_records(path):
    raw = json.loads(Path(path).read_text(encoding='utf-8-sig'))
    synthetic = isinstance(raw,dict) and raw.get('synthetic') is True
    records = raw.get('events',raw.get('documents',[])) if isinstance(raw,dict) else raw
    if not isinstance(records,list): raise ValueError('Input must contain an event list.')
    return [unwrap(x.get('fields',x)) for x in records], synthetic

def write_csv(path, rows, fallback):
    fields = list(dict.fromkeys([*fallback,*(key for row in rows for key in row)]))
    with path.open('w',encoding='utf-8-sig',newline='') as f:
        w=csv.DictWriter(f,fieldnames=fields); w.writeheader(); w.writerows(rows)

def event_metadata(event):
    try: meta=json.loads(event.get('sourceRef','{}'))
    except (ValueError,TypeError): meta={}
    return meta if isinstance(meta,dict) else {}

def research_participant(event, meta):
    return meta.get('researchParticipantId') or (meta.get('evaluation') or {}).get('researchParticipantId') or event.get('session','')

def analyse(input_file, output_folder, content_file=P/'EvaluationContent.json'):
    events,synthetic = load_records(input_file)
    out=Path(output_folder);out.mkdir(parents=True,exist_ok=True)
    content=json.loads(Path(content_file).read_text(encoding='utf-8'))
    questions={q['id']:q for key in ['onboarding','postTest'] for q in content[key]}
    ratings={q['id']:q for key in ['experience','participationIntention'] for q in content[key]}
    errors=[]; warnings=[]; duplicates=0; contextual_replays=0; unique={}
    for i,e in enumerate(events):
        meta=event_metadata(e)
        key=(research_participant(e,meta),e.get('session',''),e.get('eventId') or f'input-row-{i}')
        if key in unique:
            duplicates+=1
            previous=unique[key]
            semantic_fields=('type','nodeId','choiceId','answer','correct','orientation','stance','outcome','session')
            if any(previous.get(field)!=e.get(field) for field in semantic_fields):
                errors.append({'kind':'conflicting-event-id','event_id':key[2],'session':key[1]})
            elif previous!=e: contextual_replays+=1
            # A resumed browser can reconstruct the same answer with a later
            # checkpoint timestamp. Preserve its earliest observed record.
            if str(e.get('t',''))<str(previous.get('t','')):unique[key]=e
            continue
        unique[key]=e
    rows=[];knowledge=[];rating_rows=[];decisions=[];points=[]
    for order,e in enumerate(unique.values()):
        meta=event_metadata(e)
        evaluation=meta.get('evaluation') or {}
        point=meta.get('point') or meta.get('assessmentPoint') or evaluation.get('point') or {}
        stage=meta.get('stage') or evaluation.get('stage') or {'initial_knowledge':'pre','final_knowledge':'post'}.get(e.get('type'),'')
        participant=research_participant(e,meta)
        instrument=meta.get('instrumentVersion') or evaluation.get('instrumentVersion') or meta.get('instrument') or 'unknown'
        common={'research_participant_id':participant,'session':e.get('session',''),'instrument_version':instrument,
          'administration_version':meta.get('administrationVersion','legacy-midcampaign-post-v1'),
          'post_test_status':meta.get('postTestStatus','unknown'),
          'assessment_id':meta.get('assessmentId') or evaluation.get('assessmentId') or (e.get('session','')+':'+stage),
          'stage':stage,'timestamp_utc':e.get('t',''),'event_id':e.get('eventId',''),
          'prytany':point.get('prytany',e.get('prytany','')),'week':point.get('week',e.get('week','')),
          'turn':point.get('turn',meta.get('turn','')),'completed_cases':point.get('completedCases',meta.get('completedCases','')),
          'current_case_id':point.get('caseId',meta.get('caseId','')),
          'content_version':point.get('contentVersion',meta.get('contentVersion',meta.get('bank',''))),
          'active_seconds':point.get('activeSeconds',e.get('duration','')),
          'baseline_exposure':meta.get('exposure') or evaluation.get('exposure') or '',
          'research_consent':meta.get('researchConsent','unknown'),
          'synthetic':synthetic or meta.get('synthetic') is True}
        kind=e.get('type','');item_id=meta.get('questionId') or meta.get('itemId') or e.get('nodeId','')
        choice_deltas={}
        if kind in ['choice','case_choice']:
            choice_deltas={'city_delta':meta.get('cityDelta',''),'personal_delta':meta.get('personalDelta',''),
                **{'metric_'+k:v for k,v in (meta.get('metricsDelta') or {}).items() if k in METRICS},
                **{'city_metric_'+k:v for k,v in (meta.get('cityMetricsDelta') or {}).items() if k in CITY_METRICS}}
        rows.append({**common,'type':kind,'answer':e.get('answer',''),'choice_id':e.get('choiceId',''),**choice_deltas})
        if kind in ['initial_knowledge','final_knowledge','assessment_response']:
            q=questions.get(item_id)
            if not q:
                warnings.append({'kind':'unknown-question-id','event_id':e.get('eventId'), 'question_id':item_id});continue
            answer=e.get('answer',-1)
            if not isinstance(answer,int) or answer not in range(4):
                errors.append({'kind':'invalid-knowledge-answer','event_id':e.get('eventId')});continue
            correct=int(answer==q['correctIndex'])
            if e.get('correct',correct) not in [-1,correct]:errors.append({'kind':'incorrect-correctness-flag','event_id':e.get('eventId')})
            if stage not in ['pre','post']:
                errors.append({'kind':'missing-stage','event_id':e.get('eventId')});continue
            knowledge.append({**common,'question_id':item_id,'construct':q['construct'],'answer_index':answer,
                              'correct_index':q['correctIndex'],'correct':correct,'source_ref':q['sourceRef'],'_order':order})
        if kind in ['experience_rating','participation_intention']:
            q=ratings.get(item_id)
            if not q:
                warnings.append({'kind':'unknown-rating-id','event_id':e.get('eventId'),'item_id':item_id});continue
            answer=e.get('answer',-1)
            if not isinstance(answer,int) or answer not in range(1,6):
                errors.append({'kind':'invalid-rating','event_id':e.get('eventId')});continue
            rating_rows.append({**common,'item_id':item_id,'construct':q['construct'],'rating':answer,'_order':order})
        if kind=='case_choice':
            observation=meta.get('observation') is True
            answer=e.get('answer',-1);stance=e.get('stance','');orientation=e.get('orientation','unclassified')
            eligible=stance=='vote' and not observation and answer in [0,1] and orientation in ['democracy','oligarchy','mixed']
            collective=meta.get('collectiveOutcome',-1)
            if type(collective) is not int or collective not in [-1,0,1]:
                errors.append({'kind':'invalid-collective-outcome','event_id':e.get('eventId')})
                collective=-1
            political=meta.get('political') or {}
            decisions.append({**common,'case_id':meta.get('caseId') or e.get('nodeId','').split(':')[0],
                'stance':stance,'answer_index':answer,'orientation':orientation,'observation':observation,
                'abstention':stance=='vote' and not observation and answer==2,'orientation_eligible':eligible,
                'choice_id':e.get('choiceId',''),'collective_outcome':collective,
                'player_choice_prevailed':answer==collective if stance=='vote' and not observation and answer in [0,1] and collective in [0,1] else '',
                **choice_deltas,
                **{'political_'+k:v for k,v in political.items() if not isinstance(v,(dict,list))},'_order':order})
        if kind in ['assessment_start','assessment_complete','assessment_not_administered','evaluation_start','evaluation_offer','evaluation_deferred','evaluation_complete']:
            points.append({**common,'type':kind})
    # Keep a final answer per item when the questionnaire permits edits. The event CSV
    # still preserves all original answer changes and their timestamps.
    def final_answers(items, id_name):
        final={}
        for r in sorted(items,key=lambda r:(r['timestamp_utc'],r['_order'])):
            final[(r['research_participant_id'],r['session'],r['instrument_version'],r['assessment_id'],r[id_name])]=r
        return list(final.values())
    knowledge=final_answers(knowledge,'question_id');rating_rows=final_answers(rating_rows,'item_id')
    grouped=defaultdict(list)
    for r in knowledge:grouped[(r['research_participant_id'],r['session'],r['instrument_version'],r['stage'])].append(r)
    # Never add two separate administrations into an apparent score out of eight.
    for key,items in list(grouped.items()):
        administrations=defaultdict(list)
        for r in items:administrations[r['assessment_id']].append(r)
        candidates=sorted(administrations.values(),key=lambda a:min(r['timestamp_utc'] for r in a))
        complete=[a for a in candidates if len({r['construct'] for r in a})==8]
        grouped[key]=complete[0] if complete else candidates[-1]
        if len(candidates)>1:warnings.append({'kind':'multiple-administrations-first-complete-selected','session':key[1],'stage':key[3],'count':len(candidates)})
    session_keys=sorted({(r['research_participant_id'],r['session'],r['instrument_version']) for r in knowledge+rating_rows+points})
    sessions=[];gains=[]
    for key in session_keys:
        pre=grouped[key+('pre',)];post=grouped[key+('post',)]
        pre_complete=len({r['construct'] for r in pre})==8
        post_complete=len({r['construct'] for r in post})==8
        paired=pre_complete and post_complete
        gain=sum(r['correct'] for r in post)-sum(r['correct'] for r in pre) if paired else ''
        if paired:gains.append(gain)
        post_counts={r['completed_cases'] for r in post}
        context=[r for r in rows if (r['research_participant_id'],r['session'],r['instrument_version'])==key]
        observed_statuses={r['post_test_status'] for r in context}
        post_status='complete' if post_complete else 'partial' if post else 'in_progress' if 'in_progress' in observed_statuses else 'not_administered' if 'not_administered' in observed_statuses else 'not_started'
        evaluations=[r for r in points if (r['research_participant_id'],r['session'],r['instrument_version'])==key]
        if paired and '' in post_counts:warnings.append({'kind':'missing-assessment-progress','session':key[1]})
        sessions.append({'research_participant_id':key[0],'session':key[1],'instrument_version':key[2],
            'pre_answered':len(pre),'post_answered':len(post),'pre_complete':pre_complete,'post_complete':post_complete,
            'administration_versions':','.join(sorted({r['administration_version'] for r in context})),
            'post_test_status':post_status,'post_test_administered':bool(post) or post_status=='in_progress',
            'evaluation_complete':any(r['type']=='evaluation_complete' for r in evaluations),
            'evaluation_completed_cases':','.join(map(str,sorted({r['completed_cases'] for r in evaluations if r['type']=='evaluation_complete'},key=str))),
            'paired_complete':paired,'pre_score_8':sum(r['correct'] for r in pre) if pre_complete else '',
            'post_score_8':sum(r['correct'] for r in post) if post_complete else '', 'gain_points_8':gain,
            'post_completed_cases':','.join(map(str,sorted(post_counts,key=str))), 'synthetic':synthetic})
        eligible=[r for r in decisions if r['research_participant_id']==key[0] and r['session']==key[1] and r['orientation_eligible']]
        counts=Counter(r['orientation'] for r in eligible)
        sessions[-1].update({'orientation_eligible_votes':len(eligible), **{v+'_votes':counts[v] for v in ['democracy','oligarchy','mixed']},
            **{v+'_percent':100*counts[v]/len(eligible) if eligible else '' for v in ['democracy','oligarchy','mixed']}})
    orientation=Counter(r['orientation'] for r in decisions if r['orientation_eligible'])
    denominator=sum(orientation.values())
    rating_summary=[]
    for item,question in ratings.items():
        values=[r['rating'] for r in rating_rows if r['item_id']==item]
        rating_summary.append({'item_id':item,'construct':question['construct'],'n':len(values),
            'median':median(values) if values else None,'distribution':{str(i):values.count(i) for i in range(1,6)}})
    def cleaned(items):return [{k:v for k,v in r.items() if k!='_order'} for r in items]
    for name,items,columns in [('events',rows,['event_id','type']),('knowledge',knowledge,['question_id','correct']),
        ('ratings',rating_rows,['item_id','rating']),('decisions',decisions,['case_id','orientation_eligible']),
        ('assessment_points',points,['type','completed_cases']),('sessions',sessions,['session','paired_complete'])]:
        write_csv(out/(name+'.csv'),cleaned(items),columns)
    summary={'synthetic':synthetic,'input_events':len(events),'unique_events':len(unique),'deduplicated_events':duplicates,
       'sessions':len(sessions),'paired_complete_sessions':len(gains),'mean_paired_gain_points_8':mean(gains) if gains else None,
       'post_test_not_administered_sessions':sum(r['post_test_status']=='not_administered' for r in sessions),
       'median_paired_gain_points_8':median(gains) if gains else None,'orientation_denominator':denominator,
       'in_game_orientation_counts':dict(orientation),
       'in_game_orientation_percent':{k:100*v/denominator for k,v in orientation.items()} if denominator else {},
       'abstention_ballots':sum(r['abstention'] for r in decisions),
       'excluded_observation_decisions':sum(r['observation'] and r['stance']=='vote' for r in decisions),
       'ratings_by_item':rating_summary,'knowledge_missing_data':'Mid-campaign evaluation has no post-test. Empty CSV scores are missing, never zero. Paired gain requires eight actual answers in each administration; keep administration versions and exposure separate.',
       'interpretation':'Descriptive pilot data; not validated ideology, causal participation change, or comparative efficacy.'}
    (out/'Summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
    quality={'errors':errors,'warnings':warnings,'contextual_replays_deduplicated':contextual_replays,'no_imputation':True,'auth_uid_exported':False,'email_exported':False,
             'raw_source_ref_exported':False,'synthetic':synthetic}
    (out/'Quality.json').write_text(json.dumps(quality,ensure_ascii=False,indent=2),encoding='utf-8')
    return summary,quality

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input',required=True);parser.add_argument('--output',required=True)
    parser.add_argument('--content',default=str(P/'EvaluationContent.json'))
    args=parser.parse_args()
    summary,quality=analyse(args.input,args.output,args.content)
    print(json.dumps({'sessions':summary['sessions'],'paired':summary['paired_complete_sessions'],'errors':len(quality['errors']),
                      'synthetic':summary['synthetic'],'output':str(Path(args.output).resolve())},ensure_ascii=False))
