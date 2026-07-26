# Conflict Scope — `ui_rf` → `dev` integration (Epic-1/P1 line into the ML/UI line)

**Date:** 2026-07-26
**Author:** PM/QA session (owner-directed)
> **⚠️ SUPERSEDED (2026-07-26).** The owner ruled `ui_rf` the newest, most stable, manually-tested trunk
> and chose **not** to integrate the dev/ML+UI line. The via-`dev` route and "adopt dev's redesign as base"
> recommendation below are **void**. Actual resolution: a `ui_rf`-authoritative, history-preserving merge
> makes `main` reflect `ui_rf` (dev line preserved in history, not integrated) — **PR #49**. This document is
> retained only as the conflict-surface analysis that informed the decision.

**Status:** scope-only — **no PR opened, no merge performed.** Advisory input to the owner's integration decision. **(Superseded — see banner above; PR #49 is the outcome.)**
**Method:** `git merge-tree --write-tree origin/dev ui_rf` (in-memory 3-way merge; no working tree touched).

---

## TL;DR

Merging `ui_rf` into `origin/dev` produces **18 conflicts — 7 code, 10 docs, 1 config.** The 188-commit
divergence is large in *count* but the actual conflict surface is **modest and tractable** (most commits
touch non-overlapping files). One semantic cluster (Analytics/Dashboard) and one keep-or-delete decision
(`WorkloadBalancerWindow`) are the only parts needing **owner intent**; everything else is mechanical.

- **merge-base:** `7225dba` (`feat(ui): Dashboard redesign — native XAML charts (#44)`)
- **head:** `ui_rf` @ `d0cea95`  •  **base:** `origin/dev` @ `3d8d850`
- **merge-tree exit:** 1 (conflicts, as expected)

## Divergence context (why two lines exist)

| Line | Carries | Where it lives |
|---|---|---|
| **`ui_rf`** (Epic-1/P1) | Sync-ready data model (M1.x), crash-safety, soft-delete cascade, the 2026-06-27 analytics redesign, Analytics stale-render fix, the P1 smart-add negation fix | `origin/ui_rf` (pushed 2026-07-26) |
| **`dev`/`main`** (M8/UI) | M8 study-time predictor retrain on real telemetry (#43), TextClassifier retrain, the **newer** native-charts "mission-control" Dashboard/Analytics redesign (#44/#46/#47) | `origin/dev`, partly `origin/main` |

They forked at `7225dba` and both evolved the **same analytics/dashboard surfaces** independently — that is
the root of the semantic conflicts below.

## The 18 conflicts, by severity

### Tier 1 — Semantic, needs owner intent (the Analytics/Dashboard cluster)
Both lines edited these since the fork (ui_rf/dev commit counts shown). Two competing redesigns of the same
screens — a mechanical merge is **not** enough; someone must decide which redesign is canonical and re-apply
the other line's *bug fixes* on top.

| File | ui_rf edits | dev edits | Note |
|---|---|---|---|
| `ViewModels/AnalyticsViewModel.cs` | 4 | 2 | ui_rf has the **stale-render fix** (`c4291c7`) that must survive |
| `Views/AnalyticsPage.xaml` | 2 | 1 | competing layouts |
| `Services/Analytics/StudyAnalyticsService.cs` | 2 | 1 | competing `GroupBy`/insight logic |
| `Services/Pipeline/Stages/AdaptStage.cs` | content | content | dedup-by-`TenMonHoc` touched on both |

**Recommendation:** take **dev's newer native-charts redesign as the base**, then re-apply ui_rf's
Analytics stale-render fix (reset chart outputs on the `!HasData` branch) and the dedup semantics on top.

### Tier 2 — Keep-or-delete decision
| File | Conflict | Decision needed |
|---|---|---|
| `Views/WorkloadBalancerWindow.xaml` | **modify/delete** — deleted in `ui_rf`, modified in `origin/dev` | Did ui_rf intentionally retire the workload balancer, or does dev's enhanced version stay? Owner call. |

### Tier 3 — Mechanical code conflicts (read both sides, resolve)
| File | Type |
|---|---|
| `Infrastructure/Persistence/SQLite/Repositories/SqliteHocKyRepository.cs` | content |
| `ViewModels/FocusViewModel.cs` | content |

### Tier 4 — Low-risk, combine both sides
`.gitignore` (content) + **10 docs** — mostly `add/add` where both lines created same-named files:
`docs/CHANGELOG.md`, `docs/specs/system_roadmap.md`, `docs/README.md`, `docs/plans/README.md`,
`docs/architecture/{async-workflow,data-model,usecase-flows}.md`,
`docs/knowledge/{debugging,programming,system-design}.md`. Resolve by concatenating/merging sections.

## Orphan to preserve

**`#48` (`0bdc5b5` "Dev (#48)") is on `origin/main` but on neither `origin/dev` nor `ui_rf`.** The chosen
route (`ui_rf → dev`, then `dev → main`) does **not** lose it — but the later `dev → main` merge must carry
it forward (a normal merge will; a squash/rebase could drop it — don't squash that step).

## Recommended path (owner-directed route = via `dev`)

1. **Reconciliation session** (scoped, ~0.5–1 day) on a throwaway `integ/ui_rf-into-dev` branch off
   `origin/dev`: merge `ui_rf`, resolve the 18 conflicts with the tier guidance above, `dotnet build` +
   full test suite must go green, spot-check Analytics/Dashboard render.
2. **PR `integ/ui_rf-into-dev` → `dev`** for review (not a local push to `dev`).
3. Later, separately: **PR `dev` → `main`**, preserving `#48`.

Do **not** do a local merge into `dev`/`main` and push the result — route through PRs so conflicts are
reviewable and shared branches are never force-moved.

## Decisions made

- **Route chosen: `ui_rf → dev` (not `ui_rf → main` directly).** *Why:* the repo already integrates through
  a `dev → main` PR flow (the "Dev (#NN)" merges), and `dev` already holds the M8 predictor retrain (#43) so
  the analytics/ML surfaces reconcile once, on `dev`, instead of twice. *What for:* one conflict-resolution
  pass on the branch built for it, with `main` protected behind a second review. *Experience:* the
  `merge-tree` scope (18, not 138) is what made "route via dev" safe to recommend — without it, the
  188-commit gap looked like a rewrite.
- **Scope-first, no PR yet (owner instruction).** *Why:* opening a conflicted PR before knowing the surface
  invites a blind resolution. *What for:* the owner decides the two intent-level questions (which analytics
  redesign is canonical; keep-or-delete `WorkloadBalancerWindow`) *before* anyone touches the merge.
  *Experience:* the two hard items are both **product** decisions, not merge mechanics — they belong to the
  owner, not the resolver.

## Reproduce this scope

```bash
git fetch origin
git merge-tree --write-tree --name-only origin/dev ui_rf   # exit 1 = conflicts; grep 'CONFLICT'
```
