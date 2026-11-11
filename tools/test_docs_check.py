#!/usr/bin/env python3
"""Regression test for tools/docs_check.py.

The checker is only useful if it still fails when the documents break. This test
copies the set into a temporary directory, injects one fault at a time, and asserts
that the checker reports it. It deliberately asserts on the specific finding rather
than on a clean exit, because a copy without prototype/ legitimately reports the
wireframe image links as missing.

    python tools/test_docs_check.py
"""

from __future__ import annotations

import os
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, ".."))
CHECKER = os.path.join(HERE, "docs_check.py")

FAULTS = [
    (
        "stale error-code total",
        "docs/README.md",
        "| 27 codes |",
        "| 26 codes |",
        "but codes is 27",
    ),
    (
        "broken in-page anchor",
        "docs/01-project/decisions-and-open-questions.md",
        "(#dec-016--running-without-storage)",
        "(#dec-016--no-such-heading)",
        "anchor does not resolve",
    ),
    (
        "undefined error code",
        "docs/11-operations/configuration-reference.md",
        "`ERR-MIG-001`",
        "`ERR-MIG-002`",
        "ERR-MIG-002 is not defined",
    ),
    (
        "stale milestone total",
        "docs/README.md",
        "8 milestones, 30 packages",
        "7 milestones, 30 packages",
        "but milestones is 8",
    ),
    (
        "missing link target",
        "docs/README.md",
        "(02-business/requirements.md)",
        "(02-business/no-such-file.md)",
        "link target does not exist",
    ),
    (
        "invented status word",
        "docs/02-business/requirements.md",
        "status: Approved",
        "status: Final",
        "is not one of the controlled words",
    ),
]


def run(checker_dir: str) -> tuple[int, str]:
    result = subprocess.run(
        [sys.executable, os.path.join(checker_dir, "tools", "docs_check.py")],
        capture_output=True,
        text=True,
        cwd=checker_dir,
    )
    return result.returncode, result.stdout + result.stderr


def copy_set(destination: str) -> None:
    shutil.copytree(os.path.join(ROOT, "docs"), os.path.join(destination, "docs"))
    shutil.copytree(os.path.join(ROOT, "tools"), os.path.join(destination, "tools"))
    shutil.copy2(os.path.join(ROOT, "README.md"), os.path.join(destination, "README.md"))


def main() -> int:
    failures = 0

    with tempfile.TemporaryDirectory() as workspace:
        copy_set(workspace)
        code, output = run(workspace)
        baseline_ok = "but " not in output and "is not defined" not in output
        print(f"{'PASS' if baseline_ok else 'FAIL'}  clean copy reports no total or identifier fault")
        if not baseline_ok:
            failures += 1
            print(output)

    for name, relative, old, new, expected in FAULTS:
        with tempfile.TemporaryDirectory() as workspace:
            copy_set(workspace)
            path = os.path.join(workspace, relative)
            with open(path, encoding="utf-8") as handle:
                text = handle.read()
            if old not in text:
                print(f"FAIL  {name}: fixture text not found in {relative}")
                failures += 1
                continue
            with open(path, "w", encoding="utf-8") as handle:
                handle.write(text.replace(old, new, 1))
            code, output = run(workspace)
            caught = expected in output and code == 1
            print(f"{'PASS' if caught else 'FAIL'}  {name}")
            if not caught:
                failures += 1
                print(f"      expected {expected!r}, exit 1; got exit {code}")
                print("      " + "\n      ".join(output.splitlines()[:6]))

    print(f"\n{len(FAULTS) + 1 - failures} of {len(FAULTS) + 1} checks passed")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
