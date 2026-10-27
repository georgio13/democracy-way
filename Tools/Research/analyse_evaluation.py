"""Reproducible descriptive tables and figures from the same real or QA pipeline.

Never generates participants or infers educational effectiveness. For synthetic
records --include-synthetic is required; every output cohort is clearly labeled.
"""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict
import csv
from datetime import datetime, timezone
import hashlib
from html import escape
import json
from pathlib import Path

from export_evaluation import analyse, P

SYNTHETIC_LABEL = 'ΣΥΝΘΕΤΙΚΑ ΔΕΔΟΜΕΝΑ — ΤΕΧΝΙΚΗ ΠΡΟΣΟΜΟΙΩΣΗ'
REAL_LABEL = 'ΠΕΡΙΓΡΑΦΙΚΗ ΠΙΛΟΤΙΚΗ ΑΝΑΛΥΣΗ — ΕΛΕΓΧΟΣ ΠΡΟΕΛΕΥΣΗΣ ΑΠΑΡΑΙΤΗΤΟΣ'
GROUP_NAMES = {
    'usability': 'Ευχρηστία · προσαρμοσμένα στοιχεία MEEGA+',
    'player-experience': 'Εμπειρία παίκτη · προσαρμοσμένα στοιχεία MEEGA+',
    'learning-goals': 'Ειδικοί μαθησιακοί στόχοι · αντιλαμβανόμενη μάθηση',
    'game-specific': 'Ειδικοί μηχανισμοί παιχνιδιού · συμπληρωματικά στοιχεία',
    'custom': 'Συμπληρωματικά στοιχεία παιχνιδιού',
    'participation-intention': 'Δηλωμένη πρόθεση συμμετοχής · συμπληρωματικά στοιχεία',
    'legacy-adapted': 'Προγενέστερο πρωτότυπο ερωτηματολόγιο',
}


def number(value, digits=2):
    return '—' if value is None or value == '' else f'{float(value):.{digits}f}'.rstrip('0').rstrip('.')


def source_digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def load_csv(path):
    with Path(path).open(encoding='utf-8-sig', newline='') as stream:
        return list(csv.DictReader(stream))


def table_html(title, label, headers, rows):
    return ('<h3>' + escape(title) + '</h3><p class="data-label">' + escape(label) + '</p><table><thead><tr>' +
            ''.join('<th>' + escape(str(header)) + '</th>' for header in headers) + '</tr></thead><tbody>' +
            ''.join('<tr>' + ''.join('<td>' + escape(str(cell)) + '</td>' for cell in row) + '</tr>' for row in rows) +
            '</tbody></table>')


def plot_group(items, title, label, destination):
    # Standard plotting library: PNG for Word and PDF for print. No normative score.
    import matplotlib
    matplotlib.use('Agg')
    from matplotlib import pyplot as plt
    import textwrap
    plt.rcParams.update({'font.family': 'DejaVu Sans', 'font.size': 10})
    height = max(4.2, .88 * len(items) + 2.2)
    figure, axis = plt.subplots(figsize=(12.8, height))
    colors = ('#8b4a44', '#bf8873', '#d9d1b9', '#82aaa0', '#2c7069')
    y = list(range(len(items)))
    left = [0.0] * len(items)
    for rating, color in enumerate(colors, 1):
        widths = [item.get(f'rating_{rating}_percent') or 0 for item in items]
        axis.barh(y, widths, left=left, color=color, label=str(rating), height=.64)
        for index, width in enumerate(widths):
            if width >= 12:
                axis.text(left[index] + width / 2, index, f'{width:.0f}%', va='center', ha='center',
                          color='white' if rating in (1, 5) else '#172e31', fontsize=9)
        left = [start + width for start, width in zip(left, widths)]
    labels = ['\n'.join(textwrap.wrap(item['item_id'] + ' · ' + item.get('prompt', ''), 62)) +
              f"\n(n={item['n']}, Δ/Ε={item['n_not_applicable']})" for item in items]
    axis.set_yticks(y, labels)
    axis.invert_yaxis()
    axis.set_xlim(0, 100)
    axis.set_xlabel('Ποσοστό έγκυρων απαντήσεων 1–5 (%) · Δ/Ε και ελλείπουσες εκτός παρονομαστή')
    axis.grid(axis='x', alpha=.16)
    axis.set_axisbelow(True)
    for spine in ('top', 'right', 'left'):
        axis.spines[spine].set_visible(False)
    figure.suptitle(label + '\n' + title, fontsize=12, color='#8b4a44' if items[0]['synthetic'] else '#172e31')
    handles, _ = axis.get_legend_handles_labels()
    figure.legend(handles, ['1 Διαφωνώ απόλυτα', '2 Διαφωνώ', '3 Ουδέτερη θέση', '4 Συμφωνώ', '5 Συμφωνώ απόλυτα'],
                  title='Κλίμακα συμφωνίας', ncols=5, loc='lower center', bbox_to_anchor=(.5, .045),
                  fontsize=8, title_fontsize=9)
    figure.text(.5, .01, 'Οι συνθετικές τιμές δεν αποτελούν ευρήματα πραγματικών συμμετεχόντων.' if items[0]['synthetic'] else
                'Περιγραφική παρουσίαση προσαρμοσμένου εργαλείου· χωρίς αιτιώδη ή ψυχομετρικό ισχυρισμό.',
                ha='center', fontsize=9)
    figure.subplots_adjust(left=.47, right=.98, top=1 - 1.05 / height, bottom=1.65 / height)
    figure.savefig(destination.with_suffix('.png'), dpi=180)
    figure.savefig(destination.with_suffix('.pdf'))
    plt.close(figure)


def run_report(input_events, output, content=P / 'EvaluationContent.json', *, include_synthetic=False):
    out = Path(output)
    out.mkdir(parents=True, exist_ok=True)
    exports = out / 'CSV'
    summary, quality = analyse(input_events, exports, content, include_synthetic=include_synthetic)
    figures = out / 'Figures'
    figures.mkdir(exist_ok=True)
    decision_rows = load_csv(exports / 'decisions.csv')
    rating_rows = load_csv(exports / 'ratings.csv')
    sections, figure_manifest = [], []
    for cohort in summary['cohorts']:
        cohort_id = cohort['cohort_id']
        label = SYNTHETIC_LABEL if cohort['synthetic'] else REAL_LABEL
        heading = f"{cohort['instrument_version']} · {cohort['evaluation_scope']} · {cohort_id}"
        sections.append('<section><h2>' + escape(heading) + '</h2><p class="data-label">' + escape(label) + '</p>')
        sections.append('<p>Ολοκληρωμένες και επιλεγμένες συνεδρίες: <strong>' + str(cohort['completed_sessions']) +
                        '</strong>. Κάθε ερευνητικός κωδικός συνεισφέρει την πρώτη έγκυρη ολοκλήρωση για την ίδια έκδοση και περιεχόμενο.</p>')
        sections.append('<p>Έκδοση χορήγησης: ' + escape(str(cohort['administration_version'])) +
                        ' · έκδοση περιεχομένου: ' + escape(str(cohort['content_version'])) +
                        ' · hash τράπεζας: ' + escape(str(cohort['bank_hash'])) + '</p>')
        items = [item for item in summary['ratings_by_item'] if item['cohort_id'] == cohort_id]
        groups = list(dict.fromkeys(item['item_group'] for item in items))
        for index, group in enumerate(groups):
            group_items = [item for item in items if item['item_group'] == group]
            group_title = GROUP_NAMES.get(group, group)
            sections.append(table_html(group_title, label,
                                      ['Κωδικός', 'Δήλωση', 'n', 'Δ/Ε', 'Ελλ.', 'Διάμεσος 1–5', 'Q1–Q3', 'Διάμεσος −2…+2'],
                                      [[item['item_id'], item['prompt'], item['n'], item['n_not_applicable'], item['n_missing'],
                                        number(item['median']), number(item['q1']) + '–' + number(item['q3']),
                                        number(item['normalized_median_minus2_plus2'])] for item in group_items]))
            if any(item['n'] for item in group_items):
                figure_path = figures / f'{cohort_id}-{index + 1:02d}'
                plot_group(group_items, group_title, label, figure_path)
                relative = figure_path.with_suffix('.png').relative_to(out).as_posix()
                sections.append('<figure><img src="' + relative + '" alt="' + escape(label + ' · ' + group_title) +
                                '"><figcaption>' + escape(label + ' · ' + group_title) + '</figcaption></figure>')
                figure_manifest.append({'cohort_id': cohort_id, 'synthetic': cohort['synthetic'], 'label': label,
                                        'group': group, 'png': relative,
                                        'pdf': figure_path.with_suffix('.pdf').relative_to(out).as_posix()})
        dimensions = [item for item in summary['dimensions_descriptive'] if item['cohort_id'] == cohort_id]
        sections.append(table_html('Περιγραφικές διαστάσεις — χωρίς συνολική επικυρωμένη βαθμολογία', label,
                                  ['Ομάδα', 'Διάσταση', 'Πλήθος στοιχείων', 'n', 'Διάμεσος ατομικών μέσων', 'Q1–Q3'],
                                  [[GROUP_NAMES.get(item['item_group'], item['item_group']), item['construct'], item['item_count'],
                                    item['n_participant_means'], number(item['median_participant_mean']),
                                    number(item['q1_participant_mean']) + '–' + number(item['q3_participant_mean'])] for item in dimensions]))
        cohort_people = {(row['research_participant_id'], row['session']): row['completed_cases'] for row in rating_rows
                         if row['cohort_id'] == cohort_id and row['analysis_included'] == 'True'}
        eligible_decisions = [row for row in decision_rows if row['orientation_eligible'] == 'True'
                              and row['instrument_version'] == cohort['instrument_version']
                              and row['instrument_hash'] == cohort['instrument_hash']
                              and row['bank_hash'] == cohort['bank_hash'] and row['synthetic'] == str(cohort['synthetic'])
                              and (row['research_participant_id'], row['session']) in cohort_people
                              and row['turn'].isdigit()
                              and cohort_people[(row['research_participant_id'], row['session'])].isdigit()
                              and int(row['turn']) < int(cohort_people[(row['research_participant_id'], row['session'])])]
        by_case = defaultdict(Counter)
        for row in eligible_decisions:
            by_case[row['case_id']][row['orientation']] += 1
        if by_case:
            sections.append(table_html('Συμπληρωματική καταγραφή επιλογών έως το σημείο αξιολόγησης', label,
                                      ['Υπόθεση', 'Δημοκρατική κλίση επιχειρήματος', 'Ολιγαρχική κλίση επιχειρήματος', 'Μικτή κλίση επιχειρήματος'],
                                      [[case, values['democracy'], values['oligarchy'], values['mixed']]
                                       for case, values in sorted(by_case.items())]))
            mixed_only = sum(not values['democracy'] and not values['oligarchy'] for values in by_case.values())
            sections.append('<p class="warning">Στα διαθέσιμα συμβάντα, ' + str(mixed_only) + ' από ' + str(len(by_case)) +
                            ' υποθέσεις έχουν μόνο επιλογές μικτής κλίσης. Η κάλυψη αυτή δεν θεμελιώνει σταθερό ιδεολογικό προφίλ. '
                            'Η κλίση χαρακτηρίζει συγκεκριμένα επιχειρήματα του παιχνιδιού και όχι την πραγματική ιδεολογία των προσώπων.</p>')
        sections.append('</section>')
    warning = ('Πρόκειται αποκλειστικά για τεχνική προσομοίωση συνθετικών εγγραφών. Δεν συμμετείχαν δέκα άνθρωποι και '
               'δεν προκύπτουν εμπειρικά συμπεράσματα για την ποιότητα, τη μάθηση ή την πολιτική συμμετοχή.'
               if summary['synthetic'] else
               'Ελέγξτε την προέλευση και το πρωτόκολλο συλλογής. Τα δεδομένα αναλύονται περιγραφικά ανά έκδοση και έκθεση. '
               'Τυχόν συνθετικές εγγραφές παρουσιάζονται χωριστά και δεν αποτελούν εμπειρική τεκμηρίωση.')
    status_rows = [
        ['Εγγραφές εισόδου', summary['input_events']], ['Μοναδικά συμβάντα', summary['unique_events']],
        ['Διπλότυπα συμβάντα που συγχωνεύθηκαν', summary['deduplicated_events']],
        ['Επαναλήψεις υποβολής που συγχωνεύθηκαν', summary['duplicate_submissions_deduplicated']],
        ['Συνεδρίες', summary['sessions']], ['Επιλεγμένες έγκυρες αξιολογήσεις', summary['analysed_evaluations']],
        ['Διακριτοί ερευνητικοί κωδικοί στις επιλεγμένες αξιολογήσεις', summary['analysed_unique_participants']],
        ['Αποκλεισμένα συνθετικά συμβάντα', summary['synthetic_events_excluded']],
        ['Σφάλματα ελέγχου', len(quality['errors'])], ['Προειδοποιήσεις ελέγχου', len(quality['warnings'])],
    ]
    dataset_label = SYNTHETIC_LABEL if summary['synthetic'] else REAL_LABEL
    status = table_html('Έλεγχος διαδρομής δεδομένων', dataset_label, ['Έλεγχος', 'Πλήθος'], status_rows)
    feedback_rows = load_csv(exports / 'feedback.csv')
    comments = ''
    for synthetic in (False, True):
        selected = [row for row in feedback_rows if row['synthetic'] == str(synthetic) and row.get('feedback') and row.get('analysis_included') == 'True']
        if selected:
            comments += table_html('Προαιρετικά σχόλια', SYNTHETIC_LABEL if synthetic else REAL_LABEL,
                                   ['Ερευνητικός κωδικός', 'Έκδοση', 'Σχόλιο'],
                                   [[row['research_participant_id'], row['instrument_version'], row['feedback']] for row in selected])
    errors_html = '<pre>' + escape(json.dumps(quality, ensure_ascii=False, indent=2)) + '</pre>'
    html = '''<!doctype html><html lang="el"><meta charset="utf-8"><meta name="viewport" content="width=device-width">
<title>Περιγραφική ανάλυση αξιολόγησης</title><style>
body{font:16px/1.55 system-ui,sans-serif;color:#172e31;max-width:1500px;margin:36px auto;padding:0 28px;background:#f7f4ed}
h1,h2,h3{line-height:1.25}section{border-top:2px solid #ad9460;margin-top:36px;padding-top:20px}
.warning{background:#f3ded3;border-left:6px solid #8b4a44;padding:18px}.data-label{font-weight:700;color:#8b4a44}
table{border-collapse:collapse;width:100%;font-size:13px;margin:12px 0 28px}td,th{border:1px solid #c2b99f;padding:9px;text-align:left;vertical-align:top}
th{background:#e8e1d0}figure{margin:24px 0}img{width:100%;height:auto}figcaption{font-size:13px}pre{white-space:pre-wrap;background:#ece8df;padding:16px}
@media print{body{margin:0;padding:0}section{break-before:page}figure, tr{break-inside:avoid}}
</style><h1>Περιγραφική ανάλυση αξιολόγησης</h1>'''
    html += '<p class="warning">' + escape(warning) + '</p>'
    html += '<p>Οι τιμές 1–5 μετατρέπονται σε −2…+2 με αφαίρεση του 3. Η απάντηση «Δεν εφαρμόζεται» δεν είναι ουδέτερη απάντηση και δεν βαθμολογείται. '
    html += 'Οι ελλείπουσες απαντήσεις δεν αντικαθίστανται με μηδέν. Οι διαστάσεις είναι περιγραφικές· δεν παράγεται συνολικό επικυρωμένο σκορ MEEGA+ ή κατηγορία ποιότητας.</p>'
    html += status + ''.join(sections) + comments + '<h2>Ποιοτικός έλεγχος εξαγωγής</h2>' + errors_html
    html += '<p>Οι ερωτήσεις γνώσεων και οι επιλογές αποτελούν συμπληρωματικά δεδομένα στα CSV. Η επίδοση μετά από ανατροφοδότηση δεν αποδεικνύει από μόνη της μαθησιακό κέρδος, '
    html += 'και η ταξινόμηση επιλογών στο παιχνίδι δεν αποτελεί διάγνωση πραγματικής πολιτικής ιδεολογίας.</p></html>'
    (out / 'Analysis.html').write_text(html, encoding='utf-8')
    content_paths = list(content) if isinstance(content, (list, tuple)) else [content]
    manifest = {
        'generatedAtUtc': datetime.now(timezone.utc).isoformat(), 'datasetLabel': summary['dataset_label'],
        'synthetic': summary['synthetic'], 'includeSynthetic': include_synthetic,
        'inputSha256': source_digest(input_events),
        'contentFiles': [{'filename': Path(path).name, 'sha256': source_digest(path)} for path in content_paths],
        'scripts': {name: source_digest(P / name) for name in ('export_evaluation.py', 'analyse_evaluation.py')},
        'interpretation': warning, 'cohorts': summary['cohorts'], 'figures': figure_manifest,
        'qualityErrors': len(quality['errors']), 'qualityWarnings': len(quality['warnings']),
        'reproduction': 'Run analyse_evaluation.py with --input, --content and --output. Add --include-synthetic only for labeled QA data.',
    }
    (out / 'Analysis.manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    return {'report': str(out / 'Analysis.html'), 'manifest': str(out / 'Analysis.manifest.json'),
            'summary': summary, 'quality': quality}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--content', action='append')
    parser.add_argument('--include-synthetic', action='store_true')
    args = parser.parse_args(argv)
    result = run_report(args.input, args.output, args.content or P / 'EvaluationContent.json',
                        include_synthetic=args.include_synthetic)
    print(json.dumps({'report': result['report'], 'manifest': result['manifest'],
                      'datasetLabel': result['summary']['dataset_label'],
                      'qualityErrors': len(result['quality']['errors'])}, ensure_ascii=False))
    return 1 if result['quality']['errors'] else 0


if __name__ == '__main__':
    raise SystemExit(main())
