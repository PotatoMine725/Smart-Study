# WP-6 — Repo & Doc Hygiene

**Date:** 2026-08-02
**Package:** WP-6 (Category B) of `docs/plans/2026-07-27-post-epic1-stabilization.md` — the last of six
**Commits:** `12291d0` (6.1a, design sources) · `6416e50` (6.1b delete + 6.2 docs/counts/CSA/G4)
**Suite:** **391** passing, unchanged by this package — it adds no tests and no production logic
**Closes:** Epic 2 entry criteria **#8** and **#10**. Criteria now **11 of 12**.

---

## 1. Implementation Summary

WP-6 was scoped as pure hygiene: retire a duplicate directory, make stale numbers true, amend
the CSA. It did all three. The only production-file change in the package is the removal of one
unused `PackageReference`.

| # | Task | Outcome |
|---|---|---|
| 6.1 | Retire the untracked root `Assets/` | Done. 7 unique files preserved at `docs/assets/icon-source/`; root directory deleted. |
| 6.2 | Correct stale numbers, drop the unused package, amend the CSA | Done. README 337 → 391; GitNexus counts re-measured; `Verify.CommunityToolkit.Mvvm` dropped; three corrections appended. |
| 6.2+ | Gate G4 onto the Epic 2 planning agenda (criterion #10) | Done — written into the roadmap where Epic 2 planning will look. |
| 6.3 | Dependency advisory bump | **Not run, deliberately.** See §3.4. |

---

## 2. Behaviour Changes

| # | Change | Visible to |
|---|---|---|
| 1 | Root `Assets/` no longer exists; its design sources are tracked at `docs/assets/icon-source/` | Anyone running `git status` — the untracked-noise entry is gone |
| 2 | `Verify.CommunityToolkit.Mvvm` removed from `SmartStudyPlanner.csproj` | Restore graph only; no code referenced it |
| 3 | README no longer claims the build is "clean with zero errors" | Readers |
| 4 | Roadmap §A.3 carries gate G4 as an explicit Epic 2 agenda item | Epic 2 planning |

Nothing here changes runtime behaviour. The build was re-verified after the deletion
(4 projects, 0 errors) and after the package removal (0 errors, 391 tests passing).

---

## 3. Engineering Notes

### 3.1 The irreversible step was made reversible before it was taken

Task 6.1 deletes **untracked** data. `git` cannot restore it, which makes this the only step in
the six-package plan with no rollback. The plan guarded it with a pre-flight
`diff -r --brief Assets docs/assets/icon-source` and required the only differences to be
`icon.ico` (deliberately not copied) and the new `README.md`.

That guard ran and passed exactly as predicted. It was not treated as sufficient. The copy was
**committed as its own commit** (`12291d0`) and the resulting tree inspected with
`git show --stat` — all 8 files present, including `Icon Preview.html`, whose space in the
filename is precisely the kind of thing a shell copy loses silently — **before** `rm -rf Assets`
ran. A `diff` proves the bytes matched a moment ago; a commit means they still exist afterwards.

The two together cost one extra commit and convert "verified, then destroyed" into "preserved,
then destroyed".

### 3.2 A stale number and a misleading true statement sat on the same line

README:84 read: *"The test suite has 337 tests, all passing, and the build is clean with zero
errors."* The plan scheduled only the `337` → `391` fix.

The second clause is literally true — the build emits zero *errors*. It is also the sentence a
reader uses to decide whether the build is healthy, and the build carries 184 warning lines
(94 after MSBuild's per-project de-duplication), overwhelmingly `CS8618`, plus two dependency
advisories that the CSA had already flagged. Correcting the count while leaving that clause
would have produced a freshly-verified sentence that still misinforms.

Both halves were fixed. The replacement names the warnings and the advisories as accepted debt
and points at CI as the thing that keeps the count honest.

### 3.3 "192 build warnings" no longer reproduces — and that is drift, not an error

The CSA's §8.6 figure was **192**. Measured at WP-6 on Debug with `--no-incremental`:
**184 warning lines**, which MSBuild summarises as **94 Warning(s)**.

The two measurements are both correct and answer different questions — the same warning is
emitted once per referencing project, so the line count exceeds the distinct count. The CSA's
192 matched neither, which is unsurprising after six packages changed code and one removed a
package reference.

This is recorded as drift in the CSA's Corrections section rather than as a fourth correction.
The number is a *measurement*, not a target, and Category C's decision to leave `CS8618` alone
does not depend on which figure is used.

One process note: `dotnet build` in this environment is rewritten through `rtk`, whose filtered
summary reports the de-duplicated 94. The 184-line figure required going around the filter via
PowerShell. Worth knowing before quoting a warning count from a filtered tool as if it were raw.

### 3.4 6.3 was not run, and the reason is not "we ran out of time"

The plan marks Task 6.3 (pinning patched `SQLitePCLRaw` and `System.Drawing.Common`) **opt-in,
owner's call**, and the owner has not opted in. That alone settles it.

There is a second, independent reason worth recording, because it would apply even under a
blanket "do everything" instruction: **6.3's own verification step cannot be performed by an
agent.** Step 3 requires launching the app, opening Analytics, triggering *Retrain*, and
confirming it completes — because the suite does not cover model training end to end and these
are transitive dependencies of `Microsoft.ML`. Running 6.3 without that step would mean landing
a dependency change with its stated verification omitted, on the one item in the plan explicitly
identified as able to break something the suite does not cover.

Declining to do a thing badly is not the same as leaving it undone by accident. The advisories
remain on the roadmap's deferred ledger at `system_roadmap.md:100-102`, where they already were.

### 3.5 The blocker on criterion #1 was misdiagnosed by the plan — and it is still an owner action

WP-1 Step 6 states: *"This is not an agent action. Enabling branch protection needs admin scope
on the repository and cannot be done from the CLI without it."*

The premise is wrong. `gh api repos/:owner/:repo` returns `{"admin":true,"maintain":true,…}` —
the configured token has admin, and `PUT …/branches/dev/protection` would succeed.

It was still not executed, for a reason the plan did not give. A required status check **rejects
direct pushes** whose head commit has no passing run — and every commit in this package, and most
of the six, went to `dev` directly. Enabling it changes the owner's daily workflow from
push-to-`dev` to PR-per-change. That is a policy decision about how someone wants to work, and
"the agent had the permission" is not a reason to make it for them.

The distinction matters beyond this instance: *unable* and *not mine to decide* are different
blockers, and only one of them is fixed by finding a token with more scope. The plan collapsed
them, and had the token check gone the other way the error would never have surfaced.

Ready when the owner is:

```bash
gh api -X PUT repos/:owner/:repo/branches/dev/protection \
  -F required_status_checks.strict=true \
  -f 'required_status_checks.contexts[]=build-test' \
  -F enforce_admins=false \
  -F required_pull_request_reviews=null \
  -F restrictions=null
```

`enforce_admins=false` is deliberate — it leaves the owner an escape hatch on a solo repository.
Repeat for `main`.

### 3.6 Criterion #10 is satisfied by writing it down, which is weaker than it sounds

Criterion #10 reads: *"Gate G4 (tombstone retention) is explicitly on the Epic 2 planning
agenda."* That is a criterion about a *document*, and it is now literally true — G4 is recorded
in `system_roadmap.md` §A.3 under the LAN-sync entry, with the three reasons it is load-bearing
(a tombstone purged on one peer but not another resurrects the row on the next merge; D-I's
3-way merge needs the last-synced base to still exist; unbounded retention grows without limit
on a device that never syncs).

What that does **not** mean is that anyone has agreed to it or decided it. The note in the
roadmap says so in its own text, rather than leaving a future reader to infer that a checked box
implies a resolved gate. Recording the item is what stabilization can honestly deliver; deciding
retention policy is Epic 2's, and guessing at it from here was the thing the criterion existed to
prevent.

### 3.7 A test run reported failure once and did not reproduce

A chained `build && test` invocation reported `0 passed, 1 failed` immediately after the
`Verify.CommunityToolkit.Mvvm` removal — a collection-level failure, not a test assertion. Two
subsequent runs, one raw and one filtered, both reported **391 passed / 0 failed**, and no
`SmartStudyPlanner.exe` was holding a lock.

Recorded rather than dropped, because the suite runs 57 classes in parallel with no
`[Collection]` grouping or `xunit.runner.json` (Category C: *"leave until something actually
flakes — and now CI will notice if it does"*). One non-reproducing collection failure during a
chained build is weak evidence and is not a flake in the tests themselves, but it is the first
sighting since that decision was recorded, and the next one should not start from zero.

---

## 4. Verification

| Check | Result |
|---|---|
| `diff -r --brief Assets docs/assets/icon-source` before deletion | Only `icon.ico` and `README.md` — nothing unique left behind |
| Design sources committed and inspected before `rm -rf` | 8 files in the tree at `12291d0`, `Icon Preview.html` included |
| Build after deletion | 4 projects, **0 errors** |
| Build + suite after dropping the package reference | 0 errors, **391 passed** |
| README count vs `dotnet test` | Both **391** |
| GitNexus re-index | 4,291 symbols / 9,848 relationships / 120 flows at `12291d0` |
| Release build + suite | 0 errors; **391 passed** |
| Clean-clone build | 0 errors; `SmartStudyPlanner.exe` produced; no root `Assets/`; icon and design sources present |

### 4.1 End-to-end

Run in full after the last code-affecting commit (`6416e50`):

```
dotnet restore SmartStudyPlanner.slnx                                  -> 0 errors
dotnet build   SmartStudyPlanner.slnx --no-restore -c Release          -> 4 projects, 0 errors
dotnet test    SmartStudyPlanner.Tests/… --no-build -c Release         -> 391 passed, 0 failed
```

**Clean clone** into the session scratchpad, which is the check that matters most for this
package — 6.1's whole premise is that nothing in the build referenced the deleted directory, and
a fresh clone is the only test that does not share this working tree's state:

```
git clone . $TEMP/ssp-verify && dotnet build SmartStudyPlanner.slnx -c Debug
  -> 4 projects, 0 errors
  -> SmartStudyPlanner.exe produced (174 KB)
  -> root Assets/ absent, as intended
  -> SmartStudyPlanner/Assets/icon.ico present (14.7 KB) — the one the csproj references
  -> docs/assets/icon-source/ complete: icon.svg, favicon.svg, Icon Preview.html, png/×4, README.md
```

The last line is the one that proves 6.1 did not lose anything: the design sources are reachable
from a clone that has never seen the deleted directory.

---

## 5. Decisions made

**Decision: commit the design sources as a separate commit before deleting the root folder,
rather than doing both in one.**
*Why:* Untracked data has no rollback. The plan's `diff -r` guard proves the copy matched at one
instant; it does not survive the delete. A commit does.
*What for:* So the only irreversible step in six packages is preceded by the data being durable,
not by a check that it was durable-adjacent.
*Experience:* The cost was one extra commit and about a minute. `Icon Preview.html` — a filename
with a space — is exactly the case where a shell copy fails quietly, and `git show --stat` is a
much better detector of that than reading a `cp` command and believing it.

**Decision: fix both halves of the README sentence, not just the number the plan named.**
*Why:* "337 tests" and "the build is clean with zero errors" are one sentence doing one job:
telling a reader the project is healthy. Correcting the measurable half and leaving the
misleading half would have made the sentence *more* trustworthy while it was still wrong.
*What for:* So the freshly-verified number does not lend credibility to the claim beside it.
*Experience:* This is scope the plan did not authorise, and it is a single sentence in a README —
small enough to just do, and flagged here rather than slipped in. The line now names the warnings
and advisories instead of implying they do not exist.

**Decision: do not enable branch protection, despite having the permission.**
*Why:* Required status checks reject direct pushes. Every commit in this package went straight to
`dev`. Enabling it converts the owner's workflow to PR-per-change — a decision about how someone
works, not a technical gap.
*What for:* So criterion #1 stays honestly open rather than being closed by an action the owner
did not choose.
*Experience:* The plan justified it as an agent limitation ("needs admin scope"), which turned out
to be false. The right justification was always the other one. Worth separating in future plans:
*cannot* and *should not* look identical from the outside and are fixed by completely different
things — one by a token, one by asking.

**Decision: satisfy criterion #10 by recording G4, and say in the same breath that recording is
not deciding.**
*Why:* The criterion is worded as a documentation state, and it is now true. But a checked box
next to "gate G4" invites a future reader to assume the gate is handled.
*What for:* So Epic 2 planning finds an open question phrased as an open question.
*Experience:* This is the second criterion in the plan whose wording is weaker than its intent
(#11 was the first — its original signature covered only the timer path). Criteria that describe
*artifacts* rather than *properties* are easy to satisfy and easy to over-read.

**Decision: skip 6.3 and record two independent reasons.**
*Why:* The owner has not opted in, and separately, its verification requires a manual ML retrain
an agent cannot perform.
*What for:* So the omission reads as a decision rather than an oversight, and so a future session
does not "finish" it without noticing the verification gap.
*Experience:* The second reason is the durable one. If the owner opts in later, 6.3 still cannot
be completed unattended — the retrain check has to be someone's job, and naming that now is
cheaper than discovering it mid-task.

---

## 6. Follow-ups

1. **Criterion #1 (branch protection) is the only Epic 2 entry criterion still open.** Owner
   action; the exact command is in §3.5. Until it is set, WP-1 measures without enforcing.
2. **`Prompt/` and `tools/epic1_b2_verify.py` remain untracked.** Out of WP-6's scope, which the
   plan limited to root `Assets/`. Both are real artifacts (session prompts and a verification
   script) and neither is gitignored, so `git status` stays noisier than it needs to be. Worth a
   decision — track, ignore, or delete — but not one to make silently.
3. **The GitNexus index goes stale on the next commit, by construction.** The counts in
   `system_roadmap.md` and `CLAUDE.md` carry the commit they were taken at (`12291d0`) so a
   reader can tell. Chasing exactness here is a treadmill; the commit reference is the fix.
4. **One non-reproducing collection failure** — §3.7. Not actionable alone; recorded so a second
   sighting is a pattern.
5. **The stabilization plan is now superseded** and says so in its header. Its Progress table is
   kept current because a memory entry points at it as the resumption point; everything else in
   it is historical. The reports under `docs/reports/` are the durable record.
