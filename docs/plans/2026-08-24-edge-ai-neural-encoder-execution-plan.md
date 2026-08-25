# Edge AI Neural Encoder — Execution Plan

**Date:** 2026-08-24 · **Status:** **`closed`** · **Lifecycle:** **`stopped_at_s0`** — S0 executed,
initiative STOPPED at S0 (owner ruling, 2026-08-25)
**Outcome:** [`../reports/2026-08-25-encoder-pilot.md`](../reports/2026-08-25-encoder-pilot.md) — EVA-16 kill criterion fired; S1–S4 **cancelled, not entered**

> **Read this before reading the plan.** Only **Phase S0** was ever executed. Everything from
> **WP-1.0 onward describes work that never happened** and is retained as the design it was, not as a
> record of anything built. The stop was a designed outcome (STOP-1, PD-3), not a failure of the plan.
**Implements:** [`../specs/2026-08-24-neural-encoder-smart-parser.md`](../specs/2026-08-24-neural-encoder-smart-parser.md) (RATIFIED 2026-08-24, `stopped_at_s0`)
**Reasoning / history:** [`2026-08-24-edge-ai-encoder-adoption.md`](2026-08-24-edge-ai-encoder-adoption.md) (approved proposal, `stopped_at_s0`)
**Durable lessons:** [`../knowledge/ml-experimentation.md`](../knowledge/ml-experimentation.md)
**Ratification record:** [`2026-08-24-edge-ai-encoder-owner-decision-handoff.md`](2026-08-24-edge-ai-encoder-owner-decision-handoff.md) (PD-1 … PD-10)

> **Precedence.** The specification governs *what must be true*. The proposal governs *why*. **This
> document governs *how*, and nothing else.** Where this plan appears to say something the
> specification does not, the specification wins and this plan is wrong.
>
> **This plan adds no scope.** Every work package below traces to a requirement ID already in the
> ratified specification. No new capability, threshold, encoder, route, or deliverable is introduced.

---

## Required-section map

Per [`README.md`](README.md): **Goal** → §1 · **Status** → header + §1 · **Slice list** → §5, §6 ·
**Pre-edit checklist** → §8.1 · **Acceptance gates** → §8.2 · **Out of scope** → §15.

---

# 1. Executive summary

**Goal.** Replace the featurizer of the M8-A task-type classifier with a frozen, bundled, locally
executed neural sentence encoder — and re-derive the confidence gate that reads its output — without
weakening the heuristic-first, offline-first, deterministic-fallback guarantees the product already
holds. Shipping this looks like: Smart Add classifies `tgk giải tích tuần sau má ơi cứu` correctly,
the displayed confidence means what it says, and deleting every model file still leaves a fully
working application.

**Status.** **`closed` · `stopped_at_s0` — S0 executed, initiative STOPPED at S0** (owner ruling,
2026-08-25; EVA-16).
S-SPEC is **executed** (`d141db1`) and S0 **ran**; **S1–S4 were cancelled and never entered.** EVA-01
held throughout and now permanently: no file under `SmartStudyPlanner/` was created or modified.

> *As written 2026-08-24 — superseded:* `draft`. *S0 is dispatchable now and is the next action.
> Nothing downstream of S0 may begin until the S0 report is owner-accepted (EVA-01).* The report was
> accepted, and under EVA-16 acceptance meant **stop**, so nothing downstream ever began.

**Shape of the work.** Four phases, strictly sequential, separated by three owner checkpoints:

| Phase | What it produces | User-visible change | Entered by |
|---|---|---|---|
| **S0** | Evidence. Throwaway harnesses, one report, one ruling. **No production code.** | None | Nothing — ready now |
| **S1** | An infrastructure seam. Encoder can exist; nothing consumes it. | **None, by requirement** (REL-01) | S0 report owner-accepted |
| **S2 + S3** | **One release unit.** Featurizer swap + recalibrated dual-signal gate. | **Yes** — classification, routing, and displayed confidence all change | S1 complete |
| **S4** | Runtime tiering + bundled distribution. | Tier visibility, Tier 2 opt-in | **Owner checkpoint** (OP-1, OP-6) |

**The one thing this plan is built to prevent.** S0 exists so the initiative can die cheaply. A null
result is a complete, valid, successful outcome of S0 (PD-3). Every task in Phase S0 is written so
that stopping after it costs one throwaway harness and one report — no dependency added, no
production symbol touched, no packaging built.

**What this plan deliberately does not decide.** The winning encoder (OP-9), the tokenization route
(OP-8), the threshold value (OP-10), the memory ceiling (OP-4), the size cap (OP-1), and the delivery
mechanism (OP-6). Each is a `[gate]` or an owner decision in the specification. Where an
implementation task depends on one, this plan writes **the derivation procedure and both concrete
branches** — never a guessed value. See §16.

---

# 2. Frozen scope reference

## 2.1 The contract this plan may not move

Reproduced as a checklist so a fresh session can audit itself without re-reading the specification.
The specification remains the normative source; if this list and §3/§4/§10 of the spec disagree, the
spec governs.

| # | Frozen requirement | Spec |
|---|---|---|
| 1 | Encoder is **frozen and pretrained**; weights immutable in the shipped app | ARC-01 |
| 2 | **No fine-tuning** at runtime or on-device, under any trigger | ARC-02 |
| 3 | Encoder is a **feature extractor only** — never an autonomous decision-maker | ARC-03 |
| 4 | Decision layer stays **linear/deterministic** and authoritative for label + confidence | ARC-04 |
| 5 | **Model-artifact governance**: one deployed artifact against the §10 cap; heads don't each count | ARC-07 |
| 6 | **Per-head owner approval**, whatever the artifact count permits | ARC-08 |
| 7 | **Offline-first**: no network operation anywhere in the ML layer, ever | AST-01, ARC-06 |
| 8 | **Bundled model policy**: present after normal install, no runtime acquisition path | AST-02, AST-03 |
| 9 | **CPU-first**: CPU EP is the baseline and default; DirectML is acceleration only | AST-07, PRF-02 |
| 10 | **Optional DirectML**, opt-in, gated on a CPU-parity check | AST-08 |
| 11 | **Tier 0 stays functional and tested** — a fault-tolerance state, not an install variant | AST-09 |
| 12 | **S0 is a hard gate**: no production code before the report is owner-accepted | EVA-01 |
| 13 | **S2 + S3 are one production release unit**; the uncalibrated intermediate must never exist | CNF-09, REL-02 |
| 14 | **S5 / S6 deferred**, and not activated by the encoder's acceptance | REL-04, §1.3 |
| 15 | Encoder binary **never enters git** | AST-05 |
| 16 | Encoder resolves from a **read-only** location distinct from the writable artifact store | AST-06 |
| 17 | Quick-parse **structured output contract unchanged**, provenance preserved | BEH-07 |
| 18 | Inference **once per explicit submit**, never per keystroke | BEH-02 |
| 19 | Encoder load **never blocks startup**; lifecycle exception never fails startup | BEH-11, FLB-02 |
| 20 | **500 ms** submit-to-populate ceiling on the PRF-01 class, CPU EP, model loaded | PRF-04 (PD-12) |

## 2.2 Divergences between proposal and specification — ruled, not reconciled silently

Three places where the proposal's text does not match the ratified specification. **In all three the
specification governs**, per its own precedence clause. None is a contradiction requiring owner
resolution; each is recorded so a fresh session does not "fix" the plan back toward the proposal.

| # | Proposal says | Spec says | Ruling |
|---|---|---|---|
| **D-1** | §2.1 *"Chosen: EmbeddingGemma-300M, int8 ONNX"* — a recommendation, labelled `[R]` | §3.2: *"This specification deliberately names **no winner**"* `[gate]`; OP-9 open | **Spec governs.** This plan names no winner. The proposal's recommendation is a prior, not a selection. Any task that reads as pre-selecting an arm is a defect in this plan |
| **D-2** | §S4 file map: *"Modify `LocalModelStorageProvider.cs` / `IModelStorageProvider.cs` — locate encoder assets"* | AST-06: encoder location MUST be **distinct** from the writable trained-artifact store, and MUST NOT create directories | **Spec governs.** A separate locator type is introduced (WP-1.3); `IModelStorageProvider` is not extended. The proposal's own §1.8 reaches the same conclusion — its S4 file map is the stale line |
| **D-3** | Handoff: *"The proposal is still a draft… NOT scope-frozen and implementation must NOT begin from this handoff"* | Spec header + proposal §11.1: S-SPEC → S3 **scope-frozen and activated 2026-08-24**; S0 dispatchable now | **Superseded, not contradictory.** The handoff is the round-1 record; rounds 2–3 and the 2026-08-24 ratification post-date it. A fresh session hitting that sentence should not stall — S0 is authorised |

**One additional precedence note already settled by the spec** (§2.4): `ML_Heuristic_design.md` §5.1
calls the Smart Parser *"ML-first"* while §6 makes ML advisory. **§6 governs.** No task in this plan
expands the parser's ML surface beyond task-type classification.

**DOC-01 carries no forward obligation.** It was **RESOLVED 2026-08-24** — six sites across four
documents were reconciled against the specification, each narrowed and dated (spec §15). This plan
inherits none of that work. What it *does* inherit are **DOC-03** (architecture documents updated when
S2+S3 ships, not before) and **DOC-04** (no further amendment as a side effect of implementation) —
both owned by WP-2.7 and verified by AC-32.

---

# 3. Gate structure

```
                     S-SPEC  ✅ EXECUTED (d141db1)
                        │
                        ▼
┌───────────────────────────────────────────────────────────┐
│  PHASE S0 — Offline pilot.  No production code. (EVA-01)  │
│  WP-0.1 … WP-0.8                                          │
└───────────────────────────────────────────────────────────┘
                        │
                 ╔══════▼══════════════════════════════════╗
                 ║  ⛔ OWNER CHECKPOINT 1  —  WP-0.9       ║
                 ║  Accept or reject the S0 report.        ║
                 ║  REJECT / NULL / KILL  →  INITIATIVE     ║
                 ║  STOPS. Valid, complete outcome (PD-3). ║
                 ╚══════╤══════════════════════════════════╝
                        │ accepted, with a declared winner
                        ▼
┌───────────────────────────────────────────────────────────┐
│  PHASE S1 — Encoder infrastructure seam.                  │
│  No user-visible behaviour change. (REL-01)               │
│  WP-1.0 … WP-1.6                                          │
│    └─ WP-1.1 = ⛔ OWNER CHECKPOINT 2 (conditional):       │
│       shared-ML-package blast radius, BEFORE the           │
│       dependency commit (TOK-07, AC-08)                    │
└───────────────────────────────────────────────────────────┘
                        │ S1 merged, suite at baseline
                        ▼
┌───────────────────────────────────────────────────────────┐
│  PHASE S2+S3 — ONE PRODUCTION RELEASE UNIT (CNF-09)       │
│  Single branch. Single PR. The neural path stays          │
│  unreachable in production until the FINAL commit.        │
│  WP-2.1 … WP-2.7                                          │
└───────────────────────────────────────────────────────────┘
                        │ released
                        ▼
                 ╔══════▼══════════════════════════════════╗
                 ║  ⛔ OWNER CHECKPOINT 3  —  WP-4.0       ║
                 ║  OP-1 size cap · OP-6 delivery          ║
                 ║  mechanism · OP-4 memory ceiling.       ║
                 ║  Blocking: S4 writes no packaging until ║
                 ║  the cap has a value (AC-20).           ║
                 ╚══════╤══════════════════════════════════╝
                        ▼
┌───────────────────────────────────────────────────────────┐
│  PHASE S4 — Runtime tiering & distribution.               │
│  WP-4.1 … WP-4.4                                          │
└───────────────────────────────────────────────────────────┘
                        │
                        ▼
              S5 / S6 — NOT ACTIVATED.
              Each needs its own owner approval AND
              (S5) a DifficultyLabelLogs count against
              the deferred proposal's trigger. §15.
```

## 3.1 Stop conditions — where the initiative legitimately ends

| ID | Condition | Where detected | Consequence |
|---|---|---|---|
| **STOP-1** | Both encoder arms fail to beat baseline macro-F1 beyond run-to-run variance | WP-0.8 | **Initiative stops at S0.** Kill criterion, stated in advance (EVA-16) |
| **STOP-2** | An arm wins accuracy but has **no verified tokenization route** | WP-0.7 | That arm is **rejected regardless of accuracy** (TOK-05). If no arm survives → STOP-1 |
| **STOP-3** | A and B cannot be reliably distinguished | WP-0.8 | **No winner is declared** (EVA-15). Owner decides: Arm C / data expansion / stop / defer. Not a failure |
| **STOP-4** | Latency or peak memory outside the §7 budget for every arm | WP-0.7 → WP-0.8 | Fails EVA-14 dimension 4 → no winner → STOP-1 branch |
| **STOP-5** | Owner rejects the S0 report | WP-0.9 | Initiative stops (PD-3) |
| **STOP-6** | S0-selected tokenization route proves unworkable during S1 | WP-1.4 | **No silent substitution** (TOK-06). Use the other *verified* route for that candidate if one exists; else candidate void; if no candidate survives, **stop and reopen the owner decision** |
| **STOP-7** | Packaged size breaches the owner-set cap | WP-4.3 | **Stop and reopen the owner decision.** Do not side-load, raise the cap, or substitute a model (AST-04) |

---

# 4. Dependency graph

```
WP-0.1  pilot ground rules, .gitignore, AC-21 CI guard, OP-3 protocol pre-registration
   │
   ├──► WP-0.2  DAT-05 shared Vietnamese fixture set  ─────────────┐
   ├──► WP-0.3  candidate artifact acquisition (Arm A + B)  ───────┤
   └──► WP-0.4  deterministic split (EVA-02/03/04)  ───────────────┤
                    │                                              │
                    ▼                                              │
              WP-0.5  accuracy harness + BASELINE arm               │
                    │                                              │
        ┌───────────┴───────────┐                                  │
        ▼                       ▼                                  │
   WP-0.6 Arm A+B         WP-0.7 .NET runtime  ◄───────────────────┘
   accuracy (1,2)         characterisation (3,4,5,6,8)
   [PARALLEL]             [PARALLEL]
        └───────────┬───────────┘
                    ▼
              WP-0.8  aggregation, report, winner/kill/tie ruling
                    ▼
              WP-0.9  ⛔ OWNER CHECKPOINT 1
                    ▼
              WP-1.0  route + winner ruling  (reads OP-8, OP-9 from the report)
                    │
                    ├──► WP-1.1  ⛔ OWNER CHECKPOINT 2 (Route A only)
                    │         │
                    ▼         ▼
              WP-1.2  contract + null provider + registration
                    │
              WP-1.3  read-only encoder asset locator
                    │
              WP-1.4  ONNX provider + tokenization  (Route A | Route B variant)
                    │
              WP-1.5  cross-cutting guards (AC-04 arch test, AC-05, AC-16, AC-17, AC-18)
                    │
              WP-1.6  S1 no-behaviour-delta verification + PR
                    ▼
              WP-2.1  embedding feature column + featurizer swap  (NOT wired to production)
                    │
              WP-2.2  BEH-13 lifecycle preservation
                    │
              WP-2.3  confidence distribution measurement  (internal S3 input)
                    │
              WP-2.4  confidence policy SPLIT + AC-11 literal-pinned regression
                    │
              WP-2.5  threshold derivation + dual-signal gate
                    │
              WP-2.6  mutation test — prove the gate can go red
                    │
              WP-2.7  WIRE-UP FLIP + latency re-check + AC-03 + release docs   ◄── last commit
                    ▼
              WP-4.0  ⛔ OWNER CHECKPOINT 3
                    │
              WP-4.1  execution-provider probe + tier resolution
                    │
              WP-4.2  Tier 2 parity check + opt-in surface
                    │
              WP-4.3  bundled asset integration + size-cap check
                    │
              WP-4.4  Tier 0 fault-tolerance re-verification + manual QA
```

**Critical path:** WP-0.1 → WP-0.4 → WP-0.5 → {WP-0.6 ∥ WP-0.7} → WP-0.8 → **CP1** → WP-1.0 → WP-1.4
→ WP-1.6 → WP-2.1 → WP-2.5 → WP-2.7 → **CP3** → WP-4.3.

**The only genuine parallelism is WP-0.6 ∥ WP-0.7.** See §7.

---

# 5. Ordered work packages

| WP | Name | Phase | Depends on | Owner CP | Spec requirements satisfied |
|---|---|---|---|---|---|
| 0.1 | Pilot ground rules & repo hygiene | S0 | — | no | AST-05, PRF-06/OP-3, EVA-12 |
| 0.2 | Shared Vietnamese fixture set | S0 | 0.1 | no | DAT-05 |
| 0.3 | Candidate artifact acquisition | S0 | 0.1 | no | AST-05, EVA-06 |
| 0.4 | Deterministic split construction | S0 | 0.1 | no | EVA-02, EVA-03, EVA-04 |
| 0.5 | Accuracy harness + baseline arm | S0 | 0.2, 0.4 | no | EVA-05, EVA-08 (1,2,7) |
| 0.6 | Arm A + Arm B accuracy | S0 | 0.3, 0.5 | no | EVA-05, EVA-06, EVA-07, EVA-08 (1,2) |
| 0.7 | .NET runtime characterisation | S0 | 0.2, 0.3, 0.4 | no | EVA-08 (3,4,5,6,8), EVA-09, EVA-10, TOK-02/03/04/05, PRF-01/02/04/05/07 |
| 0.8 | Aggregation, report, winner ruling | S0 | 0.6, 0.7 | no | EVA-11, EVA-12, EVA-13/14/15/16, DAT-01/02, PRF-03/08 |
| **0.9** | **Owner acceptance of the S0 report** | S0 | 0.8 | **YES — CP1** | EVA-01 |
| 1.0 | Route + winner ruling from the report | S1 | 0.9 | no | TOK-04, OP-8, OP-9 |
| **1.1** | **Shared-ML-package blast radius report** | S1 | 1.0 | **YES — CP2 (conditional)** | TOK-07, AC-08 |
| 1.2 | Embedding contract + null provider | S1 | 1.0, 1.1 | no | BEH-10, FLB-01, ARC-03 |
| 1.3 | Read-only encoder asset locator | S1 | 1.2 | no | AST-06 |
| 1.4 | ONNX provider + tokenization | S1 | 1.3 | no | ARC-09, BEH-05, BEH-11, BEH-12, TOK-01/02/03/06, FLB-01 |
| 1.5 | Cross-cutting guard tests | S1 | 1.4 | no | AST-01, ARC-06, BEH-02, ARC-01/02, FLB-02/03 |
| 1.6 | S1 no-behaviour-delta verification | S1 | 1.5 | no | REL-01, BEH-07, BEH-13 |
| 2.1 | Embedding feature column + swap | S2+S3 | 1.6 | no | BEH-06, ARC-03, ARC-04 |
| 2.2 | Head-retrain lifecycle preservation | S2+S3 | 2.1 | no | BEH-13 |
| 2.3 | Confidence distribution measurement | S2+S3 | 2.2 | no | CNF-03 (input) |
| 2.4 | Confidence policy split | S2+S3 | 2.3 | no | CNF-05 |
| 2.5 | Threshold derivation + dual-signal gate | S2+S3 | 2.4 | no | CNF-01, CNF-02, CNF-03, CNF-04 |
| 2.6 | Gate mutation test | S2+S3 | 2.5 | no | FLB-03, CNF-01/02/03 |
| 2.7 | Wire-up flip + release | S2+S3 | 2.6 | no | CNF-06, CNF-07, CNF-09, REL-02, REL-03, PRF-04, DOC-03, DOC-04 |
| **4.0** | **S4 parameters decision** | S4 | 2.7 | **YES — CP3** | AST-04, OP-1, OP-4, OP-6 |
| 4.1 | Execution-provider probe + tiering | S4 | 4.0 | no | AST-07, ARC-09, FLB-01 |
| 4.2 | Tier 2 parity check + opt-in | S4 | 4.1 | no | AST-08 |
| 4.3 | Bundled asset + size-cap check | S4 | 4.0, 4.2 | no | AST-02, AST-03, AST-04, AST-05 |
| 4.4 | Tier 0 re-verification + manual QA | S4 | 4.3 | no | AST-09, BEH-10, CNF-08, FLB-01/02/03 |

---

# 6. Detailed task cards

> **Common preamble — applies to every card below.**
>
> - **Venue:** `D:\Code\C#\SmartStudyPlanner`. **Branch off `dev`**, never off `docs/epic3-state-sync`.
>   `dev` and `main` are PR-only since 2026-08-09 — every WP lands through a PR with CI green.
> - **Skills:** `superpowers:test-driven-development` for every code-bearing task;
>   `superpowers:verification-before-completion` before any completion claim;
>   `superpowers:systematic-debugging` on any failure.
> - **Tools:** GitNexus MCP **first** — `gitnexus_impact` before editing any symbol,
>   `gitnexus_detect_changes` before every commit. Then Read/Edit/Grep. **RTK prefix on all shell
>   commands** (`rtk dotnet build`, `rtk git status`, …), including inside `&&` chains.
> - **Never:** modify the master plan; touch Epic 2 / Epic 4 surfaces; reopen any Epic 3 decision;
>   commit a model binary; write results into this plan or into the specification (results go to
>   `docs/reports/`); amend a normative document as a side effect of an implementation commit (DOC-04).
> - **Baseline suite count:** **measure it at slice start on the branch; never assume it.** Figures of
>   470 / 391 / 337 all appear in merged documents and none of them is authoritative for this branch.
> - **Commit convention:** small, one concern per commit; no `Co-Authored-By` trailer.

---

## PHASE S0 — Offline pilot

> **EVA-01 boundary, ruled once for the whole phase.** "No production code" means: **no file under
> `SmartStudyPlanner/` is created or modified, and `SmartStudyPlanner.csproj` is not touched.**
> `tools/` is outside `SmartStudyPlanner.slnx` **[verified]**, so a harness there does not enter the
> build or CI. Test *data*, `.gitignore`, and a CI guard step are **not production code** and are
> permitted — they are how S0 stays honest, and DAT-05 requires the fixture set to exist by the first
> slice that uses it, which is S0.

---

### WP-0.1 — Pilot ground rules & repo hygiene

**Objective.** Make it impossible for S0 to damage the repository or to produce an unfalsifiable
number, before any model binary or measurement exists.

**Scope.** AST-05 (no binary in git), PRF-06 / OP-3 (protocol written down *before* measurement),
EVA-12 (report location).

**Files / surfaces.**
- Modify: `.gitignore` — ignore local encoder artifacts.
- Modify: `.github/workflows/ci.yml` — add a repository binary-guard step (AC-21).
- Create: `tools/ml-pilot/README.md` — venue, ground rules, and the pre-registered protocol.
- Create: `docs/reports/2026-XX-XX-encoder-pilot.md` — **skeleton only**, sections stubbed with the
  eight required outputs as empty headings so no output can be quietly dropped.

**Dependencies.** None. This is the first task.

**Implementation strategy.**

1. `.gitignore`: add a block ignoring the pilot's model working directory and every common encoder /
   tokenizer artifact extension — `*.onnx`, `*.onnx_data`, `*.safetensors`, `*.gguf`, `*.model`
   (SentencePiece), plus the whole `tools/ml-pilot/models/` tree. Encoder assets live only there.
2. CI guard (AC-21): a `pwsh` step that fails the build if any tracked file matches those extensions
   or exceeds a size threshold. It must assert over `git ls-files`, **not** over the working tree —
   the point is to catch a *committed* binary, and an untracked local file is expected and fine.
3. **Pre-register the OP-3 measurement protocol in `tools/ml-pilot/README.md` before any number is
   measured.** PRF-06 requires the statistics to be written down before any number is compared
   against the 500 ms ceiling. Record, as a decision with reasoning: warm vs cold runs, which
   percentile is reported, sample count, and how outliers are handled. **The boundary is already
   fixed by PRF-05** (invocation of the quick-parse action → structured fields populated, including
   tokenization and the forward pass, excluding model load) — do not re-open it; only the statistics
   are S0's to choose.
4. Report skeleton: eight headings matching EVA-08 outputs 1–8, plus **Limitations** (EVA-11),
   **Machine used** (EVA-10), **Measurement protocol** (PRF-06), and **Decisions made** (ADR-style —
   required for every agent-written report by `docs/reports/README.md`).

**Verification.**
- **Prove the CI guard can go red.** Commit a ≥1 MB dummy `.onnx` file on a scratch branch, confirm
  CI fails, then remove it. A guard whose pass is indistinguishable from a broken guard is not a
  guard. Record the red run in the WP's PR description.
- `rtk git status` shows a clean tree with `tools/ml-pilot/models/` present but untracked.

**Acceptance criteria.** AC-21 (first occurrence — re-verified at S1, S2+S3, S4).

**Risks.** The guard is written over the working tree instead of the index and fires on every local
download, tempting someone to weaken it. Mitigation: assert over `git ls-files` and say so in a
comment.

**Rollback.** Revert one commit. No production surface touched.

**Owner checkpoint.** No.

---

### WP-0.2 — Shared Vietnamese fixture set (DAT-05)

**Objective.** Create the **single** Vietnamese input fixture set that every slice compares against,
so that four different acceptance criteria (AC-06, AC-13, AC-17, AC-30) are testing one thing rather
than four things wearing the same words.

**Scope.** DAT-05.

**Files / surfaces.**
- Create: `datasheets/vn_input_fixtures.csv` — the committed, versioned fixture set.
- Create: `datasheets/vn_input_fixtures.md` — what each category is for and which AC reads it.

**Dependencies.** WP-0.1.

**Implementation strategy.**

Place the set in `datasheets/` — outside `SmartStudyPlanner/`, so S0 does not mutate the production
project (EVA-01), and outside `tools/ml-pilot/`, so it is not a throwaway. **S1 (WP-1.5) adds the
csproj/test-project include**; S0 only reads it from disk.

Columns: `Id`, `Category`, `Input`, `Note`. Categories are exactly the six DAT-05 minima:

| Category | What it must contain | Read by |
|---|---|---|
| `diacritics` | Correct Vietnamese with full diacritics | AC-06, AC-13, AC-17 |
| `stripped` | The same meanings with diacritics removed | AC-06, AC-30 |
| `runtogether` | Run-together tokens (`thigiuaky`, `baitapvenha`) | AC-06, AC-30 |
| `abbrev` | Domain abbreviations (`tgk`, `xstk`, `ktvm`, `csdl`) | AC-06, AC-30 |
| `empty` | Empty string and whitespace-only inputs | AC-28, FLB-01 |
| `pathological` | Pathologically long input | AC-28, FLB-01 |

Draw the realistic rows from `datasheets/collected_v4.csv`, which already contains real collected
input. **Pair the `diacritics` and `stripped` rows** — same semantic content, one row each — so AC-30
can measure preprocessing independence as a within-pair comparison rather than across unrelated
inputs.

**Verification.** All six categories non-empty; every `stripped` row has a `diacritics` partner; the
file is valid UTF-8. **Read it back with a byte-level reader, not PowerShell `Get-Content`** —
PowerShell mangles BOM-less UTF-8 Vietnamese in this environment.

**Acceptance criteria.** DAT-05, feeding AC-06, AC-13, AC-17, AC-30.

**Risks.** The set gets forked later ("the tokenizer test needs its own cases"). Mitigation: the
companion `.md` names the four ACs that read it, and WP-1.5 asserts the test project loads *this*
path.

**Rollback.** Delete two data files. No code depends on them until S1.

**Owner checkpoint.** No.

---

### WP-0.3 — Candidate artifact acquisition

**Objective.** Get the Arm A and Arm B encoder + tokenizer artifacts onto disk, at pinned revisions,
with recorded hashes and sizes — and nowhere near git.

**Scope.** AST-05, EVA-06 (Arm C **not** acquired), EVA-08 output 8 (size input).

**Files / surfaces.**
- Create: `tools/ml-pilot/models/` — **git-ignored**, populated locally.
- Create: `tools/ml-pilot/ARTIFACTS.md` — per arm: source, pinned revision/commit, SHA-256, on-disk
  byte size of every file, and the licence acceptance status.

**Dependencies.** WP-0.1 (the `.gitignore` must exist first — this task downloads hundreds of MB).

**Implementation strategy.**

1. Acquire, per arm, the **ONNX encoder export** and **its tokenizer assets** — Arm A:
   EmbeddingGemma-300M; Arm B: `multilingual-e5-small`. **Do not acquire Arm C** (`hiieu/halong_embedding`)
   — EVA-06 makes running it "while we're here" a scope violation, and acquisition is the first step
   of running it.
2. Pin an exact revision per artifact and record its SHA-256. Two arms measured against different
   silently-updated exports are not comparable.
3. Record **packaged on-disk size per arm** — encoder file(s) plus tokenizer assets, as they would
   ship. This is EVA-08 output 8 and the input to OP-1; WP-0.8 copies it into the report.
4. Note any gated-model licence acceptance in `ARTIFACTS.md`. If an artifact cannot be obtained under
   a licence the project can accept, that arm is **blocked, not silently dropped** — record it and
   raise it at WP-0.8.

**Verification.** `rtk git status` clean after acquisition — nothing under `models/` is tracked.
Re-run the WP-0.1 CI guard locally. Every recorded hash reproduces on re-download.

**Acceptance criteria.** AST-05 (ongoing), EVA-06, EVA-08 output 8 (measurement captured).

**Risks.** Quantization choice (`[choice]`, §3.2) silently differs between arms, making the size and
latency comparison meaningless. Mitigation: `ARTIFACTS.md` records the precision/quantization of each
export explicitly, and WP-0.8 reports it beside the numbers.

**Rollback.** Delete an untracked directory.

**Owner checkpoint.** No.

---

### WP-0.4 — Deterministic split construction

**Objective.** Build the split **once**, so that every arm is measured on identical data and the
featurizer is genuinely the only variable.

**Scope.** EVA-02, EVA-03, EVA-04.

**Files / surfaces.**
- Create: `tools/ml-pilot/split/build_split.py` (language is at the implementer's discretion —
  nothing ships).
- Create: `tools/ml-pilot/split/train.csv`, `tools/ml-pilot/split/test.csv` — **committed**; they are
  small text files and committing them is what makes "no arm re-split" auditable.
- Create: `tools/ml-pilot/split/SPLIT.md` — row counts, class distribution, source hash.

**Dependencies.** WP-0.1.

**Implementation strategy.**

Read `SmartStudyPlanner/Services/ML/TextClassifier/seed_intents.csv` (**read-only** — S0 does not
modify anything under `SmartStudyPlanner/`).

- **train** = the synthetic subset only: rows where `Source` ∈ {`m8a_uniform`, `synthetic_v3`} —
  expect **597 + 101 = 698** rows (EVA-02).
- **test** = the real held-out subset: rows where `Source` = `collected_v4` — expect **205** rows
  (EVA-03), **excluded from training**.
- Assert the counts. If they do not match 698 / 205 / 903, **stop and report** — the seed has changed
  since the specification's `[fact]` was established, and that is a finding, not something to absorb.
- Record the class distribution of the test set. Expect **3 of 5 classes**: `ThiGiuaKy` 99,
  `BaiTapVeNha` 56, `DoAnCuoiKy` 50; no `KiemTraThuongXuyen`, no `ThiCuoiKy`. This is the source of
  EVA-08 output 7 and of the DAT-01 reporting bound.
- Record the SHA-256 of the source CSV in `SPLIT.md`, so a later reader can tell whether the split
  still corresponds to the seed.

**Verification.**
- `train ∩ test = ∅` on exact input text — assert it, do not assume it. The 205 real rows were merged
  *into* the seed by `datasheets/_merge_seed.py`, which is precisely why the published 96.2% figure
  is not a generalization number; a leaky split here would recreate that error inside S0 itself.
- Re-running `build_split.py` produces byte-identical output.

**Acceptance criteria.** EVA-02, EVA-03, EVA-04 → AC-02.

**Risks.** Near-duplicate rows across the boundary (not exact duplicates) leak information. The
corpus is not deduplicated (§9 `[limit]`). Mitigation: **report** near-duplicate overlap as a
measured number in `SPLIT.md` and carry it into the report's limitations (EVA-11). Do **not** filter
them out — that would silently change the split the specification defines.

**Rollback.** Delete `tools/ml-pilot/split/`. Nothing depends on it yet.

**Owner checkpoint.** No.

---

### WP-0.5 — Accuracy harness + baseline arm

**Objective.** Build the harness that produces outputs 1, 2 and 7, and produce the **baseline**
numbers against which everything else is judged.

**Scope.** EVA-05, EVA-08 outputs 1, 2, 7.

**Files / surfaces.**
- Create: `tools/ml-pilot/accuracy/` — harness.
- Create: `tools/ml-pilot/results/baseline.json` — per-class metrics + confidence/accuracy bins.

**Dependencies.** WP-0.2, WP-0.4.

**Implementation strategy.**

1. **Baseline featurizer = the current production one.** The pipeline in
   `TextClassifierModelManager.TrainAndSaveAsync` **[verified]** is:
   `MapValueToKey("Label","TaskType")` → `FeaturizeText("Features","InputText")` →
   `SdcaMaximumEntropy("Label","Features")` → `MapKeyToValue("PredictedLabel")`, with
   `MLContext(seed: 42)`. The baseline arm must reproduce this, **including the seed** — an unseeded
   baseline manufactures run-to-run variance and corrupts the EVA-16 kill criterion.
2. **One head family for every arm** (EVA-05): `SdcaMaximumEntropy` throughout. The featurizer is the
   only variable. If the encoder arms use a non-.NET head for speed of iteration, the head family and
   its hyperparameters must still match, and the divergence must be reported.
3. **Output 1 — per-class precision and recall** for the three covered classes. **Emit no single
   headline accuracy figure**, anywhere, including in intermediate JSON. EVA-08 forbids it and a
   number that exists in a file will end up in a summary.
4. **Output 2 — confidence-versus-accuracy relationship.** Bin predictions by confidence and report
   observed accuracy per bin, with bin population counts. **Population counts are not optional** —
   this is the input to CNF-03's threshold derivation, and a bin holding four samples cannot support
   a gate. Persist the raw per-row `(confidence, correct)` pairs too, so WP-2.5 can re-derive without
   re-running S0.
5. **Run-to-run variance is a first-class output**, not a footnote: repeat each arm across several
   seeds and report the spread. EVA-16's kill criterion and EVA-14's dimension 1 are both defined
   *relative to variance* — without a measured spread, neither is decidable.
6. **Output 7 — the coverage limitation**, written as prose into `docs/reports/`, sourced from
   WP-0.4's `SPLIT.md`.

**Verification.** Re-running the baseline with a fixed seed reproduces its numbers. Confidence bins
sum to the test-set size. Per-class support counts match `SPLIT.md`.

**Acceptance criteria.** EVA-05, EVA-08 (1, 2, 7) → AC-01, AC-02.

**Risks.** The harness is written to make the encoder look good — e.g. baseline gets default
hyperparameters while arms get tuned ones. Mitigation: identical head configuration is asserted in
code and stated in the report.

**Rollback.** Delete `tools/ml-pilot/accuracy/`.

**Owner checkpoint.** No.

---

### WP-0.6 — Arm A + Arm B accuracy  ‖ *parallel with WP-0.7*

**Objective.** Produce outputs 1 and 2 for the two encoder arms, on the identical split, through the
identical harness.

**Scope.** EVA-05, EVA-06, EVA-07, EVA-08 outputs 1, 2.

**Files / surfaces.**
- Create: `tools/ml-pilot/results/arm_a.json`, `tools/ml-pilot/results/arm_b.json`.
- Modify: `tools/ml-pilot/accuracy/` — add the embedding featurizer path.

**Dependencies.** WP-0.3, WP-0.5.

**Implementation strategy.**

1. Consume WP-0.4's split **verbatim**. **No re-splitting, no re-shuffling, no stratification pass.**
   EVA-04 is absolute.
2. Run **Arm A and Arm B only**. **Do not run Arm C.** It is unlocked only by an explicit owner
   decision after A and B report (EVA-06, PD-8). If a strong case for Arm C emerges mid-run, that is
   an input to WP-0.8's ruling — not permission.
3. Embedding dimensionality and any representation truncation are `[choice]` (§3.2). **Whatever is
   chosen must be recorded per arm** and held identical across the variance repeats.
4. **EVA-07 discipline.** No prior benchmark claim may appear in any output as evidence that one
   candidate is better for this project. The positional-encoding argument survives **only as an
   architectural prior**; the withdrawn VN-MTEB justification must not be restored, quoted, or
   paraphrased.
5. Same variance protocol as WP-0.5 — same number of seeds, same head configuration.

**Verification.** Both arms' result files reference the same `SPLIT.md` hash as `baseline.json`. Per-class
support counts identical across all three arms. Confidence bin edges identical across arms.

**Acceptance criteria.** EVA-05, EVA-06, EVA-07, EVA-08 (1, 2) → AC-02.

**Risks.** An arm is quietly advantaged by a different preprocessing step (lower-casing, diacritic
handling). Mitigation: **BEH-04 forbids any preprocessing precondition** — feed the raw input string
as typed to every arm, and assert in the harness that the string handed to each featurizer is
byte-identical.

**Rollback.** Delete two result files.

**Owner checkpoint.** No.

---

### WP-0.7 — .NET runtime characterisation  ‖ *parallel with WP-0.6*

**Objective.** Produce outputs 3, 4, 5, 6 and 8 **on the stack that ships**, on the reference hardware
— the measurements that decide whether the winning arm is deployable at all.

**Scope.** EVA-08 outputs 3, 4, 5, 6, 8; EVA-09, EVA-10; TOK-02, TOK-03, TOK-04, TOK-05; PRF-01,
PRF-02, PRF-04, PRF-05, PRF-07.

**Files / surfaces.**
- Create: `tools/ml-pilot/dotnet/` — throwaway .NET console harness. **Not added to
  `SmartStudyPlanner.slnx`** (`tools/` is already outside it **[verified]**), so it never enters CI or
  the product build.
- Create: `tools/ml-pilot/results/runtime_<arm>.json`.

**Dependencies.** WP-0.2, WP-0.3, WP-0.4.

**Implementation strategy.**

1. **The stack must be the real one** (EVA-09): `Microsoft.ML.OnnxRuntime` `InferenceSession` + the
   real tokenizer + a real `SdcaMaximumEntropy` head. **Numbers from a Python `onnxruntime` +
   scikit-learn stack do not transfer and must not be used to satisfy these outputs.** This harness
   exists solely because measuring off-path would clear a gate that was never tested.
2. **Output 6 — tokenization viability, per candidate, `[gate]`.** For each arm, attempt **both**
   recognised routes and record which actually works:
   - **Route A** — a .NET-side tokenizer library, loading the encoder's **real vocabulary file**.
   - **Route B** — tokenization embedded in the model graph.

   **Verify by loading the real vocabulary and comparing output against the encoder's reference
   tokenizer on the WP-0.2 fixture set** — across `diacritics`, `stripped`, `runtogether` and
   `abbrev` categories. **Do not verify by reading a documentation page** (TOK-04). Silent divergence
   from the reference tokenizer degrades the encoder to noise while appearing to work, which is
   exactly why TOK-02 makes this checked rather than assumed.

   Record the **verified route(s) per arm**. An arm with **no** workable, verified route is
   **rejected regardless of its accuracy** (TOK-05) — record it as rejected, and say so in the report
   (AC-07).

   **TOK-03 constraint:** the route must work on `net10.0-windows10.0.19041.0`, **fully offline**,
   with **no non-.NET runtime dependency** — no JVM, no Python, no external process. A route that
   shells out to Python is not a route.

   **TOK-07 trigger:** if Route A implies a **version change to an ML package shared with the existing
   predictors** — the project pins `Microsoft.ML` at **3.0.1 [verified]**, and both M7 and M8-A depend
   on it — **record that finding here**. Do not act on it. It becomes owner checkpoint CP2 at WP-1.1,
   before any dependency change is committed.
3. **Output 3 — cold-start model load time.** Measured separately from inference, because PRF-05
   **excludes** load time from the latency boundary.
4. **Output 4 — per-inference latency**, on the **CPU execution provider**, over the **PRF-05
   boundary**: from invocation of the quick-parse action to structured fields being populated,
   **including tokenization and the encoder forward pass**, **excluding** model load. Apply the
   protocol pre-registered in WP-0.1 — warm/cold, percentile, sample count — and **write the protocol
   into the report before any number is compared against the 500 ms ceiling** (PRF-06).
5. **Output 5 — peak resident memory during inference**, with the model resident, reported against
   PRF-01's **8 GB** budget. **Assert no ceiling** — PRF-08 requires measuring first and deriving the
   ceiling later, at S4, precisely so it is not reverse-engineered from whatever the winner used.
6. **Output 8 — packaged on-disk size**, carried from WP-0.3's `ARTIFACTS.md`.
7. **Hardware (PRF-01, PRF-03, EVA-10, OP-5).** Run on the reference class: 10th-gen Intel Core mobile
   U-series or equivalent, 8 GB RAM, integrated graphics, Windows 10 build 19041 or a supported newer
   environment. **Name the actual machine** — model, CPU, RAM, OS build — in `runtime_<arm>.json` and
   in the report. **A developer-machine-only number is not an acceptable output** (PRF-03).
   **If no such machine is available, this WP cannot complete legitimately — stop and raise it at
   CP1.** See §16, UQ-1.
8. **Derived quantity worth capturing while the harness exists:** per-inference latency × 698 training
   rows estimates the cost of embedding the seed during head retraining. It is not an EVA-08 output —
   record it as a note. It is the input to R-17 (§11) and to WP-2.2's mitigation.

**Verification.** Every reported number carries the machine identity and the protocol that produced
it. The tokenizer comparison is run against the reference tokenizer's actual output, and a
deliberately corrupted vocabulary makes the comparison **fail** — prove the check can go red before
trusting a pass.

**Acceptance criteria.** EVA-08 (3,4,5,6,8), EVA-09, EVA-10, TOK-02/03/04/05, PRF-01/02/04/05/07 →
AC-01, AC-02, **AC-07**, AC-23 (first occurrence), AC-24.

**Risks.** See R-12, R-15, R-10, R-11 in §11.

**Rollback.** Delete `tools/ml-pilot/dotnet/`. It is outside the solution; nothing else changes.

**Owner checkpoint.** No — but it **produces** CP2's input (TOK-07) and CP3's inputs (OP-1, OP-4).

---

### WP-0.8 — Aggregation, report, and the winner / kill / tie ruling

**Objective.** Turn the measurements into the one artifact the owner decides on — and apply the
ratified win logic honestly, including the branches where the answer is "no".

**Scope.** EVA-11, EVA-12, EVA-13, EVA-14, EVA-15, EVA-16, DAT-01, DAT-02, PRF-03, PRF-06, PRF-08.

**Files / surfaces.**
- Modify: `docs/reports/2026-XX-XX-encoder-pilot.md` — fill the WP-0.1 skeleton.

**Dependencies.** WP-0.6 **and** WP-0.7 — both. The ruling needs accuracy and runtime together,
because EVA-14 requires all five dimensions.

**Implementation strategy.**

1. Fill all eight outputs **per arm**. A missing output is a failed report, not a caveat.
2. **Apply EVA-14 — a win requires all five dimensions**, evaluated explicitly and in writing:
   1. improvement over baseline **beyond run-to-run variance** (use WP-0.5's measured spread);
   2. **per-class** results acceptable — not one class carrying the average;
   3. **confidence behaviour usable** — the measured relationship can actually support a gate,
      with enough population in the bins near any plausible boundary;
   4. **latency and peak memory within the §7 budget**;
   5. a **viable, verified tokenization path** (TOK-05).
3. **EVA-13 — no fixed effect size.** Do not introduce "+2 F1" or any equivalent threshold, before or
   after the fact. The comparison is against measured variance, not an invented margin.
4. **EVA-15 — honour the tie branch.** If A and B cannot be reliably distinguished, **say so and
   declare no winner.** The decision then becomes whether more evidence is justified — conditional
   Arm C, or data expansion — and that is an **owner decision at CP1**, not this task's to make.
   Declaring a winner on a difference inside the noise is the specific failure EVA-15 exists to
   prevent.
5. **EVA-16 — apply the kill criterion.** If both encoder arms fail to improve macro-F1 over baseline
   beyond run-to-run variance, **the initiative does not proceed to implementation.** Write that
   conclusion plainly. EVA-14 is a strictly higher bar than merely surviving EVA-16 — an arm can
   survive the kill criterion and still not win.
6. **EVA-11 — state limitations in the report's own text**, not by reference to the specification:
   the 3-of-5 class coverage, the corpus's un-deduplicated / unversioned / unbalanced state, the
   near-duplicate overlap measured in WP-0.4, and **the measurement protocol actually used**.
7. **DAT-01 / DAT-02 — no general-accuracy claim** from a 3-class evaluation. And dataset immaturity
   is **not** recorded as a production acceptance failure; it is a known, bounded limitation of the
   evidence.
8. **PRF-08 — assert no memory ceiling.** Report the measurement against the 8 GB budget and state
   explicitly that the ceiling is derived at S4 (OP-4).
9. Add the **"Decisions made"** section — why / what-for / experience — required of every agent-written
   report by `docs/reports/README.md`.

**Verification.** Walk EVA-08's eight rows against the report and tick each. Walk EVA-14's five
dimensions and confirm each is answered in writing for each arm. Confirm no single headline accuracy
figure appears anywhere in the document.

**Acceptance criteria.** **AC-01, AC-02, AC-07, AC-24, AC-29 (first occurrence), AC-31.**

**Risks.** R-1, R-2, R-15 (§11). Plus: the report is written to justify continuing. Mitigation: the
five dimensions are answered individually before any overall conclusion is drafted, and a null result
is stated in the plan (here) as a **success condition**, not a failure.

**Rollback.** The report is the deliverable; there is nothing to roll back. If it is wrong, it is
amended and re-submitted to CP1.

**Owner checkpoint.** No — it **produces** CP1's input.

---

### WP-0.9 — ⛔ OWNER CHECKPOINT 1 — accept or reject the S0 report

**Objective.** Obtain the owner's explicit acceptance or rejection. **This is the hard gate.**

**Scope.** EVA-01.

**Dependencies.** WP-0.8.

**What the owner is being asked to decide.**

1. **Accept or reject the report** (PD-3). Rejection ends the initiative — a valid outcome.
2. **If a winner is declared** — confirm the adopted encoder (OP-9) and note the verified tokenization
   route (OP-8) that comes with it.
3. **If the tie branch fired** (EVA-15) — decide between: unlock **Arm C** (OP-11), expand the
   dataset and re-run, defer, or stop. **Arm C requires an explicit owner decision; nothing else
   unlocks it.**
4. **If the kill criterion fired** (EVA-16) — confirm the stop.

**Exit criteria.** A written owner ruling, recorded in the repository — appended to the report or
filed as a dated decision record. **Until it exists, no file under `SmartStudyPlanner/` may be
created or modified for this initiative** (EVA-01).

**Acceptance criteria.** **AC-01** (the "owner-accepted before any production code exists" clause).

**Owner checkpoint.** **YES — blocking.**

---

## PHASE S1 — Encoder infrastructure seam

> **REL-01 governs this entire phase: S1 MUST NOT change user-visible behaviour.** Its correctness is
> demonstrated by the **absence of a behaviour delta**, not by a feature. Nothing in S1 consumes the
> encoder for a user-visible result — that is S2's job, and doing it early would create exactly the
> uncalibrated intermediate CNF-09 forbids.

---

### WP-1.0 — Route and winner ruling from the S0 report

**Objective.** Read the two `[gate]` outcomes out of the accepted report and fix them for the rest of
the phase, in writing, so no downstream task re-decides them.

**Scope.** TOK-04, OP-8, OP-9.

**Files / surfaces.**
- Create: `docs/reports/2026-XX-XX-s1-route-ruling.md` — short record: adopted encoder, verified
  tokenization route, embedding rank, quantization, packaged size, and the report + owner ruling it
  derives from.

**Dependencies.** WP-0.9.

**Implementation strategy.**

1. Record the **adopted encoder** (OP-9) exactly as the owner accepted it.
2. Record the **verified tokenization route** (OP-8) for that encoder, and **whether a second route
   was also verified**. This matters for STOP-6: if the selected route later proves unworkable, TOK-06
   permits falling back **only to another *verified* route for the same candidate** — never to a
   substituted route or candidate.
3. Record the **embedding rank** and quantization from WP-0.3 / WP-0.7. WP-2.1 needs the rank as a
   compile-time constant.
4. **Select the S1 file-map variant:**
   - **Route A** → a .NET tokenizer type exists (`ITextTokenizer` + implementation), and WP-1.1's
     checkpoint applies if a shared-package bump is implied.
   - **Route B** → tokenization lives inside the ONNX graph; **no .NET tokenizer type is created**,
     and WP-1.1 is skipped unless a bump is implied for another reason.

**Verification.** Every field traces to a line in the accepted report. Nothing is inferred.

**Acceptance criteria.** Feeds AC-07, AC-08.

**Risks.** The ruling paraphrases the report and drifts. Mitigation: quote the report and cite its
heading.

**Rollback.** Delete one document.

**Owner checkpoint.** No — it consumes CP1's output.

---

### WP-1.1 — ⛔ OWNER CHECKPOINT 2 (conditional) — shared-ML-package blast radius

**Objective.** If the adopted route implies a version change to an ML package shared with the shipped
predictors, put that in front of the owner **before** the dependency change is committed — not inside
a dependency-addition commit.

**Scope.** TOK-07.

**Applies when.** WP-0.7 recorded that the selected route implies moving `Microsoft.ML` off its
pinned **3.0.1** **[verified]**, or any equivalent shared-package change. **Skip only if no such
change is implied**, and record the skip with its reason.

**Dependencies.** WP-1.0.

**What the owner is being asked to decide.** Whether to accept a version bump to a package that
**both shipped predictors depend on** — M7 `StudyTimePredictor` (`Microsoft.ML.FastTree`) and M8-A
`TextClassifier` (`Microsoft.ML`).

**Implementation strategy.**

1. Run `gitnexus_impact` upstream on the ML surfaces that consume the package —
   `TextClassifierModelManager`, `MLModelManager`, `StudyTimePredictorService` — and report the blast
   radius: direct dependents, affected execution flows, risk level.
2. Report **what a bump changes**, not merely that it happens: behavioural differences in the trainers
   both predictors use, and whether the existing model artifacts on users' machines remain loadable.
3. Present alternatives explicitly: the other verified route for the same candidate, if WP-1.0
   recorded one, avoids the dependency entirely.
4. **Warn explicitly if the impact analysis returns HIGH or CRITICAL** — required by `CLAUDE.md`, and
   this one plausibly is.

**Exit criteria.** A recorded owner decision **preceding** the commit that changes the dependency.
`AC-08` is verified by that ordering being visible in the commit/PR history.

**Acceptance criteria.** **AC-08.**

**Owner checkpoint.** **YES — blocking, when it applies.**

---

### WP-1.2 — Embedding contract + null provider + registration

**Objective.** Introduce the abstraction seam through which text becomes a dense vector, with a null
implementation that makes the encoder's absence a normal, tested state.

**Scope.** BEH-10, FLB-01, ARC-03.

**Files / surfaces.**
- Create: `SmartStudyPlanner/Core/ML/Contracts/ITextEmbeddingProvider.cs`
- Create: `SmartStudyPlanner/Services/ML/Embedding/NullTextEmbeddingProvider.cs`
- Modify: `SmartStudyPlanner/Services/ServiceLocator.cs` (ML registration block, currently lines
  82–108 **[verified]**)
- Create: `SmartStudyPlanner.Tests/Services/ML/Embedding/NullTextEmbeddingProviderTests.cs`

**Dependencies.** WP-1.0, WP-1.1.

**Interfaces.**
- **Produces**, consumed by WP-1.4, WP-2.1:
  ```
  namespace SmartStudyPlanner.Core.ML.Contracts;
  public interface ITextEmbeddingProvider
  {
      bool IsAvailable { get; }
      int Rank { get; }          // documented rank of the vector; 0 when unavailable
      float[]? Embed(string text);   // null when unavailable or on any failure — never throws
  }
  ```
  `Embed` returning `null` (rather than throwing) is what makes FLB-01 structural instead of
  aspirational: the caller's fallback path is the *normal* path, not an exception handler.

**Implementation strategy.**

1. Place the contract in `Core/ML/Contracts/` alongside `IIntentClassifierService` and
   `IMlConfidencePolicy` **[verified layout]** — Core holds ports, Services holds implementations.
2. `NullTextEmbeddingProvider`: `IsAvailable => false`, `Rank => 0`, `Embed(_) => null`. No state, no
   I/O.
3. **Register the null provider now.** Production resolves `ITextEmbeddingProvider` to the null
   implementation at the end of S1 — the ONNX provider exists but is not the registered default until
   WP-2.7 flips it. This is what keeps REL-01 true by construction rather than by care.

**Verification (TDD order).**
1. Write `NullTextEmbeddingProviderTests`: `Embed` returns `null` for every WP-0.2 fixture category
   including empty, whitespace-only, and pathological input; `IsAvailable` is false; nothing throws.
2. Run — fails (type does not exist).
3. Implement.
4. Run — passes.
5. `gitnexus_detect_changes` — affected symbols must be the new types plus `ServiceLocator` only.
6. `rtk dotnet build` clean; `rtk dotnet test` at the measured baseline count.

**Acceptance criteria.** Contributes to AC-13, AC-28.

**Risks.** `ServiceLocator` is the composition root — MEDIUM blast radius. Mitigation: run
`gitnexus_impact` on the registration block first; the change is additive.

**Rollback.** Revert the WP's commits; the registration is additive and nothing consumes it.

**Owner checkpoint.** No.

---

### WP-1.3 — Read-only encoder asset locator

**Objective.** Resolve the encoder asset from a **read-only** location that is **not** the writable
trained-artifact store, without creating directories.

**Scope.** AST-06.

**Files / surfaces.**
- Create: `SmartStudyPlanner/Core/ML/Contracts/IEncoderAssetLocator.cs`
- Create: `SmartStudyPlanner/Services/ML/Embedding/BaseDirectoryEncoderAssetLocator.cs`
- Create: `SmartStudyPlanner.Tests/Services/ML/Embedding/EncoderAssetLocatorTests.cs`
- Modify: `SmartStudyPlanner/Services/ServiceLocator.cs`

**Dependencies.** WP-1.2.

**Interfaces.**
- **Produces**, consumed by WP-1.4, WP-4.3:
  ```
  public interface IEncoderAssetLocator
  {
      string? TryResolve(string assetFileName);   // null when absent; never creates anything
      string RootDirectory { get; }
  }
  ```

**Implementation strategy.**

**Do not extend `IModelStorageProvider`.** This is divergence **D-2** in §2.2. The existing
`LocalModelStorageProvider` resolves to `%AppData%\SmartStudyPlanner\models` and
**`Directory.CreateDirectory(BaseDirectory)` in its constructor [verified, line 22]** — correct for
trained artifacts, and exactly wrong for a bundled read-only one. AST-06 requires distinctness and
requires that resolution creates nothing.

1. `BaseDirectoryEncoderAssetLocator` resolves under `AppContext.BaseDirectory` — next to the
   executable, where a bundled asset lands.
2. The root directory is **injectable** (constructor parameter, defaulting to `AppContext.BaseDirectory`)
   so tests can point at a temp directory and **so WP-4.3 can fix the default once OP-6 is decided**,
   without re-opening this type.
3. `TryResolve` performs `File.Exists` and returns the path or `null`. **No `Directory.CreateDirectory`,
   no write handle, no side effect of any kind.**

**Verification (TDD order).**
1. Test: constructing the locator against a **non-existent** directory path creates nothing — assert
   `Directory.Exists(root) == false` **after** construction and after a `TryResolve` call. This is the
   test that would have caught the `LocalModelStorageProvider` behaviour, so it must be able to go
   red: temporarily add a `CreateDirectory` call and confirm the test fails.
2. Test: `TryResolve` returns `null` for an absent file, the full path for a present one.
3. Test: the locator's root is **not equal to** `LocalModelStorageProvider.DefaultBaseDirectory` —
   this is AC-18's distinctness clause, asserted rather than assumed.
4. Tests must not write into the real user profile — CI fails the build on any
   `%APPDATA%\SmartStudyPlanner` write **[verified CI step]**. Use temp directories.

**Acceptance criteria.** **AC-18.**

**Risks.** A later change "helpfully" makes the locator create its directory. Mitigation: the
no-side-effect test is explicit and named for the requirement.

**Rollback.** Revert; nothing consumes it yet.

**Owner checkpoint.** No.

---

### WP-1.4 — ONNX inference provider + tokenization

**Objective.** Implement the real embedding provider — one long-lived session, off the startup path,
never throwing — with the tokenization route S0 verified.

**Scope.** ARC-09, BEH-05, BEH-11, BEH-12, TOK-01, TOK-02, TOK-03, TOK-06, FLB-01.

**Files / surfaces.**
- Create: `SmartStudyPlanner/Services/ML/Embedding/OnnxTextEmbeddingProvider.cs`
- **Route A only** — Create: `SmartStudyPlanner/Core/ML/Contracts/ITextTokenizer.cs` and
  `SmartStudyPlanner/Services/ML/Embedding/SentencePieceTextTokenizer.cs`
- **Route B only** — no tokenizer type; the graph consumes the string tensor directly
- Modify: `SmartStudyPlanner/SmartStudyPlanner.csproj` — add the ONNX runtime package (**Route A also
  adds the tokenizer package — only after WP-1.1's checkpoint**)
- Modify: `SmartStudyPlanner/Services/ServiceLocator.cs`
- Create: `SmartStudyPlanner.Tests/Services/ML/Embedding/OnnxTextEmbeddingProviderTests.cs`

**Dependencies.** WP-1.3, and — under Route A with an implied bump — WP-1.1's recorded decision.

**Interfaces.**
- **Consumes:** `ITextEmbeddingProvider` (WP-1.2), `IEncoderAssetLocator` (WP-1.3).
- **Produces** (Route A only), consumed by nothing outside this WP:
  ```
  public interface ITextTokenizer
  {
      bool IsAvailable { get; }
      (int[] InputIds, int[] AttentionMask)? Encode(string text);   // null on failure — never throws
  }
  ```

**Implementation strategy.**

1. **One session, created once, lazily.** BEH-12 forbids paying cold-start load cost per parse, and
   BEH-11 forbids paying it on the startup path. Use a lazy, thread-safe single construction —
   **`InferenceSession` is created exactly once per process and is asserted to be** (R-7). The existing
   `TextClassifierModelManager.Predict` creates a `PredictionEngine` **per call** [verified, line 118];
   replicating that pattern for an encoder would be a serious performance defect, not a style issue.
2. **Never throw.** Asset missing, corrupt, truncated, wrong format, session construction failure,
   tokenizer failure, inference exception, malformed output tensor → `Embed` returns `null` and
   `IsAvailable` becomes false. **Contain the failure at load — do not retry per parse** (§10).
3. **Prefer calling `InferenceSession` directly** over `Microsoft.ML.OnnxTransformer`, whose
   documented opset support is too old for a current export, and which would surrender session
   lifetime control.
4. **Route A**: `SentencePieceTextTokenizer` loads the vocabulary through the locator and reproduces
   the reference tokenizer's output. **Route B**: the graph handles it; `Embed` passes the string
   tensor.
5. **TOK-01**: the provider converts the raw string itself. Callers never supply ids or masks.
6. **BEH-04**: the raw input string as typed. **No diacritic restoration, no segmentation, no spelling
   correction** as a precondition.
7. **STOP-6 discipline (TOK-06):** if the S0-selected route proves unworkable here, **do not
   substitute silently.** Use the other route **only if WP-1.0 recorded it as verified for this same
   candidate**; otherwise the candidate is void and the initiative stops pending an owner decision.

**Verification (TDD order).**
1. Test: with **no asset present**, `IsAvailable` is false, `Embed` returns `null`, nothing throws.
2. Test: with a **corrupt/truncated** asset present, same — and construction does not throw.
3. Test **(BEH-05, AC-17)**: for the same input, the same asset and the same provider, `Embed`
   returns vectors equal within a **documented tolerance**. Write the tolerance into the XML doc
   comment and assert against that constant, over the WP-0.2 fixture set.
4. Test: returned vector length equals the documented `Rank` for every fixture.
5. Test **(R-7, and BEH-11)**: `InferenceSession` is constructed **exactly once** across many `Embed`
   calls — count constructions through a seam or a counter, and prove the test goes red by
   constructing twice.
6. Test **(BEH-12 explicitly)**: a **second and subsequent** `Embed` call pays **no load cost** — the
   same construction counter must still read 1 after many calls, and no asset read occurs after the
   first. BEH-12 is what makes PRF-05's "model already loaded" exclusion legitimate, so it is asserted
   in its own named test rather than inferred from the single-construction count.
7. Test **(AC-06, TOK-02)**: tokenizer output matches the reference tokenizer for the `diacritics`,
   `stripped`, `runtogether` and `abbrev` fixture categories. **Prove it can fail** — perturb one
   expected token and confirm red.
8. Tag the slow ones `[Trait("Category", "ML")]` per existing convention so `--filter "Category!=ML"`
   stays fast.
9. `gitnexus_detect_changes` before commit; `rtk dotnet build`; `rtk dotnet test` at baseline.

**Acceptance criteria.** **AC-06, AC-16 (partial), AC-17, AC-28 (rows: model missing, load failure,
tokenizer failure, inference failure, malformed input).**

**Risks.** R-7, R-10, R-11 (§11).

**Rollback.** Revert the WP's commits, including the csproj package addition. The registered provider
is still the null one, so production behaviour never depended on this.

**Owner checkpoint.** No — CP2 (WP-1.1) precedes it when a bump is implied.

---

### WP-1.5 — Cross-cutting guard tests

**Objective.** Build the guards that no existing surface provides: the offline-first architecture
test, the once-per-submit test, the startup-safety test, and the frozen-encoder static check.

**Scope.** AST-01, ARC-06, BEH-02, ARC-01, ARC-02, BEH-11, FLB-02, FLB-03.

**Files / surfaces.**
- Create: `SmartStudyPlanner.Tests/Architecture/MlLayerOfflineTests.cs` — **new test area**;
  `Tests/Infrastructure/` currently holds only `Persistence/` **[verified]**
- Create: `SmartStudyPlanner.Tests/ViewModels/QuickParseSubmitOnlyTests.cs`
- Create: `SmartStudyPlanner.Tests/Services/ML/Embedding/EncoderLifecycleTests.cs`
- Modify: `SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj` — include
  `datasheets/vn_input_fixtures.csv` as content, so DAT-05's set is *the* set the suite reads

**Dependencies.** WP-1.4.

**Implementation strategy.**

1. **AC-04 — offline-first architecture test.** Assert by reflection over the ML layer's types
   (`SmartStudyPlanner.Services.ML.*`, `SmartStudyPlanner.Core.ML.*`) that **no network-capable type
   is reachable** — walk field, property, parameter and return types transitively and fail on
   `System.Net.*`, `HttpClient`, `WebClient`, `Socket`. **The check must fail if one is introduced**
   — prove it by temporarily adding an `HttpClient` field to a scratch ML type and confirming red.
   That demonstration is the whole value of the test; without it, a green run means nothing.
2. **AC-05 — once per submit.** `QuanLyTaskViewModel` **already accepts an injected
   `IParsingOrchestrator`** (constructor overloads at lines 82 and 85, used by four existing test
   files **[verified]**), so **no new testability seam is required** and REL-01 is not at risk.
   - Inject a counting fake orchestrator.
   - Set `VanBanNhapNhanh` **many times** — the TextBox binds with `UpdateSourceTrigger=PropertyChanged`,
     so this is exactly what typing does — and assert **zero** `Parse` calls.
   - Invoke the `PhanTichNhapNhanh` command once and assert **exactly one** `Parse` call.
   - This locks BEH-02 as a requirement rather than an accident. At S2 (WP-2.7) it is re-asserted with
     an **inference-counting embedding provider**, which is when "once per submit" becomes a statement
     about the *encoder*.
3. **AC-16 — startup safety.** Assert the encoder is **not loaded on the startup path** and that
   **startup succeeds with a corrupt asset present**. `App.xaml.cs` already initialises both model
   managers on fire-and-forget background tasks with swallowed exceptions **[verified, lines 66–90]**
   — so BEH-11/FLB-02 hold structurally today. The test pins that structure so a future change cannot
   quietly move encoder load onto the startup path.
4. **AC-26 — frozen encoder.** A static check asserting no code path updates encoder weights and no
   encoder training occurs in the shipped application: assert the ML layer references no ONNX
   Runtime training entry point, and record the code-review obligation in the PR template.

**Verification.** Each of the four guards is **individually proven able to go red** by a temporary
mutation, and each demonstration is recorded in the PR description. `rtk dotnet test` at baseline plus
the new tests.

**Acceptance criteria.** **AC-04, AC-05 (first occurrence), AC-16, AC-26.**

**Risks.** The reflection walk is either so shallow it proves nothing or so deep it is flaky.
Mitigation: the red-demonstration in step 1 is the calibration — it must catch a realistically
introduced `HttpClient`, not only a contrived one.

**Rollback.** Revert; tests only.

**Owner checkpoint.** No.

---

### WP-1.6 — S1 no-behaviour-delta verification and PR

**Objective.** Demonstrate S1's correctness the way REL-01 defines it — by the **absence** of a
behaviour delta.

**Scope.** REL-01, BEH-07, BEH-13, DAT-05.

**Files / surfaces.**
- Create: `SmartStudyPlanner.Tests/Core/Parsing/ParseOutputCharacterizationTests.cs`
- Modify: PR description.

**Dependencies.** WP-1.5.

**Implementation strategy.**

1. **Fixture-corpus comparison (AC-13).** For every row of `datasheets/vn_input_fixtures.csv`, capture
   the full parse output — `TenTask`, `HanChot`, `Loai`, `DoKho`, `Confidence`, and **`Source`
   (`ParseSource.Heuristic` vs `MlAugmented`) [verified enum]** — and assert it is unchanged from the
   pre-slice baseline. **`Source` is the provenance marker BEH-07 protects**; a delta there is a
   behaviour change even when every other field matches.
   Use a deterministic `IClock` — `HanChot` derives from `_clock.Now.AddDays(1)` **[verified]** and a
   wall-clock-dependent expectation is a known source of false failures in this suite.
2. **Suite at baseline (AC-13).** Measure the count at slice start; require no regression at slice end.
3. **BEH-13 sanity.** The head-retrain lifecycle is untouched by S1 — assert the existing
   `TextClassifierSchemaTests` still pass unchanged. The real BEH-13 work is WP-2.2.
4. **PR description** states plainly: *no user-visible behaviour change; the registered
   `ITextEmbeddingProvider` is still the null implementation.*

**Verification.** `rtk dotnet build` clean · `rtk dotnet test` at the measured baseline ·
`gitnexus_detect_changes` shows only the S1 file map · CI green.

**Acceptance criteria.** **AC-13, AC-21 (re-verified), AC-25.**

**Risks.** A characterization test written *after* the change bakes in a delta as the expectation.
Mitigation: capture the expectations on the **pre-slice commit** and commit them before WP-1.2's code.

**Rollback.** S1 is a single PR — revert the merge. Production still resolves the null provider, so no
user-visible state depends on it.

**Owner checkpoint.** No.

---

## PHASE S2 + S3 — one production release unit

> ### The mechanism that makes CNF-09 structural, not aspirational
>
> **REL-02 forbids S2 reaching a production state without S3.** This plan enforces it with a
> mechanism rather than a promise:
>
> 1. **One branch, one PR** for WP-2.1 … WP-2.7. Nothing merges to `dev` in between.
> 2. **The neural path is unreachable in production until the final commit.** Production keeps
>    resolving `ITextEmbeddingProvider` to the **null** implementation through WP-2.1 … WP-2.6. The
>    swap is exercised by tests that inject the ONNX provider directly. **WP-2.7 is the only commit
>    that changes the registration**, and it lands together with the derived threshold.
> 3. **AC-14 is verified by branch/release-history review** — the reviewer confirms no merge to `dev`
>    or `main` exists in which the featurizer is swapped and the gate is not re-derived.
>
> This is why the internal ordering (measure → derive → ship) is permitted by PD-4 while the
> intermediate production state is not: the intermediate never becomes reachable.

---

### WP-2.1 — Embedding feature column and featurizer swap

**Objective.** Make the classifier consume the embedding vector as its feature representation, behind
the seam, without making the neural path reachable in production.

**Scope.** BEH-06, ARC-03, ARC-04.

**Files / surfaces.**
- Modify: `SmartStudyPlanner/Services/ML/Schema/TextClassifierInput.cs`
- Modify: `SmartStudyPlanner/Services/ML/TextClassifierModelManager.cs`
- Modify: `SmartStudyPlanner/Services/ML/TextClassifierService.cs` (only if the DTO shape moves)
- Modify: `SmartStudyPlanner.Tests/Services/ML/TextClassifierSchemaTests.cs`

**Dependencies.** WP-1.6.

**Pre-edit impact analysis — mandatory.** Run `gitnexus_impact` upstream on
`TextClassifierModelManager.Predict` and `TextClassifierModelManager.TrainAndSaveAsync` (expected
MEDIUM — single consumer via `TextClassifierService`). **Report the blast radius before editing, and
warn the owner if it returns HIGH or CRITICAL.**

**Implementation strategy.**

1. Add an embedding column to `TextClassifierInput`:
   ```
   [VectorType(EmbeddingRank)]
   public float[] Embedding { get; set; } = Array.Empty<float>();
   ```
   **`VectorType` requires a compile-time constant**, so the rank recorded in WP-1.0 becomes a `const
   int EmbeddingRank` here. This is the one place an S0-derived value is frozen into source — and it is
   frozen *from the report*, not guessed. Document the source report in the XML doc comment.
2. Replace `FeaturizeText("Features","InputText")` with a direct mapping from `Embedding` to
   `"Features"`. **Keep everything else identical** — `MapValueToKey("Label","TaskType")`,
   `SdcaMaximumEntropy("Label","Features")`, `MapKeyToValue("PredictedLabel")`, `MLContext(seed: 42)`
   **[verified]**. **ARC-04: the decision layer stays linear and authoritative.** The encoder supplies
   a representation; the head still produces the label and the confidence.
3. Inject `ITextEmbeddingProvider` into `TextClassifierModelManager`. `Predict` embeds `InputText`,
   then predicts. **When the provider is unavailable (`Embed` returns `null`), the manager reports
   not-ready and `Predict` returns `null`** — which the existing `IntentClassifierAdapter` already
   translates into a heuristic result **[verified]**. Tier 0 therefore falls out of the existing seam
   rather than needing new machinery.
4. **Do not change the registration.** Production still resolves the null provider. See the phase
   preamble.

**Verification (TDD order).**
1. Test: with an injected fake provider returning a fixed vector, the classifier trains from the
   embedded seed and predicts.
2. Test **(AC-17, second occurrence)**: for an **unchanged vector**, the head's decision is unchanged
   — pin the label and the score, so a future pipeline edit that perturbs the decision goes red.
3. Test: with the **null** provider, the manager reports not ready and `Predict` returns `null`.
4. `gitnexus_detect_changes` must show only the WP's file map.

**Acceptance criteria.** **AC-17 (second occurrence), AC-25.**

**Risks.** R-17 (§11) — seed embedding cost during retraining.

**Rollback.** Revert within the branch; nothing has merged.

**Owner checkpoint.** No.

---

### WP-2.2 — Head-retrain lifecycle preservation

**Objective.** Prove the existing retrain lifecycle survives the swap intact — the requirement most
likely to break silently.

**Scope.** BEH-13.

**Files / surfaces.**
- Modify: `SmartStudyPlanner/Services/ML/TextClassifierModelManager.cs`
- Modify: `SmartStudyPlanner.Tests/Services/ML/TextClassifierSchemaTests.cs`
- Create: `SmartStudyPlanner.Tests/Services/ML/TextClassifierRetrainLifecycleTests.cs`

**Dependencies.** WP-2.1.

**Implementation strategy.**

The four lifecycle behaviours that must survive, all present today **[verified]**:

| Behaviour | Where it lives | Must still |
|---|---|---|
| Train from the embedded seed | `LoadSeedData()`, `SeedResourceSuffix` | produce a working model with no model file on disk |
| Seed-hash staleness gate | `ComputeSeedHash()`, `ModelMeta.SeedHash` | force a retrain when the shipped seed differs from the cached model's seed |
| Atomic model swap | `TrainAndSaveAsync` | leave no partially written model on failure |
| Model-version increment | `ModelMeta.ModelVersion + 1` | increment on every retrain |

**R-17 mitigation.** Training now embeds every seed row — **698 training rows** through the encoder.
At the per-inference latency WP-0.7 measured, this is the difference between a retrain that finishes
in seconds and one that takes minutes. It does **not** violate BEH-11 (initialisation is already
fire-and-forget on a background task **[verified]**), but it does mean the classifier is unavailable
for that window on first run after a seed change.
- **Batch the embedding calls** rather than looping one row at a time.
- **Measure the actual retrain wall time** and record it in the release notes as a known first-run
  characteristic.
- If the measured time is materially worse than WP-0.7's projection, that is a finding for the
  release notes — not something to absorb silently.

**Verification (TDD order).**
1. Test **(AC-15)**: a **stale seed hash forces a retrain** — write a `meta.json` with a wrong
   `SeedHash`, initialise, assert a retrain occurred and `ModelVersion` incremented.
2. Test **(AC-15)**: the swap is **atomic** — force a failure mid-write and assert no partial model is
   left behind and the previous model still loads.
3. Test **(AC-15)**: training from the embedded seed works with **no model file on disk**.
4. **Prove each can go red** — e.g. bypass the hash comparison and confirm test 1 fails.
5. Tests use temp directories; **CI fails on any write into the real user profile [verified step]**.

**Acceptance criteria.** **AC-15.**

**Risks.** R-17, R-8 (§11).

**Rollback.** Revert within the branch.

**Owner checkpoint.** No.

---

### WP-2.3 — Confidence distribution measurement

**Objective.** Measure what the *new* featurizer's confidence actually looks like — the input CNF-03
requires and which by definition cannot exist before WP-2.1.

**Scope.** CNF-03 (input).

**Files / surfaces.**
- Create: `tools/ml-pilot/recalibration/` — measurement harness reusing WP-0.4's split.
- Create: `docs/reports/2026-XX-XX-confidence-recalibration.md`.

**Dependencies.** WP-2.2.

**Implementation strategy.**

1. Run the **post-swap** classifier over WP-0.4's **held-out** split and record per-row
   `(confidence, predicted, actual)`.
2. Produce the confidence-versus-accuracy relationship **with bin population counts**, in the same
   bins WP-0.5 used, so the pre- and post-swap distributions are directly comparable.
3. Record the heuristic parser's agreement rate with the ML label at each confidence level — this is
   the candidate independent signal for CNF-02, and measuring it here is what makes WP-2.5's choice
   evidence-based rather than assumed.
4. **This is the internal separation PD-4 permits.** It happens on the branch; nothing ships.

**Verification.** The new distribution is compared against S0's on identical bins. Bin populations are
reported, not just rates.

**Acceptance criteria.** Feeds AC-09, AC-10.

**Risks.** The distribution is measured on the same 3-of-5-class data, so the derived threshold
inherits that coverage limit. Mitigation: **DAT-01** — the limitation is restated in this report and
carried into the release notes (AC-29). It is a bounded limitation, not a defect.

**Rollback.** Delete the harness; the report stands as evidence.

**Owner checkpoint.** No.

---

### WP-2.4 — Confidence policy split (CNF-05)

**Objective.** Make it **impossible** for the parser's recalibration to move the weight optimizer's
review/apply tiers.

**Scope.** CNF-05.

**Files / surfaces.**
- Create: `SmartStudyPlanner/Services/ML/ParserIntentConfidencePolicy.cs`
- **Do not modify** `SmartStudyPlanner/Services/ML/DefaultMlConfidencePolicy.cs`
- Modify: `SmartStudyPlanner/Services/ServiceLocator.cs` (lines 96–100 **[verified]**)
- Create: `SmartStudyPlanner.Tests/Services/ML/WeightOptimizerTierBoundaryTests.cs`
- Modify: `SmartStudyPlanner.Tests/Services/ML/IntentClassifierAdapterTests.cs`

**Dependencies.** WP-2.3.

**Pre-edit impact analysis — mandatory.** `gitnexus_impact` on `DefaultMlConfidencePolicy.Decide`
(expected **HIGH** — shared with M8-B). **Warn the owner before proceeding.**

**Why a split is forced, not chosen.** Verified in code:
- `DefaultMlConfidencePolicy` is registered **once** as a singleton (`ServiceLocator.cs:96`).
- It is consumed by `IntentClassifierAdapter` (constructor-injected, `ServiceLocator.cs:97–100`)
  **and** by `WeightOptimizerViewModel` (`ServiceLocator.Get<IMlConfidencePolicy>()`,
  `WeightOptimizerViewModel.cs:46`) — **two different call-site shapes**.
- `Decide` reads `ReviewThreshold = 0.60` and `AutoApplyThreshold = 0.75`. Changing `0.60` moves the
  **Reject/Review boundary the weight optimizer also reads.**

So re-deriving the parser threshold on the shared type **necessarily retunes M8-B** — precisely what
CNF-05 forbids. CNF-05's own instruction applies: *"the policies MUST be separated rather than both
retuned."*

**Implementation strategy — minimal blast radius.**

1. **Leave `DefaultMlConfidencePolicy` completely untouched**, thresholds included. The weight
   optimizer keeps resolving it and its behaviour does not change **by construction** — the strongest
   available form of AC-11.
2. Add `ParserIntentConfidencePolicy : IMlConfidencePolicy` carrying the derived parser threshold
   (value supplied by WP-2.5).
3. Change **only** the `IIntentClassifier` registration (lines 97–100) to inject the new policy. The
   `AddSingleton<IMlConfidencePolicy, DefaultMlConfidencePolicy>` registration at line 96 stays, so
   `WeightOptimizerViewModel`'s `ServiceLocator.Get<IMlConfidencePolicy>()` resolves exactly what it
   resolves today.

**Verification (TDD order) — the trap that must be avoided.**

`WeightOptimizerTests.cs:87` and `:95` assert against **`DefaultMlConfidencePolicy.ReviewThreshold`
and `.AutoApplyThreshold` as symbols [verified]**. If a threshold constant changed, **those tests
would move with it and stay green** — so they cannot satisfy AC-11.

1. Write `WeightOptimizerTierBoundaryTests` pinning the weight-optimizer tier boundaries to
   **literal `0.60` and `0.75`**, not to the constants. Assert `Decide(0.59) == Reject`,
   `Decide(0.60) == Review`, `Decide(0.74) == Review`, `Decide(0.75) == AutoApply`.
2. **Prove it can go red**: temporarily change `ReviewThreshold` to `0.55` and confirm the new test
   fails while the existing symbol-referencing tests still pass. That contrast **is** the evidence
   AC-11 asks for. Revert the mutation.
3. Test that the parser adapter and the weight-optimizer path resolve **different policy instances**.
4. `gitnexus_detect_changes` must show `ServiceLocator` and the new type — **not**
   `DefaultMlConfidencePolicy`.

**Acceptance criteria.** **AC-11.**

**Risks.** R-4 (§11).

**Rollback.** Revert within the branch. Because `DefaultMlConfidencePolicy` is never edited, reverting
cannot damage M8-B.

**Owner checkpoint.** No.

---

### WP-2.5 — Threshold derivation and dual-signal gate

**Objective.** Derive the routing threshold from measured evidence, record the derivation where the
value lives, and add the independent signal the project's own rules require.

**Scope.** CNF-01, CNF-02, CNF-03, CNF-04, **BEH-08** (ML output stays advisory — at or above the gate
the ML label MAY be applied, below it the heuristic result is what the user receives), **BEH-09** (the
ML path never silently overrides heuristic logic and never produces a user-visible result whose
provenance the application cannot report).

**Files / surfaces.**
- Modify: `SmartStudyPlanner/Services/ML/ParserIntentConfidencePolicy.cs`
- Modify: `SmartStudyPlanner/Services/ML/IntentClassifierAdapter.cs`
- Modify: `SmartStudyPlanner/Core/Parsing/Contracts/IIntentClassifier.cs`
- Modify: `SmartStudyPlanner/Core/Parsing/Orchestrators/ParsingOrchestrator.cs`
- Modify: `SmartStudyPlanner.Tests/Services/ML/IntentClassifierAdapterTests.cs`
- Modify: test doubles implementing `IIntentClassifier`

**Dependencies.** WP-2.4.

**Pre-edit impact analysis — mandatory.** `gitnexus_impact` on `IntentClassifierAdapter.Classify` and
`ParsingOrchestrator.Parse` (expected **HIGH** — Smart Add entry point, user-visible routing, and the
`QuickInputHint` confidence string). **Warn the owner before proceeding.**

**Implementation strategy.**

**Step 1 — derive the threshold (CNF-03). The procedure, not a number.**

The value is **[gate]**-derived; this plan fixes the procedure and forbids the shortcut:

1. Input: WP-2.3's confidence-versus-accuracy relationship with bin populations.
2. **The existing 0.60 MUST NOT be carried over unexamined** — it is calibrated to
   SDCA-over-bag-of-n-grams, the featurizer being replaced. Reusing it would silently move the
   boundary between ML-augmented and heuristic results.
3. Choose the lowest confidence at which observed accuracy meets the standard the current gate
   actually delivers today — measured, not assumed — **subject to that bin holding enough population
   to support the claim.** If no bin qualifies, the honest outcome is a **more conservative** gate,
   not a lower bar.
4. State the reasoning in prose. A number without a recoverable derivation is what CNF-04 exists to
   prevent.

**Step 2 — record the derivation where the value lives (CNF-04).** In the XML doc comment on
`ParserIntentConfidencePolicy`: the **date**, the **report it came from**, and the **reasoning** — so a
future reader can tell a derived threshold from a guessed one.

**Step 3 — the independent signal (CNF-01, CNF-02).**

CNF-01 forbids the gate relying on the model's raw score as its **only** signal — an existing project
rule (*"never trust raw model confidence as the only gating signal — compare against the deterministic
baseline"*) that the current gate does not satisfy **[verified: `Predict` uses `output.Score.Max()`
and the adapter gates on that single number]**.

**Chosen signal: agreement with the heuristic task-type parser.** It is available at zero additional
cost — that parser already runs on every parse **[verified: `ParsingOrchestrator.Parse` calls
`_taskEngine.ExtractType` before consulting the classifier]**.

**Chosen placement: extend the `IIntentClassifier` contract to carry the heuristic label.**

```
// Core/Parsing/Contracts/IIntentClassifier.cs
IntentPrediction? Classify(string rawInput, LoaiCongViec heuristicLoai);
```

`ParsingOrchestrator.Parse` already computes `loaiHeuristic` immediately before calling
`_intentClassifier?.Classify(input)` **[verified]** — passing it costs nothing and guarantees the
adapter compares against **the same value the orchestrator will fall back to**.

*Alternatives considered and rejected:* (a) the adapter re-running `TaskExtractionEngine` itself — it
would have to replicate the orchestrator's exact lowering and default (`LoaiCongViec.BaiTapVeNha`), and
any drift would silently produce a signal that looks right and is not; (b) moving the gate decision
into `ParsingOrchestrator` — it would push ML policy into Core, which BEH-03 and the §5.1 isolation
rule argue against.

Gate logic: a prediction whose confidence clears the derived threshold **and** agrees with the
heuristic label is applied. Disagreement at any confidence is treated conservatively —
**disagreement is exactly the case where the raw score is least trustworthy.** Where the ML label is
not applied, the heuristic result is delivered and `ParseSource` reflects it (CNF-07, BEH-07).

**Verification (TDD order).**
1. Test: confidence just below the derived threshold → `null` → heuristic result, `Source ==
   ParseSource.Heuristic`.
2. Test: confidence above threshold **and** agreeing with the heuristic → ML label applied,
   `Source == ParseSource.MlAugmented`.
3. Test: confidence above threshold **but disagreeing** → conservative branch, asserted explicitly.
4. Test **(AC-25)**: parser isolation still holds — no scheduling, allocation or balancing effect.
5. Test: the adapter still **never throws** — a throwing service yields `null` **[existing behaviour,
   verified]**.
6. `gitnexus_detect_changes` before commit.

**Acceptance criteria.** **AC-09 (with WP-2.6), AC-10, AC-25.**

**Risks.** R-3, R-18 (§11).

**Rollback.** Revert within the branch. The contract change touches one interface, one implementation,
one call site, and the test doubles.

**Owner checkpoint.** No.

---

### WP-2.6 — Gate mutation test

**Objective.** Prove the confidence gate can **fail** — because a gate whose pass is
indistinguishable from a broken gate is not evidence.

**Scope.** FLB-03, and the verifiability of CNF-01, CNF-02, CNF-03.

**Files / surfaces.**
- Create: `SmartStudyPlanner.Tests/Services/ML/ConfidenceGateMutationTests.cs`
- Modify: PR description — record the red runs.

**Dependencies.** WP-2.5.

**Implementation strategy.**

AC-09 requires that **a deliberately miscalibrated threshold makes a test go red.** Structure it so
the mutation is a test input rather than a source edit:

1. Parameterise the gate tests over the policy instance, so a miscalibrated policy can be injected.
2. Assert that a policy with a **deliberately wrong threshold** produces routing decisions that fail
   the expectations — for a fixed set of `(confidence, heuristic agreement)` cases drawn from WP-2.3's
   measured distribution.
3. Assert the same for a **defeated independent signal** — a policy that ignores heuristic agreement
   must fail a case the dual-signal gate passes. This proves CNF-02's signal is *load-bearing* rather
   than decorative.
4. Record in the PR description **which mutations were run and that each went red.**

**Verification.** Every mutation listed produces a red run; the unmutated configuration is green.

**Acceptance criteria.** **AC-09.**

**Risks.** The mutation test is written so loosely that any policy passes. Mitigation: the mutations
are enumerated in the PR description and each must be shown red — a claim without a red run is not
accepted.

**Rollback.** Revert; tests only.

**Owner checkpoint.** No.

---

### WP-2.7 — Wire-up flip, latency re-check, and release

**Objective.** The **single commit** that makes the neural path live — together with the derived gate
— followed by the release-level verification and documentation.

**Scope.** CNF-06, CNF-07, CNF-09, REL-02, REL-03, PRF-04, PRF-05, DOC-03, DOC-04.

**Files / surfaces.**
- Modify: `SmartStudyPlanner/Services/ServiceLocator.cs` — resolve `ITextEmbeddingProvider` to
  `OnnxTextEmbeddingProvider`
- Modify: `docs/architecture/` — the documents describing shipped ML behaviour
- Modify: changelog / release notes
- Modify: PR description

**Dependencies.** WP-2.6.

**Implementation strategy.**

1. **The flip.** One commit changes the registration. Before it, production resolved the null provider
   and the neural path was unreachable; after it, the featurizer swap **and** the recalibrated gate are
   both live. **This ordering is what makes AC-14 checkable from history.**
2. **AC-05, second occurrence.** Re-assert once-per-submit with an **inference-counting embedding
   provider**: many `VanBanNhapNhanh` changes → **zero** encoder inferences; one command invocation →
   **exactly one**. At S1 this was a statement about the parse path; here it becomes a statement about
   the encoder, which is what BEH-02 actually requires.
3. **AC-23, second occurrence — latency re-check.** Re-measure Smart Add **submit-to-populate** on the
   **PRF-01 reference class**, CPU provider, model already loaded, over the **PRF-05 boundary**, using
   **the protocol recorded in the S0 report**. Confirm it stays under **500 ms** (PD-12). Report the
   number with its machine and protocol. If it breaches, that is a release blocker, not a caveat.
4. **AC-03, first mandated re-run — the zero-model-file fault-tolerance check.** **Delete every model
   file, including the bundled encoder asset**, launch, and confirm **Smart Add, Dashboard and
   Analytics all function**. Under bundling the asset is expected to be present, so this gate
   deliberately deletes something the build placed there — it tests **fault tolerance, not an install
   variant** (AST-09). Automate what can be automated; the launch-and-click portion is manual QA.
   Record PASS/FAIL per surface, decided **before** running.
5. **AC-12 / CNF-06 — user-visible semantics.** Confirm the confidence percentage shown in
   `QuickInputHint` (`"AI gợi ý Loại: {loai} ({conf:P0})"`) is **the same quantity the gate reads**.
   If recalibration changed what that number means or what value it typically takes, **that is a
   behaviour change and ships as one.**
6. **REL-03 — describe it as a behaviour change.** The changelog entry and the PR title/description
   say **behaviour change**, not refactor. Name what changes for the user: classification quality,
   routing between ML-augmented and heuristic results, and the displayed confidence value.
7. **AC-29 — carry the coverage limitation.** Release notes state the 3-of-5-class evaluation bound.
   **No general production-accuracy claim** is made from it (DAT-01).
8. **AC-32 / DOC-03 — update `docs/architecture/` now, not before.** Those documents describe
   **shipped** behaviour and were correct as written until this moment. **DOC-04: this is an
   owner-approved documentation edit in its own right — it must not ride along inside an
   implementation commit.** Separate commit, clearly labelled.
9. **AC-27 — artifact inventory.** Confirm exactly **one** deployed model artifact is introduced and
   **no additional prediction head exists**. S5 and S6 remain unactivated (REL-04).

**Verification.** Full suite green at or above baseline · CI green · single PR containing WP-2.1 …
WP-2.7 · branch history shows no intermediate merge to `dev`/`main` with a swapped featurizer and an
underived gate.

**Acceptance criteria.** **AC-03 (S2 occurrence), AC-05 (second), AC-12, AC-14, AC-21 (re-verified),
AC-23 (second), AC-27, AC-29 (second), AC-32.**

**Risks.** R-3, R-8, R-19 (§11).

**Rollback boundary.** **This is the phase's rollback boundary.** Reverting the single PR returns
production to S1's state — null provider, legacy featurizer, original gate, no user-visible change.
There is no partial revert that leaves a swapped featurizer with an underived gate, because no commit
in the branch ever reached production in that state.

**Owner checkpoint.** No — but CP3 follows before any S4 work begins.

---

## PHASE S4 — Runtime tiering and distribution

---

### WP-4.0 — ⛔ OWNER CHECKPOINT 3 — S4 parameters

**Objective.** Obtain the three decisions PD-11 deferred to this point, **before** any S4
implementation begins.

**Scope.** AST-04, OP-1, OP-4, OP-6.

**Dependencies.** WP-2.7.

**What the owner is being asked to decide.**

1. **OP-1 — the package/model size cap value.** **Currently unset.** The *"1–2 GB acceptable, >2 GB
   reopens debate"* remark from requirements gathering is an **install-size preference, not the cap**,
   and MUST NOT be treated as one. **AC-20 requires the cap to have a value before S4 packaging is
   implemented.** Input: S0's EVA-08 output 8 (measured packaged size).
2. **OP-6 — the delivery mechanism.** Policy is settled and **not reopened**: bundled, one build, no
   first-run download, no CDN, no auto-update, no sanctioned side-loading (AST-02, AST-03). Only the
   *mechanism* is open. The recorded option set (proposal §S4):

   | Option | Shape | Trade |
   |---|---|---|
   | **a** — build-time fetch, shipped in output | MSBuild target fetches the encoder at a pinned revision with SHA-256 verification; the `dotnet publish` folder is the deliverable | Satisfies every AST-02/03 clause, keeps git clean, needs no installer. Makes the build network-dependent. **Assumes folder-handoff distribution — unverified (OP-7)** |
   | **b** — Git LFS | Asset travels with the clone | Build stays offline; ~250 MB per version against a small free quota with CI clones; no `.gitattributes` exists today; unwinding later is painful |
   | **c** — build the installer | Pull post-Epic-2 productionisation forward | Matches PD-5 exactly and gives the project the release story it lacks. A whole unscoped workstream, sequenced ahead of Epic 2 |
   | **d** — documented manual asset drop | Group places the file once; Tier 0 until then | Zero pipeline work. **This is side-loading, which AST-03 refuses** — choosing it means *amending* the ratified policy, not answering the question |

   The standing recommendation is **(a)**, conditional on **OP-7** — *how the application currently
   reaches its users*, which **no document in the repository records**. **The owner must answer OP-7
   for (a) to be decidable.**
3. **OP-4 — the peak-memory ceiling**, derived from S0's measurement against the 8 GB budget.
   **PRF-08 requires deriving it from the measurement, not reverse-engineering it** from whatever the
   winner used.

**Also confirm here.** Whether the **DirectML capability-probe mechanism** (OP-12/P2) and the
packaging mechanics (OP-12/P1) are left to implementation. These are **planning questions, not owner
policy** — the owner declined to expand them, and they must not be dressed up as decisions.

**Exit criteria.** A recorded owner decision naming: the cap value, the delivery mechanism, the
memory ceiling, and the answer to OP-7. **No S4 packaging is written before it exists** (AC-20).

**Acceptance criteria.** **AC-20 (checkpoint occurrence).**

**Owner checkpoint.** **YES — blocking.**

---

### WP-4.1 — Execution-provider probe and tier resolution

**Objective.** Resolve Tier 0 / 1 / 2 at runtime by capability probe, with CPU as the default.

**Scope.** AST-07, ARC-09, FLB-01.

**Files / surfaces.**
- Create: `SmartStudyPlanner/Services/ML/Embedding/ExecutionProviderProbe.cs`
- Modify: `SmartStudyPlanner/Services/ML/Embedding/OnnxTextEmbeddingProvider.cs`
- Create: `SmartStudyPlanner.Tests/Services/ML/Embedding/ExecutionTierTests.cs`

**Dependencies.** WP-4.0.

**Implementation strategy.**

1. **Tier 1 (CPU) is the default and the baseline.** DirectML is acceleration only and **MUST NOT be a
   precondition for any specified behaviour or for meeting §7** (AST-07).
2. **Tier 0** when the asset is absent, unreadable, corrupt, or the session cannot be constructed →
   heuristic-only, fully functional.
3. **Tier 2** only on explicit opt-in **and** after WP-4.2's parity check.
4. **FLB-01 fallback chain:** an unsupported or unavailable execution provider falls back to CPU; if
   CPU is unavailable, Tier 0. **A Tier 2 request that cannot be honoured MUST NOT fail the parse.**

**Verification (TDD order).**
1. Test: each of Tier 0, 1, 2 is exercised and its resolution asserted.
2. Test: requesting an unavailable provider falls back to CPU without throwing.
3. Test: with CPU unavailable, Tier 0 is entered and the parse still returns the heuristic result.
4. **Prove the probe can go red** — force a probe failure and confirm the fallback path is taken
   rather than an exception escaping.

**Acceptance criteria.** **AC-19 (automated portion).**

**Risks.** R-6, R-14 (§11).

**Rollback.** Revert; the provider's default path is CPU, unchanged from S2+S3.

**Owner checkpoint.** No.

---

### WP-4.2 — Tier 2 parity check and opt-in surface

**Objective.** Make Tier 2 safe to trust — opt-in, and never used for a user-visible result until its
output matches CPU.

**Scope.** AST-08.

**Files / surfaces.**
- Modify: `SmartStudyPlanner/Services/ML/Embedding/ExecutionProviderProbe.cs`
- Modify: settings view + view-model (tier display and Tier 2 opt-in toggle)
- Create: `SmartStudyPlanner.Tests/Services/ML/Embedding/TierParityTests.cs`

**Dependencies.** WP-4.1.

**Implementation strategy.**

1. **Opt-in only.** Tier 2 is never entered on availability alone.
2. **Output-parity check against the CPU provider** before Tier 2 serves any user-visible result:
   embed the WP-0.2 fixture set on both providers and compare within the tolerance documented in
   WP-1.4.
3. **On parity failure, Tier 2 is not used** and the CPU provider serves instead. This is the
   fallback, not an error.

> **Why this exists.** A known metacommand defect between ONNX Runtime/DirectML and Intel drivers
> affects inference accuracy at certain dimensions. Tier 2 is not trusted on availability alone.

**Verification (TDD order).**
1. Test: parity failure → Tier 2 rejected, CPU serves, no exception.
2. Test: without opt-in, Tier 2 is never selected even when available.
3. **Prove the parity check can go red** — inject a deliberately divergent second provider and confirm
   rejection. A parity check that cannot fail is not a check.

**Acceptance criteria.** **AC-19.**

**Risks.** R-6 (§11).

**Rollback.** Revert; Tier 1 remains the default and nothing user-visible depended on Tier 2.

**Owner checkpoint.** No.

---

### WP-4.3 — Bundled asset integration and size-cap check

**Objective.** Make the encoder present after a normal install, with no user action and no network —
by the mechanism the owner chose at CP3.

**Scope.** AST-02, AST-03, AST-04, AST-05.

**Files / surfaces.** **Determined by the CP3 decision.** Under option (a): build/publish
configuration plus a pinned-revision, hash-verified fetch target. Under (b): `.gitattributes` and LFS
configuration. Under (c): a separate installer workstream. **Do not invent packaging mechanics before
CP3 decides** — that is OP-12/P1, and pre-fixing it here would freeze a detail the owner owns.

Also: Modify `BaseDirectoryEncoderAssetLocator`'s **default root** to the location the chosen
mechanism produces (WP-1.3 made it injectable precisely so this is a default change, not a redesign).

**Dependencies.** WP-4.0, WP-4.2.

**Implementation strategy.**

1. Implement only the mechanism CP3 chose.
2. **AST-05 holds throughout:** no encoder binary enters git under any mechanism. The WP-0.1 CI guard
   is re-verified here.
3. **AST-01/AST-03 hold at runtime:** whatever the build does, **no component of the ML layer performs
   a network operation at runtime** — not at first run, not at load, not at inference, not for
   telemetry, not for updates. The WP-1.5 architecture test is re-run.
   **State plainly what that green result does and does not prove.** AC-04's check scans
   `Services.ML.*` / `Core.ML.*` **types**; a build-time fetch target lives in neither, so AC-04
   passing says nothing about the fetch. That is correct — a **build-time** fetch is permitted, only a
   **runtime** acquisition path is forbidden — but the distinction must be written into the WP's
   report, or a reviewer at CP3 will read a green AC-04 as clearing something it never tested. This is
   R-12's failure mode wearing different clothes.
4. **Size-cap check (AC-20).** Measure the packaged size and compare against the CP3 cap value.
   **On breach: STOP-7 — stop and reopen the owner decision.** Do **not** silently side-load, raise
   the cap, or substitute a smaller model. Absorbing a breach is the specific failure AST-04 exists to
   prevent.

**Verification.** Packaged size measured and recorded against the cap · AC-04 architecture test green ·
AC-21 CI guard green · a clean install on a machine with **no network** yields **Tier 1, not Tier 0**
(AC-22, manual QA).

**Acceptance criteria.** **AC-20 (packaging occurrence), AC-21 (re-verified), AC-22, AC-04 (re-run).**

**Risks.** R-5, R-9, R-13 (§11).

**Rollback.** Revert the packaging change; the application falls back to Tier 0 and remains fully
functional — which is exactly what AST-09 guarantees and WP-4.4 tests.

**Owner checkpoint.** No — CP3 precedes it.

---

### WP-4.4 — Tier 0 re-verification and manual QA

**Objective.** Prove, at the end, that the application the user receives still works with every model
file deleted.

**Scope.** AST-09, BEH-10, CNF-08, FLB-01, FLB-02, FLB-03.

**Files / surfaces.**
- Create: `docs/plans/2026-XX-XX-encoder-s4-manual-qa-runbook.md` (runbook shape — exempt from the
  six plan sections, per `docs/plans/README.md`)
- Create: `docs/reports/2026-XX-XX-encoder-s4-qa.md`

**Dependencies.** WP-4.3.

**Implementation strategy.**

1. **AC-03, second mandated occurrence.** **Delete every model file — including the bundled encoder
   asset** — launch, and confirm **Smart Add, Dashboard and Analytics all function.** Under bundling
   the asset is expected present, so this deliberately removes what the build placed. **Tier 0 is a
   fault-tolerance state, not an install variant** (AST-09).
2. **AC-19 manual portion.** Exercise Tiers 0, 1, 2 by hand. Confirm Tier 2 is opt-in and is not used
   for user-visible results unless parity passed.
3. **AC-22.** Install normally with **no network access** and confirm the asset is present with no
   user action, yielding **Tier 1, not Tier 0**.
4. **AC-28 walk.** Walk every row of §10's failure table and confirm each has a test demonstrating the
   heuristic result is delivered and **no exception escapes**: model missing · model load failure ·
   tokenizer failure · inference failure after load · unavailable execution provider · Tier 2 parity
   failure · confidence below gate · malformed input (empty, whitespace-only, pathologically long).

**Manual-check discipline — required before running.**
- **Write the PASS/FAIL criteria for every check before executing any of them.**
- **Test the observation channel independently** — confirm the QA procedure can actually detect a
  failure by deliberately breaking one surface first and seeing the check report FAIL.
- **Verify artifact provenance** — confirm the build under test is the one the packaging step
  produced, not a stale local build.
- **In the report, distinguish observation from ruling from inference.** If an instrument turns out to
  be broken, results taken with it are **withdrawn**, not reinterpreted.
- The QA report carries a **"Decisions made"** section (`docs/reports/README.md`).

**Acceptance criteria.** **AC-03 (S4 occurrence), AC-19 (manual portion), AC-22, AC-28.**

**Risks.** R-8, R-14 (§11).

**Rollback.** WP-4.3's rollback applies; Tier 0 is by construction the safe state.

**Owner checkpoint.** No.

---

# 7. Parallelism opportunities

## 7.1 Decision

**Do not parallelise across phases.** S0 → S1 → S2+S3 → S4 are strictly sequential: S0 gates
everything, S1's seam is S2's precondition, S3 depends on a distribution that cannot exist before S2,
and S4 depends on the released S2+S3 unit.

**Exactly one genuinely parallel pair exists: WP-0.6 ∥ WP-0.7.**

## 7.2 Why that pair is safe

Tested against the three conditions the task requires:

| Condition | WP-0.6 (accuracy) ∥ WP-0.7 (runtime) |
|---|---|
| **Inputs independent?** | ✅ Both consume WP-0.4's split and WP-0.3's artifacts — **read-only, already frozen** |
| **Outputs mutate shared state?** | ✅ No. Separate result files (`arm_*.json` vs `runtime_*.json`), separate directories (`accuracy/` vs `dotnet/`) |
| **Does one invalidate the other?** | ✅ No. Accuracy does not depend on runtime figures, and runtime characterisation needs the **model exports**, not WP-0.6's results |

**The precondition is that the split exists first.** Both cards must consume WP-0.4's split
**verbatim** — EVA-04 forbids re-splitting, and two concurrently running arms that each re-split would
produce numbers that cannot be compared and would not be detectable after the fact.

## 7.3 What must NOT be parallelised

| Not parallel | Why |
|---|---|
| WP-0.5 with WP-0.6 | The baseline defines the comparison and the variance spread; arms run against it |
| WP-0.8 with anything | The ruling needs **both** accuracy and runtime — EVA-14 requires all five dimensions |
| Any S1 work with S0 | **EVA-01.** No production code before the report is owner-accepted |
| WP-2.1 … WP-2.7 across agents | **CNF-09.** Splitting the swap from the recalibration across agents invites precisely the uncalibrated intermediate that is forbidden |
| WP-4.3 with WP-4.0 | The cap must have a value **before** packaging is implemented (AC-20) |

## 7.4 Suggested sub-agent task cards

**Do not dispatch from this session — this is a planning-only session.** Cards are provided for the
execution session. All inherit §6's common preamble.

---

**Card S0-A — pilot harness, split, and baseline arm** *(WP-0.1 … WP-0.5)*
- **Mission:** stand up the pilot venue, commit the DAT-05 fixture set, build the split once, and
  produce the baseline arm's per-class metrics and confidence relationship.
- **Venue:** `D:\Code\C#\SmartStudyPlanner`, branch off `dev`. Work confined to `tools/ml-pilot/`,
  `datasheets/`, `.gitignore`, `.github/workflows/ci.yml`, `docs/reports/`.
- **Scope:** WP-0.1 … WP-0.5. **No file under `SmartStudyPlanner/` is created or modified.**
- **Skills:** `superpowers:test-driven-development`, `superpowers:verification-before-completion`.
- **Tools:** GitNexus MCP first; RTK prefix on all shell commands.
- **Stop when:** the split is committed with assert-verified counts (698 / 205), the fixture set covers
  all six DAT-05 categories, and baseline per-class P/R plus the confidence relationship (with bin
  populations and measured run-to-run variance) are committed. **Emit no headline accuracy figure.**

---

**Card S0-B — encoder arms, accuracy** *(WP-0.6)* — **may run concurrently with S0-C**
- **Mission:** run **Arms A and B only** through S0-A's harness for outputs 1 and 2.
- **Venue:** as above. Scope: `tools/ml-pilot/accuracy/` and `tools/ml-pilot/results/` only.
- **Scope guard:** consume S0-A's split **verbatim — no re-splitting**. **Do not run Arm C** — it is
  unlocked only by an explicit owner decision after A and B report (EVA-06). **Do not restore the
  withdrawn benchmark justification** (EVA-07).
- **Skills / Tools:** as the common preamble.
- **Stop when:** both arms are reported on identical metrics, bins, and variance protocol. **If A and B
  are indistinguishable, say so and stop — do not force a winner** (EVA-15).

---

**Card S0-C — .NET runtime characterisation** *(WP-0.7)* — **may run concurrently with S0-B**
- **Mission:** outputs **3, 4, 5, 6, 8** — cold-start load, per-inference latency, peak RSS, verified
  tokenization route, packaged size — through `InferenceSession` + the real tokenizer + a real
  `SdcaMaximumEntropy` head.
- **Venue:** as above. Scope: `tools/ml-pilot/dotnet/` only; **not added to the solution**.
- **Hardware:** the **PRF-01 reference class**, CPU execution provider. **Name the actual machine.**
  A developer-machine-only number is **not an acceptable output** (PRF-03, EVA-10). **If no such
  machine is available, stop and escalate — do not substitute the dev machine.**
- **Skills / Tools:** as the common preamble; `superpowers:systematic-debugging` on tokenizer failures.
- **Scope guard:** verify tokenization **by loading the real vocabulary and diffing against the
  reference tokenizer** on the DAT-05 fixture set — not by reading documentation (TOK-04). **Report,
  do not act on**, any implied `Microsoft.ML` bump (TOK-07 → CP2). Assert **no** memory ceiling
  (PRF-08).
- **Stop when:** every surviving arm has a verified tokenization route and load/latency/RSS/size
  figures from the named reference machine, with the pre-registered protocol recorded.

---

**Card S1 — encoder seam** *(WP-1.0 … WP-1.6)* — **NOT dispatchable until CP1 passes**
- **Mission:** contract, null provider, read-only locator, ONNX provider with the S0-selected
  tokenization route, cross-cutting guards. **Zero behaviour change.**
- **Venue:** as above, own branch off `dev`.
- **Scope:** WP-1.0 … WP-1.6 file maps only. **Do not touch `TextClassifierModelManager`** — that is
  S2's.
- **Skills:** `superpowers:test-driven-development`, `superpowers:verification-before-completion`.
- **Tools:** `gitnexus_impact` before every symbol edit; `gitnexus_detect_changes` before every commit.
- **Stop when:** AC-04, AC-05, AC-06, AC-13, AC-16, AC-17, AC-18, AC-21, AC-26 hold; the registered
  provider is still the **null** one; suite at the measured baseline.

---

**Card S2+S3 — featurizer swap and recalibration** *(WP-2.1 … WP-2.7)* — **one card, deliberately**
- **Mission:** swap the featurizer and re-derive the gate **together**, on one branch, in one PR.
- **Venue:** as above, single branch off `dev`.
- **Scope:** WP-2.1 … WP-2.7 file maps.
- **Why one card:** splitting the swap from the recalibration across agents is the most direct route to
  the uncalibrated production state CNF-09 forbids.
- **Skills / Tools:** as the common preamble; `gitnexus_impact` is mandatory on
  `TextClassifierModelManager.Predict`, `TrainAndSaveAsync`, `IntentClassifierAdapter.Classify`,
  `DefaultMlConfidencePolicy.Decide`, `ParsingOrchestrator.Parse` — **warn the owner on HIGH/CRITICAL**.
- **Stop when:** every WP-2.x exit criterion is met **including the mutation test proving the gate can
  go red**, the wire-up flip is the final commit, and the release is documented as a **behaviour
  change**.

---

# 8. Verification strategy

## 8.1 Pre-edit checklist (required by `docs/plans/README.md`)

`CLAUDE.md` makes `gitnexus_impact` **mandatory before editing any symbol**. Run upstream impact and
**report the blast radius before** the corresponding WP; **warn the owner on HIGH or CRITICAL**.

| Symbol | WP | Expected risk | Note |
|---|---|---|---|
| *(no code symbols)* | 0.1 – 0.9 | **NONE** | `detect_changes` will **not** return empty — this repo's graph indexes markdown headings as `Section:` symbols, so docs-only changes fire them. **The gate is zero *code* symbols and zero affected processes**, not an empty result |
| `ServiceLocator` ML registrations | 1.2, 1.3, 1.4, 2.4, 2.7, 4.1 | **MEDIUM** | Composition root |
| `TextClassifierModelManager.Predict` | 2.1 | **MEDIUM** | Single consumer via `TextClassifierService` |
| `TextClassifierModelManager.TrainAndSaveAsync` | 2.1, 2.2 | **MEDIUM** | Lifecycle; atomic swap must survive |
| `DefaultMlConfidencePolicy.Decide` | 2.4 | **HIGH** | **Shared with M8-B WeightOptimizer tiers.** The plan's answer is to **not edit it** — see WP-2.4 |
| `IntentClassifierAdapter.Classify` | 2.5 | **HIGH** | User-visible routing + the `QuickInputHint` confidence string |
| `IIntentClassifier.Classify` | 2.5 | **HIGH** | Core contract; one implementation, one call site, plus test doubles |
| `ParsingOrchestrator.Parse` | 2.5 | **HIGH** | Smart Add entry point |

**Never rename with find-and-replace** — use `gitnexus_rename`, which understands the call graph.

## 8.2 Acceptance gates (required by `docs/plans/README.md`)

Applied to **every WP from S1 onward**:

1. `rtk dotnet build` — clean.
2. `rtk dotnet test` — **measure the baseline count on the branch at slice start; require no
   regression.** Do not assume 470 / 391 / 337.
3. `gitnexus_detect_changes()` before every commit — affected symbols must match the WP's file map.
4. **Zero-model-file fault-tolerance check** — delete `%AppData%\SmartStudyPlanner\models\*` **and the
   bundled encoder asset**, launch, confirm Dashboard + Analytics + Smart Add function on Tier 0.
   **Re-run at S2+S3 (WP-2.7) and at S4 (WP-4.4)** — not once.
5. **Latency budget** — Smart Add submit-to-populate under **500 ms** on the PRF-01 class, CPU
   provider, model loaded, over the PRF-05 boundary, using the protocol recorded in the S0 report.
6. **Tag slow ML tests** `[Trait("Category", "ML")]` per existing convention, so
   `--filter "Category!=ML"` stays fast.
7. **CI green + PR.** `dev` and `main` are PR-only since 2026-08-09.

## 8.3 Verification by kind

| Kind | Where it applies | What it must show |
|---|---|---|
| **Unit** | WP-1.2, 1.3, 1.4, 2.1, 2.4, 2.5, 4.1, 4.2 | Each contract behaves as specified in isolation, including every null/failure return |
| **Integration** | WP-1.6, 2.2, 2.7 | Parse path end-to-end; retrain lifecycle end-to-end |
| **Characterization** | WP-1.5 (AC-05), WP-1.6 (AC-13) | Existing behaviour is **unchanged** — expectations captured on the **pre-slice** commit |
| **Architecture / static** | WP-1.5 (AC-04, AC-26), WP-0.1 + CI (AC-21) | No network-capable type reachable from the ML layer; no encoder weight update; no binary in git |
| **Mutation** | WP-2.6 (AC-09), WP-2.4 (AC-11) | A deliberately miscalibrated gate goes **red**; a defeated independent signal goes **red** |
| **Runtime measurement** | WP-0.7 (AC-23, AC-24), WP-2.7 (AC-23 re-check) | Latency and peak memory on the **named** reference machine, with the recorded protocol |
| **Manual QA** | WP-2.7 (AC-03), WP-4.4 (AC-03, AC-19, AC-22) | Tier 0 fully functional with assets deleted; tiers exercised; offline install yields Tier 1 |

## 8.4 The verification rule that governs all of the above

**A passing check is not evidence until it has been shown able to fail.** Every guard this plan
introduces carries an explicit red-demonstration step, and the demonstration is recorded in the WP's
PR description:

| Guard | Its red-demonstration |
|---|---|
| AC-21 binary guard | Commit a dummy `.onnx`; CI must fail |
| AC-04 offline architecture test | Add an `HttpClient` field to a scratch ML type; test must fail |
| AC-06 tokenizer correctness | Perturb one expected token; test must fail |
| AC-18 no-side-effect locator | Add a `CreateDirectory` call; test must fail |
| R-7 single session | Construct twice; test must fail |
| AC-11 tier boundaries | Change `ReviewThreshold` to `0.55`; the **literal-pinned** test must fail while the symbol-referencing tests stay green |
| AC-09 confidence gate | Inject a miscalibrated threshold, and separately a defeated independent signal; both must fail |
| AC-19 Tier 2 parity | Inject a divergent second provider; parity must reject |
| WP-4.4 manual QA | Break one surface deliberately; the check must report FAIL |

---

# 9. Acceptance criteria mapping

**Every MUST-level acceptance criterion in spec §12 maps to at least one work package.** Criteria the
specification requires **more than once** are listed with each occurrence — a single-row mapping would
silently drop the second one.

| AC | Verifies | Work package(s) | Occurrences |
|---|---|---|---|
| **AC-01** | EVA-01/02/03/08/10/11/12, PRF-03/06, DAT-01 | WP-0.8, **WP-0.9** | Report + owner acceptance |
| **AC-02** | EVA-04/05/06/09, PRF-01/02 | WP-0.4, WP-0.5, WP-0.6, WP-0.7, WP-0.8 | |
| **AC-03** | BEH-10, CNF-08, AST-09, FLB-01/02/03 | **WP-2.7**, **WP-4.4** | **Twice — S2 and S4** |
| **AC-04** | AST-01, AST-03, ARC-06 | WP-1.5, re-run WP-4.3 | Test authored S1; re-run S4 |
| **AC-05** | BEH-02 | **WP-1.5**, **WP-2.7** | **Twice** — parse path (S1), encoder inference (S2) |
| **AC-06** | TOK-01/02/03, DAT-05 | WP-0.2 (fixtures), WP-1.4 | |
| **AC-07** | TOK-04, TOK-05 | WP-0.7, WP-0.8 | |
| **AC-08** | TOK-06, TOK-07 | WP-0.7 (finding), **WP-1.1** | Owner checkpoint CP2 |
| **AC-09** | CNF-01/02/03, FLB-03 | WP-2.5, **WP-2.6** | |
| **AC-10** | CNF-04 | WP-2.5 | |
| **AC-11** | CNF-05 | **WP-2.4** | Literal-pinned, not symbol-referencing |
| **AC-12** | CNF-06, CNF-07, REL-03 | WP-2.7 | |
| **AC-13** | REL-01, BEH-07, BEH-13, DAT-05 | WP-1.6 | |
| **AC-14** | CNF-09, REL-02 | WP-2.7 + branch/release-history review | Verified by history, not a test |
| **AC-15** | BEH-13 | WP-2.2 | |
| **AC-16** | BEH-11, BEH-12, FLB-01, FLB-02 | WP-1.4, WP-1.5 | |
| **AC-17** | BEH-05, BEH-06, ARC-03, ARC-04, DAT-05 | **WP-1.4**, **WP-2.1** | **Twice** — reproducibility (S1), unchanged-vector decision (S2) |
| **AC-18** | AST-06 | WP-1.3 | |
| **AC-19** | AST-07/08/09, ARC-09 | **WP-4.1** (automated), **WP-4.2**, **WP-4.4** (manual) | **Automated + manual QA** |
| **AC-20** | AST-04 | **WP-0.7** (output 8), **WP-4.0** (cap set), **WP-4.3** (breach check) | **Three points** |
| **AC-21** | AST-05 | **WP-0.1**, re-verified WP-1.6, WP-2.7, WP-4.3 | Cross-cutting CI guard |
| **AC-22** | AST-02, AST-03 | WP-4.4 | Manual QA at S4 |
| **AC-23** | PRF-04, PRF-05, PRF-06 | **WP-0.7**, **WP-2.7** | **Twice — measured at S0, re-checked at S2+S3** |
| **AC-24** | PRF-07, PRF-08 | WP-0.7, WP-0.8 | |
| **AC-25** | BEH-01, BEH-03, BEH-08, BEH-09, ARC-05 | WP-1.6, WP-2.1, WP-2.5 | |
| **AC-26** | ARC-01, ARC-02 | WP-1.5 | Static check + code review |
| **AC-27** | ARC-07, ARC-08, REL-04 | WP-2.7, §15 | Owner record + asset inventory |
| **AC-28** | FLB-01, FLB-03, TOK-06 | WP-1.4, WP-4.1, WP-4.2, **WP-4.4** | **One test per §10 row** |
| **AC-29** | DAT-01/02/04, EVA-07, EVA-11 | **WP-0.8** (report), **WP-2.7** (changelog) | **Twice — report and release notes** |
| **AC-30** | BEH-04, DAT-05 | WP-0.2, WP-0.6, WP-1.4 | |
| **AC-31** | EVA-13/14/15/16 | WP-0.8 | |
| **AC-32** | DOC-03, DOC-04 | WP-2.7 | Separate documentation commit |

**Coverage audit.** All 32 ACs are mapped. `DAT-03` is SHOULD-level, describes a workstream outside
this initiative's delivery, and carries no AC by design — see §15.

---

# 10. Owner checkpoints

Three blocking checkpoints. **None is buried inside an implementation task** — each is its own numbered
work package with its own exit criteria.

| # | WP | Decision | Blocking? | Input required |
|---|---|---|---|---|
| **CP1** | **WP-0.9** | **Accept or reject the S0 report.** If a winner: confirm the encoder (OP-9) and note the route (OP-8). If a tie: choose Arm C (OP-11) / data expansion / defer / stop. If the kill criterion fired: confirm the stop | **YES** — EVA-01 blocks all production code until it passes | The S0 report (WP-0.8) |
| **CP2** | **WP-1.1** | **Accept or reject a shared-ML-package version change** implied by the tokenization route. Applies only when one is implied | **YES, when it applies** — must precede the dependency commit (AC-08) | Blast-radius report (WP-1.1) |
| **CP3** | **WP-4.0** | **OP-1** size cap value · **OP-6** delivery mechanism · **OP-4** peak-memory ceiling · **OP-7** how the app reaches its users | **YES** — AC-20 blocks S4 packaging until the cap has a value | S0 output 8 + output 5; the option set in WP-4.0 |

**Additional owner approvals — outside this plan's scope** (§15): S5 requires **two** separate gates
(capability approval **and** a `DifficultyLabelLogs` count against the deferred proposal's trigger);
S6 requires capability approval **and** its own commissioned plan.

**Escalation obligation.** `CLAUDE.md` requires warning the owner before proceeding on any **HIGH or
CRITICAL** impact-analysis result. §8.1 marks five such symbols; WP-2.4 and WP-2.5 will both hit them.

---

# 11. Risks and mitigations

Risks R-1 … R-16 are carried from the approved proposal. R-17 … R-19 are **engineering consequences
surfaced by this planning pass** — they add no scope; each is a consequence of an already-ratified
requirement.

| # | Risk | Mitigation | Owning WP |
|---|---|---|---|
| R-1 | Encoder shows no gain over n-grams | S0 is a gate with a stated kill criterion; cost is one harness | WP-0.8 |
| R-2 | S0 result over-read from 3 of 5 classes | Per-class reporting mandated; caveat in the report's own text, and in the release notes | WP-0.8, WP-2.7 |
| R-3 | Threshold shift silently changes user-visible routing | S3 ships with S2; mutation test required; single-PR mechanism | WP-2.5, WP-2.6, WP-2.7 |
| R-4 | Shared `IMlConfidencePolicy` retunes M8-B by accident | **`DefaultMlConfidencePolicy` is never edited**; a separate parser policy is added; boundaries pinned to literals | WP-2.4 |
| R-5 | Bundled package exceeds the cap | **STOP-7** — stop and reopen the owner decision. No side-loading, no cap raise, no model substitution | WP-4.3 |
| R-6 | DirectML accuracy defect on Intel iGPU | Tier 2 opt-in + CPU-parity check, proven able to reject | WP-4.2 |
| R-7 | Session-lifetime mistake replicates the per-call `CreatePredictionEngine` pattern | S1 asserts **exactly one** session construction, proven red-able | WP-1.4 |
| R-8 | Zero-model-file contract breaks | Re-verified at **S2 and S4**, deliberately deleting a bundled asset | WP-2.7, WP-4.4 |
| R-9 | Model binary lands in git because policy says "bundled" | `.gitignore` + a CI guard **proven able to fail**, from the first S0 task onward | WP-0.1, WP-4.3 |
| R-10 | No workable .NET tokenizer for the winning encoder | S0 output 6 verifies the route **per arm before S1**; an arm without one is disqualified regardless of accuracy | WP-0.7 |
| R-11 | Tokenizer route forces a `Microsoft.ML` 3.0.1 → 4.x bump touching M7 + M8-A | **Reported at CP2 before the dependency commit** (AC-08); the other verified route avoids it | WP-0.7, WP-1.1 |
| R-12 | S0 latency measured off the .NET path clears a gate it never tested | Harness split mandated; outputs 3–6 come from the .NET console harness | WP-0.7 |
| R-13 | Bundling names an installer the repo does not have | Surfaced, not absorbed. Confined to S4 behind CP3; S0–S2+S3 unaffected | WP-4.0 |
| R-14 | Tier 0 rots because bundling makes it look unreachable | Tier 0 is a **fault-tolerance state**; its gate deliberately deletes a bundled asset, twice | WP-2.7, WP-4.4 |
| R-15 | S0 measured only on the developer's machine, quietly becoming the product floor | PRF-01 class fixed; the report **names the machine**; a dev-machine-only number is not acceptable. **See UQ-1** | WP-0.7 |
| R-16 | A head is added later on the strength of the shared encoder alone | Artifact count ≠ capability count; every head needs its own owner approval | §15, WP-2.7 (AC-27) |
| **R-17** | **Seed-embedding cost at retrain.** Head retraining now embeds **698 training rows**; at measured per-inference latency this can turn a seconds-long retrain into a minutes-long one | Not a startup blocker — initialisation is already fire-and-forget on a background task **[verified]**. **Batch the embedding calls**; measure actual retrain wall time; record it in the release notes as a first-run characteristic | WP-2.2 |
| **R-18** | **The independent signal is decorative.** A dual-signal gate that never changes an outcome satisfies CNF-02 in letter only | The mutation test includes a **defeated-signal** case that must go red — proving the signal is load-bearing | WP-2.6 |
| **R-19** | **The rank constant drifts from the shipped asset.** `VectorType` needs a compile-time constant; a later asset swap with a different rank would mismatch silently | The constant's XML doc names the source report; WP-1.4 asserts the returned vector length equals the documented `Rank` for every fixture | WP-2.1, WP-1.4 |

---

# 12. Rollback boundaries

| Boundary | Scope of a revert | Resulting state | Safe? |
|---|---|---|---|
| **Any S0 WP** | One or more commits under `tools/`, `datasheets/`, `.gitignore`, `.github/`, `docs/reports/` | Unchanged product. **No production symbol was ever touched** | ✅ Trivially |
| **CP1 rejection** | Nothing to revert | Initiative stops having cost one harness and one report | ✅ **This is a designed outcome, not a failure** |
| **S1 (one PR)** | Revert the merge | Product returns to pre-S1. Production still resolved the **null** provider throughout S1, so no user-visible state ever depended on the encoder | ✅ |
| **S2+S3 (one PR)** | Revert the merge | Product returns to S1's state: legacy featurizer, original gate, no user-visible change. **There is no partial revert leaving a swapped featurizer with an underived gate**, because no such state ever reached production | ✅ **This is the phase's rollback boundary** |
| **S4 packaging (WP-4.3)** | Revert the packaging change | The asset is absent → **Tier 0** → fully functional heuristic application. AST-09 guarantees this and WP-4.4 tests it | ✅ |
| **S4 tiering (WP-4.1/4.2)** | Revert | Tier 1 (CPU) remains the default, unchanged from S2+S3. Nothing user-visible depended on Tier 2 | ✅ |

**The boundary that must not be crossed.** No revert may produce a production state in which the
featurizer is swapped and the gate is not re-derived (CNF-09, REL-02). The single-PR mechanism for
S2+S3 makes that state unreachable by construction, not by discipline — which is why AC-14 is
verifiable by reading branch history.

---

# 13. Documentation artifacts

Produced **during execution**. Each has one owner and one purpose. **Do not duplicate; do not turn
execution reports into knowledge articles** — durable lessons are distilled later, during project
consolidation.

| Artifact | Path | Produced by | Purpose |
|---|---|---|---|
| **S0 pilot report** | `docs/reports/2026-XX-XX-encoder-pilot.md` | WP-0.1 (skeleton), WP-0.8 (filled) | **The CP1 decision artifact.** Eight outputs per arm, named machine, protocol, limitations, winner/kill/tie ruling. **EVA-12: it goes here, never into the plan or the spec** |
| **Split record** | `tools/ml-pilot/split/SPLIT.md` | WP-0.4 | Counts, class distribution, source hash — makes EVA-04 auditable |
| **Artifact record** | `tools/ml-pilot/ARTIFACTS.md` | WP-0.3 | Per arm: source, pinned revision, SHA-256, sizes, quantization, licence status |
| **Fixture set + guide** | `datasheets/vn_input_fixtures.csv` / `.md` | WP-0.2 | The **single** DAT-05 set; the `.md` names the four ACs that read it |
| **S1 route ruling** | `docs/reports/2026-XX-XX-s1-route-ruling.md` | WP-1.0 | Adopted encoder, verified route(s), rank, quantization — traced to the accepted report |
| **CP2 blast-radius record** | Owner-checkpoint record / PR description | WP-1.1 | AC-08 evidence: reported **before** the dependency commit |
| **Recalibration report** | `docs/reports/2026-XX-XX-confidence-recalibration.md` | WP-2.3 | Post-swap confidence distribution with bin populations; the threshold derivation's source |
| **Threshold derivation** | XML doc comment on `ParserIntentConfidencePolicy` | WP-2.5 | **CNF-04: recorded where the value lives** — date, source report, reasoning |
| **Release notes / changelog** | Repo changelog + PR | WP-2.7 | **REL-03: described as a behaviour change**, carrying the coverage limitation (AC-29) |
| **Architecture updates** | `docs/architecture/` | WP-2.7 | **DOC-03: updated when S2+S3 ships, not before.** **DOC-04: a separate, owner-approved commit — never a side effect** |
| **CP3 decision record** | Owner-checkpoint record | WP-4.0 | Cap value, delivery mechanism, memory ceiling, OP-7 answer |
| **S4 QA runbook + report** | `docs/plans/…-encoder-s4-manual-qa-runbook.md`, `docs/reports/…-encoder-s4-qa.md` | WP-4.4 | Manual QA procedure and its results, with PASS/FAIL fixed in advance |

**Convention.** Every agent-written report under `docs/reports/` carries an ADR-style **"Decisions
made"** section — why / what-for / experience — per `docs/reports/README.md`. Owner-authored evidence
records are exempt.

---

# 14. Definition of Done

## 14.1 S0 is done when

- [ ] The report exists in `docs/reports/`, with **all eight outputs per arm**.
- [ ] Runtime outputs 3, 4, 5, 6 come from the **.NET stack** on the **named** PRF-01 reference machine.
- [ ] The **measurement protocol** (OP-3) is written into the report, and was fixed **before** any
      number was compared against the 500 ms ceiling.
- [ ] A **verified tokenization route** is stated per surviving arm; arms without one are recorded as
      **rejected**.
- [ ] Every arm's numbers derive from **one split constructed once**; **Arm C is absent**.
- [ ] The report states its **own limitations** — coverage, maturity, protocol — in its own text.
- [ ] **No single headline accuracy figure** appears anywhere.
- [ ] **No memory ceiling is asserted.**
- [ ] The **EVA-14 five dimensions** are each answered in writing per arm; the **tie branch** is
      honoured if it applies; the **kill criterion** is applied if it applies.
- [ ] **No file under `SmartStudyPlanner/` was created or modified.**
- [ ] **The owner has accepted or rejected the report in writing (CP1).**

## 14.2 S1 is done when

- [ ] `ITextEmbeddingProvider`, its ONNX implementation, its null implementation, and the read-only
      asset locator exist and are registered.
- [ ] The tokenization route matches the one S0 verified; **no route was substituted silently**.
- [ ] **Production still resolves the null provider.**
- [ ] AC-04, AC-05, AC-06, AC-13, AC-16, AC-17, AC-18, AC-21, AC-26 all hold.
- [ ] **Every new guard has been demonstrated red** and the demonstrations are recorded in the PR.
- [ ] **No parse output changes** for the DAT-05 fixture corpus — including `ParseSource`.
- [ ] Suite at the **measured** pre-slice baseline; CI green; merged via PR.
- [ ] CP2 was cleared **before** any dependency commit, or recorded as not applicable with a reason.

## 14.3 S2+S3 is done when

- [ ] The featurizer consumes the embedding; the decision layer is **still linear and authoritative**.
- [ ] The retrain lifecycle survives: seed training, seed-hash staleness gate, atomic swap, version
      increment (AC-15).
- [ ] The threshold is **derived from measured data**, and its **date, source report and reasoning are
      recorded where the value lives** (AC-10).
- [ ] **The 0.60 value was not carried over unexamined.**
- [ ] At least one signal **independent of the model's own score** contributes to routing, and a
      **defeated-signal mutation goes red** (AC-09).
- [ ] **Weight-optimizer tier behaviour is unchanged**, verified by a **literal-pinned** regression test
      (AC-11).
- [ ] The displayed confidence and the gated quantity are **the same value** (AC-12).
- [ ] Latency re-checked under **500 ms** on the reference class with the recorded protocol (AC-23).
- [ ] **AC-03 re-run**: every model file deleted, including the bundled asset → Smart Add, Dashboard,
      Analytics all function.
- [ ] Shipped as **one PR**, described as a **behaviour change**, with the coverage limitation carried
      into the release notes.
- [ ] `docs/architecture/` updated in a **separate, owner-approved commit** (AC-32).
- [ ] **No production state ever existed with a swapped featurizer and an underived gate** (AC-14).

## 14.4 S4 is done when

- [ ] **CP3 decided** the cap value, the delivery mechanism, and the memory ceiling **before** any
      packaging was written.
- [ ] Tiers 0, 1 and 2 are each exercised; **Tier 2 is opt-in and passes CPU parity** before serving
      any user-visible result.
- [ ] The asset is present after a **normal, offline install** with no user action, yielding **Tier 1,
      not Tier 0** (AC-22).
- [ ] **Packaged size measured against the cap**; a breach **stopped the slice and reopened the owner
      decision** rather than being absorbed (AC-20).
- [ ] **No encoder binary in git at any commit of this initiative** (AC-21).
- [ ] **Every row of §10's failure table has a test** showing the heuristic result is delivered and no
      exception escapes (AC-28).
- [ ] **Tier 0 fully functional with the asset deleted** (AC-03, second occurrence).
- [ ] Manual QA report filed with PASS/FAIL criteria fixed **before** execution, and the observation
      channel independently shown able to report FAIL.

## 14.5 The initiative is done when

S0 through S4 are each done **or** the initiative stopped cleanly at a stop condition (§3.1) with the
outcome recorded. **A null result at S0 with a written owner rejection is a complete and successful
conclusion of this plan.**

---

# 15. Deferred / future work

**Nothing in this section is authorised by this plan.** Each item names the gate that would make it
*eligible* — and eligibility is not approval.

| Item | Gate that must pass first | Approval still required after the gate? |
|---|---|---|
| **S5 — difficulty head** | **(1)** explicit owner approval of the capability (ARC-08 governance); **and (2)** a count of `DifficultyLabelLogs` measured against the trigger conditions in `Difficulty_ML_model_proposal.md`, **with an insufficient count recorded as a result and the slice stopped** | **Yes — both gates are separate** |
| **S6 — temporal span head** | Its own plan, commissioned separately | **Yes** |
| **Arm C** (`hiieu/halong_embedding`) | Arms A and B together failing to produce evidence strong enough for a trustworthy decision (the EVA-15 tie branch) | **Yes — owner decision after A and B report** |
| **Dataset maturity workstream** | None — it is **independent and ongoing** (DAT-03, SHOULD-level) | Runs in parallel; **never a reason to delay S0**; expanding it does **not** authorise re-running or reversing an S0 outcome (DAT-04) |

**Why S5/S6 cannot leak into current scope.** REL-04 forbids folding them into this initiative's
implementation contract, and forbids treating them as activated by the encoder's acceptance. **A shared
encoder does not activate any head.** AC-27 makes this checkable: exactly one deployed model artifact
is introduced, and no additional prediction head exists without a recorded owner approval. The
artifact count governs deployment surface; the **capability count governs product scope**, and they are
separate axes by ratified decision.

**Excluded regardless of outcome** (spec §13 — each requires a **new owner decision** to re-enter, not
a plan revision): generative SLM inference of any kind, on any tier, for any field · Windows AI APIs /
Phi Silica / Aion Instruct / Foundry Local · **fine-tuning the encoder — prohibited, not merely
declined**, including offline developer-side fine-tuning producing a new bundled artifact · cloud model
inference or storage · any model acquisition beyond bundling · **building the installer or release
pipeline itself** · uncontrolled model proliferation · the rule-based weight optimizer's ML replacement
· the study-time predictor retrain on focus telemetry · Epic 2 (LAN sync) and Epic 4 surfaces · any
Epic 3 decision · a second install variant or SKU.

---

# 16. Open implementation questions

## 16.1 Genuine blockers

Only one item blocks **execution**, and **nothing blocks planning**.

| # | Question | Blocks | Why it is a real blocker |
|---|---|---|---|
| **UQ-1** | **Is a physical PRF-01-class machine available for S0 runtime measurement — and which one?** (OP-5) | **WP-0.7 (Card S0-C)**, and therefore CP1 | PRF-03 forbids treating a developer-machine-only number as the product floor, and EVA-10 requires the report to **name the actual machine**. If no reference-class machine exists, **WP-0.7 cannot complete legitimately** and the honest response is to stop and escalate — not to substitute the dev machine and annotate it. **The PRF-01 class is fixed; only the specific machine is open**, and the practical availability question is what needs an answer before S0-C is dispatched |

## 16.2 Not blockers — resolved by sequencing, each owned by a named checkpoint

Listed to prevent them being escalated as blockers, and to prevent them being silently decided by an
implementation agent.

| # | Item | Owner | Resolved at | Status |
|---|---|---|---|---|
| OP-1 | Package/model size cap **value** | **Owner** | **CP3 (WP-4.0)** | Unset. The *"1–2 GB acceptable"* remark is an install-size preference, **not a cap** |
| OP-3 | Latency measurement **statistics** — warm/cold, percentile, sample count | S0 | WP-0.1 (pre-registered), recorded in the report | The **boundary** is fixed (PRF-05); the **ceiling** is ratified (PD-12); only statistics remain |
| OP-4 | Peak-memory **ceiling** | S0 → S4 | Measured WP-0.7, fixed at **CP3** | **Not asserted in advance**, by requirement |
| OP-6 | **Delivery mechanism** | **Owner** | **CP3 (WP-4.0)** | Policy settled (bundled); mechanism open; option set recorded in WP-4.0 |
| OP-7 | **How the application currently reaches its users** | **Owner** | **CP3** | **Unknown — no document in the repository records it.** The standing recommendation for OP-6 depends on this answer |
| OP-8 | **Tokenization route**, per candidate | S0 | WP-0.7 → ruled at WP-1.0 | `[gate]` — determined by measurement, never chosen in advance |
| OP-9 | **Which encoder is adopted** | S0 → owner | **CP1** | `[gate]` — **may be *none***. This plan names no winner |
| OP-10 | Post-swap **threshold value** | S3, from S0/WP-2.3 data | WP-2.5 | **Derived, not chosen.** The procedure is fixed here; the number is not |
| OP-11 | **Arm C activation** | **Owner** | **CP1**, tie branch only | `[gate]` |
| OP-12 | Installer packaging mechanics; DirectML capability-probe mechanism | S4 | WP-4.1, WP-4.3 | **Planning questions, not owner policy.** Must not be dressed up as owner decisions |

## 16.3 Implementation choices this plan fixes — and the ones it deliberately leaves open

The specification marks these `[choice]`, *"deliberately left open for the execution plan"*. Fixing
them is this document's job; **freezing what the spec left to measurement is not.**

**Fixed here** (naming them is what makes the plan executable):

| Choice | Fixed to | WP |
|---|---|---|
| Name and shape of the .NET abstraction | `ITextEmbeddingProvider` — `bool IsAvailable`, `int Rank`, `float[]? Embed(string)` | WP-1.2 |
| Its null implementation | `NullTextEmbeddingProvider` | WP-1.2 |
| Encoder location resolution | A **separate** `IEncoderAssetLocator` — **not** an extension of `IModelStorageProvider` (AST-06, §2.2 D-2) | WP-1.3 |
| Session lifetime | One lazily constructed, long-lived `InferenceSession`, off the startup path, asserted single (BEH-11, BEH-12) | WP-1.4 |
| Where the head's feature column is defined | `TextClassifierInput.Embedding`, `[VectorType]` with the rank from the S0 report | WP-2.1 |
| The independent routing signal (CNF-02) | Agreement with the heuristic task-type parser, passed through an extended `IIntentClassifier.Classify` | WP-2.5 |
| Policy separation shape (CNF-05) | Add `ParserIntentConfidencePolicy`; **leave `DefaultMlConfidencePolicy` untouched** | WP-2.4 |

**Left open on purpose** — these belong to measurement or to the owner, and this plan writes the
**procedure**, never the value: embedding dimensionality and representation truncation · quantization
of the encoder asset · the specific inference runtime version · threading, batching and warm-up
strategy beyond the single-session requirement · input-length bounding (`[choice]`, constrained only by
the requirement that truncation must not silently change a user-visible field without provenance
saying so).

---

## Lifecycle

**`closed` — 2026-08-25.** The plan ran to a stop condition it defined in advance and reached the
completion state §14.5 describes: *"the initiative stopped cleanly at a stop condition (§3.1) with the
outcome recorded."*

What actually happened, against the sequence this section originally laid out:

| # | Planned | Actual |
|---|---|---|
| 1 | Owner reviews this execution plan | Execution was directed from it in session; the `draft` status was **deliberately left unchanged** at the time rather than flipped on a verbal direction, and is closed here by the CP1 ruling instead |
| 2 | **S0 runs** (WP-0.1 … WP-0.8) | ✅ **Executed.** All eight EVA-08 outputs, three arms, two precisions. Cards S0-A/B/C were run in one session rather than dispatched to sub-agents — the parallel pair WP-0.6 ∥ WP-0.7 shared a single .NET harness, so splitting them would have duplicated it |
| 3 | **CP1** — report accepted or rejected | ✅ **ACCEPTED 2026-08-25**, and **the kill criterion fired**. Acceptance of the report *is* acceptance of the stop |
| 4 | On acceptance: S1, then S2+S3 | ❌ **Cancelled, not entered.** EVA-16 forecloses it |
| 5 | **CP3** decides S4's parameters | ❌ **Never reached.** OP-1, OP-4 and OP-6 remain **unset** |
| 6 | S5 and S6 remain unactivated | ✅ **Still unactivated**, unchanged. REL-04 is unaffected — a stopped encoder activates nothing |

**STOP-1 fired and was honoured.** The plan's own §1 says S0 exists so the initiative can die cheaply,
and it did: one throwaway harness, one report, **zero production symbols touched** (EVA-01).

**This plan is retained in `docs/plans/` rather than archived.** The archive is a gitignored local
folder, and the record of *why* the encoder work stopped — and of the guardrails it was built against
— is worth keeping in the tree next to the report that closed it.

**Reviving this work needs a new owner decision, not this plan.** DAT-04 is explicit that expanding
the dataset does not by itself authorise re-running or reversing an S0 outcome. If a later plan
supersedes this one, link the replacement here.
