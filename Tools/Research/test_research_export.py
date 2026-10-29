"""Offline integration tests: actual event envelope -> tidy CSV -> analysis.

All fixtures are synthetic technical records, never human research observations.
No production Firebase requests or credentials are used.
"""
import csv
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

from export_evaluation import analyse, P
from pull_firestore_events import collect, minimise_metadata

VERSION = 'first-prytany-meega-adapted-el-1'
NEW_CONTENT = {
    'instrumentVersion': VERSION, 'onboarding': [], 'postTest': [],
    'experience': [
        {'id': 'meega-01', 'construct': 'aesthetics', 'group': 'usability', 'officialItemId': '1', 'prompt': 'Γραφικά'},
        {'id': 'meega-29', 'construct': 'relevance', 'group': 'player-experience', 'officialItemId': '29',
         'prompt': 'Σύγκριση', 'allowNotApplicable': True},
        {'id': 'ux-agency', 'construct': 'agency', 'group': 'game-specific', 'prompt': 'Επιρροή'},
    ],
    'participationIntention': [{'id': 'intent-understand', 'construct': 'participation-intention', 'prompt': 'Πρόθεση'}],
}


def event(kind, item='', answer=-1, *, event_id=None, participant='person-1', session='run-1',
          version=VERSION, submission='submission-1', synthetic=True, second=0, point=None, **extra):
    point = point or {'prytany': 2, 'week': 1, 'turn': 5, 'completedCases': 5,
                      'caseId': 'case-6', 'contentVersion': 'bank-1', 'activeSeconds': 1500}
    meta = {'instrumentVersion': version, 'researchParticipantId': participant, 'researchConsent': True,
            'assessmentId': session + ':experience', 'submissionId': submission, 'stage': 'experience',
            'administrationVersion': 'meega-experience-v1', 'postTestStatus': 'not_administered',
            'bank': 'bank-1', 'bankHash': 'bank-hash', 'point': point, 'synthetic': synthetic, **extra}
    if item:
        meta['itemId'] = item
    return {'type': kind, 'eventId': event_id or kind + '-' + item + '-' + str(second), 'session': session,
            'nodeId': item, 'answer': answer, 't': f'2026-10-20T10:00:{second:02d}Z',
            'sourceRef': json.dumps(meta, ensure_ascii=False)}


def complete_events(**kwargs):
    return [event('evaluation_start', second=0, **kwargs),
            event('experience_rating', 'meega-01', 5, second=1, **kwargs),
            event('experience_rating', 'meega-29', 0, second=2, **kwargs),
            event('experience_rating', 'ux-agency', 3, second=3, **kwargs),
            event('participation_intention', 'intent-understand', 1, second=4, **kwargs),
            event('evaluation_complete', second=5, **kwargs)]


class ExportTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.content = self.root / 'Content.json'
        self.content.write_text(json.dumps(NEW_CONTENT, ensure_ascii=False), encoding='utf-8')

    def run_export(self, events, *, include_synthetic=True, envelope=False):
        source = self.root / 'events.json'
        source.write_text(json.dumps({'synthetic': envelope, 'events': events}), encoding='utf-8')
        self.out = self.root / 'CSV'
        return analyse(source, self.out, self.content, include_synthetic=include_synthetic)

    def rows(self, name):
        with (self.out / (name + '.csv')).open(encoding='utf-8-sig', newline='') as stream:
            return list(csv.DictReader(stream))

    def test_new_roundtrip_raw_normalized_na_and_metadata(self):
        summary, quality = self.run_export(complete_events())
        self.assertEqual([], quality['errors'])
        self.assertEqual(1, summary['analysed_evaluations'])
        ratings = {r['item_id']: r for r in self.rows('ratings')}
        self.assertEqual('2', ratings['meega-01']['normalized_minus2_plus2'])
        self.assertEqual('0', ratings['ux-agency']['normalized_minus2_plus2'])
        self.assertEqual('', ratings['meega-29']['rating'])
        self.assertEqual('not_applicable', ratings['meega-29']['response_status'])
        self.assertEqual('0', ratings['meega-29']['raw_answer'])
        self.assertEqual('29', ratings['meega-29']['official_item_id'])
        self.assertEqual('participation-intention', ratings['intent-understand']['item_group'])
        self.assertEqual('5', ratings['meega-01']['completed_cases'])
        self.assertEqual('submission-1', ratings['meega-01']['assessment_id'])
        session = self.rows('sessions')[0]
        self.assertEqual('', session['post_score_8'])
        self.assertEqual('False', session['paired_complete'])

    def test_synthetic_default_exclusion_and_mixed_cohorts(self):
        events = complete_events() + complete_events(participant='real-1', session='real-run', synthetic=False)
        summary, quality = self.run_export(events, include_synthetic=False)
        self.assertEqual(6, summary['synthetic_events_excluded'])
        self.assertEqual(1, summary['sessions'])
        self.assertTrue(all(row['synthetic'] == 'False' for row in self.rows('ratings')))
        summary, quality = self.run_export(events)
        self.assertEqual('mixed-separated-cohorts', summary['dataset_label'])
        self.assertEqual(2, len(summary['cohorts']))
        self.assertTrue(all(row['n'] <= 1 for row in summary['ratings_by_item']))

    def test_envelope_synthetic_cannot_be_overridden_by_event(self):
        summary, quality = self.run_export(complete_events(synthetic=False), envelope=True, include_synthetic=False)
        self.assertEqual(0, summary['sessions'])
        self.assertEqual(6, summary['synthetic_events_excluded'])

    def test_replay_and_duplicate_submit_do_not_double_count(self):
        events = complete_events()
        events.append(events[1].copy())
        events.append(event('evaluation_complete', second=6, event_id='retry-submit'))
        summary, quality = self.run_export(events)
        self.assertEqual([], quality['errors'])
        self.assertEqual(1, summary['deduplicated_events'])
        self.assertEqual(1, summary['duplicate_submissions_deduplicated'])
        self.assertEqual(1, summary['analysed_evaluations'])
        self.assertEqual(4, len(self.rows('ratings')))

    def test_restarted_game_same_person_is_not_second_participant(self):
        events = complete_events() + complete_events(session='run-2', submission='submission-2')
        summary, quality = self.run_export(events)
        self.assertEqual(2, summary['sessions'])
        self.assertEqual(1, summary['analysed_evaluations'])
        self.assertEqual(1, summary['analysed_unique_participants'])
        self.assertEqual(4, sum(row['analysis_included'] == 'True' for row in self.rows('ratings')))

    def test_answer_change_last_response_is_retained(self):
        events = complete_events()
        events.insert(-1, event('experience_rating', 'meega-01', 2, second=4, event_id='changed'))
        summary, quality = self.run_export(events)
        row = next(r for r in self.rows('ratings') if r['item_id'] == 'meega-01')
        self.assertEqual('2', row['rating'])
        self.assertEqual('-1', row['normalized_minus2_plus2'])

    def test_same_timestamp_uses_persisted_event_sequence(self):
        records = complete_events()
        records += [event('experience_rating', 'meega-01', 1, second=3, event_id='sequence-3', eventSequence=3),
                    event('experience_rating', 'meega-01', 2, second=3, event_id='sequence-2', eventSequence=2)]
        summary, quality = self.run_export(records)
        row = next(r for r in self.rows('ratings') if r['item_id'] == 'meega-01')
        self.assertEqual('1', row['rating'])

    def test_same_version_different_instrument_hash_is_separate(self):
        alternate = self.root / 'Alternate.json'
        alternate.write_text(json.dumps({**NEW_CONTENT, 'language': 'alternate-text'}), encoding='utf-8')
        original_hash = hashlib.sha256(self.content.read_bytes()).hexdigest()[:32]
        alternate_hash = hashlib.sha256(alternate.read_bytes()).hexdigest()[:32]
        records = complete_events(instrumentHash=original_hash) + complete_events(instrumentHash=alternate_hash)
        source = self.root / 'events.json'
        source.write_text(json.dumps({'events': records}), encoding='utf-8')
        self.out = self.root / 'CSV'
        summary, quality = analyse(source, self.out, [self.content, alternate], include_synthetic=True)
        self.assertEqual([], quality['errors'])
        self.assertEqual(2, len(summary['cohorts']))
        self.assertEqual(2, summary['analysed_evaluations'])
        self.assertEqual(8, len(self.rows('ratings')))

    def test_missing_partial_is_preserved_not_zero_not_in_summary(self):
        events = [event('evaluation_start'), event('experience_rating', 'meega-01', 3, second=1),
                  event('experience_rating', 'ux-agency', -1, second=2)]
        summary, quality = self.run_export(events)
        self.assertEqual([], quality['errors'])
        self.assertEqual(0, summary['analysed_evaluations'])
        missing = next(row for row in self.rows('ratings') if row['item_id'] == 'ux-agency')
        self.assertEqual('missing', missing['response_status'])
        self.assertEqual('', missing['rating'])
        self.assertTrue(all(item['n'] == 0 for item in summary['ratings_by_item']))

    def test_false_completion_incomplete_items_is_rejected(self):
        events = [event('experience_rating', 'meega-01', 5), event('evaluation_complete', second=2)]
        summary, quality = self.run_export(events)
        self.assertEqual(0, summary['analysed_evaluations'])
        self.assertEqual('completed-submission-has-missing-items', quality['errors'][0]['kind'])

    def test_na_not_allowed_is_error(self):
        summary, quality = self.run_export([event('experience_rating', 'meega-01', 0)])
        self.assertEqual('not-applicable-not-allowed', quality['errors'][0]['kind'])

    def test_case_question_correctness_and_final_vote_are_exported(self):
        first = event('case_choice', answer=0, second=1, caseId='case-1', collectiveOutcome=0)
        first.update(stance='vote', orientation='democracy')
        last = event('case_choice', answer=1, second=3, caseId='case-1', collectiveOutcome=1)
        last.update(stance='vote', orientation='oligarchy')
        question = event('case_knowledge', 'case-1-question', answer=2, second=2, caseId='case-1', correctIndex=2)
        question['correct'] = 1
        summary, quality = self.run_export([first, last, question])
        self.assertEqual([], quality['errors'])
        self.assertEqual(1, summary['orientation_denominator'])
        self.assertEqual({'oligarchy': 1}, summary['in_game_orientation_counts'])
        self.assertEqual(1, summary['case_knowledge_responses'])
        self.assertEqual('1', self.rows('case_knowledge')[0]['correct'])
        self.assertEqual(1, len(self.rows('decisions')))

    def test_observation_is_not_counted_as_political_vote(self):
        observed = event('case_choice', answer=0, caseId='court-1', collectiveOutcome=0, observation=True)
        observed.update(stance='vote', orientation='democracy')
        summary, quality = self.run_export([observed])
        self.assertEqual(0, summary['orientation_denominator'])
        self.assertEqual(1, summary['excluded_observation_decisions'])

    def test_state_snapshots_and_decision_provenance_remain_queryable(self):
        row = event('case_choice', answer=0, caseId='case-1', collectiveOutcome=1,
                    reason='Historical institutional rationale', reference='Aristotle 43.4',
                    citySnapshot={'food': 61, 'trust': -4, 'readiness': 8, 'war': -2},
                    political={'metricsBefore': {'ethics': 44}, 'supportTotal': 48})
        row.update(stance='vote', orientation='democracy', treasury=800, personalTreasury=12,
                   consistency=51, ethics=45, authority=42, persuasion=46, parrhesia=44, recognition=49)
        summary, quality = self.run_export([row])
        self.assertEqual([], quality['errors'])
        for table in ('events', 'decisions'):
            result = self.rows(table)[0]
            self.assertEqual('800', result['state_treasury'])
            self.assertEqual('45', result['state_ethics'])
            self.assertEqual('-4', result['state_city_trust'])
        decision = self.rows('decisions')[0]
        self.assertEqual('Historical institutional rationale', decision['orientation_reason'])
        self.assertEqual('Aristotle 43.4', decision['source_ref'])
        self.assertEqual('44', decision['political_metric_before_ethics'])
        self.assertEqual('False', decision['player_choice_prevailed'])

    def test_older_packets_do_not_impute_city_state_or_character_metrics(self):
        self.run_export([event('choice')])
        result = self.rows('events')[0]
        self.assertEqual('', result['state_city_food'])
        self.assertEqual('', result['state_ethics'])

    def test_same_case_id_in_later_week_is_not_lost_as_duplicate(self):
        records = []
        for turn in (0, 25):
            point = {'turn': turn, 'prytany': turn // 5 + 1, 'week': turn % 5 + 1,
                     'completedCases': turn + 1, 'caseId': 'repeated-case', 'contentVersion': 'bank-1'}
            ballot = event('case_choice', answer=turn % 2, second=turn, caseId='repeated-case',
                           collectiveOutcome=1, point=point)
            ballot.update(stance='vote', orientation='mixed')
            question = event('case_knowledge', 'repeated-case:knowledge', answer=0,
                             second=turn + 1, caseId='repeated-case', correctIndex=0, point=point)
            records.extend([ballot, question])
        summary, quality = self.run_export(records)
        self.assertEqual([], quality['errors'])
        self.assertEqual(2, len(self.rows('decisions')))
        self.assertEqual(2, len(self.rows('case_knowledge')))
        self.assertEqual({'0', '25'}, {r['turn'] for r in self.rows('decisions')})

    def test_paired_gain_not_pooled_across_different_question_bank_hashes(self):
        actual = json.loads((P / 'EvaluationContent.json').read_text(encoding='utf-8-sig'))
        self.content.write_text(json.dumps(actual, ensure_ascii=False), encoding='utf-8')
        records = []
        for run, bank_hash in (('run-a', 'frozen-bank-a'), ('run-b', 'frozen-bank-b')):
            for stage, questions in (('pre', actual['onboarding']), ('post', actual['postTest'])):
                for index, question in enumerate(questions):
                    record = event('initial_knowledge' if stage == 'pre' else 'final_knowledge',
                                   answer=question['correctIndex'], participant=run, session=run,
                                   stage=stage, questionId=question['id'], bankHash=bank_hash,
                                   assessmentId=run + ':' + stage, second=index)
                    records.append(record)
        summary, quality = self.run_export(records)
        self.assertEqual(2, summary['paired_complete_sessions'])
        self.assertIsNone(summary['mean_paired_gain_points_8'])

    def test_legacy14_remains_mapped_and_versions_never_pool(self):
        legacy = json.loads((P / 'EvaluationContent.legacy.json').read_text(encoding='utf-8-sig'))
        old = []
        for i, item in enumerate(legacy['experience'] + legacy['participationIntention']):
            kind = 'experience_rating' if item in legacy['experience'] else 'participation_intention'
            old.append(event(kind, item['id'], 4, version=legacy['instrumentVersion'], session='legacy-run',
                             participant='legacy-person', submission='legacy-submit', second=i))
        old.append(event('evaluation_complete', version=legacy['instrumentVersion'], session='legacy-run',
                         participant='legacy-person', submission='legacy-submit', second=20))
        summary, quality = self.run_export(complete_events() + old)
        self.assertEqual([], quality['errors'])
        self.assertEqual(18, len(self.rows('ratings')))
        self.assertEqual(2, len(summary['cohorts']))
        self.assertEqual(2, summary['analysed_evaluations'])
        self.assertTrue(all(item['n'] <= 1 for item in summary['ratings_by_item']))

    def test_actual_full36_instrument_roundtrip(self):
        actual = json.loads((P / 'EvaluationContent.json').read_text(encoding='utf-8-sig'))
        self.content.write_text(json.dumps(actual, ensure_ascii=False), encoding='utf-8')
        records = [event('evaluation_start')]
        for index, item in enumerate(actual['experience'] + actual['participationIntention']):
            kind = 'experience_rating' if index < len(actual['experience']) else 'participation_intention'
            records.append(event(kind, item['id'], 0 if item.get('allowNotApplicable') else index % 5 + 1, second=index + 1))
        records.append(event('evaluation_complete', second=45))
        summary, quality = self.run_export(records)
        self.assertEqual([], quality['errors'])
        self.assertEqual(36, len(self.rows('ratings')))
        self.assertEqual(34, sum(row['response_status'] == 'answered' for row in self.rows('ratings')))
        self.assertEqual(2, sum(row['response_status'] == 'not_applicable' for row in self.rows('ratings')))
        self.assertEqual(1, summary['analysed_evaluations'])
        self.assertEqual(5, len({row['item_group'] for row in summary['ratings_by_item']}))

    def test_knowledge_missing_post_never_becomes_zero_or_gain(self):
        legacy = json.loads((P / 'EvaluationContent.legacy.json').read_text(encoding='utf-8-sig'))
        records = []
        for index, question in enumerate(legacy['onboarding']):
            record = event('initial_knowledge', question['id'], question['correctIndex'], stage='pre',
                           version=legacy['instrumentVersion'], second=index)
            records.append(record)
        summary, quality = self.run_export(records)
        session = self.rows('sessions')[0]
        self.assertEqual('8', session['pre_score_8'])
        self.assertEqual('', session['post_score_8'])
        self.assertEqual('', session['gain_points_8'])
        self.assertIsNone(summary['mean_paired_gain_points_8'])

    def test_three_feedback_fields_are_kept_separately(self):
        """Δυνατά / αδύνατα σημεία / σχόλια: τρεις γραμμές, όχι μία (παλιά κρατιόταν μόνο το τελευταίο σχόλιο)."""
        records = complete_events() + [
            event('evaluation_feedback', second=6, feedback='Οι δίκες', feedbackKind='strengths'),
            event('evaluation_feedback', second=7, feedback='Πολύ κείμενο', feedbackKind='weaknesses'),
            event('evaluation_feedback', second=8, feedback='Περισσότερες δίκες', feedbackKind='comments'),
            event('evaluation_feedback', second=9, feedback='Παλιό σχόλιο', submission='submission-0')]
        self.run_export(records)
        rows = {r['feedback_kind']: r['feedback'] for r in self.rows('feedback')}
        self.assertEqual({'strengths': 'Οι δίκες', 'weaknesses': 'Πολύ κείμενο', 'comments': 'Περισσότερες δίκες', 'open': 'Παλιό σχόλιο'}, rows)

    def test_feedback_redacts_email_and_escapes_spreadsheet_formula(self):
        records = complete_events() + [event('evaluation_feedback', second=6, feedback='=MAIL participant@example.org')]
        summary, quality = self.run_export(records)
        row = self.rows('feedback')[0]
        self.assertNotIn('participant@example.org', row['feedback'])
        self.assertIn('[redacted-email]', row['feedback'])
        self.assertTrue(row['feedback'].startswith("'="))
        for file in self.out.glob('*'):
            self.assertNotIn('participant@example.org', file.read_text(encoding='utf-8-sig'))

    def test_future_unmapped_version_warns_does_not_reuse_positions(self):
        summary, quality = self.run_export([event('experience_rating', 'meega-01', 4, version='unknown-future')])
        self.assertEqual([], self.rows('ratings'))
        self.assertTrue(any(row['kind'] == 'unknown-instrument-version' for row in quality['warnings']))

    def test_report_uses_same_pipeline_and_labels_synthetic_every_figure(self):
        try:
            import matplotlib
        except ImportError:
            self.skipTest('matplotlib is optional for CSV-only installations')
        from analyse_evaluation import run_report, SYNTHETIC_LABEL
        source = self.root / 'events.json'
        source.write_text(json.dumps({'events': complete_events()}), encoding='utf-8')
        result = run_report(source, self.root / 'report', self.content, include_synthetic=True)
        manifest = json.loads(Path(result['manifest']).read_text(encoding='utf-8'))
        self.assertTrue(manifest['synthetic'])
        self.assertTrue(manifest['figures'])
        self.assertTrue(all(row['synthetic'] and row['label'] == SYNTHETIC_LABEL for row in manifest['figures']))
        self.assertIn(SYNTHETIC_LABEL, Path(result['report']).read_text(encoding='utf-8'))


class PullPrivacyTests(unittest.TestCase):
    def test_question_and_signed_city_snapshot_preserved(self):
        result = minimise_metadata({'question': 'Question text',
                                    'citySnapshot': {'food': 62, 'trust': -3, 'readiness': 4, 'war': -1, 'email': 'private'},
                                    'political': {'metricsBefore': {'ethics': 41}}})
        self.assertEqual('Question text', result['question'])
        self.assertEqual(-3, result['citySnapshot']['trust'])
        self.assertNotIn('email', result['citySnapshot'])
        self.assertEqual(41, result['political']['metricsBefore']['ethics'])

    def test_snapshot_pagination_keeps_one_read_time_and_advances_cursor(self):
        study = {'instrumentVersion': VERSION, 'bankVersion': '1', 'bankHash': 'hash'}
        prefix = 'projects/demo/databases/(default)/documents'
        meta = {'researchConsent': True, 'instrumentVersion': VERSION, 'researchParticipantId': 'a' * 32,
                'bank': '1', 'bankHash': 'hash', 'synthetic': False}
        def document(number):
            event_id = str(number) * 32
            return {'document': {'name': prefix + '/participants/private-auth/events/' + event_id,
                                  'fields': {'eventId': {'stringValue': event_id}, 'session': {'stringValue': 'b' * 32},
                                             't': {'stringValue': '2026-10-20T10:00:00Z'},
                                             'sourceRef': {'stringValue': json.dumps(meta)}}}}
        class Reader:
            parent = prefix
            calls = []
            def query(self, query, read_time=None):
                self.calls.append((query, read_time))
                if query['limit'] == 0:
                    return [{'readTime': '2026-10-20T12:00:00.123456789Z'}]
                if 'startAt' not in query:
                    return [document(1), document(2)]
                return [document(3)]
        reader = Reader()
        records, counts = collect(reader, study, page_size=2)
        self.assertEqual(3, len(records))
        self.assertEqual(2, counts['pages'])
        self.assertEqual('2026-10-20T12:00:00.123456Z', reader.calls[1][1])
        self.assertEqual(reader.calls[1][1], reader.calls[2][1])
        self.assertFalse(reader.calls[2][0]['startAt']['before'])
        self.assertTrue(reader.calls[2][0]['startAt']['values'][0]['referenceValue'].endswith('/' + '2' * 32))
        self.assertNotIn('private-auth', json.dumps(records))

    def test_new_metadata_preserved_without_email_or_account_uid(self):
        result = minimise_metadata({'submissionId': 'sub-1', 'itemId': 'meega-29', 'group': 'player-experience',
                                    'officialItemId': '29', 'allowNotApplicable': True,
                                    'feedback': 'Contact participant@example.org', 'email': 'secret@example.org',
                                    'uid': 'account-secret', 'synthetic': True})
        self.assertEqual('sub-1', result['submissionId'])
        self.assertEqual('29', result['officialItemId'])
        self.assertNotIn('uid', result)
        self.assertNotIn('email', result)
        self.assertNotIn('participant@example.org', result['feedback'])

    def test_pull_default_excludes_synthetic_before_export(self):
        participant, session, event_id = '1' * 32, '2' * 32, '3' * 32
        meta = {'researchConsent': True, 'instrumentVersion': VERSION, 'researchParticipantId': participant,
                'bank': '1', 'bankHash': 'hash', 'synthetic': True}
        document = {'name': 'projects/demo/databases/(default)/documents/participants/auth-uid/events/' + event_id,
                    'fields': {'eventId': {'stringValue': event_id}, 'session': {'stringValue': session},
                               't': {'stringValue': '2026-10-20T10:00:00Z'},
                               'sourceRef': {'stringValue': json.dumps(meta)}}}
        class Reader:
            parent = 'projects/demo/databases/(default)/documents'
            def query(self, query, read_time=None):
                return [{'readTime': '2026-10-20T12:00:00Z'}] if query['limit'] == 0 else [{'document': document}]
        study = {'instrumentVersion': VERSION, 'bankVersion': '1', 'bankHash': 'hash'}
        records, counts = collect(Reader(), study)
        self.assertEqual([], records)
        self.assertEqual(1, counts['skipped']['synthetic-excluded'])
        records, counts = collect(Reader(), study, include_synthetic=True)
        self.assertEqual(1, len(records))
        self.assertNotIn('auth-uid', json.dumps(records))


class AgoraExportTests(unittest.TestCase):
    """Βήμα 9: τα γεγονότα του παιχνιδιού (ψηφοφορία, δίκη, πρυτανεία, τέλος) γίνονται πίνακες."""
    setUp = ExportTests.setUp
    run_export = ExportTests.run_export
    rows = ExportTests.rows

    def agora_event(self, name, second, **data):
        meta = {'researchParticipantId': 'person-1', 'researchConsent': True, 'instrumentVersion': VERSION,
                'bank': 'bank-1', 'bankHash': 'bank-hash', 'synthetic': True, 'knowledgeFormOrder': 'AB',
                'eventSequence': second, 'agoraEvent': name, 'campaignLayout': {'casesPerPrytany': 2, 'totalCases': 20}, 'data': {'runId': 'run-1', 'case': 5, 'prytany': 3, 'week': 1, **data}}
        return {'type': 'agora_' + name, 'eventId': 'agora-' + name + '-' + str(second), 'session': 'run-1',
                'nodeId': 'case-5', 'answer': -1, 'prytany': 3, 'week': 1, 't': f'2026-10-20T11:00:{second:02d}Z',
                'sourceRef': json.dumps(meta, ensure_ascii=False)}

    def test_agora_votes_trials_prytanies_and_endings_are_tables(self):
        """Βήμα 9: κάθε γραμμή της ανάλυσης σε στήλη· οι 9 δείκτες ανά πρυτανεία· τα τέλη με knowledgeFormOrder."""
        events = [self.agora_event('case_result', 1, total=61, passedA=True, vote='A', spokeFor='A',
                                   lines='Base:55,Pressure:-5,Speech:6,Mood:5', mood=5),
                  self.agora_event('trial', 2, accuser=True, total=58, guilty=True, lines='Evidence:58,Accusation:6,Mood:-6'),
                  self.agora_event('prytany_end', 3, peitho=36, fame=18, ethos=57, wallet=210, T=52, Ep=47, Et=33, Em=61, ID=50),
                  self.agora_event('game_end', 4, cityEnding='GarrisonOnly', personalEnding='Demades', knowledgeFormOrder='AB')]
        summary, quality = self.run_export(events)
        self.assertEqual([], quality['errors'])
        vote = self.rows('agora_votes')[0]
        self.assertEqual(('55', '-5', '6', '5'), (vote['line_Base'], vote['line_Pressure'], vote['line_Speech'], vote['line_Mood']))
        self.assertEqual(('61', 'True', 'A', '5'), (vote['total'], vote['passedA'], vote['vote'], vote['case_id']))
        trial = self.rows('agora_trials')[0]
        self.assertEqual(('58', '6', 'True'), (trial['line_Evidence'], trial['line_Accusation'], trial['guilty']))
        prytany = self.rows('agora_prytanies')[0]
        self.assertEqual(['52', '47', '33', '61', '50', '36', '18', '57', '210'],
                         [prytany[k] for k in ('T', 'Ep', 'Et', 'Em', 'ID', 'peitho', 'fame', 'ethos', 'wallet')])
        ending = self.rows('agora_endings')[0]
        self.assertEqual(('GarrisonOnly', 'Demades', 'AB'), (ending['cityEnding'], ending['personalEnding'], ending['knowledgeFormOrder']))
        self.assertEqual(1, summary['agora_votes_rows'])
        self.assertEqual('4', vote['turn'], 'πρυτανεία 3, εβδομάδα 1, 2 υποθέσεις ανά πρυτανεία')
        self.assertNotIn('runId', vote, 'το runId είναι ήδη η στήλη session')

    def test_broken_analysis_lines_are_an_error(self):
        _, quality = self.run_export([self.agora_event('case_result', 1, total=50, lines='Base:fifty')])
        self.assertIn('invalid-analysis-lines', [e['kind'] for e in quality['errors']])

    def test_pull_keeps_agora_data_and_shown_order(self):
        result = minimise_metadata({'shownOrder': 'c,a,d,b', 'knowledgeFormOrder': 'AB', 'agoraEvent': 'case_result',
                                    'data': {'total': 61, 'lines': 'Base:55', 'unknown': 'x', 'nested': {'a': 1}}})
        self.assertEqual('c,a,d,b', result['shownOrder'])
        self.assertEqual('AB', result['knowledgeFormOrder'])
        self.assertEqual({'total': 61, 'lines': 'Base:55'}, result['data'])


if __name__ == '__main__':
    unittest.main(verbosity=2)
