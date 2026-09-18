# S-2 §10 reproducibility probe — formal result (PASS under owner ruling `F-1`/`D-6`, Reading A)

**Date:** 2026-09-18
**Author:** agent, documentation/evidence-maintenance session
**Baseline:** `origin/dev` `6556923ffb283b47183afda865923b575a00ba2`; worked in an isolated worktree
**Depends on:** [`2026-09-18-epic4-s2-state-reconstruction.md`](2026-09-18-epic4-s2-state-reconstruction.md)
(the probe as performed, computed under both readings) and its §12 (the owner's `F-1` ruling,
recorded in full as `F-1` in
[`../plans/2026-09-05-s2-owner-decisions.md`](../plans/2026-09-05-s2-owner-decisions.md))

---

## §0. Verdict

**§10 reproducibility probe: PASS under owner ruling `D-6` / `F-1` (Reading A).**

This is a **Data Maturation / S-2 prerequisite milestone, not the canonical Epic 4.** Canonical
Epic 4 (`T4.1`–`T4.3`, ML Maturation) has **not started** — see §5. This result is **not** a
corpus-wide agreement claim, **not** an ML accuracy result, **not** a dataset validation result, and
**not** a guarantee of any future annotator's behavior — see §4's scope statement, which travels with
every figure below.

The full result never reduces to a bare "20/20" — every figure in §1–§3 is required to travel
together with it, per `D-7` and `G4`/`G2`/`C4`.

---

## §1. Scope

What this report formally records:

- the §10 result, both readings, and which one now governs (Reading A, per `F-1`);
- the complete set of mandatory companion figures (`G4`, `G2`, `C4`, `D-2`) at both the 20-row and
  primary 16-row grain;
- the independence/custody facts behind the probe, graded by how they are known;
- `DFD-2`'s resulting status is **not** decided here — see §5, which points to the freeze record.

What it does not do — same exclusions as the state-reconstruction report:

| Not done | Why |
|---|---|
| Re-derive or re-score any row | Would be a third pass; contaminates the record |
| Fill `R-12` / `R-20` | Both parties independently declared them unresolved; that stands |
| Edit either annotation sheet | Primary evidence; immutable |
| Move the evidence sheets out of `docs/s2-reader-package/` | Needs a new owner decision (`F-4`); not taken here — see §6 |
| Re-run the test suite / GitNexus | No production code is in scope |

---

## §2. The ruling that resolves the probe

The owner ruled, verbatim (recorded in full as `F-1` in
[`../plans/2026-09-05-s2-owner-decisions.md`](../plans/2026-09-05-s2-owner-decisions.md)):

> D-6 = Reading A.
>
> When the Owner Gold annotation and the independent human reader both record `unresolved=true` for a
> row because Guideline v1 cannot resolve that row, that shared `unresolved` outcome counts as an
> exact agreement for §10 reproducibility scoring.
>
> Therefore the §10 human probe result is PASS under Reading A.

**Post-hoc, and recorded as such.** `D-6` left this question deliberately unauthorised *before* the
probe ran — an honest-instrument choice, not an oversight — so the ruling could only be taken after
both passes produced the situation it anticipated. `S-2.7`'s no-post-hoc-adjustment invariant governs
thresholds and procedure that were *already fixed*; it does not retroactively forbid ruling a question
that was openly left open. This report does not re-litigate that call — it is the owner's to make and
has been made.

**What `F-1` does not do,** restated because it bounds everything below: it does not touch `v1`, the
sealed batch, the `D-7` thresholds, the primary 16-row composition, or either annotator's record;
`R-12` and `R-20` remain `unresolved=true`, unedited, in both sheets; it does not authorise
re-annotation or a repeat of the probe.

---

## §3. The result, with every required companion

### §3.1 Per-dimension scoring — both readings, for context; Reading A governs

**Reading A** (governs, per `F-1`) — an agreed `unresolved` counts as an exact match:

| Set | TaskType | Threshold | | Difficulty | Threshold | |
|---|---|---|---|---|---|---|
| 20-row gate | **20/20** | ≥ 17/20 | **PASS** | **20/20** | ≥ 18/20 | **PASS** |
| 16-row primary | **16/16** | ≥ 14/16 | **PASS** | **16/16** | ≥ 15/16 | **PASS** |

Reading B (superseded by `F-1`; recorded so the fork `D-6` decided is not lost) — only a substantive
label can exact-match, denominator unchanged:

| Set | TaskType | Threshold | | Difficulty | Threshold | |
|---|---|---|---|---|---|---|
| 20-row gate | 18/20 | ≥ 17/20 | PASS | 18/20 | ≥ 18/20 | PASS |
| 16-row primary | 14/16 | ≥ 14/16 | PASS (zero margin) | 14/16 | ≥ 15/16 | **FAIL** |

Reading B's 16-row Difficulty figure is what made `D-6` outcome-determinative in the first place —
three of four dimensions passed under either reading; `F-1` settles the fourth.

### §3.2 `G4` — batch composition

Matches pre-registration exactly: **12 contested** (`cross_only` 1 · `third_only` 9 · `both` 2) +
**8 Difficulty-spread** (L1 1 · L2 1 · L3 2 · L4 2 · L5 2).

### §3.3 `G2` — per-stratum breakdown, Reading A

| Stratum | n | TaskType | Difficulty | Agreed-unresolved |
|---|---|---|---|---|
| contested / `both` | 2 | 2/2 | 2/2 | — |
| contested / `cross_only` | 1 | 1/1 | 1/1 | — |
| contested / `third_only` | 9 | 9/9 | 9/9 | — |
| Difficulty-spread L1 | 1 | 1/1 | 1/1 | `R-20` |
| Difficulty-spread L2 | 1 | 1/1 | 1/1 | — |
| Difficulty-spread L3 | 2 | 2/2 | 2/2 | `R-12` |
| Difficulty-spread L4 | 2 | 2/2 | 2/2 | — |
| Difficulty-spread L5 | 2 | 2/2 | 2/2 | — |

Every applicable stratum is exact agreement under Reading A. `R-20` sits in stratum L1; `R-12` sits in
stratum L3.

### §3.4 `C4` — per-boundary detail (labelled rows)

`B-1` **11/11** · `B-2` **1/1** · `B-4` **2/2** · `B-5` **1/1** · `B-6` **3/3**.

**`B-3` was never exercised** — no row in the sealed batch triggers it. This result says nothing
about `B-3`; it is not scored, not passed, not failed. See §7 for where this belongs long-term.

### §3.5 `D-2` diagnostic

`shared_label_cross_pass` **3/3** · `third_pass_forced` **9/9** — matches the pre-registered 3 + 9
exactly.

### §3.6 Difficulty span actually exercised

Assigned Difficulty values in the tested batch span only **{3, 4, 5}**. `v1`'s reproducibility
threshold governs L3–L5 on that basis. **L1 and L2 remain the declared authored-example gap** — the
two agreed-unresolved rows (`R-20` at L1, `R-12` at L3) do not close that gap; an unresolved row
contributes no substantive Difficulty label at any level. This must not be silently represented as
validated L1/L2 coverage.

### §3.7 Scope statement — travels with every quotation of this result

> This demonstrates reproducibility of Guideline v1 as rendered in Vietnamese against the Owner Gold
> on the sealed 20-row test batch. It is not a corpus-wide agreement claim, ML accuracy result,
> dataset validation result, or guarantee of future annotator behavior.

---

## §4. Independence and custody

This section distinguishes **owner attestation** (the owner's statement, no independent repository
record), **repository evidence** (something checked directly), and **inference** (a plausible
reading of circumstantial evidence) — per `docs/reports/README.md`'s claim-scoping rule. It is
compiled by an agent from what the repository holds plus what the owner has stated in this
conversation; it is **not** a substitute for an owner-authored evidence record, and does not claim to
be primary evidence itself.

| Fact | Grade | Basis |
|---|---|---|
| An independent human reader participated | `[owner attestation]` | No recruitment record exists anywhere in `docs/` |
| The reader used the supplied frozen guideline and sealed reader sheet | `[repository evidence]` | Returned sheet's 20 item blocks are byte-identical to the `b9691ae` materialisation (validator `C4`); all 20 rows hash into the sealed `scored_batch` |
| The reader did not participate in drafting the guideline | `[owner attestation]` | Not independently checkable from repository state |
| The reader did not search project data | `[owner attestation]` | Not independently checkable |
| The reader did not discuss the annotations with the owner before completing the sheet | `[owner attestation]` | Not independently checkable |
| The probe was performed independently | `[owner attestation]`, consistent with `[repository evidence]` | The reader-facing sheet carries none of the four excluded categories (row hash, locator, historical label, catalogue reference — validator `C9`/`C10`), so nothing in the issued instrument itself could have primed agreement |
| Gold vs. independent-reader role | `[repository evidence]` | `02-annotation-sheet-gold.md` (untracked) is the owner's Gold pass; `02-annotation-sheet.md` (modified, uncommitted, `annotator: Hoai nam`, `date: 12/09/2026`) is the returned independent pass |
| Date of the independent pass | `[repository evidence]`, weakly corroborated | In-sheet date field reads `12/09/2026`. File mtime is `[inference]` only — the gold sheet's mtime predates it and the returned sheet's mtime is today's, neither of which dates the actual pass |
| Neither sheet declares a `role: gold \| probe` field | `[repository evidence]` | Checked directly; the field is required by `v1` §12 and present in the English blind-reader sheet spec (`docs/specs/2026-09-05-s2-blind-reader-sheet.md`), absent from both Vietnamese sheets as issued |

**What the repository lacks, stated plainly:** formal recruitment paperwork for the independent
reader, and the `role` field the instrument specification requires. Neither gap changes the scoring
result — the field-level and hash-level checks above are independent of it — but it is the reason
independence rests on attestation rather than on a written record, and a future reader should not
infer stronger documentary evidence than this table shows.

**This report does not attempt to close those gaps** by editing the sheets (forbidden — §6/§7 of the
governing task) or by fabricating an owner-authored evidence record. If the owner wants a formal,
owner-authored custody record for these three attested facts, that is a new document only the owner
can author; this table is the closest an agent can honestly get.

---

## §5. `DFD-2`

**Recorded separately, not here**, because it requires reading what the governing Data Maturation
documents explicitly authorize after a §10 PASS — a narrower question than restating this result.

See the appended section in
[`../plans/2026-09-04-s2-v1-freeze-record.md`](../plans/2026-09-04-s2-v1-freeze-record.md)
("Gates — status update, appended 2026-09-18").

**Summary only** (full reasoning at that link): the freeze record's own condition for `DFD-2` —
"satisfied only after the §10 test passes" — is now met. That is a narrow, single-gate statement.
It does **not** by itself authorize `S-3` provenance/lineage implementation, labelled-data
collection/import/generation, data promotion, or any other Data Maturation stage — those carry their
own separate, still-open prerequisites, named at that link rather than inferred here.

---

## §6. Reader-package hygiene

**Not changed by this report.** The reconstruction report's `5.6` found the working-tree package
fails its own validator (`C1`, `C4`, `C12` — 3 of 18) purely because the two probe artifacts
(`02-annotation-sheet-gold.md`, and the filled-in `02-annotation-sheet.md`) sit inside
`docs/s2-reader-package/`, which is meant to hold only the blank 4-file instrument. Rebuilding the
package from committed blobs gives **18/18 PASS** — re-verified in this session's isolated worktree,
same result.

**This is left exactly as found.** Moving the evidence sheets to a different location, or restoring
`docs/s2-reader-package/` to its blank form, requires deciding *where* returned evidence should live
— a new evidence-location policy the owner has not yet set (`F-4` in the reconstruction report). That
decision is not taken here. **No files were moved, and neither sheet was touched.**

---

## §7. Findings that outlive this decision

Carried forward from the reconstruction report's §7, unchanged, because this report does not
supersede that section:

1. `B-3` has never been exercised by any row in the sealed scored batch. Whatever §10 concludes, it
   concludes nothing about `B-3`.
2. `R-12` and `R-20` being agreed-unresolved by two independent passes is evidence about the
   specification, not about either annotator. That finding survives the `D-6` ruling either way.
3. A pre-registered instrument can still leave an outcome-determinative question open — `D-6` was a
   deliberate, well-reasoned silence that became the deciding variable, resolved only after the fact.
4. The validator (`tools/data-maturation/s2_reader_package_validate.py`) caught the package-hygiene
   regression immediately, and its `C11` check independently confirmed the `D-6` silence held in the
   shipped instrument.

---

## §8. Verification

All checks in this session were read-only against evidence; nothing was recomputed by editing either
sheet.

| Check | Result |
|---|---|
| `sha256` of `02-annotation-sheet-gold.md` (owner's checkout, live) | `d7102896b066c58ca60bd2d9b0d34728e79fa193a90706550ea66eeff6c1472a` — matches the reconstruction report's §6 recorded value |
| `sha256` of `02-annotation-sheet.md` (owner's checkout, live) | `e3bf1102262bad4c47a3586515f1bde74eaf778e1af4e32215752715be0460d7` — matches the reconstruction report's §6 recorded value |
| `unresolved` field, both sheets | `R-12` and `R-20` read `true`; all other 18 rows read `false`, in both sheets — re-checked directly, not re-derived from the report |
| `python tools/data-maturation/s2_reader_package_validate.py`, committed package in this worktree | **18/18 PASS** — matches the reconstruction report's §6 finding for the clean-baseline case |
| `git fetch origin` + baseline check | worktree branched from `origin/dev` `6556923`; owner's local `dev` was 3 commits behind `origin/dev` at session start — unrelated to the probe evidence (both sheets are uncommitted/untracked working-tree files, independent of which branch tip local `dev` points at), flagged per this repository's standing git-workflow convention, not investigated further here |

**Not run:** the working-tree validator against the owner's dirty checkout (would only reproduce the
already-established 3-fail finding at §5.6/§6 above); the test suite; GitNexus re-analyze. No
production code is in scope.

---

## §9. Follow-ups

| # | Item | Status | Owner | Where it belongs |
|---|---|---|---|---|
| F-3 | Formal owner-authored custody record (recruitment, role field) | recommendation, not taken here | Owner | New evidence record if the owner wants one; §4 above is the agent-compiled substitute |
| F-4 | Decide where the two annotation sheets should live, and whether/when to commit them | **needs a new owner decision** | Owner | Evidence location outside `docs/s2-reader-package/` |
| F-5 | Restore `docs/s2-reader-package/` to the blank 4-file instrument once `F-4` is settled | deferred until `F-4` | Owner / agent | Working tree |
| F-6 | Lift the `B-3` gap and the §7 lessons into `docs/knowledge/` | knowledge only, not taken here | Agent | `docs/knowledge/` |

`F-1` and `F-2` from the reconstruction report are **closed by this report and its companion `F-1`
entry** in `2026-09-05-s2-owner-decisions.md`.

---

## §10. Decisions made

### 10.1 Record the ruling in the D-series' own file, not only as a report appendix

**Why it had to be made.** The reconstruction report reserved its own §12 for the ruling and the
task that produced this report asked for it to be recorded "as a new post-hoc ruling." Both a report
appendix and a new owner-decisions entry were legitimate homes per `docs/reports/README.md`'s stated
default ("may be appended here" — permissive, not exclusive).

**What it's for.** `D-6`–`D-9` all live in `2026-09-05-s2-owner-decisions.md`; a reader who finds
`D-6` there and stops, without also opening the state-reconstruction report, would otherwise never
learn it was resolved. Putting the full ruling and its effects in that file, with a short pointer from
the report's §12, keeps the D-series self-contained and avoids two independently-editable copies of
the same verbatim owner text.

**Experience for future development.** When a convention offers two valid homes for the same
decision, pick the one an unrelated future reader is more likely to land on first, and cross-link
rather than duplicate the full text.

### 10.2 Keep the full companion figures in this report rather than only pointing at the reconstruction report

**Why it had to be made.** The governing task explicitly forbade recording the result as a bare
"20/20" and listed every `G2`/`G4`/`C4`/`D-2` figure that must travel with it. The reconstruction
report already carries all of them.

**What it's for.** A result record that only points elsewhere for its own required companions is one
broken link away from becoming exactly the bare figure the discipline exists to prevent. Restating
them here, sourced from the same verified numbers, means this document is self-sufficient as *the*
formal result record.

**Experience for future development.** A "formally record the result" task is not satisfied by a
cross-reference to where the number was first computed; the companions travel with the citation, not
just with the original computation.

### 10.3 Fold independence/custody into this report instead of authoring a separate "evidence record"

**Why it had to be made.** The governing task asked for a separate owner-authored observation record
"if appropriate," and `docs/reports/README.md` defines evidence records as "usually owner-authored" —
this session is an agent, not the owner.

**What it's for.** Writing a document styled as primary evidence, but agent-authored, would misstate
its own provenance. §4 above uses the report format's own fact/inference/attestation grading instead,
which the reconstruction report already established as the house style for exactly this situation.

**Experience for future development.** "Create an evidence record" is not always satisfiable literally
by an agent; when the convention ties that document type to a specific author, the honest substitute
is a clearly-graded section inside a document the agent is allowed to author, not a same-named
document with the wrong provenance.

### 10.4 Leave reader-package hygiene untouched

**Why it had to be made.** The governing task allowed housekeeping only if "obvious and safe," and
required a STOP if it would need a new evidence-location policy. The reconstruction report's `F-4`
already identified exactly that requirement.

**What it's for.** Moving the two uncommitted/untracked evidence files — the only copies of this
probe's raw result — without an owner ruling on where returned evidence should live risks losing or
misplacing the one asset this whole task exists to protect.

**Experience for future development.** "The validator would pass if X moved" is not the same evidence
as "the owner has decided X is where evidence lives." The first is a diagnosis; the second is a
decision this report does not have standing to take.

---

## §11. Do not repeat

Same list as the reconstruction report's §11 — unchanged, not restated in full. See
[`2026-09-18-epic4-s2-state-reconstruction.md`](2026-09-18-epic4-s2-state-reconstruction.md)§11.
