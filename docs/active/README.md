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

**One effort is in progress: the edge-AI encoder adoption** — scope-frozen and activated
2026-08-24 for S-SPEC through S3, with S-SPEC already executed and **S0 the next action**. The other
two rows are queued/proposed, not being worked.

*Superseded 2026-08-19 (kept for history):* the previous banner said *"Epic 3 (SOE) is next"*, which
was true when written on 2026-08-02. The order it cited still holds — the
[master plan](../plans/2026-07-03-master-plan.md) sequences **E1 → E3 → E2 → E4**, and *"Epic 2 entry
criteria"* remains the stabilization plan's name for a set of gates, **not** an execution order.
With E1 and E3 both closed, the next epic in that sequence is the **LAN-sync epic (Epic 2)**, which
**has not been started**. Naming the order is not the same as choosing it: **G3-1** — wiring the
Epic 3 optimizer into production, still unscheduled — could reasonably come first. That call is the
owner's and has not been made.

## Current (2026-08-24)

| Work | Plan | State |
|---|---|---|
| **Edge AI — neural encoder for the Smart Parser (M8-A)** | **Execution:** [`../plans/2026-08-24-edge-ai-neural-encoder-execution-plan.md`](../plans/2026-08-24-edge-ai-neural-encoder-execution-plan.md) · **Contract:** [`../specs/2026-08-24-neural-encoder-smart-parser.md`](../specs/2026-08-24-neural-encoder-smart-parser.md) · **Why:** [`../plans/2026-08-24-edge-ai-encoder-adoption.md`](../plans/2026-08-24-edge-ai-encoder-adoption.md) | **S0 COMPLETE — BLOCKED at CP1, awaiting the owner's ruling (2026-08-25).** The pilot ran and **the EVA-16 kill criterion fired**: neither encoder arm improves macro-F1 over the current n-gram baseline — both score *below* it, across both precisions. **Under the specification the initiative stops at S0, and PD-3 makes that a complete, valid outcome.** Nothing proceeds until the owner accepts or rejects [the report](../reports/2026-08-25-encoder-pilot.md). S-SPEC–S3 scope-frozen and activated 2026-08-24. **S-SPEC executed** (`d141db1`): `ML_Heuristic_design.md` gains §9.1 (narrow frozen-encoder exception, eight guardrails) and §10 "Unit of the cap" (artifacts, not heads), under PD-1/PD-2. The specification was **ratified 2026-08-24** together with **PD-12** (the 500 ms latency ceiling). **S0 was the offline pilot** — a hard gate with a kill criterion stated in advance — and **that criterion fired**. Its report is [`../reports/2026-08-25-encoder-pilot.md`](../reports/2026-08-25-encoder-pilot.md), decided at **CP1**; **no production code exists** (EVA-01 held — zero files under `SmartStudyPlanner/` touched). Two findings there outlive the decision: the shipped M8-A gate sits at `≥0.60` while the baseline's own `[0.6,0.7)` band scored 0.000 on real held-out rows, and `tgk` appears in 28 of 205 real rows but 0 of 698 training rows. S4 left open by design (PD-11: delivery mechanism + size cap decided there, from S0's measurements); S5–S6 not activated, each needs its own owner approval. Owner decisions: [`../plans/2026-08-24-edge-ai-encoder-owner-decision-handoff.md`](../plans/2026-08-24-edge-ai-encoder-owner-decision-handoff.md) |
| **Analytics two-section redesign** | [`../plans/2026-07-20-analytics-two-section-redesign.md`](../plans/2026-07-20-analytics-two-section-redesign.md) | QUEUED — design brief, **plus a delivered implementation package** (2026-08-02) now under version control at [`../assets/analytics-ui-package/`](../assets/analytics-ui-package/). **Not integrated**; no code merged. Phase 3 unlocked, not started. *Known gap: the package README cites an interactive mockup `Analytics Redesign Proposal.dc.html` that is not in the repository.* |
| **UI fidelity + mobile-ready polish** | [`../plans/2026-07-05-ui-mobile-ready-polish.md`](../plans/2026-07-05-ui-mobile-ready-polish.md) | PROPOSED, on `dev` — `ui_rf` was adopted as the tested trunk and merged (PR #49, 2026-07-26), so the plan is no longer branch-scoped; it remains unimplemented |

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
