# -*- coding: utf-8 -*-
"""Προσομοίωση των τελικών κανόνων της «Οδού Δημοκρατίας» (έγγραφο σχεδίασης, 28/10).
Ένας κανόνας για κάθε ψηφοφορία: S = Βάση + Πίεση + Πόλεμος + Άρειος + Αξιωματούχος + Δίκη + Λόγος + Τύχη."""
import json, random, statistics as st
from collections import Counter, defaultdict

EFF = {int(k): v for k, v in json.load(open('effects.json')).items()}

# ---------- Δεδομένα ανά υπόθεση (εκτός από effects, που έρχονται από τα assets) ----------
# pressure: (δείκτης, πλευρά που ευνοείται όταν ο δείκτης < 35) ; 'MIN' = οποιοσδήποτε ορατός δείκτης
# undecided: (όνομα, δυσκολία πειθούς σε Πειθώ, δωροδοκήσιμος)
DATA = {
 1: dict(pressure=('Em','B'), und=('Κέφαλος',30,False)),
 2: dict(pressure=('Ep','B'), und=('Αριστόνικος',40,False)),
 3: dict(pressure=('Et','B'), und=('Αριστόνικος',40,False), trial=dict(base=58, venue='Ηλιαία', conv='B')),
 4: dict(pressure=('Em','B'), und=('Κέφαλος',30,False)),
 5: dict(pressure=('Em','B'), und=('Πυθέας',30,True)),
 6: dict(pressure=('Em','B'), und=('Λυκούργος',50,False)),
 7: dict(pressure=('Et','A'), und=('Μνησίθεος',30,False)),
 8: dict(pressure=('T','A'),  und=('Δημάδης',40,True)),
 9: dict(pressure=('Em','B'), und=('Υπερείδης',40,False), trial=dict(base=18, venue='Ηλιαία', conv='A')),
10: dict(pressure=('Et','A'), und=('Ευθύδημος',40,True), trial=dict(base=62, venue='Άρειος Πάγος', conv='A')),
11: dict(pressure=('Em','B'), und=('Πυθέας',30,True), trial=dict(base=55, venue='Ηλιαία', conv='A')),
12: dict(pressure=('T','B'),  und=('Δημάδης',40,True)),
13: dict(pressure=('Et','B'), und=('Κηφισόδωρος',30,False), trial=dict(base=18, venue='Ηλιαία', conv='A')),
14: dict(pressure=('Em','B'), und=('Δημοσθένης',40,False)),
15: dict(pressure=('Ep','B'), und=('Δημάδης',50,True)),
16: dict(pressure=('Ep','A'), und=('Λυκούργος',30,False)),
17: dict(pressure=('T','B'),  und=('Κέφαλος',30,False)),
18: dict(pressure=('Em','B'), und=('Φωκίων',50,False)),
19: dict(pressure=('Et','B'), und=('Δημάδης',50,True)),
20: dict(pressure=('MIN','B'), und=('Φωκίων',50,False)),
}
# Η υπόθεση 20 είναι η συνταγματική απόφαση: βαρύτερη επίπτωση στην Ισχύ του Δήμου.
EFF[20]['effA']['ID'] = 6
EFF[20]['effB']['ID'] = -12

BASES = {i: 50 for i in range(1, 21)}

CLASSES = {'thes': (60, 30), 'zeug': (150, 60), 'hipp': (300, 120), 'pent': (600, 250)}
CFG = dict(noise=6, pressure=5, low=35, war_emerg=10, areo=5, official=4, trial=4, logo=3, logo_cost=40,
           bribe_cost=30, bribe_ethos=10, expose_p=1/3, expose_fame=5, expose_trust=3, pay=10,
           fine=50, fifth=20, peitho_gain=2, peitho_cap=80, fame_speech=2, fame_win=2,
           fame_conv=5, fame_acq=-3, fame_silence=2, ethos_mismatch=5, ethos_conv=3,
           war_at=5, fame_end=40, faction_speeches=8, bribes_harpalus=3)

def clamp(x, lo=0, hi=100): return max(lo, min(hi, x))

class Player:
    def __init__(s, cls='zeug'):
        s.wallet, s.income = CLASSES[cls]
        s.peitho, s.fame, s.ethos = 30, 10, 60
        s.spokeA = s.spokeB = s.bribes = 0
        s.silent_streak = 0; s.sycophant = False; s.flips = 0; s.decided = 0

def drift(city, war):
    d = dict(T=-3, Ep=-4, Et=-1) if war else dict(T=3, Ep=2, Et=-3)
    for m, v in d.items(): city[m] = clamp(city[m] + v)

def city_ending(city, war, garrison):
    if city['Ep'] < 15 or (war and city['Et'] < 15): return 'Λιμός'
    if city['ID'] < 25: return 'Όριο περιουσίας'
    if city['Em'] < 35 and city['ID'] < 50: return 'Στάσις'
    if city['ID'] >= 50 and min(city[m] for m in ('T','Ep','Et','Em')) >= 25 and not garrison: return 'Η πόλη άντεξε'
    return 'Φρουρά'

def personal_ending(p, cend):
    if cend == 'Όριο περιουσίας' and p.spokeA >= CFG['faction_speeches'] and p.spokeA > p.spokeB: return 'Υπερείδης'
    if cend == 'Όριο περιουσίας' and p.spokeB >= CFG['faction_speeches'] and p.spokeB > p.spokeA: return 'Δημάδης'
    if p.bribes >= CFG['bribes_harpalus']: return 'Αρπάλου'
    if p.fame >= CFG['fame_end'] and p.ethos >= 50: return 'Λυκούργος'
    if p.fame >= CFG['fame_end']: return 'Στρατοκλής'
    return 'Ένας από τους πολλούς'

def play(strategy, rng, cls='zeug', bases=None):
    bases = bases or BASES
    city = dict(T=50, Ep=50, Et=50, Em=50, ID=50)
    war = garrison = pay_abolished = areopagus = False; audit = False
    p = Player(cls); log = []
    for pr in range(1, 11):
        if pr == CFG['war_at']: war = True
        drift(city, war)
        if audit: city['T'] = clamp(city['T'] + 6); audit = False
        p.wallet += p.income + (0 if pay_abolished else CFG['pay'])
        for wk in range(2):
            cid = (pr - 1) * 2 + wk + 1; D = DATA[cid]; E = EFF[cid]
            plan = strategy(cid, p, city, war, rng)
            side = plan.get('side')           # 'A' / 'B' / None
            parts = {}
            # --- δίκη της εβδομάδας ---
            trial_mod = 0
            if 'trial' in D:
                T = D['trial']; accuser = plan.get('accuse') and not p.sycophant
                speech = 0
                logo_t = False
                if accuser and T['venue'] == 'Ηλιαία':
                    speech = p.peitho // 10
                    if plan.get('logo') == 'trial' and p.wallet >= CFG['logo_cost']:
                        p.wallet -= CFG['logo_cost']; speech += CFG['logo']; logo_t = True
                v = T['base'] + speech + rng.randint(-CFG['noise'], CFG['noise'])
                conv = v >= 50
                win_side = T['conv'] if conv else ('A' if T['conv'] == 'B' else 'B')
                trial_mod = CFG['trial'] if win_side == 'A' else -CFG['trial']
                if accuser:
                    if conv: p.fame = clamp(p.fame + CFG['fame_conv']); p.ethos = clamp(p.ethos + CFG['ethos_conv'])
                    else:
                        p.fame = clamp(p.fame + CFG['fame_acq'])
                        if v < CFG['fifth']:
                            p.wallet = max(0, p.wallet - CFG['fine']); p.sycophant = True
            # --- αξιωματούχος ---
            off_mod = 0
            act = plan.get('official')  # 'persuade' / 'bribe' / None
            name, diff, bribable = D['und']
            if side and act == 'persuade' and p.peitho >= diff:
                off_mod = CFG['official'] if side == 'A' else -CFG['official']
            elif side and act == 'bribe' and bribable and p.wallet >= CFG['bribe_cost']:
                p.wallet -= CFG['bribe_cost']; p.ethos = clamp(p.ethos - CFG['bribe_ethos']); p.bribes += 1
                off_mod = CFG['official'] if side == 'A' else -CFG['official']
                if rng.random() < CFG['expose_p']:
                    p.fame = clamp(p.fame - CFG['expose_fame']); city['Em'] = clamp(city['Em'] - CFG['expose_trust'])
            # --- βάση και τροποποιητές της πόλης ---
            s0 = bases[cid]
            m, fav = D['pressure']
            low = (min(city[k] for k in ('T','Ep','Et','Em')) if m == 'MIN' else city[m]) < CFG['low']
            press = (CFG['pressure'] if fav == 'A' else -CFG['pressure']) if low else 0
            warm = -CFG['war_emerg'] if (war and E['emerg']) else 0
            areo = -CFG['areo'] if (areopagus and cid in (19, 20)) else 0
            s0 += press + warm + areo
            # --- λόγος ---
            speech = 0
            if side and plan.get('speak'):
                speech = p.peitho // 10
                if plan.get('logo') == side and p.wallet >= CFG['logo_cost']:
                    p.wallet -= CFG['logo_cost']; speech += CFG['logo']
            sp = speech if side == 'A' else -speech
            player_part = sp + off_mod
            noise = rng.randint(-CFG['noise'], CFG['noise'])
            S = s0 + trial_mod + player_part + noise
            passA = S >= 50
            without = S - player_part
            if side and (without >= 50) != passA: p.flips += 1
            # --- συνέπειες ---
            for k, v in (E['effA'] if passA else E['effB']).items():
                f = 1.5 if (k == 'ID' and v < 0 and garrison) else 1
                city[k] = clamp(city[k] + round(v * f))
            if cid == 12 and not passA: pay_abolished = True
            if cid == 15: war = passA; garrison = not passA
            if cid == 17 and passA: audit = True
            if cid == 18 and not passA: areopagus = True
            # --- παίκτης ---
            if side and plan.get('speak'):
                p.peitho = min(CFG['peitho_cap'], p.peitho + CFG['peitho_gain'])
                p.fame = clamp(p.fame + CFG['fame_speech'])
                if (side == 'A') == passA: p.fame = clamp(p.fame + CFG['fame_win'])
                if side == 'A': p.spokeA += 1
                else: p.spokeB += 1
                p.silent_streak = 0
                vote = plan.get('vote', side)
                if vote != side: p.ethos = clamp(p.ethos - CFG['ethos_mismatch'])
            else:
                p.silent_streak += 1
                if p.silent_streak >= 2: p.fame = clamp(p.fame - CFG['fame_silence'])
            log.append((cid, passA, s0, S))
    ce = city_ending(city, war, garrison)
    return ce, personal_ending(p, ce), city, p, log

# ---------- στρατηγικές ----------
def silent(cid, p, city, war, rng): return {}
def activist(which, bribe=False, logo=True, accuse=True):
    def f(cid, p, city, war, rng):
        D = DATA[cid]; plan = {'side': which, 'speak': True}
        name, diff, bribable = D['und']
        if p.peitho >= diff: plan['official'] = 'persuade'
        elif bribe and bribable: plan['official'] = 'bribe'
        if 'trial' in D and accuse and D['trial']['base'] >= 35 and D['trial']['conv'] == which:
            plan['accuse'] = True
        reserve = CFG['bribe_cost'] if bribe else 0
        if logo and p.wallet >= CFG['logo_cost'] + reserve:
            plan['logo'] = 'trial' if plan.get('accuse') and D['trial']['venue'] == 'Ηλιαία' and rng.random() < 0.3 else which
        return plan
    return f
def random_player(cid, p, city, war, rng):
    side = rng.choice('AB'); plan = {'side': side, 'speak': rng.random() < 0.6}
    if rng.random() < 0.5: plan['official'] = rng.choice(['persuade', 'bribe'])
    if rng.random() < 0.3: plan['logo'] = side
    if 'trial' in DATA[cid] and rng.random() < 0.4: plan['accuse'] = True
    plan['vote'] = side if rng.random() < 0.85 else ('A' if side == 'B' else 'B')
    return plan
def speaker_sometimes(cid, p, city, war, rng):   # ρεαλιστικός μαθητής: μιλά στο μισό, πάντα δημοκρατικά
    plan = {'side': 'A', 'speak': rng.random() < 0.5}
    if p.peitho >= DATA[cid]['und'][1]: plan['official'] = 'persuade'
    return plan

STRATS = {'σιωπηλός': silent, 'δημοκράτης ρήτορας': activist('A'), 'ολιγαρχικός ρήτορας': activist('B'),
          'δημοκράτης δωροδόκος': activist('A', bribe=True), 'ολιγαρχικός δωροδόκος': activist('B', bribe=True),
          'τυχαίος': random_player, 'μισός ρήτορας (Α)': speaker_sometimes}

def experiment(n=4000, seed=7, cls='zeug', bases=None, strats=STRATS):
    rng = random.Random(seed); out = {}
    for name, s in strats.items():
        ce = Counter(); pe = Counter(); flips = []; fin = defaultdict(list); pa = Counter(); tot = Counter(); fame=[]; eth=[]; pei=[]
        for _ in range(n):
            c, e, city, p, log = play(s, rng, cls, bases)
            ce[c] += 1; pe[e] += 1; flips.append(p.flips); fame.append(p.fame); eth.append(p.ethos); pei.append(p.peitho)
            for k, v in city.items(): fin[k].append(v)
            for cid, passA, *_ in log: tot[cid] += 1; pa[cid] += passA
        out[name] = dict(city={k: round(100*v/n) for k, v in ce.most_common()},
                         personal={k: round(100*v/n) for k, v in pe.most_common()},
                         flips=round(st.mean(flips), 1),
                         final={k: round(st.mean(v)) for k, v in fin.items()},
                         player=dict(Πειθώ=round(st.mean(pei)), Φήμη=round(st.mean(fame)), Ηθική=round(st.mean(eth))),
                         perCase=[round(100*pa[c]/tot[c]) for c in range(1, 21)],
                         Arate=round(sum(pa.values())/sum(tot.values()), 2))
    return out

TARGET_SILENT = {1:80, 2:50, 3:40, 4:55, 5:75, 6:65, 7:55, 8:70, 9:55, 10:45,
                 11:50, 12:50, 13:45, 14:65, 15:40, 16:55, 17:60, 18:50, 19:40, 20:40}

def tune(iters=14, n=1500):
    bases = dict(BASES)
    for it in range(iters):
        r = experiment(n=n, seed=100+it, bases=bases, strats={'σιωπηλός': silent})['σιωπηλός']['perCase']
        err = 0
        for c in range(1, 21):
            d = TARGET_SILENT[c] - r[c-1]; err += abs(d)
            bases[c] = int(round(bases[c] + d / 8))
        print('iter', it, 'mean abs err', round(err/20, 1))
    return bases

if __name__ == '__main__':
    import sys
    bases = tune()
    json.dump(bases, open('final_bases.json', 'w'))
    print('BASES', bases)
    res = experiment(bases=bases)
    for k, v in res.items(): print('==', k); print(json.dumps(v, ensure_ascii=False))
