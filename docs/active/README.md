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

**Epic 2 (LAN Sync) is now in progress (started 2026-09-07)** — see the table below. T2.3 merge core
and the T2.4 sync-apply seam through `ConflictResolver` (PR-1..PR-6) are merged, and the
policy-driven mutation-routing / structural-conflict fence's Slices 0–2 are merged for **local UI
saves only**. See `docs/CHANGELOG.md` (2026-09-12, 2026-09-17) and
[`../specs/system_roadmap.md`](../specs/system_roadmap.md) §A.3 item 3.

**DFD-9a's last gate closed 2026-08-27** — the end-to-end check no automated test could perform was
run by the owner at a keyboard and ruled **PASS**. The shipped application demonstrably logs what it
predicted, through its production DI wiring, on both command paths
([evidence](../reports/2026-08-27-dfd9a-instrumentation-observation.md)). Telemetry now accrues
usable rows; it does not make the *existing* ones usable, and says nothing about prediction quality.

**The data-foundation decision phase closed 2026-08-26** (owner ruling: nine policies ratified; the
project holds **zero verified real user rows**). It produced two items that need an owner call and one
completed correction pass — see the table below and
[`../plans/2026-08-26-data-foundation-owner-decision-handoff.md`](../plans/2026-08-26-data-foundation-owner-decision-handoff.md).

**The proposal's own review closed 2026-08-27** — the owner ruled on **Q-1 … Q-5**, the five questions
the proposal had declined to answer with invented figures
([`../plans/2026-08-27-data-maturation-owner-decision-outcomes.md`](../plans/2026-08-27-data-maturation-owner-decision-outcomes.md)).
**Four of the five rulings are instructions not to invent the number yet.** The proposal reached
**revision 2** on that basis.

**S-1 and S-2 have since executed (2026-09-04/05).** S-0 was pre-registered, S-1's limited taxonomy
review completed, and S-2's catalogue was ratified — `GuidelineVersion v1` **froze 2026-09-04**
([freeze record](../plans/2026-09-04-s2-v1-freeze-record.md)). The independent human §10
reproducibility probe on the frozen guideline was **performed 2026-09-12**; the owner's Gold pass and
the independent reader agree cell-for-cell on all 20 rows, with `R-12`/`R-20` `unresolved=true` in
both. The result is **PASS under owner ruling `F-1`/`D-6` (Reading A)**, recorded 2026-09-18 — figures
and required companions in
[`../reports/2026-09-18-epic4-s2-section10-result.md`](../reports/2026-09-18-epic4-s2-section10-result.md).
It is scoped to one independent reader on one sealed 20-row batch — not corpus-wide, ML or dataset
accuracy. The two filled annotation sheets remain uncommitted; their evidence location (`F-4`) is an
open owner decision. The PASS satisfies `DFD-2`'s single condition and **authorizes nothing further**:
stages beyond S-2 (provenance → Gold-A/Gold-R → evaluation → controlled expansion) remain **not
authorized, not scheduled**, and this is Data Maturation, not canonical Epic 4 (`T4.1`–`T4.3`), which
has **not started**.

*Superseded 2026-08-19 (kept for history):* the previous banner said *"Epic 3 (SOE) is next"*, which
was true when written on 2026-08-02. The order it cited still holds — the
[master plan](../plans/2026-07-03-master-plan.md) sequences **E1 → E3 → E2 → E4**. With E1 and E3
both closed, the next epic in that sequence, the **LAN-sync epic (Epic 2), started 2026-09-07** (see
above) — this line itself is now superseded and kept only for history, same as the line above it.
**G3-1** — wiring the Epic 3 optimizer into production — remains unscheduled regardless of Epic 2's
start; that was never an either/or.

## Current (2026-09-18)

| Work | Plan | State |
|---|---|---|
| **Epic 2 — LAN Sync (T2.3/T2.4 core + structural-conflict fence)** | [`../plans/2026-09-13-policy-driven-mutation-routing-structural-conflict-fence-plan.md`](../plans/2026-09-13-policy-driven-mutation-routing-structural-conflict-fence-plan.md) | **In progress since 2026-09-07.** T2.3 merge core + T2.4 sync-apply seam through `ConflictResolver` merged (PR-1..PR-6, through PR #83, 2026-09-12). Fence Slices 0–2 merged for **local UI saves only** (through PR #95, 2026-09-17); Slices 3–6 not started, Slice 6 gated on open owner decision **SB-2/OD-1**. T2.5 recon-complete, not started. See `docs/CHANGELOG.md` 2026-09-12/2026-09-17 |
| **Prediction instrumentation defect (DFD-9a)** | [`../plans/2026-08-26-prediction-instrumentation-defect.md`](../plans/2026-08-26-prediction-instrumentation-defect.md) | **FIXED 2026-08-26**, suite 487 → 492. Seam returns the prediction record, `TaskDashboardItem` carries it, the write site logs both columns on both branches. **CLOSED 2026-08-27 — the last gate is shut.** The owner ran the end-to-end check at a keyboard against the real Debug database: four new rows through the production DI wiring, both command paths, all with `PredictedMinutes` and `Confidence` non-null, while the two pre-fix rows still read `NULL` in the same output. Ruled **PASS** on all four pre-registered criteria, twice. The second pass (non-overdue tasks) logged `WasMlPrediction = 1` with `Confidence` 0.90 and 0.7333, both reproducing exactly from each task's own `DiemUuTien` — so the ML branch demonstrably ran. Evidence: [`../reports/2026-08-27-dfd9a-instrumentation-observation.md`](../reports/2026-08-27-dfd9a-instrumentation-observation.md). Runbook: [`../plans/2026-08-26-dfd9a-instrumentation-runbook.md`](../plans/2026-08-26-dfd9a-instrumentation-runbook.md). **Still true:** pre-2026-08-26 rows remain unusable (no backfill is possible), `Confidence = 0` stays ambiguous under DFD-5, and nothing here says the predicted numbers are *good* |
| **Data Maturation & Coverage Expansion** | [`../plans/2026-08-26-data-maturation-coverage-expansion.md`](../plans/2026-08-26-data-maturation-coverage-expansion.md) | **S-0/S-1/S-2 executed; `GuidelineVersion v1` frozen 2026-09-04; independent §10 reproducibility probe performed 2026-09-12; PASS under owner ruling `F-1`/`D-6` (Reading A), recorded 2026-09-18** (see [`../reports/2026-09-18-epic4-s2-section10-result.md`](../reports/2026-09-18-epic4-s2-section10-result.md) for the figures and required companions; source sheets still uncommitted, `F-4` open). Upstream of, and distinct from, canonical Epic 4 (`T4.1`–`T4.3`, not started). Maturity is a `T-0…T-3` tier ladder with three binary invariants; **I-1/I-2/I-3 status as of S-2 needs a fresh read against the current plan revision, not restated here to avoid drifting out of sync with it.** Stages beyond S-2 (provenance → Gold-A/Gold-R → evaluation → controlled expansion) remain **not authorized, not scheduled** |
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
| **F-1** — M8-A merge gate at `≥0.60` vs a 0.000-accuracy `[0.6,0.7)` band | **Indication**, not a proven defect — n=11 at seed 42, and **authored** input against a model trained on other authored rows (DFD-1, 2026-08-26) | **Separate investigation candidate.** Deferred by owner ruling to [`../specs/system_roadmap.md`](../specs/system_roadmap.md) §A.4, and **reinforced 2026-08-27**: DFD-9a's ratified wording says *"do not change any confidence threshold as part of this defect."* The shipped fix moved none. **Not fixed, not scheduled**; a fix must *separate* the shared `DefaultMlConfidencePolicy`, not retune both consumers |
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
