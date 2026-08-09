# Branch protection hardening — direct pushes blocked on `dev` and `main`

**Date:** 2026-08-09 · **Trigger:** an Epic 3 merge push to `dev` reported `Bypassed rule violations`,
revealing that `dev`'s required status check had never been satisfiable · **Applied by:** owner
instruction, after the lockout risk below was surfaced and ruled on.

**Reads with:** [`2026-08-02-ci-required-check-mismatch-fix.md`](2026-08-02-ci-required-check-mismatch-fix.md)
— the check-name mismatch that started this, and its 2026-08-07 addendum recording that `dev` carried
the same defect five days longer than `main`.

---

## What changed

| Setting | `dev` before | `main` before | both after |
|---|---|---|---|
| `required_status_checks.checks[].context` | `build-test` ❌ | `Build & Test (Windows)` | `Build & Test (Windows)` |
| `required_status_checks.strict` | true | true | true |
| `enforce_admins` | **false** | true | **true** |
| `required_approving_review_count` | 1 | 1 | **0** |
| `allow_force_pushes` | false | false | false |

Three API calls, no workflow or code change:

```bash
gh api -X PATCH repos/PotatoMine725/Smart-Study/branches/dev/protection/required_status_checks \
  --input - <<'EOF'
{ "strict": true, "checks": [ {"context": "Build & Test (Windows)", "app_id": 15368} ] }
EOF

# same body against dev and main
gh api -X PATCH repos/PotatoMine725/Smart-Study/branches/{dev,main}/protection/required_pull_request_reviews \
  --input - <<'EOF'
{ "required_approving_review_count": 0, "dismiss_stale_reviews": false, "require_code_owner_reviews": false }
EOF

gh api -X POST repos/PotatoMine725/Smart-Study/branches/dev/protection/enforce_admins
```

## The lockout that was avoided

**This repo has exactly one collaborator, and GitHub does not let anyone approve their own pull
request.** Both branches required **1 approving review**. On `dev` that rule was inert, because
`enforce_admins: false` let the owner bypass everything — which is also why nobody noticed. Setting
`enforce_admins: true` while leaving the review requirement at 1 would have made it bite, and the
result is not "stricter": it is **`dev` frozen**, because no PR into it could ever accumulate the one
approval it needs.

`main` was **already in exactly that state**. `enforce_admins: true` + 1 required approval + one
human = unmergeable. PR #51 (the last merge into `main`, 2026-08-02) carries only
`sourcery-ai[bot]` and `coderabbitai[bot]` reviews, both `COMMENTED`, zero `APPROVED` — so
enforcement was almost certainly switched on after it merged, and nothing has been merged into `main`
since to discover it. It would have surfaced at the worst possible moment: the next release PR.

Dropping the approval count to 0 keeps *"Require a pull request before merging"* on — that is the
presence of the `required_pull_request_reviews` object, not the count — while removing an approval
that can never be obtained. **CI becomes the real gate instead of a human check that structurally
cannot happen.** If a second collaborator is ever added, raising the count back to 1 is one API call
and is the point at which four-eyes review becomes achievable rather than decorative.

## Verification

Reading the config back proves only that the write landed. Both directions were tested.

**Direct push is genuinely blocked** — an empty probe commit was pushed at `dev`, rejected, and
removed locally with `git reset --soft` (working tree untouched):

```
remote: error: GH006: Protected branch update failed for refs/heads/dev.
remote: - Changes must be made through a pull request.
remote: - Required status check "Build & Test (Windows)" is expected.
 ! [remote rejected] dev -> dev (protected branch hook declined)
```

Note what is *absent*: the `Bypassed rule violations` line that every previous push to `dev` printed.
That line was never a warning about CI — it was a report that a gate did not apply.

**The PR path still works** — verified by shipping this very document through it: branch →
push → PR → CI green → self-merge, with no approval available and none required. That test is the
one that mattered, because it is the failure mode that would have locked the branch.

## Decisions made (ADR-style)

### D1 — Drop the approval requirement to 0 rather than enforce an unobtainable one

- **Why:** "block direct pushes" and "require an approval" look like the same hardening but are not.
  With a single collaborator the first is achievable and the second is a deadlock. Enforcing a rule
  that cannot be satisfied is not a stricter gate, it is an outage with a security-shaped
  justification.
- **What for:** every change to `dev` and `main` now goes through a PR that must pass
  `Build & Test (Windows)` before it can merge, and the owner can still merge their own work.
- **Experience:** the config alone did not reveal this. It took three facts read together — sole
  collaborator, `required_approving_review_count: 1`, and GitHub's no-self-approval rule — none of
  which is alarming on its own. Before enabling `enforce_admins` on any branch, check what *other*
  rules it is about to start enforcing.

### D2 — Fix `main` in the same pass, unprompted by any symptom

- **Why:** `main` was already latently locked and had no symptom, because nothing had been merged
  into it since enforcement was enabled. A latent lockout on the release branch is worse than an
  obvious one — it is discovered under time pressure, during a release, by someone who assumes they
  broke something.
- **What for:** `main` is mergeable again, with its protection intact and strictly stronger than
  before on the axis that matters (CI must pass, no direct pushes, no force pushes).
- **Experience:** the same blind spot as the 2026-08-02 fix, which corrected `main` and never looked
  at `dev`. Both times the rule existed in two places and only one was examined. **Enumerate every
  branch holding a copy of a shared rule.** It is one API call per branch.

### D3 — Prove the block with a rejected push, not with a config read

- **Why:** this entire chain of work exists because a required status check sat unsatisfiable for
  five days while every config read looked correct. `build-test` was present, well-formed, and
  enforced-looking; it just named a check that no run ever produced. A green config read is not
  evidence.
- **What for:** the `GH006` rejection above is a signal that demonstrably fires. So is its
  counterpart — the successful PR merge of this document — which proves the gate is not *over*-tight.
- **Experience:** an empty commit plus `git reset --soft` is a zero-cost, fully reversible probe for
  push-side branch protection, and `--soft` specifically leaves an uncommitted working tree intact
  where `--hard` would have destroyed it.

## How to undo, if this proves too strict

```bash
# restore admin bypass on dev (re-enables direct pushes for admins)
gh api -X DELETE repos/PotatoMine725/Smart-Study/branches/dev/protection/enforce_admins

# restore a 1-approval requirement (only once a second collaborator exists)
gh api -X PATCH repos/PotatoMine725/Smart-Study/branches/dev/protection/required_pull_request_reviews \
  --input - <<'EOF'
{ "required_approving_review_count": 1 }
EOF
```

Emergency note: with `enforce_admins: true` there is no in-band override. If CI is broken or GitHub
Actions is unavailable, the only way to land a change on `dev` or `main` is to `DELETE` the
`enforce_admins` endpoint first, push, then `POST` it back. That is the deliberate cost of the gate
being real.
