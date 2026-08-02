# Architecture Review — Post-Epic 1 Engineering Stabilization Plan

**Subject:** `docs/plans/2026-07-27-post-epic1-stabilization.md` (1843 lines, 6 work packages)
**Reviewer brief:** maintainability, longevity, execution quality. Not a redesign, not more implementation detail.
**Baseline:** plan written against `fcd4b42` / `d3deca7`.

---

## 1. Overall assessment

**The plan is sound and should be executed close to as written.** Scope discipline is the strongest thing about it: six packages, every CSA finding classified A/B/C/D, and the Category C/D tables carry real justification rather than a shrug. The one genuinely new abstraction (`DeviceIdentity`) is defended in the ADR section, and the honesty about what it does *not* fix (hostname collision) is the mark of a plan written to be executed rather than approved.

What weakens it is not judgement but **anchoring**. The document is coupled to a code snapshot in three ways that will bite before the plan even finishes:

- one step **cannot pass as written** (E1) — a guard test that matches its own source;
- one package's stated internal ordering **contradicts its own task body** (E2);
- two tasks reference **line numbers that an earlier step of the same task invalidates** (E3).

Separately, the plan's single declared dependency (WP-2 → WP-3) is argued from a premise that does not hold (E4). The order is right; the reason given is not — and that costs credibility on the dependencies that *are* real, while hiding parallelism the plan explicitly says it wants.

Everything else is additive: per-WP exit/rollback rows, a lifecycle header, and closing WP-1's enforcement gap.

**Verdict: approve with the eight essential refinements below.** None changes a priority, a package boundary, or the 4–6 day estimate.

---

## 2. Strengths (keep these — do not let refactoring erode them)

1. **The "Three CSA corrections" section.** A plan that pushes back on its own sole input, with evidence (`git ls-files`, md5, the actual `try`/`catch` in `App.xaml.cs`), and then schedules the amendment as WP-6.5. This is the single most valuable page in the document.
2. **Failing-test-first sequencing inside every task.** Step 1 write the test → Step 2 confirm it fails *and what the failure message is* → Step 3 fix → Step 4 confirm. The expected failure text (`SQLite Error 19: NOT NULL constraint failed`) is stated, so an executor knows whether it failed for the right reason.
3. **The WP-2 Task 2.1 contingency.** "If it passes, good. If it fails, **stop, do not fix the test**, report a new Category A finding." Naming the unknown outcome in advance, and pre-authorising the stop, is what makes removing a false green safe to delegate.
4. **Characterization tests explicitly labelled as such** (WP-4): "the implementation is the specification; if a test fails, fix the test." Prevents an executor from "improving" `GenerateSchedule` under cover of adding coverage.
5. **The `DeviceIdentity` seeding decision and its honesty table.** Fixed / not-fixed as two rows, plus an explicit instruction not to claim collision is fixed in the commit message. Rare and correct.
6. **Epic 2 Entry Criteria are mechanically checkable** — named test classes and grep commands, not "quality is good".
7. **Category C/D entries record *why*, not just *that*.** The 192-warning entry ("It is a measurement. CI will keep measuring it.") is the right framing and will still be right in a year.

---

## 3. Essential refinements

Eight items. Estimated net effect on the document: **≈ +45 lines on 1843** (E3 and E4 are net-negative). If a revision adds materially more than that, it has drifted from "more robust, not larger".

---

### E1 — `TestFile_KhongDungWallClock` can never pass, and the Entry Criterion that verifies it is unsatisfiable

**Where:** WP-2 Task 2.1 Step 4 (plan `:347-375`); Entry Criteria bullet 2 (`:1719`).

**The defect.** The guard test reads `DecisionEngineTests.cs` — the file it is appended to — and asserts `Assert.DoesNotContain("DateTime.Now", source)`. That literal appears **twice in its own source**: in the assertion itself, and in the Vietnamese comment above it (`// DateTime.Now đều trôi theo wall clock…`). The assertion therefore always trips. The executor will hit a red test at Step 5 with no diagnosis in the plan, on the package whose entire purpose is restoring trust in the suite.

**Recommended patch.** Build the needle by concatenation and scrub the comment:

```csharp
        // Chốt chặn: file này phải hoàn toàn tất định. Bất kỳ deadline nào dựng từ
        // wall clock đều trôi và sớm muộn sẽ pass/fail vì lý do sai
        // (xem CSA 2026-07-27, §8.4 "the false green").
        [Fact]
        public void TestFile_KhongDungWallClock()
        {
            // Ghép chuỗi để chính dòng assert này không tự trở thành một match.
            var needle = "DateTime" + "." + "Now";
            var source = System.IO.File.ReadAllText(
                System.IO.Path.Combine(TestSourceRoot(), "Services", "DecisionEngineTests.cs"));

            Assert.DoesNotContain(needle, source);
        }
```

And amend Entry Criteria bullet 2 — currently *"contains zero occurrences of `DateTime.Now`"*, which the guard's own needle construction makes literally false forever. Append: *"(the guard builds its search string by concatenation so it does not match itself; `TestFile_KhongDungWallClock` passing is the criterion)."*

- **Rationale:** a Definition of Done whose verifier contradicts the criterion it verifies is the exact failure focus area 7 asks for. Both halves must move together.
- **Expected benefit:** removes the only step in the plan that is guaranteed to fail; removes a permanently-unsatisfiable acceptance gate.
- **Impact:** ~4 lines changed in the plan's code block, one clause added to a criterion. No design change.

**Retain as a strength:** the guard's `File.ReadAllText` throws loudly if the file is renamed, rather than silently passing. That is the right failure mode and should not be "fixed" into a soft skip.

---

### E2 — WP-3's "either order works" claim contradicts Task 3.3's own body

**Where:** WP-3 inventory row *Internal order* (`:100`) vs Task 3.3 Step 1 (`:1026`).

**The contradiction.** The inventory says the three tasks are independent and *"running **3.3 first** is equally valid and slightly tidier"*. Task 3.3 Step 1 says *"Add this to the `SoftDeleteReadPathTests` class created in Task 3.1, so it reuses that file's `NewDb()`."* If 3.3 runs first, the class and its fixture do not exist. This is a hard file dependency, not a preference.

**Recommended patch.** Replace the *Internal order* row with:

> **Internal order** | 3.1 → 3.2 → 3.3. **3.1 before 3.3 is a hard dependency**, not a preference: Task 3.3's test is appended to the `SoftDeleteReadPathTests` class Task 3.1 creates and reuses its `NewDb()` fixture. 3.2 is genuinely independent of both and may run at any point in the package. Note that 3.1's fixtures set `MucDoCanhBao = "An toàn"` explicitly *because* 3.3 has not landed; that is expected, and 3.3 does not require removing it.

- **Rationale:** the plan's most dangerous inconsistencies are the ones that read as permission. An agent following the inventory row will start with 3.3 and immediately be off-plan.
- **Expected benefit:** eliminates a reordering that fails on the first step.
- **Impact:** one table row rewritten. Net ~0 lines. No task body changes.

---

### E3 — Re-key edit targets to code text; demote line numbers to baseline hints

**Where:** worst instances are WP-2 Task 2.1 Step 3 (`:332-345`) and WP-3 Task 3.2 Step 5 (`:902-953`). The pattern is document-wide.

**The defect is not staleness in the abstract — it is self-invalidation within a single task.** Task 2.1 Step 1 replaces a 10-line test with a ~21-line one. By the time the executor reaches Step 3, its table (`:110, :121, :134, :149`) is off by roughly eleven lines. Task 3.2 Step 5 inserts a ~4-line property near `AppDbContext.cs:15-19`, then instructs edits "at `:103` and `:109`" — which by then are `:107` and `:113`.

**Recommended patch — three parts.**

**(a)** Add a preamble immediately under the *Input* line at the top of the plan:

> **On line numbers.** Every `file.cs:NN` in this document is a navigational hint taken at baseline `fcd4b42`. Where a step also quotes the code, **the quoted snippet is the authoritative anchor** — search for it. If a line number and a snippet disagree, the snippet wins. Steps that insert lines shift every reference below them within the same file.

**(b)** Replace Task 2.1 Step 3's six-row line table entirely with one instruction — shorter *and* more robust:

> Replace every remaining `DateTime.Now` in this file with `FixedNow`, preserving any `.AddDays(n)` suffix. At baseline there are exactly six (`:37, 78, 110, 121, 134, 149`; `:92` was handled by Step 1). If the count differs, the file has moved on from the baseline — replace all of them and note it in the commit body. The Step-5 guard test is the check.

**(c)** In Task 3.2 Step 5, append to the `SyncStamper.Apply(...)` instruction: *"— both call sites; their line numbers shift once the `DeviceIdProvider` property above is inserted, so search for the `DeviceHelper.GetId()` argument rather than counting lines."*

- **Rationale:** focus area 1, with the sharpest available evidence. The plan already quotes every snippet it needs, so the fix is re-pointing the anchor, not adding detail.
- **Expected benefit:** the plan survives its own execution, and survives an unrelated commit landing on `dev` mid-flight.
- **Impact:** **net −4 lines** (the six-row table collapses to one paragraph). Zero change to what gets edited.

**Deliberately not recommended:** stripping line numbers globally. In sections like WP-3 Task 3.1's "exactly two read paths leak, and `SqliteHocKyRepository:98` / `SqliteStudyTaskRepository:56` omit the predicate *deliberately*", the line numbers carry the evidence that the sweep was actually done. Keep those.

---

### E4 — Correct the WP-2 → WP-3 rationale, and unlock the parallelism it hides

**Where:** asserted three times — WP-2 inventory *Reasoning* (`:88`), *Why this order* (`:154`), and the ADR at `:1795-1798`.

**The non-sequitur.** All three justify the dependency as: WP-3 changes `GetForHocKyAsync`, and validating that against a suite containing a demonstrated false green would be circular. But the false green is in the **DecisionEngine 31–60-day priority band**. It has no bearing on whether the Analytics and stats tests that would catch a WP-3.1 regression are trustworthy. Restoring it does not make those tests more meaningful.

**The real reason to run Task 2.1 first is the contingency, which the plan already documents at `:330`:** if 2.1 surfaces a genuine priority-band defect, Category A scope grows and the owner has to re-decide before WP-3 starts. That is a **scheduling gate**, not a validation gate — and it is a better argument, because it is true.

**Recommended patch.** Rewrite the three passages to the scheduling-gate framing, and amend the Execution Order block to show what actually falls out:

```
WP-1 CI Gate  (S)                        — land via PR (see E5)
   │
   ├─ WP-2.1 False green + de-date  (S)  — the ONLY gate on WP-3
   │     ↓  contingency: if 2.1 fails, a real priority-band defect
   │     ↓  exists — stop and re-scope with the owner before WP-3.
   │  WP-3 Persistence & Sync Identity (M)
   │     3.1 → 3.3 (hard, see E2);  3.2 independent
   │
   ├─ WP-2.2 / WP-2.3  test isolation    (S)  ┐  disjoint files from
   ├─ WP-4 Scheduling characterization   (S)  ├─ WP-3 and from 2.1;
   └─ WP-5 Runtime robustness            (S)  ┘  safe in parallel
                    ↓
WP-6 Repo & Doc Hygiene  (XS)            — strictly last (needs the final test count)
```

Note that **WP-2.2 and WP-2.3 do not depend on 2.1 either** — 2.2 touches `LocalModelStorageProvider`/`LocalModelStorageTests`, 2.3 touches `PipelineStageTests`, and neither shares a file with `DecisionEngineTests.cs`. They are grouped into WP-2 by theme, not by sequence.

- **Rationale:** correcting a false rationale is not changing a priority — the order is unchanged. Focus area 3 asks for hidden dependencies and unnecessary serialization; this is both. The current diagram serializes *all* of WP-2 ahead of WP-3, when only Task 2.1 gates it.
- **Expected benefit:** an honest dependency graph, plus four genuinely parallelisable packages instead of three. Meaningful if the work is split across agents or sessions.
- **Impact:** three short passages rewritten (net ~0), diagram grows ~+6 lines. No task bodies change.

---

### E5 — Close WP-1's enforcement gap: land it via PR, and name the branch protection as an owner action

**Where:** WP-1 Objective (`:72`), Task 1.1 Steps 4–5 (`:262-279`).

**The gap.** WP-1's objective is *"make green a fact the repository **enforces**, not a claim someone made locally."* The workflow as written *measures*; nothing enforces it. Two consequences:

1. Step 4 pushes straight to `dev`, and Step 5 anticipates the first run may be **red** because of the very tests WP-2 fixes — then instructs *"note it and proceed to WP-2"*. That leaves `dev` red for 1–2 days while WP-2 and WP-3 land. A CI signal that is normally red is worse than none: every "still green" claim WP-1 exists to underwrite becomes unreadable, and the plan's own Verification step 3 (`newest run 'completed success'`) has nothing to compare against.
2. Nothing prevents a later merge that fails CI.

**Recommended patch.**

- Change Step 4 to branch + PR: `git checkout -b ci/build-test-workflow`, push the branch, open a PR to `dev`. The workflow already carries `on: pull_request`, so the gate runs *before* the merge — converting Step 5's "budget one debugging round" from a post-merge red into a pre-merge iteration on a branch nobody else is reading. Merge only once the PR check is green.
- Add a Step 6, explicitly flagged as an **owner action, not an agent action**: enable *Require status checks to pass* for `build-test` on `dev` and `main` in repository settings. Note it cannot be done from the CLI without admin scope.
- Add to Entry Criteria bullet 1: *"…and `build-test` is a required status check on `dev`."*

- **Rationale:** completes WP-1's stated objective rather than expanding it. The distinction between a workflow that runs and one that gates is the whole point of the package.
- **Expected benefit:** `dev` never sits red; every subsequent WP merges under a real gate; Verification step 3 becomes meaningful.
- **Impact:** ~+6 lines. Consistent with repo convention (PR #49). One owner action to schedule.

---

### E6 — Add two rows to every WP inventory table: **Exit criteria** and **Rollback**

**Where:** all six WP inventory tables (`:68-135`).

**The gap.** The tables carry Objective, Findings covered, Dependencies, Effort, Priority, Reasoning — and, unevenly, *Explicitly not in scope*. Missing from every one: a per-package Definition of Done and a rollback story. Both exist in the document, but scattered: exit criteria only in the global Epic 2 Entry Criteria 1600 lines away, rollback only in WP-6.3's timebox.

**Recommended patch — cross-reference, do not duplicate.** Two one-line rows per table:

| WP | **Exit criteria** | **Rollback** |
|---|---|---|
| WP-1 | Entry Criteria #1; PR check green before merge | Revert the workflow commit / delete `ci.yml`. No code or data touched. |
| WP-2 | Entry Criteria #2, #3 | Revert the three commits. The only production change is one **optional** ctor parameter — revert is clean, DI registration never moved. |
| WP-3 | Entry Criteria #4, #5, #6 | Revert the three commits. **3.1** is read-path-only — no schema, no writes, no data migration. **3.2**'s revert leaves `device-id.txt` on disk. Harmless on any machine whose hostname has not changed since 3.2 landed — seeding makes the file's contents identical to `DeviceHelper.GetId()`. **If the hostname *has* changed, delete `device-id.txt` as part of the revert**: the file then holds the old derived ID while `DeviceHelper.GetId()` returns a new one, so reverting would silently switch identity back on a machine whose rows are already stamped both ways. **3.3** is a property default; reverting re-exposes `SQLite Error 19` on non-UI writes only. |
| WP-4 | Entry Criteria #7 | Delete the test file. Zero production code touched. |
| WP-5 | Manual checks 1 and 2 recorded (see E6b) | Revert one commit per task, independently. |
| WP-6 | Entry Criteria #8, #9 | Revert per task — **except 6.1, which deletes untracked data (see E7)**. |

**E6b — WP-5 has no objective completion criterion at all.** Both its tasks are verified only by the *Manual checks* list at `:1763`, which is not part of the Entry Criteria checklist. Promote both to Entry Criteria as recorded manual checks:

> - [ ] **Deadline toast fires once, not once per tick** (WP-5.1). Manual. Verified by: ______ on ______.
> - [ ] **`capacity.txt` contains a dot-decimal value that survives a restart** (WP-5.2). Manual. Verified by: ______ on ______.

- **Rationale:** focus areas 2, 5 and 7 in one edit. A manual check with no recorded verifier is not a Definition of Done.
- **Expected benefit:** each package becomes independently completable, revertible and auditable — which is what makes the parallelism in E4 safe to actually use.
- **Impact:** +12 lines (2 rows × 6 tables) plus 2 Entry Criteria lines. No new work, no new detail.

---

### E7 — WP-6.1 is the plan's only irreversible step; make it verify-then-delete

**Where:** WP-6.1 Steps 2–4 (`:1484-1520`).

**The exposure.** `rm -rf Assets` destroys **untracked** data. Git cannot restore it. Steps 2–3 copy an enumerated list (`icon.svg`, `favicon.svg`, `Icon Preview.html`, `png/*.png`) and Step 1 md5-checks `icon.ico` — but nothing asserts the enumeration is *complete*. Anything in root `Assets/` not on the list is gone permanently.

I verified the current contents: exactly the 8 files the plan enumerates, all covered. **So the risk today is low and the plan is not wrong** — the recommendation is to make the step assert that rather than assume it, because the plan may execute days after it was written.

**Recommended patch.** Insert between Steps 3 and 4:

> - [ ] **Step 3b: Assert the copy is complete before deleting anything**
>
> ```bash
> diff -r --brief Assets docs/assets/icon-source
> ```
> Expected: differences limited to `icon.ico` (kept only at `SmartStudyPlanner/Assets/`, md5-verified in Step 1) and `README.md` (new). **Any other `Only in Assets/` line means a file would be lost — stop, copy it, re-run.**

- **Rationale:** focus area 5 asks for a rollback strategy per package. WP-6.1 has none available, so the mitigation has to be a pre-flight assertion.
- **Expected benefit:** converts the one unrecoverable action into a checked one, at the cost of one command.
- **Impact:** +6 lines. No change to the outcome.

---

### E8 — Give the document a lifecycle header and a progress table

**Where:** top of the document.

**The gap.** The plan is an ~1800-line execution artifact containing ~600 lines of literal source, targeted at multi-session agentic execution over 4–6 days. Two things a resuming agent cannot currently determine: whether this document is still authoritative, and where execution stopped. Checkboxes are the only state, spread across 1800 lines and 40+ steps.

**Recommended patch.** Add under the title:

```
**Status:** Approved · not started
**Baseline:** fcd4b42 (tree d3deca7) — all line numbers are hints at this commit; see *On line numbers*
**Lifecycle:** Execution artifact, not living documentation. Superseded when WP-6 lands.
**Successor doc:** the stabilization report (to be written) is the durable record.
```

and a progress table immediately after the Execution Order block:

| WP | Status | Commit(s) | Notes |
|---|---|---|---|
| WP-1 | ☐ not started | | |
| WP-2 | ☐ not started | | 2.1 result decides whether WP-3 proceeds |
| … | | | |

- **Rationale:** focus area 4. The main longevity threat to this document is not staleness of any one line — it is a future reader mistaking a spent execution artifact for current architecture documentation, or an agent resuming without knowing what already landed.
- **Expected benefit:** the plan self-declares its own expiry; resumption cost drops to reading one table.
- **Impact:** +15 lines, all at the top.

---

## 4. Nice-to-have improvements

Not required for the plan to execute correctly.

**N1 — Re-label WP-6.3 (dependency bump) as optional, owner opt-in.** It is the only item in the plan that can break something the suite does not cover (the ML pipeline — its own verification requires a manual *Retrain*), it delivers zero Epic 2 benefit, and it is already on the roadmap's deferred ledger. The plan timeboxes it at 30 minutes with a revert, which is good; making it explicitly opt-in matches its actual risk/benefit. *Impact: one word in the task heading, one line in the WP-6 row.*

**N2 — Name WP-5.1's interval change as user-visible.** WP-5's objective is *"stop two latent runtime faults."* Raising the timer from 1 minute to 5 and adding a 30-minute toast cooldown is a third change, and it is one the owner will *notice* — deadline notifications become materially less frequent. Defensible and small, but it belongs in the WP-5 inventory row and in the release note, not folded silently into a robustness fix. *Impact: one clause in two places.*

**N3 — Justify `DeviceIdentity`'s namespace in one sentence.** It is filed under `Services/ML/` (File Structure `:171`) apparently because `DeviceHelper` lives there, but it is sync identity and it is the plan's only new abstraction — Epic 2 builds directly on it. Note that the `Data` → `Services.ML` reference already exists today at the stamping seam, so the plan introduces no new coupling. **The ask is one sentence of justification, not a move**; relocating it mid-stabilization would be exactly the scope creep the plan otherwise avoids. *Impact: one sentence.*

**N4 — `rtk gitnexus detect_changes` in Verification step 5 (`:1757`) is not a valid invocation.** GitNexus is exposed as MCP tools and as `npx gitnexus`; `rtk` proxies shell commands. Replace with the MCP tool call or `npx gitnexus`, consistent with WP-6.2 Step 3 which already uses `npx gitnexus analyze`. *Impact: one line.*

**N5 — Task 2.1's prose says "Six `DateTime.Now` deadline constructions remain" then lists seven line numbers (`:292`).** `:92` is consumed by Step 1, so six is right and the list is inclusive. Subsumed if E3(b) is applied. *Impact: nil once E3 lands.*

**N6 — Note that WP-3.3's `"An toàn"` default adds a fourth site to the "`MucDoCanhBao` derivation duplicated at 3 sites" Category C entry.** Accepted, not a problem — but the Category C row should say four, or the ADR should acknowledge it, so a future reader deduplicating those sites knows the model default is one of them. *Impact: one word.*

---

## 5. What I recommend leaving exactly as it is

- The Category C and D tables. They are the plan's longevity asset and every entry is justified.
- The embedded code blocks. They are verbose, but they are what makes the plan delegable, and E3 makes them the authoritative anchor rather than a liability.
- The Vietnamese inline comments in the proposed code. They match repo convention.
- The 4–6 day estimate and the A/B/C/D classification. Nothing in this review moves either.
- WP-2's exclusion of the 184 repo-wide wall-clock sites, and WP-4's refusal to add a deadline-ordering assertion. Both are correct scope control.

---

## 6. Decisions made

**Decision: report findings as quotable patches, and do not edit the plan.**
*Why:* The brief asked for recommendations, and several findings (E1, E2) are concrete enough to be tempting to just apply. Applying them would have merged the review into the artifact under review and removed the owner's decision point.
*What for:* So each refinement can be accepted, modified or rejected individually.
*Experience:* Every essential item is expressed as "replace X with Y" precisely so accepting it is mechanical.

**Decision: state a diff budget (≈ +45 lines on 1843) in the review itself.**
*Why:* The brief's constraint is "more robust, not larger", and a review of a 1800-line document naturally produces recommendations that grow it.
*What for:* So the owner has a number to check a revision against, rather than a feeling.
*Experience:* Two of the eight essential items (E3, E4) are net-neutral or net-negative. Robustness and length are not the same axis — E3 makes the plan both shorter and more durable by replacing a six-row line table with one search instruction.

**Decision: correct the WP-2 → WP-3 rationale rather than reorder anything.**
*Why:* The brief forbids changing priorities without strong justification. The order is right; the argument given for it is a non-sequitur (a DecisionEngine false green does not make Analytics tests untrustworthy). Fixing a false rationale is not a priority change.
*What for:* So the plan's one declared dependency is defensible, and so the parallelism it obscures (2.2/2.3, WP-4, WP-5 all disjoint from WP-3) becomes visible.
*Experience:* This was the item most at risk of being under-reported as a wording nit. It is not — the plan asserts the same false premise in three places including its ADR section, and it is why the Execution Order diagram serializes more than it needs to.

**Decision: file WP-1's enforcement gap as essential rather than optional.**
*Why:* WP-1's objective is enforcement. A workflow that runs but gates nothing measures. The gap between those two words is the package.
*What for:* So `dev` does not sit red for two days while WP-2 and WP-3 land — which would make every subsequent "still green" claim unreadable, the exact condition WP-1 exists to prevent.
*Experience:* The plan already anticipates the red first run and instructs the executor to proceed past it. That instruction is what turned a nice-to-have into an essential: it converts a known-red gate into normal operating state.

**Decision: verify the root `Assets/` contents before sizing E7.**
*Why:* The instinct was to flag the `rm -rf` on untracked data as a serious unrecoverable risk. A single directory listing showed the plan's enumeration is exactly complete — 8 files, all copied.
*What for:* So the recommendation is "assert completeness before deleting" (one command) rather than "the enumeration may be incomplete" (alarming and, as it happens, false).
*Experience:* Worth doing before writing. The finding survived, at a quarter of the size it would have been.

---

## 7. Summary table

| # | Refinement | Class | Focus area | Net lines |
|---|---|---|---|---|
| E1 | Guard test cannot pass; Entry Criterion unsatisfiable | **Essential** | 7, 2 | +2 |
| E2 | WP-3 "either order" contradicts Task 3.3 | **Essential** | 3 | 0 |
| E3 | Re-key edit targets to code text; line-number preamble | **Essential** | 1, 4 | −4 |
| E4 | Correct WP-2→WP-3 rationale; expose parallelism | **Essential** | 3 | +6 |
| E5 | WP-1 via PR + required status check | **Essential** | 5, 7 | +6 |
| E6 | Per-WP Exit criteria + Rollback rows; WP-5 DoD | **Essential** | 2, 5, 7 | +14 |
| E7 | Verify-then-delete on root `Assets/` | **Essential** | 5 | +6 |
| E8 | Lifecycle header + progress table | **Essential** | 4 | +15 |
| N1 | WP-6.3 → optional, owner opt-in | Nice-to-have | 6 | +1 |
| N2 | Name WP-5.1's interval change as user-visible | Nice-to-have | 6 | +2 |
| N3 | Justify `DeviceIdentity` namespace (do not move it) | Nice-to-have | 4 | +1 |
| N4 | Fix `rtk gitnexus detect_changes` invocation | Nice-to-have | — | 0 |
| N5 | "Six" vs seven line numbers | Nice-to-have | 1 | 0 |
| N6 | `MucDoCanhBao` duplication count 3 → 4 | Nice-to-have | 6 | 0 |
| | **Essential total** | | | **≈ +45** |
