"""Offline, version-aware research export. No network or credentials are used.

Synthetic events are excluded unless --include-synthetic is explicit. Ratings are
reported by instrument/content/exposure cohort; there is no validated total score.
"""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict
import csv
import hashlib
import json
from pathlib import Path
import re
from statistics import mean, median

P = Path(__file__).resolve().parent
METRICS = ('consistency', 'ethics', 'authority', 'persuasion', 'parrhesia', 'recognition')
CITY_METRICS = ('food', 'trust', 'readiness', 'war')
STATE_FIELDS = ('treasury', 'personalTreasury', *METRICS)
EMAIL = re.compile(r'\b[^\s<>@]+@[^\s<>@]+\.[^\s<>@]+\b')
RATING_EVENTS = ('experience_rating', 'participation_intention')
POINT_EVENTS = ('assessment_start', 'assessment_complete', 'assessment_not_administered',
                'evaluation_start', 'evaluation_offer', 'evaluation_deferred', 'evaluation_complete')
COHORT_FIELDS = ('instrument_version', 'instrument_hash', 'administration_version', 'content_version',
                 'bank_hash', 'evaluation_scope', 'synthetic')


def unwrap(value):
    if not isinstance(value, dict):
        return value
    for name, cast in (('stringValue', str), ('integerValue', int), ('doubleValue', float),
                       ('booleanValue', bool), ('timestampValue', str)):
        if name in value:
            return cast(value[name])
    if 'nullValue' in value:
        return None
    if 'mapValue' in value:
        return {k: unwrap(v) for k, v in value['mapValue'].get('fields', {}).items()}
    if 'arrayValue' in value:
        return [unwrap(v) for v in value['arrayValue'].get('values', [])]
    return {k: unwrap(v) for k, v in value.items()}


def event_metadata(event):
    source = event.get('sourceRef', {})
    if isinstance(source, dict):
        return source
    try:
        meta = json.loads(source or '{}')
    except (ValueError, TypeError):
        return {}
    return meta if isinstance(meta, dict) else {}


def load_records(path):
    raw = json.loads(Path(path).read_text(encoding='utf-8-sig'))
    synthetic = isinstance(raw, dict) and raw.get('synthetic') is True
    records = raw.get('events', raw.get('documents', [])) if isinstance(raw, dict) else raw
    if not isinstance(records, list):
        raise ValueError('Input must contain an event list.')
    return [unwrap(x.get('fields', x)) for x in records], synthetic


def clean_text(value):
    return EMAIL.sub('[redacted-email]', str(value or ''))


def pseudonym(value):
    value = str(value or '')
    # IDs supplied by the game are random research IDs, not Authentication UIDs.
    # Do not allow an accidentally supplied email address to become a CSV key.
    if '@' in value:
        return 'redacted-' + hashlib.sha256(value.encode()).hexdigest()[:16]
    return value


def research_participant(event, meta):
    return pseudonym(meta.get('researchParticipantId') or
                     (meta.get('evaluation') or {}).get('researchParticipantId') or event.get('session', ''))


def write_csv(path, rows, fallback):
    fields = list(dict.fromkeys([*fallback, *(key for row in rows for key in row)]))
    with Path(path).open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader()
        # Formula-like free text is escaped for spreadsheet consumers.
        for row in rows:
            writer.writerow({key: ("'" + value if isinstance(value, str) and value.startswith(('=', '+', '-', '@'))
                                  else value) for key, value in row.items()})


class InstrumentRegistry:
    def __init__(self):
        self.by_hash = {}
        self.versions = defaultdict(dict)

    def add(self, content, content_hash):
        version = content['instrumentVersion']
        self.by_hash[(version, content_hash)] = content
        self.versions[version][content_hash] = content

    def resolve(self, version, content_hash=''):
        if content_hash:
            return self.by_hash.get((version, content_hash))
        candidates = self.versions.get(version, {})
        return next(iter(candidates.values())) if len(candidates) == 1 else None


def load_instruments(content_file):
    paths = [P / 'EvaluationContent.legacy.json', *sorted((P / 'Instruments').glob('*.json'))]
    paths += list(content_file) if isinstance(content_file, (list, tuple)) else [content_file]
    registry = InstrumentRegistry()
    for raw_path in paths:
        path = Path(raw_path)
        if not path.exists() and path.name == 'EvaluationContent.legacy.json':
            continue
        # The browser hashes the exact TextAsset bytes-as-UTF8 text, including CRLF.
        text = path.read_bytes().decode('utf-8-sig')
        content = json.loads(text)
        version = content.get('instrumentVersion')
        if not version:
            raise ValueError('Each frozen content file must contain instrumentVersion.')
        registry.add(content, hashlib.sha256(text.encode('utf-8')).hexdigest()[:32])
    return registry


def item_metadata(item, intention=False):
    group = item.get('group') or item.get('instrumentSection') or item.get('section')
    if intention:
        group = 'participation-intention'
    if not group:
        group = 'custom' if item['id'] in ('ux-agency', 'ux-consequences', 'ux-role', 'ux-continue') else 'legacy-adapted'
    return {
        'item_id': item['id'], 'construct': item.get('construct', ''), 'item_group': group,
        'instrument_section': item.get('instrumentSection', item.get('section', '')),
        'official_item_id': item.get('officialItemId', ''), 'prompt': item.get('prompt', ''),
        'source_ref': item.get('sourceRef', ''), 'allow_not_applicable': item.get('allowNotApplicable') is True,
        'reverse': item.get('reverse') is True,
    }


def content_items(content):
    return {item['id']: item_metadata(item, key == 'participationIntention')
            for key in ('experience', 'participationIntention') for item in content.get(key, [])}


def cohort(row):
    return tuple(row.get(key, '') for key in COHORT_FIELDS)


def cohort_id(key):
    return hashlib.sha256(json.dumps(key, ensure_ascii=False).encode()).hexdigest()[:12]


def quantile(values, proportion):
    """Linear interpolation (R type 7); defined even for a single observation."""
    values = sorted(values)
    if not values:
        return None
    position = (len(values) - 1) * proportion
    lower = int(position)
    upper = min(lower + 1, len(values) - 1)
    return values[lower] + (values[upper] - values[lower]) * (position - lower)


def scope_for(point):
    count = point.get('completedCases')
    if type(count) not in (int, float):
        return 'unknown-exposure'
    if point.get('totalCases', 0) > 0:
        return 'full-campaign' if count >= point['totalCases'] else 'first-prytany' if count == point.get('casesPerPrytany', 2) else 'partial-campaign'
    return 'first-prytany' if count == 5 else 'extended-play' if count > 5 else 'below-first-prytany'


def analyse(input_file, output_folder, content_file=P / 'EvaluationContent.json', *, include_synthetic=False):
    events, envelope_synthetic = load_records(input_file)
    instruments = load_instruments(content_file)
    out = Path(output_folder)
    out.mkdir(parents=True, exist_ok=True)
    errors, warnings = [], []
    unique = {}
    duplicates = contextual_replays = skipped_synthetic = 0
    for order, event in enumerate(events):
        meta = event_metadata(event)
        synthetic = envelope_synthetic or event.get('synthetic') is True or meta.get('synthetic') is True
        if synthetic and not include_synthetic:
            skipped_synthetic += 1
            continue
        key = (research_participant(event, meta), event.get('session', ''),
               event.get('eventId') or f'input-row-{order}', synthetic,
               meta.get('instrumentVersion', ''), meta.get('instrumentHash', ''), meta.get('bankHash', ''))
        if key in unique:
            duplicates += 1
            previous = unique[key][1]
            fields = ('type', 'nodeId', 'choiceId', 'answer', 'correct', 'orientation', 'stance', 'outcome', 'session')
            if any(previous.get(name) != event.get(name) for name in fields):
                errors.append({'kind': 'conflicting-event-id', 'event_id': key[2], 'session': pseudonym(key[1])})
            elif previous != event:
                contextual_replays += 1
            if str(event.get('t', '')) < str(previous.get('t', '')):
                unique[key] = (order, event, meta, synthetic)
            continue
        unique[key] = (order, event, meta, synthetic)

    rows, knowledge, rating_rows, decisions, points, feedback, case_knowledge = [], [], [], [], [], [], []
    unknown_versions = set()
    tendency_rows = []
    demographic_rows = []
    for order, event, meta, synthetic in unique.values():
        evaluation = meta.get('evaluation') or {}
        point = meta.get('point') or meta.get('assessmentPoint') or evaluation.get('point') or {}
        stage = meta.get('stage') or evaluation.get('stage') or {
            'initial_knowledge': 'pre', 'final_knowledge': 'post'}.get(event.get('type'), '')
        version = meta.get('instrumentVersion') or evaluation.get('instrumentVersion') or meta.get('instrument') or 'unknown'
        content_hash = meta.get('instrumentHash', '')
        content = instruments.resolve(version, content_hash)
        if content is None:
            unknown_versions.add((version, content_hash))
        questions = {item['id']: item for key in ('onboarding', 'postTest') for item in (content or {}).get(key, [])}
        rating_items = content_items(content or {})
        session = pseudonym(event.get('session', ''))
        source_assessment = meta.get('assessmentId') or evaluation.get('assessmentId') or session + ':' + stage
        submission = meta.get('submissionId') or evaluation.get('submissionId') or ''
        assessment = submission if submission and event.get('type') not in ('initial_knowledge', 'final_knowledge', 'assessment_response') else source_assessment
        common = {
            'research_participant_id': research_participant(event, meta), 'session': session,
            'instrument_version': version,
            'instrument_hash': meta.get('instrumentHash', ''),
            'administration_version': meta.get('administrationVersion', evaluation.get('administrationVersion', 'legacy-midcampaign-post-v1')),
            'post_test_status': meta.get('postTestStatus', evaluation.get('postTestStatus', 'unknown')),
            'assessment_id': pseudonym(assessment), 'source_assessment_id': pseudonym(source_assessment),
            'submission_id': pseudonym(submission), 'stage': stage, 'timestamp_utc': event.get('t', ''),
            'event_id': pseudonym(event.get('eventId', '')), 'event_sequence': meta.get('eventSequence', ''),
            'prytany': point.get('prytany', event.get('prytany', '')),
            'week': point.get('week', event.get('week', '')), 'turn': point.get('turn', meta.get('turn', '')),
            'completed_cases': point.get('completedCases', meta.get('completedCases', '')),
            'current_case_id': point.get('caseId', meta.get('caseId', '')),
            'content_version': point.get('contentVersion', meta.get('contentVersion', meta.get('bank', event.get('contentVersion', '')))),
            'bank_hash': meta.get('bankHash', ''), 'active_seconds': point.get('activeSeconds', event.get('duration', '')),
            'response_active_seconds': meta.get('responseActiveSeconds', ''),
            'baseline_exposure': meta.get('exposure') or evaluation.get('exposure') or '',
            'research_consent': meta.get('researchConsent', 'unknown'), 'synthetic': synthetic,
            'evaluation_scope': scope_for(point), '_order': order,
        }
        if common['turn'] == '' and type(common['prytany']) is int and type(common['week']) is int and common['prytany'] >= 1 and common['week'] >= 1:
            common['turn'] = (common['prytany'] - 1) * (meta.get('campaignLayout') or {}).get('casesPerPrytany',5) + common['week'] - 1
        kind = event.get('type', '')
        item_id = meta.get('questionId') or meta.get('itemId') or event.get('nodeId', '')
        deltas = {}
        if kind in ('choice', 'case_choice'):
            deltas = {'city_delta': meta.get('cityDelta', ''), 'personal_delta': meta.get('personalDelta', ''),
                      **{'metric_' + k: v for k, v in (meta.get('metricsDelta') or {}).items() if k in METRICS},
                      **{'city_metric_' + k: v for k, v in (meta.get('cityMetricsDelta') or {}).items() if k in CITY_METRICS}}
        snapshot = {**{'state_' + key: event.get(key, '') for key in STATE_FIELDS},
                    **{'state_city_' + key: (meta.get('citySnapshot') or {}).get(key, '') for key in CITY_METRICS}}
        rows.append({**common, 'type': kind, 'answer': event.get('answer', ''),
                     'choice_id': clean_text(event.get('choiceId', '')), **snapshot, **deltas})
        if kind in ('initial_knowledge', 'final_knowledge', 'assessment_response'):
            question = questions.get(item_id)
            answer = event.get('answer', -1)
            if not question:
                warnings.append({'kind': 'unknown-question-id', 'event_id': common['event_id'], 'question_id': item_id, 'instrument_version': version})
            elif type(answer) is not int or answer not in range(4):
                errors.append({'kind': 'invalid-knowledge-answer', 'event_id': common['event_id']})
            elif stage not in ('pre', 'post'):
                errors.append({'kind': 'missing-stage', 'event_id': common['event_id']})
            else:
                correct = int(answer == question['correctIndex'])
                if event.get('correct', correct) not in (-1, correct):
                    errors.append({'kind': 'incorrect-correctness-flag', 'event_id': common['event_id']})
                knowledge.append({**common, 'question_id': item_id, 'construct': question['construct'],
                                  'answer_index': answer, 'correct_index': question['correctIndex'],
                                  'correct': correct, 'source_ref': question.get('sourceRef', '')})
        if kind == 'demographic_response':
            demographic_id = meta.get('itemId', event.get('nodeId','').removeprefix('demographic:'))
            demographic = next((q for q in ((content or {}).get('demographics') or {}).get('questions',[]) if q['id']==demographic_id), None)
            if not demographic:
                errors.append({'kind':'unknown-demographic-id','event_id':common['event_id'],'item_id':demographic_id})
            else:
                option_id = meta.get('demographicOptionId','')
                options = demographic.get('options') or []
                if demographic['kind']=='choice' and option_id not in {o['id'] for o in options}:
                    errors.append({'kind':'invalid-demographic-option','event_id':common['event_id']})
                else:
                    demographic_rows.append({**common,'item_id':demographic_id,'demographic_version':meta.get('demographicVersion',''),
                        'option_id':option_id,'value':clean_text(meta.get('demographicValue',''))[:100],
                        'prompt':demographic['prompt'],'response_kind':demographic['kind'],
                        'not_disclosed':option_id=='not-disclosed'})
        if kind in ('initial_tendency', 'final_tendency'):
            item = next((x for x in (content or {}).get('tendencies', []) if x['id'] == item_id), None)
            answer = event.get('answer', -1)
            if item is None or type(answer) is not int or answer not in range(1, 6):
                errors.append({'kind': 'invalid-tendency-response', 'event_id': common['event_id'], 'item_id': item_id})
            else:
                tendency_rows.append({**common, 'item_id': item_id, 'stage': 'post' if kind == 'final_tendency' else 'pre',
                    'prompt': item.get('prompt', ''), 'raw_answer': answer,
                    'coded_answer': 6-answer if item.get('reverse') else answer,
                    'reverse': bool(item.get('reverse')), 'orientation_code': item.get('orientation', 'mixed'),
                    'analysis_weight': item.get('analysisWeight', 1),
                    'tendency_version': meta.get('tendencyVersion', (content or {}).get('tendencyVersion', '')),
                    'instrument_status': (content or {}).get('tendencyStatus', 'custom-not-validated')})
        if kind in RATING_EVENTS:
            question = rating_items.get(item_id)
            answer = event.get('answer', -1)
            if not question:
                warnings.append({'kind': 'unknown-rating-id', 'event_id': common['event_id'], 'item_id': item_id, 'instrument_version': version})
                continue
            if type(answer) is not int or answer not in range(-1, 6):
                errors.append({'kind': 'invalid-rating', 'event_id': common['event_id']})
                continue
            if answer == 0 and not question['allow_not_applicable']:
                errors.append({'kind': 'not-applicable-not-allowed', 'event_id': common['event_id'], 'item_id': item_id})
                continue
            status = 'answered' if answer >= 1 else 'not_applicable' if answer == 0 else 'missing'
            normalized = answer - 3 if status == 'answered' else None
            rating_rows.append({**common, **question, 'raw_answer': answer,
                                'rating': answer if status == 'answered' else None,
                                'normalized_minus2_plus2': normalized, 'response_status': status})
        if kind == 'evaluation_feedback':
            text = meta.get('feedback', meta.get('openFeedback', ''))
            feedback.append({**common, 'feedback': clean_text(text)[:1000]})
        if kind == 'case_knowledge':
            answer = event.get('answer', -1)
            correct_index = meta.get('correctIndex', -1)
            if type(answer) is not int or answer not in range(4) or type(correct_index) is not int or correct_index not in range(4):
                errors.append({'kind': 'invalid-case-knowledge-answer-or-key', 'event_id': common['event_id']})
            else:
                correct = int(answer == correct_index)
                if event.get('correct', correct) not in (-1, correct):
                    errors.append({'kind': 'incorrect-case-knowledge-flag', 'event_id': common['event_id']})
                case_knowledge.append({**common, 'case_id': meta.get('caseId', ''),
                                       'question_id': event.get('nodeId', ''),
                                       'answer_index': answer, 'correct_index': correct_index, 'correct': correct,
                                       'question': clean_text(meta.get('question', '')), 'source_ref': clean_text(meta.get('reference', ''))})
        if kind == 'case_choice':
            observation = meta.get('observation') is True
            answer, stance = event.get('answer', -1), event.get('stance', '')
            orientation = event.get('orientation', 'unclassified')
            eligible = stance == 'vote' and not observation and answer in (0, 1) and orientation in ('democracy', 'oligarchy', 'mixed')
            collective = meta.get('collectiveOutcome', -1)
            if type(collective) is not int or collective not in (-1, 0, 1):
                errors.append({'kind': 'invalid-collective-outcome', 'event_id': common['event_id']})
                collective = -1
            decisions.append({**common, 'case_id': meta.get('caseId') or event.get('nodeId', '').split(':')[0],
                              'stance': stance, 'answer_index': answer, 'orientation': orientation, 'observation': observation,
                              'abstention': stance == 'vote' and not observation and answer == 2,
                              'orientation_eligible': eligible, 'choice_id': clean_text(event.get('choiceId', '')),
                              'collective_outcome': collective,
                              'orientation_reason': clean_text(meta.get('reason', '')),
                              'source_ref': clean_text(meta.get('reference', '')),
                              'player_choice_prevailed': answer == collective if stance == 'vote' and not observation and answer in (0, 1) and collective in (0, 1) else '',
                              **snapshot, **deltas,
                              **{'political_metric_before_' + k: v for k, v in ((meta.get('political') or {}).get('metricsBefore') or {}).items() if k in METRICS},
                              **{'political_' + k: v for k, v in (meta.get('political') or {}).items() if not isinstance(v, (dict, list))}})
        if kind in POINT_EVENTS:
            points.append({**common, 'type': kind})

    def response_key(row):
        return (row['research_participant_id'], row['session'], row['instrument_version'], row['assessment_id'],
                row['synthetic'], row['instrument_hash'], row['content_version'], row['bank_hash'])

    def final_answers(items, id_name):
        final = {}
        for row in sorted(items, key=lambda r: (r['timestamp_utc'], r['event_sequence'] if type(r['event_sequence']) is int else r['_order'], r['_order'])):
            final[response_key(row) + (row[id_name],)] = row
        return list(final.values())

    knowledge = final_answers(knowledge, 'question_id')
    rating_rows = final_answers(rating_rows, 'item_id')
    final_feedback = {}
    for row in sorted(feedback, key=lambda r: (r['timestamp_utc'], r['event_sequence'] if type(r['event_sequence']) is int else r['_order'], r['_order'])):
        final_feedback[response_key(row)] = row
    feedback = list(final_feedback.values())
    # Stable submission IDs prevent a retried checkpoint from becoming a second person.
    completion = {}
    duplicate_submissions = 0
    for point in sorted(points, key=lambda r: (r['timestamp_utc'], r['_order'])):
        if point['type'] != 'evaluation_complete':
            continue
        key = response_key(point)
        if key in completion:
            duplicate_submissions += 1
        else:
            completion[key] = point
    all_by_submission = defaultdict(list)
    for row in rating_rows:
        all_by_submission[response_key(row)].append(row)
    invalid_submissions = set()
    for key, point in completion.items():
        content = instruments.resolve(point['instrument_version'], point['instrument_hash'])
        if not content:
            invalid_submissions.add(key)
            continue
        expected = set(content_items(content))
        responses = {row['item_id'] for row in all_by_submission[key] if row['response_status'] != 'missing'}
        if responses != expected:
            invalid_submissions.add(key)
            errors.append({'kind': 'completed-submission-has-missing-items', 'session': point['session'],
                           'assessment_id': point['assessment_id'], 'missing_item_ids': sorted(expected - responses)})
    first_complete = {}
    for key, point in sorted(completion.items(), key=lambda pair: (pair[1]['timestamp_utc'], pair[1]['_order'])):
        # One research participant can restart the game. A new run is not a new person.
        person_version = (key[0], key[2], point['instrument_hash'], point['content_version'], point['bank_hash'], point['synthetic'])
        if key not in invalid_submissions:
            first_complete.setdefault(person_version, key)
    for rating in rating_rows:
        key = response_key(rating)
        point = completion.get(key)
        if point:
            # The submission point defines exposure for every answer in that administration.
            for field in ('completed_cases', 'active_seconds', 'prytany', 'week', 'turn', 'current_case_id',
                          'evaluation_scope', 'content_version', 'bank_hash', 'administration_version', 'instrument_hash'):
                rating[field] = point[field]
        person_version = (key[0], key[2], rating['instrument_hash'], rating['content_version'], rating['bank_hash'], rating['synthetic'])
        rating['submission_complete'] = point is not None
        rating['analysis_included'] = point is not None and first_complete.get(person_version) == key
        rating['cohort_id'] = cohort_id(cohort(rating))
    for row in feedback:
        key = response_key(row)
        point = completion.get(key)
        person_version = (key[0], key[2], row['instrument_hash'], row['content_version'], row['bank_hash'], row['synthetic'])
        row['analysis_included'] = point is not None and first_complete.get(person_version) == key

    def run_key(row):
        return (row['research_participant_id'], row['session'], row['instrument_version'], row['synthetic'],
                row['instrument_hash'], row['content_version'], row['bank_hash'])

    def final_case_records(items, fields):
        final = {}
        for row in sorted(items, key=lambda r: (r['timestamp_utc'], r['event_sequence'] if type(r['event_sequence']) is int else r['_order'], r['_order'])):
            final[run_key(row) + tuple(row[field] for field in fields)] = row
        return list(final.values())

    # A historical case ID can recur in a later week. Deduplicate revisions of
    # that occurrence, not separate participations in two different turns.
    decisions = final_case_records(decisions, ('turn', 'case_id', 'stance'))
    case_knowledge = final_case_records(case_knowledge, ('turn', 'case_id', 'question_id'))

    grouped_knowledge = defaultdict(lambda: defaultdict(list))
    for row in knowledge:
        key = run_key(row) + (row['stage'],)
        grouped_knowledge[key][row['assessment_id']].append(row)
    selected_knowledge = {}
    for key, administrations in grouped_knowledge.items():
        candidates = sorted(administrations.values(), key=lambda a: min(r['timestamp_utc'] for r in a))
        complete = [a for a in candidates if len({r['construct'] for r in a}) == 8]
        selected_knowledge[key] = complete[0] if complete else candidates[-1]
        if len(candidates) > 1:
            warnings.append({'kind': 'multiple-knowledge-administrations-first-complete-selected',
                             'session': key[1], 'stage': key[-1], 'count': len(candidates)})

    session_keys = sorted({run_key(r) for r in rows}, key=str)
    sessions = []
    for key in session_keys:
        pre, post = selected_knowledge.get(key + ('pre',), []), selected_knowledge.get(key + ('post',), [])
        pre_complete = len({r['construct'] for r in pre}) == 8
        post_complete = len({r['construct'] for r in post}) == 8
        paired = pre_complete and post_complete
        context = [r for r in rows if run_key(r) == key]
        evaluations = [r for r in points if run_key(r) == key]
        completed = [r for r in completion.values() if run_key(r) == key]
        first = min(completed, key=lambda r: (r['timestamp_utc'], r['_order'])) if completed else {}
        own_ratings = [r for r in rating_rows if run_key(r) == key]
        observed_statuses = {r['post_test_status'] for r in context}
        post_status = 'complete' if post_complete else 'partial' if post else 'in_progress' if 'in_progress' in observed_statuses else 'not_administered' if 'not_administered' in observed_statuses else 'not_started'
        eligible = [r for r in decisions if run_key(r) == key and r['orientation_eligible']]
        counts = Counter(r['orientation'] for r in eligible)
        starts = [r for r in evaluations if r['type'] == 'evaluation_start' and r['assessment_id'] == first.get('assessment_id')]
        start = min(starts, key=lambda r: r['timestamp_utc']) if starts else {}
        duration = first.get('active_seconds', '')
        start_seconds = start.get('active_seconds', '')
        questionnaire_seconds = max(0, duration - start_seconds) if type(duration) in (int, float) and type(start_seconds) in (int, float) else ''
        sessions.append({
            'research_participant_id': key[0], 'session': key[1], 'instrument_version': key[2], 'synthetic': key[3],
            'pre_answered': len(pre), 'post_answered': len(post), 'pre_complete': pre_complete,
            'post_complete': post_complete, 'post_test_status': post_status,
            'post_test_administered': bool(post) or post_status == 'in_progress', 'paired_complete': paired,
            'pre_score_8': sum(r['correct'] for r in pre) if pre_complete else '',
            'post_score_8': sum(r['correct'] for r in post) if post_complete else '',
            'gain_points_8': sum(r['correct'] for r in post) - sum(r['correct'] for r in pre) if paired else '',
            'administration_versions': ','.join(sorted({r['administration_version'] for r in context})),
            'evaluation_complete': bool(completed), 'completed_submissions': len(completed),
            'selected_submission_id': first.get('submission_id', ''),
            'evaluation_completed_cases': first.get('completed_cases', ''), 'evaluation_scope': first.get('evaluation_scope', ''),
            'evaluation_active_seconds': duration, 'questionnaire_active_seconds': questionnaire_seconds,
            'content_version': key[5], 'bank_hash': key[6], 'instrument_hash': key[4],
            'ratings_answered': sum(r['response_status'] == 'answered' for r in own_ratings if r['analysis_included']),
            'ratings_not_applicable': sum(r['response_status'] == 'not_applicable' for r in own_ratings if r['analysis_included']),
            'orientation_eligible_votes': len(eligible),
            **{v + '_votes': counts[v] for v in ('democracy', 'oligarchy', 'mixed')},
            **{v + '_percent': 100 * counts[v] / len(eligible) if eligible else '' for v in ('democracy', 'oligarchy', 'mixed')},
        })

    item_summaries, dimension_summaries, cohort_summaries = [], [], []
    rating_cohorts = defaultdict(list)
    for rating in rating_rows:
        rating_cohorts[cohort(rating)].append(rating)
    for key, cohort_rows in sorted(rating_cohorts.items(), key=lambda pair: str(pair[0])):
        info = {**dict(zip(COHORT_FIELDS, key)), 'cohort_id': cohort_id(key)}
        selected = [r for r in cohort_rows if r['analysis_included']]
        participant_count = len({(r['research_participant_id'], r['session']) for r in selected})
        cohort_summaries.append({**info, 'completed_sessions': participant_count,
                                 'observed_sessions': len({(r['research_participant_id'], r['session']) for r in cohort_rows}),
                                 'interpretation': 'Descriptive adapted-instrument cohort; not a validated total or causal estimate.'})
        for item_id, item in content_items(instruments.resolve(key[0], key[1]) or {}).items():
            candidates = [r for r in selected if r['item_id'] == item_id]
            values = [r['rating'] for r in candidates if r['response_status'] == 'answered']
            counts = Counter(values)
            item_summaries.append({**info, **item, 'n': len(values),
                                   'n_not_applicable': sum(r['response_status'] == 'not_applicable' for r in candidates),
                                   'n_missing': participant_count - len(candidates) + sum(r['response_status'] == 'missing' for r in candidates),
                                   'median': median(values) if values else None,
                                   'normalized_median_minus2_plus2': median(values) - 3 if values else None,
                                   'q1': quantile(values, .25), 'q3': quantile(values, .75),
                                   **{'rating_' + str(i) + '_n': counts[i] for i in range(1, 6)},
                                   **{'rating_' + str(i) + '_percent': 100 * counts[i] / len(values) if values else None for i in range(1, 6)}})
        # Equal-weight within-person descriptive means, then medians across people.
        # No overall MEEGA+ score, normative quality category or psychometric validation.
        by_dimension = defaultdict(list)
        for row in selected:
            by_dimension[(row['item_group'], row['construct'])].append(row)
        for (group, construct), members in by_dimension.items():
            people = defaultdict(list)
            for row in members:
                people[(row['research_participant_id'], row['session'])].append(row)
            item_count = len({r['item_id'] for r in members})
            person_means = []
            excluded = 0
            for responses in people.values():
                values = [r['rating'] for r in responses if r['response_status'] == 'answered']
                if len(responses) != item_count or any(r['response_status'] == 'missing' for r in responses) or not values:
                    excluded += 1
                    continue
                person_means.append(mean(values))
            dimension_summaries.append({**info, 'item_group': group, 'construct': construct,
                                        'item_count': item_count, 'n_participant_means': len(person_means),
                                        'n_excluded_missing_or_all_na': excluded,
                                        'median_participant_mean': median(person_means) if person_means else None,
                                        'q1_participant_mean': quantile(person_means, .25),
                                        'q3_participant_mean': quantile(person_means, .75),
                                        'interpretation': 'Descriptive unvalidated dimension mean; applicable items only; no imputation.'})

    def cleaned(items):
        return [{k: v for k, v in row.items() if not k.startswith('_')} for row in items]

    for name, items, columns in (
        ('events', rows, ['event_id', 'type']), ('knowledge', knowledge, ['question_id', 'correct']),
        ('ratings', rating_rows, ['item_id', 'rating', 'response_status']),
        ('decisions', decisions, ['case_id', 'orientation_eligible']),
        ('case_knowledge', case_knowledge, ['case_id', 'question_id', 'answer_index', 'correct']),
        ('assessment_points', points, ['type', 'completed_cases']),
        ('sessions', sessions, ['session', 'paired_complete']), ('feedback', feedback, ['assessment_id', 'feedback']),
        ('item_summary', item_summaries, ['cohort_id', 'item_id', 'n']),
        ('dimension_summary', dimension_summaries, ['cohort_id', 'construct']),
        ('cohorts', cohort_summaries, ['cohort_id', 'instrument_version', 'synthetic']),
    ):
        write_csv(out / (name + '.csv'), cleaned(items), columns)
    for version, content_hash in sorted(unknown_versions):
        warnings.append({'kind': 'unknown-instrument-version', 'instrument_version': version, 'instrument_hash': content_hash,
                         'action': 'Supply that frozen content using another --content; its ratings are not interpreted.'})
    included_flags = {row['synthetic'] for row in rows}
    label = 'empty-after-filtering' if not rows else 'mixed-separated-cohorts' if len(included_flags) > 1 else 'synthetic' if included_flags == {True} else 'real-or-unmarked'
    orientation = Counter(r['orientation'] for r in decisions if r['orientation_eligible'])
    gains = [row['gain_points_8'] for row in sessions if row['paired_complete']]
    # A shared instrument version does not make different question banks comparable.
    one_version = len({(row['instrument_version'], row['instrument_hash'], row['content_version'],
                       row['bank_hash'], row['administration_versions']) for row in sessions}) <= 1 and len(included_flags) <= 1
    summary = {
        'synthetic': included_flags == {True}, 'dataset_label': label,
        'include_synthetic': include_synthetic, 'synthetic_events_excluded': skipped_synthetic,
        'input_events': len(events), 'unique_events': len(unique), 'deduplicated_events': duplicates,
        'duplicate_submissions_deduplicated': duplicate_submissions,
        'sessions': len(sessions), 'completed_evaluations': sum(row['evaluation_complete'] for row in sessions),
        'analysed_evaluations': len({response_key(row) for row in rating_rows if row['analysis_included']}),
        'analysed_unique_participants': len({row['research_participant_id'] for row in rating_rows if row['analysis_included']}),
        'paired_complete_sessions': len(gains),
        'mean_paired_gain_points_8': mean(gains) if gains and one_version else None,
        'median_paired_gain_points_8': median(gains) if gains and one_version else None,
        'post_test_not_administered_sessions': sum(r['post_test_status'] == 'not_administered' for r in sessions),
        'orientation_denominator': sum(orientation.values()), 'in_game_orientation_counts': dict(orientation),
        'abstention_ballots': sum(r['abstention'] for r in decisions),
        'excluded_observation_decisions': sum(r['observation'] and r['stance'] == 'vote' for r in decisions),
        'case_knowledge_responses': len(case_knowledge),
        'cohorts': cohort_summaries, 'ratings_by_item': item_summaries,
        'dimensions_descriptive': dimension_summaries,
        'knowledge_missing_data': 'Mid-campaign evaluation has no post-test. Missing/N/A are never zero. Eight actual answers in both administrations are required for a descriptive score difference.',
        'interpretation': 'Synthetic records are technical examples, never participant evidence. Real records support a descriptive pilot, not validated ideology, causal participation change, comparative efficacy or a validated Greek MEEGA+ total score.',
    }
    quality = {
        'errors': errors, 'warnings': warnings, 'contextual_replays_deduplicated': contextual_replays,
        'no_imputation': True, 'auth_uid_exported': False, 'email_exported': False,
        'raw_source_ref_exported': False, 'synthetic': summary['synthetic'],
        'synthetic_events_excluded': skipped_synthetic, 'duplicate_submissions_deduplicated': duplicate_submissions,
        'descriptive_only': True, 'group_versions_separately': True,
    }
    # Paired statements are separate from behavioral choices and MEEGA+ dimensions.
    # No ideology/psychometric total is inferred from these custom, possibly placeholder items.
    last_tendencies = {}
    for row in sorted(tendency_rows, key=lambda r: (r['timestamp_utc'], r['_order'])):
        key = tuple(row.get(k, '') for k in ('research_participant_id','session','instrument_version','instrument_hash','content_version','bank_hash','synthetic','tendency_version','item_id','stage'))
        last_tendencies[key] = row
    tendency_rows = list(last_tendencies.values())
    paired = {}
    for row in tendency_rows:
        key = tuple(row.get(k, '') for k in ('research_participant_id','session','instrument_version','instrument_hash','content_version','bank_hash','synthetic','tendency_version','item_id'))
        pair = paired.setdefault(key, {k:v for k,v in row.items() if k not in ('stage','raw_answer','coded_answer','_order')})
        pair[row['stage']+'_raw_answer'] = row['raw_answer']
        pair[row['stage']+'_coded_answer'] = row['coded_answer']
    for row in paired.values():
        row['paired_complete'] = 'pre_raw_answer' in row and 'post_raw_answer' in row
        row['change'] = row['post_coded_answer']-row['pre_coded_answer'] if row['paired_complete'] else ''
    write_csv(out/'Tendencies-Responses.csv', [{k:v for k,v in r.items() if k!='_order'} for r in tendency_rows], ['research_participant_id','session','item_id','stage','raw_answer','instrument_status'])
    write_csv(out/'Tendencies-Paired.csv', list(paired.values()), ['research_participant_id','session','item_id','pre_raw_answer','post_raw_answer','change','paired_complete'])
    summary['tendency_responses'] = len(tendency_rows)
    summary['paired_tendency_items'] = sum(r['paired_complete'] for r in paired.values())
    latest_demographics = {}
    for row in sorted(demographic_rows,key=lambda r:(r['timestamp_utc'],r['_order'])):
        key=tuple(row.get(k,'') for k in ('research_participant_id','session','instrument_version','instrument_hash','content_version','bank_hash','synthetic','demographic_version','item_id'))
        latest_demographics[key]=row
    write_csv(out/'Demographics.csv',[{k:v for k,v in r.items() if k!='_order'} for r in latest_demographics.values()],['research_participant_id','session','item_id','option_id','value','demographic_version','not_disclosed'])
    summary['demographic_responses']=len(latest_demographics)
    for filename, value in (('Summary.json', summary), ('Quality.json', quality)):
        (out / filename).write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')
    return summary, quality


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--content', action='append', help='Frozen EvaluationContent.json; repeat for distinct versions')
    parser.add_argument('--include-synthetic', action='store_true', help='Explicitly include labeled synthetic QA records in distinct cohorts')
    args = parser.parse_args(argv)
    summary, quality = analyse(args.input, args.output, args.content or P / 'EvaluationContent.json',
                               include_synthetic=args.include_synthetic)
    print(json.dumps({'sessions': summary['sessions'], 'completed': summary['completed_evaluations'],
                      'errors': len(quality['errors']), 'dataset_label': summary['dataset_label'],
                      'synthetic_excluded': summary['synthetic_events_excluded'],
                      'output': str(Path(args.output).resolve())}, ensure_ascii=False))
    return 1 if quality['errors'] else 0


if __name__ == '__main__':
    raise SystemExit(main())
