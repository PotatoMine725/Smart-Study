# Epic 1 Release Gate & Transition Plan

**Status:** Architecture Frozen ✅ | Master Plan Frozen ✅ | Epic 1 Code Complete ✅ | Release Gate Pending ⏳

This document defines the exact execution order before opening the next implementation cycle.

---

# Guiding Principle

Epic 1 is **not considered complete** until it passes a real-world release validation.

No new implementation work (Epic 3 / Epic 2) should begin before this release gate finishes successfully.

The remaining work is **release engineering**, not feature development.

---

# Phase 1 — Agent Responsibilities

These tasks should be completed entirely by the implementation/review agents.

---

## Task A1 — WAL-safe Backup Fix (C3a)

Priority: CRITICAL

Implement the backup hardening discussed during the Epic 1 closure review.

Requirements:

- Execute `PRAGMA wal_checkpoint(TRUNCATE)` immediately before backup.
- Or migrate to SQLite's native backup API if appropriate.
- Ensure backup always contains committed WAL data.
- Preserve current backup behavior.

Deliverables:

- Code implementation
- Unit/integration tests
- Live WAL fixture test
- Code review
- Merge approval

Acceptance:

- Backup is lossless even when WAL contains pending pages.
- Existing backup tests remain green.
- New WAL-specific regression test passes.

---

## Task A2 — Documentation Synchronization

Synchronize all documentation with the current project state.

Update:

- Master Plan progress
- Epic status
- Roadmap
- CHANGELOG
- Architecture references
- Decision records
- Lessons Learned references
- Cross-document links

Goal:

Every document should reflect the same project state.

No document should contain stale implementation status.

---

## Task A3 — Knowledge Distillation

Create a long-term engineering knowledge base.

New folder:

docs/knowledge/

Suggested structure:

docs/knowledge/
    architecture/
    implementation/
    reviews/
    releases/
    engineering/

The goal is NOT to archive reports.

The goal is to distill engineering knowledge.

---

### Distill from:

- milestone reviews
- epic reviews
- implementation reports
- refinement reports
- architecture reviews
- lessons learned
- release verdicts

---

Each knowledge article should answer:

- What problem was discovered?
- Why was it difficult?
- What assumptions were wrong?
- How was it solved?
- What engineering principle emerged?
- How should future contributors avoid repeating it?

Examples:

- Deadline ownership
- Hard Constraint vs Objective
- Relative feasibility
- WAL backup lesson
- Migration safety
- Review methodology
- Architecture freeze process
- Sync metadata rationale
- Constraint ownership
- Release engineering lessons

Avoid copying reports verbatim.

Instead, transform temporary discussions into permanent engineering knowledge.

Reports remain historical records.

Knowledge documents become timeless references.

---

## Task A4 — Documentation Consistency Audit

Perform one final documentation audit.

Verify:

- no stale roadmap entries
- no outdated architecture wording
- no duplicated decisions
- all accepted decisions have one authoritative source
- cross-references remain valid
- knowledge documents link correctly
- README indexes the new knowledge section

No architectural changes.

Documentation only.

---

# Phase 2 — Owner Responsibilities

These steps should be performed manually by the project owner.

They intentionally should NOT be delegated to AI.

---

## Task B1 — Supervised First Launch

This is the first real migration of an organically grown development database.

Procedure:

1. Verify backup location.
2. Launch the application.
3. Observe migration.
4. Confirm timestamped backup creation.
5. Confirm application starts successfully.

---

## Task B2 — Database Verification

Immediately verify:

- Rev columns exist
- Sync metadata exists
- Row counts match reference
- No missing data
- No duplicated entities
- Database opens normally

Record results.

---

## Task B3 — GUI Smoke Test

Run only the scenarios affected by Epic 1.

Examples:

- Duplicate subject warning
- Delete task → restart
- Semester save repeatedly
- Analytics dropdown
- Analytics filter
- Focus session completion

Follow:

docs/ux_quality_gate_checklist.md

Record observations.

Do not perform exploratory testing.

---

## Task B4 — Release Decision

Based on:

- Backup
- Migration
- Database verification
- GUI smoke testing

Choose one:

✅ Epic 1 Released

or

❌ Reopen Epic 1

Evidence must drive the decision.

---

# Phase 3 — Agent Closeout

If Epic 1 is released successfully:

---

## Task C1 — Closing Documentation

Produce:

- Epic Closing Note
- Success Metrics
- Final Release Report
- CHANGELOG update

Mark Epic 1 as Released.

---

## Task C2 — Archive Temporary Artifacts

Archive:

- temporary reports
- implementation logs
- review notes

Keep:

- distilled knowledge
- architecture decisions
- closing reports

---

## Task C3 — Prepare Next Epic

Only after Epic 1 is officially released:

- update roadmap
- mark Epic 1 complete
- activate next Epic
- prepare execution contract
- generate implementation plan
- stop before writing code

Implementation begins only after owner approval.

---

# Execution Order

Architecture Freeze
↓

Master Plan Freeze
↓

Agent
→ WAL Backup Fix
↓

Agent
→ Documentation Sync
↓

Agent
→ Knowledge Distillation
↓

Agent
→ Documentation Audit
↓

Owner
→ First Real Launch
↓

Owner
→ Database Verification
↓

Owner
→ GUI Smoke Test
↓

Owner
→ Release Decision
↓

Agent
→ Closing Documentation
↓

Agent
→ Prepare Next Epic
↓

Epic 1 Released
↓

Open Next Implementation Cycle

---

# Execution Rules

The following activities are prohibited until Epic 1 is released:

- Opening a new Epic
- Feature development
- Architecture changes
- Planner redesign
- SOE implementation
- Sync redesign
- ML redesign

Only release-related work is allowed.

---

# Success Criteria

Epic 1 is considered complete only when:

- Backup path verified
- Real migration succeeds
- Database integrity verified
- GUI smoke tests pass
- Documentation synchronized
- Knowledge distilled
- Closing report completed
- Owner explicitly signs the release

Only then may the next implementation cycle begin.