# CI required-status-check name mismatch — main branch protection stuck forever

**Date:** 2026-08-02 · **Trigger:** PR #51 (`dev` → `main`, "Epic 1 + post-Epic 1 stabilization") ·
**Status:** DONE — fixed live via GitHub API, no code change involved.

## What happened and why

PR #51 was the first PR opened against `main` since branch protection was turned on
(`0419a62`, "enforce branch protection on main only") with the `ci.yml` workflow in place. Its
merge box showed **"1 pending check: `build-test` — Expected — Waiting for status to be
reported"**, permanently, while three *other* checks (including two runs of the same workflow)
were green. Re-running the workflow ("Re-run all jobs") did not help — the pending check never
moved.

The owner had just watched CodeRabbit auto-apply a two-line fix to `ci.yml`
(`persist-credentials: false` on the checkout step) and suspected that change broke CI. It did
not — that edit is orthogonal. **The workflow was never broken; the required-check name was
wrong from the day branch protection was enabled**, and this PR was simply the first one to run
into it.

## Root cause

GitHub Actions reports a check under the job's `name:` field when present, not its YAML key
(job id). `ci.yml` has always had both:

```yaml
jobs:
  build-test:                        # job id
    name: Build & Test (Windows)     # what GitHub Actions actually reports
```

Confirmed via `gh api repos/.../commits/dev/check-runs`: the real check-run name is
`"Build & Test (Windows)"`. But `main`'s classic branch protection
(`gh api repos/.../branches/main --jq .protection`) had:

```json
"required_status_checks": { "contexts": ["build-test"], "checks": [{"context": "build-test"}] }
```

`"build-test"` (job id) and `"Build & Test (Windows)"` (job name / actual check-run name) are
different strings, and GitHub requires an **exact** context match. No amount of re-running the
workflow can ever satisfy a required context that nothing reports. Compounding it:
`enforce_admins` is `true` on this ruleset, so even the repo owner cannot merge past a stuck
required check by admin override — the rule itself has to change.

Two dead ends hit while diagnosing, worth recording so they're not re-walked:

- `gh api repos/PotatoMine725/Smart-Study/branches/main/protection` 404'd on the *first* attempt
  because the repo slug was mistyped (`Potatomine725/SmartStudyPlanner` — wrong case, wrong repo
  name). The actual repo is `PotatoMine725/Smart-Study`. Once correct, the endpoint returned data
  fine — this repo has no admin-token permission issue.
- The repo also has a **ruleset** named "main sec" (`gh api repos/.../rulesets`) covering
  `deletion` and `non_fast_forward` only. It looked like a plausible source of the required check
  but wasn't — the required-status-check rule lives in **classic branch protection**, a separate
  and independently-configured system from rulesets. Both can apply to the same branch
  simultaneously; check both when diagnosing.

## Fix applied

```bash
gh api -X PATCH repos/PotatoMine725/Smart-Study/branches/main/protection/required_status_checks \
  --input - <<'EOF'
{
  "strict": true,
  "checks": [
    {"context": "Build & Test (Windows)", "app_id": null}
  ]
}
EOF
```

`app_id: null` lets GitHub bind it to whichever app last reported that context (resolved to
`15368`, the GitHub Actions app) — confirmed in the response. `strict: true` was preserved
unchanged from the prior config (branch must be up to date before merging). No workflow file
change was needed or made.

**Verification:** `gh pr checks 51` → `Passed: 3, Failed: 0` immediately after the patch, with no
new workflow run — the existing green check-run just started counting once the context matched.

## Decisions made (ADR-style)

### D1 — Fix the branch-protection rule, not the workflow's job `name:`
- **Why:** the job has carried `name: Build & Test (Windows)` since the very first commit that
  added the workflow (`f98e4c7`). Renaming the job to match `build-test` would be the accidental
  fix riding on a cosmetic regression (uglier PR check list) rather than a deliberate one.
  Branch protection is the newer, more-recently-touched, more-likely-to-be-wrong side.
- **What for:** zero code/workflow diff; the fix is purely a repo-settings correction, so it
  can't regress a test or introduce a review surface.
- **Experience:** confirmed via `gh api .../commits/dev/check-runs` *before* touching anything —
  never guess which side is "right" between a config value and a reported name; read the actual
  check-run name from the API first.

### D2 — Use `checks: [{context, app_id: null}]`, not the deprecated `contexts` array
- **Why:** `contexts` still works but is documented as legacy; `checks` is what the GitHub UI
  itself writes today, and pinning `app_id: null` avoids ambiguity if some other app/bot ever
  reports a check with the same string.
- **What for:** keeps the rule resilient — future-proofed against the deprecated field being
  dropped, without behavior change today.

### D3 — Diagnose via API before changing anything, in a protected/shared setting
- **Why:** branch protection on `main` is shared, hard-to-notice-if-wrong infrastructure — a bad
  guess here either locks out all future PRs (too strict) or silently disables the safety net
  (too loose, e.g. accidentally clearing `checks` entirely). Confirmed the exact stored value and
  the exact reported value before writing anything.
- **What for:** the fix was a single, minimal PATCH with a verified-correct target string, applied
  only after the owner explicitly confirmed (`AskUserQuestion`) — this class of change is exactly
  the "affects shared systems beyond local environment" case that needs a stop-and-confirm.

## How this class of bug shows up again

Anything that changes what string a required check reports will silently break `main` merges the
same way, with the same symptom (permanently pending, not failing). Concretely:

- Renaming the job's `name:` field in `ci.yml`.
- Renaming the workflow itself (`name: CI` at the top) — the check-run grouping in the PR UI
  uses `<workflow name> / <job name> (<event>)`, though the stored *required* context is just the
  job name/id, not the full grouped string.
- Replacing GitHub Actions with a different CI provider (or a second workflow file) that reports
  under a different name.
- Copying branch protection settings to a new repo via a template/script that hardcodes the old
  context string.

---

## Manual guide: fixing this yourself in the GitHub UI (no `gh` CLI needed)

Use this if a PR into `main` is stuck with a check showing **"Waiting for status to be
reported"** that never resolves, even after re-running the workflow.

### Step 1 — Confirm it's actually a name mismatch, not a real failure

1. Open the PR → **Checks** tab (or the merge box at the bottom of the **Conversation** tab).
2. Note the exact name of the check marked **Required** but stuck pending (e.g. `build-test`).
3. Note the exact name(s) of the checks that show **green/successful** for the same workflow
   (e.g. `CI / Build & Test (Windows) (pull_request)`).
4. If the pending check's name doesn't appear anywhere in the successful list (ignoring the
   `<workflow> / ... (<event>)` wrapping GitHub adds for display), it's a name mismatch — continue.
   If instead you see a check with the *same* name that's red/failed, that's a real CI failure,
   not this bug — go fix the workflow/tests instead.

### Step 2 — Find the real check name

1. Go to the **Actions** tab → open the latest run of the CI workflow for your branch.
2. The job name shown in the run's sidebar (e.g. "Build & Test (Windows)") is the string GitHub
   Actions reports for that check. This comes from the job's `name:` field in the workflow YAML
   (`.github/workflows/*.yml`) if set, otherwise the job's YAML key (job id).

### Step 3 — Open branch protection settings

1. Repo → **Settings** → **Branches** (left sidebar, under "Code and automation").
2. Find the rule for `main` (or whichever branch is stuck) → click **Edit**.
   - If you don't see a rule here, also check **Settings → Rules → Rulesets** — some repos use
     the newer Rulesets system instead of (or alongside) classic branch protection. Required
     status checks can live in either; check both.

### Step 4 — Fix the required check name

1. Scroll to **"Require status checks to pass before merging"**.
2. In the search box, you'll see the currently-required check(s) listed as chips (e.g.
   `build-test`) — remove the wrong one (click the ✕ on the chip).
3. Type the real check name from Step 2 into the search box. GitHub autocompletes from checks
   that have actually reported on the repo recently — if it doesn't show up, the workflow needs
   to have run at least once on some branch first (push a trivial commit or use
   `workflow_dispatch` to trigger it).
4. Select it to add it to the required list.
5. Scroll down and click **Save changes**.

### Step 5 — Verify

1. Go back to the stuck PR. Do **not** re-run the workflow — the existing successful run will be
   picked up immediately once the required-check name matches.
2. The merge box should flip to all-green within a few seconds. If it doesn't, hard-refresh the
   page (GitHub sometimes caches the merge-box state briefly).

### Notes for next time

- This only needs fixing once per branch/rule — it's a settings correction, not something that
  recurs unless the workflow's job name or the branch protection rule is changed again later.
- If you rename a CI job's `name:` field in the future, remember to also update the required
  check name in **Settings → Branches** (or **Rules → Rulesets**) for every protected branch — the
  two are not linked and GitHub will not warn you they've drifted apart.
- `enforce_admins` being on for `main` means this can't be worked around by an admin merge
  override either — the rule itself must be corrected, as above.
