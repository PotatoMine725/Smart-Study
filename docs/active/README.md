# Active

> Pointers to work **currently in progress** — nothing else lives here. Completed trackers
> are archived to `legacy/Archived plans/` (local-only, gitignored; content stays recoverable
> in git history). Canonical status lives in
> [`../specs/system_roadmap.md`](../specs/system_roadmap.md) §A.3 and the
> [master plan](../plans/2026-07-03-master-plan.md) — this folder only answers
> *"what is being worked on right now, and where is its plan?"*.

**Epic 3 (Study Optimization Engine) closed 2026-08-19** — code complete 2026-08-07, manual QA gate
**CLOSED, PASS WITH FINDINGS** (every scenario passed; no scenario produced a defect). Suite 391 →
**487**. See `docs/CHANGELOG.md`, the [gate closure](../reports/2026-08-19-epic3-manual-gate-closure.md),
and the [closing note](../reports/2026-08-07-epic3-closing-note.md).

**The edge-AI encoder adoption is CLOSED (2026-08-25)** — S0 ran, the **EVA-16 kill criterion fired**,
and the owner accepted the stop. **No production code was written and none will be.** See the
[pilot report](../reports/2026-08-25-encoder-pilot.md) and the CP1 ruling at its end.
**Nothing is in progress right now** — the rows below are queued/proposed/raised, not being worked.

**The data-foundation decision phase closed 2026-08-26** (owner ruling: nine policies ratified; the
project holds **zero verified real user rows**). It produced two items that need an owner call and one
completed correction pass — see the table below and
[`../plans/2026-08-26-data-foundation-owner-decision-handoff.md`](../plans/2026-08-26-data-foundation-owner-decision-handoff.md).

**The proposal's own review closed 2026-08-27** — the owner ruled on **Q-1 … Q-5**, the five questions
the proposal had declined to answer with invented figures
([`../plans/2026-08-27-data-maturation-owner-decision-outcomes.md`](../plans/2026-08-27-data-maturation-owner-decision-outcomes.md)).
**Four of the five rulings are instructions not to invent the number yet.** The proposal is now at
**revision 2** and waits on *authorization*, not decisions. Its immediate next step, **S-1**, needs no
tooling and no data — only four owner rulings.

*Superseded 2026-08-19 (kept for history):* the previous banner said *"Epic 3 (SOE) is next"*, which
was true when written on 2026-08-02. The order it cited still holds — the
[master plan](../plans/2026-07-03-master-plan.md) sequences **E1 → E3 → E2 → E4**, and *"Epic 2 entry
criteria"* remains the stabilization plan's name for a set of gates, **not** an execution order.
With E1 and E3 both closed, the next epic in that sequence is the **LAN-sync epic (Epic 2)**, which
**has not been started**. Naming the order is not the same as choosing it: **G3-1** — wiring the
Epic 3 optimizer into production, still unscheduled — could reasonably come first. That call is the
owner's and has not been made.

## Current (2026-08-26)

| Work | Plan | State |
|---|---|---|
| **Prediction instrumentation defect (DFD-9a)** | [`../plans/2026-08-26-prediction-instrumentation-defect.md`](../plans/2026-08-26-prediction-instrumentation-defect.md) | **FIXED 2026-08-26**, suite 487 → 492. Seam returns the prediction record, `TaskDashboardItem` carries it, the write site logs both columns on both branches. **One gate still open:** the end-to-end check needs the owner at a keyboard — automated tests cover the three hops but not the production DI wiring. Runbook ready (~10 min): [`../plans/2026-08-26-dfd9a-instrumentation-runbook.md`](../plans/2026-08-26-dfd9a-instrumentation-runbook.md) |
| **Data Maturation & Coverage Expansion** | [`../plans/2026-08-26-data-maturation-coverage-expansion.md`](../plans/2026-08-26-data-maturation-coverage-expansion.md) | **Rev 2 (2026-08-27) — DRAFT, awaiting *authorization*.** Reviewed; Q-1…Q-5 ruled. Staged S-1…S-8 plus the **S-T** telemetry strand; maturity is now a `T-0…T-3` tier ladder with three binary invariants, **all three false today**. Not authorized, not scheduled. **Next: S-1**, the limited taxonomy review — four owner rulings, no tooling. Then S-2, then the Q-1 measurement |
| **Owner decision outcomes (Q-1…Q-5)** | [`../plans/2026-08-27-data-maturation-owner-decision-outcomes.md`](../plans/2026-08-27-data-maturation-owner-decision-outcomes.md) | **RATIFIED 2026-08-27**, implementation still not authorized. Owner has a bounded participant network (Q-2); collection runs outside the app (Q-3); hybrid sampling, no forced quotas (Q-4); tiered maturity (Q-5); adjudication effort is measured, not estimated (Q-1). **Where its wording differs from the 2026-08-26 handoff, this one governs** — one such difference is material, see its §A.2 |
| **Analytics two-section redesign** | [`../plans/2026-07-20-analytics-two-section-redesign.md`](../plans/2026-07-20-analytics-two-section-redesign.md) | QUEUED — design brief, **plus a delivered implementation package** (2026-08-02) now under version control at [`../assets/analytics-ui-package/`](../assets/analytics-ui-package/). **Not integrated**; no code merged. Phase 3 unlocked, not started. *Known gap: the package README cites an interactive mockup `Analytics Redesign Proposal.dc.html` that is not in the repository.* |
| **UI fidelity + mobile-ready polish** | [`../plans/2026-07-05-ui-mobile-ready-polish.md`](../plans/2026-07-05-ui-mobile-ready-polish.md) | PROPOSED, on `dev` — `ui_rf` was adopted as the tested trunk and merged (PR #49, 2026-07-26), so the plan is no longer branch-scoped; it remains unimplemented |

## Closed from here (2026-08-25)

**Edge AI — neural encoder for the Smart Parser (M8-A)** — **STOPPED at S0** on the EVA-16 kill
criterion, owner-accepted 2026-08-25. Neither candidate encoder improved macro-F1 over the shipped
n-gram baseline; both scored **below** it, at both precisions. A null result was a designed, valid
outcome of the S0 gate (PD-3), and it cost one throwaway harness and one report — **zero production
symbols touched**.

- **Outcome + CP1 ruling:** [`../reports/2026-08-25-encoder-pilot.md`](../reports/2026-08-25-encoder-pilot.md)
- **Plan** (`closed`; only Phase S0 was ever executed): [`../plans/2026-08-24-edge-ai-neural-encoder-execution-plan.md`](../plans/2026-08-24-edge-ai-neural-encoder-execution-plan.md)
- **Proposal** (`stopped_at_s0`): [`../plans/2026-08-24-edge-ai-encoder-adoption.md`](../plans/2026-08-24-edge-ai-encoder-adoption.md)
- **Contract** (stays as the ratified record, `stopped_at_s0`): [`../specs/2026-08-24-neural-encoder-smart-parser.md`](../specs/2026-08-24-neural-encoder-smart-parser.md)
- **Durable lessons:** [`../knowledge/ml-experimentation.md`](../knowledge/ml-experimentation.md)

**Disposition of everything the pilot produced** — so a later reader can tell a *commitment* from a
*candidate* from a *fact*, without rereading the investigation:

| Item | Evidence status | Disposition |
|---|---|---|
| Neither encoder beat the n-gram baseline | **Confirmed** — measured, both arms, both precisions, one shared split | **The S0 conclusion.** Closed. Revival needs a new owner decision + its own plan |
| Dataset-distribution limitation (94.6 % of the held-out `collected_v4` rows — **authored, not real**, DFD-1 — carry an unseen token; `tgk` 28/205 test vs 0/698 train; 3-of-5 class coverage) | **Confirmed observation** | **Knowledge**, plus a *candidate* for a future dataset proposal. **Not scheduled.** DAT-04: dataset growth alone does not authorise re-running the encoder experiment |
| **F-1** — M8-A merge gate at `≥0.60` vs a 0.000-accuracy `[0.6,0.7)` band | **Indication**, not a proven defect — n=11 at seed 42, and **authored** input against a model trained on other authored rows (DFD-1, 2026-08-26) | **Separate investigation candidate.** Deferred by owner ruling to [`../specs/system_roadmap.md`](../specs/system_roadmap.md) §A.4. **Not fixed, not scheduled**; a fix must *separate* the shared `DefaultMlConfidencePolicy`, not retune both consumers |
| EmbeddingGemma int8 export ~6× slower than its fp32 export, at ~2× peak memory | **Measured observation**, on non-reference hardware — bounded to that export / runtime / CPU | **Knowledge** ([`../knowledge/ml-experimentation.md`](../knowledge/ml-experimentation.md)). Not a general claim that int8 is slower; **not optimised** — the initiative stopped |
| Tokenization / runtime facts (no in-graph tokenization; fairseq `+1` offset; whitespace-axis divergence; no shared-package version bump needed) | **Confirmed**, verified against real vocabularies with the checks proven red first | **Knowledge.** Accepted by the owner at CP1 |
| **Arm C** (`hiieu/halong_embedding`) | **Not tested** — never acquired | **Remains unactivated.** The tie branch did not fire; a third encoder on the same 698 synthetic rows would test the hypothesis that just failed |
| **S5 / S6** (difficulty head, temporal-span head) | **Not entered** | **Unchanged — still not activated.** Each needs its own approval; a stopped encoder cannot activate a head |
| `tools/ml-pilot/` harness | — | **Retained.** Outside `SmartStudyPlanner.slnx`, so it costs nothing in build or CI, and it is the only way to re-derive the numbers. Model binaries stay **untracked**; the AC-21 CI guard enforces that on every commit |

**`ML_Heuristic_design.md` §9.1 remains in force.** The ratified policy exception permitting frozen
pretrained encoders as feature extractors was **not withdrawn** — only never exercised. A future
proposal re-enters through that gate, and **DAT-04** means dataset growth alone does not authorise a
re-run.

**One finding outlived the initiative**, now tracked in `specs/system_roadmap.md` §A.4: the shipped
M8-A merge gate sits at `≥0.60` while the **baseline** classifier's own `[0.6,0.7)` band scored 0.000
on the 205 held-out `collected_v4` rows — which are **not real data** (AI-generated, AI-labelled;
DFD-1, 2026-08-26). Produced by the baseline arm — no encoder involved.

Deferred items tracked in the roadmap (§A.4), not here — listed so they are not mistaken for active
work: **G3-1** (wire `IScheduleOptimizer.Optimize` into production — the engine has no production
call site); the **E6 surviving mutant** (`DetectChanges()` ordering — pin it or prove it redundant,
do **not** delete the call); M8-B ML training (waits for matured `WeightChangeLog` rows with class
balance); M8-A `TextClassifierModelManager.RetrainAsync` consumer wiring. The last two exit via
*data*, not code.

## Rules

- One tracker file **or** one row in the table above per in-progress effort; the detailed plan
  lives in `plans/` (naming `YYYY-MM-DD-<kebab>.md`).
- When an effort ships: append to `CHANGELOG.md`, reflect the end state in `architecture/`,
  then move its tracker/plan to `legacy/Archived plans/`.
- Keep this folder near-empty on purpose — if something has been "active" for weeks with no
  commits, it is not active; archive it or re-plan it.

## Archived from here (2026-07-07 sweep)

`refactor-god-object.md` (Slices 1–8 shipped), `m8-text-classifier.md` (M8-A shipped) →
`legacy/Archived plans/`. `m8-weight-optimizer.md` was copied there too but **stays tracked
here as well** — it is still the live tracker for the one M8-B item that hasn't shipped (ML
training, gated on `WeightChangeLog` data volume; see Deferred items above).

## Archived from here (2026-07-26 sweep)

Epic 1 shipped and Released (2026-07-20), so its execution/QA plans moved to
`legacy/Archived plans/` (local-only, gitignored; content stays in git history):
`2026-07-02-next-session-agenda.md`, `2026-07-03-epic-1-execution-plan.md`,
`2026-07-03-g1-soft-delete-cascade.md`, `2026-07-10-epic1-m1.3-monhoc-identity-brief.md`,
`2026-07-12-epic1-closure-phase1-execution.md`, `2026-07-15-epic1-phase2-owner-runbook.md`,
`2026-07-19-epic1-reopen-fix-plan.md`, `2026-07-20-epic1-reopen-owner-reclosure-runbook.md`,
`2026-07-20-analytics-stale-render-fix.md`. Kept in `plans/`: the closure-gate record
(`2026-07-11-epic-1-closure-gate.md`, holds the B4=Released decision) and the decision records.
