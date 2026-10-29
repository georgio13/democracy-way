"""Read-only administrator export, with privacy filtering before any disk write.

Uses Python's standard library and the existing `gcloud auth` login. Access tokens
are captured in memory and never printed or written. No Firebase admin key is used.
Only POST documents:runQuery (a read operation) is issued. No data is changed.
Run --dry-run to inspect the intended query without authentication or network use.
"""
from __future__ import annotations

import argparse
from collections import Counter
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time
from urllib.error import HTTPError, URLError
from urllib.parse import quote
from urllib.request import Request, urlopen


EVENT_FIELDS = (
    "eventId", "schemaVersion", "contentVersion", "t", "session", "type",
    "chapterId", "nodeId", "choiceId", "outcome", "prytany", "week", "answer",
    "correct", "duration", "treasury", "personalTreasury", "integrity", "trust",
    "participation", "abstentions", "consistency", "ethics", "authority",
    "persuasion", "parrhesia", "recognition", "orientation", "stance", "sourceRef",
)
METRICS = ("consistency", "ethics", "authority", "persuasion", "parrhesia", "recognition")
CITY_METRICS = ("food", "trust", "readiness", "war", "treasury", "demosPower", "garrison")
POINT_FIELDS = ("prytany", "week", "turn", "completedCases", "caseId", "lastCompletedCaseId",
                "contentVersion", "activeSeconds", "phase", "utc", "casesPerPrytany", "totalCases")
META_FIELDS = (
    "researchParticipantId", "researchConsent", "instrumentVersion", "administrationVersion", "postTestStatus", "exposure", "bank", "bankHash",
    "assessmentId", "stage", "caseId", "institution", "observation", "reason", "reference",
    "cityDelta", "personalDelta", "questionId", "construct", "correctIndex", "responseActiveSeconds",
    "itemId", "scale", "status", "synthetic", "metadataCompacted", "collectiveOutcome", "question", "notApplicable",
    "submissionId", "group", "section", "instrumentSection", "officialItemId", "allowNotApplicable",
    "demographicVersion", "demographicKind", "demographicOptionId", "demographicValue",
    "prompt", "orientation", "reverse", "analysisWeight", "tendencyVersion", "knowledgeVersion",
    "feedback", "openFeedback", "feedbackKind", "questionnaireSchemaVersion", "instrumentHash", "eventSequence", "syntheticScenario",
    "shownOrder", "knowledgeFormOrder", "agoraEvent",
)
# Τα δεδομένα των γεγονότων του παιχνιδιού (AgoraFirestoreTelemetry, βήμα 9): ψηφοφορία, δίκη, πρυτανεία, τέλος, κινήσεις.
AGORA_FIELDS = (
    "case", "prytany", "week", "total", "passedA", "vote", "spokeFor", "lines", "mood", "pressureMetric", "pressureValue",
    "logographer", "logographerBought", "officialFavors", "officialBribed", "trialFavors",
    "accuser", "guilty", "sycophant", "venue", "evidence", "judgeVote",
    "peitho", "fame", "ethos", "wallet", "T", "Ep", "Et", "Em", "ID", "war", "garrison", "payAbolished",
    "cityEnding", "personalEnding", "knowledgeFormOrder", "spokeForA", "spokeForB", "bribes", "excluded",
    "for", "cost", "target", "side", "exposed", "defendant",
)
POLITICAL_FIELDS = (
    "ruleVersion", "approach", "base", "foodContext", "trustContext", "fundsContext", "speaker",
    "writer", "official", "speechSide", "supportSide", "supportTotal", "threshold", "foodBefore",
    "foodDelta", "trustBefore", "trustDelta", "fullyFunded",
)
HEX_ID = re.compile(r"^[a-f0-9]{32}$")
EMAIL = re.compile(r"\b[^\s<>@]+@[^\s<>@]+\.[^\s<>@]+\b")


class ExportError(RuntimeError):
    """A safe message that contains no response body, token, UID or document path."""


def utc(value: str) -> datetime:
    try:
        result = datetime.fromisoformat(value.replace("Z", "+00:00"))
        if result.tzinfo is None:
            raise ValueError("missing timezone")
        return result.astimezone(timezone.utc)
    except (ValueError, TypeError) as exc:
        raise ExportError("Use an ISO-8601 timestamp with timezone, for example 2026-10-19T00:00:00Z.") from exc


def unwrap(value):
    if not isinstance(value, dict):
        return value
    for key, cast in (("stringValue", str), ("integerValue", int), ("doubleValue", float),
                      ("booleanValue", bool), ("timestampValue", str)):
        if key in value:
            return cast(value[key])
    if "nullValue" in value:
        return None
    if "mapValue" in value:
        return {k: unwrap(v) for k, v in value["mapValue"].get("fields", {}).items()}
    if "arrayValue" in value:
        return [unwrap(v) for v in value["arrayValue"].get("values", [])]
    return None  # Reference/bytes/geolocation fields are unnecessary for this instrument.


def primitive(value):
    if isinstance(value, str):
        return EMAIL.sub("[redacted-email]", value)
    return value if isinstance(value, (int, float, bool)) or value is None else None


def selected(source, keys):
    if not isinstance(source, dict):
        return {}
    return {k: primitive(source[k]) for k in keys if k in source and not isinstance(source[k], (dict, list))}


def minimise_metadata(meta):
    result = selected(meta, META_FIELDS)
    for key in ("point", "assessmentPoint", "startPoint", "submitPoint"):
        if isinstance(meta.get(key), dict):
            result[key] = selected(meta[key], POINT_FIELDS)
    if isinstance(meta.get("metricsDelta"), dict):
        result["metricsDelta"] = selected(meta["metricsDelta"], METRICS)
    if isinstance(meta.get("cityMetricsDelta"), dict):
        result["cityMetricsDelta"] = selected(meta["cityMetricsDelta"], CITY_METRICS)
    if isinstance(meta.get("citySnapshot"), dict):
        result["citySnapshot"] = selected(meta["citySnapshot"], CITY_METRICS)
    if isinstance(meta.get("political"), dict):
        result["political"] = selected(meta["political"], POLITICAL_FIELDS)
        if isinstance(meta["political"].get("metricsBefore"), dict):
            result["political"]["metricsBefore"] = selected(meta["political"]["metricsBefore"], METRICS)
    if isinstance(meta.get("campaignLayout"), dict):
        result["campaignLayout"] = selected(meta["campaignLayout"], ("casesPerPrytany", "totalCases"))
    if isinstance(meta.get("answers"), list):
        result["answers"] = [primitive(value) for value in meta["answers"][:4] if isinstance(value,str)]
    if isinstance(meta.get("data"), dict):
        result["data"] = selected(meta["data"], AGORA_FIELDS)
    return result


def load_study(campaign_path, evaluation_path):
    # Preserve line endings: the browser hashes the exact TextAsset text, not a re-serialized JSON object.
    campaign_text = Path(campaign_path).read_bytes().decode("utf-8-sig")
    campaign = json.loads(campaign_text)
    instrument_text = Path(evaluation_path).read_bytes().decode("utf-8-sig")
    instrument = json.loads(instrument_text)
    version = str(campaign.get("contentVersion", ""))
    instrument_version = instrument.get("instrumentVersion", "")
    if not version or not instrument_version:
        raise ExportError("The frozen study files must contain contentVersion and instrumentVersion.")
    return {"bankVersion": version, "bankHash": hashlib.sha256(campaign_text.encode("utf-8")).hexdigest()[:32],
            "instrumentVersion": instrument_version,
            "instrumentFileSha256": hashlib.sha256(instrument_text.encode("utf-8")).hexdigest()}


def build_query(page_size, cursor=None):
    query = {
        "from": [{"collectionId": "events", "allDescendants": True}],
        "select": {"fields": [{"fieldPath": key} for key in EVENT_FIELDS]},
        "orderBy": [{"field": {"fieldPath": "__name__"}, "direction": "ASCENDING"}],
        "limit": page_size,
    }
    if cursor is not None:
        query["startAt"] = {"values": [{"referenceValue": cursor}], "before": False}
    return query


class FirestoreReader:
    def __init__(self, project, database, quota_project=None):
        self.parent = f"projects/{project}/databases/{database}/documents"
        self.endpoint = "https://firestore.googleapis.com/v1/" + quote(self.parent, safe="/()") + ":runQuery"
        self.quota_project = quota_project
        self.access_token = None

    def refresh_token(self):
        binary = shutil.which("gcloud")
        if not binary:
            raise ExportError("Google Cloud CLI was not found. Install it and sign in with gcloud auth login.")
        try:
            result = subprocess.run([binary, "auth", "print-access-token", "--quiet"],
                                    capture_output=True, text=True, timeout=60, check=False)
        except (OSError, subprocess.SubprocessError) as exc:
            raise ExportError("Could not obtain the administrator token from the existing gcloud login.") from exc
        token = result.stdout.strip()
        if result.returncode != 0 or not token or any(c.isspace() for c in token):
            raise ExportError("The existing gcloud login is unavailable. Sign in yourself and retry.")
        self.access_token = token

    def query(self, query, read_time=None):
        if self.access_token is None:
            self.refresh_token()
        payload = {"structuredQuery": query}
        if read_time:
            payload["readTime"] = read_time
        for attempt in range(4):
            headers = {"Authorization": "Bearer " + self.access_token, "Content-Type": "application/json"}
            if self.quota_project:
                headers["X-Goog-User-Project"] = self.quota_project
            request = Request(self.endpoint, data=json.dumps(payload).encode("utf-8"), headers=headers, method="POST")
            try:
                with urlopen(request, timeout=60) as response:
                    data = json.load(response)
                if not isinstance(data, list):
                    raise ExportError("Firestore returned an unexpected runQuery response; no export was saved.")
                return data
            except HTTPError as exc:
                # Never print the response body: it can contain document paths or account identifiers.
                if exc.code == 401 and attempt == 0:
                    self.refresh_token()
                    continue
                if exc.code in (429, 500, 502, 503, 504) and attempt < 3:
                    time.sleep(2 ** attempt)
                    continue
                if exc.code == 403:
                    raise ExportError("Firestore denied IAM access (403). Check the selected administrator, database viewer permissions and quota project. Do not weaken Security Rules.") from exc
                if exc.code == 400:
                    raise ExportError("Firestore rejected runQuery (400). Check database mode, collection-group indexes and snapshot timestamp. No output was saved.") from exc
                raise ExportError(f"Firestore runQuery failed with HTTP {exc.code}; no output was saved.") from exc
            except (URLError, TimeoutError) as exc:
                raise ExportError("Network request failed; no output was saved. Retry the read-only export.") from exc
        raise ExportError("Firestore query did not complete.")


def snapshot_time(reader):
    # A zero-result query obtains the server's readTime without enumerating parent documents.
    rows = reader.query(build_query(0))
    values = [row["readTime"] for row in rows if row.get("readTime")]
    if not values:
        raise ExportError("Firestore did not provide a snapshot readTime.")
    value = values[-1]
    # runQuery requires microsecond precision for an explicit historical readTime.
    return re.sub(r"\.(\d{6})\d+Z$", r".\1Z", value)


def collect(reader, study, page_size=500, after=None, before=None, max_documents=100000, include_synthetic=False):
    start = time.monotonic()
    read_time = snapshot_time(reader)
    cursor = None
    events, skipped = [], Counter()
    scanned = pages = 0
    prefix = reader.parent + "/"
    while True:
        if time.monotonic() - start > 3000:
            raise ExportError("Export exceeded the snapshot time budget; no output was saved. Retry with a smaller study scope.")
        rows = reader.query(build_query(page_size, cursor), read_time)
        documents = [row["document"] for row in rows if isinstance(row.get("document"), dict)]
        pages += 1
        for document in documents:
            scanned += 1
            if scanned > max_documents:
                raise ExportError("Document safety limit exceeded; no partial export was saved. Increase --max-documents deliberately if required.")
            name = document.get("name", "")
            path = name[len(prefix):].split("/") if name.startswith(prefix) else []
            if len(path) != 4 or path[0] != "participants" or path[2] != "events":
                skipped["outside-participants-events"] += 1
                continue
            raw = {k: unwrap(v) for k, v in document.get("fields", {}).items() if k in EVENT_FIELDS}
            try:
                meta = json.loads(raw.get("sourceRef", "{}"))
                if not isinstance(meta, dict):
                    raise ValueError()
            except (ValueError, TypeError):
                skipped["invalid-metadata"] += 1
                continue
            if meta.get("synthetic") is True and not include_synthetic:
                skipped["synthetic-excluded"] += 1
                continue
            if meta.get("researchConsent") is not True:
                skipped["no-recorded-research-consent"] += 1
                continue
            if (meta.get("instrumentVersion") != study["instrumentVersion"] or
                    str(meta.get("bank", "")) != study["bankVersion"] or
                    meta.get("bankHash") != study["bankHash"]):
                skipped["different-study-version-or-bank-hash"] += 1
                continue
            if meta.get("instrumentHash") and meta["instrumentHash"] != study["instrumentFileSha256"][:32]:
                skipped["different-instrument-content-hash"] += 1
                continue
            if not all(HEX_ID.fullmatch(str(value or "")) for value in
                       (meta.get("researchParticipantId"), raw.get("session"), raw.get("eventId"))):
                skipped["invalid-pseudonymous-identifiers"] += 1
                continue
            if meta.get("researchParticipantId") == path[1]:
                skipped["research-id-equals-authentication-id"] += 1
                continue
            try:
                event_time = utc(raw.get("t", ""))
            except ExportError:
                skipped["invalid-event-time"] += 1
                continue
            if (after is not None and event_time < after) or (before is not None and event_time >= before):
                skipped["outside-time-window"] += 1
                continue
            event = selected(raw, tuple(k for k in EVENT_FIELDS if k != "sourceRef"))
            event["sourceRef"] = json.dumps(minimise_metadata(meta), ensure_ascii=False, separators=(",", ":"))
            # A free-text authoring field must not accidentally reintroduce the known transport UID.
            for key, value in event.items():
                if isinstance(value, str):
                    event[key] = value.replace(path[1], "[redacted-auth-uid]")
            events.append(event)
        if not documents or len(documents) < page_size:
            break
        next_cursor = documents[-1].get("name")
        if not next_cursor or next_cursor == cursor:
            raise ExportError("Pagination cursor did not advance; no partial export was saved.")
        cursor = next_cursor  # Kept in memory only: the path contains an authentication UID.
    return events, {"snapshotReadTime": read_time, "pages": pages, "documentsScanned": scanned,
                    "eventsIncluded": len(events), "skipped": dict(skipped)}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", default="democracy-way")
    parser.add_argument("--database", default="(default)")
    parser.add_argument("--campaign-content", help="Frozen CampaignContent.json of the evaluated build")
    parser.add_argument("--evaluation-content")
    parser.add_argument("--study-manifest", default=str(Path(__file__).with_name("StudyManifest.json")))
    parser.add_argument("--output", required=True, help="Local privacy-minimised JSON; never a public repository")
    parser.add_argument("--after", help="Inclusive UTC start, ISO-8601 with timezone")
    parser.add_argument("--before", help="Exclusive UTC end, ISO-8601 with timezone")
    parser.add_argument("--page-size", type=int, default=500)
    parser.add_argument("--max-documents", type=int, default=100000)
    parser.add_argument("--quota-project", help="Optional Google Cloud quota project; requires serviceusage.services.use")
    parser.add_argument("--overwrite", action="store_true")
    parser.add_argument("--include-synthetic", action="store_true", help="Explicit QA-only inclusion; synthetic records remain labeled")
    parser.add_argument("--dry-run", action="store_true", help="No login, requests or output files")
    args = parser.parse_args(argv)
    try:
        if not re.fullmatch(r"[a-z0-9][a-z0-9-]*", args.project) or not re.fullmatch(r"[A-Za-z0-9()_-]+", args.database):
            raise ExportError("Invalid project or database identifier.")
        if not 1 <= args.page_size <= 1000 or args.max_documents < 1:
            raise ExportError("Use page-size 1–1000 and a positive max-documents limit.")
        after, before = utc(args.after) if args.after else None, utc(args.before) if args.before else None
        if after and before and after >= before:
            raise ExportError("--after must precede --before.")
        if args.campaign_content:
            campaign_path = args.campaign_content
            evaluation_path = args.evaluation_content or Path(__file__).with_name("EvaluationContent.json")
        else:
            manifest_file = Path(args.study_manifest)
            current_study = json.loads(manifest_file.read_text(encoding="utf-8-sig"))
            campaign_path = manifest_file.parent / current_study["campaignFile"]
            evaluation_path = args.evaluation_content or manifest_file.parent / current_study["instrumentFile"]
        study = load_study(campaign_path, evaluation_path)
        output = Path(args.output).resolve()
        manifest_path = output.with_suffix(".manifest.json")
        if not args.overwrite and (output.exists() or manifest_path.exists()):
            raise ExportError("Output already exists. Choose a new filename or explicitly use --overwrite.")
        plan = {"project": args.project, "database": args.database, "study": study,
                "afterInclusive": args.after, "beforeExclusive": args.before,
                "includeSynthetic": args.include_synthetic,
                "networkOperation": "read-only POST documents:runQuery", "query": build_query(args.page_size),
                "rawDocumentPathsSaved": False, "authUidsSaved": False, "emailsSaved": False}
        if args.dry_run:
            print(json.dumps({"dryRun": True, **plan}, ensure_ascii=False, indent=2))
            return 0
        reader = FirestoreReader(args.project, args.database, args.quota_project)
        events, counts = collect(reader, study, args.page_size, after, before, args.max_documents, args.include_synthetic)
        manifest = {"exportedAtUtc": datetime.now(timezone.utc).isoformat(), **plan, **counts,
                    "privacy": "Pseudonymous research data; not anonymous. No raw response, credentials, authentication document path or account profile saved.",
                    "interpretation": "Keep study versions and exposure/duration context separate. Descriptive pilot; no causal or ideology diagnosis."}
        output.parent.mkdir(parents=True, exist_ok=True)
        # No raw intermediate file: only projected/filtered records reach the disk.
        output.write_text(json.dumps({"synthetic": False, "export": manifest, "events": events}, ensure_ascii=False, indent=2), encoding="utf-8")
        manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
        print(json.dumps({"eventsIncluded": len(events), "documentsScanned": counts["documentsScanned"],
                          "output": str(output), "manifest": str(manifest_path)}, ensure_ascii=False))
        return 0
    except (ExportError, OSError, ValueError) as exc:
        # Deliberately no traceback/request details on failures involving administrator credentials.
        message = str(exc) if isinstance(exc, ExportError) else "Local input/output or JSON validation failed; check study files and output folder."
        print(message, file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
