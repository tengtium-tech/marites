---
document_id: DOC-DEL-003
title: CI and Release
product: Marites
status: Approved — Product owner, 2026-11-11
version: 0.1
date: 2026-11
---

# CI and release

What runs on a pull request, what runs on a tag, and what a release *is* for this product. The gates here are the same ones the [verification plan](../10-testing/verification-plan.md) names — this document is about *when* they run and what happens when they fail, not about what they assert.

## 1. What runs when

| Trigger | Stages | Target time |
|---|---|---|
| Pull request | Build · unit · contract validation · architecture test · docs checks | Under 10 minutes, or people stop waiting for it |
| Merge to `main` | The above, plus the contract conformance suite and the integration suite against a real broker | Under 30 minutes |
| Nightly | Performance (`perf-sustained`), the full scenario set on all three providers, dependency audit | Unbounded |
| Tag `v*` | Everything, plus the release build and the upgrade test | — |

**The split is deliberate.** A ten-minute pull-request pipeline gets run; a forty-minute one gets bypassed. The slow suites — performance, three-provider parity, upgrade — run where their cost is paid once.

## 2. Stages and gates

| Stage | Fails when | Blocks |
|---|---|---|
| Build | It does not compile, or a warning is treated as an error | Everything |
| Unit | A test fails, or coverage of a changed file drops below the floor | Merge |
| Contract validation | `openapi.yaml` or `openapi.deferred.yaml` does not validate, an operation is untagged, or a `$ref` does not resolve | Merge |
| Contract breaking-change check | A required field is added, a type narrows, or an operation is removed without a new version | Merge — the frontend is generated from this file |
| Architecture test | A project reference violates ADR-025's inward dependency rules | Merge |
| Docs checks | `tools/docs_check.py` exits non-zero: a relative link or anchor does not resolve, a cited identifier is not defined in its owner, a stated total disagrees with the file that owns it, a requirement lacks exactly one acceptance row, or a retired project name is used | Merge |
| Conformance suite | An implementation of the contract returns a shape the spec does not allow | Release |
| Integration | A case against a real broker fails — the [traffic harness](../10-testing/traffic-harness.md) seeds it | Release |
| Performance | `perf-sustained` falls below NFR-001, or the curve regresses against the last run | Release |
| Upgrade | A database at the previous version cannot be migrated forward | Release |

**The docs checks are a gate, not a courtesy.** The review has caught broken links, dead anchors and undefined identifiers three times; a rule that is only checked by hand is a rule that decays between reviews. The check itself now exists — `python tools/docs_check.py`, non-zero on a finding — but it runs by hand, because **no pipeline exists yet**. CI/CD is a later increment; until it lands, every stage in this table is a specification of what the pipeline will do, and this one is also a command a person can run today.

## 3. What must be green to merge

Build, unit, contract validation, the breaking-change check, the architecture test and the docs checks. Nothing else — the slow suites gate the release, and making them gate the merge would put a thirty-minute wait on a one-line fix.

**A red pipeline is never merged past.** No `--no-verify`, no skip flag, no "it's flaky". A flaky test is quarantined explicitly — removed from the gate, recorded in the dashboard, and fixed or deleted within the milestone — because a suite that is sometimes ignored is a suite that is always ignored.

## 4. Versioning and migrations

**Semantic versioning on the contract, not on the marketing.** A minor version may add operations and optional fields; a major version is required to remove one, to narrow a type, or to change what an existing field means.

| Rule | Why |
|---|---|
| Migrations are append-only | A released migration is never edited; a correction is a new migration |
| Migrations are forward-only | There is no down-migration. Rollback is restore ([upgrade and rollback](../11-operations/upgrade-and-rollback.md)) |
| The binary refuses to start against an older schema | `ERR-MIG-001` names the installed and required versions; running newer code on an older schema fails loudly rather than subtly |
| One migration set per store | The bookkeeping store and the message store migrate independently, because they are separate databases (DEC-012) |

## 5. Artifacts

| Artifact | Detail |
|---|---|
| Container image | `ghcr.io/<org>/marites:<version>`, multi-arch (`linux/amd64`, `linux/arm64`). `latest` is published but **never** recommended for production |
| Software bill of materials | CycloneDX, attached to the release |
| Checksums | For the self-contained binaries |
| Contract | The tagged `openapi.yaml` and `openapi.deferred.yaml`, so a client can pin to a version |
| Docs | The `docs/` tree at the same tag — the specification and the binary version together |

**The image is the deployment unit.** There is no installer, no package repository and no sidecar; §3 of [deployment and onboarding](../11-operations/deployment-and-onboarding.md) covers the three shapes it can take.

## 6. Release checklist

1. **Migrations rehearsed on a copy** of a database at the previous version — not on an empty one, which proves nothing about an upgrade.
2. **Upgrade test green** from the previous minor to this one.
3. **The dashboard updated** — no milestone claimed that the gates did not earn.
4. **The release notes written** in the same voice as the specification: what changed and why, not a list of commits.
5. **The contract diff read** — every removed or narrowed field accounted for.
6. **Rollback stated** — what an adopter does if this release is wrong, with the restore path tested.

## 7. Rollback

Not a CI concern, and not duplicated here: [upgrade and rollback](../11-operations/upgrade-and-rollback.md) owns it. The one rule this document adds is that **a release is not published until its rollback has been performed once** on a test instance.

## 8. Document control

Revision 0.1, November 2026. Written as specification: no pipeline exists, no image is published, and the timings in §1 are targets rather than measurements. The gate list is derived from the verification plan and the review's mechanical checks; the versioning rules follow from the contract being the product boundary (ADR-006).
