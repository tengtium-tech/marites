#!/usr/bin/env python3
"""Documentation consistency check for the Marites specification set.

The set claims that its links, identifiers and counts are checked mechanically.
This is that check. It is deliberately stdlib-only so it runs anywhere Python
does, with no install step, and it exits non-zero so a pipeline can gate on it.

    python tools/docs_check.py            # check, print findings, exit 1 on error
    python tools/docs_check.py --strict   # treat warnings as errors

What it verifies:

  1. Every relative Markdown link resolves to a file.
  2. Every in-page and cross-page anchor resolves to a heading, using GitHub's slug rules.
  3. Every cited identifier (ADR, DEC, FR, NFR, TC, THR, RSK, ASM, ERR, OP) is defined
     in the document that owns it.
  4. Every stated total matches the file that owns the number.
  5. Every FR and NFR has exactly one acceptance-criteria row.
  6. Names retired by a superseding decision are not used as if current.

On counts: totals are checked against an explicit list of claims rather than by scanning
for any number followed by a noun. "Ten gates" is a true statement about CI stages and
"three tables" is a true statement about the message store, so a naive scan produces more
false findings than real ones, and a checker that cries wolf is switched off. Adding a
total claim to the documents means adding a line to CLAIMS below; that is the cost of
precision, and it is one line.

It cannot check whether a statement is true of the system, because there is no system.
It checks that the documents agree with themselves.
"""

from __future__ import annotations

import argparse
import os
import re
import sys
from collections import defaultdict

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
DOCS = os.path.join(ROOT, "docs")

errors: list[str] = []
warnings: list[str] = []


def error(where: str, msg: str) -> None:
    errors.append(f"{where}: {msg}")


def warn(where: str, msg: str) -> None:
    warnings.append(f"{where}: {msg}")


def rel(path: str) -> str:
    return os.path.relpath(path, ROOT).replace(os.sep, "/")


def read(path: str) -> str:
    with open(path, encoding="utf-8") as handle:
        return handle.read()


def doc(relative: str) -> str:
    return os.path.normpath(os.path.join(DOCS, relative))


def markdown_files() -> list[str]:
    found = []
    for root, _dirs, files in os.walk(DOCS):
        for name in files:
            if name.endswith(".md"):
                found.append(os.path.normpath(os.path.join(root, name)))
    return sorted(found)


# ---------------------------------------------------------------------------
# 1 + 2. Links and anchors
# ---------------------------------------------------------------------------

def slug(heading: str) -> str:
    """GitHub heading slug: lowercase, drop punctuation, spaces to hyphens."""
    text = heading.strip().lower()
    text = re.sub(r"[^\w\s-]", "", text)
    return text.replace(" ", "-")


def headings_by_file(files: list[str]) -> dict[str, set[str]]:
    table = {}
    for path in files:
        anchors = set()
        in_fence = False
        for line in read(path).splitlines():
            if line.lstrip().startswith("```"):
                in_fence = not in_fence
                continue
            if in_fence or not line.startswith("#"):
                continue
            anchors.add(slug(line.lstrip("#")))
        table[path] = anchors
    return table


LINK = re.compile(r"\]\(([^)\s]+)(?:\s+\"[^\"]*\")?\)")


def check_links(files: list[str], anchors: dict[str, set[str]]) -> int:
    total = 0
    for path in files:
        text = read(path)
        for match in LINK.finditer(text):
            target = match.group(1)
            if target.startswith(("http://", "https://", "mailto:", "tel:")):
                continue
            total += 1
            where = f"{rel(path)}:{text[: match.start()].count(chr(10)) + 1}"
            file_part, _, fragment = target.partition("#")
            if file_part:
                resolved = os.path.normpath(os.path.join(os.path.dirname(path), file_part))
                if not os.path.exists(resolved):
                    error(where, f"link target does not exist: {target}")
                    continue
                if fragment and resolved.endswith(".md"):
                    known = anchors.get(resolved, set())
                    if known and fragment not in known:
                        error(where, f"anchor does not resolve: {target}")
            elif fragment and anchors[path] and fragment not in anchors[path]:
                error(where, f"anchor does not resolve: {target}")
    return total


# ---------------------------------------------------------------------------
# 3. Identifiers
# ---------------------------------------------------------------------------

OWNERS = {
    "ADR": ("05-architecture/decisions.md", re.compile(r"^\|\s*(ADR-\d{3})\s*\|", re.M)),
    "DEC": ("01-project/decisions-and-open-questions.md", re.compile(r"^##\s*(DEC-\d{3})\b", re.M)),
    "FR": ("02-business/requirements.md", re.compile(r"^\|\s*(FR-\d{3})\s*\|", re.M)),
    "NFR": ("02-business/requirements.md", re.compile(r"^\|\s*(NFR-\d{3})\s*\|", re.M)),
    "TC": ("10-testing/verification-plan.md", re.compile(r"^\|\s*(TC-\d{3})\s*\|", re.M)),
    "THR": ("09-security/threat-model.md", re.compile(r"^\|\s*(THR-\d{3})\s*\|", re.M)),
    "RSK": ("01-project/assumptions-and-risks.md", re.compile(r"^\|\s*(RSK-\d{3})\s*\|", re.M)),
    "ASM": ("01-project/assumptions-and-risks.md", re.compile(r"^\|\s*(ASM-\d{3})\s*\|", re.M)),
    "ERR": ("07-data-and-contracts/error-catalog.md", re.compile(r"`(ERR-[A-Z]+-\d{3})`")),
    "OP": ("07-data-and-contracts/api-catalog.md", re.compile(r"^\|\s*(OP-[A-Z]{3}-\d{3})\s*\|", re.M)),
}

CITED = {
    "ADR": re.compile(r"\bADR-\d{3}\b"),
    "DEC": re.compile(r"\bDEC-\d{3}\b"),
    "FR": re.compile(r"\bFR-\d{3}\b"),
    "NFR": re.compile(r"\bNFR-\d{3}\b"),
    "TC": re.compile(r"\bTC-\d{3}\b"),
    "THR": re.compile(r"\bTHR-\d{3}\b"),
    "RSK": re.compile(r"\bRSK-\d{3}\b"),
    "ASM": re.compile(r"\bASM-\d{3}\b"),
    "ERR": re.compile(r"\bERR-[A-Z]+-\d{3}\b"),
    "OP": re.compile(r"\bOP-[A-Z]{3}-\d{3}\b"),
}


def definitions() -> dict[str, set[str]]:
    table = {}
    for kind, (owner, pattern) in OWNERS.items():
        table[kind] = set(pattern.findall(read(doc(owner))))
    return table


def check_identifiers(files: list[str], defined: dict[str, set[str]]) -> int:
    total = 0
    for path in files:
        text = read(path)
        for kind, pattern in CITED.items():
            for match in pattern.finditer(text):
                total += 1
                if match.group(0) not in defined[kind]:
                    error(
                        f"{rel(path)}:{text[: match.start()].count(chr(10)) + 1}",
                        f"{match.group(0)} is not defined in {OWNERS[kind][0]}",
                    )
    return total


# ---------------------------------------------------------------------------
# 4. Stated totals
# ---------------------------------------------------------------------------

def section(text: str, start: str, end: str | None = None) -> str:
    body = text.split(start, 1)[1]
    return body.split(end, 1)[0] if end else body


def config_variables() -> int:
    text = read(doc("11-operations/configuration-reference.md"))
    table = len(re.findall(r"^\|\s*`MARITES_", section(text, "## 2. The variables", "## 3."), re.M))
    kek = len(re.findall(r"^\|\s*`MARITES_KEK", text, re.M))
    return table + kek


def milestone_rows() -> int:
    text = read(doc("11-operations/deployment-and-onboarding.md"))
    return len(re.findall(r"^\|\s*M\d\s*\|", text, re.M))


def milestone_gates() -> int:
    text = read(doc("10-testing/verification-plan.md"))
    body = section(text, "## 3. Milestone gates", "## 4.")
    return len(re.findall(r"^\|\s*[A-Z][^|]*\|\s*TC-", body, re.M))


def operation_count(filename: str) -> int:
    """Count operations in a contract by their indentation, without a YAML parser."""
    text = read(doc(f"07-data-and-contracts/openapi/{filename}"))
    return len(re.findall(r"^\s{6}operationId:", text, re.M))


def package_count() -> int:
    text = read(doc("12-delivery/delivery-plan.md"))
    return len(set(re.findall(r"WP-M\d-\d+", text)))


def table_rows() -> int:
    text = read(doc("07-data-and-contracts/data-model.md"))
    return len(re.findall(r"^([a-z_]+)\s+\(", text, re.M))


def file_counts() -> dict[str, int]:
    files = markdown_files()
    indexes = [f for f in files if os.path.basename(f) == "README.md"]
    # A section index sits one level below docs/ (01-project/README.md); a nested index
    # sits deeper (05-architecture/diagrams/README.md); the root index sits at docs/.
    depth = {f: len(os.path.relpath(f, DOCS).split(os.sep)) for f in indexes}
    nested = [f for f in indexes if depth[f] > 2]
    section_indexes = [f for f in indexes if depth[f] == 2]
    return {
        "files": len(files),
        "indexes": len(indexes),
        "section_indexes": len(section_indexes),
        "nested_indexes": len(nested),
        "substantive": len(files) - len(indexes),
    }


def totals(defined: dict[str, set[str]]) -> dict[str, int]:
    files = file_counts()
    return {
        "codes": len(defined["ERR"]),
        "variables": config_variables(),
        "milestones": milestone_rows(),
        "cases": len(defined["TC"]),
        "milestone_gates": milestone_gates(),
        "threats": len(defined["THR"]),
        "tables": table_rows(),
        "operations": len(defined["OP"]),
        "served": operation_count("openapi.yaml"),
        "deferred": operation_count("openapi.deferred.yaml"),
        "packages": package_count(),
        "fr": len(defined["FR"]),
        "nfr": len(defined["NFR"]),
        "adrs": len(defined["ADR"]),
        "decisions": len(defined["DEC"]),
        "files": files["files"],
        "indexes": files["indexes"],
        "section_indexes": files["section_indexes"],
        "nested_indexes": files["nested_indexes"],
        "substantive": files["substantive"],
    }


WORD = {"two": 2, "eight": 8, "twelve": 12, "fourteen": 14, "forty-eight": 48}

# (path from the repository root, regex, total key, capture group)
CLAIMS: list[tuple[str, str, str, int]] = [
    # --- docs/README.md ---------------------------------------------------
    ("docs/README.md", r"(\d+) substantive documents", "substantive", 1),
    ("docs/README.md", r"([A-Za-z-]+) substantive documents", "substantive", 1),
    ("docs/README.md", r"(\d+) section indexes", "section_indexes", 1),
    ("docs/README.md", r"([A-Za-z-]+) section indexes", "section_indexes", 1),
    ("docs/README.md", r"(two) nested indexes", "nested_indexes", 1),
    ("docs/README.md", r"(\d+) Markdown files", "files", 1),
    ("docs/README.md", r"([A-Za-z0-9-]+) Markdown files", "files", 1),
    ("docs/README.md", r"(\d+) FR, \d+ NFR", "fr", 1),
    ("docs/README.md", r"\d+ FR, (\d+) NFR", "nfr", 1),
    ("docs/README.md", r"(\d+) DEC answered", "decisions", 1),
    ("docs/README.md", r"(\d+) ADRs", "adrs", 1),
    ("docs/README.md", r"([Tt]welve) tables", "tables", 1),
    ("docs/README.md", r"(\d+) served and (\d+) deferred", "served", 1),
    ("docs/README.md", r"(\d+) served and (\d+) deferred", "deferred", 2),
    ("docs/README.md", r"(\d+) cases, (\d+) gates", "cases", 1),
    ("docs/README.md", r"(\d+) cases, (\d+) gates", "milestone_gates", 2),
    ("docs/README.md", r"(\d+) cases, (eight|8) gates", "milestone_gates", 2),
    ("docs/README.md", r"(\d+) threats", "threats", 1),
    ("docs/README.md", r"(\d+) operations — (\d+) served, (\d+) deferred", "operations", 1),
    ("docs/README.md", r"(\d+) operations — (\d+) served, (\d+) deferred", "served", 2),
    ("docs/README.md", r"(\d+) operations — (\d+) served, (\d+) deferred", "deferred", 3),
    ("docs/README.md", r"\| (\d+) codes \|", "codes", 1),
    ("docs/README.md", r"(\d+) variables, none read", "variables", 1),
    ("docs/README.md", r"(\d+) milestones, (\d+) packages", "milestones", 1),
    ("docs/README.md", r"(\d+) milestones, (\d+) packages", "packages", 2),
    ("docs/README.md", r"(\d+) cases, 0 executed", "cases", 1),
    # --- readiness dashboard ---------------------------------------------
    ("docs/12-delivery/readiness-dashboard.md", r"(\d+) FR and (eight|8) NFR", "fr", 1),
    ("docs/12-delivery/readiness-dashboard.md", r"\d+ FR and (eight|8) NFR", "nfr", 1),
    ("docs/12-delivery/readiness-dashboard.md", r"([Tt]welve) tables", "tables", 1),
    ("docs/12-delivery/readiness-dashboard.md", r"\[(\d+) codes\]", "codes", 1),
    ("docs/12-delivery/readiness-dashboard.md", r"(\d+) threats, six planned", "threats", 1),
    ("docs/12-delivery/readiness-dashboard.md", r"\((\d+) variables\)", "variables", 1),
    ("docs/12-delivery/readiness-dashboard.md", r"(\d+) cases, (eight|8) gates", "cases", 1),
    ("docs/12-delivery/readiness-dashboard.md", r"(\d+) cases, (eight|8) gates", "milestone_gates", 2),
    ("docs/12-delivery/readiness-dashboard.md", r"covers (\d+) FR", "fr", 1),
    ("docs/12-delivery/readiness-dashboard.md", r"maps all (\d+) operations", "operations", 1),
    # --- data and contracts index ---------------------------------------
    ("docs/07-data-and-contracts/README.md", r"\| (\d+) codes with HTTP", "codes", 1),
    ("docs/07-data-and-contracts/README.md", r"([Tt]welve) current tables", "tables", 1),
    ("docs/07-data-and-contracts/README.md", r"(\d+) operations \((\d+) served, (\d+) deferred\)", "operations", 1),
    ("docs/07-data-and-contracts/README.md", r"(\d+) operations \((\d+) served, (\d+) deferred\)", "served", 2),
    ("docs/07-data-and-contracts/README.md", r"(\d+) operations \((\d+) served, (\d+) deferred\)", "deferred", 3),
    # --- delivery index --------------------------------------------------
    ("docs/12-delivery/README.md", r"(\d+) cases, (\d+) gates", "cases", 1),
    ("docs/12-delivery/README.md", r"(\d+) cases, (\d+) gates", "milestone_gates", 2),
    # --- portfolio overview ----------------------------------------------
    ("docs/01-project/portfolio-overview.md", r"(\d+) error codes", "codes", 1),
    ("docs/01-project/portfolio-overview.md", r"(\d+) FR, (\d+) NFR", "fr", 1),
    ("docs/01-project/portfolio-overview.md", r"(\d+) FR, (\d+) NFR", "nfr", 2),
    ("docs/01-project/portfolio-overview.md", r"(\d+) ADRs, (\d+) decisions", "adrs", 1),
    ("docs/01-project/portfolio-overview.md", r"(\d+) ADRs, (\d+) decisions", "decisions", 2),
    ("docs/01-project/portfolio-overview.md", r"(\d+) current tables", "tables", 1),
    # --- root index ------------------------------------------------------
    ("README.md", r"(\d+) substantive documents", "substantive", 1),
    ("README.md", r"(\d+) indexes", "indexes", 1),
]


def check_claims(defined: dict[str, set[str]]) -> int:
    known = totals(defined)
    checked = 0
    for relative, pattern, key, group in CLAIMS:
        path = os.path.join(ROOT, relative)
        if not os.path.exists(path):
            error(relative, "file named in CLAIMS does not exist")
            continue
        text = read(path)
        for match in re.finditer(pattern, text):
            checked += 1
            raw = match.group(group)
            value = int(raw) if raw.isdigit() else WORD.get(raw.lower())
            if value is None:
                error(
                    f"{relative}:{text[: match.start()].count(chr(10)) + 1}",
                    f'unrecognised number "{raw}" in "{match.group(0).strip()}"',
                )
                continue
            expected = known[key]
            if value != expected:
                error(
                    f"{relative}:{text[: match.start()].count(chr(10)) + 1}",
                    f'says "{match.group(0).strip()}", but {key} is {expected}',
                )
    return checked


# ---------------------------------------------------------------------------
# 5. One acceptance row per requirement
# ---------------------------------------------------------------------------

def check_acceptance_rows(defined: dict[str, set[str]]) -> int:
    path = doc("02-business/requirements.md")
    text = read(path)
    body = section(text, "### 3.1 Acceptance criteria", "## 4.") + section(
        text, "### 4.1 Acceptance criteria", "## 5."
    )
    rows = defaultdict(list)
    for match in re.finditer(r"^\|\s*((?:FR|NFR)-\d{3})\s*\|", body, re.M):
        rows[match.group(1)].append(match.group(0))
    checked = 0
    for kind in ("FR", "NFR"):
        for identifier in sorted(defined[kind]):
            checked += 1
            count = len(rows.get(identifier, []))
            if count == 0:
                error("docs/02-business/requirements.md", f"{identifier} has no acceptance row")
            elif count > 1:
                error("docs/02-business/requirements.md", f"{identifier} has {count} acceptance rows, expected 1")
    return checked


# ---------------------------------------------------------------------------
# 6. Retired names
# ---------------------------------------------------------------------------

RETIRED = {
    r"Marites\.(Storage|Operations|Capture|Identity)": "retired by ADR-025 (four layers)",
}


def check_retired(files: list[str]) -> int:
    checked = 0
    for path in files:
        text = read(path)
        for pattern, why in RETIRED.items():
            for match in re.finditer(pattern, text):
                checked += 1
                warn(
                    f"{rel(path)}:{text[: match.start()].count(chr(10)) + 1}",
                    f"{match.group(0)} is {why}",
                )
    return checked


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main() -> int:
    parser = argparse.ArgumentParser(description="Check the Marites documentation set for internal consistency.")
    parser.add_argument("--strict", action="store_true", help="treat warnings as errors")
    args = parser.parse_args()

    files = markdown_files()
    if not files:
        print("no Markdown files found under docs/", file=sys.stderr)
        return 2

    anchors = headings_by_file(files)
    defined = definitions()

    links = check_links(files, anchors)
    identifiers = check_identifiers(files, defined)
    claims = check_claims(defined)
    acceptance = check_acceptance_rows(defined)
    retired = check_retired(files)

    for finding in errors:
        print(f"ERROR  {finding}")
    for finding in warnings:
        print(f"WARN   {finding}")

    print(
        f"\n{len(files)} files · {links} links · {identifiers} identifier citations · "
        f"{claims} totals · {acceptance} requirements · {retired} retired-name uses"
    )
    print(f"{len(errors)} errors, {len(warnings)} warnings")

    if errors or (args.strict and warnings):
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
