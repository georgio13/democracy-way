// Έλεγχος του research.js χωρίς δίκτυο: τρέχει σε ψεύτικο browser με ένα αποθηκευμένο παιχνίδι και τα γεγονότα
// του παιχνιδιού (AgoraFirestoreTelemetry), και ελέγχει ότι ΚΑΘΕ έγγραφο που θα έφευγε περνά τους κανόνες του
// firestore.rules (επιτρεπτά κλειδιά, υποχρεωτικά πεδία, όρια τιμών, μέγεθος sourceRef).
// Εκτέλεση: node Tools/Research/test_research_js.mjs
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, '..', '..');
const script = readFileSync(process.argv[2] || join(root, 'Assets/WebGLTemplates/DemocracyWayLoader/research.js'), 'utf8');   // προαιρετικά: άλλο αρχείο
const rules = readFileSync(join(here, 'firestore.rules'), 'utf8');

// ---------- Οι κανόνες της συλλογής participants/{uid}/events ----------
const eventsBlock = rules.slice(rules.indexOf('match /participants/{uid}/events/{eventId}'));
const allowed = JSON.parse(eventsBlock.match(/keys\(\)\.hasOnly\((\[[^\]]*\])\)/)[1].replace(/'/g, '"'));
const required = JSON.parse(eventsBlock.match(/keys\(\)\.hasAll\((\[[^\]]*\])\)/)[1].replace(/'/g, '"'));
const isInt = v => Number.isInteger(v);
const intRange = (lo, hi) => v => isInt(v) && v >= lo && v <= hi;
const str = max => v => typeof v === 'string' && v.length <= max;
const mandatory = {
  schemaVersion: v => [2, 5].includes(v), contentVersion: v => ['1.0.0', '5.0.0'].includes(v),
  session: str(64), type: str(48), t: str(40), choiceId: str(160), nodeId: str(160), chapterId: str(80), outcome: str(160),
  prytany: intRange(0, 11), week: intRange(0, 5), answer: intRange(-1, 20), correct: intRange(-1, 1),
  treasury: intRange(0, 1000000), integrity: intRange(0, 100), trust: intRange(0, 100),
  participation: intRange(0, 50), abstentions: intRange(0, 50), duration: v => typeof v === 'number' && v >= 0 && v <= 86400,
};
const optional = {
  consistency: intRange(0, 100), ethics: intRange(0, 100), authority: intRange(0, 100), persuasion: intRange(0, 100),
  parrhesia: intRange(0, 100), recognition: intRange(0, 100), warMandate: intRange(0, 100), preparedness: intRange(0, 100),
  diplomacy: intRange(0, 100), autonomy: intRange(0, 100), cohesion: intRange(0, 100), personalTreasury: intRange(-1000000, 1000000),
  orientation: v => ['democracy', 'oligarchy', 'mixed', 'unclassified'].includes(v), stance: str(160), sourceRef: str(4000),
};

function violations(doc, id) {
  const problems = [];
  for (const key of Object.keys(doc)) if (!allowed.includes(key)) problems.push('κλειδί εκτός λίστας: ' + key);
  for (const key of required) if (!(key in doc)) problems.push('λείπει: ' + key);
  if (doc.eventId !== id || !/^[a-f0-9]{32}$/.test(id)) problems.push('eventId');
  for (const [key, ok] of Object.entries(mandatory)) if (!ok(doc[key])) problems.push(key + ' = ' + JSON.stringify(doc[key]));
  for (const [key, ok] of Object.entries(optional)) if (key in doc && !ok(doc[key])) problems.push(key + ' = ' + JSON.stringify(doc[key]));
  return problems;
}

// ---------- Ψεύτικος browser ----------
const storage = new Map();
const sent = [];
const fromFields = f => Object.fromEntries(Object.entries(f).map(([k, v]) => [k, 'integerValue' in v ? Number(v.integerValue) : 'doubleValue' in v ? v.doubleValue : v.stringValue]));
async function fetchStub(url, options) {
  if (url.includes('identitytoolkit') || url.includes('securetoken'))
    return { ok: true, json: async () => ({ localId: 'uid1', user_id: 'uid1', idToken: 'token', id_token: 'token', refreshToken: 'refresh', refresh_token: 'refresh', expiresIn: '3600', expires_in: '3600' }) };
  const id = new URL(url).searchParams.get('documentId');
  sent.push({ id, doc: fromFields(JSON.parse(options.body).fields) });
  return { ok: true, status: 200 };
}
const window = {};
const context = vm.createContext({
  window, console, crypto: globalThis.crypto, TextEncoder, URLSearchParams, fetch: fetchStub, setTimeout, clearTimeout,
  navigator: { onLine: true }, addEventListener: () => {},
  localStorage: { getItem: k => storage.has(k) ? storage.get(k) : null, setItem: (k, v) => storage.set(k, String(v)) },
});
vm.runInContext(script, context);
const research = window.DWResearch;

// ---------- Ένα αποθηκευμένο παιχνίδι και τα γεγονότα του ----------
const participant = 'a'.repeat(32), runId = 'b'.repeat(32);
const instrument = {
  instrumentVersion: 'campaign-final-meega-el-6',
  onboarding: [{ id: 'k01a', prompt: 'Ερώτηση;', correctId: 'a', answers: ['a', 'b', 'c', 'd'].map(id => ({ id, text: 'Απάντηση ' + id })) }],
  postTest: [], tendencies: [], participation: [], experience: [], participationIntention: [],
};
const point = { utc: '2026-10-29T10:00:00Z', caseId: '1', contentVersion: '5', prytany: 1, week: 1, turn: 0, completedCases: 0, casesPerPrytany: 2 };
const save = {
  runId, phase: 'map-stela', prytany: 3, week: 2, activeSeconds: 1200, casesPerPrytany: 2, totalCases: 20,
  citizen: { wallet: 210, peitho: 36, fame: 18, ethos: 57 },
  polis: { treasury: 52, food: 47, readiness: 33, trust: 61, demosPower: 50, war: false, garrison: false },
  campaign: { cases: [], cityEnding: '', personalEnding: '' },
  evaluation: { instrumentVersion: instrument.instrumentVersion, baselinePoint: point, baselineAnswers: [2], baselineOptionOrders: ['c,a,d,b'], baselineComplete: true },
  evaluationContent: JSON.stringify(instrument),
};
const agoraEvent = (event, data) => JSON.stringify({ event, participantId: participant, utc: '2026-10-29T10:05:00.0000000Z', data: { runId, case: 5, prytany: 3, week: 1, ...data } });

let failures = 0;
const fail = message => { failures++; console.error('✘ ' + message); };
const send = json => research.agoraEvent ? research.agoraEvent(json) : fail('το research.js δεν ορίζει agoraEvent: τα γεγονότα του παιχνιδιού χάνονται');

research.setAccountContext({ researchParticipantId: participant, allowed: true });
await research.capture(JSON.stringify(save));
await send(agoraEvent('case_result', { total: 61, passedA: true, vote: 'A', spokeFor: 'A', lines: 'Base:55,Speech:6,Mood:0', mood: 0, pressureMetric: 'Trust', pressureValue: 61 }));
await send(agoraEvent('trial', { accuser: true, total: 58, guilty: true, sycophant: false, lines: 'Evidence:58,Accusation:6,Mood:-6', venue: 'Heliaia', evidence: 58, judgeVote: 'none' }));
await send(agoraEvent('prytany_end', { peitho: 36, fame: 18, ethos: 57, wallet: 210, T: 52, Ep: 47, Et: 33, Em: 61, ID: 50, war: false, garrison: false }));
await send(agoraEvent('game_end', { cityEnding: 'GarrisonOnly', personalEnding: 'Demades', knowledgeFormOrder: 'AB' }));
for (let i = 0; i < 50 && (research.status.pending > 0 || sent.length === 0); i++) await new Promise(r => setTimeout(r, 10));

// ---------- Έλεγχοι ----------
if (research.status.lastError) fail('research.js: ' + research.status.lastError);
for (const { id, doc } of sent) { const p = violations(doc, id); if (p.length) fail(doc.type + ': ' + p.join(' · ')); }
const byType = type => sent.filter(s => s.doc.type === type).map(s => ({ ...s.doc, ref: JSON.parse(s.doc.sourceRef) }));
for (const type of ['agora_case_result', 'agora_trial', 'agora_prytany_end', 'agora_game_end'])
  if (byType(type).length !== 1) fail(type + ': στάλθηκαν ' + byType(type).length);
const vote = byType('agora_case_result')[0];
if (vote && (vote.ref.data.lines !== 'Base:55,Speech:6,Mood:0' || vote.outcome !== 'A' || vote.trust !== 61)) fail('ψηφοφορία: γραμμές/αποτέλεσμα/Εμπιστοσύνη');
const prytany = byType('agora_prytany_end')[0];
if (prytany && prytany.ref.data.ID !== 50) fail('πρυτανεία: Ισχύς του Δήμου');
const end = byType('agora_game_end')[0];
if (end && (end.outcome !== 'GarrisonOnly:Demades' || end.ref.knowledgeFormOrder !== 'AB')) fail('τέλος: αποτέλεσμα/knowledgeFormOrder');
const knowledge = byType('initial_knowledge')[0];
if (!knowledge || knowledge.ref.shownOrder !== 'c,a,d,b') fail('γνώσεις: σειρά επιλογών');
if (new Set(sent.map(s => s.id)).size !== sent.length) fail('διπλά eventId');

// Χωρίς συγκατάθεση δεν φεύγει τίποτα.
const before = sent.length;
research.setAccountContext({ researchParticipantId: 'c'.repeat(32), allowed: false });
await send(agoraEvent('case_result', { total: 40, passedA: false }));
await new Promise(r => setTimeout(r, 20));
if (sent.length !== before) fail('έφυγε γεγονός χωρίς συγκατάθεση');

console.log(failures ? `${failures} αποτυχίες σε ${sent.length} έγγραφα` : `OK: ${sent.length} έγγραφα, όλα μέσα στους κανόνες του firestore.rules`);
process.exit(failures ? 1 : 0);
