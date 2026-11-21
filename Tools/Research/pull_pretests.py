"""Read-only: initial (pre-test) responses of ALL email accounts, except anonymous users and
the emails passed with --exclude. Only accounts with research consent are included.

Writes a pseudonymised JSON (codes P001, P002, ... in account-creation order); emails and UIDs
are never written to disk. The code -> email mapping is printed to the console only.

    python pull_pretests.py --exclude me@example.com --output raw\pretests.json
"""
from __future__ import annotations
import argparse, json, sys
from datetime import datetime, timezone
from pathlib import Path
from urllib.error import HTTPError
from urllib.request import Request, urlopen

import pull_firestore_events as pfe
from pull_one_participant import read_player

PRE_TYPES = ("initial_knowledge", "initial_tendency", "initial_participation", "demographic_response",
             "agora_case_result", "agora_prytany_end", "agora_game_end", "final_knowledge", "experience_rating",
             "final_tendency", "final_participation", "assessment_start", "assessment_complete", "evaluation_complete")


def list_email_accounts(reader, project):
    if reader.access_token is None:
        reader.refresh_token()
    users, token = [], None
    while True:
        url = f"https://identitytoolkit.googleapis.com/v1/projects/{project}/accounts:batchGet?maxResults=1000"
        if token:
            url += "&nextPageToken=" + token
        req = Request(url, headers={"Authorization": "Bearer " + reader.access_token, "X-Goog-User-Project": project})
        try:
            with urlopen(req, timeout=60) as r:
                data = json.load(r)
        except HTTPError as exc:
            raise pfe.ExportError(f"Listing accounts failed (HTTP {exc.code}).") from exc
        users += data.get("users", [])
        token = data.get("nextPageToken")
        if not token:
            break
    # anonymous users have no email and no provider
    return [u for u in users if u.get("email")]


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--project", default="democracy-way")
    ap.add_argument("--database", default="(default)")
    ap.add_argument("--exclude", action="append", default=[], help="email to leave out (repeatable)")
    ap.add_argument("--output", required=True)
    ap.add_argument("--overwrite", action="store_true")
    a = ap.parse_args(argv)
    try:
        out = Path(a.output).resolve()
        if out.exists() and not a.overwrite:
            raise pfe.ExportError(f"{out.name} exists; add --overwrite.")
        reader = pfe.FirestoreReader(a.project, a.database)
        excl = {e.strip().lower() for e in a.exclude}
        accounts = [u for u in list_email_accounts(reader, a.project) if u["email"].lower() not in excl]
        accounts.sort(key=lambda u: int(u.get("createdAt", "0")))
        rid_to_code, mapping, no_profile, no_consent = {}, [], 0, 0
        for u in accounts:
            prof = read_player(reader, a.project, a.database, u["localId"])
            if not prof:
                no_profile += 1; mapping.append((None, u["email"], "χωρίς προφίλ (δεν έφτασε στη συγκατάθεση)")); continue
            if prof.get("researchConsent") is not True or not prof.get("researchParticipantId"):
                no_consent += 1; mapping.append((None, u["email"], "χωρίς συγκατάθεση")); continue
            code = f"P{len(rid_to_code) + 1:03d}"
            rid_to_code[prof["researchParticipantId"]] = code
            mapping.append((code, u["email"], ""))
        # one scan of all research events
        prefix = reader.parent + "/"
        cursor, scanned = None, 0
        people = {c: {"code": c, "events": []} for c in rid_to_code.values()}
        while True:
            rows = reader.query(pfe.build_query(500, cursor))
            docs = [r["document"] for r in rows if isinstance(r.get("document"), dict)]
            for d in docs:
                scanned += 1
                path = d.get("name", "")[len(prefix):].split("/")
                if len(path) != 4 or path[0] != "participants":
                    continue
                raw = {k: pfe.unwrap(v) for k, v in d.get("fields", {}).items() if k in pfe.EVENT_FIELDS}
                try:
                    meta = json.loads(raw.get("sourceRef", "{}") or "{}")
                except ValueError:
                    continue
                code = rid_to_code.get(meta.get("researchParticipantId"))
                if not code or meta.get("synthetic") is True or raw.get("type") not in PRE_TYPES:
                    continue
                people[code]["events"].append({
                    "t": raw.get("t"), "session": raw.get("session"), "type": raw.get("type"), "node": raw.get("nodeId"),
                    "choice": raw.get("choiceId"), "answer": raw.get("answer"), "correct": raw.get("correct"),
                    "prytany": raw.get("prytany"), "itemId": meta.get("itemId"), "option": meta.get("demographicOptionId"),
                    "sec": meta.get("responseActiveSeconds"), "instrument": meta.get("instrumentVersion")})
            if len(docs) < 500:
                break
            cursor = docs[-1]["name"]
        result = {"exportedAtUtc": datetime.now(timezone.utc).isoformat(), "emailsSaved": False, "authUidsSaved": False,
                  "accountsConsidered": len(accounts), "withoutProfile": no_profile, "withoutConsent": no_consent,
                  "documentsScanned": scanned, "participants": list(people.values())}
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(json.dumps(result, ensure_ascii=False, indent=1), encoding="utf-8")
        print(f"Λογαριασμοί με email: {len(accounts)} | με συγκατάθεση: {len(rid_to_code)} | χωρίς προφίλ: {no_profile} | χωρίς συγκατάθεση: {no_consent}")
        print("Αντιστοίχιση (ΜΟΝΟ στην οθόνη, δεν αποθηκεύεται):")
        for code, email, note in mapping:
            print(f"  {code or '----'}  {email}  {note}")
        print("→", out)
        return 0
    except pfe.ExportError as exc:
        print(exc, file=sys.stderr); return 1


if __name__ == "__main__":
    raise SystemExit(main())
