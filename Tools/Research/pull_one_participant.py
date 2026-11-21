"""Read-only export of ONE participant's research events, selected by account email.

Reuses pull_firestore_events.py (same gcloud login, same consent / version / synthetic
filters, same privacy minimisation). The email is used only in memory to look up the
Firebase Auth UID; neither the email nor the UID is written to disk.

    python pull_one_participant.py --email someone@example.com --output raw/one.json
"""
from __future__ import annotations

import argparse, json, sys
from datetime import datetime, timezone
from pathlib import Path
from urllib.error import HTTPError
from urllib.parse import quote
from urllib.request import Request, urlopen

import pull_firestore_events as pfe


def lookup_uid(reader, project, email):
    if reader.access_token is None:
        reader.refresh_token()
    url = f"https://identitytoolkit.googleapis.com/v1/projects/{project}/accounts:lookup"
    headers = {"Authorization": "Bearer " + reader.access_token, "Content-Type": "application/json",
               "X-Goog-User-Project": project}
    req = Request(url, data=json.dumps({"email": [email]}).encode(), headers=headers, method="POST")
    try:
        with urlopen(req, timeout=60) as r:
            users = json.load(r).get("users", [])
    except HTTPError as exc:
        raise pfe.ExportError(f"Authentication lookup failed (HTTP {exc.code}). The gcloud account needs Firebase Authentication viewer rights.") from exc
    if not users:
        raise pfe.ExportError("No account with this email in Firebase Authentication.")
    return users[0]["localId"]


def load_study_tolerant(mf, cur):
    """Like pfe.load_study, but takes contentVersion from StudyManifest.json when the
    frozen case bank (e.g. agora-5) does not carry it itself."""
    import hashlib
    camp_text = (mf.parent / cur["campaignFile"]).read_bytes().decode("utf-8-sig")
    inst_text = (mf.parent / cur["instrumentFile"]).read_bytes().decode("utf-8-sig")
    camp, inst = json.loads(camp_text), json.loads(inst_text)
    version = str(camp.get("contentVersion") or cur.get("contentVersion") or "")
    iv = inst.get("instrumentVersion") or cur.get("instrumentVersion") or ""
    if not version or not iv:
        raise pfe.ExportError("Could not determine contentVersion / instrumentVersion.")
    return {"bankVersion": version, "bankHash": hashlib.sha256(camp_text.encode("utf-8")).hexdigest()[:32],
            "instrumentVersion": iv, "instrumentFileSha256": hashlib.sha256(inst_text.encode("utf-8")).hexdigest()}


def read_player(reader, project, database, uid):
    """GET players/{uid} (read-only). Returns {} if missing."""
    if reader.access_token is None:
        reader.refresh_token()
    url = (f"https://firestore.googleapis.com/v1/projects/{project}/databases/"
           f"{quote(database, safe='()')}/documents/players/{uid}")
    req = Request(url, headers={"Authorization": "Bearer " + reader.access_token}, method="GET")
    try:
        with urlopen(req, timeout=60) as r:
            doc = json.load(r)
    except HTTPError as exc:
        if exc.code == 404:
            return {}
        raise pfe.ExportError(f"Reading the player profile failed (HTTP {exc.code}).") from exc
    return {k: pfe.unwrap(v) for k, v in doc.get("fields", {}).items()}


def collect_by_research_id(reader, rid, any_version=False, study=None):
    """Scan all participants/*/events (same as the full export) and keep only this research id."""
    cursor, events, scanned, skipped = None, [], 0, {}
    prefix = reader.parent + "/"
    while True:
        rows = reader.query(pfe.build_query(500, cursor))
        docs = [r["document"] for r in rows if isinstance(r.get("document"), dict)]
        for d in docs:
            scanned += 1
            path = d.get("name", "")[len(prefix):].split("/")
            if len(path) != 4 or path[0] != "participants" or path[2] != "events":
                continue
            raw = {k: pfe.unwrap(v) for k, v in d.get("fields", {}).items() if k in pfe.EVENT_FIELDS}
            try:
                meta = json.loads(raw.get("sourceRef", "{}") or "{}")
            except ValueError:
                continue
            if not isinstance(meta, dict) or meta.get("researchParticipantId") != rid:
                continue
            reason = None
            if meta.get("synthetic") is True: reason = "synthetic"
            elif meta.get("researchConsent") is not True: reason = "no-consent"
            elif not any_version and (meta.get("instrumentVersion") != study["instrumentVersion"]
                                      or str(meta.get("bank", "")) != study["bankVersion"]):
                reason = f"version:{meta.get('instrumentVersion')}/{meta.get('bank')}"
            if reason:
                skipped[reason] = skipped.get(reason, 0) + 1
                continue
            e = pfe.selected(raw, tuple(k for k in pfe.EVENT_FIELDS if k != "sourceRef"))
            e["sourceRef"] = json.dumps(pfe.minimise_metadata(meta), ensure_ascii=False, separators=(",", ":"))
            for k, v in e.items():
                if isinstance(v, str):
                    e[k] = v.replace(path[1], "[redacted-auth-uid]")
            events.append(e)
        if len(docs) < 500:
            break
        cursor = docs[-1]["name"]
    return events, {"eventsIncluded": len(events), "documentsScanned": scanned, "skipped": skipped}


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--email", required=True)
    ap.add_argument("--project", default="democracy-way")
    ap.add_argument("--database", default="(default)")
    ap.add_argument("--study-manifest", default=str(Path(__file__).with_name("StudyManifest.json")))
    ap.add_argument("--any-version", action="store_true", help="Keep events of every instrument/bank version")
    ap.add_argument("--output", required=True)
    ap.add_argument("--overwrite", action="store_true", help="Γράφει πάνω σε υπάρχον αρχείο (χωρίς αυτό σταματά αν το αρχείο υπάρχει)")
    a = ap.parse_args(argv)
    try:
        mf = Path(a.study_manifest); cur = json.loads(mf.read_text(encoding="utf-8-sig"))
        study = load_study_tolerant(mf, cur)
        reader = pfe.FirestoreReader(a.project, a.database)
        uid = lookup_uid(reader, a.project, a.email)
        profile = read_player(reader, a.project, a.database, uid)
        rid = profile.get("researchParticipantId")
        consent = profile.get("researchConsent")
        print(json.dumps({"playerProfileFound": bool(profile), "researchConsent": consent,
                          "consentVersion": profile.get("consentVersion"), "hasResearchId": bool(rid)}, ensure_ascii=False))
        if not profile:
            raise pfe.ExportError("No players/{uid} profile for this account: the player never reached the consent step.")
        if consent is not True:
            raise pfe.ExportError("This account did not give research consent; its data must not be used. Nothing saved.")
        if not rid:
            raise pfe.ExportError("Profile has no researchParticipantId.")
        events, counts = collect_by_research_id(reader, rid, any_version=a.any_version, study=study)
        manifest = {"exportedAtUtc": datetime.now(timezone.utc).isoformat(), "scope": "single participant (selected by email, not stored)",
                    "study": study, **counts, "emailsSaved": False, "authUidsSaved": False}
        out = Path(a.output).resolve(); out.parent.mkdir(parents=True, exist_ok=True)
        if out.exists() and not a.overwrite:
            raise pfe.ExportError(f"Το {out.name} υπάρχει ήδη. Πρόσθεσε --overwrite για να το αντικαταστήσεις.")
        out.write_text(json.dumps({"synthetic": False, "export": manifest, "events": events}, ensure_ascii=False, indent=2), encoding="utf-8")
        print(json.dumps({"eventsIncluded": len(events), **{k: v for k, v in counts.items() if k != "eventsIncluded"}, "output": str(out)}, ensure_ascii=False))
        return 0
    except pfe.ExportError as exc:
        print(exc, file=sys.stderr); return 1


if __name__ == "__main__":
    raise SystemExit(main())
